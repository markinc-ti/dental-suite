using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    public sealed class TreatmentSessionRow
    {
        public TreatmentSessionDto Session { get; init; } = null!;

        public int SessionNumber => Session.SessionNumber;
        public string ScheduledDateText => Session.ScheduledDate.ToString("dd/MM/yyyy HH:mm");
        public bool CanComplete => Session.Status == "pending";

        public string StatusText => Session.Status switch
        {
            "pending" => "Pendiente",
            "completed" => "Completada",
            "cancelled" => "Cancelada",
            _ => Session.Status,
        };
    }
}
