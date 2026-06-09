using System;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;

namespace SnipDock.App.ViewModels
{
    public class FloatingViewModel : ViewModelBase
    {
        public FloatingViewModel()
        {
            TogglePanelCommand = new RelayCommand(OnTogglePanel);
            ResetPositionCommand = new RelayCommand(OnResetPosition);
            ResetStoragePathCommand = new RelayCommand(OnResetStoragePath);
            ExitCommand = new RelayCommand(OnExit);
        }

        public ICommand TogglePanelCommand { get; }
        public ICommand ResetPositionCommand { get; }
        public ICommand ResetStoragePathCommand { get; }
        public ICommand ExitCommand { get; }

        public event EventHandler? TogglePanelRequested;
        public event EventHandler? ResetPositionRequested;
        public event EventHandler? ResetStoragePathRequested;

        private void OnTogglePanel()
        {
            TogglePanelRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnResetPosition()
        {
            ResetPositionRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnResetStoragePath()
        {
            ResetStoragePathRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnExit()
        {
            if (Application.Current is App app)
            {
                app.RequestShutdown("Floating ball context menu");
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
    }
}
