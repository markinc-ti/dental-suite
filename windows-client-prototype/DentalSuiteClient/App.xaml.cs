using System;
using System.IO;
using System.Windows;

namespace DentalSuiteClient
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            var license = DentalSuiteClient.Licensing.LicenseValidator.CheckStoredLicense();
            if (!license.IsValid)
            {
                var activation = new LicenseActivationWindow();
                bool? activated = activation.ShowDialog();
                if (activated != true)
                {
                    Shutdown();
                    return;
                }
            }

            // Dental Suite trabaja PRIMERO en esta PC (backend local +
            // base de datos SQLite en %LOCALAPPDATA%\DentalSuite): no
            // necesita internet para funcionar, y esos datos no se tocan
            // al reinstalar. El servidor en la nube queda solo como
            // opción para quien la configure a propósito.
            string? savedUrl = ServerConfig.GetSavedBaseUrl();
            if (savedUrl == null)
            {
                // Primera vez: modo local directo, sin preguntar nada.
                ServerConfig.SaveBaseUrl(LocalBackendLauncher.LocalBaseUrl);
                savedUrl = LocalBackendLauncher.LocalBaseUrl;
            }
            else if (!ServerConfig.IsLocalUrl(savedUrl) && !await CloudServerReachableAsync(savedUrl))
            {
                // Estaba configurada contra la nube y no hay conexión:
                // en vez de no poder abrir la app, se ofrece trabajar en
                // modo local.
                var answer = MessageBox.Show(
                    "No hay conexión con el servidor en la nube:\n" + savedUrl + "\n\n"
                    + "¿Cambiar esta computadora a modo LOCAL (funciona sin internet)?\n\n"
                    + "Sí = trabajar en esta PC desde ahora.\n"
                    + "No = salir e intentar más tarde.",
                    "Sin conexión", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                {
                    Shutdown();
                    return;
                }
                ServerConfig.SaveBaseUrl(LocalBackendLauncher.LocalBaseUrl);
                savedUrl = LocalBackendLauncher.LocalBaseUrl;
            }

            if (ServerConfig.IsLocalUrl(savedUrl))
            {
                // Hay que asegurarse de que el backend local esté
                // corriendo en cada arranque.
                if (!await LocalBackendLauncher.IsRunningAsync())
                {
                    try
                    {
                        LocalBackendLauncher.Start();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                        return;
                    }

                    bool ready = await LocalBackendLauncher.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));
                    if (!ready)
                    {
                        MessageBox.Show(
                            "El backend local no respondió a tiempo.",
                            "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                        return;
                    }
                }
            }

            var api = new Api.ApiClient();
            bool adminExists;
            try
            {
                adminExists = await api.AdminExistsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo conectar con el servidor: {ex.Message}",
                    "Error de conexión", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            if (!adminExists)
            {
                var setup = new AdminSetupWindow();
                bool? created = setup.ShowDialog();
                if (created != true)
                {
                    Shutdown();
                    return;
                }
            }

            var login = new LoginWindow();
            bool? loggedIn = login.ShowDialog();
            if (loggedIn != true)
            {
                Shutdown();
                return;
            }

            var patientSelector = new PatientSelectorWindow();
            patientSelector.Show();
        }

        /// <summary>El plan gratuito de Render tarda hasta ~1 minuto en
        /// "despertar", por eso el tiempo de espera es largo.</summary>
        private static async System.Threading.Tasks.Task<bool> CloudServerReachableAsync(string baseUrl)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogAndShowCrash(e.Exception);
            // Marcamos como manejado para que la app no se cierre en
            // silencio — mejor mostrar el error y dejar seguir usándola
            // que perderla de golpe sin explicación.
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogAndShowCrash(ex);
        }

        private void LogAndShowCrash(Exception ex)
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DentalSuite");
            try
            {
                Directory.CreateDirectory(logDir);
                File.AppendAllText(
                    Path.Combine(logDir, "client-errors.log"),
                    $"--- {DateTime.Now:s} ---\n{ex}\n\n");
            }
            catch
            {
                // Si ni el log se puede escribir, seguimos mostrando el error igual.
            }

            MessageBox.Show(
                $"Ocurrió un error inesperado:\n\n{ex.Message}\n\n"
                + "El detalle completo se guardó en:\n"
                + $"{logDir}\\client-errors.log",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
