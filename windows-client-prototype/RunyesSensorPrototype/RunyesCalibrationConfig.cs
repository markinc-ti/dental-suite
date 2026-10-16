using System;
using System.IO;

namespace RunyesSensorPrototype
{
    /// <summary>
    /// Carpeta donde viven los archivos de calibración de cada sensor
    /// Runyes físico (Gain_Image, Offset_Image, Pixel_Map, con el
    /// número de serie como prefijo del nombre).
    ///
    /// Vive JUNTO al programa (no en una carpeta oculta de Windows),
    /// para que cualquier persona -- sin conocimientos técnicos -- pueda
    /// encontrarla fácilmente si algún día hay que agregar un sensor
    /// nuevo: solo arrastrar sus archivos ahí, sin escribir rutas ni
    /// buscar carpetas ocultas.
    ///
    /// Los sensores que la clínica ya tiene al momento de compilar el
    /// instalador vienen incluidos de fábrica (ver installer.iss) -- no
    /// hace falta copiar nada a mano para esos.
    /// </summary>
    public static class RunyesCalibrationConfig
    {
        public static string DefaultCalibrationDir { get; } = Path.Combine(
            AppContext.BaseDirectory, "SensorCalibration", "Runyes");

        public static void EnsureDirectoryExists()
        {
            Directory.CreateDirectory(DefaultCalibrationDir);
        }
    }
}
