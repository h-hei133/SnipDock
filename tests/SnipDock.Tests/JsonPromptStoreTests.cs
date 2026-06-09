using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using SnipDock.Core.Models;
using SnipDock.Infrastructure.Storage;

namespace SnipDock.Tests
{
    public class JsonPromptStoreTests : IDisposable
    {
        private readonly string _tempDir;

        public JsonPromptStoreTests()
        {
            // Use a unique subfolder inside the temporary folder for isolation
            _tempDir = Path.Combine(Path.GetTempPath(), "SnipDockTests_" + Guid.NewGuid().ToString("N"));
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
                // Suppress clean-up failures in testing environments
            }
        }

        [Fact]
        public async Task SaveAndLoad_ReturnsConsistentData()
        {
            var store = new JsonPromptStore(_tempDir);

            var list = new List<PromptItem>
            {
                new() { Name = "Test 1", Content = "Content 1", Tags = new List<string> { "Tag1" } },
                new() { Name = "Test 2", Content = "Content 2" }
            };

            // Save
            await store.SaveAsync(list);

            // Load
            var loaded = await store.LoadAsync();

            Assert.Equal(2, loaded.Count);
            Assert.Equal("Test 1", loaded[0].Name);
            Assert.Equal("Content 1", loaded[0].Content);
            Assert.Single(loaded[0].Tags);
            Assert.Equal("Tag1", loaded[0].Tags[0]);
            Assert.Equal("Test 2", loaded[1].Name);
        }

        [Fact]
        public async Task Load_PrimaryMissingButBackupExists_RecoversFromBackup()
        {
            var store = new JsonPromptStore(_tempDir);

            var list = new List<PromptItem>
            {
                new() { Name = "Backup Recovered", Content = "Secret info" }
            };

            // Save once to create prompts.json
            await store.SaveAsync(list);

            // Save a second time to trigger backup creation (prompts.json is backed up to prompts.json.bak before overwrite)
            await store.SaveAsync(list);

            var primaryFile = Path.Combine(_tempDir, "prompts.json");
            var backupFile = Path.Combine(_tempDir, "prompts.json.bak");

            // Verify both primary and backup files are safely written
            Assert.True(File.Exists(primaryFile));
            Assert.True(File.Exists(backupFile));

            // Now delete primary file to simulate a partial crash/loss
            File.Delete(primaryFile);
            Assert.False(File.Exists(primaryFile));

            // Load should recover cleanly from backup!
            var loaded = await store.LoadAsync();
            Assert.Single(loaded);
            Assert.Equal("Backup Recovered", loaded[0].Name);
        }
    }
}
