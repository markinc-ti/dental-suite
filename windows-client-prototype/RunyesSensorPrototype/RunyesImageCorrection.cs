using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RunyesSensorPrototype
{
    /// <summary>
    /// Corrección de imagen cruda del sensor ("flat-field correction"):
    /// normaliza con los mapas de ganancia y offset propios de CADA
    /// sensor físico, repara los píxeles marcados como defectuosos de
    /// fábrica (Pixel_Map), y limpia píxeles atípicos residuales (hot
    /// pixels dinámicos) que no están en el mapa fijo.
    ///
    /// Esto reemplaza la necesidad de llamar a Scilib21.dll /
    /// ImageCorrect.dll directamente (nunca se confirmó una firma que
    /// funcionara): implementamos el mismo algoritmo estándar de la
    /// industria para sensores CMOS de rayos X, verificado visualmente
    /// contra una captura real (la textura de "tejido/estática" del raw
    /// sin corregir desaparece por completo tras aplicar esto).
    ///
    /// Todos los archivos de calibración (crudo del sensor y los mapas
    /// Gain/Offset) usan el mismo formato: enteros de 16 bits,
    /// BIG-ENDIAN. Confirmado en vivo.
    /// </summary>
    public sealed class RunyesCalibrationSet
    {
        public string Serial { get; }
        public int Width { get; }
        public int Height { get; }
        public double[] Gain { get; }     // fila-mayor, largo Width*Height
        public double[] Offset { get; }
        public bool[] DefectMask { get; }

        private RunyesCalibrationSet(string serial, int width, int height,
            double[] gain, double[] offset, bool[] defectMask)
        {
            Serial = serial;
            Width = width;
            Height = height;
            Gain = gain;
            Offset = offset;
            DefectMask = defectMask;
        }

        public static RunyesCalibrationSet Load(string calibDir, string serial, int width, int height)
        {
            var gainPaths = FindCalibVariants(calibDir, serial, "Gain_Image");
            var offsetPaths = FindCalibVariants(calibDir, serial, "Offset_Image");
            string? pixelMapPath = FindSingleCalibFile(calibDir, serial, "Pixel_Map");

            if (gainPaths.Count == 0)
                throw new FileNotFoundException($"No hay Gain_Image para el sensor {serial} en {calibDir}");
            if (offsetPaths.Count == 0)
                throw new FileNotFoundException($"No hay Offset_Image para el sensor {serial} en {calibDir}");
            if (pixelMapPath == null)
                throw new FileNotFoundException($"No hay Pixel_Map para el sensor {serial} en {calibDir}");

            int n = width * height;
            double[] gain = AverageBigEndian16(gainPaths, n);
            double[] offset = AverageBigEndian16(offsetPaths, n);

            bool[] mask = new bool[n];
            foreach (var (x, y) in ReadPixelMap(pixelMapPath))
            {
                int xi = x - 1, yi = y - 1;
                if (xi >= 0 && xi < width && yi >= 0 && yi < height)
                    mask[yi * width + xi] = true;
            }

            return new RunyesCalibrationSet(serial, width, height, gain, offset, mask);
        }

        private static List<string> FindCalibVariants(string calibDir, string serial, string kind)
        {
            // Tolerante a que alguien haya guardado los archivos con
            // espacios en vez de guiones bajos (pasa seguido al
            // descargarlos/copiarlos manualmente) -- se compara
            // normalizando espacios y guiones bajos como si fueran lo
            // mismo, sin distinguir mayúsculas/minúsculas.
            if (!Directory.Exists(calibDir))
                return new List<string>();

            string Normalize(string s) => s.Replace(' ', '_').Replace('-', '_').ToLowerInvariant();

            string target0 = Normalize($"{serial}_{kind}0");
            string target1 = Normalize($"{serial}_{kind}1");
            string targetSingle = Normalize($"{serial}_{kind}");

            string? found0 = null, found1 = null, foundSingle = null;
            foreach (var file in Directory.GetFiles(calibDir))
            {
                string name = Normalize(Path.GetFileName(file));
                if (name == target0) found0 = file;
                else if (name == target1) found1 = file;
                else if (name == targetSingle) foundSingle = file;
            }

            if (found0 != null && found1 != null)
                return new List<string> { found0, found1 };
            if (foundSingle != null)
                return new List<string> { foundSingle };
            return new List<string>();
        }

        private static string? FindSingleCalibFile(string calibDir, string serial, string kind)
        {
            if (!Directory.Exists(calibDir))
                return null;

            string Normalize(string s) => s.Replace(' ', '_').Replace('-', '_').ToLowerInvariant();
            string target = Normalize($"{serial}_{kind}");

            foreach (var file in Directory.GetFiles(calibDir))
            {
                if (Normalize(Path.GetFileName(file)) == target)
                    return file;
            }
            return null;
        }

        private static double[] AverageBigEndian16(List<string> paths, int count)
        {
            var sum = new double[count];
            foreach (var path in paths)
            {
                var values = ReadBigEndian16(path, count);
                for (int i = 0; i < count; i++)
                    sum[i] += values[i];
            }
            for (int i = 0; i < count; i++)
                sum[i] /= paths.Count;
            return sum;
        }

        internal static ushort[] ReadBigEndian16(string path, int count)
        {
            byte[] raw = File.ReadAllBytes(path);
            var values = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                int b0 = raw[i * 2];
                int b1 = raw[i * 2 + 1];
                values[i] = (ushort)((b0 << 8) | b1); // big-endian
            }
            return values;
        }

        private static IEnumerable<(int x, int y)> ReadPixelMap(string path)
        {
            var regex = new Regex(@"^P(\d+),(\d+)$");
            foreach (var line in File.ReadLines(path))
            {
                var trimmed = line.Trim();
                var match = regex.Match(trimmed);
                if (match.Success)
                {
                    yield return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
                }
            }
        }
    }

    public static class RunyesImageCorrection
    {
        /// <summary>
        /// Aplica corrección de ganancia/offset + reparación de píxeles
        /// defectuosos + limpieza de atípicos residuales sobre los bytes
        /// crudos (big-endian, 16 bits) que entrega Sensor_LoadRawImage.
        ///
        /// Regresa un arreglo double (fila-mayor) -- todavía SIN
        /// cuantizar a 16 bits para mostrar; usa ToGray16Bytes() para eso.
        /// </summary>
        public static double[] CorrectRawImage(byte[] rawBytes, RunyesCalibrationSet calib,
            double despeckleThreshold = 3.0, int despecklePasses = 1,
            int localContrastRadius = 40, double localContrastAmount = 0.6)
        {
            int n = calib.Width * calib.Height;
            var raw = new double[n];
            for (int i = 0; i < n; i++)
            {
                int b0 = rawBytes[i * 2];
                int b1 = rawBytes[i * 2 + 1];
                raw[i] = (b0 << 8) | b1; // big-endian
            }

            var denom = new double[n];
            double denomSum = 0;
            for (int i = 0; i < n; i++)
            {
                double d = calib.Gain[i] - calib.Offset[i];
                denom[i] = d <= 0 ? 1.0 : d;
                denomSum += denom[i];
            }
            double k = denomSum / n;

            var corrected = new double[n];
            for (int i = 0; i < n; i++)
                corrected[i] = (raw[i] - calib.Offset[i]) / denom[i] * k;

            corrected = FixDefects(corrected, calib.DefectMask, calib.Width, calib.Height);

            for (int pass = 0; pass < despecklePasses; pass++)
                corrected = Despeckle(corrected, calib.Width, calib.Height, despeckleThreshold);

            // Realce de contraste LOCAL: el estiramiento de contraste
            // normal (ToGray16Bytes) es GLOBAL -- mira toda la imagen de
            // un jalón. Cuando el fondo abierto domina el histograma
            // (caso típico en radiografías dentales), ese estiramiento
            // global le da la mayor parte de los niveles de gris al
            // fondo, dejando muy pocos niveles distintos para la zona
            // del diente/raíz -- se ve "plana" ahí aunque los datos
            // reales sí tengan variación. Esto compara cada píxel contra
            // el promedio de su vecindad amplia (no toda la imagen) y
            // resalta esa diferencia local, revelando textura fina
            // (trabéculas óseas, contorno de raíces) sin importar en qué
            // parte del histograma global caiga esa zona.
            if (localContrastAmount > 0)
                corrected = EnhanceLocalContrast(corrected, calib.Width, calib.Height,
                    localContrastRadius, localContrastAmount);

            return corrected;
        }

        /// <summary>Realce de contraste local ("clarity"): resta un
        /// promedio local amplio (radio grande) y lo vuelve a sumar
        /// amplificado, resaltando variaciones de mediana escala sin
        /// amplificar tanto el ruido de un solo píxel como el afilado
        /// de radio chico.</summary>
        private static double[] EnhanceLocalContrast(double[] arr, int width, int height, int radius, double amount)
        {
            var localMean = BoxBlur(arr, width, height, radius);
            var result = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                result[i] = arr[i] + amount * (arr[i] - localMean[i]);
            return result;
        }

        /// <summary>Blur de caja usando sumas acumuladas (rápido incluso con
        /// radios grandes, igual que el usado en el visor).</summary>
        private static double[] BoxBlur(double[] arr, int width, int height, int radius)
        {
            if (radius < 1) return (double[])arr.Clone();

            var horizontal = new double[width * height];
            for (int y = 0; y < height; y++)
            {
                double sum = 0;
                for (int x = -radius; x <= radius; x++)
                {
                    int xi = Math.Clamp(x, 0, width - 1);
                    sum += arr[y * width + xi];
                }
                for (int x = 0; x < width; x++)
                {
                    horizontal[y * width + x] = sum / (2 * radius + 1);
                    int addX = Math.Clamp(x + radius + 1, 0, width - 1);
                    int removeX = Math.Clamp(x - radius, 0, width - 1);
                    sum += arr[y * width + addX] - arr[y * width + removeX];
                }
            }

            var result = new double[width * height];
            for (int x = 0; x < width; x++)
            {
                double sum = 0;
                for (int y = -radius; y <= radius; y++)
                {
                    int yi = Math.Clamp(y, 0, height - 1);
                    sum += horizontal[yi * width + x];
                }
                for (int y = 0; y < height; y++)
                {
                    result[y * width + x] = sum / (2 * radius + 1);
                    int addY = Math.Clamp(y + radius + 1, 0, height - 1);
                    int removeY = Math.Clamp(y - radius, 0, height - 1);
                    sum += horizontal[addY * width + x] - horizontal[removeY * width + x];
                }
            }

            return result;
        }

        private static double[] FixDefects(double[] arr, bool[] mask, int width, int height)
        {
            bool anyDefect = false;
            foreach (var m in mask) if (m) { anyDefect = true; break; }
            if (!anyDefect) return arr;

            var result = (double[])arr.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (!mask[idx]) continue;

                    double sum = 0;
                    int valid = 0;
                    foreach (var (dx, dy) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int nidx = ny * width + nx;
                        if (mask[nidx]) continue;
                        sum += arr[nidx];
                        valid++;
                    }
                    if (valid > 0)
                        result[idx] = sum / valid;
                }
            }
            return result;
        }

        /// <summary>Elimina píxeles atípicos ('hot pixels' dinámicos) comparando
        /// cada píxel contra la mediana de su vecindad 3x3.</summary>
        private static double[] Despeckle(double[] arr, int width, int height, double threshold)
        {
            var result = (double[])arr.Clone();
            var neighborhood = new double[9];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int k = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = Math.Min(Math.Max(x + dx, 0), width - 1);
                            int ny = Math.Min(Math.Max(y + dy, 0), height - 1);
                            neighborhood[k++] = arr[ny * width + nx];
                        }
                    }
                    Array.Sort(neighborhood);
                    double median = neighborhood[4];

                    var deviations = new double[9];
                    for (int i = 0; i < 9; i++)
                        deviations[i] = Math.Abs(neighborhood[i] - median);
                    Array.Sort(deviations);
                    double mad = deviations[4];
                    double madSafe = mad < 1 ? 1 : mad;

                    int idx = y * width + x;
                    if (Math.Abs(arr[idx] - median) > threshold * madSafe)
                        result[idx] = median;
                }
            }
            return result;
        }

        /// <summary>
        /// Cuantiza los valores corregidos (double) a UInt16, listos para
        /// PixelFormats.Gray16 de WPF (que espera orden NATIVO/little-endian
        /// en Windows -- distinto del big-endian que usa el sensor).
        /// Usa estiramiento por percentiles para aprovechar el rango
        /// completo de 16 bits sin que unos pocos valores extremos
        /// aplasten el resto de la imagen, dejando margen para que el
        /// editor (contraste/brillo) siga ajustando después.
        /// </summary>
        public static byte[] ToGray16Bytes(double[] corrected, double pLow = 0.1, double pHigh = 99.9)
        {
            var sorted = (double[])corrected.Clone();
            Array.Sort(sorted);
            int n = sorted.Length;
            double lo = sorted[(int)(n * pLow / 100)];
            double hi = sorted[Math.Min(n - 1, (int)(n * pHigh / 100))];
            if (hi <= lo) hi = lo + 1;
            double span = hi - lo;

            var bytes = new byte[n * 2];
            for (int i = 0; i < n; i++)
            {
                double scaled = (corrected[i] - lo) / span * 65535.0;
                ushort value = (ushort)Math.Clamp(scaled, 0, 65535);
                var b = BitConverter.GetBytes(value); // little-endian nativo en x86/x64
                bytes[i * 2] = b[0];
                bytes[i * 2 + 1] = b[1];
            }
            return bytes;
        }
    }
}
