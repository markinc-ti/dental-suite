using System;
using RunyesSensorPrototype;

namespace WoodpeckerSensorPrototype
{
    /// <summary>
    /// Segundo intento de adaptador Woodpecker/DBA — esta vez contra
    /// FpdSys.dll (la capa genérica), en vez de XVDetector.dll (que
    /// resultó no ser la ruta real que usa la app con tu sensor E4S).
    ///
    /// Sigue el mismo patrón de polling que Runyes: ScanOnce dispara y
    /// espera la captura, QueryImageBuf pregunta el tamaño resultante,
    /// GetImageFromBuf entrega los píxeles.
    /// </summary>
    public sealed class FpdSysSensorAdapter : IDentalSensor
    {
        private IntPtr _handle = IntPtr.Zero;

        // productType: no confirmado. 0 es la suposición más simple
        // ("detectar automáticamente"); si no conecta, el siguiente
        // valor a probar sería un código específico para "E4S".
        private const int DefaultProductType = 0;

        public string SensorName => "Woodpecker (FpdSys/E4S)";

        public int GetDeviceCount() => FpdSysInterop.GetProdCount();

        public bool Connect(int deviceIndex = 0)
        {
            _handle = FpdSysInterop.Create(DefaultProductType);
            return _handle != IntPtr.Zero;
        }

        public void Disconnect()
        {
            if (_handle != IntPtr.Zero)
            {
                FpdSysInterop.Destroy(_handle);
                _handle = IntPtr.Zero;
            }
        }

        public CapturedImage CaptureImage()
        {
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("El sensor Woodpecker (FpdSys) no está conectado.");

            int scanResult = FpdSysInterop.ScanOnce(_handle);
            if (scanResult != 0 && scanResult != 1)
            {
                throw new InvalidOperationException(
                    $"ScanOnce devolvió {scanResult}, no parece indicar éxito.");
            }

            int sizeResult = FpdSysInterop.QueryImageBuf(_handle, out int width, out int height);
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException(
                    $"QueryImageBuf devolvió dimensiones inválidas "
                    + $"(resultado={sizeResult}, width={width}, height={height}).");
            }

            var buffer = new byte[width * height * 2]; // 16 bits por píxel, mismo supuesto de siempre
            int imageResult = FpdSysInterop.GetImageFromBuf(_handle, buffer, buffer.Length);
            if (imageResult != 0 && imageResult != 1)
            {
                throw new InvalidOperationException(
                    $"GetImageFromBuf devolvió {imageResult}, no parece indicar éxito.");
            }

            return new CapturedImage
            {
                Width = width,
                Height = height,
                PixelData = buffer,
            };
        }

        public void Dispose() => Disconnect();
    }
}
