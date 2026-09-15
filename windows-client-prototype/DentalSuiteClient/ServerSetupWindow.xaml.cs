using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace DentalSuiteClient
{
    public partial class ServerSetupWindow : Window
    {
        public ServerSetupWindow()
        {
            InitializeComponent();
        }

        private async void OnLocalModeClick(object sender, RoutedEventArgs e)
        {
            StatusText.Foreground = System.Windows.Media.Brushes.Gray;
            StatusText.Text = "Iniciando el backend local...";

            if (!await LocalBackendLauncher.IsRunningAsync())
            {
                try
                {
                    LocalBackendLauncher.Start();
                }
                catch (Exception ex)
                {
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                    StatusText.Text = ex.Message;
                    return;
                }

                bool ready = await LocalBackendLauncher.WaitUntilReadyAsync(TimeSpan.FromSeconds(15));
                if (!ready)
                {
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                    StatusText.Text = "El backend local no respondió a tiempo. Intenta de nuevo.";
                    return;
                }
            }

            ServerConfig.SaveBaseUrl(LocalBackendLauncher.LocalBaseUrl);
            DialogResult = true;
        }

        private async void OnConnectClick(object sender, RoutedEventArgs e)
        {
            string url = ServerUrlBox.Text.Trim().TrimEnd('/');

            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                StatusText.Text = "Ingresa una dirección válida, empezando con https://";
                return;
            }

            StatusText.Foreground = System.Windows.Media.Brushes.Gray;
            StatusText.Text = "Conectando...";

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await client.GetAsync($"{url}/health");
                if (!response.IsSuccessStatusCode)
                {
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                    StatusText.Text = $"El servidor respondió con un error ({(int)response.StatusCode}). Verifica la dirección.";
                    return;
                }
            }
            catch (Exception ex)
            {
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                StatusText.Text = $"No se pudo conectar: {ex.Message}";
                return;
            }

            ServerConfig.SaveBaseUrl(url);
            DialogResult = true;
        }
    }
}
