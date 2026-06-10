using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SnipDock.Core.Models;

namespace SnipDock.Core.Services
{
    public static class DuplicateDetectionService
    {
        public static IReadOnlyList<DuplicateGroup> FindDuplicates(IEnumerable<PromptItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            var materialized = items.ToList();
            var groups = new List<DuplicateGroup>();

            groups.AddRange(materialized
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => NormalizeTitle(item.Name), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => new DuplicateGroup
                {
                    Reason = "Title",
                    Key = group.Key,
                    Items = group.ToList()
                }));

            groups.AddRange(materialized
                .Where(item => !string.IsNullOrWhiteSpace(item.Content))
                .GroupBy(item => ComputeContentHash(item.Content), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => new DuplicateGroup
                {
                    Reason = "ContentHash",
                    Key = group.Key,
                    Items = group.ToList()
                }));

            return groups;
        }

        public static string NormalizeTitle(string title)
        {
            return (title ?? string.Empty).Trim();
        }

        public static string ComputeContentHash(string content)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content ?? string.Empty));
            return Convert.ToHexString(bytes);
        }
    }
}
