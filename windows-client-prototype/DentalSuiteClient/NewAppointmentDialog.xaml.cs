using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class NewAppointmentDialog : Window
    {
        private readonly ApiClient _api = new();

        public string? SelectedPatientId { get; private set; }
        public string? SelectedDentistId { get; private set; }
        public DateTime StartsAt { get; private set; }
        public DateTime EndsAt { get; private set; }
        public string? Reason { get; private set; }

        public NewAppointmentDialog(DateTime defaultDay)
        {
            InitializeComponent();
            DatePickerControl.SelectedDate = defaultDay.Date;
            Loaded += async (_, _) => await LoadDoctorsAsync();
        }

        private async System.Threading.Tasks.Task LoadDoctorsAsync()
        {
            var doctors = await _api.ListDoctorsAsync();
            DoctorComboBox.ItemsSource = doctors;
            if (doctors.Count > 0)
                DoctorComboBox.SelectedIndex = 0;
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e) =>
            await SearchAsync();

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

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (PatientList.SelectedItem is not PatientDto patient)
            {
                MessageBox.Show("Selecciona un paciente de la lista.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DoctorComboBox.SelectedItem is not UserDto doctor)
            {
                MessageBox.Show(
                    "Todavía no hay ningún doctor registrado. Ve a la pantalla "
                    + "de Doctores y crea uno antes de agendar una cita.",
                    "Falta información", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DatePickerControl.SelectedDate is not DateTime day)
            {
                MessageBox.Show("Selecciona una fecha.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TimeSpan.TryParseExact(StartTimeBox.Text.Trim(), "hh\\:mm",
                    CultureInfo.InvariantCulture, out var startTime))
            {
                MessageBox.Show("La hora de inicio debe tener el formato HH:mm, por ejemplo 09:00.",
                    "Formato inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(DurationBox.Text.Trim(), out int durationMinutes) || durationMinutes <= 0)
            {
                MessageBox.Show("La duración debe ser un número de minutos mayor a cero.",
                    "Formato inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedPatientId = patient.Id;
            SelectedDentistId = doctor.Id;
            StartsAt = day.Date + startTime;
            EndsAt = StartsAt.AddMinutes(durationMinutes);
            Reason = string.IsNullOrWhiteSpace(ReasonBox.Text) ? null : ReasonBox.Text.Trim();

            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
