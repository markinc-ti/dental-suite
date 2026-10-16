using System;
using System.Runtime.InteropServices;

namespace RunyesSensorPrototype
{
    /// <summary>
    /// Wrapper P/Invoke sobre sensor_io.dll, con las firmas CONFIRMADAS
    /// contra el hardware real (sesión de depuración en vivo con x32dbg
    /// sobre USB Series.exe, disparando el sensor de verdad). A
    /// diferencia de la versión anterior de este archivo, aquí ya no hay
    /// firmas especulativas para las funciones que usamos en la captura.
    ///
    /// Hallazgos confirmados en vivo:
    ///   - Sensor_GetImageSize(handle) devuelve un solo int: el tamaño
    ///     total en bytes de la imagen (NO ancho/alto por separado).
    ///   - Sensor_GetImageBits, llamada por separado DESPUÉS de
    ///     Sensor_LoadRawImage, devuelve éxito pero con el buffer lleno
    ///     de ceros (el estado interno del sensor ya se había
    ///     consumido). Por eso ya no se usa.
    ///   - Sensor_LoadRawImage(handle, buffer) es la función correcta:
    ///     escribe los bytes crudos de la imagen directamente en el
    ///     buffer que le pasas. Confirmado con captura real: 3,840,000
    ///     bytes, imagen reconocible como radiografía tras interpretarla
    ///     como 1200x1600 píxeles, 16 bits, BIG-ENDIAN.
    ///   - Sensor_GetXrayState(handle): 0 en reposo, 1 justo en el
    ///     instante del disparo (dura muy poco, hay que hacer polling
    ///     rápido, cada ~20ms).
    ///   - Sensor_CorrectImage / Sensor_CreatePixelMap (de
    ///     ImageCorrect.dll) NUNCA se confirmaron funcionando con una
    ///     firma correcta. Se abandonó ese camino: en vez de pedirle al
    ///     DLL que corrija la imagen, implementamos la corrección
    ///     nosotros mismos en software (ver RunyesImageCorrection.cs),
    ///     usando los archivos de calibración (Gain_Image, Offset_Image,
    ///     Pixel_Map) que Runyes entrega por cada sensor físico. Por eso
    ///     esas dos funciones ya no están declaradas aquí.
    /// </summary>
    internal static class RunyesSensorInterop
    {
        private const string SensorIoDll = "sensor_io.dll";

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_DeviceCount();

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr Sensor_OpenDevice(int deviceIndex);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_CloseDevice(IntPtr deviceHandle);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetDeviceInfo(
            IntPtr deviceHandle, [Out] byte[] buffer, int bufferSize);

        /// <summary>CONFIRMADO: regresa 0 en reposo, 1 en el instante del
        /// disparo de rayos X. El pulso dura muy poco -- haz polling con
        /// intervalos cortos (20-50ms) para no perderlo.</summary>
        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetXrayState(IntPtr deviceHandle);

        /// <summary>CONFIRMADO: regresa el tamaño en bytes que debe tener
        /// el buffer para Sensor_LoadRawImage. Para el sensor probado en
        /// vivo (1200x1600, 16 bits): 3,840,000.</summary>
        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetImageSize(IntPtr deviceHandle);

        /// <summary>CONFIRMADO: esta es la función real de captura. El
        /// segundo parámetro es un buffer de ESCRITURA (no un código):
        /// pasar un entero pequeño en vez de un buffer real provoca
        /// "access violation" porque la función trata ese valor como
        /// una dirección de memoria donde escribir. Regresa 1 en éxito
        /// en las pruebas realizadas.</summary>
        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_LoadRawImage(
            IntPtr deviceHandle, [Out] byte[] buffer);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Sensor_GetLastError(IntPtr deviceHandle);

        [DllImport(SensorIoDll, CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern IntPtr Sensor_GetLastErrorText(IntPtr deviceHandle);

        // ------------------------------------------------------------------
        // NO CONFIRMADAS / ya no se usan (se dejan documentadas por si
        // algún día se retoma este camino en vez de la corrección en
        // software):
        //
        // Sensor_GetImageBits(handle, buffer, size) -- devuelve el buffer
        //   en ceros si se llama después de Sensor_LoadRawImage.
        //
        // Sensor_CorrectImage / Sensor_CreatePixelMap (ImageCorrect.dll)
        //   -- nunca se probaron con una firma que funcionara.
        // ------------------------------------------------------------------
    }
}
