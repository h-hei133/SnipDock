using System.Collections.Generic;

namespace SnipDock.Core.Models
{
    public sealed class DuplicateGroup
    {
        public string Reason { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public List<PromptItem> Items { get; set; } = new();
    }
}
