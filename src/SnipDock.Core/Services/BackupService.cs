using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SnipDock.Core.Services
{
    public class BackupService
    {
        private readonly string _filePath;
        private readonly string _backupDir;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly ILogger<BackupService> _logger;

        public BackupService(string storagePath, ILogger<BackupService>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path cannot be empty.", nameof(storagePath));

            _filePath = Path.Combine(storagePath, "prompts.json");
            _backupDir = Path.Combine(storagePath, "backups");
            _logger = logger ?? NullLogger<BackupService>.Instance;
        }

        public async Task BackupOnStartupIfNeededAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!File.Exists(_filePath) || new FileInfo(_filePath).Length == 0)
                {
                    return;
                }

                Directory.CreateDirectory(_backupDir);

                var files = Directory.GetFiles(_backupDir, "prompts_*.json")
                                     .Select(f => new FileInfo(f))
                                     .OrderByDescending(f => f.CreationTime)
                                     .ToList();

                var todayString = DateTime.Today.ToString("yyyyMMdd");
                var alreadyBackedUpToday = files.Any(f => f.Name.Contains($"prompts_{todayString}_"));

                if (!alreadyBackedUpToday)
                {
                    _logger.LogInformation("Creating automatic startup backup.");
                    await CreateBackupInternalAsync("Startup");
                    await CleanupOldBackupsInternalAsync(20);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic startup backup failed.");
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
                _logger.LogInformation("Creating backup before import.");
                await CreateBackupInternalAsync("BeforeImport");
                await CleanupOldBackupsInternalAsync(20);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup before import failed.");
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
                await CreateBackupInternalAsync("BeforeRestore");

                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                }

                using (var sourceStream = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var destinationStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }

                _logger.LogInformation("Restored data from backup file {BackupFileName}.", Path.GetFileName(backupFilePath));
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
                return;
            }

            Directory.CreateDirectory(_backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFilename = $"prompts_{timestamp}.json";
            var destPath = Path.Combine(_backupDir, backupFilename);

            using (var sourceStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var destinationStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }

            _logger.LogInformation("Created backup file {BackupFileName}. Reason: {Reason}", backupFilename, reason);
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
                    _logger.LogInformation("Deleted old backup file {BackupFileName}.", file.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up old backup files.");
            }

            return Task.CompletedTask;
        }
    }
}
