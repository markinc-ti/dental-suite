using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class AppointmentsWindow : Window
    {
        private readonly ApiClient _api = new();

        public AppointmentsWindow()
        {
            InitializeComponent();
            DayPicker.SelectedDate = DateTime.Today;
            Loaded += async (_, _) => await LoadAppointmentsAsync();
        }

        private async System.Threading.Tasks.Task LoadAppointmentsAsync()
        {
            try
            {
                var day = DayPicker.SelectedDate ?? DateTime.Today;
                var dayStart = day.Date;
                var dayEnd = dayStart.AddDays(1).AddTicks(-1);

                var appointments = await _api.ListAppointmentsAsync(dayStart, dayEnd);

                // Resolvemos el nombre del paciente por cada cita. Para el
                // volumen de una agenda diaria de un consultorio esto es
                // más que suficiente; si el listado creciera mucho valdría
                // la pena que el backend devolviera el nombre ya incluido.
                var rows = new List<AppointmentRow>();
                foreach (var appt in appointments.OrderBy(a => a.StartsAt))
                {
                    string patientName = await ResolvePatientNameAsync(appt.PatientId);
                    string doctorName = await ResolveDoctorNameAsync(appt.DentistId);
                    rows.Add(new AppointmentRow
                    {
                        Id = appt.Id ?? "",
                        StartsAt = appt.StartsAt,
                        EndsAt = appt.EndsAt,
                        PatientName = patientName,
                        DoctorName = doctorName,
                        Reason = appt.Reason,
                        Status = appt.Status,
                    });
                }

                AppointmentsList.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo cargar la agenda: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private readonly Dictionary<string, string> _patientNameCache = new();
        private readonly Dictionary<string, string> _doctorNameCache = new();

        private async System.Threading.Tasks.Task<string> ResolvePatientNameAsync(string patientId)
        {
            if (_patientNameCache.TryGetValue(patientId, out var cached))
                return cached;

            var patients = await _api.SearchPatientsAsync();
            foreach (var p in patients)
            {
                if (p.Id != null)
                    _patientNameCache[p.Id] = p.FullName;
            }

            return _patientNameCache.TryGetValue(patientId, out var name) ? name : "(paciente desconocido)";
        }

        private async System.Threading.Tasks.Task<string> ResolveDoctorNameAsync(string dentistId)
        {
            if (_doctorNameCache.TryGetValue(dentistId, out var cached))
                return cached;

            var doctors = await _api.ListDoctorsAsync();
            foreach (var d in doctors)
                _doctorNameCache[d.Id] = d.FullName;

            return _doctorNameCache.TryGetValue(dentistId, out var name) ? name : "(doctor desconocido)";
        }

        private async void OnDayChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
            await LoadAppointmentsAsync();

        private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
            await LoadAppointmentsAsync();

        private async void OnNewAppointmentClick(object sender, RoutedEventArgs e)
        {
            var day = DayPicker.SelectedDate ?? DateTime.Today;
            var dialog = new NewAppointmentDialog(day) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _api.CreateAppointmentAsync(
                        dialog.SelectedPatientId!, dialog.SelectedDentistId!, dialog.StartsAt, dialog.EndsAt, dialog.Reason);
                    await LoadAppointmentsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo crear la cita: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
