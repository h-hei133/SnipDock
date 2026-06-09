using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using SnipDock.Core.Services;
using SnipDock.App.ViewModels;
using SnipDock.App.Services;

namespace SnipDock.Tests
{
    public class PromptPanelViewModelTests : IDisposable
    {
        private readonly List<string> _tempDirectories = new();

        private class MockPromptStore : IPromptStore
        {
            public List<PromptItem> Items { get; set; } = new();
            public bool WasRecoveredFromBackup => false;
            public Task<IReadOnlyList<PromptItem>> LoadAsync() => Task.FromResult<IReadOnlyList<PromptItem>>(Items);
            public Task SaveAsync(IReadOnlyList<PromptItem> prompts) => Task.CompletedTask;
        }

        private class MockAppSettingsStore : IAppSettingsStore
        {
            public AppSettings Settings { get; set; } = new();
            public AppSettings Load() => Settings;
            public void Save(AppSettings settings) => Settings = settings;
        }

        private class MockStartupLaunchService : IStartupLaunchService
        {
            public bool IsEnabled { get; set; } = false;
            public bool IsDevMode { get; set; } = false;
            public bool IsDevelopmentMode() => IsDevMode;
            public string GetCurrentExecutablePath() => @"C:\MockApp.exe";
            public Task<bool> IsEnabledAsync() => Task.FromResult(IsEnabled);
            public Task<bool> EnableAsync() { IsEnabled = true; return Task.FromResult(true); }
            public Task<bool> DisableAsync() { IsEnabled = false; return Task.FromResult(true); }
        }

