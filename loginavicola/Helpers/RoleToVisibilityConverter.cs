using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace loginavicola.Helpers
{
    public class RoleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Lee el rol directamente de la sesión, ignora el value del binding
            string rol = loginavicola.UserSession.UsuarioActual?.Rol ?? string.Empty;

            System.Diagnostics.Debug.WriteLine($"=== CONVERTER - Rol leído: '{rol}' ===");

            return rol == "Administrador" ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}