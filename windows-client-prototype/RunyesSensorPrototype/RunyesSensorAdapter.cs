using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace RunyesSensorPrototype
{
    /// <summary>
    /// Adaptador de sensor Runyes, reescrito con el flujo CONFIRMADO en
    /// vivo contra hardware real (ver notas en RunyesSensorInterop.cs):
    ///
    ///   1. Sensor_GetDeviceInfo -> ancho, alto (offsets 2 y 4) y, con
    ///      mejor esfuerzo, el número de serie (para elegir su
    ///      calibración automáticamente).
    ///   2. Poll de Sensor_GetXrayState hasta ver un 1 (disparo).
    ///   3. Sensor_GetImageSize + Sensor_LoadRawImage(handle, buffer)
    ///      para traer los bytes crudos reales.
    ///   4. Corrección propia en software (gain/offset/pixel map +
    ///      despeckle) usando los archivos de calibración del sensor,
    ///      en vez de depender de ImageCorrect.dll (nunca confirmado).
    ///
    /// Agregar otro fabricante después significa crear una clase nueva
    /// que implemente IDentalSensor, sin tocar esta ni el resto de la
    /// aplicación.
    /// </summary>
    public sealed class RunyesSensorAdapter : IDentalSensor
    {
        private IntPtr _deviceHandle = IntPtr.Zero;
        private int _imageWidth;
        private int _imageHeight;
        private string? _serial;
        private RunyesCalibrationSet? _calibration;

        /// <summary>Permite apuntar a otra carpeta de calibración en
        /// pruebas; por defecto usa RunyesCalibrationConfig.DefaultCalibrationDir.</summary>
        public string CalibrationDir { get; set; } = RunyesCalibrationConfig.DefaultCalibrationDir;

        public string SensorName => "Runyes";

        public int GetDeviceCount()
        {
            return RunyesSensorInterop.Sensor_DeviceCount();
        }

        public bool Connect(int deviceIndex = 0)
        {
            _deviceHandle = RunyesSensorInterop.Sensor_OpenDevice(deviceIndex);
            if (_deviceHandle == IntPtr.Zero)
                return false;

            DumpDeviceInfoForDiagnostics();
            ReadImageDimensionsAndSerial();
            LoadCalibrationForCurrentSensor();
            return true;
        }

        /// <summary>
        /// Lee Sensor_GetDeviceInfo y extrae ancho/alto reales del sensor.
        /// Confirmado con datos reales del hardware: la estructura trae,
        /// en little-endian, un uint16 de ancho en el offset 2 y un
        /// uint16 de alto en el offset 4.
        ///
        /// También intenta extraer el número de serie como el primer
        /// bloque de 6 o más dígitos ASCII en el resto del buffer -- esta
        /// parte NO está confirmada con la misma certeza que ancho/alto;
        /// si el número de serie detectado no coincide con el que
        /// muestra el diálogo "Sensor" de Runyes en pantalla, revisa
        /// el log de diagnóstico (sensor-diagnostic.log) y ajusta
        /// ExtractSerialFromDeviceInfo.
        /// </summary>
        private void ReadImageDimensionsAndSerial()
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

            _serial = ExtractSerialFromDeviceInfo(buffer);
        }

        private static string? ExtractSerialFromDeviceInfo(byte[] buffer)
        {
            // Busca ASCII imprimible en el buffer y dentro de eso el
            // primer bloque de 6+ dígitos consecutivos -- suficientemente
            // genérico para no depender de un offset exacto no confirmado.
            string ascii = new string(buffer
                .Select(b => (char)b)
                .Where(c => c >= 0x20 && c < 0x7F)
                .ToArray());

            var match = Regex.Match(ascii, @"\d{6,}");
            return match.Success ? match.Value : null;
        }

        private void LoadCalibrationForCurrentSensor()
        {
            if (_serial == null)
            {
                _calibration = null; // se valida (con error claro) al capturar
                return;
            }

            try
            {
                _calibration = RunyesCalibrationSet.Load(CalibrationDir, _serial, _imageWidth, _imageHeight);
            }
            catch (FileNotFoundException)
            {
                // No hay calibración para este sensor todavía -- se
                // reporta con un mensaje claro al intentar capturar, en
                // vez de fallar aquí silenciosamente durante Connect().
                _calibration = null;
            }
        }

        /// <summary>
        /// Vuelca los bytes crudos de Sensor_GetDeviceInfo a un archivo de
        /// texto, para poder inspeccionar a mano dónde vienen el ancho,
        /// alto y número de serie del sensor si algo no cuadra.
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

        public CapturedImage CaptureImage(Action<string>? onStatus = null, CancellationToken cancellationToken = default)
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("El sensor no está conectado.");

            if (_calibration == null)
            {
                string serialText = _serial ?? "(no detectado)";
                throw new InvalidOperationException(
                    $"No hay archivos de calibración para el sensor {serialText} en "
                    + $"{CalibrationDir}. Copia ahí los archivos "
                    + $"{serialText}_Gain_Image0/1, {serialText}_Offset_Image0/1 y "
                    + $"{serialText}_Pixel_Map que vienen con este sensor.");
            }

            onStatus?.Invoke("Esperando disparo de rayos X...");

            // Espera activa a que el sensor detecte el disparo de rayos X.
            // CONFIRMADO en vivo: 0 = reposo, 1 = disparo (dura muy poco).
            var xrayTimeout = TimeSpan.FromSeconds(60);
            var waitStart = DateTime.UtcNow;
            while (RunyesSensorInterop.Sensor_GetXrayState(_deviceHandle) != 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow - waitStart > xrayTimeout)
                    throw new InvalidOperationException(
                        "Pasaron 60 segundos sin detectar el disparo de rayos X.");
                System.Threading.Thread.Sleep(20);
            }

            onStatus?.Invoke("Transfiriendo imagen...");

            int size = RunyesSensorInterop.Sensor_GetImageSize(_deviceHandle);
            if (size <= 0)
                throw new InvalidOperationException($"Sensor_GetImageSize devolvió un tamaño inválido: {size}");

            var rawBuffer = new byte[size];
            int result = RunyesSensorInterop.Sensor_LoadRawImage(_deviceHandle, rawBuffer);
            if (result == 0)
            {
                int errorCode = RunyesSensorInterop.Sensor_GetLastError(_deviceHandle);
                throw new InvalidOperationException(
                    $"Sensor_LoadRawImage falló (código de error {errorCode}).");
            }

            onStatus?.Invoke("Procesando imagen...");

            double[] corrected = RunyesImageCorrection.CorrectRawImage(rawBuffer, _calibration);
            byte[] gray16Bytes = RunyesImageCorrection.ToGray16Bytes(corrected);

            return new CapturedImage
            {
                Width = _imageWidth,
                Height = _imageHeight,
                PixelData = gray16Bytes,
            };
        }

        public void Dispose() => Disconnect();

        // ------------------------------------------------------------------
        // Solo para diagnóstico (usado por RunyesImageBitsTestConsole): deja
        // ver ancho/alto/serie detectados y los bytes crudos SIN corrección,
        // para poder aislar si un problema está en la captura o en la
        // corrección posterior.
        // ------------------------------------------------------------------
        public int ImageWidth => _imageWidth;
        public int ImageHeight => _imageHeight;
        public string? DetectedSerial => _serial;
        public bool HasCalibration => _calibration != null;

        public byte[] CaptureRawBytesForDiagnostics(Action<string>? onStatus = null)
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("El sensor no está conectado.");

            onStatus?.Invoke("Esperando disparo de rayos X...");
            var xrayTimeout = TimeSpan.FromSeconds(60);
            var waitStart = DateTime.UtcNow;
            while (RunyesSensorInterop.Sensor_GetXrayState(_deviceHandle) != 1)
            {
                if (DateTime.UtcNow - waitStart > xrayTimeout)
                    throw new InvalidOperationException(
                        "Pasaron 60 segundos sin detectar el disparo de rayos X.");
                System.Threading.Thread.Sleep(20);
            }

            onStatus?.Invoke("Transfiriendo imagen...");
            int size = RunyesSensorInterop.Sensor_GetImageSize(_deviceHandle);
            if (size <= 0)
                throw new InvalidOperationException($"Sensor_GetImageSize devolvió un tamaño inválido: {size}");

            var rawBuffer = new byte[size];
            int result = RunyesSensorInterop.Sensor_LoadRawImage(_deviceHandle, rawBuffer);
            if (result == 0)
            {
                int errorCode = RunyesSensorInterop.Sensor_GetLastError(_deviceHandle);
                throw new InvalidOperationException(
                    $"Sensor_LoadRawImage falló (código de error {errorCode}).");
            }

            return rawBuffer;
        }

        public double[] CorrectRawBytesForDiagnostics(byte[] rawBytes)
        {
            if (_calibration == null)
                throw new InvalidOperationException("No hay calibración cargada para este sensor.");
            return RunyesImageCorrection.CorrectRawImage(rawBytes, _calibration);
        }
    }
}
