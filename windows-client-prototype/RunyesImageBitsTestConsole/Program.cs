using System;
using System.IO;
using System.Linq;
using RunyesSensorPrototype;

// ---------------------------------------------------------------------
// Diagnóstico completo: captura los bytes CRUDOS (sin corrección),
// imprime estadísticas (igual que hicimos con Python cuando cazamos el
// bug de la llamada duplicada a Sensor_GetImageBits), guarda una vista
// previa del raw, luego aplica la corrección sobre esos MISMOS bytes
// (sin pedir un segundo disparo) y guarda también esa vista previa —
// así se puede comparar raw vs. corregida y ver en qué etapa aparece
// un problema, si lo hay.
// ---------------------------------------------------------------------

Console.WriteLine("=== Diagnóstico completo: captura + corrección ===");
Console.WriteLine($"Carpeta de calibración: {RunyesCalibrationConfig.DefaultCalibrationDir}");
RunyesCalibrationConfig.EnsureDirectoryExists();
Console.WriteLine();

using var sensor = new RunyesSensorAdapter();

int deviceCount = sensor.GetDeviceCount();
Console.WriteLine($"Sensores detectados: {deviceCount}");
if (deviceCount == 0)
{
    Console.WriteLine("No hay sensor conectado. Verifica el cable USB.");
    return;
}

bool connected = sensor.Connect(0);
if (!connected)
{
    Console.WriteLine("No se pudo conectar al sensor.");
    return;
}

Console.WriteLine($"Sensor conectado. Dimensiones reportadas: {sensor.ImageWidth} x {sensor.ImageHeight}");
Console.WriteLine($"Número de serie detectado: {sensor.DetectedSerial ?? "(no se pudo detectar)"}");
Console.WriteLine($"¿Tiene calibración cargada?: {sensor.HasCalibration}");
Console.WriteLine();

Console.WriteLine(">>> Dispara el sensor de rayos X ahora (tienes 60 segundos)...");

byte[] rawBytes;
try
{
    rawBytes = sensor.CaptureRawBytesForDiagnostics(msg => Console.WriteLine($"  [estado] {msg}"));
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    return;
}

Console.WriteLine($"Captura cruda: {rawBytes.Length} bytes");
Console.WriteLine();

// ---- Estadísticas del raw, interpretado como 16 bits big-endian ----
int width = sensor.ImageWidth;
int height = sensor.ImageHeight;
int count = width * height;
var rawValues = new int[count];
for (int i = 0; i < count; i++)
    rawValues[i] = (rawBytes[i * 2] << 8) | rawBytes[i * 2 + 1];

PrintStats("RAW (sin corregir, big-endian 16 bits)", rawValues);

string rawPreviewPath = Path.Combine(Directory.GetCurrentDirectory(), "diagnostico_raw.bmp");
WriteGrayscaleBmp(rawValues, width, height, rawPreviewPath);
Console.WriteLine($"Vista previa RAW guardada en: {rawPreviewPath}");
Console.WriteLine();

// ---- Corrección sobre los MISMOS bytes (sin pedir otro disparo) ----
if (sensor.HasCalibration)
{
    double[] corrected = sensor.CorrectRawBytesForDiagnostics(rawBytes);
    var correctedInts = corrected.Select(v => (int)Math.Round(v)).ToArray();
    PrintStats("CORREGIDA (gain/offset/pixelmap/despeckle)", correctedInts);

    string correctedPreviewPath = Path.Combine(Directory.GetCurrentDirectory(), "diagnostico_corregida.bmp");
    WriteGrayscaleBmp(correctedInts, width, height, correctedPreviewPath);
    Console.WriteLine($"Vista previa CORREGIDA guardada en: {correctedPreviewPath}");
}
else
{
    Console.WriteLine("(Sin calibración cargada -- no se pudo aplicar corrección para comparar.)");
}

Console.WriteLine();
Console.WriteLine("Copia y comparte TODA esta salida de texto, junto con las dos imágenes BMP generadas.");
Console.WriteLine("Presiona una tecla para salir...");
Console.ReadKey();


static void PrintStats(string label, int[] values)
{
    int n = values.Length;
    int min = values.Min();
    int max = values.Max();
    double mean = values.Average();
    int zeros = values.Count(v => v == 0);
    int nearMax = values.Count(v => v >= 0xFFF0);

    Console.WriteLine($"--- {label} ---");
    Console.WriteLine($"  n={n}  min={min}  max={max}  promedio={mean:F1}");
    Console.WriteLine($"  en cero: {zeros} ({100.0 * zeros / n:F1}%)   cerca de saturación (>=0xFFF0): {nearMax} ({100.0 * nearMax / n:F2}%)");

    var sorted = (int[])values.Clone();
    Array.Sort(sorted);
    int bucketSize = Math.Max(1, (max - min + 1) / 10);
    Console.WriteLine("  Distribución (10 rangos):");
    for (int b = 0; b < 10; b++)
    {
        int lo = min + b * bucketSize;
        int hi = (b == 9) ? max : lo + bucketSize - 1;
        int c = values.Count(v => v >= lo && v <= hi);
        double pct = 100.0 * c / n;
        string bar = new string('#', (int)(pct / 2));
        Console.WriteLine($"    {lo,7}-{hi,-7}: {pct,5:F1}%  {bar}");
    }
    Console.WriteLine();
}

static void WriteGrayscaleBmp(int[] values, int width, int height, string path)
{
    int count = width * height;
    var sorted = (int[])values.Clone();
    Array.Sort(sorted);
    int lo = sorted[(int)(count * 0.01)];
    int hi = sorted[Math.Min(count - 1, (int)(count * 0.99))];
    int span = Math.Max(1, hi - lo);

    var pixels8 = new byte[count];
    for (int i = 0; i < count; i++)
    {
        int scaled = (values[i] - lo) * 255 / span;
        pixels8[i] = (byte)Math.Clamp(scaled, 0, 255);
    }

    int rowSize = (width + 3) / 4 * 4;
    int pixelDataSize = rowSize * height;
    int paletteSize = 256 * 4;
    int headerSize = 14 + 40 + paletteSize;
    int fileSize = headerSize + pixelDataSize;

    using var fs = new FileStream(path, FileMode.Create);
    using var bw = new BinaryWriter(fs);

    bw.Write((byte)'B'); bw.Write((byte)'M');
    bw.Write(fileSize); bw.Write((short)0); bw.Write((short)0); bw.Write(headerSize);

    bw.Write(40); bw.Write(width); bw.Write(height);
    bw.Write((short)1); bw.Write((short)8);
    bw.Write(0); bw.Write(pixelDataSize);
    bw.Write(2835); bw.Write(2835); bw.Write(256); bw.Write(0);

    for (int i = 0; i < 256; i++)
    {
        bw.Write((byte)i); bw.Write((byte)i); bw.Write((byte)i); bw.Write((byte)0);
    }

    for (int y = height - 1; y >= 0; y--)
    {
        bw.Write(pixels8, y * width, width);
        bw.Write(new byte[rowSize - width]);
    }
}
