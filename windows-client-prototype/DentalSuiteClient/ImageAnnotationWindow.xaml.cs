using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DentalSuiteClient
{
    public partial class ImageAnnotationWindow : Window
    {
        private enum ToolMode { None, Draw, Rectangle, Circle, Text }

        private readonly string _imagePath;
        private readonly (string SlotId, int ToothNumber, string ImageType)? _remoteSlotInfo;
        private ToolMode _currentTool = ToolMode.None;
        private Brush _currentColor = Brushes.Red;

        private readonly List<UIElement> _addedElements = new();
        private bool _isDrawing;
        private Point _startPoint;
        private Polyline? _currentStroke;
        private Shape? _currentShape;

        // Datos originales de la imagen, para recalcular los ajustes de
        // Brillo/Contraste/etc. siempre desde cero (nunca acumulando
        // sobre un resultado ya procesado, que iría perdiendo calidad).
        private ushort[]? _originalGray16;   // si la imagen es una radiografía (Gray16)
        private byte[]? _originalColorBgra;  // si es una foto a color (cámara intraoral)
        private int _pixelWidth;
        private int _pixelHeight;
        private bool _suppressAdjustmentEvents;

        // Ya no se usa una curva fija: ApplyAutoStandardize() calcula
        // brillo/contraste según las estadísticas de CADA captura, así
        // que el tono queda consistente entre disparos sin necesidad de
        // un gamma fijo adicional (que se sumaría de forma impredecible).
        private const double DefaultBrightnessGamma = 1.0;

        public bool WasSaved { get; private set; }

        public ImageAnnotationWindow(
            string imagePath, (string SlotId, int ToothNumber, string ImageType)? remoteSlotInfo = null)
        {
            InitializeComponent();
            _imagePath = imagePath;
            _remoteSlotInfo = remoteSlotInfo;
            Loaded += (_, _) => LoadImage();
        }

        private void LoadImage()
        {
            // Se lee el archivo completo a memoria antes de crear el
            // BitmapImage, para no dejarlo bloqueado — de lo contrario,
            // "Guardar" fallaría al intentar sobrescribir un archivo que
            // esta misma ventana sigue teniendo abierto. Si la imagen
            // viene del servidor (por ejemplo, al revisar el historial
            // de un paciente desde otra PC), se descarga en vez de leer
            // un archivo local.
            byte[] fileBytes = _imagePath.StartsWith("http://") || _imagePath.StartsWith("https://")
                ? new System.Net.Http.HttpClient().GetByteArrayAsync(_imagePath).GetAwaiter().GetResult()
                : File.ReadAllBytes(_imagePath);

            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(fileBytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();

            _pixelWidth = bitmap.PixelWidth;
            _pixelHeight = bitmap.PixelHeight;

            if (bitmap.Format == PixelFormats.Gray16)
            {
                // Radiografía: se guardan los 16 bits completos para
                // poder ajustar brillo/contraste con toda la precisión
                // original, no solo sobre una versión ya achatada a 8 bits.
                int stride = _pixelWidth * 2;
                var rawBytes = new byte[stride * _pixelHeight];
                bitmap.CopyPixels(rawBytes, stride, 0);

                _originalGray16 = new ushort[_pixelWidth * _pixelHeight];
                for (int i = 0; i < _originalGray16.Length; i++)
                    _originalGray16[i] = BitConverter.ToUInt16(rawBytes, i * 2);

                _originalColorBgra = null;
            }
            else
            {
                // Foto a color (cámara intraoral): se trabaja en Bgra32.
                var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
                int stride = _pixelWidth * 4;
                _originalColorBgra = new byte[stride * _pixelHeight];
                converted.CopyPixels(_originalColorBgra, stride, 0);
                _originalGray16 = null;
            }

            // Tamaño de despliegue: la imagen completa, pero sin pasarse
            // del espacio disponible en la ventana — así el Canvas de
            // anotaciones queda exactamente del mismo tamaño que la
            // imagen mostrada, sin desalinearse.
            const double maxWidth = 720;
            const double maxHeight = 560;
            double scale = Math.Min(maxWidth / _pixelWidth, maxHeight / _pixelHeight);
            scale = Math.Min(scale, 1.0);

            ImageHost.Width = _pixelWidth * scale;
            ImageHost.Height = _pixelHeight * scale;

            // El negativo por defecto solo tiene sentido para
            // radiografías (nuestro sensor entrega la polaridad
            // invertida respecto a la convención de rayos X). Las fotos
            // de la cámara intraoral son color normal, sin invertir.
            _suppressAdjustmentEvents = true;
            NegativeCheckBox.IsChecked = _originalGray16 != null;
            _suppressAdjustmentEvents = false;

            // Primero se dibuja siempre algo (foto a color o radiografía),
            // para que nunca quede la pantalla en negro. La estandarización
            // automática (que solo aplica a radiografías en escala de
            // grises) se hace después, y ya vuelve a dibujar con los
            // valores ajustados si corresponde.
            RenderAdjustedImage();
            if (_originalGray16 != null)
                ApplyAutoStandardize();
        }

        // ------------------------------------------------------------------
        // Ajustes de imagen (Brillo / Contraste / Afilar / Suavizar / Negativo)
        // ------------------------------------------------------------------

        private void OnAdjustmentChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressAdjustmentEvents) return;
            RenderAdjustedImage();
        }

        private void OnAdjustmentChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressAdjustmentEvents) return;
            RenderAdjustedImage();
        }

        private void OnResetAdjustmentsClick(object sender, RoutedEventArgs e)
        {
            SetSlidersWithoutTriggering(brightness: 0, contrast: 0, sharpen: 0, smooth: 0, negative: false);
            RenderAdjustedImage();
        }

        private void OnAutoStandardizeClick(object sender, RoutedEventArgs e)
        {
            ApplyAutoStandardize();
        }

        /// <summary>
        /// Calcula brillo/contraste automáticos para que la imagen quede
        /// con un tono/nivel consistente sin importar cómo haya sido la
        /// exposición o el encuadre de este disparo en particular -- esto
        /// es lo que evita que dos capturas reales del mismo diente se
        /// vean con brillos muy distintos entre sí.
        /// </summary>
        private void ApplyAutoStandardize()
        {
            if (_originalGray16 == null)
                return; // estandarización solo aplica a radiografías (escala de grises)

            double mean = 0;
            foreach (var v in _originalGray16) mean += v;
            mean /= _originalGray16.Length;

            double variance = 0;
            foreach (var v in _originalGray16) variance += (v - mean) * (v - mean);
            double std = Math.Sqrt(variance / _originalGray16.Length);

            double targetMeanNorm = 0.5;
            double targetStdNorm = 0.18;
            double currentMeanNorm = mean / 65535.0;
            double currentStdNorm = std / 65535.0;

            double contrast = currentStdNorm < 1e-6 ? 0 : (targetStdNorm / currentStdNorm - 1.0) * 100.0;
            double brightness = (targetMeanNorm - currentMeanNorm) * 200.0;

            contrast = Math.Clamp(contrast, -100, 100);
            brightness = Math.Clamp(brightness, -100, 100);

            SetSlidersWithoutTriggering(brightness, contrast,
                SharpenSlider.Value, SmoothSlider.Value, NegativeCheckBox.IsChecked == true);
            RenderAdjustedImage();
        }

        private void SetSlidersWithoutTriggering(double brightness, double contrast, double sharpen, double smooth, bool negative)
        {
            _suppressAdjustmentEvents = true;
            BrightnessSlider.Value = brightness;
            ContrastSlider.Value = contrast;
            SharpenSlider.Value = sharpen;
            SmoothSlider.Value = smooth;
            NegativeCheckBox.IsChecked = negative;
            _suppressAdjustmentEvents = false;
        }

        private void RenderAdjustedImage()
        {
            if (_originalGray16 != null)
                RenderAdjustedGrayscale();
            else if (_originalColorBgra != null)
                RenderAdjustedColor();
        }

        private void RenderAdjustedGrayscale()
        {
            int n = _pixelWidth * _pixelHeight;
            var norm = new double[n];
            for (int i = 0; i < n; i++)
                norm[i] = Math.Pow(_originalGray16![i] / 65535.0, DefaultBrightnessGamma);

            double contrast = ContrastSlider.Value / 100.0;
            double brightness = BrightnessSlider.Value / 100.0 * 0.5;
            for (int i = 0; i < n; i++)
            {
                double v = (norm[i] - 0.5) * (1 + contrast) + 0.5 + brightness;
                norm[i] = Math.Clamp(v, 0, 1);
            }

            double sharpenAmount = SharpenSlider.Value;
            if (sharpenAmount > 0)
                norm = Sharpen(norm, _pixelWidth, _pixelHeight, sharpenAmount);

            int smoothRadius = (int)SmoothSlider.Value;
            if (smoothRadius > 0)
                norm = BoxBlur(norm, _pixelWidth, _pixelHeight, smoothRadius);

            bool negative = NegativeCheckBox.IsChecked == true;

            var pixels8 = new byte[n];
            for (int i = 0; i < n; i++)
            {
                double v = Math.Clamp(norm[i], 0, 1);
                if (negative) v = 1 - v;
                pixels8[i] = (byte)(v * 255);
            }

            var bitmap = BitmapSource.Create(
                _pixelWidth, _pixelHeight, 96, 96, PixelFormats.Gray8, null, pixels8, _pixelWidth);
            bitmap.Freeze();
            BaseImage.Source = bitmap;
        }

        private void RenderAdjustedColor()
        {
            int n = _pixelWidth * _pixelHeight;
            var pixels = (byte[])_originalColorBgra!.Clone();

            double contrast = ContrastSlider.Value / 100.0;
            double brightness = BrightnessSlider.Value / 100.0 * 0.5 * 255;
            bool negative = NegativeCheckBox.IsChecked == true;

            for (int i = 0; i < n; i++)
            {
                int baseIdx = i * 4;
                for (int c = 0; c < 3; c++) // B, G, R -- se deja Alpha (índice 3) sin tocar
                {
                    double v = pixels[baseIdx + c];
                    v = (v - 127.5) * (1 + contrast) + 127.5 + brightness;
                    v = Math.Clamp(v, 0, 255);
                    if (negative) v = 255 - v;
                    pixels[baseIdx + c] = (byte)v;
                }
            }

            int stride = _pixelWidth * 4;
            var bitmap = BitmapSource.Create(
                _pixelWidth, _pixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            bitmap.Freeze();
            BaseImage.Source = bitmap;
        }

        /// <summary>Afilado por "unsharp mask": resalta bordes restando una
        /// versión suavizada de la imagen a la original.</summary>
        private static double[] Sharpen(double[] arr, int width, int height, double amount)
        {
            var blurred = BoxBlur(arr, width, height, 1);
            var result = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++)
                result[i] = Math.Clamp(arr[i] + amount * (arr[i] - blurred[i]), 0, 1);
            return result;
        }

        /// <summary>Blur de caja usando sumas acumuladas (rápido incluso con
        /// radios grandes, sin dependencias externas).</summary>
        private static double[] BoxBlur(double[] arr, int width, int height, int radius)
        {
            if (radius < 1) return arr;

            var horizontal = new double[width * height];
            for (int y = 0; y < height; y++)
            {
                double sum = 0;
                for (int x = -radius; x <= radius; x++)
                {
                    int xi = Math.Clamp(x, 0, width - 1);
                    sum += arr[y * width + xi];
                }
                for (int x = 0; x < width; x++)
                {
                    horizontal[y * width + x] = sum / (2 * radius + 1);
                    int addX = Math.Clamp(x + radius + 1, 0, width - 1);
                    int removeX = Math.Clamp(x - radius, 0, width - 1);
                    sum += arr[y * width + addX] - arr[y * width + removeX];
                }
            }

            var result = new double[width * height];
            for (int x = 0; x < width; x++)
            {
                double sum = 0;
                for (int y = -radius; y <= radius; y++)
                {
                    int yi = Math.Clamp(y, 0, height - 1);
                    sum += horizontal[yi * width + x];
                }
                for (int y = 0; y < height; y++)
                {
                    result[y * width + x] = sum / (2 * radius + 1);
                    int addY = Math.Clamp(y + radius + 1, 0, height - 1);
                    int removeY = Math.Clamp(y - radius, 0, height - 1);
                    sum += horizontal[addY * width + x] - horizontal[removeY * width + x];
                }
            }

            return result;
        }

        private void OnToolSelected(object sender, RoutedEventArgs e)
        {
            // Solo una herramienta activa a la vez.
            foreach (var button in new[] { DrawToolButton, RectangleToolButton, CircleToolButton, TextToolButton })
            {
                if (button != sender)
                    button.IsChecked = false;
            }

            if (sender == DrawToolButton) _currentTool = DrawToolButton.IsChecked == true ? ToolMode.Draw : ToolMode.None;
            else if (sender == RectangleToolButton) _currentTool = RectangleToolButton.IsChecked == true ? ToolMode.Rectangle : ToolMode.None;
            else if (sender == CircleToolButton) _currentTool = CircleToolButton.IsChecked == true ? ToolMode.Circle : ToolMode.None;
            else if (sender == TextToolButton) _currentTool = TextToolButton.IsChecked == true ? ToolMode.Text : ToolMode.None;
        }

        private void OnColorChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPicker.SelectedItem is ComboBoxItem { Tag: string colorName })
            {
                var converted = new BrushConverter().ConvertFromString(colorName);
                if (converted is Brush brush)
                    _currentColor = brush;
            }
        }

        private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(AnnotationCanvas);

            switch (_currentTool)
            {
                case ToolMode.Draw:
                    _isDrawing = true;
                    _currentStroke = new Polyline
                    {
                        Stroke = _currentColor,
                        StrokeThickness = 3,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                    };
                    _currentStroke.Points.Add(_startPoint);
                    AnnotationCanvas.Children.Add(_currentStroke);
                    _addedElements.Add(_currentStroke);
                    break;

                case ToolMode.Rectangle:
                    _isDrawing = true;
                    _currentShape = new Rectangle
                    {
                        Stroke = _currentColor,
                        StrokeThickness = 3,
                    };
                    Canvas.SetLeft(_currentShape, _startPoint.X);
                    Canvas.SetTop(_currentShape, _startPoint.Y);
                    AnnotationCanvas.Children.Add(_currentShape);
                    _addedElements.Add(_currentShape);
                    break;

                case ToolMode.Circle:
                    _isDrawing = true;
                    _currentShape = new Ellipse
                    {
                        Stroke = _currentColor,
                        StrokeThickness = 3,
                    };
                    Canvas.SetLeft(_currentShape, _startPoint.X);
                    Canvas.SetTop(_currentShape, _startPoint.Y);
                    AnnotationCanvas.Children.Add(_currentShape);
                    _addedElements.Add(_currentShape);
                    break;

                case ToolMode.Text:
                    StartTextInput(_startPoint);
                    break;
            }
        }

        private void OnCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDrawing)
                return;

            var current = e.GetPosition(AnnotationCanvas);

            if (_currentTool == ToolMode.Draw && _currentStroke != null)
            {
                _currentStroke.Points.Add(current);
            }
            else if (_currentShape != null)
            {
                double x = Math.Min(_startPoint.X, current.X);
                double y = Math.Min(_startPoint.Y, current.Y);
                double width = Math.Abs(current.X - _startPoint.X);
                double height = Math.Abs(current.Y - _startPoint.Y);

                Canvas.SetLeft(_currentShape, x);
                Canvas.SetTop(_currentShape, y);
                _currentShape.Width = width;
                _currentShape.Height = height;
            }
        }

        private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = false;
            _currentStroke = null;
            _currentShape = null;
        }

        private void StartTextInput(Point position)
        {
            var textBox = new TextBox
            {
                MinWidth = 120,
                FontSize = 18,
                Background = Brushes.White,
                BorderBrush = _currentColor,
                BorderThickness = new Thickness(1),
            };
            Canvas.SetLeft(textBox, position.X);
            Canvas.SetTop(textBox, position.Y);
            AnnotationCanvas.Children.Add(textBox);
            textBox.Focus();

            void Commit()
            {
                AnnotationCanvas.Children.Remove(textBox);
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    var textBlock = new TextBlock
                    {
                        Text = textBox.Text,
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = _currentColor,
                    };
                    Canvas.SetLeft(textBlock, position.X);
                    Canvas.SetTop(textBlock, position.Y);
                    AnnotationCanvas.Children.Add(textBlock);
                    _addedElements.Add(textBlock);
                }
            }

            textBox.LostFocus += (_, _) => Commit();
            textBox.KeyDown += (_, keyArgs) =>
            {
                if (keyArgs.Key == Key.Enter)
                {
                    Commit();
                    keyArgs.Handled = true;
                }
            };
        }

        private void OnUndoClick(object sender, RoutedEventArgs e)
        {
            if (_addedElements.Count == 0)
                return;

            var last = _addedElements[^1];
            _addedElements.RemoveAt(_addedElements.Count - 1);
            AnnotationCanvas.Children.Remove(last);
        }

        private void OnClearAllClick(object sender, RoutedEventArgs e)
        {
            AnnotationCanvas.Children.Clear();
            _addedElements.Clear();
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var renderTarget = new RenderTargetBitmap(
                (int)ImageHost.ActualWidth, (int)ImageHost.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(ImageHost);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            bool isRemote = _imagePath.StartsWith("http://") || _imagePath.StartsWith("https://");

            if (isRemote)
            {
                // La imagen original vive en el servidor (viene del
                // historial, no de la sesión actual) — se renderiza a un
                // archivo temporal, se sube de vuelta, y se actualiza el
                // registro de esa captura con la nueva versión anotada.
                if (_remoteSlotInfo == null)
                {
                    MessageBox.Show(
                        "No se pudo determinar a qué captura pertenece esta imagen remota.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string tempPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"anotada_{Guid.NewGuid():N}.png");
                using (var fileStream = new FileStream(tempPath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }

                try
                {
                    var api = new Api.ApiClient();
                    string newUrl = await api.UploadImageFileAsync(tempPath);
                    var (slotId, toothNumber, imageType) = _remoteSlotInfo.Value;
                    await api.UpdateSlotImageAsync(slotId, toothNumber, imageType, newUrl);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo subir la imagen anotada: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                finally
                {
                    try { File.Delete(tempPath); } catch { /* no es crítico si no se puede borrar */ }
                }
            }
            else
            {
                using (var fileStream = new FileStream(_imagePath, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }

            WasSaved = true;
            DialogResult = true;
        }

        private void OnCloseWithoutSavingClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
