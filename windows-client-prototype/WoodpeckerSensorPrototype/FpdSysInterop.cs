using System;
using System.Runtime.InteropServices;

namespace WoodpeckerSensorPrototype
{
    /// <summary>
    /// Wrapper P/Invoke sobre FpdSys.dll — la capa genérica que usa
    /// Ai-Dental (Woodpecker/DBA) para hablar con el sensor detectado
    /// (en tu caso, confirmado como modelo E4S). A diferencia de
    /// XVDetector.dll, estos nombres NO vienen decorados con "@N", así
    /// que no sabemos con certeza cuántos parámetros tiene cada función
    /// solo con el nombre — las firmas de abajo son la mejor suposición
    /// razonable, pendientes de confirmar con el depurador contra el
    /// software real (ver README).
    ///
    /// Se incluye "ScanOnce" porque, a diferencia del patrón de eventos
    /// de XVDetector, esta función suena a que hace todo en una sola
    /// llamada (disparar + esperar + entregar) — si funciona así, sería
    /// la ruta más simple de las dos.
    /// </summary>
    internal static class FpdSysInterop
    {
        private const string FpdSysDll = "FpdSys.dll";

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr Create(int productType);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int Destroy(IntPtr handle);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int GetProdCount();

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall,
            CharSet = CharSet.Ansi)]
        internal static extern int GetProdList([Out] byte[] buffer, int bufferSize);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int GetAttr(IntPtr handle, int attrId, [Out] byte[] valueBuffer, int bufferSize);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int SetAttr(IntPtr handle, int attrId, byte[] valueBuffer, int bufferSize);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int GetAttrIDList([Out] int[] idBuffer, int bufferSize);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int GetAttrsCount();

        /// <summary>
        /// Dispara una captura completa de manera sincrónica (a
        /// confirmar). Si funciona así, es mucho más simple que el
        /// patrón Create+RegisterScanNotify+Invoke.
        /// </summary>
        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int ScanOnce(IntPtr handle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void ScanNotifyCallback(IntPtr handle, int eventCode, IntPtr userData);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int RegisterScanNotify(IntPtr handle, ScanNotifyCallback callback, IntPtr userData);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int QueryImageBuf(IntPtr handle, out int width, out int height);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int GetImageFromBuf(IntPtr handle, [Out] byte[] imageBuffer, int bufferSize);

        [DllImport(FpdSysDll, CallingConvention = CallingConvention.StdCall)]
        internal static extern int GetErrInfo(int errorCode, [Out] byte[] messageBuffer, int bufferSize);
    }
}
