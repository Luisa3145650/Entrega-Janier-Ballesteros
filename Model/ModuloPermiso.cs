using System.ComponentModel;

namespace loginavicola.Model
{
    public class ModuloPermiso : INotifyPropertyChanged
    {
        public int IdModulo { get; set; }
        public string NombreModulo { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                OnPropertyChanged("IsSelected");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}