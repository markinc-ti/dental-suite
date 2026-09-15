import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../models/models.dart';
import '../services/api_client.dart';

class AgendaScreen extends StatefulWidget {
  final ApiClient api;
  const AgendaScreen({super.key, required this.api});

  @override
  State<AgendaScreen> createState() => _AgendaScreenState();
}

class _AgendaScreenState extends State<AgendaScreen> {
  DateTime _selectedDay = DateTime.now();
  List<AppointmentModel> _appointments = [];
  Map<String, String> _patientNames = {};
  Map<String, String> _doctorNames = {};
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final dayStart = DateTime(_selectedDay.year, _selectedDay.month, _selectedDay.day);
      final dayEnd = dayStart.add(const Duration(days: 1)).subtract(const Duration(seconds: 1));
      final appointments = await widget.api.listAppointments(dayStart, dayEnd);

      final patients = await widget.api.searchPatients();
      final doctors = await widget.api.listDoctors();

      setState(() {
        _appointments = appointments..sort((a, b) => a.startsAt.compareTo(b.startsAt));
        _patientNames = {for (var p in patients) p.id: p.fullName};
        _doctorNames = {for (var d in doctors) d.id: d.fullName};
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _error = 'No se pudo cargar la agenda: $e';
        _loading = false;
      });
    }
  }

  void _changeDay(int deltaDays) {
    setState(() => _selectedDay = _selectedDay.add(Duration(days: deltaDays)));
    _load();
  }

  @override
  Widget build(BuildContext context) {
    final dayLabel = DateFormat("EEEE d 'de' MMMM", 'es_MX').format(_selectedDay);

    return Column(
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
          color: Colors.grey.shade100,
          child: Row(
            children: [
              IconButton(icon: const Icon(Icons.chevron_left), onPressed: () => _changeDay(-1)),
              Expanded(
                child: Text(
                  dayLabel[0].toUpperCase() + dayLabel.substring(1),
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
              IconButton(icon: const Icon(Icons.chevron_right), onPressed: () => _changeDay(1)),
            ],
          ),
        ),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
                  ? Center(child: Padding(padding: const EdgeInsets.all(16), child: Text(_error!)))
                  : _appointments.isEmpty
                      ? const Center(child: Text('Sin citas este día.'))
                      : RefreshIndicator(
                          onRefresh: _load,
                          child: ListView.builder(
                            itemCount: _appointments.length,
                            itemBuilder: (context, index) {
                              final appt = _appointments[index];
                              final time =
                                  '${DateFormat.Hm().format(appt.startsAt)} - ${DateFormat.Hm().format(appt.endsAt)}';
                              return ListTile(
                                leading: const Icon(Icons.event),
                                title: Text(_patientNames[appt.patientId] ?? 'Paciente'),
                                subtitle: Text(
                                    '$time · ${_doctorNames[appt.dentistId] ?? 'Doctor'}${appt.reason != null ? ' · ${appt.reason}' : ''}'),
                              );
                            },
                          ),
                        ),
        ),
      ],
    );
  }
}
