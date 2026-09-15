using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class NewTreatmentDialog : Window
    {
        private readonly ApiClient _api = new();

        public NewTreatmentDialog()
        {
            InitializeComponent();
            StartDatePicker.SelectedDate = DateTime.Today;
            Loaded += async (_, _) =>
            {
                await LoadDoctorsAsync();
                await LoadTreatmentTypesAsync();
            };
        }

        private async System.Threading.Tasks.Task LoadDoctorsAsync()
        {
            var doctors = await _api.ListDoctorsAsync();
            DoctorComboBox.ItemsSource = doctors;
            if (doctors.Count > 0)
                DoctorComboBox.SelectedIndex = 0;
        }

        private async System.Threading.Tasks.Task LoadTreatmentTypesAsync()
        {
            var types = await _api.ListTreatmentTypesAsync();
            TreatmentTypeComboBox.ItemsSource = types;
        }

        private void OnTreatmentTypeSelected(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TreatmentTypeComboBox.SelectedItem is not TreatmentTypeDto type)
                return;

            NameBox.Text = type.Name;
            SessionCountBox.Text = type.DefaultSessionCount.ToString();
            IntervalDaysBox.Text = type.DefaultIntervalDays.ToString();
            if (type.DefaultCost.HasValue)
                CostBox.Text = type.DefaultCost.Value.ToString(CultureInfo.InvariantCulture);
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e) => await SearchAsync();

        private async void OnSearchKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await SearchAsync();
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            var patients = await _api.SearchPatientsAsync(SearchBox.Text);
            PatientList.ItemsSource = patients;
        }

        private async void OnCreateClick(object sender, RoutedEventArgs e)
        {
            if (PatientList.SelectedItem is not PatientDto patient)
            {
                MessageBox.Show("Selecciona un paciente de la lista.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DoctorComboBox.SelectedItem is not UserDto doctor)
            {
                MessageBox.Show("Todavía no hay ningún doctor registrado.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string name = NameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("El nombre del tratamiento es obligatorio.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(SessionCountBox.Text.Trim(), out int sessionCount) || sessionCount < 1)
            {
                MessageBox.Show("El número de sesiones debe ser un entero mayor a cero.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(IntervalDaysBox.Text.Trim(), out int intervalDays) || intervalDays < 1)
            {
                MessageBox.Show("El intervalo de días debe ser un entero mayor a cero.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(DurationBox.Text.Trim(), out int durationMinutes) || durationMinutes < 1)
            {
                MessageBox.Show("La duración por sesión debe ser un entero mayor a cero.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StartDatePicker.SelectedDate is not DateTime startDate)
            {
                MessageBox.Show("Selecciona la fecha de la primera sesión.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal? cost = null;
            if (!string.IsNullOrWhiteSpace(CostBox.Text))
            {
                if (!decimal.TryParse(CostBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedCost))
                {
                    MessageBox.Show("El costo estimado no es un número válido.", "Formato inválido",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                cost = parsedCost;
            }

            string startTime = StartTimeBox.Text.Trim();
            if (!TimeSpan.TryParseExact(startTime, "hh\\:mm", CultureInfo.InvariantCulture, out _))
            {
                MessageBox.Show("La hora debe tener el formato HH:mm, por ejemplo 10:00.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? treatmentTypeId = (TreatmentTypeComboBox.SelectedItem as TreatmentTypeDto)?.Id;
            string? notes = string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim();

            try
            {
                await _api.CreateTreatmentAsync(
                    patient.Id!, doctor.Id, treatmentTypeId, name, cost,
                    sessionCount, intervalDays, startDate, startTime, durationMinutes, notes);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo crear el tratamiento: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
