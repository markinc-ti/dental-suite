using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DentalSuiteClient.Models;

namespace DentalSuiteClient
{
    /// <summary>
    /// Arma las mismas cuatro filas de 8 que se ven en la pantalla de
    /// Runyes:
    ///   Fila superior:  R  1  2  3  4  5  6  7  8   |   9 10 11 12 13 14 15 16  L
    ///   Fila inferior:  R 32 31 30 29 28 27 26 25   |  24 23 22 21 20 19 18 17  L
    /// (arcada superior a la izquierda de la pantalla, inferior a la derecha,
    /// tal como en el chart real de la captura).
    /// </summary>
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ToothSlot> UpperRightTeeth { get; } = new();
        public ObservableCollection<ToothSlot> UpperLeftTeeth { get; } = new();
        public ObservableCollection<ToothSlot> LowerRightTeeth { get; } = new();
        public ObservableCollection<ToothSlot> LowerLeftTeeth { get; } = new();

        /// <summary>
        /// Las casillas de captura que aparecen abajo: por cada diente
        /// marcado arriba en el odontograma aparecen DOS casillas —una
        /// para radiografía y otra para foto— para poder capturar y
        /// comparar cada una por separado, sin que una sobreescriba a la
        /// otra.
        /// </summary>
        public ObservableCollection<CaptureItem> CaptureSlots { get; } = new();

        private DentitionType _dentitionType = DentitionType.Permanent;
        public DentitionType DentitionType
        {
            get => _dentitionType;
            set { _dentitionType = value; OnPropertyChanged(); }
        }

        private string _patientName = "";
        public string PatientName
        {
            get => _patientName;
            set { _patientName = value; OnPropertyChanged(); }
        }

        private string _patientId = "";
        public string PatientId
        {
            get => _patientId;
            set { _patientId = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            for (int n = 1; n <= 8; n++) UpperRightTeeth.Add(new ToothSlot { ToothNumber = n, DisplayNumber = 9 - n });
            for (int n = 9; n <= 16; n++) UpperLeftTeeth.Add(new ToothSlot { ToothNumber = n, DisplayNumber = n - 8 });
            // El orden visual en Runyes va de 32 a 25 de izquierda a derecha
            // del lado R, y de 24 a 17 del lado L.
            for (int n = 32; n >= 25; n--) LowerRightTeeth.Add(new ToothSlot { ToothNumber = n, DisplayNumber = n - 24 });
            for (int n = 24; n >= 17; n--) LowerLeftTeeth.Add(new ToothSlot { ToothNumber = n, DisplayNumber = 25 - n });

            foreach (var tooth in AllTeeth())
            {
                tooth.PropertyChanged += OnToothPropertyChanged;
            }
        }

        private void OnToothPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ToothSlot.IsSelected) || sender is not ToothSlot tooth)
                return;

            if (tooth.IsSelected)
            {
                CaptureSlots.Add(new CaptureItem
                {
                    ToothNumber = tooth.ToothNumber,
                    DisplayNumber = tooth.DisplayNumber,
                    ImageType = CaptureImageType.Xray,
                });
                CaptureSlots.Add(new CaptureItem
                {
                    ToothNumber = tooth.ToothNumber,
                    DisplayNumber = tooth.DisplayNumber,
                    ImageType = CaptureImageType.Photo,
                });
            }
            else
            {
                var toRemove = CaptureSlots.Where(c => c.ToothNumber == tooth.ToothNumber).ToList();
                foreach (var item in toRemove)
                    CaptureSlots.Remove(item);
            }
        }

        /// <summary>Todas las posiciones marcadas, en cualquiera de las cuatro
        /// cuadrantes, listas para enviar al sensor una por una.</summary>
        public System.Collections.Generic.IEnumerable<ToothSlot> AllTeeth() =>
            UpperRightTeeth.Concat(UpperLeftTeeth).Concat(LowerRightTeeth).Concat(LowerLeftTeeth);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
