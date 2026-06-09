using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnipDock.App.Models
{
    public class TypeFilterItem : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _displayName = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        private void SetProperty(ref string storage, string value, [CallerMemberName] string? propertyName = null)
        {
            if (storage == value) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
