using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace loginavicola.Helpers
{
    public class RolColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string rol = value as string;
            string param = parameter as string;

            if (string.IsNullOrEmpty(rol))
                return "#A855F7";

            switch (rol.ToLower())
            {
                case "administrador":
                    return param == "Background" ? "#FEF3C7" : "#F59E0B";
                case "usuario":
                    return param == "Background" ? "#DBEAFE" : "#3B82F6";
                case "visitante":
                    return param == "Background" ? "#D1FAE5" : "#10B981";
                default:
                    return param == "Background" ? "#F3E8FF" : "#A855F7";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}