using System.Windows;

namespace DentalSuiteClient
{
    public partial class NewDoctorDialog : Window
    {
        public string FullName => FullNameBox.Text.Trim();
        public string Username => UsernameBox.Text.Trim();
        public string Password => PasswordBox.Password;
        public string? Phone => string.IsNullOrWhiteSpace(PhoneBox.Text) ? null : PhoneBox.Text.Trim();
        public string? Specialty => string.IsNullOrWhiteSpace(SpecialtyBox.Text) ? null : SpecialtyBox.Text.Trim();
        public string? ProfessionalLicense =>
            string.IsNullOrWhiteSpace(LicenseBox.Text) ? null : LicenseBox.Text.Trim();

        public NewDoctorDialog()
        {
            InitializeComponent();
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Nombre y correo/teléfono son obligatorios.", "Falta información",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Password.Length < 6)
            {
                MessageBox.Show("La contraseña debe tener al menos 6 caracteres.", "Falta información",
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
