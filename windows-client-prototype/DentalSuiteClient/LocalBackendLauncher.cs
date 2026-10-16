using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DentalSuiteClient
{
    /// <summary>
    /// Para quien NO necesita conectar otros dispositivos (celular, otra
    /// PC), Dental Suite puede trabajar sola: arranca su propio backend
    /// local (el mismo código Python, empaquetado con un runtime
    /// portátil) en esta misma máquina, con su base de datos SQLite
    /// local — nadie tiene que configurar ni desplegar nada.
    /// </summary>
    public static class LocalBackendLauncher
    {
        public const string LocalBaseUrl = "http://127.0.0.1:8003";

        /// <summary>%LOCALAPPDATA%\DentalSuite — no se toca al reinstalar.</summary>
        public static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DentalSuite");

        public static async Task<bool> IsRunningAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await client.GetAsync($"{LocalBaseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public static void Start()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string runtimeDir = Path.Combine(baseDir, "backend", "python-runtime");
            string pythonwPath = Path.Combine(runtimeDir, "pythonw.exe");
            string scriptPath = Path.Combine(runtimeDir, "run_server.py");

            if (!File.Exists(pythonwPath) || !File.Exists(scriptPath))
            {
                throw new FileNotFoundException(
                    "No se encontró el backend local incluido con la instalación. "
                    + "Reinstala Dental Suite, o usa la opción de conectar a un servidor en la nube.",
                    pythonwPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonwPath,
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = runtimeDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // Todos los datos (base de datos, fotos subidas, respaldos)
            // van FUERA de la carpeta del programa, para que reinstalar
            // o actualizar Dental Suite nunca los borre.
            startInfo.Environment["DENTALSUITE_DATA_DIR"] = DataDirectory;
            Process.Start(startInfo);
        }

        public static async Task<bool> WaitUntilReadyAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await IsRunningAsync())
                    return true;
                await Task.Delay(500);
            }
            return false;
        }
    }
}
