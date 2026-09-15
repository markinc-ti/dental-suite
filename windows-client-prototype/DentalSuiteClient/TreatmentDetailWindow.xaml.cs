using System;
using System.Linq;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class TreatmentDetailWindow : Window
    {
        private readonly ApiClient _api = new();
        private readonly string _treatmentId;

        public TreatmentDetailWindow(string treatmentId)
        {
            InitializeComponent();
            _treatmentId = treatmentId;
            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                var treatment = await _api.GetTreatmentAsync(_treatmentId);

                TitleText.Text = treatment.Name;
                SubtitleText.Text = treatment.EstimatedCost.HasValue
                    ? $"Costo estimado: ${treatment.EstimatedCost.Value:N2}"
                    : "Sin costo estimado registrado";

                int completed = treatment.Sessions.Count(s => s.Status == "completed");
                int total = treatment.TotalSessions;
                ProgressText.Text = $"{completed} / {total} sesiones completadas";
                ProgressBarControl.Value = total == 0 ? 0 : (100.0 * completed / total);

                SessionsList.ItemsSource = treatment.Sessions
                    .OrderBy(s => s.SessionNumber)
                    .Select(s => new TreatmentSessionRow { Session = s })
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar el tratamiento: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnRescheduleSessionClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: TreatmentSessionRow row })
                return;

            var dialog = new PickDateTimeDialog(
                $"Nueva fecha para la sesión {row.SessionNumber}", row.Session.ScheduledDate) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _api.RescheduleSessionAsync(row.Session.Id, dialog.SelectedDateTime);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo reprogramar la sesión: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnAddExtraSessionClick(object sender, RoutedEventArgs e)
        {
            var dialog = new PickDateTimeDialog(
                "Fecha de la sesión extra", DateTime.Today, showNotes: true) { Owner = this };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _api.AddExtraSessionAsync(_treatmentId, dialog.SelectedDateTime, 30, dialog.Notes);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo agregar la sesión extra: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnCompleteSessionClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: TreatmentSessionRow row })
                return;

            try
            {
                await _api.CompleteSessionAsync(row.Session.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo marcar la sesión como completada: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnCancelTreatmentClick(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "¿Cancelar este tratamiento? Las sesiones pendientes se quedarán como están, "
                + "pero el tratamiento se marcará como cancelado.",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await _api.UpdateTreatmentStatusAsync(_treatmentId, "cancelled");
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cancelar el tratamiento: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
