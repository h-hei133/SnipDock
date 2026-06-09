using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnipDock.App.ViewModels;

namespace SnipDock.App.Views
{
    public partial class PromptPanelWindow : Window
    {
        public PromptPanelWindow(PromptPanelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Hide instead of close, keeping the application active and the floating ball in control
            Hide();
        }

        public void FocusSearchBox(bool selectAll)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchTextBox.Focus();
                if (selectAll)
                {
                    SearchTextBox.SelectAll();
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var vm = DataContext as PromptPanelViewModel;
            if (vm == null) return;

            // 1. IsEditing Branching
            if (vm.IsEditing)
            {
                // Esc: Cancel edit in editing mode
                if (e.Key == Key.Escape)
                {
                    Serilog.Log.Information("PreviewKeyDown: Esc triggered cancel edit");
                    if (vm.CancelEditCommand.CanExecute(null))
                    {
                        vm.CancelEditCommand.Execute(null);
                        e.Handled = true;
                    }
                    return;
                }

                // Ctrl + S: Save edit in editing mode
                if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    Serilog.Log.Information("PreviewKeyDown: Ctrl+S triggered save edit");
                    if (vm.SavePromptCommand.CanExecute(null))
                    {
                        vm.SavePromptCommand.Execute(null);
                        e.Handled = true;
                    }
                    return;
                }

                // Block/Ignore list keys during editing and log it
                if (e.Key == Key.Delete || (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) ||
                    e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Enter)
                {
                    Serilog.Log.Information("PreviewKeyDown ignored because editing mode (Key={Key})", e.Key);
                }
                return;
            }

            // 2. Normal non-editing mode shortcuts
            // Esc: Hide management panel
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
                return;
            }

            // Ctrl + F: Focus search box
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                FocusSearchBox(vm.SelectSearchTextOnOpen);
                e.Handled = true;
                return;
            }

            // Ctrl + N: New item
            if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.AddCommand.CanExecute(null))
                {
                    vm.AddCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            // Ctrl + Shift + N: New from clipboard
            if (e.Key == Key.N && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (vm.AddFromClipboardCommand.CanExecute(null))
                {
                    vm.AddFromClipboardCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            // Ctrl + E: Edit current selected item
            if (e.Key == Key.E && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.SelectedPrompt != null && vm.EditCommand.CanExecute(vm.SelectedPrompt))
                {
                    vm.EditCommand.Execute(vm.SelectedPrompt);
                    e.Handled = true;
                }
                return;
            }

            // Ctrl + D: Favorite / Unfavorite current selected item
            if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (vm.SelectedPrompt != null && vm.ToggleFavoriteCommand.CanExecute(null))
                {
                    vm.ToggleFavoriteCommand.Execute(null);
                    e.Handled = true;
                }
                return;
            }

            // Delete: Delete current selected item with confirm (only when not editing in a text box)
            if (e.Key == Key.Delete)
            {
                // Check if focus is inside editing textbox or search box
                var focusedElement = Keyboard.FocusedElement as UIElement;
                bool isFocusedInEditing = focusedElement is System.Windows.Controls.TextBox tb && tb != SearchTextBox;

                if (!isFocusedInEditing && vm.SelectedPrompt != null && vm.DeleteCommand.CanExecute(vm.SelectedPrompt))
                {
                    vm.DeleteCommand.Execute(vm.SelectedPrompt);
                    e.Handled = true;
                }
                return;
            }

            // Enter: Copy selected item (if focus is not in multiline AcceptsReturn TextBox)
            if (e.Key == Key.Enter)
            {
                if (Keyboard.FocusedElement is System.Windows.Controls.TextBox tb && tb.AcceptsReturn)
                {
                    // Allow textbox to handle enter normally
                    return;
                }

                if (vm.SelectedPrompt != null && vm.CopyContentCommand.CanExecute(vm.SelectedPrompt.Content))
                {
                    vm.CopyContentCommand.Execute(vm.SelectedPrompt.Content);
                    e.Handled = true;
                }
                return;
            }

            // Up / Down arrow keys navigation (when focus is in search box or listbox itself)
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                if (Keyboard.FocusedElement == SearchTextBox || Keyboard.FocusedElement == PromptsListBox)
                {
                    NavigateList(e.Key == Key.Down);
                    e.Handled = true;
                }
            }
        }

        private void NavigateList(bool down)
        {
            int count = PromptsListBox.Items.Count;
            if (count == 0) return;

            int currentIndex = PromptsListBox.SelectedIndex;
            int nextIndex;

            if (down)
            {
                nextIndex = currentIndex + 1;
                if (nextIndex >= count) nextIndex = 0; // Wrap around
            }
            else
            {
                nextIndex = currentIndex - 1;
                if (nextIndex < 0) nextIndex = count - 1; // Wrap around
            }

            PromptsListBox.SelectedIndex = nextIndex;

            // Ensure the new item is scrolled into view
            var selectedItem = PromptsListBox.SelectedItem;
            if (selectedItem != null)
            {
                PromptsListBox.ScrollIntoView(selectedItem);
            }
        }
    }
}
