using System;
using System.IO;

namespace DentalSuiteClient.Licensing
{
    public sealed class LicenseStatus
    {
        public bool IsValid { get; init; }
        public string? Customer { get; init; }
        public string? ExpiresAt { get; init; }
        public string? Error { get; init; }
    }

    public static class LicenseValidator
    {
        private static string LicenseFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DentalSuite", "license.key");

        /// <summary>Revisa si ya hay una licencia guardada en esta PC y si sigue siendo válida.</summary>
        public static LicenseStatus CheckStoredLicense()
        {
            if (!File.Exists(LicenseFilePath))
            {
                return new LicenseStatus { IsValid = false, Error = "No hay ninguna licencia activada en esta PC." };
            }

            string storedKey = File.ReadAllText(LicenseFilePath);
            return Validate(storedKey);
        }

        /// <summary>Valida una llave (sin guardarla todavía).</summary>
        public static LicenseStatus Validate(string licenseKey)
        {
            bool valid = LicenseFormat.TryDecodeAndVerify(licenseKey, out var payload, out string error);
            return new LicenseStatus
            {
                IsValid = valid,
                Customer = payload?.Customer,
                ExpiresAt = payload?.ExpiresAt,
                Error = valid ? null : error,
            };
        }

        /// <summary>Guarda la llave como la licencia activa de esta PC (después de validarla).</summary>
        public static void SaveLicense(string licenseKey)
        {
            string dir = Path.GetDirectoryName(LicenseFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(LicenseFilePath, licenseKey.Trim());
        }
    }
}
