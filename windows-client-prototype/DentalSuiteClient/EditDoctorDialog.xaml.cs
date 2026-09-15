using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class EditDoctorDialog : Window
    {
        public string FullName => FullNameBox.Text.Trim();
        public string? Phone => string.IsNullOrWhiteSpace(PhoneBox.Text) ? null : PhoneBox.Text.Trim();
        public string? Specialty => string.IsNullOrWhiteSpace(SpecialtyBox.Text) ? null : SpecialtyBox.Text.Trim();
        public string? ProfessionalLicense =>
            string.IsNullOrWhiteSpace(LicenseBox.Text) ? null : LicenseBox.Text.Trim();

        public EditDoctorDialog(UserDto doctor)
        {
            InitializeComponent();
            FullNameBox.Text = doctor.FullName;
            UsernameBox.Text = doctor.Username;
            PhoneBox.Text = doctor.Phone ?? "";
            SpecialtyBox.Text = doctor.Specialty ?? "";
            LicenseBox.Text = doctor.ProfessionalLicense ?? "";
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                MessageBox.Show("El nombre es obligatorio.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
