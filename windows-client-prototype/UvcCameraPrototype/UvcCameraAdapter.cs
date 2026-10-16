using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using AForge.Video;
using AForge.Video.DirectShow;
using RunyesSensorPrototype;

namespace UvcCameraPrototype
{
    /// <summary>
    /// Cámara intraoral genérica: cualquier cámara USB compatible con
    /// UVC (USB Video Class) — la misma clase de dispositivo que una
    /// webcam normal de laptop. Confirmado con el software de
    /// referencia del fabricante (DentistCam.exe), que usa Qt
    /// Multimedia sobre Windows Media Foundation — ningún SDK
    /// propietario de por medio.
    ///
    /// Aquí se usa AForge.Video.DirectShow (paquete NuGet) para
    /// enumerar y capturar de cualquier dispositivo de video
    /// reconocido por Windows, sin importar la marca.
    /// </summary>
    public sealed class UvcCameraAdapter : IDentalSensor
    {
        private VideoCaptureDevice? _device;
        private TaskCompletionSource<Bitmap>? _pendingFrame;

        public string SensorName => "Cámara intraoral (UVC)";

        public int GetDeviceCount()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            return devices.Count;
        }

        public bool Connect(int deviceIndex = 0)
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0 || deviceIndex >= devices.Count)
                return false;

            _device = new VideoCaptureDevice(devices[deviceIndex].MonikerString);
            return true;
        }

        public void Disconnect()
        {
            if (_device is { IsRunning: true })
            {
                _device.SignalToStop();
                _device.WaitForStop();
            }
            _device = null;
        }

        public CapturedImage CaptureImage(Action<string>? onStatus = null, System.Threading.CancellationToken cancellationToken = default)
        {
            if (_device == null)
                throw new InvalidOperationException("La cámara intraoral no está conectada.");

            onStatus?.Invoke("Tomando foto...");

            _pendingFrame = new TaskCompletionSource<Bitmap>();
            _device.NewFrame += OnNewFrame;

            try
            {
                _device.Start();

                // Espera a que llegue el primer cuadro de video, con
                // límite de tiempo por si la cámara no responde.
                bool arrived = _pendingFrame.Task.Wait(TimeSpan.FromSeconds(10));
                if (!arrived)
                    throw new InvalidOperationException(
                        "No se recibió ninguna imagen de la cámara en 10 segundos.");

                using Bitmap frame = _pendingFrame.Task.Result;
                using var memoryStream = new MemoryStream();
                frame.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);

                return new CapturedImage
                {
                    Width = frame.Width,
                    Height = frame.Height,
                    PixelData = memoryStream.ToArray(),
                    IsPreEncodedPng = true,
                };
            }
            finally
            {
                _device.NewFrame -= OnNewFrame;
                _device.SignalToStop();
                _device.WaitForStop();
            }
        }

        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // El evento reutiliza el mismo bitmap internamente en cada
            // cuadro — hay que clonarlo antes de que AForge lo recicle.
            _pendingFrame?.TrySetResult((Bitmap)eventArgs.Frame.Clone());
        }

        public void Dispose() => Disconnect();
    }
}
