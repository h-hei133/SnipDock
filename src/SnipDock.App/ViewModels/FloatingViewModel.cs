using System;
using System.Windows;
using System.Windows.Input;
using SnipDock.App.Services;
using Application = System.Windows.Application;

namespace SnipDock.App.ViewModels
{
    public class FloatingViewModel : ViewModelBase
    {
        private readonly LocalizationService _localizationService = new();
        private string _selectedLanguage = LocalizationService.DetectDefaultLanguage();
        private LocalizedStrings _loc;

        public FloatingViewModel()
        {
            _loc = _localizationService.CreateStrings(_selectedLanguage);
            TogglePanelCommand = new RelayCommand(OnTogglePanel);
            ResetPositionCommand = new RelayCommand(OnResetPosition);
            ResetStoragePathCommand = new RelayCommand(OnResetStoragePath);
            ExitCommand = new RelayCommand(OnExit);
        }

        public ICommand TogglePanelCommand { get; }
        public ICommand ResetPositionCommand { get; }
        public ICommand ResetStoragePathCommand { get; }
        public ICommand ExitCommand { get; }

        public LocalizedStrings Loc
        {
            get => _loc;
            private set => SetProperty(ref _loc, value);
        }

        public event EventHandler? TogglePanelRequested;
        public event EventHandler? ResetPositionRequested;
        public event EventHandler? ResetStoragePathRequested;

        public void SetLanguage(string? language)
        {
            var normalized = LocalizationService.NormalizeLanguage(language);
            if (string.Equals(_selectedLanguage, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedLanguage = normalized;
            Loc = _localizationService.CreateStrings(_selectedLanguage);
        }

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
