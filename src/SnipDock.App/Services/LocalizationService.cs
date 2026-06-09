using System;
using System.Collections.Generic;
using System.Globalization;

namespace SnipDock.App.Services
{
    public sealed class LocalizationService
    {
        private static readonly IReadOnlyDictionary<string, string> ZhCn = new Dictionary<string, string>
        {
            ["WindowTitle"] = "SnipDock - 管理面板",
            ["PanelTitle"] = "SnipDock - 片段与命令管理面板",
            ["SettingsTooltip"] = "外观与系统设置",
            ["TypeFilterLabel"] = "条目类型",
            ["TagFilterLabel"] = "标签筛选",
            ["AddItem"] = "+ 新建条目",
            ["AddFromClipboard"] = "📋 从剪贴板新建",
            ["EmptyStateTitle"] = "请从左侧列表选择一个条目查看详情",
            ["EmptyStateHint"] = "点击「+」新建条目，或使用 Ctrl+V 从剪贴板创建",
            ["ItemName"] = "条目名称",
            ["ItemType"] = "条目类型",
            ["Tags"] = "标签",
            ["ItemContent"] = "条目内容",
            ["Cancel"] = "取消",
            ["Browse"] = "浏览...",
            ["Close"] = "关闭",
            ["Save"] = "保存",
            ["SettingsTitle"] = "外观与系统设置",
            ["GeneralSettings"] = "常规设置",
            ["TagManagement"] = "标签管理",
            ["About"] = "关于",
            ["Language"] = "界面语言",
            ["Chinese"] = "中文",
            ["English"] = "English",
            ["Theme"] = "系统主题",
            ["DarkMode"] = "深色模式 (Dark)",
            ["LightMode"] = "浅色模式 (Light)",
            ["AccentColor"] = "应用配色",
            ["Behavior"] = "日常行为习惯",
            ["FocusSearch"] = "打开面板时自动聚焦搜索框",
            ["SelectSearch"] = "打开面板时选中搜索文本",
            ["HideAfterCopy"] = "复制后自动隐藏面板",
            ["HideAfterCopyHint"] = "复制内容后自动收起管理面板，程序仍会在后台运行。",
            ["ClearSearchAfterCopy"] = "一键复制后清空搜索条件",
            ["StartupSection"] = "常驻后台与自启动",
            ["StartupEnabled"] = "开机自动启动 SnipDock",
            ["StartupHint"] = "登录 Windows 时自动启动 SnipDock，无需手动打开。",
            ["StartupDevHint"] = "* 开发调试模式下（包括 bin\\Debug 或 dotnet run）不建议开启自启动。",
            ["DataManagement"] = "数据管理与备份",
            ["OpenDataDir"] = "打开数据目录",
            ["OpenLogsDir"] = "打开日志目录",
            ["OpenBackupsDir"] = "打开备份目录",
            ["OpenExportDir"] = "打开导出目录",
            ["ChangeStorageDir"] = "修改存储目录...",
            ["ImportExport"] = "导入与导出数据",
            ["ExportJson"] = "导出全部条目到 JSON 文件…",
            ["ImportJson"] = "从 JSON 文件导入条目…",
            ["RestoreBackup"] = "从自动备份中恢复...",
            ["CreateBackup"] = "立即创建手动备份",
            ["TagPanel"] = "标签操作面板",
            ["SelectedTag"] = "当前选中的标签：",
            ["TargetTag"] = "选中标签 / 输入新名称：",
            ["RenameTag"] = "重命名选中标签…",
            ["MergeTag"] = "确认合并到该标签",
            ["CleanTags"] = "清理无用标签…",
            ["UnusedTags"] = "孤立标签清理",
            ["AboutSnipDock"] = "关于 SnipDock",
            ["AppName"] = "应用名称：",
            ["Version"] = "版本号：",
            ["DataVersion"] = "数据版本：",
            ["Stats"] = "统计信息：",
            ["TotalItems"] = "总条目数: ",
            ["TotalTags"] = "总标签数: ",
            ["SystemPaths"] = "系统路径目录",
            ["StoragePath"] = "数据存储路径:",
            ["LogsPath"] = "日志目录路径:",
            ["BackupsPath"] = "自动备份路径:",
            ["Copy"] = "复制",
            ["CopyContent"] = "复制内容到剪贴板",
            ["Edit"] = "编辑",
            ["Delete"] = "删除",
            ["PinnedTooltip"] = "置顶 / 取消置顶",
            ["FavoriteTooltip"] = "收藏 / 取消收藏",
            ["CreatedAt"] = "创建于 ",
            ["UsageCount"] = "使用次数: ",
            ["LastUsed"] = " 最近使用 ",
            ["NeverUsed"] = "未使用",
            ["All"] = "全部",
            ["AllTags"] = "全部标签",
            ["Command"] = "命令",
            ["Snippet"] = "代码片段",
            ["Note"] = "笔记",
            ["Favorites"] = "收藏",
            ["RecentlyUsed"] = "最近使用",
            ["NewItemTitle"] = "新建条目",
            ["EditItemTitle"] = "编辑条目",
            ["ClipboardItemTitle"] = "添加新内容（来自剪贴板）",
            ["Saved"] = "已保存",
            ["PathCopied"] = "路径已复制到剪贴板！",
            ["OpenedBackups"] = "已打开备份目录",
            ["OpenedData"] = "已打开数据目录",
            ["BackupRestored"] = "备份已成功恢复！",
            ["BackupCreated"] = "手动备份创建成功",
            ["StartupEnabledToast"] = "已开启开机自动启动！",
            ["StartupDisabledToast"] = "已关闭开机自动启动。",
            ["TagRenamed"] = "标签重命名成功！",
            ["TagMerged"] = "标签合并成功。"
        };

