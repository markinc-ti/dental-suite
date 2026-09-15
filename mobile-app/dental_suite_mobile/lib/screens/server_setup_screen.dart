import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import '../services/api_client.dart';
import 'login_screen.dart';

class ServerSetupScreen extends StatefulWidget {
  const ServerSetupScreen({super.key});

  @override
  State<ServerSetupScreen> createState() => _ServerSetupScreenState();
}

class _ServerSetupScreenState extends State<ServerSetupScreen> {
  final _urlController = TextEditingController(text: 'https://');
  String? _error;
  bool _connecting = false;

  Future<void> _connect() async {
    final url = _urlController.text.trim().replaceAll(RegExp(r'/+$'), '');
    final uri = Uri.tryParse(url);
    if (uri == null || !uri.isAbsolute) {
      setState(() => _error = 'Ingresa una dirección válida, empezando con https://');
      return;
    }

    setState(() {
      _connecting = true;
      _error = null;
    });

    try {
      final response = await http.get(Uri.parse('$url/health')).timeout(const Duration(seconds: 10));
      if (response.statusCode != 200) {
        setState(() {
          _error = 'El servidor respondió con un error (${response.statusCode}). Verifica la dirección.';
          _connecting = false;
        });
        return;
      }
    } catch (e) {
      setState(() {
        _error = 'No se pudo conectar: $e';
        _connecting = false;
      });
      return;
    }

    await ServerConfig.saveBaseUrl(url);
    if (!mounted) return;
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => LoginScreen(api: ApiClient(url))),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Text('Dental Suite',
                  style: TextStyle(fontSize: 28, fontWeight: FontWeight.bold, color: Color(0xFF8A1538))),
              const SizedBox(height: 12),
              const Text('Todavía no está conectado a ningún servidor. Pega la dirección de tu backend '
                  '(por ejemplo, la URL de Render donde lo desplegaste).'),
              const SizedBox(height: 24),
              const Text('Dirección del servidor', style: TextStyle(fontWeight: FontWeight.bold)),
              const SizedBox(height: 6),
              TextField(
                controller: _urlController,
                decoration: const InputDecoration(
                  border: OutlineInputBorder(),
                  hintText: 'https://dental-suite-xxxx.onrender.com',
                ),
                keyboardType: TextInputType.url,
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(_error!, style: const TextStyle(color: Colors.red)),
              ],
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _connecting ? null : _connect,
                  child: _connecting
                      ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('Conectar'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
