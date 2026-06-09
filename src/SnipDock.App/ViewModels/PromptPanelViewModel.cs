using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using SnipDock.Core.Services;
using SnipDock.Core.Utils;
using SnipDock.Infrastructure.Storage;
using SnipDock.App.Services;
using SnipDock.App.Models;
using System.IO;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace SnipDock.App.ViewModels
{
    public class PromptPanelViewModel : ViewModelBase
    {
        private readonly PromptService _promptService;
        private readonly ThemeService _themeService;
        private readonly IAppSettingsStore _appSettingsStore;

        private readonly ShelfImportExportService _importExportService;
        private readonly BackupService _backupService;
        private readonly IStartupLaunchService _startupLaunchService;
        private readonly TagManagementService _tagManagementService;

        private string _searchText = string.Empty;
        private PromptItem? _selectedPrompt;
        private ObservableCollection<PromptItem> _prompts = new();

        // Phase 3 Filtering properties
        private string _selectedTypeFilter = "All";
        private string _selectedTagFilter = "全部标签";
        private ObservableCollection<string> _dynamicTags = new();

        // Phase 4 Behavioral backing fields
        private bool _focusSearchOnOpen = true;
        private bool _selectSearchTextOnOpen = true;
        private bool _hidePanelAfterCopy = false;
        private bool _clearSearchAfterCopy = false;
        private string _lastExportedDir = string.Empty;

        // Editor properties
        private bool _isEditing;
        private Guid? _editingPromptId;
        private string _editingName = string.Empty;
        private string _editingTagsText = string.Empty;
        private string _editingContent = string.Empty;
        private string _editingItemType = "Prompt";
        private string _editorTitle = string.Empty;
        private string _editorError = string.Empty;

        // Settings and Toast properties
        private bool _isSettingsOpen;
        private string _selectedTheme = "Dark";
        private string _selectedAccentColor = "Purple";
        private bool _isToastVisible;
        private string _toastMessage = string.Empty;
        private bool _isToastSuccess = true;
        private int _toastId = 0;

        public event EventHandler? HidePanelRequested;

        public PromptPanelViewModel(
            PromptService promptService, 
            ThemeService themeService, 
            IAppSettingsStore appSettingsStore,
            ShelfImportExportService importExportService,
            BackupService backupService,
            IStartupLaunchService startupLaunchService,
            TagManagementService tagManagementService)
        {
            _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _appSettingsStore = appSettingsStore ?? throw new ArgumentNullException(nameof(appSettingsStore));
            _importExportService = importExportService ?? throw new ArgumentNullException(nameof(importExportService));
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _startupLaunchService = startupLaunchService ?? throw new ArgumentNullException(nameof(startupLaunchService));
            _tagManagementService = tagManagementService ?? throw new ArgumentNullException(nameof(tagManagementService));

            SearchCommand = new RelayCommand(async () => await ExecuteSearchAsync());
            AddCommand = new RelayCommand(OnStartAdd);
            EditCommand = new RelayCommand<PromptItem>(OnStartEdit, p => p != null);
            DeleteCommand = new RelayCommand<PromptItem>(async p => await OnDeleteAsync(p), p => p != null);
            SavePromptCommand = new RelayCommand(async () => await OnSavePromptAsync(), CanSavePrompt);
            CancelEditCommand = new RelayCommand(OnCancelEdit);
            CopyContentCommand = new RelayCommand<string>(OnCopyContent);
            ToggleSettingsCommand = new RelayCommand(OnToggleSettings);
            ChangeThemeCommand = new RelayCommand<string>(OnChangeTheme);
            ChangeAccentColorCommand = new RelayCommand<string>(OnChangeAccentColor);
            ChangeStoragePathCommand = new RelayCommand(OnChangeStoragePath);

            // Phase 3 Commands
            ToggleFavoriteCommand = new RelayCommand(async () => await OnToggleFavoriteAsync(), () => SelectedPrompt != null);
            OpenStorageDirCommand = new RelayCommand(OnOpenStorageDir);
            OpenLogsDirCommand = new RelayCommand(OnOpenLogsDir);
            ExportDataCommand = new RelayCommand(async () => await OnExportDataAsync());
            ImportDataCommand = new RelayCommand(async () => await OnImportDataAsync());

            // Phase 4 Commands
            AddFromClipboardCommand = new RelayCommand(OnAddFromClipboard);
            OpenBackupsDirCommand = new RelayCommand(OnOpenBackupsDir);
            OpenExportDirCommand = new RelayCommand(OnOpenExportDir);
            RestoreBackupCommand = new RelayCommand(async () => await OnRestoreBackupAsync());
            CreateManualBackupCommand = new RelayCommand(async () => await OnCreateManualBackupAsync());

            // Phase 5 Commands
            TogglePinnedCommand = new RelayCommand(async () => await OnTogglePinnedAsync(), () => SelectedPrompt != null);
            RenameTagCommand = new RelayCommand(async () => await OnRenameTagAsync(), () => IsTagSelected && !string.IsNullOrWhiteSpace(TargetTagName));
            MergeTagsCommand = new RelayCommand(async () => await OnMergeTagsAsync(), () => IsTagSelected && !string.IsNullOrWhiteSpace(TargetTagName));
            CleanUnusedTagsCommand = new RelayCommand(OnCleanUnusedTags);
            CopyPathCommand = new RelayCommand<string>(path => {
                if (!string.IsNullOrEmpty(path)) {
                    try {
                        System.Windows.Clipboard.SetText(path);
                        ShowSuccessToast("路径已复制到剪贴板！");
                    } catch (Exception ex) {
                        Serilog.Log.Error(ex, "Failed to copy path to clipboard");
                    }
                }
            });
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _ = ExecuteSearchAsync();
                }
            }
        }

        // Phase 3 Filter & Sorting Properties
        public IReadOnlyList<TypeFilterItem> TypeFilters { get; } = new List<TypeFilterItem>
        {
            new() { Key = "All", DisplayName = "全部" },
            new() { Key = "Prompt", DisplayName = "Prompt" },
            new() { Key = "Command", DisplayName = "命令" },
            new() { Key = "Snippet", DisplayName = "代码片段" },
            new() { Key = "Note", DisplayName = "笔记" },
            new() { Key = "Favorites", DisplayName = "收藏" },
            new() { Key = "RecentlyUsed", DisplayName = "最近使用" },
        };

        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                if (SetProperty(ref _selectedTypeFilter, value))
                {
                    _ = ExecuteSearchAsync();
                    Serilog.Log.Information("条目类型筛选切换为: {Type}", value);
                }
            }
        }

        public string SelectedTagFilter
        {
            get => _selectedTagFilter;
            set
            {
                if (SetProperty(ref _selectedTagFilter, value))
                {
                    _ = ExecuteSearchAsync();
                    Serilog.Log.Information("标签筛选切换为: {Tag}", value);
                }
            }
        }

        public ObservableCollection<string> DynamicTags
        {
            get => _dynamicTags;
            set => SetProperty(ref _dynamicTags, value);
        }

        public string EditingItemType
        {
            get => _editingItemType;
            set => SetProperty(ref _editingItemType, value);
        }

        public PromptItem? SelectedPrompt
        {
            get => _selectedPrompt;
            set
            {
                if (SetProperty(ref _selectedPrompt, value))
                {
                    if (value != null)
                    {
                        // Auto-close settings and editor when a prompt is selected to prevent blank screens
                        _isSettingsOpen = false;
                        _isEditing = false;
                        OnPropertyChanged(nameof(IsSettingsOpen));
                        OnPropertyChanged(nameof(IsEditing));
                        Serilog.Log.Information("选中 Prompt 切换�? Id={Id}, Name={Name}", value.Id, value.Name);
                    }
                    else
                    {
                        Serilog.Log.Information("用户取消了备份恢复");
                    }
                    NotifyStateProperties();
                }
            }
        }

        public ObservableCollection<PromptItem> Prompts
        {
            get => _prompts;
            set => SetProperty(ref _prompts, value);
        }

        // Editor bindings
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    Serilog.Log.Information("Edit state changed: IsEditing = {State}", value);
                    if (value)
                    {
                        Serilog.Log.Information("打开编辑面板 (IsEditing = True)");
                    }
                    else
                    {
                        Serilog.Log.Information("关闭编辑面板 (IsEditing = False)");
                    }
                    NotifyStateProperties();
                }
            }
        }

        public string EditingName
        {
            get => _editingName;
            set => SetProperty(ref _editingName, value);
        }

        public string EditingTagsText
        {
            get => _editingTagsText;
            set => SetProperty(ref _editingTagsText, value);
        }

        public string EditingContent
        {
            get => _editingContent;
            set => SetProperty(ref _editingContent, value);
        }

        public string EditorTitle
        {
            get => _editorTitle;
            set => SetProperty(ref _editorTitle, value);
        }

        public string EditorError
        {
            get => _editorError;
            set => SetProperty(ref _editorError, value);
        }

        // Settings and Toast bindings
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set
            {
                if (SetProperty(ref _isSettingsOpen, value))
                {
                    if (value)
                    {
                        Serilog.Log.Information("打开外观与系统设置面�?(IsSettingsOpen = True)");
                    }
                    else
                    {
                        Serilog.Log.Information("关闭外观与系统设置面�?(IsSettingsOpen = False)");
                    }
                    NotifyStateProperties();
                }
            }
        }

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value))
                {
                    OnChangeTheme(value);
                }
            }
        }

        public string SelectedAccentColor
        {
            get => _selectedAccentColor;
            set
            {
                if (SetProperty(ref _selectedAccentColor, value))
                {
                    OnChangeAccentColor(value);
                }
            }
        }

        // Clear State properties for decoupled WPF Visibility bindings (highest reliability)
        public bool IsSettingsPanelVisible
        {
            get => IsSettingsOpen && !IsEditing;
        }

        public bool HasSelectedPrompt
        {
            get => SelectedPrompt != null;
        }

        public bool IsEditorVisible
        {
            get => IsEditing;
        }

        public bool IsEmptyStateVisible
        {
            get => SelectedPrompt == null && !IsEditing && !IsSettingsOpen;
        }

        public bool IsDetailVisible
        {
            get => SelectedPrompt != null && !IsEditing && !IsSettingsOpen;
        }

        private void NotifyStateProperties()
        {
            OnPropertyChanged(nameof(IsSettingsPanelVisible));
            OnPropertyChanged(nameof(HasSelectedPrompt));
            OnPropertyChanged(nameof(IsEditorVisible));
            OnPropertyChanged(nameof(IsEmptyStateVisible));
            OnPropertyChanged(nameof(IsDetailVisible));
        }

        // Boolean mappings for view bindings (eliminates need for converters)
        public bool IsThemeDark
        {
            get => SelectedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedTheme = "Dark"; }
        }

        public bool IsThemeLight
        {
            get => SelectedTheme.Equals("Light", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedTheme = "Light"; }
        }

        public bool IsAccentPurple
        {
            get => SelectedAccentColor.Equals("Purple", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedAccentColor = "Purple"; }
        }

        public bool IsAccentBlue
        {
            get => SelectedAccentColor.Equals("Blue", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedAccentColor = "Blue"; }
        }

        public bool IsAccentGreen
        {
            get => SelectedAccentColor.Equals("Green", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedAccentColor = "Green"; }
        }

        public bool IsAccentOrange
        {
            get => SelectedAccentColor.Equals("Orange", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedAccentColor = "Orange"; }
        }

        public bool IsAccentPink
        {
            get => SelectedAccentColor.Equals("Pink", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedAccentColor = "Pink"; }
        }

        public string CurrentStoragePath
        {
            get
            {
                try
                {
                    var bootstrapStore = new LocalBootstrapSettingsStore();
                    return bootstrapStore.Load().StoragePath;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public bool IsToastVisible
        {
            get => _isToastVisible;
            set => SetProperty(ref _isToastVisible, value);
        }

        public string ToastMessage
        {
            get => _toastMessage;
            set => SetProperty(ref _toastMessage, value);
        }

        public bool IsToastSuccess
        {
            get => _isToastSuccess;
            set => SetProperty(ref _isToastSuccess, value);
        }

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SavePromptCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand CopyContentCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand ChangeThemeCommand { get; }
        public ICommand ChangeAccentColorCommand { get; }
        public ICommand ChangeStoragePathCommand { get; }

        // Phase 3 Commands
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand OpenStorageDirCommand { get; }
        public ICommand OpenLogsDirCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand ImportDataCommand { get; }

        // Phase 5 Commands
        public ICommand TogglePinnedCommand { get; }
        public ICommand RenameTagCommand { get; }
        public ICommand MergeTagsCommand { get; }
        public ICommand CleanUnusedTagsCommand { get; }
        public ICommand CopyPathCommand { get; }

        // Phase 5 Properties
        private int _dataSchemaVersion = 1;
        public int DataSchemaVersion
        {
            get => _dataSchemaVersion;
            set => SetProperty(ref _dataSchemaVersion, value);
        }

        private bool _isStartupEnabled;
        public bool IsStartupEnabled
        {
            get => _isStartupEnabled;
            set
            {
                if (SetProperty(ref _isStartupEnabled, value))
                {
                    _ = ApplyStartupSettingsAsync(value);
                }
            }
        }

        private ObservableCollection<TagSummary> _tagSummaries = new();
        public ObservableCollection<TagSummary> TagSummaries
        {
            get => _tagSummaries;
            set => SetProperty(ref _tagSummaries, value);
        }

        private TagSummary? _selectedTagSummary;
        public TagSummary? SelectedTagSummary
        {
            get => _selectedTagSummary;
            set
            {
                if (SetProperty(ref _selectedTagSummary, value))
                {
                    OnPropertyChanged(nameof(IsTagSelected));
                    if (value != null)
                    {
                        TargetTagName = value.TagName;
                    }
                }
            }
        }

        public bool IsTagSelected => SelectedTagSummary != null;

        private string _targetTagName = string.Empty;
        public string TargetTagName
        {
            get => _targetTagName;
            set => SetProperty(ref _targetTagName, value);
        }

        public string AppVersion
        {
            get
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var attribute = Attribute.GetCustomAttribute(
                    assembly,
                    typeof(System.Reflection.AssemblyInformationalVersionAttribute))
                    as System.Reflection.AssemblyInformationalVersionAttribute;
                var informationalVersion = attribute?.InformationalVersion;

                return string.IsNullOrWhiteSpace(informationalVersion)
                    ? "v0.1.0-beta"
                    : $"v{informationalVersion}";
            }
        }

        public int TotalItemsCount => _promptService.GetAllAsync().GetAwaiter().GetResult().Count;

        public int TotalTagsCount => TagSummaries.Count;

        public string StorageDirectoryPath => CurrentStoragePath;

        public string LogsDirectoryPath => Path.Combine(StorageDirectoryPath, "logs");

        public string BackupsDirectoryPath => Path.Combine(StorageDirectoryPath, "backups");

        // Phase 4 Behavioral Settings Properties
        public bool FocusSearchOnOpen
        {
            get => _focusSearchOnOpen;
            set
            {
                if (SetProperty(ref _focusSearchOnOpen, value))
                {
                    SaveBehavioralSettings();
                    OnPropertyChanged(nameof(SelectSearchTextOnOpen)); // Notify dependency state
                }
            }
        }

        public bool SelectSearchTextOnOpen
        {
            get => _selectSearchTextOnOpen;
            set
            {
                if (SetProperty(ref _selectSearchTextOnOpen, value))
                {
                    SaveBehavioralSettings();
                }
            }
        }

        public bool HidePanelAfterCopy
        {
            get => _hidePanelAfterCopy;
            set
            {
                if (SetProperty(ref _hidePanelAfterCopy, value))
                {
                    Serilog.Log.Information("HidePanelAfterCopy setting changed: {Value}", value);
                    SaveBehavioralSettings();
                }
            }
        }

        public bool ClearSearchAfterCopy
        {
            get => _clearSearchAfterCopy;
            set
            {
                if (SetProperty(ref _clearSearchAfterCopy, value))
                {
                    SaveBehavioralSettings();
                }
            }
        }

        private void SaveBehavioralSettings()
        {
            try
            {
                var settings = _appSettingsStore.Load();
                settings.FocusSearchOnOpen = FocusSearchOnOpen;
                settings.SelectSearchTextOnOpen = SelectSearchTextOnOpen;
                settings.HidePanelAfterCopy = HidePanelAfterCopy;
                settings.ClearSearchAfterCopy = ClearSearchAfterCopy;
                _appSettingsStore.Save(settings);
                Serilog.Log.Information("用户行为设置已保存: FocusSearchOnOpen={Focus}, SelectSearchTextOnOpen={Select}, HidePanelAfterCopy={Hide}, ClearSearchAfterCopy={Clear}",
                    FocusSearchOnOpen, SelectSearchTextOnOpen, HidePanelAfterCopy, ClearSearchAfterCopy);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "保存用户行为设置失败");
            }
        }

        // Phase 4 Command Properties
        public ICommand AddFromClipboardCommand { get; }
        public ICommand OpenBackupsDirCommand { get; }
        public ICommand OpenExportDirCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand CreateManualBackupCommand { get; }

        public event EventHandler? ChangeStoragePathRequested;

        public async Task LoadPromptsAsync()
        {
            await _promptService.InitializeAsync();
            await UpdateDynamicTagsAsync();
            await ExecuteSearchAsync();

            // Load settings safely without triggering double saves
            var settings = _appSettingsStore.Load();
            _selectedTheme = settings.Theme;
            _selectedAccentColor = settings.AccentColor;

            // Load behavior settings safely
            _focusSearchOnOpen = settings.FocusSearchOnOpen;
            _selectSearchTextOnOpen = settings.SelectSearchTextOnOpen;
            _hidePanelAfterCopy = settings.HidePanelAfterCopy;
            _clearSearchAfterCopy = settings.ClearSearchAfterCopy;
            
            // Query registry for real autostart state, then sync to settings
            _isStartupEnabled = await _startupLaunchService.IsEnabledAsync();
            if (settings.IsStartupEnabled != _isStartupEnabled)
            {
                settings.IsStartupEnabled = _isStartupEnabled;
                _appSettingsStore.Save(settings);
            }

            _dataSchemaVersion = settings.DataSchemaVersion;

            OnPropertyChanged(nameof(FocusSearchOnOpen));
            OnPropertyChanged(nameof(SelectSearchTextOnOpen));
            OnPropertyChanged(nameof(HidePanelAfterCopy));
            OnPropertyChanged(nameof(ClearSearchAfterCopy));
            OnPropertyChanged(nameof(IsStartupEnabled));
            OnPropertyChanged(nameof(DataSchemaVersion));
            
            await LoadTagSummariesAsync();
            
            // Notify UI of updated states
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(SelectedAccentColor));
            OnPropertyChanged(nameof(IsThemeDark));
            OnPropertyChanged(nameof(IsThemeLight));
            OnPropertyChanged(nameof(IsAccentPurple));
            OnPropertyChanged(nameof(IsAccentBlue));
            OnPropertyChanged(nameof(IsAccentGreen));
            OnPropertyChanged(nameof(IsAccentOrange));
            OnPropertyChanged(nameof(IsAccentPink));

            NotifyStateProperties();
        }

        public async Task ExecuteSearchAsync()
        {
            var previousSelectedId = SelectedPrompt?.Id;

            var options = new PromptQueryOptions
            {
                SearchText = SearchText,
                SelectedTypeFilter = SelectedTypeFilter,
                SelectedTagFilter = SelectedTagFilter == "全部标签" ? null : SelectedTagFilter
            };

            var sorted = await _promptService.QueryAsync(options);
            Prompts = new ObservableCollection<PromptItem>(sorted);

            if (previousSelectedId.HasValue)
            {
                var newSelected = sorted.FirstOrDefault(p => p.Id == previousSelectedId.Value);
                SelectedPrompt = newSelected;
            }
            else
            {
                SelectedPrompt = null;
            }
        }

        private async Task RefreshListAsync()
        {
            await ExecuteSearchAsync();
        }

        public async Task UpdateDynamicTagsAsync()
        {
            try
            {
                var all = await _promptService.GetAllAsync();
                var tags = all
                    .SelectMany(p => p.Tags)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .GroupBy(t => t.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First()) // Keep the first casing encountered
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                tags.Insert(0, "全部标签");
                
                string currentFilter = SelectedTagFilter;
                DynamicTags = new ObservableCollection<string>(tags);
                
                if (tags.Contains(currentFilter, StringComparer.OrdinalIgnoreCase))
                {
                    _selectedTagFilter = currentFilter;
                }
                else
                {
                    _selectedTagFilter = "全部标签";
                }
                OnPropertyChanged(nameof(SelectedTagFilter));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "更新动态标签失败");
            }
        }

        private void OnStartAdd()
        {
            Serilog.Log.Information("New item started (触发新建条目命令)");
            EditorTitle = "新建条目";
            _editingPromptId = null;
            EditingName = string.Empty;
            EditingTagsText = string.Empty;
            EditingContent = string.Empty;
            EditingItemType = "Prompt";
            EditorError = string.Empty;
            
            IsSettingsOpen = false;
            IsEditing = true;
        }

        private void OnStartEdit(PromptItem prompt)
        {
            Serilog.Log.Information("Edit started: id={Id}, name={Name} (触发编辑条目命令)", prompt.Id, prompt.Name);
            EditorTitle = "编辑条目";
            _editingPromptId = prompt.Id;
            EditingName = prompt.Name;
            EditingTagsText = string.Join(", ", prompt.Tags); // Separation format display for editing
            EditingContent = prompt.Content;
            EditingItemType = prompt.ItemType;
            EditorError = string.Empty;
            
            IsSettingsOpen = false;
            IsEditing = true;
        }

        private async Task OnDeleteAsync(PromptItem prompt)
        {
            // Try to find the active panel window as Owner for centering
            Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
            if (owner == null)
            {
                owner = Application.Current.MainWindow;
            }

            var result = Views.ConfirmDialogWindow.Show(owner!, "删除确认", $"确定要删除条目 \u201c{prompt.Name}\u201d 吗？");
            if (result)
            {
                await _promptService.DeleteAsync(prompt.Id);
                Serilog.Log.Information("条目 \u201c{Name}\u201d 已删除", prompt.Name);
                await ExecuteSearchAsync();
                if (SelectedPrompt?.Id == prompt.Id)
                {
                    SelectedPrompt = null;
                }
            }
        }

        private bool CanSavePrompt()
        {
            return !string.IsNullOrWhiteSpace(EditingName) && !string.IsNullOrWhiteSpace(EditingContent);
        }

        public async Task OnSavePromptAsync()
        {
            if (string.IsNullOrWhiteSpace(EditingName))
            {
                EditorError = "名称不能为空";
                return;
            }
            if (string.IsNullOrWhiteSpace(EditingContent))
            {
                EditorError = "内容不能为空";
                return;
            }

            try
            {
                var tags = TagParser.Parse(EditingTagsText);

                if (_editingPromptId == null)
                {
                    Serilog.Log.Information("Save new item requested");
                    var newItem = new PromptItem
                    {
                        Name = EditingName.Trim(),
                        Tags = tags,
                        Content = EditingContent,
                        ItemType = EditingItemType
                    };
                    Serilog.Log.Information("保存新创建的条目: Name={Name}, Type={Type}", newItem.Name, newItem.ItemType);
                    await _promptService.AddAsync(newItem);
                    
                    Serilog.Log.Information("Save new item completed");
                    IsEditing = false;
                    
                    await UpdateDynamicTagsAsync();
                    await ExecuteSearchAsync();
                    // Automatically highlight and select the newly added item
                    SelectedPrompt = Prompts.FirstOrDefault(p => p.Id == newItem.Id);
                    Serilog.Log.Information("Selected item restored after save: Id={Id}", newItem.Id);
                    ShowSuccessToast("已保存");
                }
                else
                {
                    Serilog.Log.Information("Save edit requested: Id={Id}", _editingPromptId.Value);
                    
                    var all = await _promptService.GetAllAsync();
                    var existing = all.FirstOrDefault(p => p.Id == _editingPromptId.Value);
                    if (existing != null)
                    {
                        existing.Name = EditingName.Trim();
                        existing.Tags = tags;
                        existing.Content = EditingContent;
                        existing.ItemType = EditingItemType;
                        Serilog.Log.Information("保存编辑的条�? Id={Id}, Name={Name}, Type={Type}", existing.Id, existing.Name, existing.ItemType);
                        await _promptService.UpdateAsync(existing);
                        
                        Serilog.Log.Information("Save edit completed: Id={Id}", existing.Id);
                        IsEditing = false;
                        
                        await UpdateDynamicTagsAsync();
                        await ExecuteSearchAsync();
                        // Automatically highlight and select the edited item
                        SelectedPrompt = Prompts.FirstOrDefault(p => p.Id == existing.Id);
                        Serilog.Log.Information("Selected item restored after save: Id={Id}", existing.Id);
                    ShowSuccessToast("已保存");
                    }
                    else
                    {
                        Serilog.Log.Warning("未找到要编辑的条目 Id={Id}", _editingPromptId.Value);
                        EditorError = "保存失败：条目已被删除或不存在";
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "保存条目失败");
                EditorError = $"保存失败：{ex.Message}";
            }
        }

        private void OnCancelEdit()
        {
            Serilog.Log.Information("Cancel edit requested");
            IsEditing = false;
            Serilog.Log.Information("Cancel edit completed");
        }

        private async void OnCopyContent(string? content)
        {
            if (string.IsNullOrEmpty(content)) return;

            try
            {
                try
                {
                    Clipboard.SetText(content);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "剪贴板写入失�?(Clipboard.SetText failed)");
                    if (Application.Current != null)
                    {
                        throw;
                    }
                }

                string localizedType = "条目";
                if (SelectedPrompt != null)
                {
                    await _promptService.MarkAsUsedAsync(SelectedPrompt.Id);
                    
                    localizedType = SelectedPrompt.ItemType switch
                    {
                        "Prompt" => "Prompt",
                        "Command" => "命令",
                        "Snippet" => "片段",
                        "Note" => "笔记",
                        _ => "条目"
                    };
                    
                    Serilog.Log.Information("复制条目 {Name} ({Type}) 到剪贴板，UsageCount={Count}", SelectedPrompt.Name, SelectedPrompt.ItemType, SelectedPrompt.UsageCount);
                }

                ToastMessage = $"已复制 {localizedType} 内容";
                IsToastSuccess = true;
                IsToastVisible = true;

                // Re-sort/re-filter the list as UsageCount and LastUsedAt changed!
                await ExecuteSearchAsync();

                // Phase 4 Post-Copy Actions
                if (ClearSearchAfterCopy)
                {
                    SearchText = string.Empty; // Will automatically trigger ExecuteSearchAsync()!
                }

                if (HidePanelAfterCopy)
                {
                    Serilog.Log.Information("Copy completed, hide panel after copy enabled");
                    await Task.Delay(500);
                    HidePanelRequested?.Invoke(this, EventArgs.Empty);
                }

                int currentId = ++_toastId;
                await Task.Delay(1500);
                if (currentId == _toastId)
                {
                    IsToastVisible = false;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "复制失败");
                ToastMessage = $"复制失败：{ex.Message}";
                IsToastSuccess = false;
                IsToastVisible = true;

                int currentId = ++_toastId;
                await Task.Delay(2500); // Show errors longer
                if (currentId == _toastId)
                {
                    IsToastVisible = false;
                }
            }
        }

        private async Task OnToggleFavoriteAsync()
        {
            if (SelectedPrompt == null) return;
            try
            {
                await _promptService.ToggleFavoriteAsync(SelectedPrompt.Id);
                
                Serilog.Log.Information("条目收藏状态变�? {Name}, IsFavorite={IsFavorite}", SelectedPrompt.Name, SelectedPrompt.IsFavorite);
                
                // Refresh list sorting
                await ExecuteSearchAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "切换收藏状态失败");
            }
        }

        private void OnOpenStorageDir()
        {
            try
            {
                string path = CurrentStoragePath;
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    ShowErrorToast("数据目录不存在！");
                    Serilog.Log.Warning("无法打开数据目录：目录不存在。Path={Path}", path);
                    return;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                Serilog.Log.Information("已成功在资源管理器中打开数据目录：{Path}", path);
            }
            catch (Exception ex)
            {
                ShowErrorToast($"无法打开数据目录：{ex.Message}");
                Serilog.Log.Error(ex, "打开数据目录失败");
            }
        }

        private void OnOpenLogsDir()
        {
            try
            {
                string path = Path.Combine(CurrentStoragePath, "logs");
                if (string.IsNullOrWhiteSpace(CurrentStoragePath) || !Directory.Exists(path))
                {
                    ShowErrorToast("日志目录不存在！");
                    Serilog.Log.Warning("无法打开日志目录：目录不存在。Path={Path}", path);
                    return;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                Serilog.Log.Information("已成功在资源管理器中打开日志目录：{Path}", path);
            }
            catch (Exception ex)
            {
                ShowErrorToast($"无法打开日志目录：{ex.Message}");
                Serilog.Log.Error(ex, "打开日志目录失败");
            }
        }

        private async Task OnExportDataAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON 文件 (*.json)|*.json",
                    FileName = $"SnipDock_Export_{DateTime.Now:yyyyMMdd}.json",
                    Title = "导出全部条目�?JSON 文件"
                };
                
                Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
                if (dialog.ShowDialog(owner) == true)
                {
                    Serilog.Log.Information("开始导出数据到文件: {FilePath}", dialog.FileName);
                    await _importExportService.ExportAllAsync(dialog.FileName);
                    _lastExportedDir = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                    var count = (await _promptService.GetAllAsync()).Count;
                    Serilog.Log.Information("成功导出 {Count} 条数据到 JSON 文件", count);
                    ShowSuccessToast($"已成功导出 {count} 条！");
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast($"导出失败：{ex.Message}");
                Serilog.Log.Error(ex, "导出数据失败");
            }
        }

        private async Task OnImportDataAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON 文件 (*.json)|*.json",
                    Title = "选择要导入的 JSON 文件"
                };
                
                Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
                if (dialog.ShowDialog(owner) == true)
                {
                    Serilog.Log.Information("开始导入文件: {FilePath}", dialog.FileName);
                    var result = await _importExportService.ImportAsync(dialog.FileName);
                    
                    Serilog.Log.Information("导入成功：新增 {Added} 条，跳过 {Skipped} 条重复", result.ImportedCount, result.SkippedCount);
                    ShowSuccessToast($"导入成功：新增 {result.ImportedCount} 条，跳过 {result.SkippedCount} 条重复");
                    
                    // Refresh views and dynamic tags
                    await UpdateDynamicTagsAsync();
                    await ExecuteSearchAsync();
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast($"导入失败：{ex.Message}");
                Serilog.Log.Error(ex, "数据导入失败");
            }
        }

        private void ShowSuccessToast(string message)
        {
            ToastMessage = message;
            IsToastSuccess = true;
            IsToastVisible = true;
            
            int currentId = ++_toastId;
            Task.Delay(1500).ContinueWith(t =>
            {
                if (currentId == _toastId) IsToastVisible = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void ShowErrorToast(string message)
        {
            ToastMessage = message;
            IsToastSuccess = false;
            IsToastVisible = true;
            
            int currentId = ++_toastId;
            Task.Delay(2500).ContinueWith(t =>
            {
                if (currentId == _toastId) IsToastVisible = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnToggleSettings()
        {
            IsSettingsOpen = !IsSettingsOpen;
            if (IsSettingsOpen)
            {
                Serilog.Log.Information("About info opened");
                _isEditing = false;
                OnPropertyChanged(nameof(IsEditing));
                NotifyStateProperties();
                _ = LoadTagSummariesAsync();
            }
        }

        private void OnChangeTheme(string? theme)
        {
            if (string.IsNullOrEmpty(theme)) return;
            var settings = _appSettingsStore.Load();
            if (settings.Theme != theme)
            {
                settings.Theme = theme;
                _appSettingsStore.Save(settings);
                Serilog.Log.Information("主题切换�? {Theme}", theme);
            }
            _themeService.ApplyTheme(theme);
            // Refresh accent color because accent background depends on theme
            _themeService.ApplyAccentColor(settings.AccentColor, theme);

            // Notify UI of boolean mappings and state dependencies
            OnPropertyChanged(nameof(IsThemeDark));
            OnPropertyChanged(nameof(IsThemeLight));
            NotifyStateProperties();
        }

        private void OnChangeAccentColor(string? colorName)
        {
            if (string.IsNullOrEmpty(colorName)) return;
            var settings = _appSettingsStore.Load();
            if (settings.AccentColor != colorName)
            {
                settings.AccentColor = colorName;
                _appSettingsStore.Save(settings);
                Serilog.Log.Information("配色切换�? {AccentColor}", colorName);
            }
            _themeService.ApplyAccentColor(colorName, settings.Theme);

            // Notify UI of boolean mappings and state dependencies
            OnPropertyChanged(nameof(IsAccentPurple));
            OnPropertyChanged(nameof(IsAccentBlue));
            OnPropertyChanged(nameof(IsAccentGreen));
            OnPropertyChanged(nameof(IsAccentOrange));
            OnPropertyChanged(nameof(IsAccentPink));
            NotifyStateProperties();
        }

        private void OnChangeStoragePath()
        {
            ChangeStoragePathRequested?.Invoke(this, EventArgs.Empty);
        }

        // Phase 4 Command Handlers
        private void OnAddFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    ShowErrorToast("剪贴板中没有可用文本");
                    Serilog.Log.Information("从剪贴板新建条目失败：剪贴板为空或不含文本");
                    return;
                }

                string text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    ShowErrorToast("剪贴板中没有可用文本");
                    Serilog.Log.Information("从剪贴板新建条目失败：剪贴板文本为空");
                    return;
                }

                Serilog.Log.Information("检测到剪贴板文本 (Clipboard text detected)");

                // Create draft using domain utility factory
                var draft = ClipboardEntryFactory.CreateDraft(text, SelectedTypeFilter);

                // Load to editor UI
                EditorTitle = "添加新内容（来自剪贴板）";
                _editingPromptId = null;
                EditingName = draft.Name;
                EditingTagsText = string.Empty;
                EditingContent = draft.Content;
                EditingItemType = draft.ItemType;
                EditorError = string.Empty;

                Serilog.Log.Information("New item started (Clipboard)");
                IsSettingsOpen = false;
                IsEditing = true;
            }
            catch (Exception ex)
            {
                ShowErrorToast("读取剪贴板失败！");
                Serilog.Log.Error(ex, "读取剪贴板失败 (Clipboard text failed)");
            }
        }

        private void OnOpenBackupsDir()
        {
            try
            {
                string path = Path.Combine(CurrentStoragePath, "backups");
                if (string.IsNullOrWhiteSpace(CurrentStoragePath) || !Directory.Exists(path))
                {
                    // Create if missing
                    Directory.CreateDirectory(path);
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                Serilog.Log.Information("已成功在资源管理器中打开备份目录：{Path}", path);
                ShowSuccessToast("已打开备份目录");
            }
            catch (Exception ex)
            {
                ShowErrorToast($"无法打开备份目录：{ex.Message}");
                Serilog.Log.Error(ex, "打开备份目录失败");
            }
        }

        private void OnOpenExportDir()
        {
            try
            {
                string path = _lastExportedDir;
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    path = CurrentStoragePath;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                Serilog.Log.Information("已成功在资源管理器中打开导出目录：{Path}", path);
                ShowSuccessToast("已打开数据目录");
            }
            catch (Exception ex)
            {
                ShowErrorToast($"无法打开导出目录：{ex.Message}");
                Serilog.Log.Error(ex, "打开导出目录失败");
            }
        }

        private async Task OnRestoreBackupAsync()
        {
            try
            {
                var backupsPath = Path.Combine(CurrentStoragePath, "backups");
                if (!Directory.Exists(backupsPath))
                {
                    Directory.CreateDirectory(backupsPath);
                }

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON 文件 (*.json)|*.json",
                    InitialDirectory = backupsPath,
                    Title = "选择要恢复的备份文件"
                };

                Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
                if (dialog.ShowDialog(owner) == true)
                {
                    bool confirm = Views.ConfirmDialogWindow.Show(owner!, "恢复确认", 
                        "恢复备份会用所选备份覆盖当前数据。在执行恢复前，系统自动创建当前数据的备份以防万一。\n\n是否继续恢复备份？");

                    if (!confirm)
                    {
                        Serilog.Log.Information("用户取消了备份恢复");
                        return;
                    }

                    Serilog.Log.Information("开始从备份文件恢复数据: {Path}", dialog.FileName);
                    await _backupService.RestoreBackupAsync(dialog.FileName);

                    // Re-initialize prompt service data
                    await _promptService.InitializeAsync();

                    ShowSuccessToast("备份已成功恢复！");

                    // Reset search and filters
                    _selectedTypeFilter = "All";
                    _selectedTagFilter = "全部标签";
                    _searchText = string.Empty;
                    OnPropertyChanged(nameof(SelectedTypeFilter));
                    OnPropertyChanged(nameof(SelectedTagFilter));
                    OnPropertyChanged(nameof(SearchText));

                    await UpdateDynamicTagsAsync();
                    await ExecuteSearchAsync();
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast($"恢复备份失败：{ex.Message}");
                Serilog.Log.Error(ex, "从备份恢复数据失败");
            }
        }

        private async Task OnCreateManualBackupAsync()
        {
            try
            {
                Serilog.Log.Information("用户手动触发了创建数据备份...");
                await _backupService.CreateBackupAsync("Manual");
                ShowSuccessToast("手动备份创建成功");
            }
            catch (Exception ex)
            {
                ShowErrorToast($"备份创建失败：{ex.Message}");
                Serilog.Log.Error(ex, "手动备份创建失败");
            }
        }

        public void ShowHotkeyRegistrationFailedToast()
        {
            ShowErrorToast("全局热键 Ctrl+Alt+P 注册失败，可能被占用。");
        }

        // Phase 5 Methods & Commands Implementation
        public async Task LoadTagSummariesAsync()
        {
            var summaries = await _tagManagementService.GetTagSummariesAsync();
            TagSummaries = new ObservableCollection<TagSummary>(summaries);
            OnPropertyChanged(nameof(TotalTagsCount));
            OnPropertyChanged(nameof(TotalItemsCount));
        }

        private async Task ApplyStartupSettingsAsync(bool enable)
        {
            try
            {
                Serilog.Log.Information("Settings section changed: IsStartupEnabled = {Value}", enable);
                if (enable)
                {
                    if (_startupLaunchService.IsDevelopmentMode())
                    {
                        Serilog.Log.Warning("检测到当前运行路径在 bin\\Debug 或 dotnet run，可能不稳定");
                        System.Windows.MessageBox.Show(
                            "检测到当前程序处于开发调试目录，开机自启功能已被禁用。建议您在发布（Publish）并运行正式版可执行文件（exe）后再开启此功能。",
                            "无法开启开机自启",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        
                        _isStartupEnabled = false;
                        OnPropertyChanged(nameof(IsStartupEnabled));
                        return;
                    }
                    await _startupLaunchService.EnableAsync();
                    ShowSuccessToast("已开启开机自动启动！");
                }
                else
                {
                    await _startupLaunchService.DisableAsync();
                    ShowSuccessToast("已关闭开机自动启动。");
                }

                var settings = _appSettingsStore.Load();
                settings.IsStartupEnabled = enable;
                _appSettingsStore.Save(settings);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Startup launch failed");
                ShowErrorToast("设置自启动失败，请检查权限。");

                _isStartupEnabled = !enable;
                OnPropertyChanged(nameof(IsStartupEnabled));
            }
        }

        private async Task OnTogglePinnedAsync()
        {
            if (SelectedPrompt == null) return;
            try
            {
                await _promptService.TogglePinnedAsync(SelectedPrompt.Id);
                
                // Refresh list sorting
                await ExecuteSearchAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "切换置顶状态失败");
            }
        }

        private async Task OnRenameTagAsync()
        {
            if (SelectedTagSummary == null || string.IsNullOrWhiteSpace(TargetTagName)) return;
            var oldTag = SelectedTagSummary.TagName;
            var newTag = TargetTagName.Trim();

            if (oldTag.Equals(newTag, StringComparison.OrdinalIgnoreCase)) return;

            var confirm = System.Windows.MessageBox.Show(
                $"确定要将标签 '{oldTag}' 重命名为 '{newTag}' 吗？将同步更新所有包含该标签的条目。",
                "确认重命名",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                Serilog.Log.Information("Settings section changed: TagName = {Value}", newTag);
                await _tagManagementService.RenameTagAsync(oldTag, newTag);

                if (SelectedTagFilter != null && SelectedTagFilter.Equals(oldTag, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedTagFilter = newTag;
                }

                await UpdateDynamicTagsAsync();
                await LoadTagSummariesAsync();
                await ExecuteSearchAsync();

                ShowSuccessToast("标签重命名成功！");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Rename tag failed");
                ShowErrorToast($"重命名失败：{ex.Message}");
            }
        }

        private async Task OnMergeTagsAsync()
        {
            if (SelectedTagSummary == null || string.IsNullOrWhiteSpace(TargetTagName)) return;
            var sourceTag = SelectedTagSummary.TagName;
            var targetTag = TargetTagName.Trim();

            if (sourceTag.Equals(targetTag, StringComparison.OrdinalIgnoreCase)) return;

            var confirm = System.Windows.MessageBox.Show(
                $"确定要将标签 '{sourceTag}' 合并到 '{targetTag}' 吗？合并后所有包含 '{sourceTag}' 的条目将更新并去除重复标签。",
                "确认合并标签",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                Serilog.Log.Information("Settings section changed: TagMergeTarget = {Value}", targetTag);
                await _tagManagementService.MergeTagsAsync(sourceTag, targetTag);

                if (SelectedTagFilter != null && SelectedTagFilter.Equals(sourceTag, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedTagFilter = targetTag;
                }

                await UpdateDynamicTagsAsync();
                await LoadTagSummariesAsync();
                await ExecuteSearchAsync();

                ShowSuccessToast("标签合并成功。");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Merge tags failed");
                ShowErrorToast($"合并失败：{ex.Message}");
            }
        }

        private void OnCleanUnusedTags()
        {
            Serilog.Log.Information("Clean unused tags action triggered");
            System.Windows.MessageBox.Show(
                "在当前版本中，标签是动态从条目内容中提取的。因为没有独立的标签数据表，所有显示的标签都已被至少一个条目所使用，因此不存在孤立的无用标签。",
                "标签清理说明",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }
}
