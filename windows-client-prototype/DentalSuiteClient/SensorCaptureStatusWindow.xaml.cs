using System;
using System.Threading;
using System.Windows;

namespace DentalSuiteClient
{
    /// <summary>
    /// Ventana pequeña, sin botones de la app detrás, que muestra el
    /// progreso de una captura de sensor ("Esperando disparo...",
    /// "Transfiriendo imagen...") — igual que hace el software oficial
    /// de Runyes con su diálogo "Sensor". Se actualiza desde el hilo de
    /// captura (que corre en background) vía UpdateStatus, y permite
    /// cancelar la espera del disparo.
    /// </summary>
    public partial class SensorCaptureStatusWindow : Window
    {
        public CancellationTokenSource CancellationSource { get; } = new();

        public SensorCaptureStatusWindow()
        {
            InitializeComponent();
        }

        /// <summary>Seguro de llamar desde cualquier hilo.</summary>
        public void UpdateStatus(string text)
        {
            Dispatcher.Invoke(() => StatusText.Text = text);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            CancellationSource.Cancel();
            Close();
        }
    }
}
