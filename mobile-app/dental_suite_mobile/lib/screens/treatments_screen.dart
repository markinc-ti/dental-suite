import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_client.dart';

class TreatmentsScreen extends StatefulWidget {
  final ApiClient api;
  const TreatmentsScreen({super.key, required this.api});

  @override
  State<TreatmentsScreen> createState() => _TreatmentsScreenState();
}

class _TreatmentsScreenState extends State<TreatmentsScreen> {
  List<TreatmentModel> _treatments = [];
  Map<String, String> _patientNames = {};
  Map<String, String> _doctorNames = {};
  bool _loading = true;
  String? _error;
  int _filterIndex = 0; // 0=todos, 1=en progreso, 2=completados

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
      final treatments = await widget.api.listTreatments();
      final patients = await widget.api.searchPatients();
      final doctors = await widget.api.listDoctors();

      setState(() {
        _treatments = treatments;
        _patientNames = {for (var p in patients) p.id: p.fullName};
        _doctorNames = {for (var d in doctors) d.id: d.fullName};
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _error = 'No se pudieron cargar los tratamientos: $e';
        _loading = false;
      });
    }
  }

  String _statusLabel(String status) {
    switch (status) {
      case 'in_progress':
        return 'En progreso';
      case 'completed':
        return 'Completado';
      case 'cancelled':
        return 'Cancelado';
      default:
        return status;
    }
  }

  List<TreatmentModel> get _filtered {
    switch (_filterIndex) {
      case 1:
        return _treatments.where((t) => t.status == 'in_progress').toList();
      case 2:
        return _treatments.where((t) => t.status == 'completed').toList();
      default:
        return _treatments;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
          child: SegmentedButton<int>(
            segments: const [
              ButtonSegment(value: 0, label: Text('Todos')),
              ButtonSegment(value: 1, label: Text('En progreso')),
              ButtonSegment(value: 2, label: Text('Completados')),
            ],
            selected: {_filterIndex},
            onSelectionChanged: (s) => setState(() => _filterIndex = s.first),
          ),
        ),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
                  ? Center(child: Padding(padding: const EdgeInsets.all(16), child: Text(_error!)))
                  : _filtered.isEmpty
                      ? const Center(child: Text('Sin tratamientos.'))
                      : RefreshIndicator(
                          onRefresh: _load,
                          child: ListView.builder(
                            padding: const EdgeInsets.all(8),
                            itemCount: _filtered.length,
                            itemBuilder: (context, index) {
                              final t = _filtered[index];
                              return Card(
                                child: Padding(
                                  padding: const EdgeInsets.all(12),
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(_patientNames[t.patientId] ?? 'Paciente',
                                          style: const TextStyle(fontWeight: FontWeight.bold)),
                                      Text(t.name),
                                      Text('Doctor: ${_doctorNames[t.dentistId] ?? '—'}',
                                          style: const TextStyle(fontSize: 12, color: Colors.grey)),
                                      if (t.estimatedCost != null)
                                        Text('Costo estimado: \$${t.estimatedCost!.toStringAsFixed(2)}'),
                                      const SizedBox(height: 8),
                                      LinearProgressIndicator(value: t.progress),
                                      const SizedBox(height: 4),
                                      Row(
                                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                        children: [
                                          Text('${t.completedSessions} / ${t.totalSessions} sesiones'),
                                          Text(_statusLabel(t.status)),
                                        ],
                                      ),
                                    ],
                                  ),
                                ),
                              );
                            },
                          ),
                        ),
        ),
      ],
    );
  }
}
