using SnipDock.Core.Models;

namespace SnipDock.App.ViewModels
{
    public sealed class TemplateVariableInput : ViewModelBase
    {
        private string _value = string.Empty;

        public TemplateVariableInput(TemplateVariable variable)
        {
            Name = variable.Name;
        }

        public string Name { get; }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}
