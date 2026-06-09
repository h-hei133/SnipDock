using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SnipDock.Core.Services
{
    public class BackupService
    {
        private readonly string _storagePath;
        private readonly string _filePath;
        private readonly string _backupDir;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public BackupService(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path cannot be empty.", nameof(storagePath));

            _storagePath = storagePath;
            _filePath = Path.Combine(storagePath, "prompts.json");
            _backupDir = Path.Combine(storagePath, "backups");
        }

        public async Task BackupOnStartupIfNeededAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
                {
                    return; // Skip if main prompts file doesn't exist or is empty
                }

                if (!Directory.Exists(_backupDir))
                {
                    Directory.CreateDirectory(_backupDir);
                }

                // Check if we already have a backup from today
                var files = Directory.GetFiles(_backupDir, "prompts_*.json")
                                     .Select(f => new FileInfo(f))
                                     .OrderByDescending(f => f.CreationTime)
                                     .ToList();

                var todayString = DateTime.Today.ToString("yyyyMMdd");
                bool alreadyBackedUpToday = files.Any(f => f.Name.Contains($"prompts_{todayString}_"));

                if (!alreadyBackedUpToday)
                {
                    Log.Information("开始执行每日首次启动自动备份...");
                    await CreateBackupInternalAsync("Startup");
                    await CleanupOldBackupsInternalAsync(20);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "每日自动备份失败");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task BackupBeforeImportAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                Log.Information("执行导入前自动备份...");
                await CreateBackupInternalAsync("BeforeImport");
                await CleanupOldBackupsInternalAsync(20);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导入前自动备份失败");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task CreateBackupAsync(string reason)
        {
            await _semaphore.WaitAsync();
            try
            {
                await CreateBackupInternalAsync(reason);
                await CleanupOldBackupsInternalAsync(20);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task RestoreBackupAsync(string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
                throw new ArgumentException("Backup file path cannot be empty.", nameof(backupFilePath));
            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException("Selected backup file not found.", backupFilePath);

            await _semaphore.WaitAsync();
            try
            {
                // 1. Create a safety backup of the CURRENT data first!
                await CreateBackupInternalAsync("BeforeRestore");

                // 2. Replace current prompts.json with selected backup file
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }
                
                using (var sourceStream = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var destinationStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }

                Log.Information("成功从备份文件 {BackupPath} 中恢复数据。", backupFilePath);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task CleanupOldBackupsAsync(int keepCount)
        {
            await _semaphore.WaitAsync();
            try
            {
                await CleanupOldBackupsInternalAsync(keepCount);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task CreateBackupInternalAsync(string reason)
        {
            if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
            {
                return; // Nothing to backup
            }

            if (!Directory.Exists(_backupDir))
            {
                Directory.CreateDirectory(_backupDir);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFilename = $"prompts_{timestamp}.json";
            var destPath = Path.Combine(_backupDir, backupFilename);

            // Copy file securely
            using (var sourceStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var destinationStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }

            Log.Information("成功备份文件: {Filename} 原因: {Reason}", backupFilename, reason);
        }

        private Task CleanupOldBackupsInternalAsync(int keepCount)
        {
            try
            {
                if (!Directory.Exists(_backupDir)) return Task.CompletedTask;

                var files = Directory.GetFiles(_backupDir, "prompts_*.json")
                                     .Select(f => new FileInfo(f))
                                     .OrderByDescending(f => f.CreationTime)
                                     .Skip(keepCount)
                                     .ToList();

                foreach (var file in files)
                {
                    file.Delete();
                    Log.Information("自动清理旧备份文件: {Filename}", file.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "清理旧备份文件失败");
            }
            return Task.CompletedTask;
        }
    }
}