using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using SnipDock.Core.Services;

namespace SnipDock.Tests
{
    public class ShelfImportExportAndQueryTests : IDisposable
    {
        private readonly List<string> _tempDirectories = new();

        private class MockPromptStore : IPromptStore
        {
            public List<PromptItem> Items { get; set; } = new();
            public bool WasRecoveredFromBackup => false;
            public Task<IReadOnlyList<PromptItem>> LoadAsync() => Task.FromResult<IReadOnlyList<PromptItem>>(Items);
            public Task SaveAsync(IReadOnlyList<PromptItem> prompts) => Task.CompletedTask;
        }

        [Fact]
        public async Task QueryAsync_AppliesJointFiltersAndSorting()
        {
            var store = new MockPromptStore();
            var item1 = new PromptItem { Name = "C# OOP Guide", Tags = new List<string> { "OOP", "C#" }, ItemType = "Prompt", IsFavorite = true };
            var item2 = new PromptItem { Name = "Git Command", Tags = new List<string> { "Git" }, ItemType = "Command", IsFavorite = false, UsageCount = 5, LastUsedAt = DateTime.UtcNow.AddMinutes(-10) };
            var item3 = new PromptItem { Name = "Python Script", Tags = new List<string> { "Python" }, ItemType = "Snippet", IsFavorite = false, UsageCount = 10, LastUsedAt = DateTime.UtcNow };
            var item4 = new PromptItem { Name = "General Note", Tags = new List<string> { "General" }, ItemType = "Note", IsFavorite = true };

            store.Items.AddRange(new[] { item1, item2, item3, item4 });

            var service = new PromptService(store);
            await service.InitializeAsync();

            // 1. Text Search + Type Filter (Favorites)
            var options = new PromptQueryOptions
            {
                SearchText = "OOP",
                SelectedTypeFilter = "Favorites",
                SelectedTagFilter = null
            };

            var results = await service.QueryAsync(options);
            Assert.Single(results);
            Assert.Equal("C# OOP Guide", results[0].Name);

            // 2. Filter by RecentlyUsed
            options = new PromptQueryOptions
            {
                SearchText = "",
                SelectedTypeFilter = "RecentlyUsed",
                SelectedTagFilter = null
            };

            results = await service.QueryAsync(options);
            Assert.Equal(2, results.Count);
            // Python Script should be sorted first since LastUsedAt is newer
            Assert.Equal("Python Script", results[0].Name);
            Assert.Equal("Git Command", results[1].Name);
        }

        [Fact]
        public async Task QueryAsync_SearchesTitleAndTagsButNotContent()
        {
            var store = new MockPromptStore();
            store.Items.AddRange(new[]
            {
                new PromptItem { Name = "Deploy checklist", Tags = new List<string> { "Release" }, Content = "hidden-token-only" },
                new PromptItem { Name = "Meeting notes", Tags = new List<string> { "hidden-token-tag" }, Content = "ordinary body" },
                new PromptItem { Name = "hidden-token-title", Tags = new List<string> { "Notes" }, Content = "ordinary body" }
            });

            var service = new PromptService(store);
            await service.InitializeAsync();

            var results = await service.QueryAsync(new PromptQueryOptions
            {
                SearchText = "hidden-token",
                SelectedTypeFilter = "All",
                SelectedTagFilter = null
            });

            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(results, item => item.Name == "Deploy checklist");
            Assert.Contains(results, item => item.Name == "Meeting notes");
            Assert.Contains(results, item => item.Name == "hidden-token-title");
        }

        [Fact]
        public async Task MarkAsUsedAndToggleFavorite_DoNotChangeUpdatedAt()
        {
            var store = new MockPromptStore();
            var originalUpdatedAt = DateTime.UtcNow.AddDays(-1);
            var item = new PromptItem 
            { 
                Name = "Static Item", 
                ItemType = "Prompt", 
                UpdatedAt = originalUpdatedAt,
                UsageCount = 0,
                IsFavorite = false
            };
            store.Items.Add(item);

            var service = new PromptService(store);
            await service.InitializeAsync();

            // 1. Mark as Used
            await service.MarkAsUsedAsync(item.Id);
            var updatedItem = (await service.GetAllAsync()).First(p => p.Id == item.Id);

            Assert.Equal(1, updatedItem.UsageCount);
            Assert.NotNull(updatedItem.LastUsedAt);
            Assert.Equal(originalUpdatedAt, updatedItem.UpdatedAt); // Should remain unchanged!

            // 2. Toggle Favorite
            await service.ToggleFavoriteAsync(item.Id);
            updatedItem = (await service.GetAllAsync()).First(p => p.Id == item.Id);

            Assert.True(updatedItem.IsFavorite);
            Assert.Equal(originalUpdatedAt, updatedItem.UpdatedAt); // Should remain unchanged!
        }

        [Fact]
        public async Task ImportAsync_PerformsStrictDataValidationAndDuplicateHandling()
        {
            var store = new MockPromptStore();
            var existingId = Guid.NewGuid();
            store.Items.Add(new PromptItem { Id = existingId, Name = "Existing Item", ItemType = "Prompt" });

            var service = new PromptService(store);
            await service.InitializeAsync();

            var tempStoragePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempStoragePath);
            _tempDirectories.Add(tempStoragePath);
            var backupService = new BackupService(tempStoragePath);
            var exchangeService = new ShelfImportExportService(service, backupService);
            
