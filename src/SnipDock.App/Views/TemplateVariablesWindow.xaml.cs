using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SnipDock.App.Services;
using SnipDock.App.ViewModels;
using SnipDock.Core.Models;
using SnipDock.Core.Utils;

namespace SnipDock.App.Views
{
    public partial class TemplateVariablesWindow : Window
    {
        private readonly string _template;
        private readonly ObservableCollection<TemplateVariableInput> _inputs;

        private TemplateVariablesWindow(string template, IReadOnlyList<TemplateVariable> variables, LocalizedStrings loc)
        {
            InitializeComponent();

            _template = template;
            _inputs = new ObservableCollection<TemplateVariableInput>(variables.Select(variable =>
            {
                var input = new TemplateVariableInput(variable);
                input.PropertyChanged += (_, _) => UpdatePreview();
                return input;
            }));

            Title = loc["TemplateVariablesTitle"];
            TxtTitle.Text = loc["TemplateVariablesTitle"];
            TxtHint.Text = loc["TemplateVariablesHint"];
            TxtPreviewLabel.Text = loc["TemplatePreview"];
            BtnConfirm.Content = loc["CopyGeneratedText"];
            BtnCancel.Content = loc["Cancel"];
            BtnCloseCancel.Content = loc["Cancel"];
            VariablesList.ItemsSource = _inputs;
            UpdatePreview();
        }

        public string ResultText { get; private set; } = string.Empty;

        public static bool TryShow(Window owner, string template, IReadOnlyList<TemplateVariable> variables, LocalizedStrings loc, out string result)
        {
            var dialog = new TemplateVariablesWindow(template, variables, loc)
            {
                Owner = owner
            };

            var confirmed = dialog.ShowDialog() == true;
            result = confirmed ? dialog.ResultText : string.Empty;
            return confirmed;
        }

        private void UpdatePreview()
        {
            var values = _inputs.ToDictionary(input => input.Name, input => (string?)input.Value);
            PreviewBox.Text = TemplateVariableProcessor.Render(_template, values);
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            ResultText = PreviewBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
