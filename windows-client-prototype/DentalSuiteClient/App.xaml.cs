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

            // Dental Suite puede trabajar de dos formas: sola en esta PC
            // (backend local, sin configurar nada — para quien no
            // necesita conectar el celular ni otra computadora), o
            // conectada a un servidor central en la nube (para quien sí
            // quiere eso). La primera vez se pregunta cuál quiere.
            string? savedUrl = ServerConfig.GetSavedBaseUrl();
            if (savedUrl == null)
            {
                var serverSetup = new ServerSetupWindow();
                bool? connected = serverSetup.ShowDialog();
                if (connected != true)
                {
                    Shutdown();
                    return;
                }
                savedUrl = ServerConfig.GetSavedBaseUrl();
            }
            else if (ServerConfig.IsLocalUrl(savedUrl))
            {
                // Modo "solo esta PC": hay que asegurarse de que el
                // backend local esté corriendo en cada arranque (no solo
                // la primera vez que se configuró).
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

                    bool ready = await LocalBackendLauncher.WaitUntilReadyAsync(TimeSpan.FromSeconds(15));
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
