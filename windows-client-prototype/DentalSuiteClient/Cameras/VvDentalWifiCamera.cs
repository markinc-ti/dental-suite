using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DentalSuiteClient.Cameras
{
    /// <summary>
    /// Cámara intraoral WiFi de VVDental (Nanning VV Dental, modelo EC-3,
    /// red "EC-3-XXXX"). Habla directo con la cámara por UDP, sin
    /// depender de su programa EndoCamera.exe.
    ///
    /// Protocolo obtenido desensamblando EndoCamera.exe (Qt 5 + OpenCV):
    ///
    ///   Cámara: 192.168.15.50 (fija, la cámara es el punto de acceso)
    ///
    ///   Socket de VIDEO  — local UDP 4444 → manda a cámara:7020
    ///       Iniciar:  20 36        Detener: 20 37
    ///   Socket de CONTROL — local UDP 5555 → manda a cámara:7030
    ///       Iniciar:  55 AA 01 0F  Detener: 55 AA 00 0F
    ///
    ///   Video (llega al puerto local 4444): cada cuadro es un JPEG
    ///   partido en varios datagramas. Cada datagrama trae 4 bytes de
    ///   encabezado y luego un pedazo del JPEG:
    ///       [1] != 0  → es el último pedazo del cuadro
    ///       [2]       → (en el último pedazo) total de pedazos del cuadro
    ///   Si el total no coincide con los pedazos recibidos, se perdió
    ///   alguno y el cuadro se descarta (igual que hace EndoCamera).
    ///
    ///   Estado (llega al puerto local 5555): paquetes de 12 bytes.
    ///       [0] == 0x55 → trae datos del sensor de orientación
    ///       [10] == 1   → BOTÓN FÍSICO presionado (en cualquier paquete
    ///                     de 12 bytes, incluidos los que empiezan con 0x55)
    ///                     (EndoCamera lo usa para tomar foto)
    /// </summary>
    public sealed class VvDentalWifiCamera : IDisposable
    {
        public const string CameraIp = "192.168.15.50";
        public const int CameraVideoPort = 7020;
        public const int CameraControlPort = 7030;
        public const int LocalVideoPort = 4444;
        public const int LocalStatusPort = 5555;

        private static readonly byte[] VideoStartCmd = { 0x20, 0x36 };
        private static readonly byte[] VideoStopCmd = { 0x20, 0x37 };
        private static readonly byte[] ControlStartCmd = { 0x55, 0xAA, 0x01, 0x0F };
        private static readonly byte[] ControlStopCmd = { 0x55, 0xAA, 0x00, 0x0F };

        private readonly IPEndPoint _videoEndpoint;
        private readonly IPEndPoint _controlEndpoint;

        private UdpClient? _videoSocket;
        private UdpClient? _statusSocket;
        private CancellationTokenSource? _cts;

        /// <summary>Se dispara con los bytes de cada cuadro JPEG completo
        /// (en un hilo de fondo — hay que pasar al hilo de la ventana para
        /// tocar la interfaz).</summary>
        public event Action<byte[]>? JpegFrameReceived;

        /// <summary>Se presionó el botón físico de la cámara.</summary>
        public event Action? ShutterButtonPressed;

        public bool IsRunning => _cts != null;
        public DateTime LastFrameUtc { get; private set; } = DateTime.MinValue;
        public long FramesReceived { get; private set; }

        public string DisplayName => "Cámara WiFi VVDental (EC-3)";

        public VvDentalWifiCamera(string cameraIp = CameraIp)
        {
            var ip = IPAddress.Parse(cameraIp);
            _videoEndpoint = new IPEndPoint(ip, CameraVideoPort);
            _controlEndpoint = new IPEndPoint(ip, CameraControlPort);
        }

        /// <summary>
        /// Abre los puertos locales y le pide a la cámara que empiece a
        /// transmitir. Lanza InvalidOperationException con un mensaje
        /// claro si otro programa (normalmente EndoCamera.exe) ya está
        /// usando los puertos.
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _videoSocket = new UdpClient(new IPEndPoint(IPAddress.Any, LocalVideoPort));
                _statusSocket = new UdpClient(new IPEndPoint(IPAddress.Any, LocalStatusPort));
            }
            catch (SocketException)
            {
                _videoSocket?.Dispose();
                _statusSocket?.Dispose();
                _videoSocket = null;
                _statusSocket = null;
                throw new InvalidOperationException(
                    "Otro programa está usando la cámara WiFi (puertos 4444/5555). "
                    + "Cierra EndoCamera u otra ventana de captura e inténtalo de nuevo.");
            }

            // Buffer grande: a 20 cuadros/seg llegan muchos datagramas
            // seguidos y con el buffer por defecto de Windows se pierden.
            try { _videoSocket.Client.ReceiveBufferSize = 4 * 1024 * 1024; } catch { }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(() => VideoLoopAsync(token), token);
            _ = Task.Run(() => StatusLoopAsync(token), token);
            _ = Task.Run(() => KeepStartingAsync(token), token);
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                _videoSocket?.Send(VideoStopCmd, VideoStopCmd.Length, _videoEndpoint);
                _statusSocket?.Send(ControlStopCmd, ControlStopCmd.Length, _controlEndpoint);
            }
            catch
            {
                // Si ya no hay red con la cámara, no hay a quién avisarle.
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _videoSocket?.Dispose();
            _statusSocket?.Dispose();
            _videoSocket = null;
            _statusSocket = null;
        }

        /// <summary>
        /// Manda el comando de inicio y lo repite mientras no lleguen
        /// cuadros (por ejemplo, si la PC apenas se está conectando a la
        /// red de la cámara y el primer comando se perdió, o si la
        /// cámara se apagó y se volvió a prender).
        /// </summary>
        private async Task KeepStartingAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if ((DateTime.UtcNow - LastFrameUtc).TotalSeconds > 2)
                {
                    try
                    {
                        _videoSocket?.Send(VideoStartCmd, VideoStartCmd.Length, _videoEndpoint);
                        _statusSocket?.Send(ControlStartCmd, ControlStartCmd.Length, _controlEndpoint);
                    }
                    catch
                    {
                        // Sin ruta a la cámara todavía (red no conectada):
                        // se reintenta en el siguiente ciclo.
                    }
                }

                try { await Task.Delay(1000, token); }
                catch (TaskCanceledException) { return; }
            }
        }

        private async Task VideoLoopAsync(CancellationToken token)
        {
            var assembler = new FrameAssembler();
            var socket = _videoSocket!;

            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult packet;
                try
                {
                    packet = await socket.ReceiveAsync(token);
                }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (SocketException)
                {
                    // En Windows, un ICMP "puerto inalcanzable" de un envío
                    // anterior aparece como error en el siguiente Receive.
                    // No es fatal: se sigue escuchando.
                    continue;
                }

                byte[]? jpeg = assembler.Add(packet.Buffer);
                if (jpeg == null) continue;

                LastFrameUtc = DateTime.UtcNow;
                FramesReceived++;
                try { JpegFrameReceived?.Invoke(jpeg); }
                catch { /* un error en quien consume no debe tumbar la recepción */ }
            }
        }

        private async Task StatusLoopAsync(CancellationToken token)
        {
            var socket = _statusSocket!;

            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult packet;
                try
                {
                    packet = await socket.ReceiveAsync(token);
                }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { continue; }

                LogUnusualStatusPacket(packet.Buffer);

                if (_buttonDetector.Process(packet.Buffer, DateTime.UtcNow))
                {
                    try { ShutterButtonPressed?.Invoke(); }
                    catch { }
                }
            }
        }

        private readonly ShutterDetector _buttonDetector = new();
        private int _loggedPackets;

        /// <summary>
        /// Guarda en %LOCALAPPDATA%\DentalSuite\camara-wifi.log los
        /// paquetes de estado que no son de orientación "normal" (máx. 200),
        /// para poder diagnosticar si algún modelo manda el botón distinto.
        /// </summary>
        private void LogUnusualStatusPacket(byte[] data)
        {
            if (_loggedPackets >= 200) return;
            if (data.Length == 12 && data[10] == 0) return; // orientación sin botón: lo normal
            _loggedPackets++;
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DentalSuite");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "camara-wifi.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} estado[{data.Length}]: {Convert.ToHexString(data)}{Environment.NewLine}");
            }
            catch { }
        }

        /// <summary>
        /// Igual que EndoCamera: CUALQUIER paquete de estado de 12 bytes con
        /// el byte 10 en 1 significa botón presionado — también los que
        /// empiezan con 0x55 (que además traen la orientación).
        /// </summary>
        public static bool IsShutterPacket(byte[] data) =>
            data.Length == 12 && data[10] == 1;

        /// <summary>
        /// Convierte los paquetes de estado en "pulsaciones": mientras el
        /// botón se mantiene oprimido la cámara puede seguir mandando el
        /// byte en 1 en cada paquete; solo cuenta como UNA foto. Una nueva
        /// pulsación se reconoce cuando llega un paquete sin botón (soltó)
        /// o cuando pasan más de 400 ms sin avisos de botón.
        /// </summary>
        public sealed class ShutterDetector
        {
            private DateTime _lastPressedPacketUtc = DateTime.MinValue;
            private DateTime _lastFireUtc = DateTime.MinValue;

            public bool Process(byte[] data, DateTime nowUtc)
            {
                if (data.Length != 12)
                    return false;

                if (!IsShutterPacket(data))
                {
                    _lastPressedPacketUtc = DateTime.MinValue; // soltó el botón
                    return false;
                }

                bool newPress = (nowUtc - _lastPressedPacketUtc).TotalMilliseconds > 400;
                _lastPressedPacketUtc = nowUtc;

                if (!newPress || (nowUtc - _lastFireUtc).TotalMilliseconds < 700)
                    return false;

                _lastFireUtc = nowUtc;
                return true;
            }
        }

        public void Dispose() => Stop();

        /// <summary>
        /// Arma cuadros JPEG a partir de los datagramas, con exactamente
        /// la misma lógica que EndoCamera.exe (readdata_1).
        /// </summary>
        public sealed class FrameAssembler
        {
            private readonly MemoryStream _frame = new();
            private int _packetCount;

            public long FramesDropped { get; private set; }

            /// <summary>Agrega un datagrama. Devuelve el JPEG cuando se
            /// completa un cuadro sin pérdidas; si no, null.</summary>
            public byte[]? Add(byte[] datagram)
            {
                if (datagram.Length < 4)
                    return null;

                bool isLast = datagram[1] != 0;
                _packetCount++;
                _frame.Write(datagram, 4, datagram.Length - 4);

                if (!isLast)
                    return null;

                int expected = datagram[2];
                byte[]? result = null;

                if (expected == _packetCount && _frame.Length >= 4)
                {
                    var bytes = _frame.ToArray();
                    // Validación mínima de JPEG (FF D8 al inicio) para no
                    // entregar basura si llegó un cuadro mezclado.
                    if (bytes[0] == 0xFF && bytes[1] == 0xD8)
                        result = bytes;
                    else
                        FramesDropped++;
                }
                else
                {
                    FramesDropped++;
                }

                _frame.SetLength(0);
                _packetCount = 0;
                return result;
            }
        }
    }
}