        private (PromptPanelViewModel vm, PromptService service, MockAppSettingsStore settingsStore, BackupService backupService) CreateViewModel(MockPromptStore? store = null)
        {
            var promptStore = store ?? new MockPromptStore();
            var service = new PromptService(promptStore);
            var themeService = new ThemeService();
            var settingsStore = new MockAppSettingsStore();
            var tempStoragePath = Path.Combine(Path.GetTempPath(), "SnipDock_VM_Tests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempStoragePath);
            _tempDirectories.Add(tempStoragePath);
            var backupService = new BackupService(tempStoragePath);
            var importExportService = new ShelfImportExportService(service, backupService);
            var startupLaunchService = new MockStartupLaunchService();
            var tagManagementService = new TagManagementService(service, promptStore);
            var vm = new PromptPanelViewModel(service, themeService, settingsStore, importExportService, backupService, startupLaunchService, tagManagementService);
            return (vm, service, settingsStore, backupService);
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

        [Fact]
        public void InitialState_IsEmptyStateVisible()
        {
            var (vm, _, _, _) = CreateViewModel();

            Assert.Null(vm.SelectedPrompt);
            Assert.False(vm.IsEditing);
            Assert.False(vm.IsSettingsOpen);

            Assert.True(vm.IsEmptyStateVisible);
            Assert.False(vm.IsDetailVisible);
            Assert.False(vm.IsEditorVisible);
            Assert.False(vm.IsSettingsPanelVisible);
        }

        [Fact]
        public void SelectingPrompt_TransitionsToDetailVisible()
        {
            var (vm, _, _, _) = CreateViewModel();

            var item = new PromptItem { Name = "Test Prompt", Content = "Test Content" };

            // Act
            vm.SelectedPrompt = item;

            // Assert
            Assert.NotNull(vm.SelectedPrompt);
            Assert.True(vm.IsDetailVisible);
            Assert.False(vm.IsEmptyStateVisible);
            Assert.False(vm.IsEditorVisible);
            Assert.False(vm.IsSettingsPanelVisible);
        }

        [Fact]
        public void ToggleSettings_TransitionsToSettingsVisible()
        {
            var (vm, _, _, _) = CreateViewModel();

            // Act
            vm.ToggleSettingsCommand.Execute(null);

            // Assert
            Assert.True(vm.IsSettingsOpen);
            Assert.True(vm.IsSettingsPanelVisible);
            Assert.False(vm.IsDetailVisible);
            Assert.False(vm.IsEmptyStateVisible); // Empty state is false because Settings is open
            Assert.False(vm.IsEditorVisible);
        }

        [Fact]
        public void SelectingPromptWhileSettingsOpen_ClosesSettingsAndShowsDetail()
        {
            var (vm, _, _, _) = CreateViewModel();

            vm.ToggleSettingsCommand.Execute(null);
            Assert.True(vm.IsSettingsOpen);

            var item = new PromptItem { Name = "Test Prompt", Content = "Test Content" };

            // Act - select prompt
            vm.SelectedPrompt = item;

            // Assert - Settings should close and Detail should become visible
            Assert.False(vm.IsSettingsOpen);
            Assert.True(vm.IsDetailVisible);
            Assert.False(vm.IsSettingsPanelVisible);
            Assert.False(vm.IsEmptyStateVisible);
        }

        [Fact]
        public void OldData_PadsDefaultValuesCorrectly()
        {
            var item = new PromptItem();
            Assert.Equal("Prompt", item.ItemType);
            Assert.False(item.IsFavorite);
            Assert.Equal(0, item.UsageCount);
            Assert.Null(item.LastUsedAt);
        }

        [Fact]
        public async Task SearchAndFilterJointly_FiltersByNameTypeAndTags()
        {
            var store = new MockPromptStore();
            var item1 = new PromptItem { Name = "C# Helper", Tags = new List<string> { "Dev" }, ItemType = "Prompt", IsFavorite = true };
            var item2 = new PromptItem { Name = "Clean Script", Tags = new List<string> { "Dev" }, ItemType = "Command", IsFavorite = false };
            var item3 = new PromptItem { Name = "Sql format", Tags = new List<string> { "Db" }, ItemType = "Snippet", IsFavorite = true };
            store.Items.AddRange(new[] { item1, item2, item3 });

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            // 1. Initial sorted list has 3 items
            Assert.Equal(3, vm.Prompts.Count);
            // Favorite should be sorted first
            Assert.True(vm.Prompts[0].IsFavorite);

            // 2. Filter by Type = "Snippet" (Using stable Key "Snippet")
            vm.SelectedTypeFilter = "Snippet"; 
            Assert.Single(vm.Prompts);
            Assert.Equal("Sql format", vm.Prompts[0].Name);

            // 3. Reset type, filter by Tag = "Dev"
            vm.SelectedTypeFilter = "All";
            vm.SelectedTagFilter = "Dev";
            Assert.Equal(2, vm.Prompts.Count);

            // 4. Combined search text "C#"
            vm.SearchText = "C#";
            Assert.Single(vm.Prompts);
            Assert.Equal("C# Helper", vm.Prompts[0].Name);
        }

        [Fact]
        public async Task FilterSelections_UpdateDisplayNamesAfterLanguageReload()
        {
            var store = new MockPromptStore();
            store.Items.Add(new PromptItem { Name = "Test Prompt", Content = "Body", ItemType = "Prompt" });

            var (vm, _, settingsStore, _) = CreateViewModel(store);
            settingsStore.Settings.Language = "en-US";

            await vm.LoadPromptsAsync();

            Assert.Equal("All", vm.SelectedTypeFilter);
            Assert.NotNull(vm.SelectedTypeFilterItem);
            Assert.Equal("All", vm.SelectedTypeFilterItem!.Key);
            Assert.Equal("All", vm.SelectedTypeFilterItem.DisplayName);
            Assert.Equal("__ALL_TAGS__", vm.SelectedTagFilter);
            Assert.NotNull(vm.SelectedTagFilterItem);
            Assert.Equal("All tags", vm.SelectedTagFilterItem!.DisplayName);

            vm.IsLanguageChinese = true;

            Assert.Equal("All", vm.SelectedTypeFilter);
            Assert.NotNull(vm.SelectedTypeFilterItem);
            Assert.Equal("All", vm.SelectedTypeFilterItem!.Key);
            Assert.Equal("全部", vm.SelectedTypeFilterItem.DisplayName);
            Assert.Equal("__ALL_TAGS__", vm.SelectedTagFilter);
            Assert.NotNull(vm.SelectedTagFilterItem);
            Assert.Equal("全部标签", vm.SelectedTagFilterItem!.DisplayName);

            vm.IsLanguageEnglish = true;

            Assert.Equal("All", vm.SelectedTypeFilter);
            Assert.NotNull(vm.SelectedTypeFilterItem);
            Assert.Equal("All", vm.SelectedTypeFilterItem!.Key);
            Assert.Equal("All", vm.SelectedTypeFilterItem.DisplayName);
            Assert.Equal("__ALL_TAGS__", vm.SelectedTagFilter);
            Assert.NotNull(vm.SelectedTagFilterItem);
            Assert.Equal("All tags", vm.SelectedTagFilterItem!.DisplayName);
        }

        [Fact]
        public async Task ToggleFavorite_SavesFavoriteState()
        {
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Test Prompt", ItemType = "Prompt" };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];
            Assert.False(vm.SelectedPrompt.IsFavorite);

            // Toggle favorite
            vm.ToggleFavoriteCommand.Execute(null);
            Assert.True(vm.SelectedPrompt.IsFavorite);
        }

        [Fact]
        public async Task CopyContent_ClearSearchAndHidePanel_TriggersExpectedBehaviors()
        {
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Test Prompt", Content = "Test Content", ItemType = "Prompt" };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];

            // 1. Enable Hide and Clear settings
            vm.HidePanelAfterCopy = true;
            vm.ClearSearchAfterCopy = true;
            vm.SearchText = "Test";

            var tcs = new TaskCompletionSource<bool>();
            vm.HidePanelRequested += (s, e) => tcs.TrySetResult(true);

            // 2. Act - Copy content (uses STA Clipboard in WPF, so mock/simulate or use try-catch wrapper)
            try
            {
                vm.CopyContentCommand.Execute(item.Content);
            }
            catch (Exception)
            {
                // In headless testing context, Clipboard might fail. We manually invoke the logic if needed or catch the expected STA Exception.
            }

            // In our viewmodel logic, the properties will update regardless of Clipboard success/exception catch because we wrapped it.
            // Wait for HidePanelRequested or timeout after 1 second
            bool hidePanelRequested = await Task.WhenAny(tcs.Task, Task.Delay(1000)) == tcs.Task;

            // 3. Assert
            Assert.True(hidePanelRequested);
            Assert.Equal(string.Empty, vm.SearchText);
        }

        [Fact]
        public async Task EditExistingItem_Save_ResetsEditingAndRestoresSelectedPrompt()
        {
            // Arrange
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Original Name", Content = "Original Content", ItemType = "Prompt" };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];
            vm.EditCommand.Execute(vm.SelectedPrompt);
            Assert.True(vm.IsEditing);

            vm.EditingName = "Modified Name";

            // Act
            await vm.OnSavePromptAsync();

            // Assert
            Assert.False(vm.IsEditing);
            Assert.NotNull(vm.SelectedPrompt);
            Assert.Equal("Modified Name", vm.SelectedPrompt.Name);
            Assert.True(vm.IsDetailVisible);
        }

        [Fact]
        public async Task EditExistingItem_Cancel_ResetsEditingAndDiscardsChanges()
        {
            // Arrange
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Original Name", Content = "Original Content", ItemType = "Prompt" };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];
            vm.EditCommand.Execute(vm.SelectedPrompt);
            Assert.True(vm.IsEditing);

            vm.EditingName = "Modified Name";

            // Act
            vm.CancelEditCommand.Execute(null);

            // Assert
            Assert.False(vm.IsEditing);
            Assert.NotNull(vm.SelectedPrompt);
            Assert.Equal("Original Name", vm.SelectedPrompt.Name); // Discarded!
            Assert.True(vm.IsDetailVisible);
        }

        [Fact]
        public async Task CreateNewItem_Save_ResetsEditingAndSelectsNewItem()
        {
            // Arrange
            var store = new MockPromptStore();
            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.AddCommand.Execute(null);
            Assert.True(vm.IsEditing);

            vm.EditingName = "New Item Title";
            vm.EditingContent = "New Item Content";

            // Act
            await vm.OnSavePromptAsync();

            // Assert
            Assert.False(vm.IsEditing);
            Assert.NotNull(vm.SelectedPrompt);
            Assert.Equal("New Item Title", vm.SelectedPrompt.Name);
            Assert.True(vm.IsDetailVisible);
        }

        [Fact]
        public async Task CreateNewItem_Cancel_ResetsEditingAndRestoresPreviousState()
        {
            // Arrange
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Existing", Content = "Content", ItemType = "Prompt" };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];
            Assert.True(vm.IsDetailVisible);

            // Start Add
            vm.AddCommand.Execute(null);
            Assert.True(vm.IsEditing);

            // Act - Cancel
            vm.CancelEditCommand.Execute(null);

            // Assert
            Assert.False(vm.IsEditing);
            Assert.NotNull(vm.SelectedPrompt);
            Assert.Equal("Existing", vm.SelectedPrompt.Name); // Restored!
            Assert.True(vm.IsDetailVisible);
        }

        [Fact]
        public async Task StartEditing_ClosesSettingsPanel()
        {
            // Arrange
            var (vm, _, _, _) = CreateViewModel();
            await vm.LoadPromptsAsync();

            // Open settings
            vm.ToggleSettingsCommand.Execute(null);
            Assert.True(vm.IsSettingsOpen);
            Assert.True(vm.IsSettingsPanelVisible);

            // Act - Start Add
            vm.AddCommand.Execute(null);

            // Assert
            Assert.False(vm.IsSettingsOpen);
            Assert.False(vm.IsSettingsPanelVisible);
            Assert.True(vm.IsEditing);
            Assert.True(vm.IsEditorVisible);
        }

        [Fact]
        public async Task Save_EnsuresStatePropertiesAreNotified()
        {
            // Arrange
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Original Name", Content = "Original Content", ItemType = "Prompt" };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];
            vm.EditCommand.Execute(vm.SelectedPrompt);
            Assert.True(vm.IsEditing);
            Assert.True(vm.IsEditorVisible);
            Assert.False(vm.IsDetailVisible);

            // Act
            await vm.OnSavePromptAsync();

            // Assert
            Assert.False(vm.IsEditing);
            Assert.False(vm.IsEditorVisible);
            Assert.True(vm.IsDetailVisible); // Notified and visible!
        }

        [Fact]
        public async Task CopyContent_HidePanelDisabled_DoesNotTriggerHidePanel()
        {
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Test Prompt", Content = "Test Content", ItemType = "Prompt" };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];
            vm.HidePanelAfterCopy = false;

            var tcs = new TaskCompletionSource<bool>();
            vm.HidePanelRequested += (s, e) => tcs.TrySetResult(true);

            try
            {
                vm.CopyContentCommand.Execute(item.Content);
            }
            catch (Exception)
            {
                // STA Clipboard fallback
            }

            // Wait for HidePanelRequested or timeout after 800ms
            bool hidePanelRequested = await Task.WhenAny(tcs.Task, Task.Delay(800)) == tcs.Task;

            Assert.False(hidePanelRequested);
        }

        [Fact]
        public async Task CopyContent_HidePanelEnabled_TriggersHidePanelEvent()
        {
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Test Prompt", Content = "Test Content", ItemType = "Prompt" };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];
            vm.HidePanelAfterCopy = true;

            var tcs = new TaskCompletionSource<bool>();
            vm.HidePanelRequested += (s, e) => tcs.TrySetResult(true);

            try
            {
                vm.CopyContentCommand.Execute(item.Content);
            }
            catch (Exception)
            {
                // STA Clipboard fallback
            }

            // Wait for HidePanelRequested or timeout after 1200ms (since we have a 500ms delay in VM)
            bool hidePanelRequested = await Task.WhenAny(tcs.Task, Task.Delay(1200)) == tcs.Task;

            Assert.True(hidePanelRequested);
        }

        [Fact]
        public async Task QueryAsync_SortingLogic_PrioritizesPinnedFirst()
        {
            var store = new MockPromptStore();
            var item1 = new PromptItem { Name = "First Pinned", IsPinned = true, IsFavorite = false };
            var item2 = new PromptItem { Name = "Second Favorite", IsPinned = false, IsFavorite = true };
            var item3 = new PromptItem { Name = "Normal Item", IsPinned = false, IsFavorite = false };
            store.Items.AddRange(new[] { item3, item1, item2 });

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            // Pinned should be 1st, Favorite 2nd, Normal 3rd
            Assert.Equal(3, vm.Prompts.Count);
            Assert.Equal("First Pinned", vm.Prompts[0].Name);
            Assert.Equal("Second Favorite", vm.Prompts[1].Name);
            Assert.Equal("Normal Item", vm.Prompts[2].Name);
        }

        [Fact]
        public async Task TogglePinned_PreservesSelectionAfterRefresh()
        {
            var store = new MockPromptStore();
            var item = new PromptItem { Name = "Item to Pin", IsPinned = false };
            store.Items.Add(item);

            var (vm, _, _, _) = CreateViewModel(store);
            await vm.LoadPromptsAsync();

            vm.SelectedPrompt = vm.Prompts[0];
            Assert.False(vm.SelectedPrompt.IsPinned);

            // Act
            vm.TogglePinnedCommand.Execute(null);

            // Assert
            Assert.NotNull(vm.SelectedPrompt);
            Assert.True(vm.SelectedPrompt.IsPinned);
        }
    }
}
