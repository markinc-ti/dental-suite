import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_client.dart';
import 'gallery_screen.dart';

class PatientDetailScreen extends StatefulWidget {
  final ApiClient api;
  final PatientModel patient;
  const PatientDetailScreen({super.key, required this.api, required this.patient});

  @override
  State<PatientDetailScreen> createState() => _PatientDetailScreenState();
}

class _PatientDetailScreenState extends State<PatientDetailScreen> {
  List<TreatmentModel> _treatments = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final treatments = await widget.api.listTreatments(patientId: widget.patient.id);
      setState(() {
        _treatments = treatments;
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

  @override
  Widget build(BuildContext context) {
    final p = widget.patient;
    return Scaffold(
      appBar: AppBar(
        title: Text(p.fullName),
        backgroundColor: const Color(0xFF8A1538),
        foregroundColor: Colors.white,
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.all(12),
          children: [
            Card(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    if (p.phone != null) Text('Teléfono: ${p.phone}'),
                    if (p.email != null) Text('Correo: ${p.email}'),
                    if (p.address != null) Text('Dirección: ${p.address}'),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                icon: const Icon(Icons.photo_library),
                label: const Text('Ver fotos / radiografías'),
                onPressed: () {
                  Navigator.of(context).push(MaterialPageRoute(
                    builder: (_) => GalleryScreen(api: widget.api, patient: p),
                  ));
                },
              ),
            ),
            const SizedBox(height: 16),
            const Text('Tratamientos', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
            const SizedBox(height: 8),
            if (_loading)
              const Center(child: CircularProgressIndicator())
            else if (_error != null)
              Text(_error!)
            else if (_treatments.isEmpty)
              const Text('Sin tratamientos registrados.')
            else
              ..._treatments.map((t) => Card(
                    child: Padding(
                      padding: const EdgeInsets.all(12),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Expanded(child: Text(t.name, style: const TextStyle(fontWeight: FontWeight.bold))),
                              Text(_statusLabel(t.status)),
                            ],
                          ),
                          if (t.estimatedCost != null)
                            Text('Costo estimado: \$${t.estimatedCost!.toStringAsFixed(2)}'),
                          const SizedBox(height: 8),
                          LinearProgressIndicator(value: t.progress),
                          const SizedBox(height: 4),
                          Text('${t.completedSessions} / ${t.totalSessions} sesiones'),
                        ],
                      ),
                    ),
                  )),
          ],
        ),
      ),
    );
  }
}
