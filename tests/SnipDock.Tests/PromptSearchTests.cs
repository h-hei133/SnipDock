using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using SnipDock.Core.Services;

namespace SnipDock.Tests
{
    public class PromptSearchTests
    {
        private class MockPromptStore : IPromptStore
        {
            public List<PromptItem> Items { get; set; } = new();
            public bool WasRecoveredFromBackup => false;

            public Task<IReadOnlyList<PromptItem>> LoadAsync()
            {
                return Task.FromResult<IReadOnlyList<PromptItem>>(Items);
            }

            public Task SaveAsync(IReadOnlyList<PromptItem> prompts)
            {
                Items = new List<PromptItem>(prompts);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task SearchAsync_MatchesNameAndTags_ButNotContent()
        {
            var store = new MockPromptStore
            {
                Items = new List<PromptItem>
                {
                    new() { Name = "C# Helper", Tags = new List<string> { "Dev" }, Content = "Write clean code." },
                    // Test Chinese matches
                    new() { Name = "翻译助手", Tags = new List<string> { "English", "中文" }, Content = "Translate the following text." },
                    new() { Name = "Sql formatter", Tags = new List<string> { "DB" }, Content = "Format sql queries." }
                }
            };

            var service = new PromptService(store);
            await service.InitializeAsync();

            // 1. Search by Name (case-insensitive substring) - SHOULD MATCH
            var result1 = await service.SearchAsync("helper");
            Assert.Single(result1);
            Assert.Equal("C# Helper", result1[0].Name);

            // 2. Search by Tags (case-insensitive) - SHOULD MATCH
            var result2 = await service.SearchAsync("english");
            Assert.Single(result2);
            Assert.Equal("翻译助手", result2[0].Name);

            // 3. Search by Content - SHOULD NOT MATCH (Content search is deprecated)
            var result3 = await service.SearchAsync("clean");
            Assert.Empty(result3);

            // 4. Search by Chinese Name and Tag - SHOULD MATCH
            var result4 = await service.SearchAsync("翻译");
            Assert.Single(result4);
            Assert.Equal("翻译助手", result4[0].Name);

            var result5 = await service.SearchAsync("中文");
            Assert.Single(result5);
            Assert.Equal("翻译助手", result5[0].Name);

            // 5. Empty search returns all
            var result6 = await service.SearchAsync("");
            Assert.Equal(3, result6.Count);
        }
    }
}
