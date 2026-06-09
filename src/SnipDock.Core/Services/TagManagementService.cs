using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using Microsoft.Extensions.Logging;

namespace SnipDock.Core.Services
{
    public class TagManagementService
    {
        private readonly PromptService _promptService;
        private readonly IPromptStore _promptStore;
        private readonly ILogger<TagManagementService>? _logger;

        public TagManagementService(PromptService promptService, IPromptStore promptStore, ILogger<TagManagementService>? logger = null)
        {
            _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
            _promptStore = promptStore ?? throw new ArgumentNullException(nameof(promptStore));
            _logger = logger;
        }

        public async Task<List<TagSummary>> GetTagSummariesAsync()
        {
            _logger?.LogInformation("Tag summaries loaded");
            var items = await _promptService.GetAllAsync();
            var tagGroups = items
                .SelectMany(p => p.Tags)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .GroupBy(t => t.Trim(), StringComparer.OrdinalIgnoreCase);

            var summaries = new List<TagSummary>();
            foreach (var g in tagGroups)
            {
                // Display casing: Order by most frequent casing, fallback to alphabetical
                var displayTag = g.GroupBy(x => x)
                    .OrderByDescending(x => x.Count())
                    .ThenBy(x => x.Key)
                    .First().Key;

                summaries.Add(new TagSummary
                {
                    TagName = displayTag,
                    Count = g.Count()
                });
            }

            return summaries.OrderBy(s => s.TagName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public async Task RenameTagAsync(string oldTag, string newTag)
        {
            if (string.IsNullOrWhiteSpace(oldTag) || string.IsNullOrWhiteSpace(newTag))
                throw new ArgumentException("Tag names cannot be empty.");

            oldTag = oldTag.Trim();
            newTag = newTag.Trim();

            if (oldTag.Equals(newTag, StringComparison.OrdinalIgnoreCase))
                return;

            _logger?.LogInformation("Tag rename start: {OldTag} to {NewTag}", oldTag, newTag);
            try
            {
                var all = await _promptService.GetAllAsync();
                bool changed = false;

                foreach (var item in all)
                {
                    var oldTagIndex = item.Tags.FindIndex(t => t.Trim().Equals(oldTag, StringComparison.OrdinalIgnoreCase));
                    if (oldTagIndex >= 0)
                    {
                        var newTagIndex = item.Tags.FindIndex(t => t.Trim().Equals(newTag, StringComparison.OrdinalIgnoreCase));
                        if (newTagIndex >= 0)
                        {
                            // Already has new tag, just remove old tag to avoid duplicate
                            item.Tags.RemoveAt(oldTagIndex);
                        }
                        else
                        {
                            // Rename old tag to new tag
                            item.Tags[oldTagIndex] = newTag;
                        }
                        changed = true;
                    }
                }

                if (changed)
                {
                    await _promptStore.SaveAsync(all.ToList());
                    await _promptService.InitializeAsync();
                }
                _logger?.LogInformation("Tag rename completed: {OldTag} to {NewTag}", oldTag, newTag);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Tag rename failed: {OldTag} to {NewTag}", oldTag, newTag);
                throw;
            }
        }

        public async Task MergeTagsAsync(string sourceTag, string targetTag)
        {
            if (string.IsNullOrWhiteSpace(sourceTag) || string.IsNullOrWhiteSpace(targetTag))
                throw new ArgumentException("Tag names cannot be empty.");

            sourceTag = sourceTag.Trim();
            targetTag = targetTag.Trim();

            if (sourceTag.Equals(targetTag, StringComparison.OrdinalIgnoreCase))
                return;

            _logger?.LogInformation("Tag merge start: {SourceTag} into {TargetTag}", sourceTag, targetTag);
            try
            {
                var all = await _promptService.GetAllAsync();
                bool changed = false;

                foreach (var item in all)
                {
                    var sourceIndex = item.Tags.FindIndex(t => t.Trim().Equals(sourceTag, StringComparison.OrdinalIgnoreCase));
                    if (sourceIndex >= 0)
                    {
                        var targetIndex = item.Tags.FindIndex(t => t.Trim().Equals(targetTag, StringComparison.OrdinalIgnoreCase));
                        if (targetIndex >= 0)
                        {
                            // Target tag already exists, just remove source tag
                            item.Tags.RemoveAt(sourceIndex);
                        }
                        else
                        {
                            // Replace source tag with target tag
                            item.Tags[sourceIndex] = targetTag;
                        }
                        changed = true;
                    }
                }

                if (changed)
                {
                    await _promptStore.SaveAsync(all.ToList());
                    await _promptService.InitializeAsync();
                }
                _logger?.LogInformation("Tag merge completed: {SourceTag} into {TargetTag}", sourceTag, targetTag);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Tag merge failed: {SourceTag} into {TargetTag}", sourceTag, targetTag);
                throw;
            }
        }

        public void NormalizeTagsForItem(PromptItem item)
        {
            if (item == null || item.Tags == null) return;
            var normalized = item.Tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .GroupBy(t => t, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            item.Tags.Clear();
            item.Tags.AddRange(normalized);
        }
    }
}
