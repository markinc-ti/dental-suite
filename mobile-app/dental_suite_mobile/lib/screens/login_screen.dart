import 'package:flutter/material.dart';
import '../services/api_client.dart';
import '../services/session.dart';
import 'home_screen.dart';

class LoginScreen extends StatefulWidget {
  final ApiClient api;
  const LoginScreen({super.key, required this.api});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();
  String? _error;
  bool _loading = false;

  Future<void> _login() async {
    final username = _usernameController.text.trim();
    final password = _passwordController.text;

    if (username.isEmpty || password.isEmpty) {
      setState(() => _error = 'Ingresa tu correo o teléfono, y la contraseña.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final user = await widget.api.login(username, password);
      if (user == null) {
        setState(() {
          _error = 'Usuario o contraseña incorrectos.';
          _loading = false;
        });
        return;
      }
      CurrentSession.user = user;
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => HomeScreen(api: widget.api)),
      );
    } catch (e) {
      setState(() {
        _error = 'No se pudo conectar: $e';
        _loading = false;
      });
    }
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
              const SizedBox(height: 4),
              const Text('Inicia sesión para continuar.'),
              const SizedBox(height: 24),
              const Text('Correo o teléfono'),
              const SizedBox(height: 6),
              TextField(
                controller: _usernameController,
                decoration: const InputDecoration(border: OutlineInputBorder()),
              ),
              const SizedBox(height: 14),
              const Text('Contraseña'),
              const SizedBox(height: 6),
              TextField(
                controller: _passwordController,
                obscureText: true,
                decoration: const InputDecoration(border: OutlineInputBorder()),
                onSubmitted: (_) => _login(),
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(_error!, style: const TextStyle(color: Colors.red)),
              ],
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _loading ? null : _login,
                  child: _loading
                      ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                      : const Text('Iniciar sesión'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
