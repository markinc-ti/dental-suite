using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DentalSuiteClient
{
    public enum CaptureImageType
    {
        Xray,
        Photo,
        Video,
    }

    /// <summary>
    /// Una casilla de captura en la galería: un diente + un tipo de
    /// imagen (radiografía, foto o video). Cada diente seleccionado en
    /// el odontograma genera una de estas por tipo, para que se puedan
    /// capturar y comparar por separado.
    /// </summary>
    public sealed class CaptureItem : INotifyPropertyChanged
    {
        public int ToothNumber { get; init; }
        public int DisplayNumber { get; init; }
        public CaptureImageType ImageType { get; init; }

        /// <summary>Id del registro en el servidor, una vez subido (null
        /// mientras no se haya sincronizado, por ejemplo sin internet).
        /// Se necesita para poder borrar o re-editar esta captura
        /// específica más adelante.</summary>
        public string? SlotId { get; set; }

        public string TypeLabel => ImageType switch
        {
            CaptureImageType.Xray => "Radiografía",
            CaptureImageType.Video => "Video",
            _ => "Foto",
        };

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

        /// <summary>
        /// Solo para ImageType == Video: ruta del archivo .avi real. La
        /// miniatura que se ve en la galería (ImagePath) es una imagen
        /// del primer cuadro del video, para que use la misma vista que
        /// las demás casillas -- esta ruta es la que se abre al hacer
        /// doble clic.
        /// </summary>
        private string? _videoPath;
        public string? VideoPath
        {
            get => _videoPath;
            set { _videoPath = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
