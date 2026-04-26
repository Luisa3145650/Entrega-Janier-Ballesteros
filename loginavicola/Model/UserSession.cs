using loginavicola.Model;

namespace loginavicola
{
    public static class UserSession
    {
        public static Usuario UsuarioActual { get; set; }
        public static bool EsVisitante { get; set; } = false;

        // Propiedades útiles para verificar permisos
        public static bool EsAdministrador => !EsVisitante && UsuarioActual?.Rol == "Administrador";
        public static bool EsEmpleado => !EsVisitante && UsuarioActual?.Rol == "Empleado";
        public static bool PuedeVerInventario => !EsVisitante;
        public static bool PuedeVerAlimentacion => !EsVisitante;
        public static bool PuedeVerDiagnostico => !EsVisitante;
        public static bool PuedeVerGestionUsuarios => !EsVisitante && UsuarioActual?.Rol == "Administrador";
        public static bool PuedeVerExportar => !EsVisitante;
    }
}