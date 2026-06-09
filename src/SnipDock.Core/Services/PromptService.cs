using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using Microsoft.Extensions.Logging;

namespace SnipDock.Core.Services
{
    public class PromptService
    {
        private readonly IPromptStore _promptStore;
        private readonly List<PromptItem> _prompts = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly Microsoft.Extensions.Logging.ILogger<PromptService>? _logger;

        public PromptService(IPromptStore promptStore, Microsoft.Extensions.Logging.ILogger<PromptService>? logger = null)
        {
            _promptStore = promptStore;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                _prompts.Clear();
                var loaded = await _promptStore.LoadAsync();
                if (loaded != null)
                {
                    _prompts.AddRange(loaded);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<PromptItem>> GetAllAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                IReadOnlyList<PromptItem> readOnly = _prompts.AsReadOnly();
                return readOnly;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<PromptItem>> SearchAsync(string keyword)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(keyword))
                {
                    IReadOnlyList<PromptItem> all = _prompts.AsReadOnly();
                    return all;
                }

                var query = keyword.Trim();
                var result = _prompts.Where(p =>
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                IReadOnlyList<PromptItem> filtered = result.AsReadOnly();
                return filtered;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<PromptItem>> QueryAsync(PromptQueryOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            await _semaphore.WaitAsync();
            try
            {
                IEnumerable<PromptItem> filtered = _prompts;

                // 1. Text Search Filter (Title & Tags, case-insensitive, NOT content)
                if (!string.IsNullOrWhiteSpace(options.SearchText))
                {
                    var query = options.SearchText.Trim();
                    filtered = filtered.Where(p =>
                        p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        p.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase))
                    );
                }

                // 2. Type Filter using stable Keys (All, Prompt, Command, Snippet, Note, Favorites, RecentlyUsed)
                if (!string.IsNullOrWhiteSpace(options.SelectedTypeFilter) && 
                    !options.SelectedTypeFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    if (options.SelectedTypeFilter.Equals("Favorites", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Where(p => p.IsFavorite);
                    }
                    else if (options.SelectedTypeFilter.Equals("RecentlyUsed", StringComparison.OrdinalIgnoreCase))
                    {
                        filtered = filtered.Where(p => p.UsageCount > 0 && p.LastUsedAt != null);
                    }
                    else
                    {
                        // Direct match with Prompt, Command, Snippet, Note
                        filtered = filtered.Where(p => p.ItemType.Equals(options.SelectedTypeFilter, StringComparison.OrdinalIgnoreCase));
                    }
                }

                // 3. Tag Filter
                if (!string.IsNullOrWhiteSpace(options.SelectedTagFilter))
                {
                    filtered = filtered.Where(p => p.Tags.Any(t => t.Equals(options.SelectedTagFilter, StringComparison.OrdinalIgnoreCase)));
                }

                // 4. Multi-level Sorting
                var sorted = filtered
                    .OrderByDescending(p => p.IsPinned)
                    .ThenByDescending(p => p.IsFavorite)
                    .ThenByDescending(p => p.LastUsedAt ?? DateTime.MinValue)
                    .ThenByDescending(p => p.UpdatedAt)
                    .ThenByDescending(p => p.CreatedAt)
                    .ToList();

                IReadOnlyList<PromptItem> readOnly = sorted.AsReadOnly();
                return readOnly;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task AddAsync(PromptItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            await _semaphore.WaitAsync();
            try
            {
                item.CreatedAt = DateTime.UtcNow;
                item.UpdatedAt = DateTime.UtcNow;
                _prompts.Add(item);
                await _promptStore.SaveAsync(_prompts);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateAsync(PromptItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            await _semaphore.WaitAsync();
            try
            {
                var index = _prompts.FindIndex(p => p.Id == item.Id);
                if (index >= 0)
                {
                    item.UpdatedAt = DateTime.UtcNow;
                    _prompts[index] = item;
                    await _promptStore.SaveAsync(_prompts);
                }
                else
                {
                    throw new InvalidOperationException($"Prompt with Id {item.Id} was not found.");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            await _semaphore.WaitAsync();
            try
            {
                var index = _prompts.FindIndex(p => p.Id == id);
                if (index >= 0)
                {
                    _prompts.RemoveAt(index);
                    await _promptStore.SaveAsync(_prompts);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ImportRangeAsync(IEnumerable<PromptItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            await _semaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                foreach (var item in items)
                {
                    if (item.CreatedAt == default)
                    {
                        item.CreatedAt = now;
                    }
                    if (item.UpdatedAt == default)
                    {
                        item.UpdatedAt = now;
                    }
                    _prompts.Add(item);
                }
                await _promptStore.SaveAsync(_prompts);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task MarkAsUsedAsync(Guid id)
        {
            await _semaphore.WaitAsync();
            try
            {
                var index = _prompts.FindIndex(p => p.Id == id);
                if (index >= 0)
                {
                    _prompts[index].UsageCount++;
                    _prompts[index].LastUsedAt = DateTime.UtcNow;
                    await _promptStore.SaveAsync(_prompts);
                }
                else
                {
                    throw new InvalidOperationException($"Prompt with Id {id} was not found.");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ToggleFavoriteAsync(Guid id)
        {
            await _semaphore.WaitAsync();
            try
            {
                var index = _prompts.FindIndex(p => p.Id == id);
                if (index >= 0)
                {
                    _prompts[index].IsFavorite = !_prompts[index].IsFavorite;
                    await _promptStore.SaveAsync(_prompts);
                }
                else
                {
                    throw new InvalidOperationException($"Prompt with Id {id} was not found.");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task TogglePinnedAsync(Guid id)
        {
            await _semaphore.WaitAsync();
            try
            {
                var index = _prompts.FindIndex(p => p.Id == id);
                if (index >= 0)
                {
                    _prompts[index].IsPinned = !_prompts[index].IsPinned;
                    await _promptStore.SaveAsync(_prompts);
                    _logger?.LogInformation("Toggle pinned: Id={Id}, IsPinned={IsPinned}", id, _prompts[index].IsPinned);
                }
                else
                {
                    throw new InvalidOperationException($"Prompt with Id {id} was not found.");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
