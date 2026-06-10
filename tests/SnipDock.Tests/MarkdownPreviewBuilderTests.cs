using System;
using System.Linq;
using System.Threading;
using System.Windows.Documents;
using SnipDock.App.Services;
using Xunit;

namespace SnipDock.Tests
{
    public class MarkdownPreviewBuilderTests
    {
        [Fact]
        public void Build_CreatesHeadingParagraph()
        {
            RunSta(() =>
            {
                var document = MarkdownPreviewBuilder.Build("# Title");

                var paragraph = Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
                Assert.True(paragraph.FontWeight.ToOpenTypeWeight() >= 700);
                Assert.Contains("Title", new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text);
            });
        }

        [Fact]
        public void Build_CreatesCodeBlock()
        {
            RunSta(() =>
            {
                var document = MarkdownPreviewBuilder.Build("```csharp\nConsole.WriteLine();\n```");

                Assert.IsType<BlockUIContainer>(document.Blocks.FirstBlock);
            });
        }

        [Fact]
        public void Build_CreatesList()
        {
            RunSta(() =>
            {
                var document = MarkdownPreviewBuilder.Build("- one\n- two");

                var list = Assert.IsType<List>(document.Blocks.FirstBlock);
                Assert.Equal(2, list.ListItems.Count);
            });
        }

        [Fact]
        public void Build_CreatesFallbackParagraphForEmptyContent()
        {
            RunSta(() =>
            {
                var document = MarkdownPreviewBuilder.Build(string.Empty);

                Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
            });
        }

        private static void RunSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
            {
                throw exception;
            }
        }
    }
}
