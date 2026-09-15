using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class EditAppointmentDialog : Window
    {
        private readonly ApiClient _api = new();
        private readonly AppointmentRow _appointment;

        public EditAppointmentDialog(AppointmentRow appointment)
        {
            InitializeComponent();
            _appointment = appointment;

            PatientText.Text = appointment.PatientName;
            DatePickerControl.SelectedDate = appointment.StartsAt.Date;
            StartTimeBox.Text = appointment.StartsAt.ToString("HH:mm");
            DurationBox.Text = ((int)(appointment.EndsAt - appointment.StartsAt).TotalMinutes).ToString();
            ReasonBox.Text = appointment.Reason ?? "";

            Loaded += async (_, _) => await LoadDoctorsAsync();
        }

        private async System.Threading.Tasks.Task LoadDoctorsAsync()
        {
            var doctors = await _api.ListDoctorsAsync();
            DoctorComboBox.ItemsSource = doctors;

            var current = doctors.FirstOrDefault(d => d.Id == _appointment.DentistId);
            DoctorComboBox.SelectedItem = current ?? doctors.FirstOrDefault();
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (DoctorComboBox.SelectedItem is not UserDto doctor)
            {
                MessageBox.Show("Selecciona un doctor.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DatePickerControl.SelectedDate is not DateTime date)
            {
                MessageBox.Show("Selecciona una fecha.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TimeSpan.TryParseExact(StartTimeBox.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var startTime))
            {
                MessageBox.Show("La hora debe tener el formato HH:mm, por ejemplo 10:00.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(DurationBox.Text.Trim(), out int durationMinutes) || durationMinutes < 1)
            {
                MessageBox.Show("La duración debe ser un entero mayor a cero.", "Formato inválido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var startsAt = date.Date + startTime;
            var endsAt = startsAt.AddMinutes(durationMinutes);
            string? reason = string.IsNullOrWhiteSpace(ReasonBox.Text) ? null : ReasonBox.Text.Trim();

            try
            {
                await _api.UpdateAppointmentAsync(_appointment.Id, doctor.Id, startsAt, endsAt, reason);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo guardar la cita: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
