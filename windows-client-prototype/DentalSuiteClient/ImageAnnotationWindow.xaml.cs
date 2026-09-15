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

            BaseImage.Source = bitmap;

            // Tamaño de despliegue: la imagen completa, pero sin pasarse
            // del espacio disponible en la ventana — así el Canvas de
            // anotaciones queda exactamente del mismo tamaño que la
            // imagen mostrada, sin desalinearse.
            const double maxWidth = 940;
            const double maxHeight = 560;
            double scale = Math.Min(maxWidth / bitmap.PixelWidth, maxHeight / bitmap.PixelHeight);
            scale = Math.Min(scale, 1.0);

            ImageHost.Width = bitmap.PixelWidth * scale;
            ImageHost.Height = bitmap.PixelHeight * scale;
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
