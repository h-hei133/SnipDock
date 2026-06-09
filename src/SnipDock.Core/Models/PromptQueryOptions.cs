using System;

namespace SnipDock.Core.Models
{
    public class PromptQueryOptions
    {
        public string SearchText { get; set; } = string.Empty;
        public string SelectedTypeFilter { get; set; } = "All"; // All, Prompt, Command, Snippet, Note, Favorites, RecentlyUsed
        public string? SelectedTagFilter { get; set; } = null;
    }
}
