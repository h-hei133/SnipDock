using System;
using Xunit;
using SnipDock.Core.Utils;

namespace SnipDock.Tests
{
    public class ClipboardEntryFactoryTests
    {
        [Theory]
        [InlineData("Short first line", "Short first line")]
        [InlineData("This is exactly 30 characters!", "This is exactly 30 characters!")]
        [InlineData("Short first line\r\nSecond line is longer", "Short first line")]
        [InlineData("This first line is extremely long and exceeds the thirty characters limit.", "来自剪贴板的条目")]
        public void CreateDraft_GeneratesCorrectTitle(string clipboardText, string expectedTitle)
        {
            // Act
            var draft = ClipboardEntryFactory.CreateDraft(clipboardText, "All");

            // Assert
            Assert.Equal(expectedTitle, draft.Name);
            Assert.Equal(clipboardText, draft.Content);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void CreateDraft_ThrowsOnNullOrWhitespace(string? clipboardText)
        {
            Assert.Throws<ArgumentException>(() => ClipboardEntryFactory.CreateDraft(clipboardText!, "All"));
        }

        [Theory]
        [InlineData("Command", "Command")]
        [InlineData("Snippet", "Snippet")]
        [InlineData("Prompt", "Prompt")]
        [InlineData("Note", "Note")]
        [InlineData("All", "Note")]
        [InlineData("Favorites", "Note")]
        [InlineData("RecentlyUsed", "Note")]
        [InlineData("", "Note")]
        public void CreateDraft_AssignsCorrectDefaultType(string filter, string expectedType)
        {
            // Act
            var draft = ClipboardEntryFactory.CreateDraft("Some text", filter);

            // Assert
            Assert.Equal(expectedType, draft.ItemType);
        }
    }
}
