using System.Linq;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class NewPatientDialog : Window
    {
        public NewPatientData Result { get; } = new();
        private readonly ApiClient _api = new();
        private readonly PatientDto? _existingPatient;

        /// <summary>Constructor para crear un paciente nuevo.</summary>
        public NewPatientDialog()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadDoctorsAsync();
        }

        /// <summary>Constructor para editar un paciente existente — precarga
        /// los campos y esconde la sección de diagnóstico inicial (eso es
        /// solo para la primera cita).</summary>
        public NewPatientDialog(PatientDto existingPatient)
        {
            InitializeComponent();
            _existingPatient = existingPatient;
            Title = "Editar paciente";
            DiagnosisSection.Visibility = Visibility.Collapsed;

            FirstNameBox.Text = existingPatient.FirstName;
            LastNameBox.Text = existingPatient.LastName;
            PhoneBox.Text = existingPatient.Phone ?? "";
            EmailBox.Text = existingPatient.Email ?? "";
            AddressBox.Text = existingPatient.Address ?? "";
            EmergencyNameBox.Text = existingPatient.EmergencyContactName ?? "";
            EmergencyPhoneBox.Text = existingPatient.EmergencyContactPhone ?? "";
            EmergencyRelationshipBox.Text = existingPatient.EmergencyContactRelationship ?? "";

            Loaded += async (_, _) =>
            {
                await LoadDoctorsAsync();
                if (existingPatient.PrimaryDentistId != null && PrimaryDoctorComboBox.ItemsSource is System.Collections.Generic.List<UserDto> doctors)
                {
                    var match = doctors.FirstOrDefault(d => d.Id == existingPatient.PrimaryDentistId);
                    if (match != null)
                        PrimaryDoctorComboBox.SelectedItem = match;
                }
            };
        }

        private async System.Threading.Tasks.Task LoadDoctorsAsync()
        {
            var doctors = await _api.ListDoctorsAsync();
            PrimaryDoctorComboBox.ItemsSource = doctors;
        }

        private void OnScheduleFirstVisitChanged(object sender, RoutedEventArgs e)
        {
            FirstVisitFields.Visibility = ScheduleFirstVisitCheck.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            string firstName = FirstNameBox.Text.Trim();
            string lastName = LastNameBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                MessageBox.Show("Nombre y apellidos son obligatorios.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result.FirstName = firstName;
            Result.LastName = lastName;
            Result.Phone = NullIfEmpty(PhoneBox.Text);
            Result.Email = NullIfEmpty(EmailBox.Text);
            Result.Address = NullIfEmpty(AddressBox.Text);
            Result.EmergencyContactName = NullIfEmpty(EmergencyNameBox.Text);
            Result.EmergencyContactPhone = NullIfEmpty(EmergencyPhoneBox.Text);
            Result.EmergencyContactRelationship = NullIfEmpty(EmergencyRelationshipBox.Text);
            Result.InitialDiagnosis = _existingPatient == null ? NullIfEmpty(DiagnosisBox.Text) : null;
            Result.PrimaryDentistId = (PrimaryDoctorComboBox.SelectedItem as UserDto)?.Id;

            if (_existingPatient == null && ScheduleFirstVisitCheck.IsChecked == true)
            {
                if (Result.PrimaryDentistId == null)
                {
                    MessageBox.Show(
                        "Para agregar la primera cita al calendario, elige un doctor principal arriba.",
                        "Falta información", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (FirstVisitDatePicker.SelectedDate is not System.DateTime visitDate)
                {
                    MessageBox.Show("Selecciona la fecha de la primera cita.", "Falta información",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!System.TimeSpan.TryParseExact(
                        FirstVisitTimeBox.Text.Trim(), "hh\\:mm",
                        System.Globalization.CultureInfo.InvariantCulture, out var visitTime))
                {
                    MessageBox.Show("La hora debe tener el formato HH:mm, por ejemplo 10:00.", "Formato inválido",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Result.FirstVisitDateTime = visitDate.Date + visitTime;
            }

            DialogResult = true;
        }

        private static string? NullIfEmpty(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
