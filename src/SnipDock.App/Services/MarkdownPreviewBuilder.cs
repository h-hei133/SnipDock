using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SnipDock.App.Services
{
    public static class MarkdownPreviewBuilder
    {
        public static FlowDocument Build(string? markdown)
        {
            var document = new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontSize = 12.5,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                LineHeight = 20
            };
            document.SetResourceReference(TextElement.ForegroundProperty, "ThemeTextPrimaryBrush");

            var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');
            var index = 0;
            while (index < lines.Length)
            {
                var line = lines[index];
                var trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    index++;
                    continue;
                }

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    index = AddCodeBlock(document, lines, index + 1);
                    continue;
                }

                if (trimmed == "---" || trimmed == "***")
                {
                    document.Blocks.Add(CreateRule());
                    index++;
                    continue;
                }

                if (trimmed.StartsWith("> ", StringComparison.Ordinal))
                {
                    index = AddQuote(document, lines, index);
                    continue;
                }

                if (IsListItem(trimmed))
                {
                    index = AddList(document, lines, index);
                    continue;
                }

                if (TryAddHeading(document, trimmed))
                {
                    index++;
                    continue;
                }

                index = AddParagraph(document, lines, index);
            }

            if (document.Blocks.Count == 0)
            {
                document.Blocks.Add(CreateParagraph(string.Empty));
            }

            return document;
        }

        private static int AddCodeBlock(FlowDocument document, string[] lines, int index)
        {
            var code = new System.Text.StringBuilder();
            while (index < lines.Length && !lines[index].Trim().StartsWith("```", StringComparison.Ordinal))
            {
                if (code.Length > 0) code.AppendLine();
                code.Append(lines[index]);
                index++;
            }

            var text = new TextBlock
            {
                Text = code.ToString(),
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(10)
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");

            var border = new Border
            {
                Child = text,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 4, 0, 10)
            };
            border.SetResourceReference(Border.BackgroundProperty, "ThemeHeaderBackgroundBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "ThemeBorderBrush");

            document.Blocks.Add(new BlockUIContainer(border));
            return index < lines.Length ? index + 1 : index;
        }

        private static int AddQuote(FlowDocument document, string[] lines, int index)
        {
            var quote = new System.Text.StringBuilder();
            while (index < lines.Length && lines[index].Trim().StartsWith("> ", StringComparison.Ordinal))
            {
                if (quote.Length > 0) quote.AppendLine();
                quote.Append(lines[index].Trim()[2..]);
                index++;
            }

            var paragraph = CreateParagraph(quote.ToString());
            paragraph.Margin = new Thickness(10, 4, 0, 10);
            paragraph.BorderThickness = new Thickness(3, 0, 0, 0);
            paragraph.Padding = new Thickness(10, 0, 0, 0);
            paragraph.SetResourceReference(Block.BorderBrushProperty, "AccentColorBrush");
            document.Blocks.Add(paragraph);
            return index;
        }

        private static int AddList(FlowDocument document, string[] lines, int index)
        {
            var list = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(18, 4, 0, 10) };
            while (index < lines.Length && IsListItem(lines[index].Trim()))
            {
                var text = lines[index].Trim()[2..].Trim();
                list.ListItems.Add(new ListItem(CreateParagraph(text)));
                index++;
            }

            document.Blocks.Add(list);
            return index;
        }

        private static int AddParagraph(FlowDocument document, string[] lines, int index)
        {
            var paragraph = new System.Text.StringBuilder();
            while (index < lines.Length)
            {
                var trimmed = lines[index].Trim();
                if (string.IsNullOrWhiteSpace(trimmed) ||
                    trimmed.StartsWith("#", StringComparison.Ordinal) ||
                    trimmed.StartsWith("```", StringComparison.Ordinal) ||
                    trimmed.StartsWith("> ", StringComparison.Ordinal) ||
                    IsListItem(trimmed) ||
                    trimmed == "---" ||
                    trimmed == "***")
                {
                    break;
                }

                if (paragraph.Length > 0) paragraph.Append(' ');
                paragraph.Append(trimmed);
                index++;
            }

            document.Blocks.Add(CreateParagraph(paragraph.ToString()));
            return index;
        }

        private static bool TryAddHeading(FlowDocument document, string trimmed)
        {
            var level = 0;
            while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
            {
                level++;
            }

            if (level == 0 || level >= trimmed.Length || trimmed[level] != ' ')
            {
                return false;
            }

            var paragraph = CreateParagraph(trimmed[(level + 1)..].Trim());
            paragraph.FontWeight = FontWeights.Bold;
            paragraph.FontSize = level switch
            {
                1 => 22,
                2 => 18,
                3 => 15,
                _ => 13.5
            };
            paragraph.Margin = new Thickness(0, 0, 0, 10);
            document.Blocks.Add(paragraph);
            return true;
        }

        private static Paragraph CreateParagraph(string text)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "ThemeTextPrimaryBrush");
            paragraph.Inlines.Add(new Run(text));
            return paragraph;
        }

        private static BlockUIContainer CreateRule()
        {
            var border = new Border { Height = 1, Margin = new Thickness(0, 4, 0, 12) };
            border.SetResourceReference(Border.BackgroundProperty, "ThemeBorderBrush");
            return new BlockUIContainer(border);
        }

        private static bool IsListItem(string trimmed)
        {
            return trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                   trimmed.StartsWith("* ", StringComparison.Ordinal);
        }
    }
}
