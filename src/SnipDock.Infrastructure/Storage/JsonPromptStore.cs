using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using Serilog;

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
                string targetPath = _filePath;

                // Recovery path 1: Primary file is missing but backup exists
                if (!File.Exists(targetPath) && File.Exists(_backupFilePath))
                {
                    targetPath = _backupFilePath;
                    WasRecoveredFromBackup = true;                    Log.Warning("主数据文件不存在，正在尝试从备份文件加载: {FilePath} -> 备用路径: {BackupFilePath}", _filePath, _backupFilePath);
                }

                if (!File.Exists(targetPath))
                {                    Log.Information("数据文件不存在，返回空列表（首次使用或数据目录已清空）。");
                    return new List<PromptItem>();
                }

                try
                {
                    var json = await File.ReadAllTextAsync(targetPath);
                    var prompts = JsonSerializer.Deserialize<List<PromptItem>>(json);
                    
                    // Pad default values for older prompt records seamlessly
                    EnsureDefaults(prompts);

                    if (WasRecoveredFromBackup)
                    {                        Log.Warning("已从备份文件成功恢复数据: {BackupFilePath}，共加载 {Count} 条数据。", _backupFilePath, prompts?.Count ?? 0);
                    }
                    else
                    {                        Log.Information("数据文件加载成功，共加载 {Count} 条数据。", prompts?.Count ?? 0);
                    }

                    return prompts ?? new List<PromptItem>();
                }
                catch (Exception ex)
                {                    Log.Error(ex, "主数据文件读取或解析失败: {FilePath}，尝试从备份文件恢复。", _filePath);
                    
                    // Recovery path 2: Primary is corrupt, load from backup
                    if (File.Exists(_backupFilePath))
                    {
                        try
                        {
                            var json = await File.ReadAllTextAsync(_backupFilePath);
                            var prompts = JsonSerializer.Deserialize<List<PromptItem>>(json);
                            
                            // Pad default values for older prompt records seamlessly
                            EnsureDefaults(prompts);

                            WasRecoveredFromBackup = true;                            Log.Warning("主文件损坏，已从备份文件成功恢复: {BackupFilePath}，共加载 {Count} 条数据。", _backupFilePath, prompts?.Count ?? 0);
                            return prompts ?? new List<PromptItem>();
                        }
                        catch (Exception backupEx)
                        {                            Log.Error(backupEx, "备份文件读取失败: {BackupFilePath}，无法恢复数据。", _backupFilePath);
                            return new List<PromptItem>();
                        }
                    }
                    Log.Error("主数据文件损坏，且没有可用备份文件，返回空列表。");
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
                if (!Directory.Exists(_storagePath))
                {
                    Directory.CreateDirectory(_storagePath);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(prompts, options);

                // 1. Write to temp file
                await File.WriteAllTextAsync(_tempFilePath, json);

                // 2. Backup the current file if it exists
                if (File.Exists(_filePath))
                {
                    if (File.Exists(_backupFilePath))
                    {
                        File.Delete(_backupFilePath);
                    }
                    File.Copy(_filePath, _backupFilePath);
                }

                // 3. Move temp file to actual file path
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }
                File.Move(_tempFilePath, _filePath);

                // Reset recovery flag since we successfully saved new primary data
                WasRecoveredFromBackup = false;

                // 4. Safe cleanup of temp
                if (File.Exists(_tempFilePath))
                {
                    File.Delete(_tempFilePath);
                }
                Log.Information("数据已安全保存，共 {Count} 条记录写入 prompts.json。", prompts.Count);
            }
            catch (Exception ex)
            {                Log.Error(ex, "保存数据到 prompts.json 失败。");
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
            bool migrationLogged = false;
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.ItemType))
                {
                    item.ItemType = "Prompt";
                    
                    if (!migrationLogged)
                    {                        Log.Information("检测到旧版数据记录，已自动补全缺失字段默认値（ItemType='Prompt', IsFavorite=false, UsageCount=0, LastUsedAt=null）。");
                        migrationLogged = true;
                    }
                }
            }
        }
    }
}