            // Construct import JSON with multiple test cases violating rules:
            var importItems = new List<PromptItem>
            {
                // Rule 1: Duplicate Id (should be skipped)
                new PromptItem { Id = existingId, Name = "Duplicate ID Item", ItemType = "Prompt" },

                // Rule 2: ItemType invalid (should default to "Prompt")
                new PromptItem { Id = Guid.NewGuid(), Name = "Invalid Type Item", ItemType = "EXOTIC_TYPE" },

                // Rule 3: Tags is null (should default to empty list)
                new PromptItem { Id = Guid.NewGuid(), Name = "Null Tags Item", ItemType = "Note", Tags = null! },

                // Rule 4: Content is null (should default to empty string)
                new PromptItem { Id = Guid.NewGuid(), Name = "Null Content Item", ItemType = "Snippet", Content = null! },

                // Rule 5: Name is empty (should default to "未命名条目")
                new PromptItem { Id = Guid.NewGuid(), Name = "   ", ItemType = "Prompt" },

                // Rule 6: UsageCount is less than 0 (should default to 0)
                new PromptItem { Id = Guid.NewGuid(), Name = "Negative Usage Item", ItemType = "Command", UsageCount = -100 }
            };

            var tempFile = Path.GetTempFileName();
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                var json = System.Text.Json.JsonSerializer.Serialize(importItems, options);
                await File.WriteAllTextAsync(tempFile, json);

                // Act
                var result = await exchangeService.ImportAsync(tempFile);

                // Assert
                Assert.Equal(5, result.ImportedCount); // 5 imported, 1 skipped
                Assert.Equal(1, result.SkippedCount);  // 1 duplicate ID skipped

                var dbItems = await service.GetAllAsync();
                
                // Rule 2 check
                var invalidType = dbItems.First(p => p.Name == "Invalid Type Item");
                Assert.Equal("Prompt", invalidType.ItemType);

                // Rule 3 check
                var nullTags = dbItems.First(p => p.Name == "Null Tags Item");
                Assert.NotNull(nullTags.Tags);
                Assert.Empty(nullTags.Tags);

                // Rule 4 check
                var nullContent = dbItems.First(p => p.Name == "Null Content Item");
                Assert.Equal(string.Empty, nullContent.Content);

                // Rule 5 check
                var emptyName = dbItems.First(p => p.Id == importItems[4].Id);
                Assert.Equal("未命名条目", emptyName.Name);

                // Rule 6 check
                var negUsage = dbItems.First(p => p.Name == "Negative Usage Item");
                Assert.Equal(0, negUsage.UsageCount);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task ImportAsync_CreatesBackupBeforeChangingItems()
        {
            var store = new MockPromptStore();
            var service = new PromptService(store);
            await service.InitializeAsync();

            var tempStoragePath = Path.Combine(Path.GetTempPath(), "SnipDock_ImportBackupTests_" + Guid.NewGuid());
            Directory.CreateDirectory(tempStoragePath);
            _tempDirectories.Add(tempStoragePath);

            var currentDataFile = Path.Combine(tempStoragePath, "prompts.json");
            await File.WriteAllTextAsync(currentDataFile, "[\"current data before import\"]");

            var backupService = new BackupService(tempStoragePath);
            var exchangeService = new ShelfImportExportService(service, backupService);

            var importFile = Path.Combine(tempStoragePath, "import.json");
            var importItems = new List<PromptItem>
            {
                new() { Id = Guid.NewGuid(), Name = "Imported Item", ItemType = "Prompt", Content = "Imported content" }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(importItems);
            await File.WriteAllTextAsync(importFile, json);

            var result = await exchangeService.ImportAsync(importFile);

            Assert.Equal(1, result.ImportedCount);
            var backupDir = Path.Combine(tempStoragePath, "backups");
            var backupFile = Directory.GetFiles(backupDir, "prompts_*.json").Single();
            Assert.Equal("[\"current data before import\"]", await File.ReadAllTextAsync(backupFile));
        }

        [Fact]
        public async Task ImportAsync_SkipsPossibleDuplicatesByTitleOrContentHash()
        {
            var store = new MockPromptStore();
            store.Items.Add(new PromptItem { Name = "Existing title", ItemType = "Prompt", Content = "Existing body" });
            store.Items.Add(new PromptItem { Name = "Existing content holder", ItemType = "Note", Content = "Same body" });

            var service = new PromptService(store);
            await service.InitializeAsync();

            var tempStoragePath = Path.Combine(Path.GetTempPath(), "SnipDock_DuplicateImportTests_" + Guid.NewGuid());
            Directory.CreateDirectory(tempStoragePath);
            _tempDirectories.Add(tempStoragePath);

            var backupService = new BackupService(tempStoragePath);
            var exchangeService = new ShelfImportExportService(service, backupService);
            var importFile = Path.Combine(tempStoragePath, "import.json");
            var importItems = new List<PromptItem>
            {
                new() { Id = Guid.NewGuid(), Name = " existing title ", ItemType = "Command", Content = "Different body" },
                new() { Id = Guid.NewGuid(), Name = "Unique title", ItemType = "Prompt", Content = "Same body" },
                new() { Id = Guid.NewGuid(), Name = "Brand new", ItemType = "Prompt", Content = "Brand new body" }
            };

            await File.WriteAllTextAsync(importFile, System.Text.Json.JsonSerializer.Serialize(importItems));

            var result = await exchangeService.ImportAsync(importFile);

            Assert.Equal(1, result.ImportedCount);
            Assert.Equal(2, result.SkippedCount);
            var all = await service.GetAllAsync();
            Assert.Contains(all, item => item.Name == "Brand new");
            Assert.DoesNotContain(all, item => item.Name == "Unique title");
        }

        public void Dispose()
        {
            foreach (var path in _tempDirectories)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch
                {
                    // Ignore cleanup failures in temp directories.
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
