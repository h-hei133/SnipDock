using System;
using System.Collections.Generic;

namespace SnipDock.Core.Models
{
    public class PromptItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Phase 3 Extended Fields
        public string ItemType { get; set; } = "Prompt";
        public bool IsFavorite { get; set; } = false;
        public bool IsPinned { get; set; } = false;
        public int UsageCount { get; set; } = 0;
        public DateTime? LastUsedAt { get; set; } = null;
    }
}
