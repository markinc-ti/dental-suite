using System;
using System.IO;
using System.Text.Json;

namespace DentalSuiteClient
{
    /// <summary>
    /// Dental Suite ya no trae su propio backend local: habla con un
    /// servidor central (por ejemplo desplegado en Render, como
    /// tickets-ti) para que la PC del consultorio y los celulares vean
    /// siempre la misma información. Esta clase guarda esa URL una sola
    /// vez, en un archivo de configuración local, para no tener que
    /// volver a compilar la app si el servidor cambia de dirección.
    /// </summary>
    public static class ServerConfig
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DentalSuite", "server-config.json");

        private sealed class ConfigData
        {
            public string? ApiBaseUrl { get; set; }
        }

        public static string? GetSavedBaseUrl()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return null;

                string json = File.ReadAllText(ConfigPath);
                var data = JsonSerializer.Deserialize<ConfigData>(json);
                return string.IsNullOrWhiteSpace(data?.ApiBaseUrl) ? null : data!.ApiBaseUrl;
            }
            catch
            {
                return null;
            }
        }

        public static void SaveBaseUrl(string url)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var data = new ConfigData { ApiBaseUrl = url.TrimEnd('/') };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(data));
        }

        /// <summary>Olvida el servidor configurado — la próxima vez que
        /// abra la app, va a preguntar de nuevo (local o en la nube).</summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    File.Delete(ConfigPath);
            }
            catch
            {
                // Si no se puede borrar, no es crítico — seguirá usando
                // la configuración anterior hasta que se logre.
            }
        }

        /// <summary>Es "modo local" si la URL guardada apunta a esta
        /// misma máquina — a diferencia de un servidor en la nube, que
        /// nunca es 127.0.0.1/localhost.</summary>
        public static bool IsLocalUrl(string url) =>
            url.Contains("127.0.0.1") || url.Contains("localhost");
    }
}
