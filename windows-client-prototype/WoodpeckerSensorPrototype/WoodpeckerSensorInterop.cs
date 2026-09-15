using System;
using System.Runtime.InteropServices;

namespace WoodpeckerSensorPrototype
{
    /// <summary>
    /// Wrapper P/Invoke sobre XVDetector.dll, extraído del instalador de
    /// Ai-Dental (Woodpecker/DBA, carpeta Ai-Dental-Client).
    ///
    /// A diferencia del SDK de Runyes, aquí los nombres exportados vienen
    /// decorados al estilo clásico de stdcall de 32 bits:
    ///   _XVD_InitDetector@4
    /// El número después de la '@' es la cantidad EXACTA de bytes de
    /// parámetros (generada automáticamente por el compilador) — así que
    /// el conteo de parámetros de cada función es un hecho confirmado,
    /// no una suposición. Lo que sigue siendo una suposición razonable es
    /// el tipo exacto de cada parámetro dentro de ese tamaño ya conocido.
    /// </summary>
    internal static class WoodpeckerSensorInterop
    {
        private const string XvDetectorDll = "XVDetector.dll";

        // _XVD_InitDetector@4 -> 1 parámetro de 4 bytes. Se asume un
        // índice de dispositivo (int), siguiendo el mismo patrón que
        // Sensor_OpenDevice en el SDK de Runyes.
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_InitDetector@4",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int XVD_InitDetector(int deviceIndex);

        // _XVD_ReleaseDetector@0 -> sin parámetros.
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_ReleaseDetector@0",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern void XVD_ReleaseDetector();

        // _XVD_Expose@0 -> sin parámetros. Dispara la exposición.
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_Expose@0",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int XVD_Expose();

        // _XVD_SetExposeTime@4 -> 1 parámetro de 4 bytes (int, milisegundos o unidad propia).
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_SetExposeTime@4",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int XVD_SetExposeTime(int exposeTime);

        // _XVD_SetExposeType@4 -> 1 parámetro (probablemente un enum: manual/auto).
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_SetExposeType@4",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int XVD_SetExposeType(int exposeType);

        // _XVD_SetVoltage@4 -> 1 parámetro (kV del generador de rayos X).
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_SetVoltage@4",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int XVD_SetVoltage(int voltage);

        // _XVD_SetCalibrationEnable@4 -> 1 parámetro (bool como int).
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_SetCalibrationEnable@4",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int XVD_SetCalibrationEnable(int enable);

        // _XVD_LoadCalibrationTemplateFile@4 -> 1 parámetro: ruta del
        // archivo de calibración (string ANSI). Aprendimos con Runyes que
        // la calibración es indispensable — aquí el propio SDK ya trae
        // una función dedicada a cargarla explícitamente.
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_LoadCalibrationTemplateFile@4",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        internal static extern int XVD_LoadCalibrationTemplateFile(string filePath);

        // _XVD_GenerateCalibrationTemplateFile@8 -> 2 parámetros de 4 bytes.
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_GenerateCalibrationTemplateFile@8",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int XVD_GenerateCalibrationTemplateFile(int param1, int param2);

        // _XVD_SetSaveDateType@4 -> 1 parámetro.
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_SetSaveDateType@4",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int XVD_SetSaveDateType(int saveDateType);

        // _XVD_GetDllVersion@4 -> 1 parámetro: buffer de salida para el texto de versión.
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_GetDllVersion@4",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        internal static extern int XVD_GetDllVersion([Out] byte[] versionBuffer);

        // _XVD_GetHWVersion@4 -> 1 parámetro: buffer de salida.
        [DllImport(XvDetectorDll, EntryPoint = "_XVD_GetHWVersion@4",
            CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        internal static extern int XVD_GetHWVersion([Out] byte[] versionBuffer);

        /// <summary>
        /// Firma del callback que el SDK llama cuando hay una imagen
        /// lista. No confirmada — es la suposición más común para este
        /// tipo de SDK: puntero a los datos, ancho, alto. Puede que en
        /// realidad reciba menos o más parámetros; si el callback nunca
        /// se dispara, o se dispara con valores sin sentido, este es el
        /// primer lugar a ajustar.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void ImageValidCallback(IntPtr imageDataPtr, int width, int height);

        // _RegisterImageValidCallBack@4 -> 1 parámetro: puntero a función callback.
        [DllImport(XvDetectorDll, EntryPoint = "_RegisterImageValidCallBack@4",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int RegisterImageValidCallBack(ImageValidCallback callback);

        /// <summary>
        /// Firma del callback de eventos generales (progreso, errores,
        /// estado). No confirmada — se asume un único código entero.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void EventCallback(int eventCode);

        // _RegisterEventCallBack@4 -> 1 parámetro: puntero a función callback.
        [DllImport(XvDetectorDll, EntryPoint = "_RegisterEventCallBack@4",
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int RegisterEventCallBack(EventCallback callback);
    }
}
