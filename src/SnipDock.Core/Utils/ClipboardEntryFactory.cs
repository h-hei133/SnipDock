using System;
using System.Linq;

namespace SnipDock.Core.Utils
{
    public class ClipboardEntryFactory
    {
        public static ClipboardDraft CreateDraft(string clipboardText, string currentTypeFilter)
        {
            return CreateDraft(clipboardText, currentTypeFilter, "Clipboard item");
        }

        public static ClipboardDraft CreateDraft(string clipboardText, string currentTypeFilter, string fallbackTitle)
        {
            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                throw new ArgumentException("Clipboard text cannot be null or empty.", nameof(clipboardText));
            }

            // 1. Generate default title
            string title;
            var lines = clipboardText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var firstLine = lines.FirstOrDefault()?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(firstLine) && firstLine.Length <= 30)
            {
                title = firstLine;
            }
            else
            {
                title = string.IsNullOrWhiteSpace(fallbackTitle) ? "Clipboard item" : fallbackTitle;
            }

            // 2. Select default ItemType based on current filter key
            string defaultType = "Note";
            if (!string.IsNullOrEmpty(currentTypeFilter))
            {
                defaultType = currentTypeFilter switch
                {
                    "Command" => "Command",
                    "Snippet" => "Snippet",
                    "Prompt" => "Prompt",
                    _ => "Note"
                };
            }

            return new ClipboardDraft
            {
                Name = title,
                Content = clipboardText,
                ItemType = defaultType
            };
        }
    }

    public class ClipboardDraft
    {
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string ItemType { get; set; } = "Note";
    }
}
