using System;
using System.Security.Cryptography;
using DentalSuiteClient.Licensing;

namespace LicenseGenerator
{
    /// <summary>
    /// Herramienta de USO INTERNO para Mark Inc. Genera llaves de
    /// licencia firmadas con la clave privada (embebida abajo, NUNCA
    /// sale de este proyecto). Uso:
    ///
    ///   LicenseGenerator.exe "Nombre de la clínica" [yyyy-MM-dd]
    ///
    /// El segundo argumento (fecha de vencimiento) es opcional — si se
    /// omite, la licencia no vence.
    /// </summary>
    internal static class Program
    {
        // ⚠️ CLAVE PRIVADA — esto es lo único que hace que una licencia
        // sea válida. Si esta clave se filtra, cualquiera puede generar
        // licencias falsas para el software. Guárdala solo aquí; no
        // subas este proyecto a ningún repositorio compartido con
        // clientes ni lo incluyas en ningún instalador.
        private const string PrivateKeyPem = @"-----BEGIN PRIVATE KEY-----
MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDS3Zv9IoQhW3/P
DoNJnn2brx0tCLuMz1PegymPH7c5Zw7vHK/kRlBUkskSKZSkwJg0Y3FoaHd6xHjl
rZO/gnkzpQgXMMN5FdsqXXmP5ANuPpQXYvRUCWYESpwT51N5KwY4L/WtemvKD+WK
2wQTq+4Vd2yPLwv1a23m6ZmqOBhQkXCfouDu+kUz0np3O7ukfajJi4PS1ESEGK7W
MzDR59dyj1WgU58y7Rq8cjrHrn3Tpx/gcmAN5BIQgHCwRRa6lcItTfbiynQ8YWIq
dauCndQwf4OJhBAVIt1gx97UeBxQf17yDU+cJC4X39Zddmz88vklBvf9K+sOccCz
lvKMde+HAgMBAAECggEAH34xmfDZwxGjWQ1V1lzxbZsNxHgVasZcRFNalaGq/8vA
kz25l2JZBQiXNzs2lBSfptNjULCph5BobgUql+Oormnrd2s1BpSZEw665R6s1H5M
J+IDk7mDnwh1nxNhiCtA1otzKeGqz5boNYDYbweym0om0Iu6QYE1/NLOvHl8SCvr
KvF0YRnDLudMzSbYRTyiXIj0v9/IJTOZ/PJJ6O5XMqH8qrOFMi3upFYAzzbU+zZY
zphzfV6CBq5o9xoF+P1AwrsDEm881xQKBlOPqc/dICJzLFPeSN6m4KixV8tgBR2l
X5bLFCLZNTT5Wtp1y5F0rMTpr9QlZxvannuvbgj7gQKBgQDylCLito+0+lAFq2Er
j6AbMC62C4oaXoK6h3RqDShUZLhGZRN1teeklpJAeFrJHRloiqYIKEC/lwD7jSFP
lpsgZijv1hDfRYBjyAncv+mDn0uWhS/bqnAIHxwyBi/wYioYj2yZ/RLbs8cpq0vx
Ok+I1lBsg/+3tiU66bN+xP9StwKBgQDeiEr4MLkfvjRrQ6aH0Ia7E8iG02lXG1b+
3BOmxp/p+P14+23HNSvwfdd5SL7UW94P6/8iE2iK7+n3w/bDm/766ayreKFUWsQX
HfWkWrn960o6QcPDg8hJS7mkNuGL2k65OTHZbb4CxnwZZgJLD1fLKYg9TerjBJJc
t5/sdpU5sQKBgQClATT1qB76iOYT5S6Utf6/X7f2ZsULCMy92tPKK6cKOPTMFYb2
I8UsxKs5JWUDtkKJRP6JP/BDIpEtiyNkgtqEYFlqQt95MEqUFQb4XKaXvKxq96/F
KRtJuvev41uKlQtzZ2QFHwtL3O8VngprTTG6r6VHG0a/jSun9j39dj0C4wKBgBIt
/EHuaRFIzqt8qVwrEvaxlQ4+szpkh2Tmwcw8KdBJgRujHsLJez94aGQGJ2Dd6HRQ
iVVVVJMVKn4NSlmMovYIpQyVHEjx6VciIXE94Df4QuYjfUS5vWEPiKj7us1cFlZX
b9UdfApqALVqegF7zWZRjEnF1cyaEQL2FWCSB3rhAoGBAJRY5Xgz8xGIHHcAJsBy
5SQM8+R06iq0yMDrs/9dg9OEFnPZ624ZlB3561CCNfgoKFheBxc/LTHrGJmWV6Hc
ivIuB4CmZRIjx7I76Fl2BGPP3rXdySWbwSZ8u851zlNfUifRNk5/Y15LjTsEwmiv
0pq8Lnr/msDmNMOg7543drL3
-----END PRIVATE KEY-----";

        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Uso: LicenseGenerator.exe \"Nombre de la clínica\" [yyyy-MM-dd]");
                Console.WriteLine("El segundo argumento (fecha de vencimiento) es opcional.");
                return;
            }

            string customer = args[0];
            string? expiresAt = args.Length >= 2 ? args[1] : null;

            if (expiresAt != null && !DateTime.TryParse(expiresAt, out _))
            {
                Console.WriteLine($"La fecha '{expiresAt}' no es válida. Usa el formato yyyy-MM-dd.");
                return;
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(PrivateKeyPem);

            string licenseKey = LicenseFormat.Encode(
                new LicenseFormat.LicensePayload { Customer = customer, ExpiresAt = expiresAt },
                rsa);

            Console.WriteLine();
            Console.WriteLine($"Cliente:    {customer}");
            Console.WriteLine($"Vencimiento: {expiresAt ?? "sin vencimiento"}");
            Console.WriteLine();
            Console.WriteLine("Llave de licencia (copia y entrega esto al cliente):");
            Console.WriteLine();
            Console.WriteLine(licenseKey);
            Console.WriteLine();
        }
    }
}
