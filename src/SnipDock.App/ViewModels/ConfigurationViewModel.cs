using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using Serilog;

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
        private string _storagePath = string.Empty;
        private string _errorMessage = string.Empty;

        public ConfigurationMode Mode { get; }

        public ConfigurationViewModel(IBootstrapSettingsStore bootstrapStore, ConfigurationMode mode)
        {
            _bootstrapStore = bootstrapStore ?? throw new ArgumentNullException(nameof(bootstrapStore));
            Mode = mode;

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
        public string WindowTitle => Mode == ConfigurationMode.FirstRun ? "首次配置 SnipDock" : "更改数据存储位置";

        public string GuideText => Mode == ConfigurationMode.FirstRun
            ? "请选择一个文件夹作为 SnipDock 的数据存储目录，所有条目和备份将保存在此文件夹中。"
            : "请选择新的数据存储目录，确认后 SnipDock 将迁移现有数据到新目录并重启。";

        public string SaveButtonText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StoragePath))
                {
                    return Mode == ConfigurationMode.FirstRun ? "请先选择文件夹" : "请先选择目录";
                }
                return Mode == ConfigurationMode.FirstRun ? "确认并开始使用 SnipDock" : "保存并切换目录";
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
                Title = Mode == ConfigurationMode.FirstRun ? "选择 SnipDock 数据存储文件夹" : "重新选择数据存储文件夹",
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
                ErrorMessage = $"该目录不可用或无写权限：{ex.Message}";
            }
        }

        private void OnCancel()
        {
            Log.Information("Storage settings canceled by user. Mode={Mode}", Mode);
            ConfigurationCancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}
