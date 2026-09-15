import 'package:flutter/material.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'services/api_client.dart';
import 'screens/server_setup_screen.dart';
import 'screens/login_screen.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeDateFormatting('es_MX');
  runApp(const DentalSuiteApp());
}

class DentalSuiteApp extends StatelessWidget {
  const DentalSuiteApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Dental Suite',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF8A1538)),
        useMaterial3: true,
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFF8A1538),
          foregroundColor: Colors.white,
        ),
      ),
      home: const StartupGate(),
    );
  }
}

/// Decide con qué pantalla arrancar: si esta instalación nunca se ha
/// conectado a ningún servidor, pide la dirección primero; si ya está
/// guardada, va directo al login (hay que iniciar sesión cada vez que
/// se abre la app, igual que en la de escritorio).
class StartupGate extends StatefulWidget {
  const StartupGate({super.key});

  @override
  State<StartupGate> createState() => _StartupGateState();
}

class _StartupGateState extends State<StartupGate> {
  String? _savedUrl;
  bool _checked = false;

  @override
  void initState() {
    super.initState();
    _check();
  }

  Future<void> _check() async {
    final url = await ServerConfig.getSavedBaseUrl();
    setState(() {
      _savedUrl = url;
      _checked = true;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (!_checked) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_savedUrl == null) {
      return const ServerSetupScreen();
    }
    return LoginScreen(api: ApiClient(_savedUrl!));
  }
}
