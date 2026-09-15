using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;

namespace DentalSuiteClient
{
    public partial class CameraCaptureWindow : Window
    {
        private VideoCaptureDevice? _device;
        private Bitmap? _lastFrame;
        private readonly object _frameLock = new();

        public byte[]? CapturedPngBytes { get; private set; }
        public int CapturedWidth { get; private set; }
        public int CapturedHeight { get; private set; }

        public CameraCaptureWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => StartPreview();
            Closed += (_, _) => StopPreview();
        }

        private void StartPreview()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0)
            {
                MessageBox.Show("No se detectó ninguna cámara conectada.", "Cámara no encontrada",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
                return;
            }

            _device = new VideoCaptureDevice(devices[0].MonikerString);
            _device.NewFrame += OnNewFrame;
            _device.Start();
        }

        private void StopPreview()
        {
            if (_device is { IsRunning: true })
            {
                _device.NewFrame -= OnNewFrame;
                _device.SignalToStop();
                _device.WaitForStop();
            }
            _device = null;

            lock (_frameLock)
            {
                _lastFrame?.Dispose();
                _lastFrame = null;
            }
        }

        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            var frameCopy = (Bitmap)eventArgs.Frame.Clone();

            lock (_frameLock)
            {
                _lastFrame?.Dispose();
                _lastFrame = frameCopy;
            }

            Dispatcher.BeginInvoke(() =>
            {
                Bitmap? toShow;
                lock (_frameLock) { toShow = _lastFrame; }
                if (toShow == null) return;

                try
                {
                    PreviewImage.Source = BitmapToBitmapSource(toShow);
                }
                catch
                {
                    // Un cuadro ocasional puede fallar al convertir (por
                    // ejemplo si llega justo cuando se está cerrando la
                    // ventana) — no vale la pena tronar la app por eso.
                }
            });
        }

        private static BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            lock (_frameLock)
            {
                if (_lastFrame == null)
                {
                    MessageBox.Show("Todavía no hay imagen de la cámara para guardar.", "Sin imagen",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using var memoryStream = new MemoryStream();
                _lastFrame.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                CapturedPngBytes = memoryStream.ToArray();
                CapturedWidth = _lastFrame.Width;
                CapturedHeight = _lastFrame.Height;
            }

            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
