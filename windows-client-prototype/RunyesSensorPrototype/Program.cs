using System;
using System.IO;

namespace RunyesSensorPrototype
{
    /// <summary>
    /// Prototipo mínimo para validar la conexión con el sensor Runyes
    /// antes de integrarlo al cliente completo. Ejecutar en una PC
    /// Windows con el sensor conectado por USB y sensor_io.dll /
    /// ImageCorrect.dll copiados junto al ejecutable (se encuentran en
    /// la carpeta "Sensor" del instalador de Runyes).
    /// </summary>
    internal static class Program
    {
        private static void Main()
        {
            IDentalSensor sensor = new RunyesSensorAdapter();

            Console.WriteLine($"Buscando dispositivos {sensor.SensorName}...");
            int count = sensor.GetDeviceCount();
            Console.WriteLine($"Dispositivos encontrados: {count}");

            if (count == 0)
            {
                Console.WriteLine("No se detectó ningún sensor. Verifica la conexión USB.");
                return;
            }

            if (!sensor.Connect(0))
            {
                Console.WriteLine("No se pudo abrir el dispositivo.");
                return;
            }

            Console.WriteLine("Sensor conectado. Esperando disparo de rayos X...");

            try
            {
                var image = sensor.CaptureImage();
                Console.WriteLine(
                    $"Imagen capturada: {image.Width}x{image.Height}, "
                    + $"{image.PixelData.Length} bytes.");

                string outputPath = Path.Combine(
                    AppContext.BaseDirectory, "captura_runyes.raw");
                File.WriteAllBytes(outputPath, image.PixelData);
                Console.WriteLine($"Guardado en: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error durante la captura: {ex.Message}");
            }
            finally
            {
                sensor.Disconnect();
            }
        }
    }
}
