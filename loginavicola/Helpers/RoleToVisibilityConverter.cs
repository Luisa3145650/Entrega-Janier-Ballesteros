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
            // Si el rol es "Administrador", devolvemos Visible. Si no, Collapsed (desaparece).
            string userRole = value as string;
            if (userRole == "Administrador")
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}