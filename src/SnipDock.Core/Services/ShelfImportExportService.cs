using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SnipDock.Core.Models;

namespace SnipDock.Core.Services
{
    public class ShelfImportExportService
    {
        private readonly PromptService _promptService;
        private readonly BackupService _backupService;
        private static readonly HashSet<string> ValidItemTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Prompt", "Command", "Snippet", "Note"
        };

        public ShelfImportExportService(PromptService promptService, BackupService backupService)
        {
            _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        }

        public async Task ExportAllAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Export path cannot be empty.", nameof(filePath));

            var allItems = await _promptService.GetAllAsync();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(allItems, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<ImportResult> ImportAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Import path cannot be empty.", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Import file not found.", filePath);

            // 1. 导入前自动备份（安全保障）
            await _backupService.BackupBeforeImportAsync();

            var json = await File.ReadAllTextAsync(filePath);
            var items = JsonSerializer.Deserialize<List<PromptItem>>(json);

            var result = new ImportResult();
            if (items == null || items.Count == 0) return result;

            var allItems = await _promptService.GetAllAsync();
            var existingIds = new HashSet<Guid>(allItems.Select(p => p.Id));
            var existingTitles = new HashSet<string>(
                allItems.Select(p => DuplicateDetectionService.NormalizeTitle(p.Name)),
                StringComparer.OrdinalIgnoreCase);
            var existingContentHashes = new HashSet<string>(
                allItems.Where(p => !string.IsNullOrWhiteSpace(p.Content)).Select(p => DuplicateDetectionService.ComputeContentHash(p.Content)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                // 规则 1：ID 重复校验 - 已存在则跳过
                if (existingIds.Contains(item.Id))
                {
                    result.SkippedCount++;
                    continue;
                }

                // 规则 5：名称校验 - 为空时命名为"未命名条目"
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    item.Name = "未命名条目";
                }
                else
                {
                    item.Name = item.Name.Trim();
                }

                // 规则 2：ItemType 校验 - 为空或非法时默认 "Prompt"
                if (string.IsNullOrWhiteSpace(item.ItemType) || !ValidItemTypes.Contains(item.ItemType.Trim()))
                {
                    item.ItemType = "Prompt";
                }
                else
                {
                    item.ItemType = item.ItemType.Trim();
                }

                // 规则 4：内容为 null 时默认为空字符串
                if (item.Content == null)
                {
                    item.Content = string.Empty;
                }

                var normalizedTitle = DuplicateDetectionService.NormalizeTitle(item.Name);
                var contentHash = string.IsNullOrWhiteSpace(item.Content)
                    ? string.Empty
                    : DuplicateDetectionService.ComputeContentHash(item.Content);
                if (existingTitles.Contains(normalizedTitle) ||
                    (!string.IsNullOrEmpty(contentHash) && existingContentHashes.Contains(contentHash)))
                {
                    result.SkippedCount++;
                    continue;
                }

                // 规则 3：标签去重和清洗
                if (item.Tags != null)
                {
                    item.Tags = item.Tags
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                else
                {
                    item.Tags = new List<string>();
                }

                // 规则 6：使用次数校验 - 小于 0 时重置为 0
                if (item.UsageCount < 0)
                {
                    item.UsageCount = 0;
                }

                // 合并或新增
                var existing = allItems.FirstOrDefault(p =>
                    p.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase) &&
                    p.ItemType.Equals(item.ItemType, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    // 内容完全相同则跳过
                    if (existing.Content == item.Content)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // 否则更新内容并合并标签
                    existing.Content = item.Content;
                    if (item.Tags != null)
                    {
                        existing.Tags = existing.Tags
                            .Concat(item.Tags)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }
                    await _promptService.UpdateAsync(existing);
                    result.ImportedCount++;
                }
                else
                {
                    await _promptService.AddAsync(item);
                    existingTitles.Add(normalizedTitle);
                    if (!string.IsNullOrEmpty(contentHash))
                    {
                        existingContentHashes.Add(contentHash);
                    }
                    result.ImportedCount++;
                }
            }

            return result;
        }
    }
}
