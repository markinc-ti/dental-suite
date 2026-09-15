using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using RunyesSensorPrototype; // IDentalSensor y CapturedImage viven ahí por ahora;
                              // pendiente moverlos a un proyecto "Sensors.Core"
                              // compartido cuando se agregue un tercer fabricante.

namespace WoodpeckerSensorPrototype
{
    /// <summary>
    /// Adaptador Woodpecker/DBA usando XVDetector.dll. A diferencia de
    /// Runyes (que se consulta con un bucle de espera), este SDK avisa
    /// con un callback cuando la imagen está lista — hay que registrar
    /// ese callback antes de disparar la exposición.
    /// </summary>
    public sealed class WoodpeckerSensorAdapter : IDentalSensor
    {
        private bool _initialized;
        private TaskCompletionSource<CapturedImage>? _pendingCapture;

        // Hay que mantener una referencia viva a los delegates mientras
        // estén registrados en la DLL nativa — si el recolector de basura
        // los mueve/libera y la DLL intenta invocarlos después, se cae la
        // aplicación entera (delegate "recolectado" mientras aún es
        // llamado desde código nativo es un error clásico de P/Invoke).
        private readonly WoodpeckerSensorInterop.ImageValidCallback _imageCallback;
        private readonly WoodpeckerSensorInterop.EventCallback _eventCallback;

        public WoodpeckerSensorAdapter()
        {
            _imageCallback = OnImageValid;
            _eventCallback = OnEvent;
        }

        public string SensorName => "Woodpecker";

        public int GetDeviceCount()
        {
            // XVDetector.dll no expone una función de conteo de
            // dispositivos como Runyes — el patrón de este SDK es
            // inicializar directo con un índice y ver si funciona.
            // Devolvemos 1 como valor optimista; ajustar si se confirma
            // que existe otra forma de enumerar.
            return 1;
        }

        public bool Connect(int deviceIndex = 0)
        {
            int result = WoodpeckerSensorInterop.XVD_InitDetector(deviceIndex);
            _initialized = result == 0 || result == 1; // convención de éxito sin confirmar

            if (_initialized)
            {
                WoodpeckerSensorInterop.RegisterImageValidCallBack(_imageCallback);
                WoodpeckerSensorInterop.RegisterEventCallBack(_eventCallback);
            }

            return _initialized;
        }

        public void Disconnect()
        {
            if (_initialized)
            {
                WoodpeckerSensorInterop.XVD_ReleaseDetector();
                _initialized = false;
            }
        }

        /// <summary>
        /// Carga el archivo de calibración generado por el software
        /// original de Woodpecker/DBA. Sin esto, es muy probable que pase
        /// lo mismo que con Runyes: el sensor "funciona" pero entrega
        /// datos inválidos o vacíos.
        /// </summary>
        public bool LoadCalibrationFile(string filePath)
        {
            int result = WoodpeckerSensorInterop.XVD_LoadCalibrationTemplateFile(filePath);
            return result == 0 || result == 1;
        }

        public CapturedImage CaptureImage()
        {
            if (!_initialized)
                throw new InvalidOperationException("El sensor Woodpecker no está conectado.");

            _pendingCapture = new TaskCompletionSource<CapturedImage>();

            int exposeResult = WoodpeckerSensorInterop.XVD_Expose();
            if (exposeResult != 0 && exposeResult != 1)
            {
                throw new InvalidOperationException(
                    $"XVD_Expose devolvió {exposeResult}, no parece indicar éxito.");
            }

            // Espera a que el callback de imagen lista dispare, con
            // límite de tiempo para no colgar la aplicación si el
            // callback nunca llega (firma incorrecta, por ejemplo).
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = Task.WhenAny(_pendingCapture.Task, timeoutTask).GetAwaiter().GetResult();

            if (completedTask == timeoutTask)
            {
                throw new InvalidOperationException(
                    "Pasaron 30 segundos sin recibir el callback de imagen lista. "
                    + "Si sí disparaste el sensor, es posible que la firma de "
                    + "RegisterImageValidCallBack no coincida con el SDK real.");
            }

            return _pendingCapture.Task.GetAwaiter().GetResult();
        }

        private void OnImageValid(IntPtr imageDataPtr, int width, int height)
        {
            try
            {
                if (imageDataPtr == IntPtr.Zero || width <= 0 || height <= 0)
                {
                    _pendingCapture?.TrySetException(
                        new InvalidOperationException(
                            $"Callback de imagen recibido con datos inválidos "
                            + $"(ptr={imageDataPtr}, width={width}, height={height})."));
                    return;
                }

                int byteCount = width * height * 2; // 16 bits por píxel, mismo supuesto que Runyes
                var buffer = new byte[byteCount];
                Marshal.Copy(imageDataPtr, buffer, 0, byteCount);

                _pendingCapture?.TrySetResult(new CapturedImage
                {
                    Width = width,
                    Height = height,
                    PixelData = buffer,
                });
            }
            catch (Exception ex)
            {
                _pendingCapture?.TrySetException(ex);
            }
        }

        private void OnEvent(int eventCode)
        {
            // Diagnóstico: registrar en un log qué códigos manda el SDK
            // ayudaría a descifrar su significado con el tiempo. Por ahora
            // no se actúa sobre esto — solo la imagen nos interesa para
            // la primera versión funcional.
        }

        public void Dispose() => Disconnect();
    }
}
