using System;
using System.Runtime.InteropServices;

namespace RunyesSensorPrototype
{
    /// <summary>
    /// Wrapper P/Invoke sobre el SDK nativo de Runyes, extraído del
    /// instalador oficial (carpeta Sensor/ del instalador HandyDentist):
    ///   - sensor_io.dll     -> comunicación de bajo nivel con el sensor
    ///   - ImageCorrect.dll  -> corrección de imagen (gain/offset/pixel map)
    ///
    /// Las firmas de los parámetros (tipos, orden) NO vienen documentadas
    /// públicamente por el fabricante: se infirieron de los nombres de
    /// función exportados y de convenciones típicas de SDKs de sensores
    /// RVG. Antes de usar en producción, validar cada firma contra el
    /// comportamiento real del sensor conectado (ver notas en el README).
    /// </summary>
    internal static class RunyesSensorInterop
    {
        private const string SensorIoDll = "sensor_io.dll";
        private const string ImageCorrectDll = "ImageCorrect.dll";

        // Primer intento: StdCall. Es la convención más común en SDKs de
        // hardware para Windows (incluyendo la mayoría hechos en C/C++
        // sin especificar __cdecl explícito, que por defecto en muchos
        // compiladores de estas DLLs termina siendo __stdcall). Si el
        // problema persiste tras este cambio, el siguiente sospechoso es
        // el orden/tipo exacto de los parámetros, no ya la convención.

        // ---- sensor_io.dll ----------------------------------------------

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_DeviceCount();

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_DeviceList(
            [Out] byte[] buffer, int bufferSize);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr Sensor_OpenDevice(int deviceIndex);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_CloseDevice(IntPtr deviceHandle);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetDeviceInfo(
            IntPtr deviceHandle, [Out] byte[] buffer, int bufferSize);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetImageSize(
            IntPtr deviceHandle, out int width, out int height);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetImageBits(
            IntPtr deviceHandle, [Out] byte[] imageBuffer, int bufferSize);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetXrayState(IntPtr deviceHandle);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetLastError(IntPtr deviceHandle);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern IntPtr Sensor_GetLastErrorText(IntPtr deviceHandle);

        // ---- ImageCorrect.dll --------------------------------------------

        [DllImport(ImageCorrectDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_CorrectImage(
            [In, Out] byte[] rawImage,
            [Out] byte[] correctedImage,
            int width,
            int height,
            IntPtr pixelMap);

        [DllImport(ImageCorrectDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr Sensor_CreatePixelMap(
            [In] byte[] calibrationData, int width, int height);
    }
}
