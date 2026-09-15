using System;
using System.IO;
using WoodpeckerSensorPrototype;

namespace WoodpeckerSensorTestConsole
{
    /// <summary>
    /// Prototipo mínimo para validar la conexión con el sensor
    /// Woodpecker/DBA (modelo E4S, vía FpdSys.dll) antes de integrarlo
    /// al cliente completo. Ejecutar en una PC Windows con el sensor
    /// conectado y FpdSys.dll (junto con DetFinder.dll, E4S.dll,
    /// ConnUSBfifo.dll y sus dependencias) copiado a la carpeta del
    /// ejecutable.
    /// </summary>
    internal static class Program
    {
        private static void Main()
        {
            var sensor = new FpdSysSensorAdapter();

            Console.WriteLine($"Dispositivos detectados: {sensor.GetDeviceCount()}");
            Console.WriteLine($"Conectando con {sensor.SensorName}...");
            if (!sensor.Connect(0))
            {
                Console.WriteLine("No se pudo conectar. Verifica el cable USB.");
                return;
            }

            Console.WriteLine("Conectado. Esperando disparo de rayos X...");
            try
            {
                var image = sensor.CaptureImage();
                Console.WriteLine(
                    $"Imagen capturada: {image.Width}x{image.Height}, "
                    + $"{image.PixelData.Length} bytes.");

                string outputPath = Path.Combine(
                    AppContext.BaseDirectory, "captura_woodpecker.raw");
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
