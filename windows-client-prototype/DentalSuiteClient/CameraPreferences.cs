using System;
using System.IO;
using System.Text.Json;

namespace DentalSuiteClient
{
    /// <summary>
    /// Recuerda la última cámara elegida y si se usa en modo espejo, para
    /// no tener que escogerla cada vez. Se guarda en
    /// %LOCALAPPDATA%\DentalSuite (igual que la licencia y la base de
    /// datos), así que sobrevive a reinstalar el programa.
    /// </summary>
    public static class CameraPreferences
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DentalSuite", "camera-config.json");

        private sealed class Data
        {
            public string? LastCameraName { get; set; }
            public bool Mirror { get; set; }
        }

        private static Data Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<Data>(File.ReadAllText(ConfigPath)) ?? new Data();
            }
            catch
            {
                // Archivo dañado: se empieza de cero.
            }
            return new Data();
        }

        private static void Save(Data data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(data));
            }
            catch
            {
                // No es crítico: solo no se recordará la preferencia.
            }
        }

        public static string? LastCameraName
        {
            get => Load().LastCameraName;
            set { var d = Load(); d.LastCameraName = value; Save(d); }
        }

        public static bool Mirror
        {
            get => Load().Mirror;
            set { var d = Load(); d.Mirror = value; Save(d); }
        }
    }
}
