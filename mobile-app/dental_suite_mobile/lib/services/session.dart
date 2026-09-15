import '../models/models.dart';

/// Igual que en la app de escritorio: solo vive mientras la app está
/// abierta — hay que volver a iniciar sesión cada vez.
class CurrentSession {
  static UserModel? user;

  static void signOut() {
    user = null;
  }
}
