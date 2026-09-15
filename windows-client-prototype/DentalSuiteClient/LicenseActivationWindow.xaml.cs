using System.Windows;
using DentalSuiteClient.Licensing;

namespace DentalSuiteClient
{
    public partial class LicenseActivationWindow : Window
    {
        public LicenseActivationWindow()
        {
            InitializeComponent();
        }

        private void OnActivateClick(object sender, RoutedEventArgs e)
        {
            string key = LicenseKeyBox.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                StatusText.Text = "Ingresa una llave de licencia.";
                return;
            }

            var status = LicenseValidator.Validate(key);
            if (!status.IsValid)
            {
                StatusText.Text = status.Error ?? "Llave inválida.";
                return;
            }

            LicenseValidator.SaveLicense(key);
            DialogResult = true;
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
