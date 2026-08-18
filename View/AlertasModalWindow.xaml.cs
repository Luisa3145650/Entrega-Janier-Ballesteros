using System.Collections.Generic;
using System.Windows;
using loginavicola.Model;

namespace loginavicola.View
{
    public partial class AlertasModalWindow : Window
    {
        public AlertasModalWindow(List<AlertaSistema> alertas)
        {
            InitializeComponent();
            var listaAlertas = alertas ?? new List<AlertaSistema>();
            icAlertas.ItemsSource = listaAlertas;
            txtContadorAlertas.Text = $"{listaAlertas.Count} Alerta(s) Activa(s)";
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
