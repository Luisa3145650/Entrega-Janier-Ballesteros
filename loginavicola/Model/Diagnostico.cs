using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace loginavicola.Model
{
    public class Diagnostico : INotifyPropertyChanged
    {

        public string? NombreMedicamento { get; set; }
        private int _idDiagnostico;
        private DateTime _fechaDiagnostico;
        private string _tipo = string.Empty;
        private int _idLote;
        private string _diagnosticoMedico = string.Empty;
        private string _tratamiento = string.Empty;
        private int _gallinasAfectadas;
        private string _veterinario = string.Empty;
        private string _observaciones = string.Empty;
        private string _estado = "Activo";
        public int? IdMedicamento { get; set; }
        public int CantidadMedicamentoUsado { get; set; }



        public int IdDiagnostico
        {
            get => _idDiagnostico;
            set
            {
                _idDiagnostico = value;
                OnPropertyChanged(nameof(IdDiagnostico));
            }
        }

        public DateTime FechaDiagnostico
        {
            get => _fechaDiagnostico;
            set
            {
                _fechaDiagnostico = value;
                OnPropertyChanged(nameof(FechaDiagnostico));
            }
        }

        public string Tipo
        {
            get => _tipo;
            set
            {
                _tipo = value ?? string.Empty;
                OnPropertyChanged(nameof(Tipo));
            }
        }

        public int IdLote
        {
            get => _idLote;
            set
            {
                _idLote = value;
                OnPropertyChanged(nameof(IdLote));
            }
        }

        public string DiagnosticoMedico
        {
            get => _diagnosticoMedico;
            set
            {
                _diagnosticoMedico = value ?? string.Empty;
                OnPropertyChanged(nameof(DiagnosticoMedico));
            }
        }

        public string Tratamiento
        {
            get => _tratamiento;
            set
            {
                _tratamiento = value ?? string.Empty;
                OnPropertyChanged(nameof(Tratamiento));
            }
        }


        public int GallinasAfectadas
        {
            get => _gallinasAfectadas;
            set
            {
                _gallinasAfectadas = value;
                OnPropertyChanged(nameof(GallinasAfectadas));
            }
        }

        public string Veterinario
        {
            get => _veterinario;
            set
            {
                _veterinario = value ?? string.Empty;
                OnPropertyChanged(nameof(Veterinario));
            }
        }

        public string Observaciones
        {
            get => _observaciones;
            set
            {
                _observaciones = value ?? string.Empty;
                OnPropertyChanged(nameof(Observaciones));
            }
        }

        public string Estado
        {
            get => _estado;
            set
            {
                _estado = value ?? "Activo";
                OnPropertyChanged(nameof(Estado));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

