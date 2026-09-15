import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import '../models/models.dart';

/// Habla con el mismo backend que la app de escritorio — la URL del
/// servidor se guarda una sola vez (ver [ServerConfig]), igual que en
/// Windows, para no tener que compilar la app de nuevo si el servidor
/// cambia de dirección.
class ApiClient {
  final String baseUrl;
  String? _clinicId;

  ApiClient(this.baseUrl);

  Uri _uri(String path, [Map<String, String>? query]) =>
      Uri.parse('$baseUrl$path').replace(queryParameters: query);

  Future<String> ensureClinicId() async {
    if (_clinicId != null) return _clinicId!;

    final response = await http.get(_uri('/clinics'));
    final clinics = (jsonDecode(response.body) as List)
        .map((c) => ClinicModel.fromJson(c))
        .toList();

    if (clinics.isEmpty) {
      throw Exception('El servidor todavía no tiene ninguna clínica configurada.');
    }
    _clinicId = clinics.first.id;
    return _clinicId!;
  }

  Future<UserModel?> login(String username, String password) async {
    final response = await http.post(
      _uri('/auth/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'username': username, 'password': password}),
    );
    if (response.statusCode != 200) return null;
    return UserModel.fromJson(jsonDecode(response.body));
  }

  Future<List<PatientModel>> searchPatients([String? search]) async {
    final clinicId = await ensureClinicId();
    final params = {'clinic_id': clinicId, if (search != null && search.isNotEmpty) 'search': search};
    final response = await http.get(_uri('/patients', params));
    _checkOk(response);
    return (jsonDecode(response.body) as List).map((p) => PatientModel.fromJson(p)).toList();
  }

  Future<List<UserModel>> listDoctors() async {
    final clinicId = await ensureClinicId();
    final response = await http.get(_uri('/users', {'clinic_id': clinicId, 'role': 'dentist'}));
    _checkOk(response);
    return (jsonDecode(response.body) as List).map((u) => UserModel.fromJson(u)).toList();
  }

  Future<List<AppointmentModel>> listAppointments(DateTime from, DateTime to) async {
    final clinicId = await ensureClinicId();
    final response = await http.get(_uri('/appointments', {
      'clinic_id': clinicId,
      'date_from': from.toIso8601String(),
      'date_to': to.toIso8601String(),
    }));
    _checkOk(response);
    return (jsonDecode(response.body) as List).map((a) => AppointmentModel.fromJson(a)).toList();
  }

  Future<List<TreatmentModel>> listTreatments({String? patientId}) async {
    final clinicId = await ensureClinicId();
    final params = {'clinic_id': clinicId, if (patientId != null) 'patient_id': patientId};
    final response = await http.get(_uri('/treatments', params));
    _checkOk(response);
    return (jsonDecode(response.body) as List).map((t) => TreatmentModel.fromJson(t)).toList();
  }

  Future<List<StudyModel>> listPatientStudies(String patientId) async {
    final response = await http.get(_uri('/xray-studies/patient/$patientId'));
    _checkOk(response);
    return (jsonDecode(response.body) as List).map((s) => StudyModel.fromJson(s)).toList();
  }

  void _checkOk(http.Response response) {
    if (response.statusCode < 200 || response.statusCode >= 300) {
      String detail = 'Código ${response.statusCode}';
      try {
        final body = jsonDecode(response.body);
        if (body is Map && body['detail'] != null) detail = body['detail'].toString();
      } catch (_) {}
      throw Exception(detail);
    }
  }
}

/// Guarda la URL del servidor elegida la primera vez, igual que en la
/// app de escritorio.
class ServerConfig {
  static const _key = 'api_base_url';

  static Future<String?> getSavedBaseUrl() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_key);
  }

  static Future<void> saveBaseUrl(String url) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_key, url.trim().replaceAll(RegExp(r'/+$'), ''));
  }
}
