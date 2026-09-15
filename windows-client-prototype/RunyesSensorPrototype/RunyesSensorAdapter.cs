using System;
using System.IO;
using System.Linq;

namespace RunyesSensorPrototype
{
    /// <summary>
    /// Primer adaptador de sensor: Runyes, usando sensor_io.dll +
    /// ImageCorrect.dll extraídos del instalador oficial.
    ///
    /// Agregar otro fabricante después (Dabi Atlante, etc.) significa
    /// crear una clase nueva que implemente IDentalSensor, sin tocar
    /// esta ni el resto de la aplicación.
    /// </summary>
    public sealed class RunyesSensorAdapter : IDentalSensor
    {
        private IntPtr _deviceHandle = IntPtr.Zero;
        private int _imageWidth;
        private int _imageHeight;

        public string SensorName => "Runyes";

        public int GetDeviceCount()
        {
            return RunyesSensorInterop.Sensor_DeviceCount();
        }

        public bool Connect(int deviceIndex = 0)
        {
            _deviceHandle = RunyesSensorInterop.Sensor_OpenDevice(deviceIndex);
            if (_deviceHandle != IntPtr.Zero)
            {
                DumpDeviceInfoForDiagnostics();
                ReadImageDimensionsFromDeviceInfo();
            }
            return _deviceHandle != IntPtr.Zero;
        }

        /// <summary>
        /// Lee Sensor_GetDeviceInfo y extrae ancho/alto reales del sensor.
        /// Confirmado con datos reales del hardware: la estructura trae,
        /// en little-endian, un uint16 de ancho en el offset 2 y un
        /// uint16 de alto en el offset 4 (seguidos del número de serie en
        /// texto ASCII) — Sensor_GetImageSize, en cambio, no devuelve
        /// nada útil, así que ya no se usa para esto.
        /// </summary>
        private void ReadImageDimensionsFromDeviceInfo()
        {
            const int bufferSize = 256;
            var buffer = new byte[bufferSize];
            int result = RunyesSensorInterop.Sensor_GetDeviceInfo(_deviceHandle, buffer, bufferSize);
            if (result != 1)
                throw new InvalidOperationException(
                    $"Sensor_GetDeviceInfo devolvió {result} (se esperaba 1).");

            _imageWidth = BitConverter.ToUInt16(buffer, 2);
            _imageHeight = BitConverter.ToUInt16(buffer, 4);

            if (_imageWidth <= 0 || _imageHeight <= 0)
                throw new InvalidOperationException(
                    $"Dimensiones inválidas leídas de Sensor_GetDeviceInfo "
                    + $"(width={_imageWidth}, height={_imageHeight}).");
        }

        /// <summary>
        /// Vuelca los bytes crudos de Sensor_GetDeviceInfo a un archivo de
        /// texto, para poder inspeccionar a mano dónde vienen el ancho y
        /// alto reales del sensor. Ya resuelto (ver ReadImageDimensionsFromDeviceInfo)
        /// — se deja este volcado por si hace falta seguir investigando
        /// otros campos de la estructura (número de serie, versión, etc.).
        /// </summary>
        private void DumpDeviceInfoForDiagnostics()
        {
            try
            {
                const int bufferSize = 256;
                var buffer = new byte[bufferSize];
                int result = RunyesSensorInterop.Sensor_GetDeviceInfo(_deviceHandle, buffer, bufferSize);

                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DentalSuite");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "sensor-diagnostic.log");

                using var writer = new StreamWriter(logPath, append: false);
                writer.WriteLine($"Sensor_GetDeviceInfo devolvió: {result}");
                writer.WriteLine();
                writer.WriteLine("Bytes crudos (hex):");
                for (int i = 0; i < bufferSize; i += 16)
                {
                    var chunk = buffer.Skip(i).Take(16).Select(b => b.ToString("X2"));
                    writer.WriteLine($"{i:D4}: {string.Join(" ", chunk)}");
                }

                writer.WriteLine();
                writer.WriteLine("Posibles valores de ancho/alto (leyendo cada 4 bytes como entero, "
                    + "little-endian, mostrando solo los que caen en un rango plausible 100-6000):");
                for (int i = 0; i <= bufferSize - 4; i += 4)
                {
                    int value = BitConverter.ToInt32(buffer, i);
                    if (value >= 100 && value <= 6000)
                    {
                        writer.WriteLine($"  offset {i:D4}: {value}");
                    }
                }
            }
            catch
            {
                // Es un diagnóstico best-effort — si falla, no debe tumbar la conexión.
            }
        }

        public void Disconnect()
        {
            if (_deviceHandle != IntPtr.Zero)
            {
                RunyesSensorInterop.Sensor_CloseDevice(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
            }
        }

        public CapturedImage CaptureImage()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("El sensor no está conectado.");

            int width = _imageWidth;
            int height = _imageHeight;

            // Espera activa a que el sensor detecte el disparo de rayos X.
            // (Sensor_GetXrayState: 0 = esperando, != 0 = expuesto — a
            // confirmar el valor exacto contra el sensor real.) Con límite
            // de tiempo: si esta función también tiene una firma que no
            // coincide con el SDK real, es mejor un error claro que una
            // espera infinita silenciosa.
            var xrayTimeout = TimeSpan.FromSeconds(30);
            var waitStart = DateTime.UtcNow;
            while (RunyesSensorInterop.Sensor_GetXrayState(_deviceHandle) == 0)
            {
                if (DateTime.UtcNow - waitStart > xrayTimeout)
                    throw new InvalidOperationException(
                        "Pasaron 30 segundos sin detectar el disparo de rayos X. "
                        + "Si sí disparaste el sensor, es posible que "
                        + "Sensor_GetXrayState tampoco tenga la firma correcta.");
                System.Threading.Thread.Sleep(50);
            }

            var rawBuffer = new byte[width * height * 2]; // 16 bits por píxel típico en RVG

            // Revertido: quitar el parámetro del buffer desalineó la pila
            // (StdCall) y terminó tumbando el programa entero — un error
            // mucho más caro que uno manejable. Volvemos a la firma seria
            // con buffer, y tratamos el valor de retorno grande
            // (513671180 en la prueba anterior) simplemente como un
            // código que todavía no entendemos, sin asumir qué es.
            int readResult = RunyesSensorInterop.Sensor_GetImageBits(
                _deviceHandle, rawBuffer, rawBuffer.Length);

            bool looksLikeSuccess = readResult == 0 || readResult == 1;
            if (!looksLikeSuccess)
            {
                int errorCode = RunyesSensorInterop.Sensor_GetLastError(_deviceHandle);
                string errorText = "(sin texto disponible)";
                try
                {
                    IntPtr errorTextPtr = RunyesSensorInterop.Sensor_GetLastErrorText(_deviceHandle);
                    if (errorTextPtr != IntPtr.Zero)
                    {
                        errorText = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(errorTextPtr)
                            ?? errorText;
                    }
                }
                catch
                {
                    // Si ni esto se puede leer, seguimos con el código numérico solo.
                }

                throw new InvalidOperationException(
                    $"Sensor_GetImageBits devolvió {readResult} (0x{readResult:X8}), "
                    + $"que no es ni 0 ni 1. código de error={errorCode}: {errorText}.");
            }

            var correctedBuffer = new byte[rawBuffer.Length];
            RunyesSensorInterop.Sensor_CorrectImage(
                rawBuffer, correctedBuffer, width, height, IntPtr.Zero);

            return new CapturedImage
            {
                Width = width,
                Height = height,
                PixelData = correctedBuffer,
            };
        }

        public void Dispose() => Disconnect();
    }
}
