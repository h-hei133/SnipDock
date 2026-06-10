using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using Serilog;
using SnipDock.App.Services;

namespace SnipDock.App.ViewModels
{
    public enum ConfigurationMode
    {
        FirstRun,
        ChangeStorageLocation
    }

    public class ConfigurationViewModel : ViewModelBase
    {
        private readonly IBootstrapSettingsStore _bootstrapStore;
        private readonly LocalizedStrings _loc;
        private string _storagePath = string.Empty;
        private string _errorMessage = string.Empty;

        public ConfigurationMode Mode { get; }

        public ConfigurationViewModel(IBootstrapSettingsStore bootstrapStore, ConfigurationMode mode)
            : this(bootstrapStore, mode, LocalizationService.DetectDefaultLanguage())
        {
        }

        public ConfigurationViewModel(IBootstrapSettingsStore bootstrapStore, ConfigurationMode mode, string? language)
        {
            _bootstrapStore = bootstrapStore ?? throw new ArgumentNullException(nameof(bootstrapStore));
            Mode = mode;
            _loc = new LocalizationService().CreateStrings(LocalizationService.NormalizeLanguage(language));

            BrowseCommand = new RelayCommand(OnBrowse);
            SaveCommand = new RelayCommand(OnSave, CanSave);
            CancelCommand = new RelayCommand(OnCancel);

            Log.Information("Open storage settings window with mode {Mode}", Mode);
        }

        public string StoragePath
        {
            get => _storagePath;
            set
            {
                if (SetProperty(ref _storagePath, value))
                {
                    ErrorMessage = string.Empty;
                    // Force refresh of save button text and command can-execute
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        // Mode-specific UI bindings
        public string WindowTitle => Mode == ConfigurationMode.FirstRun ? FirstRunTitle : ChangeStorageTitle;

        public string GuideText => Mode == ConfigurationMode.FirstRun
            ? FirstRunGuide
            : ChangeStorageGuide;

        public string SaveButtonText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StoragePath))
                {
                    return ChooseFolderFirst;
                }
                return Mode == ConfigurationMode.FirstRun ? ConfirmAndStart : SaveAndSwitch;
            }
        }

        public bool IsCancelButtonVisible => Mode == ConfigurationMode.ChangeStorageLocation;

        public ICommand BrowseCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event EventHandler? ConfigurationSaved;
        public event EventHandler? ConfigurationCancelled;

        private void OnBrowse()
        {
            var dialog = new OpenFolderDialog
            {
                Title = Mode == ConfigurationMode.FirstRun ? ChooseStorageFolder : ReselectStorageFolder,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                StoragePath = dialog.FolderName;
            }
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(StoragePath);
        }

        private void OnSave()
        {
            var path = StoragePath.Trim();
            Log.Information("Attempting to save storage path. Mode={Mode}, Path={Path}", Mode, path);

            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                // Verify write access
                var tempFile = Path.Combine(path, Path.GetRandomFileName());
                File.WriteAllText(tempFile, "temp");
                File.Delete(tempFile);

                var settings = new BootstrapSettings { StoragePath = path };
                _bootstrapStore.Save(settings);

                Log.Information("Storage path changed successfully. Mode={Mode}, Path={Path}", Mode, path);
                ConfigurationSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Storage settings validation failed. Path={Path}", path);
                ErrorMessage = $"{DirectoryUnavailable}{ex.Message}";
            }
        }

        private void OnCancel()
        {
            Log.Information("Storage settings canceled by user. Mode={Mode}", Mode);
            ConfigurationCancelled?.Invoke(this, EventArgs.Empty);
        }

        public string BrowseText => _loc["Browse"];
        public string CloseText => _loc["Close"];
        public string CancelText => _loc["Cancel"];

        private string FirstRunTitle => _loc["FirstRunTitle"];
        private string ChangeStorageTitle => _loc["ChangeStorageTitle"];
        private string FirstRunGuide => _loc["FirstRunGuide"];
        private string ChangeStorageGuide => _loc["ChangeStorageGuide"];
        private string ChooseFolderFirst => _loc["ChooseFolderFirst"];
        private string ConfirmAndStart => _loc["ConfirmAndStart"];
        private string SaveAndSwitch => _loc["SaveAndSwitch"];
        private string ChooseStorageFolder => _loc["ChooseStorageFolder"];
        private string ReselectStorageFolder => _loc["ReselectStorageFolder"];
        private string DirectoryUnavailable => _loc["DirectoryUnavailable"];
    }
}
