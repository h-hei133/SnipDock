using System;
using System.Collections.Generic;
using System.Linq;
using SnipDock.Core.Models;
using SnipDock.Core.Services;
using Xunit;

namespace SnipDock.Tests
{
    public class DuplicateDetectionServiceTests
    {
        [Fact]
        public void FindDuplicates_DetectsTitleAndContentHashDuplicates()
        {
            var items = new List<PromptItem>
            {
                new() { Id = Guid.NewGuid(), Name = "Same Title", Content = "one" },
                new() { Id = Guid.NewGuid(), Name = " same title ", Content = "two" },
                new() { Id = Guid.NewGuid(), Name = "Other", Content = "same content" },
                new() { Id = Guid.NewGuid(), Name = "Another", Content = "same content" }
            };

            var groups = DuplicateDetectionService.FindDuplicates(items);

            Assert.Contains(groups, group => group.Reason == "Title" && group.Items.Count == 2);
            Assert.Contains(groups, group => group.Reason == "ContentHash" && group.Items.Count == 2);
        }

        [Fact]
        public void ComputeContentHash_IsStable()
        {
            var first = DuplicateDetectionService.ComputeContentHash("content");
            var second = DuplicateDetectionService.ComputeContentHash("content");

            Assert.Equal(first, second);
            Assert.NotEqual(first, DuplicateDetectionService.ComputeContentHash("other"));
        }
    }
}
