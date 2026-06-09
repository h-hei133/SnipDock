using Xunit;
using SnipDock.Core.Utils;

namespace SnipDock.Tests
{
    public class TagParserTests
    {
        [Fact]
        public void Parse_EmptyOrNullInput_ReturnsEmptyList()
        {
            Assert.Empty(TagParser.Parse(null!));
            Assert.Empty(TagParser.Parse("   "));
        }

        [Fact]
        public void Parse_SingleTag_ReturnsTrimmedTag()
        {
            var result = TagParser.Parse("  WPF  ");
            Assert.Single(result);
            Assert.Equal("WPF", result[0]);
        }

        [Fact]
        public void Parse_MultipleSeparators_ReturnsCorrectTags()
        {
            var input = "日常, 命令, 翻译; WPF；Code Review\n .NET 9 ";
            var result = TagParser.Parse(input);
            Assert.Equal(6, result.Count);
            Assert.Equal("日常", result[0]);
            Assert.Equal("命令", result[1]);
            Assert.Equal("翻译", result[2]);
            Assert.Equal("WPF", result[3]);
            Assert.Equal("Code Review", result[4]); // Keeps inner spaces
            Assert.Equal(".NET 9", result[5]); // Splits on newline
            Assert.Equal(".NET 9", result[5]); // Splits on newline
        }

        [Fact]
        public void Parse_DuplicateTags_DeduplicatesCaseInsensitively()
        {
            var input = "wpf, WPF, WPF, WPF";
            var result = TagParser.Parse(input);

            Assert.Single(result);
            Assert.Equal("wpf", result[0]); // Preserves first casing
        }
    }
}
