import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_client.dart';
import 'patient_detail_screen.dart';

class PatientsScreen extends StatefulWidget {
  final ApiClient api;
  const PatientsScreen({super.key, required this.api});

  @override
  State<PatientsScreen> createState() => _PatientsScreenState();
}

class _PatientsScreenState extends State<PatientsScreen> {
  final _searchController = TextEditingController();
  List<PatientModel> _patients = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load([String? query]) async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final patients = await widget.api.searchPatients(query);
      setState(() {
        _patients = patients;
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _error = 'No se pudo cargar la lista de pacientes: $e';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(8),
          child: TextField(
            controller: _searchController,
            decoration: InputDecoration(
              hintText: 'Buscar paciente...',
              border: const OutlineInputBorder(),
              prefixIcon: const Icon(Icons.search),
              suffixIcon: IconButton(
                icon: const Icon(Icons.clear),
                onPressed: () {
                  _searchController.clear();
                  _load();
                },
              ),
            ),
            onSubmitted: _load,
          ),
        ),
        Expanded(
          child: _loading
              ? const Center(child: CircularProgressIndicator())
              : _error != null
                  ? Center(child: Padding(padding: const EdgeInsets.all(16), child: Text(_error!)))
                  : _patients.isEmpty
                      ? const Center(child: Text('Sin pacientes.'))
                      : RefreshIndicator(
                          onRefresh: () => _load(_searchController.text),
                          child: ListView.builder(
                            itemCount: _patients.length,
                            itemBuilder: (context, index) {
                              final patient = _patients[index];
                              return ListTile(
                                leading: const CircleAvatar(child: Icon(Icons.person)),
                                title: Text(patient.fullName),
                                subtitle: Text(patient.phone ?? patient.email ?? ''),
                                trailing: const Icon(Icons.chevron_right),
                                onTap: () {
                                  Navigator.of(context).push(MaterialPageRoute(
                                    builder: (_) => PatientDetailScreen(api: widget.api, patient: patient),
                                  ));
                                },
                              );
                            },
                          ),
                        ),
        ),
      ],
    );
  }
}