        private static readonly IReadOnlyDictionary<string, string> EnUs = new Dictionary<string, string>
        {
            ["WindowTitle"] = "SnipDock - Manager",
            ["PanelTitle"] = "SnipDock - Snippets and Commands",
            ["SettingsTooltip"] = "Appearance and system settings",
            ["TypeFilterLabel"] = "Item type",
            ["TagFilterLabel"] = "Tag filter",
            ["AddItem"] = "+ New item",
            ["AddFromClipboard"] = "📋 New from clipboard",
            ["EmptyStateTitle"] = "Select an item on the left to view details",
            ["EmptyStateHint"] = "Click + to create an item, or use Ctrl+V from the clipboard",
            ["ItemName"] = "Item name",
            ["ItemType"] = "Item type",
            ["Tags"] = "Tags",
            ["ItemContent"] = "Content",
            ["Cancel"] = "Cancel",
            ["Browse"] = "Browse...",
            ["Close"] = "Close",
            ["Save"] = "Save",
            ["SettingsTitle"] = "Appearance and system settings",
            ["GeneralSettings"] = "General",
            ["TagManagement"] = "Tags",
            ["About"] = "About",
            ["Language"] = "Language",
            ["Chinese"] = "中文",
            ["English"] = "English",
            ["Theme"] = "Theme",
            ["DarkMode"] = "Dark",
            ["LightMode"] = "Light",
            ["AccentColor"] = "Accent color",
            ["Behavior"] = "Behavior",
            ["FocusSearch"] = "Focus search when opening the panel",
            ["SelectSearch"] = "Select search text when opening the panel",
            ["HideAfterCopy"] = "Hide panel after copy",
            ["HideAfterCopyHint"] = "Collapse the panel after copying. SnipDock keeps running in the background.",
            ["ClearSearchAfterCopy"] = "Clear search after copy",
            ["StartupSection"] = "Background and startup",
            ["StartupEnabled"] = "Start SnipDock with Windows",
            ["StartupHint"] = "Start SnipDock automatically when signing in to Windows.",
            ["StartupDevHint"] = "* Startup is not recommended in debug mode, bin\\Debug, or dotnet run.",
            ["DataManagement"] = "Data and backups",
            ["OpenDataDir"] = "Open data folder",
            ["OpenLogsDir"] = "Open logs folder",
            ["OpenBackupsDir"] = "Open backups folder",
            ["OpenExportDir"] = "Open export folder",
            ["ChangeStorageDir"] = "Change storage folder...",
            ["ImportExport"] = "Import and export",
            ["ExportJson"] = "Export all items to JSON...",
            ["ImportJson"] = "Import items from JSON...",
            ["RestoreBackup"] = "Restore from backup...",
            ["CreateBackup"] = "Create manual backup",
            ["TagPanel"] = "Tag operations",
            ["SelectedTag"] = "Selected tag:",
            ["TargetTag"] = "Selected tag / new name:",
            ["RenameTag"] = "Rename selected tag...",
            ["MergeTag"] = "Merge into this tag",
            ["CleanTags"] = "Clean unused tags...",
            ["UnusedTags"] = "Unused tag cleanup",
            ["AboutSnipDock"] = "About SnipDock",
            ["AppName"] = "App name:",
            ["Version"] = "Version:",
            ["DataVersion"] = "Data version:",
            ["Stats"] = "Stats:",
            ["TotalItems"] = "Total items: ",
            ["TotalTags"] = "Total tags: ",
            ["SystemPaths"] = "System paths",
            ["StoragePath"] = "Data storage path:",
            ["LogsPath"] = "Logs path:",
            ["BackupsPath"] = "Backups path:",
            ["Copy"] = "Copy",
            ["CopyContent"] = "Copy content to clipboard",
            ["Edit"] = "Edit",
            ["Delete"] = "Delete",
            ["PinnedTooltip"] = "Pin / unpin",
            ["FavoriteTooltip"] = "Favorite / unfavorite",
            ["CreatedAt"] = "Created ",
            ["UsageCount"] = "Used: ",
            ["LastUsed"] = " Last used ",
            ["NeverUsed"] = "Never",
            ["All"] = "All",
            ["AllTags"] = "All tags",
            ["Command"] = "Command",
            ["Snippet"] = "Code snippet",
            ["Note"] = "Note",
            ["Favorites"] = "Favorites",
            ["RecentlyUsed"] = "Recently used",
            ["NewItemTitle"] = "New item",
            ["EditItemTitle"] = "Edit item",
            ["ClipboardItemTitle"] = "Add content from clipboard",
            ["Saved"] = "Saved",
            ["PathCopied"] = "Path copied to clipboard.",
            ["OpenedBackups"] = "Backups folder opened.",
            ["OpenedData"] = "Data folder opened.",
            ["BackupRestored"] = "Backup restored.",
            ["BackupCreated"] = "Manual backup created.",
            ["StartupEnabledToast"] = "Startup launch enabled.",
            ["StartupDisabledToast"] = "Startup launch disabled.",
            ["TagRenamed"] = "Tag renamed.",
            ["TagMerged"] = "Tags merged."
        };

        public static string DetectDefaultLanguage()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
                ? "zh-CN"
                : "en-US";
        }

        public LocalizedStrings CreateStrings(string? language)
        {
            return new LocalizedStrings(IsChinese(language) ? ZhCn : EnUs);
        }

        public static bool IsChinese(string? language)
        {
            return string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeLanguage(string? language)
        {
            if (string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase)) return "en-US";
            if (string.Equals(language, "zh-CN", StringComparison.OrdinalIgnoreCase)) return "zh-CN";
            return DetectDefaultLanguage();
        }
    }

    public sealed class LocalizedStrings
    {
        private readonly IReadOnlyDictionary<string, string> _strings;

        public LocalizedStrings(IReadOnlyDictionary<string, string> strings)
        {
            _strings = strings;
        }

        public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;
    }
}
