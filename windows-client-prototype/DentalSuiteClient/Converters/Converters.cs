using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DentalSuiteClient.Models;

namespace DentalSuiteClient.Converters
{
    /// <summary>Convierte entre el enum DentitionType y el radio button
    /// correspondiente (Permanent = "Diente", Deciduous = "Diente Infantil").</summary>
    public sealed class DentitionTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DentitionType current && parameter is string target)
                return current.ToString() == target;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string target)
                return Enum.Parse<DentitionType>(target);
            return Binding.DoNothing;
        }
    }

    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>Habilita un botón solo cuando hay algo seleccionado en una lista.</summary>
    public sealed class NotNullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value != null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>Oculta un elemento cuando una cuenta es mayor a cero (para
    /// el mensaje de "selecciona un diente" que desaparece en cuanto hay
    /// al menos una casilla de captura).</summary>
    public sealed class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Carga una imagen desde disco SIN dejar el archivo bloqueado — lee
    /// todos los bytes de una vez y construye el BitmapImage desde
    /// memoria, en vez de apuntar directo al archivo con UriSource (que
    /// puede mantenerlo abierto mientras la imagen siga en pantalla).
    /// Sin esto, el editor de imágenes no puede sobrescribir un archivo
    /// que alguna otra ventana ya tiene mostrado.
    /// </summary>
    public sealed class ImagePathToBitmapConverter : IValueConverter
    {
        private static readonly System.Net.Http.HttpClient _httpClient = new();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrEmpty(path))
                return null;

            try
            {
                byte[] bytes;
                if (path.StartsWith("http://") || path.StartsWith("https://"))
                {
                    // Viene del servidor (por ejemplo, al ver el
                    // historial de otro paciente desde otra PC o desde
                    // el celular) — se descarga en vez de abrir un
                    // archivo local.
                    bytes = _httpClient.GetByteArrayAsync(path).GetAwaiter().GetResult();
                }
                else
                {
                    if (!System.IO.File.Exists(path))
                        return null;
                    bytes = System.IO.File.ReadAllBytes(path);
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                using var stream = new System.IO.MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
