using System;
using System.Windows;
using System.Windows.Input;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public partial class LoginWindow : Window
    {
        private readonly ApiClient _api = new();

        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void OnLoginClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await TryLoginAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error inesperado: {ex.Message}";
            }
        }

        private async void OnFieldKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await TryLoginAsync();
        }

        private async System.Threading.Tasks.Task TryLoginAsync()
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                StatusText.Text = "Ingresa tu correo o teléfono, y la contraseña.";
                return;
            }

            try
            {
                var user = await _api.LoginAsync(username, password);
                if (user == null)
                {
                    StatusText.Text = "Usuario o contraseña incorrectos.";
                    return;
                }

                CurrentSession.SignIn(user);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"No se pudo conectar: {ex.Message}";
            }
        }
    }
}
