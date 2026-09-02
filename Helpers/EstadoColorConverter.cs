using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace loginavicola.Helpers
{
    public class EstadoColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string estado = value as string;
            string param = parameter as string;

            if (string.IsNullOrEmpty(estado))
                return param == "Background" ? "#F3F4F6" : "#6B7280";

            switch (estado.Trim().ToLower())
            {
                case "activo":
                case "en producción":
                case "en produccion":
                case "completado":
                case "óptimo":
                case "optimo":
                case "bueno":
                    return param == "Background" ? "#D1FAE5" : "#008A38";

                case "en espera":
                case "cuarentena":
                case "pendiente":
                case "advertencia":
                    return param == "Background" ? "#FEF3C7" : "#F59E0B";

                case "inactivo":
                case "descartado":
                case "finalizado":
                case "cancelado":
                case "crítico":
                case "critico":
                case "agotado":
                    return param == "Background" ? "#FEE2E2" : "#EF4444";

                default:
                    return param == "Background" ? "#E5E7EB" : "#6B7280";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
