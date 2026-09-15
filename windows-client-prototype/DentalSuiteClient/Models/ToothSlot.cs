using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DentalSuiteClient.Models
{
    public enum DentitionType
    {
        Permanent,  // "Diente"
        Deciduous,  // "Diente Infantil"
    }

    /// <summary>
    /// Una posición del odontograma: número de diente (sistema universal
    /// 1-32), si está marcada para captura, y la miniatura resultante una
    /// vez capturada. Equivale a un checkbox + su imagen en la pantalla
    /// de Runyes.
    /// </summary>
    public sealed class ToothSlot : INotifyPropertyChanged
    {
        public int ToothNumber { get; init; }

        /// <summary>Número mostrado en pantalla, relativo al cuadrante
        /// (1 = incisivo central, 8 = muela del juicio) — distinto del
        /// ToothNumber interno (sistema universal 1-32) que se usa para
        /// hablar con el backend.</summary>
        public int DisplayNumber { get; init; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

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

        /// <summary>Posición 1-18 dentro de la miniatura, como en la
        /// captura original (7, 8, 1, 2, 3, 9, 10, ...).</summary>
        public int? ImageOrder { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
