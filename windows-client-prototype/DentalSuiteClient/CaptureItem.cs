using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DentalSuiteClient
{
    public enum CaptureImageType
    {
        Xray,
        Photo,
    }

    /// <summary>
    /// Una casilla de captura en la galería: un diente + un tipo de
    /// imagen (radiografía o foto). Cada diente seleccionado en el
    /// odontograma genera DOS de estas — una para cada tipo — para que
    /// se puedan capturar y comparar por separado.
    /// </summary>
    public sealed class CaptureItem : INotifyPropertyChanged
    {
        public int ToothNumber { get; init; }
        public int DisplayNumber { get; init; }
        public CaptureImageType ImageType { get; init; }

        public string TypeLabel => ImageType == CaptureImageType.Xray ? "Radiografía" : "Foto";

        private bool _isCaptured;
        public bool IsCaptured
        {
            get => _isCaptured;
            set { _isCaptured = value; OnPropertyChanged(); }
        }

        private string? _imagePath;
        public string? ImagePath
        {
            get => _imagePath;
            set { _imagePath = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
