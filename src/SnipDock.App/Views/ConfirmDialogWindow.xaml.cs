using System.Windows;
using System.Windows.Input;
using SnipDock.App.Services;

namespace SnipDock.App.Views
{
    public partial class ConfirmDialogWindow : Window
    {
        private ConfirmDialogWindow(string title, string message, string confirmText, string cancelText)
        {
            InitializeComponent();
            Title = title;
            TxtTitle.Text = title;
            TxtMessage.Text = message;
            BtnConfirm.Content = confirmText;
            BtnCancel.Content = cancelText;
            BtnCloseCancel.Content = cancelText;
        }

        public static bool Show(Window owner, string title, string message)
        {
            var loc = new LocalizationService().CreateStrings(LocalizationService.DetectDefaultLanguage());
            return Show(owner, title, message, loc["Confirm"], loc["Cancel"]);
        }

        public static bool Show(Window owner, string title, string message, string confirmText, string cancelText)
        {
            var dialog = new ConfirmDialogWindow(title, message, confirmText, cancelText)
            {
                Owner = owner
            };
            return dialog.ShowDialog() == true;
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
