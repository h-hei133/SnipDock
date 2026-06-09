using System.Windows;
using System.Windows.Input;

namespace SnipDock.App.Views
{
    public partial class ConfirmDialogWindow : Window
    {
        private ConfirmDialogWindow(string title, string message)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;
        }

        public static bool Show(Window owner, string title, string message)
        {
            var dialog = new ConfirmDialogWindow(title, message)
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
