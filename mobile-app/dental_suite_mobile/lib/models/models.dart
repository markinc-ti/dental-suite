class ClinicModel {
  final String id;
  final String name;

  ClinicModel({required this.id, required this.name});

  factory ClinicModel.fromJson(Map<String, dynamic> json) =>
      ClinicModel(id: json['id'], name: json['name']);
}

class UserModel {
  final String id;
  final String fullName;
  final String username;
  final String role;
  final String? phone;
  final String? specialty;

  UserModel({
    required this.id,
    required this.fullName,
    required this.username,
    required this.role,
    this.phone,
    this.specialty,
  });

  bool get isAdmin => role == 'admin';

  factory UserModel.fromJson(Map<String, dynamic> json) => UserModel(
        id: json['id'],
        fullName: json['full_name'],
        username: json['username'],
        role: json['role'],
        phone: json['phone'],
        specialty: json['specialty'],
      );
}

class PatientModel {
  final String id;
  final String firstName;
  final String lastName;
  final String? phone;
  final String? email;
  final String? address;
  final String? primaryDentistId;

  PatientModel({
    required this.id,
    required this.firstName,
    required this.lastName,
    this.phone,
    this.email,
    this.address,
    this.primaryDentistId,
  });

  String get fullName => '$lastName, $firstName';

  factory PatientModel.fromJson(Map<String, dynamic> json) => PatientModel(
        id: json['id'],
        firstName: json['first_name'],
        lastName: json['last_name'],
        phone: json['phone'],
        email: json['email'],
        address: json['address'],
        primaryDentistId: json['primary_dentist_id'],
      );
}

class AppointmentModel {
  final String id;
  final String patientId;
  final String dentistId;
  final DateTime startsAt;
  final DateTime endsAt;
  final String? reason;
  final String status;

  AppointmentModel({
    required this.id,
    required this.patientId,
    required this.dentistId,
    required this.startsAt,
    required this.endsAt,
    this.reason,
    required this.status,
  });

  factory AppointmentModel.fromJson(Map<String, dynamic> json) => AppointmentModel(
        id: json['id'],
        patientId: json['patient_id'],
        dentistId: json['dentist_id'],
        startsAt: DateTime.parse(json['starts_at']),
        endsAt: DateTime.parse(json['ends_at']),
        reason: json['reason'],
        status: json['status'],
      );
}

class TreatmentSessionModel {
  final String id;
  final int sessionNumber;
  final DateTime scheduledDate;
  final String status;

  TreatmentSessionModel({
    required this.id,
    required this.sessionNumber,
    required this.scheduledDate,
    required this.status,
  });

  factory TreatmentSessionModel.fromJson(Map<String, dynamic> json) => TreatmentSessionModel(
        id: json['id'],
        sessionNumber: json['session_number'],
        scheduledDate: DateTime.parse(json['scheduled_date']),
        status: json['status'],
      );
}

class TreatmentModel {
  final String id;
  final String patientId;
  final String dentistId;
  final String name;
  final double? estimatedCost;
  final int totalSessions;
  final String status;
  final List<TreatmentSessionModel> sessions;

  TreatmentModel({
    required this.id,
    required this.patientId,
    required this.dentistId,
    required this.name,
    this.estimatedCost,
    required this.totalSessions,
    required this.status,
    required this.sessions,
  });

  int get completedSessions => sessions.where((s) => s.status == 'completed').length;
  double get progress => totalSessions == 0 ? 0 : completedSessions / totalSessions;

  factory TreatmentModel.fromJson(Map<String, dynamic> json) => TreatmentModel(
        id: json['id'],
        patientId: json['patient_id'],
        dentistId: json['dentist_id'],
        name: json['name'],
        estimatedCost: json['estimated_cost'] == null ? null : (json['estimated_cost'] as num).toDouble(),
        totalSessions: json['total_sessions'],
        status: json['status'],
        sessions: (json['sessions'] as List)
            .map((s) => TreatmentSessionModel.fromJson(s))
            .toList(),
      );
}

class ToothImageModel {
  final int toothNumber;
  final String imageType;
  final bool isCaptured;
  final String? imageUrl;
  final DateTime? capturedAt;

  ToothImageModel({
    required this.toothNumber,
    required this.imageType,
    required this.isCaptured,
    this.imageUrl,
    this.capturedAt,
  });

  String get typeLabel => imageType == 'photo' ? 'Foto' : 'Radiografía';

  factory ToothImageModel.fromJson(Map<String, dynamic> json) => ToothImageModel(
        toothNumber: json['tooth_number'],
        imageType: json['image_type'],
        isCaptured: json['is_captured'] ?? false,
        imageUrl: json['image_url'],
        capturedAt: json['captured_at'] == null ? null : DateTime.parse(json['captured_at']),
      );
}

class StudyModel {
  final String id;
  final DateTime takenAt;
  final List<ToothImageModel> slots;

  StudyModel({required this.id, required this.takenAt, required this.slots});

  factory StudyModel.fromJson(Map<String, dynamic> json) => StudyModel(
        id: json['id'],
        takenAt: DateTime.parse(json['taken_at']),
        slots: (json['slots'] as List).map((s) => ToothImageModel.fromJson(s)).toList(),
      );
}
