import 'package:flutter/material.dart';
import '../services/api_client.dart';
import '../services/session.dart';
import 'agenda_screen.dart';
import 'patients_screen.dart';
import 'treatments_screen.dart';
import 'login_screen.dart';

class HomeScreen extends StatefulWidget {
  final ApiClient api;
  const HomeScreen({super.key, required this.api});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final screens = [
      AgendaScreen(api: widget.api),
      PatientsScreen(api: widget.api),
      TreatmentsScreen(api: widget.api),
    ];

    return Scaffold(
      appBar: AppBar(
        title: const Text('Dental Suite'),
        backgroundColor: const Color(0xFF8A1538),
        foregroundColor: Colors.white,
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 8),
            child: Center(
              child: Text(
                CurrentSession.user?.fullName ?? '',
                style: const TextStyle(fontSize: 12),
              ),
            ),
          ),
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Cerrar sesión',
            onPressed: () {
              CurrentSession.signOut();
              Navigator.of(context).pushAndRemoveUntil(
                MaterialPageRoute(builder: (_) => LoginScreen(api: widget.api)),
                (route) => false,
              );
            },
          ),
        ],
      ),
      body: screens[_index],
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.calendar_month), label: 'Agenda'),
          NavigationDestination(icon: Icon(Icons.people), label: 'Pacientes'),
          NavigationDestination(icon: Icon(Icons.medical_services), label: 'Tratamientos'),
        ],
      ),
    );
  }
}
