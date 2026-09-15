using System.Linq;
using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public sealed class TreatmentRow
    {
        public TreatmentDto Treatment { get; init; } = null!;
        public string PatientName { get; init; } = "";
        public string DoctorName { get; init; } = "";

        public string Name => Treatment.Name;
        public string CostText => Treatment.EstimatedCost.HasValue
            ? $"${Treatment.EstimatedCost.Value:N2}" : "—";

        public int CompletedSessions => Treatment.Sessions.Count(s => s.Status == "completed");
        public int TotalSessions => Treatment.TotalSessions;
        public string ProgressText => $"{CompletedSessions} / {TotalSessions} sesiones";
        public double ProgressPercent => TotalSessions == 0 ? 0 : (100.0 * CompletedSessions / TotalSessions);

        public string StatusText => Treatment.Status switch
        {
            "in_progress" => "En progreso",
            "completed" => "Completado",
            "cancelled" => "Cancelado",
            _ => Treatment.Status,
        };
    }
}
