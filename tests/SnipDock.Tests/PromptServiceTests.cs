using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using SnipDock.Core.Services;

namespace SnipDock.Tests
{
    public class PromptServiceTests
    {
        private class MockPromptStore : IPromptStore
        {
            public List<PromptItem> SavedItems { get; private set; } = new();
            public int SaveCallCount { get; private set; }
            public bool WasRecoveredFromBackup => false;

            public Task<IReadOnlyList<PromptItem>> LoadAsync()
            {
                return Task.FromResult<IReadOnlyList<PromptItem>>(new List<PromptItem>());
            }

            public Task SaveAsync(IReadOnlyList<PromptItem> prompts)
            {
                SavedItems = new List<PromptItem>(prompts);
                SaveCallCount++;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task AddAsync_AddsToMemoryAndSaves()
        {
            var mockStore = new MockPromptStore();
            var service = new PromptService(mockStore);
            await service.InitializeAsync();

            var item = new PromptItem { Name = "Add Test", Content = "Testing addition" };
            await service.AddAsync(item);

            var all = await service.GetAllAsync();
            Assert.Single(all);
            Assert.Equal("Add Test", all[0].Name);
            
            // Verify that store persistence was invoked
            Assert.Equal(1, mockStore.SaveCallCount);
            Assert.Single(mockStore.SavedItems);
            Assert.Equal("Add Test", mockStore.SavedItems[0].Name);
        }

        [Fact]
        public async Task UpdateAsync_ModifiesMemoryAndSaves()
        {
            var mockStore = new MockPromptStore();
            var service = new PromptService(mockStore);
            await service.InitializeAsync();

            var item = new PromptItem { Name = "Original", Content = "Initial" };
            await service.AddAsync(item);

            // Modify fields
            item.Name = "Updated Name";
            await service.UpdateAsync(item);

            var all = await service.GetAllAsync();
            Assert.Single(all);
            Assert.Equal("Updated Name", all[0].Name);
            Assert.Equal(2, mockStore.SaveCallCount); // 1 for add, 1 for update
        }

        [Fact]
        public async Task DeleteAsync_RemovesFromMemoryAndSaves()
        {
            var mockStore = new MockPromptStore();
            var service = new PromptService(mockStore);
            await service.InitializeAsync();

            var item = new PromptItem { Name = "To Delete", Content = "Delete me" };
            await service.AddAsync(item);

            await service.DeleteAsync(item.Id);

            var all = await service.GetAllAsync();
            Assert.Empty(all);
            Assert.Equal(2, mockStore.SaveCallCount); // 1 for add, 1 for delete
            Assert.Empty(mockStore.SavedItems);
        }

        [Fact]
        public async Task TogglePinned_DoesNotModifyUpdatedAt_AndSavesStateToStore()
        {
            var mockStore = new MockPromptStore();
            var service = new PromptService(mockStore);
            await service.InitializeAsync();

            var item = new PromptItem { Name = "Pin Test", Content = "Testing pinned" };
            await service.AddAsync(item);
            
            var originalUpdatedAt = item.UpdatedAt;
            var originalIsPinned = item.IsPinned;

            // Wait a tiny bit
            await Task.Delay(10);

            // Act
            await service.TogglePinnedAsync(item.Id);

            var all = await service.GetAllAsync();
            Assert.Single(all);
            Assert.NotEqual(originalIsPinned, all[0].IsPinned);
            Assert.True(all[0].IsPinned);
            Assert.Equal(originalUpdatedAt, all[0].UpdatedAt); // Must be identical!
            
            // Verify it was saved to the store
            Assert.Equal(2, mockStore.SaveCallCount); // 1 for add, 1 for toggle pin
            Assert.True(mockStore.SavedItems[0].IsPinned);
        }
    }
}
