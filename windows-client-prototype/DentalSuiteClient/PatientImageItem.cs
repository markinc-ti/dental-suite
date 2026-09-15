using System;

namespace DentalSuiteClient
{
    public sealed class PatientImageItem
    {
        public string SlotId { get; init; } = "";
        public string ImagePath { get; init; } = "";
        public int ToothNumber { get; init; }
        public string ImageTypeLabel { get; init; } = "";
        public string ImageType { get; init; } = "xray";
        public DateTime? CapturedAt { get; init; }

        public string Caption => CapturedAt.HasValue
            ? $"Diente {ToothNumber} — {ImageTypeLabel} — {CapturedAt.Value:dd/MM/yyyy}"
            : $"Diente {ToothNumber} — {ImageTypeLabel}";
    }
}
