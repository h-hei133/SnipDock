using System;
using System.IO;
using Xunit;
using SnipDock.Core.Models;
using SnipDock.Core.Interfaces;
using SnipDock.Infrastructure.Storage;
using SnipDock.App.Services;

namespace SnipDock.Tests
{
    public class SettingsStoreTests : IDisposable
    {
        private readonly string _tempDir;

        public SettingsStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SnipDockSettingsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
                // Suppress clean-up exceptions
            }
        }

        [Fact]
        public void LocalAppSettingsStore_SavesAndLoadsCorrectly()
        {
            var store = new LocalAppSettingsStore(_tempDir);

            // Verify default values on non-existent file
            var defaultSettings = store.Load();
            Assert.Equal(LocalizationService.DetectDefaultLanguage(), defaultSettings.Language);
            Assert.Equal(-1, defaultSettings.WindowLeft);

            // Modify values and save
            var newSettings = new AppSettings
            {
                Language = "en-US",
                WindowLeft = 250,
                WindowTop = 380,
                Theme = "Light"
            };

            store.Save(newSettings);

            // Load and assert
            var reloaded = store.Load();
            Assert.Equal("en-US", reloaded.Language);
            Assert.Equal(250, reloaded.WindowLeft);
            Assert.Equal(380, reloaded.WindowTop);
            Assert.Equal("Light", reloaded.Theme);
        }
        
        private class FakeAppPathProvider : IAppPathProvider
        {
            public string NewFolder { get; set; } = string.Empty;
            public string LegacyFolder { get; set; } = string.Empty;
            public string LogFolder { get; set; } = string.Empty;

            public string GetNewBootstrapFolderPath() => NewFolder;
            public string GetLegacyBootstrapFolderPath() => LegacyFolder;
            public string GetBootstrapLogFolderPath() => LogFolder;
        }

        [Fact]
        public void LocalBootstrapSettingsStore_CanLoadAndSave()
        {
            var fakePath = new FakeAppPathProvider
            {
                NewFolder = Path.Combine(_tempDir, "SnipDock"),
                LegacyFolder = Path.Combine(_tempDir, "PromptShelf"),
                LogFolder = Path.Combine(_tempDir, "SnipDockLogs")
            };
            
            var store = new LocalBootstrapSettingsStore(fakePath);
            
            var testSettings = new BootstrapSettings { StoragePath = @"C:\MockPath_ForTesting_Only" };
            store.Save(testSettings);
            
            var loaded = store.Load();
            Assert.Equal(@"C:\MockPath_ForTesting_Only", loaded.StoragePath);
        }

        [Fact]
        public void LocalBootstrapSettingsStore_MigratesFromLegacyConfig()
        {
            var newDir = Path.Combine(_tempDir, "SnipDock");
            var legacyDir = Path.Combine(_tempDir, "PromptShelf");
            
            Directory.CreateDirectory(legacyDir);
            
            var fakePath = new FakeAppPathProvider
            {
                NewFolder = newDir,
                LegacyFolder = legacyDir,
                LogFolder = Path.Combine(_tempDir, "SnipDockLogs")
            };
            
            var legacyFile = Path.Combine(legacyDir, "bootstrap.json");
            var legacyStoragePath = Path.Combine(_tempDir, "LegacyDataFolder");
            var legacyJson = $"{{\n  \"StoragePath\": \"{legacyStoragePath.Replace("\\", "\\\\")}\"\n}}";
            File.WriteAllText(legacyFile, legacyJson);
            
            var store = new LocalBootstrapSettingsStore(fakePath);
            
            // Act
            var settings = store.Load();
            
            // Assert
            Assert.Equal(legacyStoragePath, settings.StoragePath);
            
            // Verify new file exists and legacy file still exists
            var newFile = Path.Combine(newDir, "bootstrap.json");
            Assert.True(File.Exists(newFile));
            Assert.True(File.Exists(legacyFile));
            
            var newJson = File.ReadAllText(newFile);
            Assert.Contains("LegacyDataFolder", newJson);
        }

        [Fact]
        public void LocalBootstrapSettingsStore_InvalidLegacyConfig_ReturnsDefault()
        {
            var newDir = Path.Combine(_tempDir, "SnipDock");
            var legacyDir = Path.Combine(_tempDir, "PromptShelf");
            
            Directory.CreateDirectory(legacyDir);
            
            var fakePath = new FakeAppPathProvider
            {
                NewFolder = newDir,
                LegacyFolder = legacyDir,
                LogFolder = Path.Combine(_tempDir, "SnipDockLogs")
            };
            
            var legacyFile = Path.Combine(legacyDir, "bootstrap.json");
            File.WriteAllText(legacyFile, "{ corrupt json ... }");
            
            var store = new LocalBootstrapSettingsStore(fakePath);
            
            // Act
            var settings = store.Load();
            
            // Assert
            Assert.Equal(string.Empty, settings.StoragePath);
            
            var newFile = Path.Combine(newDir, "bootstrap.json");
            Assert.False(File.Exists(newFile));
        }

        [Fact]
        public void HidePanelAfterCopy_DefaultsToFalse_AndHandlesMissingFieldInOlderConfig()
        {
            var store = new LocalAppSettingsStore(_tempDir);

            // 1. Verify standard C# object default
            var settings = new AppSettings();
            Assert.False(settings.HidePanelAfterCopy);

            // 2. Verify load from non-existent file defaults to false
            var loadedDefaults = store.Load();
            Assert.False(loadedDefaults.HidePanelAfterCopy);

            // 3. Verify load from older settings.json missing the field
            var olderJson = "{\n  \"Theme\": \"Dark\",\n  \"Language\": \"zh-CN\"\n}";
            File.WriteAllText(Path.Combine(_tempDir, "settings.json"), olderJson);

            var reloadedOlder = store.Load();
            Assert.False(reloadedOlder.HidePanelAfterCopy);
        }

        [Fact]
        public void AppSettings_IsStartupEnabledAndSchemaVersion_DefaultCorrectly_AndHandlesOlderConfig()
        {
            var store = new LocalAppSettingsStore(_tempDir);

            // 1. Verify standard defaults
            var settings = new AppSettings();
            Assert.False(settings.IsStartupEnabled);
            Assert.Equal(1, settings.DataSchemaVersion);

            // 2. Verify load from non-existent file
            var loadedDefaults = store.Load();
            Assert.False(loadedDefaults.IsStartupEnabled);
            Assert.Equal(1, loadedDefaults.DataSchemaVersion);

            // 3. Verify load from older settings.json missing these fields
            var olderJson = "{\n  \"Theme\": \"Light\",\n  \"Language\": \"zh-CN\"\n}";
            File.WriteAllText(Path.Combine(_tempDir, "settings.json"), olderJson);

            var reloadedOlder = store.Load();
            Assert.False(reloadedOlder.IsStartupEnabled);
            Assert.Equal(1, reloadedOlder.DataSchemaVersion);
        }
    }
}
