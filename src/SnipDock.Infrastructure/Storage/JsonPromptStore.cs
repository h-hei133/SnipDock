using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;

namespace SnipDock.Infrastructure.Storage
{
    public class JsonPromptStore : IPromptStore
    {
        private readonly string _storagePath;
        private readonly string _filePath;
        private readonly string _tempFilePath;
        private readonly string _backupFilePath;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public bool WasRecoveredFromBackup { get; private set; }

        public JsonPromptStore(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path cannot be null or empty.", nameof(storagePath));

            _storagePath = storagePath;
            _filePath = Path.Combine(storagePath, "prompts.json");
            _tempFilePath = Path.Combine(storagePath, "prompts.json.tmp");
            _backupFilePath = Path.Combine(storagePath, "prompts.json.bak");
        }

        public async Task<IReadOnlyList<PromptItem>> LoadAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var targetPath = _filePath;

                if (!File.Exists(targetPath) && File.Exists(_backupFilePath))
                {
                    targetPath = _backupFilePath;
                    WasRecoveredFromBackup = true;
                    Log.Warning("Main data file is missing. Attempting to load from prompts.json.bak.");
                }

                if (!File.Exists(targetPath))
                {
                    Log.Information("Data file does not exist. Returning an empty list.");
                    return new List<PromptItem>();
                }

                try
                {
                    var json = await File.ReadAllTextAsync(targetPath);
                    var prompts = JsonSerializer.Deserialize<List<PromptItem>>(json);

                    EnsureDefaults(prompts);

                    if (WasRecoveredFromBackup)
                    {
                        Log.Warning("Recovered data from prompts.json.bak. Loaded {Count} items.", prompts?.Count ?? 0);
                    }
                    else
                    {
                        Log.Information("Data file loaded successfully. Loaded {Count} items.", prompts?.Count ?? 0);
                    }

                    return prompts ?? new List<PromptItem>();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to read or parse prompts.json. Attempting backup recovery.");

                    if (File.Exists(_backupFilePath))
                    {
                        try
                        {
                            var json = await File.ReadAllTextAsync(_backupFilePath);
                            var prompts = JsonSerializer.Deserialize<List<PromptItem>>(json);

                            EnsureDefaults(prompts);

                            WasRecoveredFromBackup = true;
                            Log.Warning("Recovered data from prompts.json.bak after primary file failure. Loaded {Count} items.", prompts?.Count ?? 0);
                            return prompts ?? new List<PromptItem>();
                        }
                        catch (Exception backupEx)
                        {
                            Log.Error(backupEx, "Failed to read prompts.json.bak. Unable to recover data.");
                            return new List<PromptItem>();
                        }
                    }

                    Log.Error("Primary data file is invalid and no backup is available. Returning an empty list.");
                    return new List<PromptItem>();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SaveAsync(IReadOnlyList<PromptItem> prompts)
        {
            if (prompts == null) throw new ArgumentNullException(nameof(prompts));

            await _semaphore.WaitAsync();
            try
            {
                Directory.CreateDirectory(_storagePath);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(prompts, options);

                await File.WriteAllTextAsync(_tempFilePath, json);

                if (File.Exists(_filePath))
                {
                    if (File.Exists(_backupFilePath))
                    {
                        File.Delete(_backupFilePath);
                    }

                    File.Copy(_filePath, _backupFilePath);
                }

                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }

                File.Move(_tempFilePath, _filePath);
                WasRecoveredFromBackup = false;

                if (File.Exists(_tempFilePath))
                {
                    File.Delete(_tempFilePath);
                }

                Log.Information("Data saved safely. Wrote {Count} items to prompts.json.", prompts.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save data to prompts.json.");
                throw new InvalidOperationException("Failed to save prompts securely.", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void EnsureDefaults(List<PromptItem>? items)
        {
            if (items == null) return;

            var migrationLogged = false;
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.ItemType))
                {
                    continue;
                }

                item.ItemType = "Prompt";

                if (!migrationLogged)
                {
                    Log.Information("Legacy data records detected. Missing default fields were filled automatically.");
                    migrationLogged = true;
                }
            }
        }
    }
}
