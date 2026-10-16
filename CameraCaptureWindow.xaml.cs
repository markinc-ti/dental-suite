using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using SharpAvi;
using SharpAvi.Codecs;
using SharpAvi.Output;

namespace DentalSuiteClient
{
    /// <summary>
    /// Ventana de captura de la cámara intraoral. Funciona en dos modos:
    ///
    ///   - Foto (VideoMode=false, por defecto): el botón principal toma
    ///     una foto fija y cierra la ventana de inmediato.
    ///   - Video (VideoMode=true): el mismo botón principal alterna
    ///     grabar/detener -- la primera pulsación empieza a grabar, la
    ///     segunda pulsación del MISMO botón detiene, guarda el archivo
    ///     y cierra la ventana.
    ///
    /// El botón físico de la cámara (o cualquier tecla, ya que muchas
    /// cámaras con botón de disparo se comportan ante Windows como un
    /// teclado) dispara la misma acción que el botón principal en
    /// pantalla, sin necesidad de saber qué tecla exacta manda cada
    /// modelo.
    /// </summary>
    public partial class CameraCaptureWindow : Window
    {
        private readonly bool _videoMode;

        private VideoCaptureDevice? _device;
        private Bitmap? _lastFrame;
        private readonly object _frameLock = new();
        private FilterInfoCollection? _availableDevices;
        private bool _suppressSelectionEvent;

        private AviWriter? _aviWriter;
        private IAviVideoStream? _videoStream;
        private readonly object _recordingLock = new();
        private bool _isRecording;

        public byte[]? CapturedPngBytes { get; private set; }
        public int CapturedWidth { get; private set; }
        public int CapturedHeight { get; private set; }
        public string? CapturedVideoPath { get; private set; }
        public string? CapturedVideoThumbnailPath { get; private set; }

        public CameraCaptureWindow(bool videoMode = false)
        {
            InitializeComponent();
            _videoMode = videoMode;

            if (_videoMode)
            {
                InstructionsText.Text = "Vista previa en vivo — presiona \"Grabar\" para empezar, y de nuevo para detener y guardar";
                PrimaryActionButton.Content = "Grabar";
            }

            Loaded += (_, _) => RefreshDeviceList(selectIndex: 0);
            Closed += (_, _) => StopPreview();
        }

