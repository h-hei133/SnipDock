using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using SnipDock.Core.Services;

namespace SnipDock.Tests
{
    public class TagManagementTests
    {
        private class MockPromptStore : IPromptStore
        {
            public List<PromptItem> Items { get; set; } = new();
            public bool WasRecoveredFromBackup => false;
            public Task<IReadOnlyList<PromptItem>> LoadAsync() => Task.FromResult<IReadOnlyList<PromptItem>>(Items);
            public Task SaveAsync(IReadOnlyList<PromptItem> prompts)
            {
                Items = new List<PromptItem>(prompts);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task GetTagSummaries_GroupsCaseInsensitively_AndPreservesInnerSpacesAndSorts()
        {
            var store = new MockPromptStore();
            var item1 = new PromptItem { Tags = new List<string> { "Code Review", "Dev", "c#" } };
            var item2 = new PromptItem { Tags = new List<string> { "code review", "Db" } };
            var item3 = new PromptItem { Tags = new List<string> { "C#", "Dev" } };
            var item4 = new PromptItem { Tags = new List<string> { "C#", "Code Review" } };
            store.Items.AddRange(new[] { item1, item2, item3, item4 });

            var service = new PromptService(store);
            await service.InitializeAsync();
            var tagManager = new TagManagementService(service, store);

            var summaries = await tagManager.GetTagSummariesAsync();

            // Expected tags (case-insensitive sorted using CurrentCultureIgnoreCase):
            // "C#" (count 3), "Code Review" (count 3), "Db" (count 1), "Dev" (count 2)
            Assert.Equal(4, summaries.Count);
            
            Assert.Equal("C#", summaries[0].TagName);
            Assert.Equal(3, summaries[0].Count);

            Assert.Equal("Code Review", summaries[1].TagName); // Inner space preserved!
            Assert.Equal(3, summaries[1].Count);

            Assert.Equal("Db", summaries[2].TagName);
            Assert.Equal(1, summaries[2].Count);

            Assert.Equal("Dev", summaries[3].TagName);
            Assert.Equal(2, summaries[3].Count);
        }

        [Fact]
        public async Task RenameTag_UpdatesAllAssociatedPrompts_AndDeduplicates()
        {
            var store = new MockPromptStore();
            var item1 = new PromptItem { Tags = new List<string> { "old-tag", "other" } };
            var item2 = new PromptItem { Tags = new List<string> { "Old-Tag", "new-tag" } }; // Duplicate warning
            store.Items.AddRange(new[] { item1, item2 });

            var service = new PromptService(store);
            await service.InitializeAsync();
            var tagManager = new TagManagementService(service, store);

            await tagManager.RenameTagAsync("old-tag", "new-tag");

            var all = await service.GetAllAsync();
            
            // item1: "old-tag" -> "new-tag", "other"
            Assert.Contains("new-tag", all[0].Tags);
            Assert.Contains("other", all[0].Tags);

            // item2: "Old-Tag" renamed to "new-tag" but it already has "new-tag", so it should deduplicate (remove "Old-Tag" and only have "new-tag")
            Assert.Single(all[1].Tags);
            Assert.Equal("new-tag", all[1].Tags[0]);
        }

        [Fact]
        public async Task MergeTags_MergesSuccessfully_AndCleansDuplicates()
        {
            var store = new MockPromptStore();
            var item1 = new PromptItem { Tags = new List<string> { "source-tag", "other" } };
            var item2 = new PromptItem { Tags = new List<string> { "Source-Tag", "target-tag" } };
            store.Items.AddRange(new[] { item1, item2 });

            var service = new PromptService(store);
            await service.InitializeAsync();
            var tagManager = new TagManagementService(service, store);

            await tagManager.MergeTagsAsync("source-tag", "target-tag");

            var all = await service.GetAllAsync();

            // item1: "source-tag" -> "target-tag", "other"
            Assert.Contains("target-tag", all[0].Tags);
            Assert.Contains("other", all[0].Tags);

            // item2: "Source-Tag" merged into "target-tag", since it already has "target-tag", "Source-Tag" is removed and duplicates are avoided
            Assert.Single(all[1].Tags);
            Assert.Equal("target-tag", all[1].Tags[0]);
        }
    }
}
