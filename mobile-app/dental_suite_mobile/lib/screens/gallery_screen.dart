import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../models/models.dart';
import '../services/api_client.dart';

class GalleryScreen extends StatefulWidget {
  final ApiClient api;
  final PatientModel patient;
  const GalleryScreen({super.key, required this.api, required this.patient});

  @override
  State<GalleryScreen> createState() => _GalleryScreenState();
}

class _GalleryScreenState extends State<GalleryScreen> {
  List<_ImageEntry> _images = [];
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
      final studies = await widget.api.listPatientStudies(widget.patient.id);
      final images = <_ImageEntry>[];
      for (final study in studies) {
        for (final slot in study.slots) {
          if (slot.isCaptured && slot.imageUrl != null && slot.imageUrl!.isNotEmpty) {
            images.add(_ImageEntry(slot));
          }
        }
      }
      // Agrupado por diente, más reciente primero — para comparar
      // fácilmente la evolución de un mismo diente en el tiempo.
      images.sort((a, b) {
        final byTooth = a.slot.toothNumber.compareTo(b.slot.toothNumber);
        if (byTooth != 0) return byTooth;
        final aDate = a.slot.capturedAt ?? DateTime(1970);
        final bDate = b.slot.capturedAt ?? DateTime(1970);
        return bDate.compareTo(aDate);
      });

      setState(() {
        _images = images;
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _error = 'No se pudieron cargar las imágenes: $e';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Radiografías y fotos — ${widget.patient.fullName}'),
        backgroundColor: const Color(0xFF8A1538),
        foregroundColor: Colors.white,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Padding(padding: const EdgeInsets.all(16), child: Text(_error!)))
              : _images.isEmpty
                  ? const Center(child: Text('Este paciente todavía no tiene ninguna imagen capturada.'))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: GridView.builder(
                        padding: const EdgeInsets.all(8),
                        gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: 2,
                          childAspectRatio: 0.78,
                          crossAxisSpacing: 8,
                          mainAxisSpacing: 8,
                        ),
                        itemCount: _images.length,
                        itemBuilder: (context, index) {
                          final entry = _images[index];
                          return GestureDetector(
                            onTap: () => _openFullscreen(context, entry),
                            child: Column(
                              children: [
                                Expanded(
                                  child: ClipRRect(
                                    borderRadius: BorderRadius.circular(6),
                                    child: Container(
                                      color: Colors.black,
                                      child: Image.network(
                                        entry.slot.imageUrl!,
                                        fit: BoxFit.cover,
                                        width: double.infinity,
                                        errorBuilder: (_, __, ___) =>
                                            const Center(child: Icon(Icons.broken_image, color: Colors.white38)),
                                        loadingBuilder: (context, child, progress) =>
                                            progress == null ? child : const Center(child: CircularProgressIndicator()),
                                      ),
                                    ),
                                  ),
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  'Diente ${entry.slot.toothNumber} — ${entry.slot.typeLabel}',
                                  style: const TextStyle(fontSize: 11, fontWeight: FontWeight.bold),
                                  textAlign: TextAlign.center,
                                ),
                                if (entry.slot.capturedAt != null)
                                  Text(
                                    DateFormat('dd/MM/yyyy').format(entry.slot.capturedAt!),
                                    style: const TextStyle(fontSize: 10, color: Colors.grey),
                                  ),
                              ],
                            ),
                          );
                        },
                      ),
                    ),
    );
  }

  void _openFullscreen(BuildContext context, _ImageEntry entry) {
    Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => Scaffold(
        backgroundColor: Colors.black,
        appBar: AppBar(
          backgroundColor: Colors.black,
          foregroundColor: Colors.white,
          title: Text('Diente ${entry.slot.toothNumber} — ${entry.slot.typeLabel}'),
        ),
        body: Center(
          child: InteractiveViewer(
            child: Image.network(entry.slot.imageUrl!),
          ),
        ),
      ),
    ));
  }
}

class _ImageEntry {
  final ToothImageModel slot;
  _ImageEntry(this.slot);
}
