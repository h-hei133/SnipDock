using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using SnipDock.Core.Services;

namespace SnipDock.Tests
{
    public class BackupServiceTests : IDisposable
    {
        private readonly string _tempPath;

        public BackupServiceTests()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), "SnipDock_BackupTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempPath))
            {
                try
                {
                    Directory.Delete(_tempPath, true);
                }
                catch
                {
                    // Ignore cleanup exceptions in temp directories
                }
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Backup_CreatesFormattedBackupFile()
        {
            // Arrange
            var promptsFile = Path.Combine(_tempPath, "prompts.json");
            await File.WriteAllTextAsync(promptsFile, "[\"dummy data\"]");

            var service = new BackupService(_tempPath);

            // Act
            await service.CreateBackupAsync("TestReason");

            // Assert
            var backupDir = Path.Combine(_tempPath, "backups");
            Assert.True(Directory.Exists(backupDir));

            var files = Directory.GetFiles(backupDir, "prompts_*.json");
            Assert.Single(files);

            var filename = Path.GetFileName(files[0]);
            // Format: prompts_yyyyMMdd_HHmmss.json -> length is 28 chars
            Assert.Equal(28, filename.Length);
            Assert.StartsWith("prompts_", filename);
            Assert.EndsWith(".json", filename);
        }

        [Fact]
        public async Task Backup_CreatesUniqueFilesWhenMultipleBackupsHappenInSameSecond()
        {
            var promptsFile = Path.Combine(_tempPath, "prompts.json");
            await File.WriteAllTextAsync(promptsFile, "[\"current data\"]");

            var service = new BackupService(_tempPath);

            await service.CreateBackupAsync("First");
            await service.CreateBackupAsync("Second");

            var backupDir = Path.Combine(_tempPath, "backups");
            var files = Directory.GetFiles(backupDir, "prompts_*.json");

            Assert.Equal(2, files.Length);
            Assert.Equal(2, files.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public async Task RestoreBackup_CreatesSafetyBackupBeforeOverwritingCurrentData()
        {
            var promptsFile = Path.Combine(_tempPath, "prompts.json");
            await File.WriteAllTextAsync(promptsFile, "[\"current data before restore\"]");

            var restoreSource = Path.Combine(_tempPath, "restore-source.json");
            await File.WriteAllTextAsync(restoreSource, "[\"restored data\"]");

            var service = new BackupService(_tempPath);

            await service.RestoreBackupAsync(restoreSource);

            Assert.Equal("[\"restored data\"]", await File.ReadAllTextAsync(promptsFile));

            var backupDir = Path.Combine(_tempPath, "backups");
            var safetyBackup = Directory.GetFiles(backupDir, "prompts_*.json")
                .Single();
            Assert.Equal("[\"current data before restore\"]", await File.ReadAllTextAsync(safetyBackup));
        }

        [Fact]
        public async Task Backup_RetainsOnlyRecent20Files()
        {
            // Arrange
            var promptsFile = Path.Combine(_tempPath, "prompts.json");
            await File.WriteAllTextAsync(promptsFile, "[\"dummy data\"]");

            var service = new BackupService(_tempPath);
            var backupDir = Path.Combine(_tempPath, "backups");
            Directory.CreateDirectory(backupDir);

            // Create 25 dummy backup files with simulated creation times
            for (int i = 0; i < 25; i++)
            {
                var timestamp = DateTime.Now.AddMinutes(-i).ToString("yyyyMMdd_HHmmss") + "_" + i;
                // Add unique timestamp format that fits standard length but allows fast mock files
                var filepath = Path.Combine(backupDir, $"prompts_{timestamp}.json");
                await File.WriteAllTextAsync(filepath, "[]");
                File.SetCreationTime(filepath, DateTime.Now.AddMinutes(-i));
            }

            // Act
            await service.CleanupOldBackupsAsync(20);

            // Assert
            var files = Directory.GetFiles(backupDir, "prompts_*.json");
            Assert.Equal(20, files.Length);
        }
    }
}
