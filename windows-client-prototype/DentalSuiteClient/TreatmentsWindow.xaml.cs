using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class TreatmentsWindow : Window
    {
        private readonly ApiClient _api = new();
        private readonly Dictionary<string, string> _patientNameCache = new();
        private readonly Dictionary<string, string> _doctorNameCache = new();

        public TreatmentsWindow()
        {
            InitializeComponent();
            TreatmentTypesButton.Visibility = CurrentSession.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            Loaded += async (_, _) => await LoadTreatmentsAsync();
        }

        private async System.Threading.Tasks.Task LoadTreatmentsAsync()
        {
            try
            {
                var treatments = await _api.ListTreatmentsAsync();

                var patients = await _api.SearchPatientsAsync();
                foreach (var p in patients)
                    if (p.Id != null) _patientNameCache[p.Id] = p.FullName;

                var doctors = await _api.ListDoctorsAsync();
                foreach (var d in doctors)
                    _doctorNameCache[d.Id] = d.FullName;

                var rows = treatments.Select(t => new TreatmentRow
                {
                    Treatment = t,
                    PatientName = _patientNameCache.TryGetValue(t.PatientId, out var pn) ? pn : "(desconocido)",
                    DoctorName = _doctorNameCache.TryGetValue(t.DentistId, out var dn) ? dn : "(desconocido)",
                }).ToList();

                TreatmentsList.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar los tratamientos: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
            await LoadTreatmentsAsync();

        private async void OnNewTreatmentClick(object sender, RoutedEventArgs e)
        {
            var dialog = new NewTreatmentDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                await LoadTreatmentsAsync();
            }
        }

        private void OnManageTypesClick(object sender, RoutedEventArgs e)
        {
            var window = new TreatmentTypesWindow { Owner = this };
            window.ShowDialog();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnTreatmentDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (TreatmentsList.SelectedItem is not TreatmentRow row)
                return;

            var detail = new TreatmentDetailWindow(row.Treatment.Id!) { Owner = this };
            detail.ShowDialog();
            await LoadTreatmentsAsync();
        }
    }
}
