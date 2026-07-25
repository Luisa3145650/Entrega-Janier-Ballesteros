using System;
using System.Globalization;
using System.Windows.Data;

namespace loginavicola.Helpers
{
    public class RolIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string rol = value as string;

            if (string.IsNullOrEmpty(rol))
                return "👤";

            switch (rol.ToLower())
            {
                case "administrador":
                    return "👑";
                case "usuario":
                    return "🧑‍💻";
                case "visitante":
                    return "👁️";
                default:
                    return "👤";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}