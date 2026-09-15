using System.Linq;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public sealed class PatientRow
    {
        public PatientDto Patient { get; init; } = null!;
        public string PrimaryDoctorName { get; init; } = "(sin asignar)";

        /// <summary>El tratamiento más relevante de este paciente: uno en
        /// progreso si existe; si no, el más reciente sin importar
        /// estado. Null si nunca ha tenido ninguno.</summary>
        public TreatmentDto? RelevantTreatment { get; init; }

        public string FullName => Patient.FullName;

        public bool HasActiveTreatment => RelevantTreatment?.Status == "in_progress";

        public string TreatmentStatusText
        {
            get
            {
                if (RelevantTreatment == null)
                    return "Sin tratamiento";

                int completed = RelevantTreatment.Sessions.Count(s => s.Status == "completed");
                int total = RelevantTreatment.TotalSessions;

                return RelevantTreatment.Status switch
                {
                    "in_progress" => $"En tratamiento: {RelevantTreatment.Name} ({completed}/{total})",
                    "completed" => $"Tratamiento completado: {RelevantTreatment.Name}",
                    "cancelled" => $"Tratamiento cancelado: {RelevantTreatment.Name}",
                    _ => RelevantTreatment.Name,
                };
            }
        }
    }
}
