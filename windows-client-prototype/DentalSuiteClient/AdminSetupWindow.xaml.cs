using System;
using System.Windows;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class AdminSetupWindow : Window
    {
        private readonly ApiClient _api = new();

        public AdminSetupWindow()
        {
            InitializeComponent();
        }

        private async void OnCreateClick(object sender, RoutedEventArgs e)
        {
            string fullName = FullNameBox.Text.Trim();
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirm = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "Todos los campos son obligatorios.";
                return;
            }

            if (password.Length < 6)
            {
                StatusText.Text = "La contraseña debe tener al menos 6 caracteres.";
                return;
            }

            if (password != confirm)
            {
                StatusText.Text = "Las contraseñas no coinciden.";
                return;
            }

            try
            {
                await _api.CreateAdminAsync(fullName, username, password);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"No se pudo crear el administrador: {ex.Message}";
            }
        }
    }
}
