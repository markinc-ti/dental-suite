using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DentalSuiteClient.Cameras
{
    /// <summary>
    /// La cámara WiFi crea su propia red ("EC-3-XXXX") y la PC tiene que
    /// unirse a ella para ver el video. Con una sola tarjeta WiFi eso
    /// desconecta la PC del WiFi de la clínica. Esta clase lo resuelve
    /// sola, sin que el usuario toque el ícono de WiFi:
    ///
    ///   - Con DOS adaptadores WiFi (la tarjeta interna + un adaptador
    ///     USB): usa el que esté libre para la cámara y deja el otro en
    ///     el WiFi de la clínica. Nunca se pierde internet.
    ///   - Con UNO solo: se cambia a la red de la cámara al abrir la
    ///     captura, y al cerrar la ventana regresa solo a la red que
    ///     tenía antes. Como Dental Suite ya funciona sin internet, el
    ///     programa sigue funcionando normal durante la captura.
    ///
    /// Usa "netsh wlan", que viene en todo Windows y no necesita
    /// permisos de administrador para conectarse a una red.
    /// </summary>
    public static class WifiCameraNetwork
    {
        private static readonly Regex CameraSsidPattern = new(@"^EC-\d", RegexOptions.IgnoreCase);
        private const string CameraSubnetPrefix = "192.168.15.";

        public sealed class WlanInterface
        {
            public string Name { get; set; } = "";
            public bool Connected { get; set; }
            public string? Ssid { get; set; }
            public string? Profile { get; set; }
        }

        /// <summary>Lo que se cambió, para poder deshacerlo al cerrar.</summary>
        public sealed class SwitchResult
        {
            public bool AlreadyConnected { get; init; }
            public string? InterfaceName { get; init; }
            public string? CameraSsid { get; init; }
            /// <summary>Red a la que estaba conectado ESE adaptador antes
            /// (null si estaba libre — caso de dos adaptadores).</summary>
            public string? PreviousProfile { get; init; }
            public bool UsedSpareAdapter => InterfaceName != null && PreviousProfile == null;
        }

        static WifiCameraNetwork()
        {
            // netsh escribe en la página de códigos OEM (850 en Windows en
            // español); .NET no la trae registrada por defecto.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        /// <summary>¿Algún adaptador de esta PC ya tiene IP en la red de
        /// la cámara (192.168.15.x)?</summary>
        public static bool IsCameraNetworkReachable()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork
                            && addr.Address.ToString().StartsWith(CameraSubnetPrefix, StringComparison.Ordinal))
                            return true;
                    }
                }
            }
            catch
            {
                // Si no se puede consultar, se asume que no.
            }
            return false;
        }

        /// <summary>
        /// Se asegura de que la PC esté en la red de la cámara. Devuelve
        /// qué se cambió (para RestoreAsync), o lanza
        /// InvalidOperationException con instrucciones claras si no se
        /// pudo hacer en automático.
        /// </summary>
        public static async Task<SwitchResult> EnsureConnectedAsync(Action<string>? onStatus, CancellationToken token = default)
        {
            if (IsCameraNetworkReachable())
                return new SwitchResult { AlreadyConnected = true };

            onStatus?.Invoke("Buscando la red WiFi de la cámara...");

            var interfaces = await GetInterfacesAsync();
            if (interfaces.Count == 0)
                throw new InvalidOperationException("Esta computadora no tiene WiFi activo. Enciende el WiFi e inténtalo de nuevo.");

            // 1) Encontrar el nombre de la red de la cámara: primero entre
            //    las redes visibles, si no entre las ya guardadas en
            //    Windows (en Windows 11 "ver redes" puede pedir permiso de
            //    ubicación y regresar vacío).
            string? cameraSsid = null;
            string? visibleOnInterface = null;
            foreach (var wlan in interfaces)
            {
                var visible = await GetVisibleSsidsAsync(wlan.Name);
                var match = visible.FirstOrDefault(s => CameraSsidPattern.IsMatch(s));
                if (match != null)
                {
                    cameraSsid = match;
                    visibleOnInterface = wlan.Name;
                    break;
                }
            }

            var savedProfiles = await GetSavedProfilesAsync();
            if (cameraSsid == null)
                cameraSsid = savedProfiles.FirstOrDefault(p => CameraSsidPattern.IsMatch(p));

            if (cameraSsid == null)
            {
                throw new InvalidOperationException(
                    "No se encontró la red WiFi de la cámara (EC-3-XXXX).\n\n"
                    + "• Verifica que la cámara esté encendida (mantén presionado su botón 2 segundos, luz verde).\n"
                    + "• Si sigue sin aparecer, conéctate UNA vez a mano a \"EC-3-XXXX\" desde el ícono de WiFi de "
                    + "Windows. Después de eso Dental Suite se conectará solo cada vez.");
            }

            // 2) Elegir adaptador: uno libre si existe (dos adaptadores →
            //    nunca se pierde internet); si no, el único que hay.
            var spare = interfaces.FirstOrDefault(i => !i.Connected);
            var chosen = spare
                ?? interfaces.FirstOrDefault(i => i.Name == visibleOnInterface)
                ?? interfaces[0];
            string? previousProfile = chosen.Connected ? (chosen.Profile ?? chosen.Ssid) : null;

            // 3) Asegurar que exista un perfil guardado para la red de la
            //    cámara (red abierta, sin contraseña).
            if (!savedProfiles.Contains(cameraSsid, StringComparer.OrdinalIgnoreCase))
                await AddOpenProfileAsync(cameraSsid, chosen.Name);

            onStatus?.Invoke(spare != null
                ? $"Conectando el segundo adaptador WiFi a {cameraSsid} (el WiFi de la clínica no se toca)..."
                : $"Conectando a {cameraSsid}... (la PC regresará a \"{previousProfile}\" al cerrar esta ventana)");

            var (code, output) = await RunNetshAsync($"wlan connect name=\"{cameraSsid}\" ssid=\"{cameraSsid}\" interface=\"{chosen.Name}\"");
            if (code != 0)
                throw new InvalidOperationException($"Windows no pudo conectarse a {cameraSsid}:\n{output.Trim()}");

            // 4) Esperar a que la PC reciba IP de la cámara.
            var switched = new SwitchResult
            {
                InterfaceName = chosen.Name,
                CameraSsid = cameraSsid,
                PreviousProfile = previousProfile,
            };
            var deadline = DateTime.UtcNow.AddSeconds(25);
            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    token.ThrowIfCancellationRequested();
                    if (IsCameraNetworkReachable())
                        return switched;
                    await Task.Delay(500, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Se cerró la ventana a media conexión: regresar a la red
                // anterior antes de salir.
                await RestoreAsync(switched);
                throw;
            }

            // No obtuvo IP a tiempo: se regresa a la red anterior para no
            // dejar la PC sin conexión.
            await RestoreAsync(switched);
            throw new InvalidOperationException(
                $"Se intentó conectar a {cameraSsid} pero no hubo respuesta. "
                + "Acerca la cámara a la computadora y verifica que esté encendida.");
        }

        /// <summary>Deshace lo que hizo EnsureConnectedAsync: regresa a la
        /// red anterior (un adaptador) o suelta el adaptador extra.</summary>
        public static async Task RestoreAsync(SwitchResult? result)
        {
            if (result == null || result.AlreadyConnected || result.InterfaceName == null)
                return;

            try
            {
                if (result.PreviousProfile != null)
                    await RunNetshAsync($"wlan connect name=\"{result.PreviousProfile}\" interface=\"{result.InterfaceName}\"");
                else
                    await RunNetshAsync($"wlan disconnect interface=\"{result.InterfaceName}\"");
            }
            catch
            {
                // Mejor no tronar al cerrar la ventana; Windows de todos
                // modos se reconecta solo a su red conocida.
            }
        }

        // ------------------------------------------------------------
        //  netsh — el texto sale en el idioma de Windows (español o
        //  inglés), así que se reconocen las dos versiones de cada campo.
        // ------------------------------------------------------------

        public static async Task<List<WlanInterface>> GetInterfacesAsync()
        {
            var (_, output) = await RunNetshAsync("wlan show interfaces");
            return ParseInterfaces(output);
        }

        public static List<WlanInterface> ParseInterfaces(string output)
        {
            var list = new List<WlanInterface>();
            WlanInterface? current = null;

            foreach (var raw in output.Split('\n'))
            {
                var (key, value) = SplitKeyValue(raw);
                if (key == null) continue;

                if (key is "name" or "nombre")
                {
                    current = new WlanInterface { Name = value };
                    list.Add(current);
                }
                else if (current == null)
                {
                    continue;
                }
                else if (key is "state" or "estado")
                {
                    var v = value.ToLowerInvariant();
                    current.Connected = v.StartsWith("connected") || v.StartsWith("conectado");
                }
                else if (key == "ssid")
                {
                    current.Ssid = value;
                }
                else if (key is "profile" or "perfil")
                {
                    current.Profile = value;
                }
            }

            foreach (var i in list.Where(i => !i.Connected))
            {
                i.Ssid = null;
                i.Profile = null;
            }
            return list;
        }

        public static async Task<List<string>> GetVisibleSsidsAsync(string interfaceName)
        {
            var (_, output) = await RunNetshAsync($"wlan show networks interface=\"{interfaceName}\"");
            return ParseVisibleSsids(output);
        }

        public static List<string> ParseVisibleSsids(string output)
        {
            var list = new List<string>();
            foreach (var raw in output.Split('\n'))
            {
                // "SSID 1 : EC-3-1A2B"
                var m = Regex.Match(raw.Trim(), @"^SSID\s+\d+\s*:(.*)$");
                if (!m.Success) continue;
                string ssid = m.Groups[1].Value.Trim();
                if (ssid.Length > 0) // las redes ocultas vienen sin nombre
                    list.Add(ssid);
            }
            return list;
        }

        public static async Task<List<string>> GetSavedProfilesAsync()
        {
            var (_, output) = await RunNetshAsync("wlan show profiles");
            return ParseSavedProfiles(output);
        }

        public static List<string> ParseSavedProfiles(string output)
        {
            // "    All User Profile     : MiRed"
            // "    Perfil de todos los usuarios: MiRed"
            var list = new List<string>();
            foreach (var raw in output.Split('\n'))
            {
                var m = Regex.Match(raw.TrimEnd(), @"^\s*(All User Profile|Current User Profile|Perfil de todos los usuarios|Perfil de usuario actual)\s*:\s*(.+?)\s*$",
                    RegexOptions.IgnoreCase);
                if (m.Success)
                    list.Add(m.Groups[2].Value);
            }
            return list;
        }

        private static async Task AddOpenProfileAsync(string ssid, string interfaceName)
        {
            string escaped = System.Security.SecurityElement.Escape(ssid) ?? ssid;
            string hex = Convert.ToHexString(Encoding.UTF8.GetBytes(ssid));
            string xml = $@"<?xml version=""1.0""?>
<WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
  <name>{escaped}</name>
  <SSIDConfig><SSID><hex>{hex}</hex><name>{escaped}</name></SSID></SSIDConfig>
  <connectionType>ESS</connectionType>
  <connectionMode>manual</connectionMode>
  <MSM><security>
    <authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption>
  </security></MSM>
</WLANProfile>";

            string path = Path.Combine(Path.GetTempPath(), $"dentalsuite-wlan-{Guid.NewGuid():N}.xml");
            await File.WriteAllTextAsync(path, xml);
            try
            {
                await RunNetshAsync($"wlan add profile filename=\"{path}\" interface=\"{interfaceName}\" user=current");
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        private static (string? key, string value) SplitKeyValue(string raw)
        {
            int idx = raw.IndexOf(':');
            if (idx <= 0) return (null, "");
            string key = raw[..idx].Trim().ToLowerInvariant();
            string value = raw[(idx + 1)..].Trim();
            return (key, value);
        }

        private static async Task<(int code, string output)> RunNetshAsync(string args)
        {
            Encoding enc;
            try { enc = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { enc = Encoding.UTF8; }

            var psi = new ProcessStartInfo("netsh.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = enc,
                StandardErrorEncoding = enc,
            };

            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            return (p.ExitCode, (await stdout) + (await stderr));
        }
    }
}
