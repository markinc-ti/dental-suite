using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DentalSuiteClient.Api;
using DentalSuiteClient.Models;
using RunyesSensorPrototype;

namespace DentalSuiteClient
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new();
        private readonly ApiClient _api = new();
        private readonly PatientDto _patient;
        private string? _studyId;

        // Cada casilla de captura ya sabe su propio tipo (radiografía o
        // foto) — no hace falta que el usuario elija un sensor a mano:
        // radiografía siempre usa Runyes, foto siempre usa la cámara.
        private readonly IDentalSensor _runyesSensor = new RunyesSensorAdapter();
        private bool _runyesConnected;
        private bool _isNavigatingAway;

        public MainWindow(PatientDto patient)
        {
            InitializeComponent();
            DataContext = _viewModel;
            _patient = patient;

            _viewModel.PatientName = patient.FullName;
            _viewModel.PatientId = patient.ExternalReference ?? patient.Id ?? "";

            // Cada vez que se abre el odontograma de un paciente es una
            // sesión nueva — así se va acumulando el historial de
            // radiografías y fotos en el tiempo, sesión por sesión.
            Loaded += async (_, _) => await CreateStudyAsync();
            Closed += (_, _) =>
            {
                _runyesSensor.Dispose();
                if (!_isNavigatingAway)
                    Application.Current.Shutdown();
            };
        }

        private void OnBackToPatientsClick(object sender, RoutedEventArgs e)
        {
            var patientSelector = new PatientSelectorWindow();
            patientSelector.Show();
            _isNavigatingAway = true;
            Close();
        }

        private async Task CreateStudyAsync()
        {
            try
            {
                var study = await _api.CreateStudyAsync(_patient.Id!, sensorBrand: "Runyes");
                _studyId = study.Id;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo crear la sesión de captura en el backend: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Se dispara al hacer doble clic en una casilla de captura
        /// (aparecen automáticamente, de dos en dos —radiografía y
        /// foto— al marcar un diente arriba en el odontograma).
        /// </summary>
        private async void OnThumbnailMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
                return;

            if (sender is not FrameworkElement { DataContext: CaptureItem item })
                return;

            if (item.IsCaptured)
            {
                // Ya tiene imagen: abrir el visor/editor en vez de
                // intentar capturar de nuevo.
                OpenImageEditor(item);
                return;
            }

            await CaptureIntoSlotAsync(item);
        }

        private void OpenImageEditor(CaptureItem item)
        {
            if (string.IsNullOrEmpty(item.ImagePath))
                return;

            var editor = new ImageAnnotationWindow(item.ImagePath) { Owner = this };
            editor.ShowDialog();

            if (editor.WasSaved)
            {
                // Forzar que la miniatura recargue la imagen desde disco.
                string path = item.ImagePath;
                item.ImagePath = null;
                item.ImagePath = path;
            }
        }

        private void OnCompareClick(object sender, RoutedEventArgs e)
        {
            var comparison = new ComparisonWindow(_viewModel.CaptureSlots.ToList()) { Owner = this };
            comparison.ShowDialog();
        }

        private async Task CaptureIntoSlotAsync(CaptureItem item)
        {
            try
            {
                CapturedImage captured;

                if (item.ImageType == CaptureImageType.Photo)
                {
                    // La cámara tiene su propia ventana con vista previa
                    // en vivo (a diferencia del sensor de rayos X, que no
                    // tiene noción de "vista previa" — solo dispara y
                    // entrega una imagen).
                    var cameraWindow = new CameraCaptureWindow { Owner = this };
                    bool? result = cameraWindow.ShowDialog();
                    if (result != true || cameraWindow.CapturedPngBytes == null)
                        return; // el usuario canceló, o no había cámara

                    captured = new CapturedImage
                    {
                        Width = cameraWindow.CapturedWidth,
                        Height = cameraWindow.CapturedHeight,
                        PixelData = cameraWindow.CapturedPngBytes,
                        IsPreEncodedPng = true,
                    };
                }
                else
                {
                    if (!_runyesConnected)
                    {
                        bool connected = await Task.Run(() => _runyesSensor.Connect(0));
                        if (!connected)
                        {
                            MessageBox.Show(
                                $"No se detectó ningún sensor {_runyesSensor.SensorName} conectado. "
                                + "Verifica el cable USB e inténtalo de nuevo.",
                                "Sensor no encontrado",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        _runyesConnected = true;
                    }

                    // CaptureImage() bloquea hasta que el sensor entregue
                    // la imagen — se corre en un hilo aparte para no
                    // congelar la ventana mientras se espera.
                    captured = await Task.Run(() => _runyesSensor.CaptureImage());
                }

                string imagePath = SaveCapturedImageToDisk(captured, item);
                item.ImagePath = imagePath;
                item.IsCaptured = true;

                if (_studyId != null)
                {
                    // Se sube el archivo real al servidor (no solo la
                    // ruta local) para que sea visible desde cualquier
                    // dispositivo — el celular, otra PC, etc. — no solo
                    // desde esta máquina.
                    string remoteUrl = await _api.UploadImageFileAsync(imagePath);
                    string imageTypeValue = item.ImageType == CaptureImageType.Xray ? "xray" : "photo";
                    await _api.UploadSlotAsync(_studyId, item.ToothNumber, imageTypeValue, remoteUrl, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al capturar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Guarda la imagen capturada como PNG en disco. Si viene de una
        /// cámara (ya codificada como PNG), se guarda tal cual. Si viene
        /// de un sensor de rayos X (bytes crudos, 16 bits por píxel en
        /// escala de grises — a confirmar contra el hardware real), se
        /// convierte primero.
        /// </summary>
        private static string SaveCapturedImageToDisk(CapturedImage captured, CaptureItem item)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "DentalSuite");
            Directory.CreateDirectory(folder);

            string typeTag = item.ImageType == CaptureImageType.Xray ? "xray" : "foto";
            string fileName = $"diente_{item.ToothNumber}_{typeTag}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(folder, fileName);

            if (captured.IsPreEncodedPng)
            {
                File.WriteAllBytes(fullPath, captured.PixelData);
                return fullPath;
            }

            int stride = captured.Width * 2; // 2 bytes por píxel (Gray16)
            BitmapSource bitmap = BitmapSource.Create(
                captured.Width,
                captured.Height,
                96, 96,
                PixelFormats.Gray16,
                null,
                captured.PixelData,
                stride);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var fileStream = new FileStream(fullPath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }

            return fullPath;
        }
    }
}
