using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class PatientSelectorWindow : Window
    {
        private readonly ApiClient _api = new();
        private bool _isNavigatingAway;
        private List<PatientRow> _allRows = new();

        public PatientSelectorWindow()
        {
            InitializeComponent();
            StatusFilterComboBox.SelectedIndex = 0;
            Loaded += async (_, _) => await LoadPatientsAsync();
            Loaded += (_, _) =>
            {
                CurrentUserText.Text = CurrentSession.User != null
                    ? $"{CurrentSession.User.FullName} ({(CurrentSession.IsAdmin ? "Administrador" : "Doctor")})"
                    : "";
                DoctorsButton.Visibility = CurrentSession.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                // El calendario lo ve cualquier rol; solo la gestión de
                // doctores queda exclusiva del administrador.
            };
            // La app ya no se cierra sola solo porque esta ventana se
            // cierre (ShutdownMode="OnExplicitShutdown", ver App.xaml) —
            // así podemos navegar cerrando y abriendo ventanas sin que
            // WPF apague todo por accidente en el instante en que no
            // queda ninguna visible. Pero si el usuario cierra esta
            // ventana con la X (no como parte de una navegación nuestra),
            // sí hay que salir del programa de verdad.
            Closed += (_, _) =>
            {
                if (!_isNavigatingAway)
                    Application.Current.Shutdown();
            };
        }

        private async System.Threading.Tasks.Task LoadPatientsAsync(string? search = null)
        {
            try
            {
                var patients = await _api.SearchPatientsAsync(search);
                var doctors = await _api.ListDoctorsAsync();
                var treatments = await _api.ListTreatmentsAsync();

                var doctorNameById = doctors.ToDictionary(d => d.Id, d => d.FullName);

                _allRows = patients.Select(p =>
                {
                    // Tratamiento más relevante: uno en progreso si existe,
                    // si no el más reciente sin importar su estado.
                    var patientTreatments = treatments.Where(t => t.PatientId == p.Id).ToList();
                    var relevant = patientTreatments.FirstOrDefault(t => t.Status == "in_progress")
                        ?? patientTreatments.FirstOrDefault();

                    string doctorName = "(sin asignar)";
                    if (!string.IsNullOrEmpty(p.PrimaryDentistId)
                        && doctorNameById.TryGetValue(p.PrimaryDentistId, out var name))
                    {
                        doctorName = name;
                    }

                    return new PatientRow
                    {
                        Patient = p,
                        PrimaryDoctorName = doctorName,
                        RelevantTreatment = relevant,
                    };
                }).ToList();

                ApplyStatusFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"No se pudo conectar con el backend local: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyStatusFilter()
        {
            if (PatientList == null)
                return; // todavía construyéndose la ventana; se llama de nuevo después en Loaded.

            int filterIndex = StatusFilterComboBox.SelectedIndex;
            IEnumerable<PatientRow> filtered = filterIndex switch
            {
                1 => _allRows.Where(r => r.RelevantTreatment?.Status == "in_progress"),
                2 => _allRows.Where(r => r.RelevantTreatment == null || r.RelevantTreatment.Status != "in_progress"),
                3 => _allRows.Where(r => r.RelevantTreatment?.Status == "completed"),
                _ => _allRows,
            };
            PatientList.ItemsSource = filtered.ToList();
        }

        private void OnStatusFilterChanged(object sender, SelectionChangedEventArgs e) => ApplyStatusFilter();

        private async void OnSearchClick(object sender, RoutedEventArgs e) =>
            await LoadPatientsAsync(SearchBox.Text);

        private async void SearchBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await LoadPatientsAsync(SearchBox.Text);
        }

        private async void OnNewPatientClick(object sender, RoutedEventArgs e)
        {
            var dialog = new NewPatientDialog { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                var patient = await _api.CreatePatientAsync(dialog.Result);

                if (!string.IsNullOrEmpty(dialog.Result.InitialDiagnosis) && CurrentSession.User != null)
                {
                    await _api.CreateClinicalNoteAsync(
                        patient.Id!, CurrentSession.User.Id, dialog.Result.InitialDiagnosis);
                }

                if (dialog.Result.FirstVisitDateTime.HasValue && dialog.Result.PrimaryDentistId != null)
                {
                    var startsAt = dialog.Result.FirstVisitDateTime.Value;
                    await _api.CreateAppointmentAsync(
                        patient.Id!, dialog.Result.PrimaryDentistId, startsAt, startsAt.AddMinutes(30),
                        "Primera cita");
                }

                await LoadPatientsAsync();
            }
        }

        private async void OnEditPatientClick(object sender, RoutedEventArgs e)
        {
            if (PatientList.SelectedItem is not PatientRow row)
            {
                MessageBox.Show("Selecciona un paciente de la lista primero.", "Falta selección",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new NewPatientDialog(row.Patient) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _api.UpdatePatientAsync(row.Patient.Id!, dialog.Result);
                    await LoadPatientsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo editar el paciente: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnDeletePatientClick(object sender, RoutedEventArgs e)
        {
            if (PatientList.SelectedItem is not PatientRow row)
            {
                MessageBox.Show("Selecciona un paciente de la lista primero.", "Falta selección",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"¿Eliminar a {row.FullName}? Su historial, citas y estudios existentes no se borran, "
                + "solo deja de aparecer en la lista de pacientes activos.",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await _api.DeletePatientAsync(row.Patient.Id!);
                await LoadPatientsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo eliminar el paciente: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnOpenOdontogramClick(object sender, RoutedEventArgs e)
        {
            if (PatientList.SelectedItem is not PatientRow row)
                return;

            var mainWindow = new MainWindow(row.Patient);
            mainWindow.Show();
            _isNavigatingAway = true;
            Close();
        }

        private void OnViewGalleryClick(object sender, RoutedEventArgs e)
        {
            if (PatientList.SelectedItem is not PatientRow row)
                return;

            var gallery = new PatientGalleryWindow(row.Patient.Id!, row.FullName) { Owner = this };
            gallery.ShowDialog();
        }

        private void OnOpenAgendaClick(object sender, RoutedEventArgs e)
        {
            var calendar = new CalendarWindow { Owner = this };
            calendar.ShowDialog();
        }

        private void OnOpenCalendarClick(object sender, RoutedEventArgs e)
        {
            var calendar = new CalendarWindow { Owner = this };
            calendar.ShowDialog();
        }

        private void OnOpenDoctorsClick(object sender, RoutedEventArgs e)
        {
            var doctors = new DoctorsWindow { Owner = this };
            doctors.ShowDialog();
        }

        private void OnOpenTreatmentsClick(object sender, RoutedEventArgs e)
        {
            var treatments = new TreatmentsWindow { Owner = this };
            treatments.ShowDialog();
        }

        private void OnSignOutClick(object sender, RoutedEventArgs e)
        {
            CurrentSession.SignOut();

            var login = new LoginWindow();
            bool? loggedIn = login.ShowDialog();
            if (loggedIn == true)
            {
                var newSelector = new PatientSelectorWindow();
                newSelector.Show();
            }
            else
            {
                Application.Current.Shutdown();
                return;
            }
            _isNavigatingAway = true;
            Close();
        }
    }
}
