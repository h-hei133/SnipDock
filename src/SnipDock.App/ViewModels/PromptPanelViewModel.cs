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
        private readonly LocalizationService _localizationService = new();
        private const string AllTagsFilterKey = "__ALL_TAGS__";

        private readonly ShelfImportExportService _importExportService;
        private readonly BackupService _backupService;
        private readonly IStartupLaunchService _startupLaunchService;
        private readonly TagManagementService _tagManagementService;

        private string _searchText = string.Empty;
        private PromptItem? _selectedPrompt;
        private ObservableCollection<PromptItem> _prompts = new();

        // Phase 3 Filtering properties
        private string _selectedTypeFilter = "All";
        private string _selectedTagFilter = AllTagsFilterKey;
        private ObservableCollection<TypeFilterItem> _dynamicTags = new();

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
        private string _selectedLanguage = LocalizationService.DetectDefaultLanguage();
        private LocalizedStrings _loc;
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
            _loc = _localizationService.CreateStrings(_selectedLanguage);
            RebuildTypeFilters();

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
            ChangeLanguageCommand = new RelayCommand<string>(OnChangeLanguage);
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
                        ShowSuccessToast(Loc["PathCopied"]);
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
        public ObservableCollection<TypeFilterItem> TypeFilters { get; private set; } = new();

        public LocalizedStrings Loc
        {
            get => _loc;
            private set => SetProperty(ref _loc, value);
        }

        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set
            {
                var normalized = NormalizeTypeFilter(value);
                if (SetProperty(ref _selectedTypeFilter, normalized))
                {
                    OnPropertyChanged(nameof(SelectedTypeFilterItem));
                    _ = ExecuteSearchAsync();
                    Serilog.Log.Information("Type filter changed: {Type}", normalized);
                }
                else if (!string.Equals(value, normalized, StringComparison.Ordinal))
                {
                    NotifySelectedTypeFilterChanged();
                }
            }
        }

        public TypeFilterItem? SelectedTypeFilterItem
        {
            get => TypeFilters.FirstOrDefault(t => t.Key.Equals(_selectedTypeFilter, StringComparison.OrdinalIgnoreCase));
            set
            {
                var normalized = NormalizeTypeFilter(value?.Key);
                if (SetProperty(ref _selectedTypeFilter, normalized, nameof(SelectedTypeFilter)))
                {
                    OnPropertyChanged(nameof(SelectedTypeFilterItem));
                    _ = ExecuteSearchAsync();
                    Serilog.Log.Information("Type filter changed: {Type}", normalized);
                }
                else
                {
                    NotifySelectedTypeFilterChanged();
                }
            }
        }

        public string SelectedTagFilter
        {
            get => _selectedTagFilter;
            set
            {
                var normalized = NormalizeTagFilter(value);
                if (SetProperty(ref _selectedTagFilter, normalized))
                {
                    OnPropertyChanged(nameof(SelectedTagFilterItem));
                    _ = ExecuteSearchAsync();
                    Serilog.Log.Information("Tag filter changed: {Tag}", normalized);
                }
                else if (!string.Equals(value, normalized, StringComparison.Ordinal))
                {
                    NotifySelectedTagFilterChanged();
                }
            }
        }

        public TypeFilterItem? SelectedTagFilterItem
        {
            get => DynamicTags.FirstOrDefault(t => t.Key.Equals(_selectedTagFilter, StringComparison.OrdinalIgnoreCase));
            set
            {
                var normalized = NormalizeTagFilter(value?.Key);
                if (SetProperty(ref _selectedTagFilter, normalized, nameof(SelectedTagFilter)))
                {
                    OnPropertyChanged(nameof(SelectedTagFilterItem));
                    _ = ExecuteSearchAsync();
                    Serilog.Log.Information("Tag filter changed: {Tag}", normalized);
                }
                else
                {
                    NotifySelectedTagFilterChanged();
                }
            }
        }

        public ObservableCollection<TypeFilterItem> DynamicTags
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
                        Serilog.Log.Information("Selected item changed: Id={Id}, Name={Name}", value.Id, value.Name);
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
                        Serilog.Log.Information("Settings panel opened (IsSettingsOpen = True)");
                    }
                    else
                    {
                        Serilog.Log.Information("Settings panel closed (IsSettingsOpen = False)");
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

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                var normalized = LocalizationService.NormalizeLanguage(value);
                if (SetProperty(ref _selectedLanguage, normalized))
                {
                    OnChangeLanguage(normalized);
                }
            }
        }

        public bool IsLanguageChinese
        {
            get => SelectedLanguage.Equals("zh-CN", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedLanguage = "zh-CN"; }
        }

        public bool IsLanguageEnglish
        {
            get => SelectedLanguage.Equals("en-US", StringComparison.OrdinalIgnoreCase);
            set { if (value) SelectedLanguage = "en-US"; }
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
        public ICommand ChangeLanguageCommand { get; }
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
            _selectedLanguage = LocalizationService.NormalizeLanguage(settings.Language);
            Loc = _localizationService.CreateStrings(_selectedLanguage);
            RebuildTypeFilters();
            RefreshAllTagsDisplay();
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
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(IsLanguageChinese));
            OnPropertyChanged(nameof(IsLanguageEnglish));
            
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
                SelectedTagFilter = SelectedTagFilter == AllTagsFilterKey ? null : SelectedTagFilter
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
                var tagItems = all
                    .SelectMany(p => p.Tags)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .GroupBy(t => t.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .Select(t => new TypeFilterItem { Key = t, DisplayName = t })
                    .ToList();

                tagItems.Insert(0, new TypeFilterItem { Key = AllTagsFilterKey, DisplayName = Loc["AllTags"] });

                var currentFilter = NormalizeTagFilter(SelectedTagFilter);
                SyncFilterItems(DynamicTags, tagItems);

                _selectedTagFilter = DynamicTags.Any(t => t.Key.Equals(currentFilter, StringComparison.OrdinalIgnoreCase))
                    ? currentFilter
                    : AllTagsFilterKey;

                OnPropertyChanged(nameof(DynamicTags));
                NotifySelectedTagFilterChanged();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to update dynamic tags.");
            }
        }

        private void OnStartAdd()
        {
            Serilog.Log.Information("New item started (触发新建条目命令)");
            EditorTitle = Loc["NewItemTitle"];
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
            EditorTitle = Loc["EditItemTitle"];
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

            var result = Views.ConfirmDialogWindow.Show(
                owner!,
                Loc["DeleteConfirmTitle"],
                string.Format(Loc["DeleteConfirmMessage"], prompt.Name),
                Loc["Confirm"],
                Loc["Cancel"]);
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
                EditorError = Loc["NameRequired"];
                return;
            }
            if (string.IsNullOrWhiteSpace(EditingContent))
            {
                EditorError = Loc["ContentRequired"];
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
                    ShowSuccessToast(Loc["Saved"]);
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
                        Serilog.Log.Information("Saving edited item: Id={Id}, Name={Name}, Type={Type}", existing.Id, existing.Name, existing.ItemType);
                        await _promptService.UpdateAsync(existing);
                        
                        Serilog.Log.Information("Save edit completed: Id={Id}", existing.Id);
                        IsEditing = false;
                        
                        await UpdateDynamicTagsAsync();
                        await ExecuteSearchAsync();
                        // Automatically highlight and select the edited item
                        SelectedPrompt = Prompts.FirstOrDefault(p => p.Id == existing.Id);
                        Serilog.Log.Information("Selected item restored after save: Id={Id}", existing.Id);
                    ShowSuccessToast(Loc["Saved"]);
                    }
                    else
                    {
                        Serilog.Log.Warning("未找到要编辑的条目 Id={Id}", _editingPromptId.Value);
                        EditorError = Loc["SaveFailedMissingItem"];
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "保存条目失败");
                EditorError = string.Format(Loc["SaveFailed"], ex.Message);
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
                    Serilog.Log.Warning(ex, "Clipboard.SetText failed");
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
                        "Command" => Loc["Command"],
                        "Snippet" => Loc["Snippet"],
                        "Note" => Loc["Note"],
                        _ => Loc["ItemType"]
                    };
                    
                    Serilog.Log.Information("复制条目 {Name} ({Type}) 到剪贴板，UsageCount={Count}", SelectedPrompt.Name, SelectedPrompt.ItemType, SelectedPrompt.UsageCount);
                }

                ShowSuccessToast(string.Format(Loc["CopiedContent"], localizedType));

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

            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "复制失败");
                ShowErrorToast(string.Format(Loc["CopyFailed"], ex.Message));
            }
        }

        private async Task OnToggleFavoriteAsync()
        {
            if (SelectedPrompt == null) return;
            try
            {
                await _promptService.ToggleFavoriteAsync(SelectedPrompt.Id);
                
                Serilog.Log.Information("Favorite state changed: {Name}, IsFavorite={IsFavorite}", SelectedPrompt.Name, SelectedPrompt.IsFavorite);
                
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
                    ShowErrorToast(Loc["DataDirMissing"]);
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
                ShowErrorToast(string.Format(Loc["OpenDataDirFailed"], ex.Message));
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
                    ShowErrorToast(Loc["LogsDirMissing"]);
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
                ShowErrorToast(string.Format(Loc["OpenLogsDirFailed"], ex.Message));
                Serilog.Log.Error(ex, "打开日志目录失败");
            }
        }

        private async Task OnExportDataAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = Loc["JsonFileFilter"],
                    FileName = $"SnipDock_Export_{DateTime.Now:yyyyMMdd}.json",
                    Title = Loc["ExportDialogTitle"]
                };
                
                Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
                if (dialog.ShowDialog(owner) == true)
                {
                    Serilog.Log.Information("开始导出数据到文件: {FilePath}", dialog.FileName);
                    await _importExportService.ExportAllAsync(dialog.FileName);
                    _lastExportedDir = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                    var count = (await _promptService.GetAllAsync()).Count;
                    Serilog.Log.Information("成功导出 {Count} 条数据到 JSON 文件", count);
                    ShowSuccessToast(string.Format(Loc["ExportSuccess"], count));
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast(string.Format(Loc["ExportFailed"], ex.Message));
                Serilog.Log.Error(ex, "导出数据失败");
            }
        }

        private async Task OnImportDataAsync()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = Loc["JsonFileFilter"],
                    Title = Loc["ImportDialogTitle"]
                };
                
                Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
                if (dialog.ShowDialog(owner) == true)
                {
                    Serilog.Log.Information("开始导入文件: {FilePath}", dialog.FileName);
                    var result = await _importExportService.ImportAsync(dialog.FileName);
                    
                    Serilog.Log.Information("导入成功：新增 {Added} 条，跳过 {Skipped} 条重复", result.ImportedCount, result.SkippedCount);
                    ShowSuccessToast(string.Format(Loc["ImportSuccess"], result.ImportedCount, result.SkippedCount));
                    
                    // Refresh views and dynamic tags
                    await UpdateDynamicTagsAsync();
                    await ExecuteSearchAsync();
                }
            }
            catch (Exception ex)
            {
                ShowErrorToast(string.Format(Loc["ImportFailed"], ex.Message));
                Serilog.Log.Error(ex, "数据导入失败");
            }
        }

        private void ShowSuccessToast(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

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
            if (string.IsNullOrWhiteSpace(message)) return;

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

        private void OnChangeLanguage(string? language)
        {
            var normalized = LocalizationService.NormalizeLanguage(language);
            Loc = _localizationService.CreateStrings(normalized);
            RebuildTypeFilters();
            RefreshAllTagsDisplay();

            var settings = _appSettingsStore.Load();
            if (!settings.Language.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                settings.Language = normalized;
                _appSettingsStore.Save(settings);
            }

            OnPropertyChanged(nameof(IsLanguageChinese));
            OnPropertyChanged(nameof(IsLanguageEnglish));
            OnPropertyChanged(nameof(SelectedTypeFilter));
            OnPropertyChanged(nameof(SelectedTagFilter));
            OnPropertyChanged(nameof(SelectedTagFilterItem));
            NotifyStateProperties();
        }

        private void RebuildTypeFilters()
        {
            _selectedTypeFilter = NormalizeTypeFilter(_selectedTypeFilter);

            var items = new[]
            {
                new TypeFilterItem { Key = "All", DisplayName = Loc["All"] },
                new TypeFilterItem { Key = "Prompt", DisplayName = "Prompt" },
                new TypeFilterItem { Key = "Command", DisplayName = Loc["Command"] },
                new TypeFilterItem { Key = "Snippet", DisplayName = Loc["Snippet"] },
                new TypeFilterItem { Key = "Note", DisplayName = Loc["Note"] },
                new TypeFilterItem { Key = "Favorites", DisplayName = Loc["Favorites"] },
                new TypeFilterItem { Key = "RecentlyUsed", DisplayName = Loc["RecentlyUsed"] },
            };
            SyncFilterItems(TypeFilters, items);
            OnPropertyChanged(nameof(TypeFilters));
            NotifySelectedTypeFilterChanged();
        }

        private void NotifySelectedTypeFilterChanged()
        {
            OnPropertyChanged(nameof(SelectedTypeFilter));
            OnPropertyChanged(nameof(SelectedTypeFilterItem));

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess() == false)
            {
                return;
            }

            dispatcher.BeginInvoke(
                new Action(() =>
                {
                    OnPropertyChanged(nameof(SelectedTypeFilter));
                    OnPropertyChanged(nameof(SelectedTypeFilterItem));
                }),
                System.Windows.Threading.DispatcherPriority.DataBind);
        }

        private void RefreshAllTagsDisplay()
        {
            _selectedTagFilter = NormalizeTagFilter(_selectedTagFilter);

            var allTags = DynamicTags.FirstOrDefault(t => t.Key == AllTagsFilterKey);
            if (allTags == null)
            {
                DynamicTags.Insert(0, new TypeFilterItem { Key = AllTagsFilterKey, DisplayName = Loc["AllTags"] });
            }
            else
            {
                allTags.DisplayName = Loc["AllTags"];
            }

            OnPropertyChanged(nameof(DynamicTags));
            NotifySelectedTagFilterChanged();
        }

        private void NotifySelectedTagFilterChanged()
        {
            OnPropertyChanged(nameof(SelectedTagFilter));
            OnPropertyChanged(nameof(SelectedTagFilterItem));
        }

        private static void SyncFilterItems(ObservableCollection<TypeFilterItem> target, IEnumerable<TypeFilterItem> source)
        {
            var desired = source.ToList();

            for (var index = target.Count - 1; index >= 0; index--)
            {
                var existing = target[index];
                if (!desired.Any(item => item.Key.Equals(existing.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    target.RemoveAt(index);
                }
            }

            for (var index = 0; index < desired.Count; index++)
            {
                var item = desired[index];
                var existing = target.FirstOrDefault(current => current.Key.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    target.Insert(index, item);
                    continue;
                }

                existing.DisplayName = item.DisplayName;

                var currentIndex = target.IndexOf(existing);
                if (currentIndex != index)
                {
                    target.Move(currentIndex, index);
                }
            }
        }

        private static string NormalizeTypeFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "All";

            var trimmed = value.Trim();
            if (trimmed.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("全部", StringComparison.OrdinalIgnoreCase))
            {
                return "All";
            }

            return trimmed;
        }

        private static string NormalizeTagFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return AllTagsFilterKey;

            var trimmed = value.Trim();
            if (trimmed.Equals(AllTagsFilterKey, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("全部标签", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals("All tags", StringComparison.OrdinalIgnoreCase))
            {
                return AllTagsFilterKey;
            }

            return trimmed;
        }

        private void OnChangeTheme(string? theme)
        {
            if (string.IsNullOrEmpty(theme)) return;
            var settings = _appSettingsStore.Load();
            if (settings.Theme != theme)
            {
                settings.Theme = theme;
                _appSettingsStore.Save(settings);
                Serilog.Log.Information("Theme changed: {Theme}", theme);
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
                Serilog.Log.Information("Accent color changed: {AccentColor}", colorName);
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
                    ShowErrorToast(Loc["ClipboardEmpty"]);
                    Serilog.Log.Information("从剪贴板新建条目失败：剪贴板为空或不含文本");
                    return;
                }

                string text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    ShowErrorToast(Loc["ClipboardEmpty"]);
                    Serilog.Log.Information("从剪贴板新建条目失败：剪贴板文本为空");
                    return;
                }

                Serilog.Log.Information("检测到剪贴板文本 (Clipboard text detected)");

                // Create draft using domain utility factory
                var draft = ClipboardEntryFactory.CreateDraft(text, SelectedTypeFilter, Loc["ClipboardDefaultTitle"]);

                // Load to editor UI
                EditorTitle = Loc["ClipboardItemTitle"];
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
                ShowErrorToast(Loc["ClipboardReadFailed"]);
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
                ShowSuccessToast(Loc["OpenedBackups"]);
            }
            catch (Exception ex)
            {
                ShowErrorToast(string.Format(Loc["OpenBackupsDirFailed"], ex.Message));
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
                ShowSuccessToast(Loc["OpenedData"]);
            }
            catch (Exception ex)
            {
                ShowErrorToast(string.Format(Loc["OpenExportDirFailed"], ex.Message));
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
                    Filter = Loc["JsonFileFilter"],
                    InitialDirectory = backupsPath,
                    Title = Loc["RestoreDialogTitle"]
                };

                Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
                if (dialog.ShowDialog(owner) == true)
                {
                    bool confirm = Views.ConfirmDialogWindow.Show(
                        owner!,
                        Loc["RestoreConfirmTitle"],
                        Loc["RestoreConfirmMessage"],
                        Loc["Confirm"],
                        Loc["Cancel"]);

                    if (!confirm)
                    {
                        Serilog.Log.Information("用户取消了备份恢复");
                        return;
                    }

                    Serilog.Log.Information("开始从备份文件恢复数据: {Path}", dialog.FileName);
                    await _backupService.RestoreBackupAsync(dialog.FileName);

                    // Re-initialize prompt service data
                    await _promptService.InitializeAsync();

                    ShowSuccessToast(Loc["BackupRestored"]);

                    // Reset search and filters
                    _selectedTypeFilter = "All";
                    _selectedTagFilter = AllTagsFilterKey;
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
                ShowErrorToast(string.Format(Loc["BackupRestoreFailed"], ex.Message));
                Serilog.Log.Error(ex, "从备份恢复数据失败");
            }
        }

        private async Task OnCreateManualBackupAsync()
        {
            try
            {
                Serilog.Log.Information("用户手动触发了创建数据备份...");
                await _backupService.CreateBackupAsync("Manual");
                ShowSuccessToast(Loc["BackupCreated"]);
            }
            catch (Exception ex)
            {
                ShowErrorToast(string.Format(Loc["BackupCreateFailed"], ex.Message));
                Serilog.Log.Error(ex, "手动备份创建失败");
            }
        }

        public void ShowHotkeyRegistrationFailedToast()
        {
            ShowErrorToast(Loc["HotkeyFailed"]);
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
                            Loc["StartupDevBlockedMessage"],
                            Loc["StartupDevBlockedTitle"],
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        
                        _isStartupEnabled = false;
                        OnPropertyChanged(nameof(IsStartupEnabled));
                        return;
                    }
                    await _startupLaunchService.EnableAsync();
                    ShowSuccessToast(Loc["StartupEnabledToast"]);
                }
                else
                {
                    await _startupLaunchService.DisableAsync();
                    ShowSuccessToast(Loc["StartupDisabledToast"]);
                }

                var settings = _appSettingsStore.Load();
                settings.IsStartupEnabled = enable;
                _appSettingsStore.Save(settings);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Startup launch failed");
                ShowErrorToast(Loc["StartupFailed"]);

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

            Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
            if (owner == null) return;

            var confirm = Views.ConfirmDialogWindow.Show(
                owner,
                Loc["TagRenameConfirmTitle"],
                string.Format(Loc["TagRenameConfirmMessage"], oldTag, newTag),
                Loc["Confirm"],
                Loc["Cancel"]);

            if (!confirm) return;

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

                ShowSuccessToast(Loc["TagRenamed"]);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Rename tag failed");
                ShowErrorToast(string.Format(Loc["TagRenameFailed"], ex.Message));
            }
        }

        private async Task OnMergeTagsAsync()
        {
            if (SelectedTagSummary == null || string.IsNullOrWhiteSpace(TargetTagName)) return;
            var sourceTag = SelectedTagSummary.TagName;
            var targetTag = TargetTagName.Trim();

            if (sourceTag.Equals(targetTag, StringComparison.OrdinalIgnoreCase)) return;

            Window? owner = Application.Current.Windows.OfType<Views.PromptPanelWindow>().FirstOrDefault();
            if (owner == null) return;

            var confirm = Views.ConfirmDialogWindow.Show(
                owner,
                Loc["TagMergeConfirmTitle"],
                string.Format(Loc["TagMergeConfirmMessage"], sourceTag, targetTag),
                Loc["Confirm"],
                Loc["Cancel"]);

            if (!confirm) return;

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

                ShowSuccessToast(Loc["TagMerged"]);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Merge tags failed");
                ShowErrorToast(string.Format(Loc["TagMergeFailed"], ex.Message));
            }
        }

        private void OnCleanUnusedTags()
        {
            Serilog.Log.Information("Clean unused tags action triggered");
            System.Windows.MessageBox.Show(
                Loc["CleanTagsInfoMessage"],
                Loc["CleanTagsInfoTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }
}