        /// <summary>
        /// Vuelve a listar las cámaras conectadas (útil si el usuario
        /// conecta la cámara intraoral, o instala su driver, DESPUÉS de
        /// haber abierto esta ventana). Si es posible, mantiene
        /// seleccionada la misma cámara que ya estaba elegida.
        /// </summary>
        private void RefreshDeviceList(int? selectIndex = null)
        {
            string? previouslySelectedName = CameraSelector.SelectedItem as string;

            _availableDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            _suppressSelectionEvent = true;
            CameraSelector.Items.Clear();
            foreach (FilterInfo device in _availableDevices)
                CameraSelector.Items.Add(device.Name);
            _suppressSelectionEvent = false;

            if (_availableDevices.Count == 0)
            {
                MessageBox.Show(
                    "No se detectó ninguna cámara conectada. Si es la cámara intraoral, "
                    + "verifica que su driver esté instalado y que el cable USB esté "
                    + "conectado, y dale a \"Actualizar lista\".",
                    "Cámara no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int indexToSelect = 0;
            if (selectIndex is int explicitIndex && explicitIndex < _availableDevices.Count)
            {
                indexToSelect = explicitIndex;
            }
            else if (previouslySelectedName != null)
            {
                for (int i = 0; i < _availableDevices.Count; i++)
                {
                    if (_availableDevices[i].Name == previouslySelectedName)
                    {
                        indexToSelect = i;
                        break;
                    }
                }
            }

            CameraSelector.SelectedIndex = indexToSelect;
        }

        private void OnRefreshDevicesClick(object sender, RoutedEventArgs e)
        {
            if (_isRecording)
            {
                MessageBox.Show("Detén la grabación antes de cambiar la lista de cámaras.",
                    "Grabando", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            RefreshDeviceList();
        }

        private void OnCameraSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvent) return;
            if (CameraSelector.SelectedIndex < 0 || _availableDevices == null) return;

            StopPreview();
            StartPreview(_availableDevices[CameraSelector.SelectedIndex].MonikerString);
        }

        private void StartPreview(string monikerString)
        {
            _device = new VideoCaptureDevice(monikerString);
            _device.NewFrame += OnNewFrame;
            _device.Start();
        }

        private void StopPreview()
        {
            lock (_recordingLock)
            {
                if (_aviWriter != null)
                {
                    // Si la ventana se cierra a media grabación, se
                    // finaliza el archivo de todos modos en vez de
                    // dejarlo corrupto/sin poder reproducirse.
                    _videoStream = null;
                    _aviWriter.Close();
                    _aviWriter = null;
                }
            }

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

            lock (_recordingLock)
            {
                if (_videoStream != null)
                {
                    try
                    {
                        byte[] frameBytes = BitmapToBgr32(frameCopy);
                        _videoStream.WriteFrame(true, frameBytes, 0, frameBytes.Length);
                    }
                    catch
                    {
                        // Un cuadro ocasional que falle al codificar no debe
                        // tronar toda la grabación -- se sigue con el
                        // siguiente cuadro.
                    }
                }
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

        private static byte[] BitmapToBgr32(Bitmap frame)
        {
            var data = frame.LockBits(
                new Rectangle(0, 0, frame.Width, frame.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
            try
            {
                int size = data.Stride * frame.Height;
                var buffer = new byte[size];
                Marshal.Copy(data.Scan0, buffer, 0, size);
                return buffer;
            }
            finally
            {
                frame.UnlockBits(data);
            }
        }

        /// <summary>
        /// Acción del botón principal, ya sea con clic de mouse o con la
        /// tecla que manda el botón físico de la cámara. En modo foto,
        /// toma la foto y cierra. En modo video, la primera vez empieza
        /// a grabar; la segunda vez (mismo botón) detiene, guarda y
        /// cierra.
        /// </summary>
        private void OnPrimaryActionClick(object sender, RoutedEventArgs e)
        {
            if (!_videoMode)
            {
                CapturePhotoAndClose();
                return;
            }

            if (!_isRecording)
                StartRecording();
            else
                StopRecordingAndClose();
        }

        private void StartRecording()
        {
            Bitmap? currentFrame;
            lock (_frameLock) { currentFrame = _lastFrame; }

            if (currentFrame == null)
            {
                MessageBox.Show("Todavía no hay imagen de la cámara para empezar a grabar.",
                    "Sin imagen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "DentalSuite");
            Directory.CreateDirectory(folder);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(folder, $"video_{timestamp}.avi");

            // Miniatura (primer cuadro) para mostrar en la galería de la
            // app -- se guarda como JPG normal, junto al video.
            string thumbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "DentalSuite");
            Directory.CreateDirectory(thumbPath);
            string thumbFile = Path.Combine(thumbPath, $"video_{timestamp}_thumb.jpg");
            currentFrame.Save(thumbFile, System.Drawing.Imaging.ImageFormat.Jpeg);
            CapturedVideoThumbnailPath = thumbFile;

            lock (_recordingLock)
            {
                _aviWriter = new AviWriter(path)
                {
                    FramesPerSecond = 15,
                    EmitIndex1 = true,
                };
                _videoStream = _aviWriter.AddUncompressedVideoStream(currentFrame.Width, currentFrame.Height);
                CapturedVideoPath = path;
            }

            _isRecording = true;
            RecordingIndicator.Visibility = Visibility.Visible;
            PrimaryActionButton.Content = "Detener y guardar";
            CameraSelector.IsEnabled = false;
            RefreshDevicesButton.IsEnabled = false;
        }

        private void StopRecordingAndClose()
        {
            lock (_recordingLock)
            {
                _videoStream = null;
                _aviWriter?.Close();
                _aviWriter = null;
            }

            _isRecording = false;
            RecordingIndicator.Visibility = Visibility.Collapsed;

            DialogResult = true;
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

        /// <summary>
        /// Muchas cámaras intraorales con botón físico de disparo se
        /// comportan ante Windows como un teclado (mandan una tecla, no
        /// una señal de video, al presionar el botón). Como no sabemos
        /// de antemano cuál tecla exacta manda cada modelo, se trata
        /// cualquier tecla (salvo Escape, que cancela) como si le
        /// hubieran dado clic al botón principal.
        ///
        /// Además se registra CADA tecla que llega (aunque no haga nada
        /// nuevo, ya que cualquier tecla ya dispara la acción) en un
        /// archivo y en pantalla, para poder confirmar con cámaras
        /// nuevas si su botón realmente manda una tecla o no.
        /// </summary>
        private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            LogInputEvent($"Tecla: {e.Key} (repetida: {e.IsRepeat})");

            if (e.IsRepeat) return; // ignora el auto-repetido de mantener la tecla oprimida

            if (e.Key == System.Windows.Input.Key.Escape)
            {
                OnCancelClick(sender, e);
                return;
            }

            OnPrimaryActionClick(sender, e);
        }

        /// <summary>
        /// Algunas cámaras mandan su botón físico como un botón EXTRA del
        /// mouse (XButton1/XButton2 -- los botones laterales que muchos
        /// mouses de gaming también tienen) en vez de como tecla de
        /// teclado. Se registran TODOS los botones del mouse (para poder
        /// confirmarlo con cámaras nuevas), pero solo los botones
        /// laterales disparan la acción -- el clic izquierdo/derecho
        /// normal se deja intacto para no interferir con los controles
        /// de la ventana (deslizadores, combo de cámara, etc.).
        /// </summary>
        private void OnWindowPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            LogInputEvent($"Mouse: {e.ChangedButton}");

            if (e.ChangedButton == System.Windows.Input.MouseButton.XButton1
                || e.ChangedButton == System.Windows.Input.MouseButton.XButton2)
            {
                OnPrimaryActionClick(sender, e);
            }
        }

        private void LogInputEvent(string description)
        {
            DebugInputText.Text = $"Última señal recibida: {description}";

            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DentalSuite");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "camera-input-diagnostic.log");
                File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff}  {description}\n");
            }
            catch
            {
                // Un log de diagnóstico nunca debe tronar la app.
            }
        }

        private void CapturePhotoAndClose()
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
            if (_isRecording)
            {
                // Se cancela a media grabación: se detiene y se borra el
                // archivo en vez de dejar un video a medias por ahí.
                lock (_recordingLock)
                {
                    _videoStream = null;
                    _aviWriter?.Close();
                    _aviWriter = null;
                }
                _isRecording = false;

                try
                {
                    if (CapturedVideoPath != null && File.Exists(CapturedVideoPath))
                        File.Delete(CapturedVideoPath);
                    if (CapturedVideoThumbnailPath != null && File.Exists(CapturedVideoThumbnailPath))
                        File.Delete(CapturedVideoThumbnailPath);
                }
                catch
                {
                    // Si no se puede borrar (archivo en uso, etc.) no es
                    // razón para bloquear el cierre de la ventana.
                }

                CapturedVideoPath = null;
                CapturedVideoThumbnailPath = null;
            }

            DialogResult = false;
        }
    }
}
