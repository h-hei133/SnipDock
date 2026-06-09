using System;
using System.Windows;
using System.Windows.Input;
using SnipDock.App.ViewModels;
using Serilog;
using Application = System.Windows.Application;

namespace SnipDock.App.Views
{
    public partial class ConfigurationWindow : Window
    {
        public ConfigurationWindow(ConfigurationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.ConfigurationSaved += ViewModel_ConfigurationSaved;
            viewModel.ConfigurationCancelled += ViewModel_ConfigurationCancelled;
        }

        private void ViewModel_ConfigurationSaved(object? sender, EventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ViewModel_ConfigurationCancelled(object? sender, EventArgs e)
        {
            DialogResult = false;
            Close();
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
            if (DataContext is ConfigurationViewModel vm)
            {
                Log.Information("Storage settings window closed by user clicking close. Mode={Mode}", vm.Mode);
                
                if (vm.Mode == ConfigurationMode.FirstRun)
                {
                    Log.Information("FirstRun configuration closed. Shutting down application.");
                    Application.Current.Shutdown();
                }
                else
                {
                    // For ChangeStorageLocation mode, just cancel and close, preserving the app session
                    DialogResult = false;
                    Close();
                }
            }
            else
            {
                Close();
            }
        }
    }
}
