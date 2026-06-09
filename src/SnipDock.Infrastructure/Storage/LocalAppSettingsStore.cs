using System;
using System.IO;
using System.Text.Json;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;

namespace SnipDock.Infrastructure.Storage
{
    public class LocalAppSettingsStore : IAppSettingsStore
    {
        private readonly string _storagePath;
        private string FilePath => Path.Combine(_storagePath, "settings.json");

        public LocalAppSettingsStore(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path cannot be null or empty.", nameof(storagePath));
            _storagePath = storagePath;
        }

        public AppSettings Load()
        {
            try
            {
                var filePath = FilePath;
                if (!File.Exists(filePath))
                {
                    return new AppSettings();
                }

                var json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            try
            {
                if (!Directory.Exists(_storagePath))
                {
                    Directory.CreateDirectory(_storagePath);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to save app settings.", ex);
            }
        }
    }
}
