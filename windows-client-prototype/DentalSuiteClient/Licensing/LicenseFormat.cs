using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DentalSuiteClient.Licensing
{
    /// <summary>
    /// Formato de licencia: un payload JSON (cliente, fecha de
    /// vencimiento opcional) firmado con RSA-2048/SHA256. La llave que
    /// se le entrega a cada clínica es:
    ///
    ///   base64url(payload_json) + "." + base64url(firma)
    ///
    /// Solo quien tenga la CLAVE PRIVADA (guardada únicamente en
    /// LicenseGenerator, nunca en este proyecto) puede generar una
    /// llave que pase la verificación — cualquiera puede ver este
    /// código y la clave pública sin poder falsificar una licencia.
    /// </summary>
    public static class LicenseFormat
    {
        // Clave pública — segura de distribuir dentro del propio
        // instalador. Corresponde a la clave privada que se queda SOLO
        // en la carpeta license-keys/ y en LicenseGenerator (ver README
        // de licensing, no se sube a ningún lado ni se comparte).
        public const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0t2b/SKEIVt/zw6DSZ59
m68dLQi7jM9T3oMpjx+3OWcO7xyv5EZQVJLJEimUpMCYNGNxaGh3esR45a2Tv4J5
M6UIFzDDeRXbKl15j+QDbj6UF2L0VAlmBEqcE+dTeSsGOC/1rXpryg/litsEE6vu
FXdsjy8L9Wtt5umZqjgYUJFwn6Lg7vpFM9J6dzu7pH2oyYuD0tREhBiu1jMw0efX
co9VoFOfMu0avHI6x65906cf4HJgDeQSEIBwsEUWupXCLU324sp0PGFiKnWrgp3U
MH+DiYQQFSLdYMfe1HgcUH9e8g1PnCQuF9/WXXZs/PL5JQb3/SvrDnHAs5byjHXv
hwIDAQAB
-----END PUBLIC KEY-----";

        public sealed class LicensePayload
        {
            public string Customer { get; set; } = "";

            /// <summary>Formato "yyyy-MM-dd", o null para licencia sin vencimiento.</summary>
            public string? ExpiresAt { get; set; }
        }

        public static string Encode(LicensePayload payload, RSA privateKey)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            byte[] signature = privateKey.SignData(
                payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
        }

        public static bool TryDecodeAndVerify(
            string licenseKey, out LicensePayload? payload, out string error)
        {
            payload = null;
            error = "";

            var parts = (licenseKey ?? "").Trim().Split('.');
            if (parts.Length != 2)
            {
                error = "El formato de la llave no es válido.";
                return false;
            }

            byte[] payloadBytes;
            byte[] signature;
            try
            {
                payloadBytes = Base64UrlDecode(parts[0]);
                signature = Base64UrlDecode(parts[1]);
            }
            catch
            {
                error = "El formato de la llave no es válido.";
                return false;
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);

            bool signatureValid = rsa.VerifyData(
                payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!signatureValid)
            {
                error = "La llave no es válida (la firma no coincide).";
                return false;
            }

            try
            {
                payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes);
            }
            catch
            {
                error = "No se pudo leer la información de la llave.";
                return false;
            }

            if (payload == null)
            {
                error = "La llave está vacía.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(payload.ExpiresAt)
                && DateTime.TryParse(payload.ExpiresAt, out var expiresAt)
                && DateTime.Today > expiresAt.Date)
            {
                error = $"La licencia venció el {expiresAt:dd/MM/yyyy}.";
                return false;
            }

            return true;
        }

        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static byte[] Base64UrlDecode(string value)
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }
    }
}
