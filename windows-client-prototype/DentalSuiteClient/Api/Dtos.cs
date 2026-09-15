using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DentalSuiteClient.Api
{
    public sealed class ClinicDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }

    /// <summary>Datos capturados en el formulario de nuevo paciente.</summary>
    public sealed class NewPatientData
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? EmergencyContactRelationship { get; set; }
        public string? InitialDiagnosis { get; set; }
        public string? PrimaryDentistId { get; set; }
        public System.DateTime? FirstVisitDateTime { get; set; }
    }

    public sealed class UploadResultDto
    {
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }

    public sealed class TreatmentTypeDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("default_session_count")] public int DefaultSessionCount { get; set; } = 1;
        [JsonPropertyName("default_interval_days")] public int DefaultIntervalDays { get; set; } = 30;
        [JsonPropertyName("default_cost")] public decimal? DefaultCost { get; set; }
    }

    public sealed class TreatmentSessionDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("session_number")] public int SessionNumber { get; set; }
        [JsonPropertyName("scheduled_date")] public DateTime ScheduledDate { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "pending";
        [JsonPropertyName("appointment_id")] public string? AppointmentId { get; set; }
        [JsonPropertyName("completed_at")] public DateTime? CompletedAt { get; set; }
        [JsonPropertyName("notes")] public string? Notes { get; set; }
    }

    public sealed class TreatmentDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("patient_id")] public string PatientId { get; set; } = "";
        [JsonPropertyName("dentist_id")] public string DentistId { get; set; } = "";
        [JsonPropertyName("treatment_type_id")] public string? TreatmentTypeId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("estimated_cost")] public decimal? EstimatedCost { get; set; }
        [JsonPropertyName("total_sessions")] public int TotalSessions { get; set; }
        [JsonPropertyName("interval_days")] public int IntervalDays { get; set; } = 30;
        [JsonPropertyName("start_date")] public DateTime StartDate { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "in_progress";
        [JsonPropertyName("notes")] public string? Notes { get; set; }
        [JsonPropertyName("sessions")] public List<TreatmentSessionDto> Sessions { get; set; } = new();
    }

    public sealed class PatientDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("clinic_id")] public string? ClinicId { get; set; }
        [JsonPropertyName("first_name")] public string FirstName { get; set; } = "";
        [JsonPropertyName("last_name")] public string LastName { get; set; } = "";
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("address")] public string? Address { get; set; }
        [JsonPropertyName("emergency_contact_name")] public string? EmergencyContactName { get; set; }
        [JsonPropertyName("emergency_contact_phone")] public string? EmergencyContactPhone { get; set; }
        [JsonPropertyName("emergency_contact_relationship")] public string? EmergencyContactRelationship { get; set; }
        [JsonPropertyName("external_reference")] public string? ExternalReference { get; set; }
        [JsonPropertyName("primary_dentist_id")] public string? PrimaryDentistId { get; set; }

        public string FullName => $"{LastName}, {FirstName}";
    }

    public sealed class UserDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("full_name")] public string FullName { get; set; } = "";
        [JsonPropertyName("username")] public string Username { get; set; } = "";
        [JsonPropertyName("role")] public string Role { get; set; } = "dentist";
        [JsonPropertyName("phone")] public string? Phone { get; set; }
        [JsonPropertyName("specialty")] public string? Specialty { get; set; }
        [JsonPropertyName("professional_license")] public string? ProfessionalLicense { get; set; }
    }

    public sealed class AppointmentDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("patient_id")] public string PatientId { get; set; } = "";
        [JsonPropertyName("dentist_id")] public string DentistId { get; set; } = "";
        [JsonPropertyName("starts_at")] public DateTime StartsAt { get; set; }
        [JsonPropertyName("ends_at")] public DateTime EndsAt { get; set; }
        [JsonPropertyName("reason")] public string? Reason { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "scheduled";
    }

    public sealed class ToothImageSlotDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("tooth_number")] public int ToothNumber { get; set; }
        [JsonPropertyName("image_type")] public string ImageType { get; set; } = "xray";
        [JsonPropertyName("is_captured")] public bool IsCaptured { get; set; }
        [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
        [JsonPropertyName("image_order")] public int? ImageOrder { get; set; }
        [JsonPropertyName("captured_at")] public DateTime? CapturedAt { get; set; }
    }

    public sealed class XRayStudyDto
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("patient_id")] public string PatientId { get; set; } = "";
        [JsonPropertyName("dentition_type")] public string DentitionType { get; set; } = "permanent";
        [JsonPropertyName("slots")] public ToothImageSlotDto[] Slots { get; set; } = Array.Empty<ToothImageSlotDto>();
    }
}
