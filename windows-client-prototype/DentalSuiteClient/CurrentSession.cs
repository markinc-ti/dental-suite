using DentalSuiteClient.Api;

namespace DentalSuiteClient
{
    /// <summary>
    /// Sesión actual, solo en memoria — se pierde al cerrar la app
    /// (hay que volver a iniciar sesión cada vez que se abre, que es
    /// justo el comportamiento esperado para un consultorio donde
    /// distintos doctores comparten la misma PC).
    /// </summary>
    public static class CurrentSession
    {
        public static UserDto? User { get; private set; }

        public static bool IsAdmin => User?.Role == "admin";

        public static void SignIn(UserDto user) => User = user;

        public static void SignOut() => User = null;
    }
}
