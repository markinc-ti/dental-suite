using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace DentalSuiteClient.Api
{
    /// <summary>
    /// Envuelve las llamadas al backend en la nube (la URL la elige el
    /// usuario una vez, ver ServerConfig — ya no es un backend local
    /// arrancado por la propia app). Resuelve solo el clinic_id una vez
    /// por sesión: en instalaciones de un solo consultorio no tiene
    /// sentido pedírselo al usuario, así que si no existe ninguna
    /// clínica se crea una por defecto en el primer arranque.
    /// </summary>
    public sealed class ApiClient
    {
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri(ServerConfig.GetSavedBaseUrl() ?? "http://127.0.0.1:8003")
        };
        private string? _clinicId;

        public async Task<string> EnsureClinicAsync()
        {
            if (_clinicId != null)
                return _clinicId;

            var clinics = await _http.GetFromJsonAsync<List<ClinicDto>>("/clinics")
                          ?? new List<ClinicDto>();

            if (clinics.Count == 0)
            {
                var created = await _http.PostAsJsonAsync(
                    "/clinics", new { name = "Mi Consultorio" });
                await created.EnsureSuccessWithDetailAsync();
                var clinic = await created.Content.ReadFromJsonAsync<ClinicDto>();
                _clinicId = clinic!.Id;
            }
            else
            {
                _clinicId = clinics[0].Id; // instalación de un solo consultorio: la primera y única
            }

            return _clinicId;
        }

        public async Task<List<PatientDto>> SearchPatientsAsync(string? search = null)
        {
            string clinicId = await EnsureClinicAsync();
            string url = $"/patients?clinic_id={clinicId}";
            if (!string.IsNullOrWhiteSpace(search))
                url += $"&search={Uri.EscapeDataString(search)}";

            return await _http.GetFromJsonAsync<List<PatientDto>>(url) ?? new List<PatientDto>();
        }

        public async Task<PatientDto> CreatePatientAsync(NewPatientData data)
        {
            string clinicId = await EnsureClinicAsync();
            var response = await _http.PostAsJsonAsync(
                $"/patients?clinic_id={clinicId}",
                new
                {
                    first_name = data.FirstName,
                    last_name = data.LastName,
                    phone = data.Phone,
                    email = data.Email,
                    address = data.Address,
                    emergency_contact_name = data.EmergencyContactName,
                    emergency_contact_phone = data.EmergencyContactPhone,
                    emergency_contact_relationship = data.EmergencyContactRelationship,
                    primary_dentist_id = data.PrimaryDentistId,
                });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<PatientDto>())!;
        }

        public async Task<PatientDto> UpdatePatientAsync(string patientId, NewPatientData data)
        {
            var response = await _http.PutAsJsonAsync(
                $"/patients/{patientId}",
                new
                {
                    first_name = data.FirstName,
                    last_name = data.LastName,
                    phone = data.Phone,
                    email = data.Email,
                    address = data.Address,
                    emergency_contact_name = data.EmergencyContactName,
                    emergency_contact_phone = data.EmergencyContactPhone,
                    emergency_contact_relationship = data.EmergencyContactRelationship,
                    primary_dentist_id = data.PrimaryDentistId,
                });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<PatientDto>())!;
        }

        public async Task DeletePatientAsync(string patientId)
        {
            var response = await _http.DeleteAsync($"/patients/{patientId}");
            await response.EnsureSuccessWithDetailAsync();
        }

        public async Task CreateClinicalNoteAsync(string patientId, string authorId, string content)
        {
            var response = await _http.PostAsJsonAsync(
                "/clinical-notes",
                new { patient_id = patientId, author_id = authorId, content });
            await response.EnsureSuccessWithDetailAsync();
        }

        public async Task<XRayStudyDto> CreateStudyAsync(string patientId, string sensorBrand = "Runyes")
        {
            string clinicId = await EnsureClinicAsync();
            var response = await _http.PostAsJsonAsync(
                $"/xray-studies?clinic_id={clinicId}",
                new { patient_id = patientId, sensor_brand = sensorBrand });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<XRayStudyDto>())!;
        }

        public async Task<string> UploadImageFileAsync(string localFilePath)
        {
            using var content = new MultipartFormDataContent();
            byte[] fileBytes = System.IO.File.ReadAllBytes(localFilePath);
            var fileContent = new ByteArrayContent(fileBytes);
            content.Add(fileContent, "file", System.IO.Path.GetFileName(localFilePath));

            var response = await _http.PostAsync("/uploads/image", content);
            await response.EnsureSuccessWithDetailAsync();

            var result = await response.Content.ReadFromJsonAsync<UploadResultDto>();
            // El backend devuelve una ruta relativa ("/uploads/xxx.png")
            // — se combina con la URL base del servidor para que quede
            // como una dirección completa, usable desde cualquier
            // dispositivo (no solo desde esta app).
            return $"{_http.BaseAddress}{result!.Url.TrimStart('/')}";
        }

        public async Task UpdateSlotImageAsync(string slotId, int toothNumber, string imageType, string imageUrl)
        {
            var response = await _http.PutAsJsonAsync(
                $"/xray-studies/slots/{slotId}",
                new { tooth_number = toothNumber, image_type = imageType, image_url = imageUrl });
            await response.EnsureSuccessWithDetailAsync();
        }

        public async Task<ToothImageSlotDto> UploadSlotAsync(
            string studyId, int toothNumber, string imageType, string imageUrl, int? imageOrder)
        {
            var response = await _http.PostAsJsonAsync(
                $"/xray-studies/{studyId}/slots",
                new { tooth_number = toothNumber, image_type = imageType, image_url = imageUrl, image_order = imageOrder });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<ToothImageSlotDto>())!;
        }

        public async Task<UserDto?> LoginAsync(string username, string password)
        {
            var response = await _http.PostAsJsonAsync("/auth/login", new { username, password });
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<UserDto>();
        }

        public async Task<bool> AdminExistsAsync()
        {
            string clinicId = await EnsureClinicAsync();
            var admins = await _http.GetFromJsonAsync<List<UserDto>>(
                $"/users?clinic_id={clinicId}&role=admin") ?? new List<UserDto>();
            return admins.Count > 0;
        }

        public async Task<UserDto> CreateAdminAsync(string fullName, string username, string password)
        {
            string clinicId = await EnsureClinicAsync();
            var response = await _http.PostAsJsonAsync(
                $"/users?clinic_id={clinicId}",
                new { full_name = fullName, username, password, role = "admin" });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<UserDto>())!;
        }

        public async Task<List<TreatmentTypeDto>> ListTreatmentTypesAsync()
        {
            string clinicId = await EnsureClinicAsync();
            return await _http.GetFromJsonAsync<List<TreatmentTypeDto>>(
                $"/treatment-types?clinic_id={clinicId}") ?? new List<TreatmentTypeDto>();
        }

        public async Task<TreatmentTypeDto> CreateTreatmentTypeAsync(
            string name, string? description, int sessionCount, int intervalDays, decimal? cost)
        {
            string clinicId = await EnsureClinicAsync();
            var response = await _http.PostAsJsonAsync(
                $"/treatment-types?clinic_id={clinicId}",
                new
                {
                    name,
                    description,
                    default_session_count = sessionCount,
                    default_interval_days = intervalDays,
                    default_cost = cost,
                });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<TreatmentTypeDto>())!;
        }

        public async Task<List<TreatmentDto>> ListTreatmentsAsync()
        {
            string clinicId = await EnsureClinicAsync();
            return await _http.GetFromJsonAsync<List<TreatmentDto>>(
                $"/treatments?clinic_id={clinicId}") ?? new List<TreatmentDto>();
        }

        public async Task<TreatmentDto> CreateTreatmentAsync(
            string patientId, string dentistId, string? treatmentTypeId, string name,
            decimal? estimatedCost, int totalSessions, int intervalDays,
            DateTime startDate, string startTime, int sessionDurationMinutes, string? notes)
        {
            string clinicId = await EnsureClinicAsync();
            var response = await _http.PostAsJsonAsync(
                $"/treatments?clinic_id={clinicId}",
                new
                {
                    patient_id = patientId,
                    dentist_id = dentistId,
                    treatment_type_id = treatmentTypeId,
                    name,
                    estimated_cost = estimatedCost,
                    total_sessions = totalSessions,
                    interval_days = intervalDays,
                    start_date = startDate,
                    start_time = startTime,
                    session_duration_minutes = sessionDurationMinutes,
                    notes,
                });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<TreatmentDto>())!;
        }

        public async Task<TreatmentDto> GetTreatmentAsync(string treatmentId) =>
            (await _http.GetFromJsonAsync<TreatmentDto>($"/treatments/{treatmentId}"))!;

        public async Task<TreatmentSessionDto> RescheduleSessionAsync(string sessionId, DateTime newDate)
        {
            var response = await _http.PutAsJsonAsync(
                $"/treatment-sessions/{sessionId}/reschedule", new { new_date = newDate });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<TreatmentSessionDto>())!;
        }

        public async Task<TreatmentSessionDto> AddExtraSessionAsync(
            string treatmentId, DateTime scheduledDate, int durationMinutes, string? notes)
        {
            var response = await _http.PostAsJsonAsync(
                $"/treatments/{treatmentId}/sessions",
                new { scheduled_date = scheduledDate, duration_minutes = durationMinutes, notes });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<TreatmentSessionDto>())!;
        }

        public async Task CompleteSessionAsync(string sessionId)
        {
            var response = await _http.PutAsync($"/treatment-sessions/{sessionId}/complete", null);
            await response.EnsureSuccessWithDetailAsync();
        }

        public async Task UpdateTreatmentStatusAsync(string treatmentId, string status)
        {
            var response = await _http.PutAsync(
                $"/treatments/{treatmentId}/status?status={status}", null);
            await response.EnsureSuccessWithDetailAsync();
        }
        public async Task<List<XRayStudyDto>> ListPatientStudiesAsync(string patientId) =>
            await _http.GetFromJsonAsync<List<XRayStudyDto>>($"/xray-studies/patient/{patientId}")
            ?? new List<XRayStudyDto>();

        public async Task<List<UserDto>> ListDoctorsAsync()
        {
            string clinicId = await EnsureClinicAsync();
            return await _http.GetFromJsonAsync<List<UserDto>>(
                $"/users?clinic_id={clinicId}&role=dentist") ?? new List<UserDto>();
        }

        public async Task<UserDto> CreateDoctorAsync(
            string fullName, string username, string password, string? phone, string? specialty,
            string? professionalLicense)
        {
            string clinicId = await EnsureClinicAsync();
            var response = await _http.PostAsJsonAsync(
                $"/users?clinic_id={clinicId}",
                new
                {
                    full_name = fullName,
                    username,
                    password,
                    role = "dentist",
                    phone,
                    specialty,
                    professional_license = professionalLicense,
                });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<UserDto>())!;
        }

        public async Task<UserDto> UpdateDoctorAsync(
            string doctorId, string fullName, string? phone, string? specialty, string? professionalLicense)
        {
            var response = await _http.PutAsJsonAsync(
                $"/users/{doctorId}",
                new
                {
                    full_name = fullName,
                    phone,
                    specialty,
                    professional_license = professionalLicense,
                });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<UserDto>())!;
        }

        public async Task DeleteDoctorAsync(string doctorId)
        {
            var response = await _http.DeleteAsync($"/users/{doctorId}");
            await response.EnsureSuccessWithDetailAsync();
        }

        public async Task<List<AppointmentDto>> ListAppointmentsAsync(DateTime dateFrom, DateTime dateTo)
        {
            string clinicId = await EnsureClinicAsync();
            string url = $"/appointments?clinic_id={clinicId}"
                + $"&date_from={Uri.EscapeDataString(dateFrom.ToString("s"))}"
                + $"&date_to={Uri.EscapeDataString(dateTo.ToString("s"))}";
            return await _http.GetFromJsonAsync<List<AppointmentDto>>(url) ?? new List<AppointmentDto>();
        }

        public async Task<AppointmentDto> CreateAppointmentAsync(
            string patientId, string dentistId, DateTime startsAt, DateTime endsAt, string? reason)
        {
            string clinicId = await EnsureClinicAsync();

            var response = await _http.PostAsJsonAsync(
                $"/appointments?clinic_id={clinicId}",
                new
                {
                    patient_id = patientId,
                    dentist_id = dentistId,
                    starts_at = startsAt,
                    ends_at = endsAt,
                    reason,
                });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<AppointmentDto>())!;
        }

        public async Task<AppointmentDto> UpdateAppointmentAsync(
            string appointmentId, string dentistId, DateTime startsAt, DateTime endsAt, string? reason)
        {
            var response = await _http.PutAsJsonAsync(
                $"/appointments/{appointmentId}",
                new
                {
                    dentist_id = dentistId,
                    starts_at = startsAt,
                    ends_at = endsAt,
                    reason,
                });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<AppointmentDto>())!;
        }

        public async Task<AppointmentDto> UpdateAppointmentStatusAsync(string appointmentId, string status)
        {
            var response = await _http.PutAsJsonAsync(
                $"/appointments/{appointmentId}", new { status });
            await response.EnsureSuccessWithDetailAsync();
            return (await response.Content.ReadFromJsonAsync<AppointmentDto>())!;
        }
    }
}
