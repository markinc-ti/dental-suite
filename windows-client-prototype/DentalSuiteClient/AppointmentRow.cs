using System;

namespace DentalSuiteClient
{
    public sealed class AppointmentRow
    {
        public string Id { get; init; } = "";
        public string? DentistId { get; init; }
        public DateTime StartsAt { get; init; }
        public DateTime EndsAt { get; init; }
        public string PatientName { get; init; } = "";
        public string? DoctorName { get; init; }
        public string? Reason { get; init; }
        public string Status { get; init; } = "scheduled";

        public string TimeRangeText => $"{StartsAt:HH:mm} - {EndsAt:HH:mm}";

        public string StatusText => Status switch
        {
            "scheduled" => "Agendada",
            "confirmed" => "Confirmada",
            "in_progress" => "En curso",
            "completed" => "Completada",
            "cancelled" => "Cancelada",
            "no_show" => "No se presentó",
            _ => Status,
        };
    }
}
