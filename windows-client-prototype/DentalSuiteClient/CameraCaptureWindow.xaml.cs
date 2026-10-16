using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using DentalSuiteClient.Cameras;
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
    /// Fuentes de video soportadas:
    ///   - Cámaras USB (UVC) — cualquier cámara que Windows reconozca.
    ///   - Cámara WiFi VVDental EC-3 — protocolo propio por UDP
    ///     (ver Cameras/VvDentalWifiCamera.cs). La ventana se conecta
    ///     sola a la red WiFi de la cámara y al cerrarse regresa a la
    ///     red anterior (ver Cameras/WifiCameraNetwork.cs).
    ///
    /// El botón físico de la cámara dispara la misma acción que el botón
    /// principal en pantalla: en cámaras USB llega como una tecla; en la
    /// cámara WiFi llega como un paquete de red.
    /// </summary>
    public partial class CameraCaptureWindow : Window
    {
        private enum SourceKind { Uvc, VvDentalWifi }

        private sealed record CameraSource(SourceKind Kind, string Name, string? Moniker);

        private const string WifiSourceName = "📶 Cámara WiFi VVDental (EC-3)";

        private readonly bool _videoMode;

        private readonly List<CameraSource> _sources = new();
        private bool _suppressSelectionEvent;

        private VideoCaptureDevice? _uvcDevice;
        private VvDentalWifiCamera? _wifiCamera;
        private WifiCameraNetwork.SwitchResult? _wifiSwitch;
        private CancellationTokenSource? _wifiConnectCts;
        private readonly DispatcherTimer _signalTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private DateTime _wifiStartedUtc;

        private Bitmap? _lastFrame;
        private readonly object _frameLock = new();
        private volatile bool _mirror;

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

            _mirror = CameraPreferences.Mirror;
            MirrorCheckBox.IsChecked = _mirror;

            _signalTimer.Tick += OnSignalTimerTick;

            Loaded += (_, _) => RefreshDeviceList(preferredName: CameraPreferences.LastCameraName);
            Closed += (_, _) =>
            {
                _signalTimer.Stop();
                StopPreview(restoreNetwork: true);
            };
        }

        // ============================================================
        //  Lista de cámaras
        // ============================================================

        /// <summary>
        /// Vuelve a listar las cámaras (útil si el usuario conecta la
        /// cámara, o instala su driver, DESPUÉS de abrir esta ventana).
        /// La cámara WiFi siempre aparece en la lista, porque no se puede
        /// "detectar" sin antes conectarse a su red.
        /// </summary>
        private void RefreshDeviceList(string? preferredName = null)
        {
            string? previouslySelected = preferredName
                ?? (CameraSelector.SelectedIndex >= 0 ? _sources[CameraSelector.SelectedIndex].Name : null);

            _sources.Clear();
            try
            {
                foreach (FilterInfo device in new FilterInfoCollection(FilterCategory.VideoInputDevice))
                    _sources.Add(new CameraSource(SourceKind.Uvc, device.Name, device.MonikerString));
            }
            catch
            {
                // Sin DirectShow disponible: queda solo la cámara WiFi.
            }
            _sources.Add(new CameraSource(SourceKind.VvDentalWifi, WifiSourceName, null));

            _suppressSelectionEvent = true;
            CameraSelector.Items.Clear();
            foreach (var s in _sources)
                CameraSelector.Items.Add(s.Name);
            CameraSelector.SelectedIndex = -1;
            _suppressSelectionEvent = false;

            int index = previouslySelected != null ? _sources.FindIndex(s => s.Name == previouslySelected) : -1;
            if (index < 0)
                index = _sources.FindIndex(s => s.Kind == SourceKind.Uvc);

            if (index < 0)
            {
                // No hay cámara USB y no se había elegido la WiFi antes:
                // no se cambia la red WiFi sin que el usuario lo pida.
                ShowOverlay("No hay cámara USB conectada.\nSi vas a usar la cámara WiFi, enciéndela y elígela en la lista de arriba.");
                return;
            }

            CameraSelector.SelectedIndex = index; // dispara OnCameraSelectionChanged
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

        private async void OnCameraSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressSelectionEvent) return;
            if (CameraSelector.SelectedIndex < 0) return;

            var source = _sources[CameraSelector.SelectedIndex];
            CameraPreferences.LastCameraName = source.Name;

            if (source.Kind == SourceKind.Uvc)
            {
                StopPreview(restoreNetwork: true);
                StartUvcPreview(source.Moniker!);
            }
            else
            {
                StopPreview(restoreNetwork: false);
                await StartWifiPreviewAsync();
            }
        }

        private void OnMirrorChanged(object sender, RoutedEventArgs e)
        {
            _mirror = MirrorCheckBox.IsChecked == true;
            CameraPreferences.Mirror = _mirror;
        }

        // ============================================================
        //  Cámara USB (UVC)
        // ============================================================

        private void StartUvcPreview(string monikerString)
        {
            HideOverlay();
            StatusText.Text = "";
            _uvcDevice = new VideoCaptureDevice(monikerString);
            _uvcDevice.NewFrame += OnUvcNewFrame;
            _uvcDevice.Start();
        }

        private void OnUvcNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // AForge recicla el mismo bitmap en cada cuadro: hay que clonarlo.
            HandleFrame((Bitmap)eventArgs.Frame.Clone());
        }

        // ============================================================
        //  Cámara WiFi VVDental
        // ============================================================

        private async Task StartWifiPreviewAsync()
        {
            _wifiConnectCts = new CancellationTokenSource();
            var token = _wifiConnectCts.Token;

            ShowOverlay("Conectando con la cámara WiFi...");
            CameraSelector.IsEnabled = false;
            RefreshDevicesButton.IsEnabled = false;

            try
            {
                var result = await WifiCameraNetwork.EnsureConnectedAsync(
                    msg => Dispatcher.BeginInvoke(() => ShowOverlay(msg)), token);

                if (token.IsCancellationRequested)
                {
                    await WifiCameraNetwork.RestoreAsync(result);
                    return;
                }

                // Si ya había un cambio de red pendiente de deshacer (por
                // ejemplo se eligió la cámara WiFi dos veces), se conserva
                // el original — es el que dice a qué red regresar.
                _wifiSwitch ??= result;

                StatusText.Text = result.AlreadyConnected
                    ? "Conectado a la red de la cámara."
                    : result.UsedSpareAdapter
                        ? $"Cámara en el adaptador \"{result.InterfaceName}\" — el WiFi de la clínica sigue conectado."
                        : $"Conectado a {result.CameraSsid}. Al cerrar esta ventana se regresa a \"{result.PreviousProfile}\".";

                _wifiCamera = new VvDentalWifiCamera();
                _wifiCamera.JpegFrameReceived += OnWifiJpegFrame;
                _wifiCamera.ShutterButtonPressed += OnWifiShutterButton;
                _wifiCamera.Start();
                _wifiStartedUtc = DateTime.UtcNow;

                ShowOverlay("Esperando video de la cámara...");
                _signalTimer.Start();
            }
            catch (OperationCanceledException)
            {
                // Se cerró la ventana o se cambió de cámara mientras conectaba.
            }
            catch (Exception ex)
            {
                ShowOverlay("No se pudo conectar con la cámara WiFi.");
                MessageBox.Show(ex.Message, "Cámara WiFi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                if (!_isRecording)
                {
                    CameraSelector.IsEnabled = true;
                    RefreshDevicesButton.IsEnabled = true;
                }
            }
        }

        private void OnWifiJpegFrame(byte[] jpeg)
        {
            Bitmap frame;
            try
            {
                using var ms = new MemoryStream(jpeg);
                using var decoded = System.Drawing.Image.FromStream(ms);
                // Copia independiente del stream (Image.FromStream exige
                // que el stream siga abierto mientras viva la imagen).
                frame = new Bitmap(decoded);
            }
            catch
            {
                return; // JPEG dañado: se ignora ese cuadro
            }
            HandleFrame(frame);
        }

        private void OnWifiShutterButton()
        {
            Dispatcher.BeginInvoke(() =>
            {
                // Ignora el botón si la ventana ya se está cerrando.
                if (IsLoaded && IsVisible)
                    OnPrimaryActionClick(this, new RoutedEventArgs());
            });
        }

        private void OnSignalTimerTick(object? sender, EventArgs e)
        {
            if (_wifiCamera == null) return;

            double secondsSinceFrame = (DateTime.UtcNow - _wifiCamera.LastFrameUtc).TotalSeconds;
            if (_wifiCamera.FramesReceived > 0 && secondsSinceFrame < 3)
            {
                HideOverlay();
            }
            else if (_wifiCamera.FramesReceived > 0)
            {
                ShowOverlay("Sin señal de la cámara.\n¿Se apagó o se alejó demasiado? (alcance aprox. 20 m)");
            }
            else if (_wifiCamera.FramesReceived == 0 && (DateTime.UtcNow - _wifiStartedUtc).TotalSeconds > 8)
            {
                ShowOverlay("Conectado a la red de la cámara, pero no llega video.\n"
                    + "Verifica que la cámara esté encendida y que EndoCamera esté cerrado.\n"
                    + "Si Windows preguntó por el Firewall, elige \"Permitir\".");
            }
        }

        // ============================================================
        //  Común a las dos fuentes
        // ============================================================

        private void HandleFrame(Bitmap frameCopy)
        {
            if (_mirror)
                frameCopy.RotateFlip(RotateFlipType.RotateNoneFlipX);

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
                        // tronar toda la grabación.
                    }
                }
            }

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    BitmapSource? source = null;
                    lock (_frameLock)
                    {
                        if (_lastFrame != null)
                            source = BitmapToBitmapSource(_lastFrame);
                    }
                    if (source != null)
                        PreviewImage.Source = source;
                }
                catch
                {
                    // Un cuadro ocasional puede fallar al convertir (por
                    // ejemplo justo al cerrar la ventana).
                }
            });
        }

        private void StopPreview(bool restoreNetwork)
        {
            lock (_recordingLock)
            {
                if (_aviWriter != null)
                {
                    // Si se cierra a media grabación, se finaliza el
                    // archivo en vez de dejarlo corrupto.
                    _videoStream = null;
                    _aviWriter.Close();
                    _aviWriter = null;
                }
            }

            if (_uvcDevice != null)
            {
                _uvcDevice.NewFrame -= OnUvcNewFrame;
                if (_uvcDevice.IsRunning)
                {
                    _uvcDevice.SignalToStop();
                    _uvcDevice.WaitForStop();
                }
                _uvcDevice = null;
            }

            _wifiConnectCts?.Cancel();
            _wifiConnectCts = null;
            _signalTimer.Stop();

            if (_wifiCamera != null)
            {
                _wifiCamera.JpegFrameReceived -= OnWifiJpegFrame;
                _wifiCamera.ShutterButtonPressed -= OnWifiShutterButton;
                _wifiCamera.Dispose();
                _wifiCamera = null;
            }

            if (restoreNetwork && _wifiSwitch != null)
            {
                var toRestore = _wifiSwitch;
                _wifiSwitch = null;
                // No se espera: la ventana puede cerrarse mientras Windows
                // regresa a la red anterior en segundo plano.
                _ = WifiCameraNetwork.RestoreAsync(toRestore);
            }

            lock (_frameLock)
            {
                _lastFrame?.Dispose();
                _lastFrame = null;
            }
        }

        private void ShowOverlay(string text)
        {
            OverlayText.Text = text;
            OverlayText.Visibility = Visibility.Visible;
        }

        private void HideOverlay() => OverlayText.Visibility = Visibility.Collapsed;

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
        /// Acción del botón principal, con clic de mouse, con la tecla del
        /// botón físico de una cámara USB, o con el botón físico de la
        /// cámara WiFi. En modo foto toma la foto y cierra. En modo video,
        /// la primera vez empieza a grabar; la segunda detiene y guarda.
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
            // Se trabaja con una COPIA del cuadro actual: el original lo
            // puede liberar el hilo de la cámara en cualquier momento (esa
            // era la causa del error "Parameter is not valid" al grabar).
            Bitmap? currentFrame;
            lock (_frameLock)
            {
                currentFrame = _lastFrame == null ? null : new Bitmap(_lastFrame);
            }

            if (currentFrame == null)
            {
                MessageBox.Show("Todavía no hay imagen de la cámara para empezar a grabar.",
                    "Sin imagen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (currentFrame)
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "DentalSuite");
                Directory.CreateDirectory(folder);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string path = Path.Combine(folder, $"video_{timestamp}.avi");

                // Miniatura (primer cuadro) para la galería.
                string thumbFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DentalSuite");
                Directory.CreateDirectory(thumbFolder);
                string thumbFile = Path.Combine(thumbFolder, $"video_{timestamp}_thumb.jpg");
                currentFrame.Save(thumbFile, ImageFormat.Jpeg);
                CapturedVideoThumbnailPath = thumbFile;

                lock (_recordingLock)
                {
                    _aviWriter = new AviWriter(path)
                    {
                        // La cámara WiFi transmite a 20 cuadros/seg (según
                        // su manual); las USB típicamente a 15.
                        FramesPerSecond = _wifiCamera != null ? 20 : 15,
                        EmitIndex1 = true,
                    };
                    _videoStream = _aviWriter.AddUncompressedVideoStream(currentFrame.Width, currentFrame.Height);
                    CapturedVideoPath = path;
                }
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
        /// Muchas cámaras USB con botón físico se comportan ante Windows
        /// como un teclado. Se trata cualquier tecla (salvo Escape, que
        /// cancela) como si le hubieran dado clic al botón principal.
        /// </summary>
        private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.IsRepeat) return;

            if (e.Key == System.Windows.Input.Key.Escape)
            {
                OnCancelClick(sender, e);
                return;
            }

            // Evita que la tecla cambie la cámara elegida en la lista.
            if (CameraSelector.IsKeyboardFocusWithin)
                return;

            OnPrimaryActionClick(sender, e);
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
                _lastFrame.Save(memoryStream, ImageFormat.Png);
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
                // Cancelado a media grabación: se borra el archivo.
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
                    // Archivo en uso, etc.: no bloquea el cierre.
                }

                CapturedVideoPath = null;
                CapturedVideoThumbnailPath = null;
            }

            DialogResult = false;
        }
    }
}
