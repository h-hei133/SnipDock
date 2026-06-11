using System;
using System.IO;
using System.Text.Json;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;

namespace SnipDock.Infrastructure.Storage
{
    public class LocalBootstrapSettingsStore : IBootstrapSettingsStore
    {
        private readonly IAppPathProvider _pathProvider;
        private readonly string _newFolderPath;
        private readonly string _newFilePath;
        private readonly string _legacyFolderPath;
        private readonly string _legacyFilePath;

        public LocalBootstrapSettingsStore(IAppPathProvider? pathProvider = null)
        {
            _pathProvider = pathProvider ?? new SnipDock.Infrastructure.Services.DefaultAppPathProvider();
            _newFolderPath = _pathProvider.GetNewBootstrapFolderPath();
            _newFilePath = Path.Combine(_newFolderPath, "bootstrap.json");
            _legacyFolderPath = _pathProvider.GetLegacyBootstrapFolderPath();
            _legacyFilePath = Path.Combine(_legacyFolderPath, "bootstrap.json");
        }

        public BootstrapSettings Load()
        {
            try
            {
                // 1. Try loading from new SnipDock path
                if (File.Exists(_newFilePath))
                {
                    var json = File.ReadAllText(_newFilePath);
                    var settings = JsonSerializer.Deserialize<BootstrapSettings>(json);
                    return settings ?? new BootstrapSettings();
                }

                // 2. If new path does not exist, check legacy app path
                if (File.Exists(_legacyFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(_legacyFilePath);
                        var settings = JsonSerializer.Deserialize<BootstrapSettings>(json);

                        if (settings != null)
                        {
                            // Migrate to new folder
                            if (!Directory.Exists(_newFolderPath))
                            {
                                Directory.CreateDirectory(_newFolderPath);
                            }
                            File.WriteAllText(_newFilePath, json);
                            
                            // Log the migration
                            Serilog.Log.Information("Legacy config detected. Migrated bootstrap config to SnipDock.");
                            return settings;
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Failed to migrate legacy bootstrap config.");
                        
                        // Try fallback to just load legacy directly
                        try
                        {
                            var json = File.ReadAllText(_legacyFilePath);
                            return JsonSerializer.Deserialize<BootstrapSettings>(json) ?? new BootstrapSettings();
                        }
                        catch
                        {
                            // Suppress
                        }
                    }
                }

                return new BootstrapSettings();
            }
            catch
            {
                return new BootstrapSettings();
            }
        }

        public void Save(BootstrapSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            try
            {
                if (!Directory.Exists(_newFolderPath))
                {
                    Directory.CreateDirectory(_newFolderPath);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_newFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to save bootstrap settings.", ex);
            }
        }
    }
}
