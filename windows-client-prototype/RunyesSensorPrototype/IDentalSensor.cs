using System;
using System.Threading;

namespace RunyesSensorPrototype
{
    /// <summary>
    /// Interfaz común que debe implementar cada adaptador de sensor
    /// (Runyes, Dabi Atlante, Woodpecker, etc.). El resto del software
    /// (odontograma, captura, guardado) solo conoce esta interfaz, nunca
    /// el SDK nativo específico de cada fabricante.
    /// </summary>
    public interface IDentalSensor : IDisposable
    {
        string SensorName { get; }

        /// <summary>Número de dispositivos del mismo tipo conectados por USB.</summary>
        int GetDeviceCount();

        /// <summary>Abre el dispositivo en el índice indicado (normalmente 0).</summary>
        bool Connect(int deviceIndex = 0);

        void Disconnect();

        /// <summary>
        /// Bloquea hasta que el sensor detecta el disparo de rayos X y
        /// entrega la imagen capturada ya corregida (gain/offset aplicados).
        ///
        /// onStatus (opcional) se llama con mensajes cortos para mostrar
        /// en pantalla mientras se espera -- ej. "Esperando disparo...",
        /// "Transfiriendo imagen..." -- igual que hace el software oficial
        /// de Runyes. Puede llegar desde un hilo distinto al de la UI:
        /// quien lo use debe despachar al hilo de UI si va a tocar
        /// controles visuales directamente.
        ///
        /// cancellationToken (opcional) permite abortar la espera del
        /// disparo desde afuera (ej. si el usuario le da "Cancelar").
        /// </summary>
        CapturedImage CaptureImage(Action<string>? onStatus = null, CancellationToken cancellationToken = default);
    }

    public sealed class CapturedImage
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public byte[] PixelData { get; init; } = Array.Empty<byte>();

        /// <summary>
        /// Si es true, PixelData ya es un archivo PNG completo y
        /// codificado (caso de las cámaras intraorales a color) — se
        /// guarda tal cual, sin pasar por la conversión de 16 bits en
        /// escala de grises que usan los sensores de rayos X.
        /// </summary>
        public bool IsPreEncodedPng { get; init; }
    }
}
