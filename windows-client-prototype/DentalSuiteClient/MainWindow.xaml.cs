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
            catch
            {
                // La app funciona "local primero": si no hay internet
                // ahora mismo, simplemente no se crea la sesión remota.
                // Todo lo que se capture en esta visita se guarda bien
                // en esta PC de todos modos; solo no se sincroniza con
                // el servidor hasta que haya conexión. No vale la pena
                // interrumpir con un diálogo de error por algo que es
                // normal en un consultorio sin internet.
                _studyId = null;
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
                if (item.ImageType == CaptureImageType.Video)
                {
                    PlayVideo(item);
                    return;
                }

                // Ya tiene imagen: abrir el visor/editor en vez de
                // intentar capturar de nuevo.
                OpenImageEditor(item);
                return;
            }

            await CaptureIntoSlotAsync(item);
        }

        /// <summary>Abre el video con el reproductor que Windows tenga
        /// configurado por defecto para archivos .avi -- así no hay que
        /// construir un reproductor propio dentro de la app.</summary>
        private void PlayVideo(CaptureItem item)
        {
            if (string.IsNullOrEmpty(item.VideoPath) || !File.Exists(item.VideoPath))
            {
                MessageBox.Show("No se encontró el archivo de video.", "Video no encontrado",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.VideoPath)
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el video: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenImageEditor(CaptureItem item)
        {
            if (string.IsNullOrEmpty(item.ImagePath))
                return;

            // Si ya se sincronizó con el servidor (tiene SlotId), la
            // edición también actualiza el registro remoto ahí -- igual
            // que hace la galería del paciente (PatientGalleryWindow).
            (string SlotId, int ToothNumber, string ImageType)? remoteInfo = item.SlotId != null
                ? (item.SlotId, item.ToothNumber, ImageTypeToApiString(item.ImageType))
                : null;

            var editor = new ImageAnnotationWindow(item.ImagePath, remoteInfo) { Owner = this };
            editor.ShowDialog();

            if (editor.WasSaved)
            {
                // Forzar que la miniatura recargue la imagen desde disco.
                string path = item.ImagePath;
                item.ImagePath = null;
                item.ImagePath = path;
            }
        }

        private static string ImageTypeToApiString(CaptureImageType type) => type switch
        {
            CaptureImageType.Xray => "xray",
            CaptureImageType.Video => "video",
            _ => "photo",
        };

        /// <summary>Borra una captura ya hecha (foto, radiografía o video):
        /// el archivo local, el registro en el servidor si ya se había
        /// sincronizado, y deja la casilla lista para volver a capturar.</summary>
        private async void OnDeleteSlotClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: CaptureItem item })
                return;

            var confirm = MessageBox.Show(
                $"¿Borrar esta {item.TypeLabel.ToLower()} del diente {item.DisplayNumber}? Esto no se puede deshacer.",
                "Confirmar borrado", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            if (item.SlotId != null)
            {
                try
                {
                    await _api.DeleteSlotAsync(item.SlotId);
                }
                catch
                {
                    // Sin conexión: se borra igual localmente; el registro
                    // del servidor queda pendiente de limpiar después.
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath))
                    File.Delete(item.ImagePath);
                if (!string.IsNullOrEmpty(item.VideoPath) && File.Exists(item.VideoPath))
                    File.Delete(item.VideoPath);
            }
            catch
            {
                // Si el archivo está en uso o ya no existe, no es motivo
                // para dejar la casilla en un estado inconsistente.
            }

            item.SlotId = null;
            item.ImagePath = null;
            item.VideoPath = null;
            item.IsCaptured = false;
        }

        /// <summary>Muestra la lista de odontogramas pasados de este
        /// paciente (como la "Historia reciente" de Runyes) y, si eligen
        /// uno, lo carga en la galería para verlo/editarlo/seguir
        /// agregando capturas ahí.</summary>
        private async void OnViewHistoryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var studies = await _api.ListPatientStudiesAsync(_patient.Id!);
                var historyWindow = new HistoryWindow(_patient.FullName, studies) { Owner = this };
                bool? result = historyWindow.ShowDialog();

                if (result == true && historyWindow.SelectedStudy != null)
                {
                    var study = historyWindow.SelectedStudy;
                    _studyId = study.Id;
                    _viewModel.LoadHistoricalSlots(study.Slots.Select(s =>
                        (s.ToothNumber, s.ImageType, s.IsCaptured, s.ImageUrl, s.Id)));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo cargar el historial (¿hay conexión a internet?): {ex.Message}",
                    "Sin conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>Empieza un odontograma completamente nuevo para este
        /// mismo paciente, dejando el actual guardado en su historial
        /// (accesible después desde "Historial").</summary>
        private async void OnNewOdontogramClick(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "¿Iniciar un odontograma nuevo? El actual se conserva en el historial de este paciente.",
                "Nuevo odontograma", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
                return;

            _viewModel.ResetSelection();
            await CreateStudyAsync();
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
                    var cameraWindow = new CameraCaptureWindow(videoMode: false) { Owner = this };
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
                else if (item.ImageType == CaptureImageType.Video)
                {
                    var cameraWindow = new CameraCaptureWindow(videoMode: true) { Owner = this };
                    bool? result = cameraWindow.ShowDialog();
                    if (result != true || cameraWindow.CapturedVideoPath == null)
                        return; // el usuario canceló, o no había cámara

                    // El video ya quedó guardado en su carpeta; aquí solo
                    // se enlaza la casilla a él y a su miniatura (primer
                    // cuadro) para que se vea en la galería igual que una
                    // foto o radiografía.
                    item.VideoPath = cameraWindow.CapturedVideoPath;
                    item.ImagePath = cameraWindow.CapturedVideoThumbnailPath;
                    item.IsCaptured = true;

                    if (_studyId != null && cameraWindow.CapturedVideoThumbnailPath != null)
                    {
                        // La app es "local primero": la captura de arriba
                        // YA se guardó bien en esta PC sin importar lo que
                        // pase aquí abajo. Si no hay internet en este
                        // momento, la subida al servidor simplemente no
                        // se hace ahora -- no se le muestra un error al
                        // usuario por algo que de todos modos ya se
                        // guardó localmente. (Pendiente para más adelante:
                        // una cola que reintente subir lo que quedó
                        // pendiente en cuanto vuelva la conexión.)
                        try
                        {
                            string remoteUrl = await _api.UploadImageFileAsync(cameraWindow.CapturedVideoThumbnailPath);
                            var savedSlot = await _api.UploadSlotAsync(_studyId, item.ToothNumber, "video", remoteUrl, null);
                            item.SlotId = savedSlot.Id;
                        }
                        catch
                        {
                            // Sin conexión, o el servidor no respondió --
                            // se queda solo en esta PC por ahora.
                        }
                    }

                    return;
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

                    // Ventana pequeña que muestra "Esperando disparo..." /
                    // "Transfiriendo imagen..." mientras CaptureImage()
                    // corre en un hilo aparte, para no dejar la ventana
                    // principal congelada sin explicar qué está pasando.
                    var statusWindow = new SensorCaptureStatusWindow { Owner = this };
                    statusWindow.Show();

                    try
                    {
                        var token = statusWindow.CancellationSource.Token;
                        captured = await Task.Run(
                            () => _runyesSensor.CaptureImage(statusWindow.UpdateStatus, token),
                            token);
                    }
                    catch (OperationCanceledException)
                    {
                        return; // el usuario le dio "Cancelar" -- no es un error
                    }
                    finally
                    {
                        statusWindow.Close();
                    }
                }

                string imagePath = SaveCapturedImageToDisk(captured, item);
                item.ImagePath = imagePath;
                item.IsCaptured = true;

                if (_studyId != null)
                {
                    // Igual que con el video: la captura local ya quedó
                    // guardada arriba, así que un fallo de red aquí no
                    // debe verse como un error de captura -- el usuario
                    // ya tiene su imagen, solo falta sincronizarla cuando
                    // haya internet.
                    try
                    {
                        string remoteUrl = await _api.UploadImageFileAsync(imagePath);
                        string imageTypeValue = item.ImageType == CaptureImageType.Xray ? "xray" : "photo";
                        var savedSlot = await _api.UploadSlotAsync(_studyId, item.ToothNumber, imageTypeValue, remoteUrl, null);
                        item.SlotId = savedSlot.Id;
                    }
                    catch
                    {
                        // Sin conexión, o el servidor no respondió --
                        // se queda solo en esta PC por ahora.
                    }
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
