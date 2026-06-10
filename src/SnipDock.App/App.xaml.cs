using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SnipDock.Core.Interfaces;
using SnipDock.Core.Models;
using SnipDock.Core.Services;
using SnipDock.Infrastructure.Logging;
using SnipDock.Infrastructure.Storage;
using SnipDock.App.ViewModels;
using SnipDock.App.Views;
using SnipDock.App.Services;
using Serilog;
using MessageBox = System.Windows.MessageBox;

namespace SnipDock.App
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;
        private const string MutexName = "SnipDock-SingleInstance-Mutex";
        private IServiceProvider? _serviceProvider;

        // Strongly-held class fields to prevent garbage collection
        private FloatingWindow? _floatingWindow;
        private PromptPanelWindow? _promptPanelWindow;
        private bool _isShuttingDown = false;
        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        // Win32 API for waking up existing instance
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        private const int SW_RESTORE = 9;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint dwProcessId);

        private const uint ATTACH_PARENT_PROCESS = 0x0fffffff;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handlerRoutine, bool add);

        private delegate bool ConsoleCtrlDelegate(uint ctrlType);

        private ConsoleCtrlDelegate? _consoleCtrlHandler;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Attach to the parent console to receive Ctrl+C CancelKeyPress events when launched from terminal
            AttachConsole(ATTACH_PARENT_PROCESS);

            // Set shutdown mode to explicit shutdown to prevent first run setup close from exiting the app
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Register Win32 Console Control handler to robustly intercept terminal Ctrl+C events in GUI app
            _consoleCtrlHandler = new ConsoleCtrlDelegate(ConsoleCtrlHandlerRoutine);
            SetConsoleCtrlHandler(_consoleCtrlHandler, true);

            // Register SessionEnding event for Windows shutdown/logoff
            SessionEnding += App_SessionEnding;

            // 1. Stage 1 Log Initialization - Run immediately on startup before Mutex check
            LoggingConfigurator.InitializeBootstrapLogging();
            Log.Information("进程启动。开始检测单实例 Mutex 状态...");

            bool isStartup = false;
            if (e.Args != null)
            {
                foreach (var arg in e.Args)
                {
                    if (arg.Equals("--startup", StringComparison.OrdinalIgnoreCase))
                    {
                        isStartup = true;
                        break;
                    }
                }
            }
            if (isStartup)
            {
                Log.Information("Started via Windows Startup option (--startup).");
            }

            // A. Detect Legacy PromptShelf Instance
            try
            {
                if (Mutex.TryOpenExisting("PromptShelf-SingleInstance-Mutex", out var legacyMutex))
                {
                    Log.Warning("检测到旧版 PromptShelf 实例仍在运行！");
                    legacyMutex.Dispose(); // Close the handle we just opened
                    var loc = CurrentLoc();
                    
                    var result = MessageBox.Show(
                        loc["LegacyInstanceMessage"],
                        loc["LegacyInstanceTitle"],
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No); // Default to No (safe exit)
                        
                    if (result != MessageBoxResult.Yes)
                    {
                        Log.Information("用户取消启动以避开旧版冲突，新进程退出。");
                        RequestShutdown("Legacy instance run cancelled");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "尝试检测旧版 Mutex 发生异常，降级忽略。");
            }

            // 2. Single Instance Protection
            _mutex = new Mutex(true, MutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                Log.Warning("检测到已有实例在运行！尝试激活并唤醒现有悬浮窗，新进程准备退出。");
                try
                {
                    IntPtr hWnd = FindWindow(null, "SnipDock-FloatingWindow");
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                        Log.Information("成功发出激活已有窗口的 Win32 命令。");
                    }
                    else
                    {
                        Log.Warning("未找到活动的 'SnipDock-FloatingWindow' 窗口，仅做单实例阻断退出。");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "尝试通过 Win32 API 激活运行中实例窗体失败。");
                }

                var loc = CurrentLoc();
                MessageBox.Show(loc["DuplicateInstanceMessage"], loc["DuplicateInstanceTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
                RequestShutdown("Duplicate process exit");
                return;
            }

            Log.Information("单实例申请成功，本进程为唯一主进程实例。");

            // 3. Global Exception Handlers
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            // 4. Load Bootstrap Configuration
            var bootstrapStore = new LocalBootstrapSettingsStore();
            var bootstrapSettings = bootstrapStore.Load();
            string storagePath = bootstrapSettings.StoragePath;

            // 5. Direct User to folder choice if path is invalid/empty (FirstRun mode)
            if (string.IsNullOrWhiteSpace(storagePath) || !Directory.Exists(storagePath))
            {
                Log.Information("当前数据存储文件夹未配置或已失效。调出首次启动向导窗口。");
                var configVm = new ConfigurationViewModel(bootstrapStore, ConfigurationMode.FirstRun);
                var configWin = new ConfigurationWindow(configVm);
                
                var result = configWin.ShowDialog();
                if (result == true)
                {
                    storagePath = bootstrapStore.Load().StoragePath;
                    Log.Information("用户已成功配置并保存了数据目录路径：{StoragePath}", storagePath);
                }
                else
                {
                    Log.Warning("用户取消了引导路径配置，程序准备关闭退出。");
                    RequestShutdown("Initial configuration cancelled");
                    return;
                }
            }

            // 6. Stage 2 Log Switch
            try
            {
                Log.Information("正在将日志记录器安全切换至第二阶段 (数据目录)...");
                LoggingConfigurator.InitializeAppLogging(storagePath);
            }
            catch (Exception ex)
            {
                var loc = CurrentLoc();
                MessageBox.Show(string.Format(loc["LoggingInitFailedMessage"], ex.Message), loc["LoggingInitFailedTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
                RequestShutdown("Logging configuration failure");
                return;
            }

            // 7. Initialize DI container
            var services = new ServiceCollection();
            ConfigureServices(services, storagePath);
            _serviceProvider = services.BuildServiceProvider();

            // 8. Load App settings and prompts
            var appSettingsStore = _serviceProvider.GetRequiredService<IAppSettingsStore>();
            var appSettings = appSettingsStore.Load();

            // Apply saved theme and accent color on startup
            var themeService = _serviceProvider.GetRequiredService<ThemeService>();
            themeService.ApplyTheme(appSettings.Theme);
            themeService.ApplyAccentColor(appSettings.AccentColor, appSettings.Theme);

            var promptService = _serviceProvider.GetRequiredService<PromptService>();
            var promptStore = _serviceProvider.GetRequiredService<IPromptStore>();
            try
            {
                Log.Information("正在加载本地条目列表...");
                await promptService.InitializeAsync();
                
                // 9. Detect Backup Recovery Event
                if (promptStore.WasRecoveredFromBackup)
                {
                    Log.Warning("系统检测到主数据文件存在损坏或丢失，已自动从备份文件完成恢复。向用户发出 UI 警示。");
                    var loc = CurrentLoc();
                    MessageBox.Show(
                        loc["BackupRecoveredMessage"],
                        loc["BackupRecoveredTitle"], 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "初始化数据源失败。");
            }

            // 10. Startup windows using strongly held fields
            var floatingVm = _serviceProvider.GetRequiredService<FloatingViewModel>();
            _floatingWindow = _serviceProvider.GetRequiredService<FloatingWindow>();
            _promptPanelWindow = _serviceProvider.GetRequiredService<PromptPanelWindow>();
            var panelVm = _serviceProvider.GetRequiredService<PromptPanelViewModel>();

            // Force handle creation on PromptPanelWindow immediately so we can bind global hotkeys safely
            var helper = new System.Windows.Interop.WindowInteropHelper(_promptPanelWindow);
            helper.EnsureHandle();

            // Register/Trigger BackupService Daily Backup asynchronously (do not block UI thread)
            var backupService = _serviceProvider.GetRequiredService<BackupService>();
            _ = Task.Run(async () => await backupService.BackupOnStartupIfNeededAsync());

            // Register Global Hotkey (Ctrl + Alt + P)
            try
            {
                var hotkeyService = _serviceProvider.GetRequiredService<GlobalHotkeyService>();
                hotkeyService.Register(_promptPanelWindow);
                hotkeyService.HotkeyTriggered += (s, ev) =>
                {
                    TogglePromptPanel("Hotkey");
                };
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "全局快捷键注册失败 (Hotkey registration failed)");
                panelVm.ShowHotkeyRegistrationFailedToast();
            }

            // Setup Floating Ball coordinates and verify screen bounds
            double initialLeft = appSettings.WindowLeft;
            double initialTop = appSettings.WindowTop;
            Rect workArea = SystemParameters.WorkArea;

            // Ensure window is in screen bounds
            if (initialLeft < workArea.Left || initialLeft + 65 > workArea.Left + workArea.Width ||
                initialTop < workArea.Top || initialTop + 65 > workArea.Top + workArea.Height)
            {
                Log.Warning("Saved window position ({Left}, {Top}) is out of screen bounds. Clamping/resetting to default safety position.", initialLeft, initialTop);
                initialLeft = workArea.Left + workArea.Width - 100;
                initialTop = workArea.Top + workArea.Height - 150;
            }

            _floatingWindow.Left = initialLeft;
            _floatingWindow.Top = initialTop;

            // Bind Toggle Panel Event to our unified helper
            floatingVm.TogglePanelRequested += (s, ev) => TogglePromptPanel("FloatingButton");

            panelVm.HidePanelRequested += (s, ev) =>
            {
                if (_promptPanelWindow != null && _promptPanelWindow.IsVisible)
                {
                    _promptPanelWindow.Hide();
                    Log.Information("Prompt panel hidden after copy");
                }
            };

            // Bind Reset Position Event
            floatingVm.ResetPositionRequested += (s, ev) =>
            {
                Log.Information("Floating position reset requested.");
                Rect currentWorkArea = SystemParameters.WorkArea;
                double defaultLeft = currentWorkArea.Left + currentWorkArea.Width - 100;
                double defaultTop = currentWorkArea.Top + currentWorkArea.Height - 150;
                
                if (_floatingWindow != null)
                {
                    _floatingWindow.Left = defaultLeft;
                    _floatingWindow.Top = defaultTop;
                    Log.Information("Floating position reset");
                    SaveFloatingWindowPosition(defaultLeft, defaultTop);
                }
            };

            // Bind Reset Storage Path Event (ChangeStorageLocation mode)
            floatingVm.ResetStoragePathRequested += (s, ev) =>
            {
                Log.Information("用户触发了重设数据存储目录的操作。");
                var configVm = new ConfigurationViewModel(bootstrapStore, ConfigurationMode.ChangeStorageLocation, appSettingsStore.Load().Language);
                var configWin = new ConfigurationWindow(configVm);
                
                var result = configWin.ShowDialog();
                if (result == true)
                {
                    string newPath = bootstrapStore.Load().StoragePath;
                    Log.Information("Storage path changed. New Path: {Path}", newPath);
                    Log.Information("Storage settings window closed");
                    var loc = CurrentLoc();
                    MessageBox.Show(loc["StorageChangedMessage"], loc["StorageChangedTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Trigger dynamic executable restart
                    System.Diagnostics.Process.Start(Environment.ProcessPath!);
                    RequestShutdown("Storage path changed - rebooting");
                }
                else
                {
                    Log.Information("Storage settings canceled");
                    Log.Information("Storage settings window closed");
                }
            };

            // Bind Settings Panel Change Storage Path Event (ChangeStorageLocation mode)
            panelVm.ChangeStoragePathRequested += (s, ev) =>
            {
                Log.Information("用户在管理面板设置区触发了重设数据存储目录的操作。");
                var configVm = new ConfigurationViewModel(bootstrapStore, ConfigurationMode.ChangeStorageLocation, appSettingsStore.Load().Language);
                var configWin = new ConfigurationWindow(configVm);
                
                var result = configWin.ShowDialog();
                if (result == true)
                {
                    string newPath = bootstrapStore.Load().StoragePath;
                    Log.Information("Storage path changed. New Path: {Path}", newPath);
                    Log.Information("Storage settings window closed");
                    var loc = CurrentLoc();
                    MessageBox.Show(loc["StorageChangedMessage"], loc["StorageChangedTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Trigger dynamic executable restart
                    System.Diagnostics.Process.Start(Environment.ProcessPath!);
                    RequestShutdown("Storage path changed via panel settings - rebooting");
                }
                else
                {
                    Log.Information("Storage settings canceled");
                    Log.Information("Storage settings window closed");
                }
            };

            // Display floating ball
            _floatingWindow.Show();
            Log.Information("主悬浮球成功渲染并显示。");

            InitializeTrayIcon(floatingVm, panelVm);
        }

        private void InitializeTrayIcon(FloatingViewModel floatingVm, PromptPanelViewModel panelVm)
        {
            Log.Information("Tray icon initializing");
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                
                try
                {
                    var exePath = System.Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    }
                }
                catch
                {
                    // Ignore extraction failure
                }

                if (_notifyIcon.Icon == null)
                {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }

                _notifyIcon.Text = "SnipDock";

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();

                var toggleItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["TogglePanel"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: TogglePanel");
                    Dispatcher.BeginInvoke(() => TogglePromptPanel("TrayMenu"));
                });

                var newItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["NewItemTitle"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: NewItem");
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (_promptPanelWindow != null)
                        {
                            if (!_promptPanelWindow.IsVisible)
                            {
                                TogglePromptPanel("TrayMenu-NewItem");
                            }
                            panelVm.AddCommand.Execute(null);
                        }
                    });
                });

                var clipboardItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["AddFromClipboard"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: NewFromClipboard");
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (_promptPanelWindow != null)
                        {
                            if (!_promptPanelWindow.IsVisible)
                            {
                                TogglePromptPanel("TrayMenu-Clipboard");
                            }
                            panelVm.AddFromClipboardCommand.Execute(null);
                        }
                    });
                });

                var dataDirItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["OpenDataDir"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: OpenDataDir");
                    Dispatcher.BeginInvoke(() => panelVm.OpenStorageDirCommand.Execute(null));
                });

                var logsDirItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["OpenLogsDir"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: OpenLogsDir");
                    Dispatcher.BeginInvoke(() => panelVm.OpenLogsDirCommand.Execute(null));
                });

                var backupsDirItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["OpenBackupsDir"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: OpenBackupsDir");
                    Dispatcher.BeginInvoke(() => panelVm.OpenBackupsDirCommand.Execute(null));
                });

                var resetPosItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["ResetFloatingPosition"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: ResetFloatingPos");
                    Dispatcher.BeginInvoke(() =>
                    {
                        floatingVm.ResetPositionCommand.Execute(null);
                    });
                });

                var settingsItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["SettingsTitle"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: Settings");
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (_promptPanelWindow != null)
                        {
                            if (!_promptPanelWindow.IsVisible)
                            {
                                TogglePromptPanel("TrayMenu-Settings");
                            }
                            panelVm.IsSettingsOpen = true;
                            panelVm.IsEditing = false;
                        }
                    });
                });

                var exitItem = new System.Windows.Forms.ToolStripMenuItem(panelVm.Loc["ExitApp"], null, (s, ev) =>
                {
                    Log.Information("Tray menu action triggered: Exit");
                    Dispatcher.BeginInvoke(() => RequestShutdown("TrayExit"));
                });

                void ApplyTrayLocalization()
                {
                    toggleItem.Text = panelVm.Loc["TogglePanel"];
                    newItem.Text = panelVm.Loc["NewItemTitle"];
                    clipboardItem.Text = panelVm.Loc["AddFromClipboard"];
                    dataDirItem.Text = panelVm.Loc["OpenDataDir"];
                    logsDirItem.Text = panelVm.Loc["OpenLogsDir"];
                    backupsDirItem.Text = panelVm.Loc["OpenBackupsDir"];
                    resetPosItem.Text = panelVm.Loc["ResetFloatingPosition"];
                    settingsItem.Text = panelVm.Loc["SettingsTitle"];
                    exitItem.Text = panelVm.Loc["ExitApp"];
                    floatingVm.SetLanguage(panelVm.SelectedLanguage);
                }

                ApplyTrayLocalization();
                panelVm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(PromptPanelViewModel.Loc) ||
                        args.PropertyName == nameof(PromptPanelViewModel.SelectedLanguage))
                    {
                        Dispatcher.BeginInvoke(ApplyTrayLocalization);
                    }
                };

                contextMenu.Items.Add(toggleItem);
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add(newItem);
                contextMenu.Items.Add(clipboardItem);
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add(dataDirItem);
                contextMenu.Items.Add(logsDirItem);
                contextMenu.Items.Add(backupsDirItem);
                contextMenu.Items.Add(resetPosItem);
                contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                contextMenu.Items.Add(settingsItem);
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = contextMenu;

                _notifyIcon.MouseClick += (s, ev) =>
                {
                    if (ev.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        Dispatcher.BeginInvoke(() => TogglePromptPanel("TrayClick"));
                    }
                };

                _notifyIcon.Visible = true;
                Log.Information("Tray icon initialized");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Tray initialization failed");
            }
        }

        private void TogglePromptPanel(string triggerSource)
        {
            if (_promptPanelWindow == null || _floatingWindow == null || _serviceProvider == null) return;

            Log.Information("收到 Toggle 面板指令，触发源: {Source}", triggerSource);
            var panelVm = _serviceProvider.GetRequiredService<PromptPanelViewModel>();

            if (_promptPanelWindow.IsVisible)
            {
                _promptPanelWindow.Hide();
                Log.Information("Prompt panel hidden via {Source}", triggerSource);
            }
            else
            {
                Log.Information("Prompt panel shown via {Source}", triggerSource);
                // Compute panel position dynamically next to the floating ball
                double left = _floatingWindow.Left - _promptPanelWindow.Width - 10;
                double top = _floatingWindow.Top;

                // Bound adjustments
                if (left < 0) left = _floatingWindow.Left + _floatingWindow.Width + 10;
                if (left + _promptPanelWindow.Width > SystemParameters.VirtualScreenWidth) left = 10;
                
                double maxTop = SystemParameters.PrimaryScreenHeight - _promptPanelWindow.Height - 60;
                if (top > maxTop) top = maxTop;
                if (top < 10) top = 10;

                _promptPanelWindow.Left = left;
                _promptPanelWindow.Top = top;

                _ = panelVm.LoadPromptsAsync(); // Refresh prompts dynamically on open
                _promptPanelWindow.Show();
                _promptPanelWindow.Activate();

                // Focus search box dynamically based on user behavioral settings
                if (panelVm.FocusSearchOnOpen && !panelVm.IsEditing && !panelVm.IsSettingsOpen)
                {
                    _promptPanelWindow.FocusSearchBox(panelVm.SelectSearchTextOnOpen);
                }
            }
        }

        public void SaveFloatingWindowPosition(double left, double top)
        {
            try
            {
                if (_serviceProvider != null)
                {
                    var appSettingsStore = _serviceProvider.GetRequiredService<IAppSettingsStore>();
                    var appSettings = appSettingsStore.Load();
                    appSettings.WindowLeft = left;
                    appSettings.WindowTop = top;
                    appSettingsStore.Save(appSettings);
                    Log.Information("Floating drag end and saved position. Left={Left}, Top={Top} (saved directly to settings.json)", left, top);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to persist AppSettings coordinates on drag end.");
            }
        }

        public void RequestShutdown(string reason)
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            DisposeTrayIcon();

            Log.Information("Shutdown requested: {Reason}", reason);

            // 1. Save settings
            Log.Information("Saving app settings");
            try
            {
                if (_floatingWindow != null && _serviceProvider != null)
                {
                    var appSettingsStore = _serviceProvider.GetRequiredService<IAppSettingsStore>();
                    var appSettings = appSettingsStore.Load();
                    appSettings.WindowLeft = _floatingWindow.Left;
                    appSettings.WindowTop = _floatingWindow.Top;
                    appSettingsStore.Save(appSettings);
                    Log.Information("Saved settings - coordinates persisted: ({Left}, {Top})", _floatingWindow.Left, _floatingWindow.Top);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save settings during RequestShutdown.");
            }

            UnregisterGlobalHotkey();

            // 2. Close windows
            Log.Information("Closing windows");
            try
            {
                if (_promptPanelWindow != null)
                {
                    _promptPanelWindow.Close();
                    Log.Information("PromptPanelWindow closed.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing PromptPanelWindow.");
            }

            try
            {
                if (_floatingWindow != null)
                {
                    _floatingWindow.Close();
                    Log.Information("FloatingWindow closed.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing FloatingWindow.");
            }

            // 3. Process Exit & Log Flush
            Log.Information("Flushing logger");
            Log.Information("Process exit");
            Log.CloseAndFlush();

            try
            {
                Shutdown();
            }
            catch
            {
                Environment.Exit(0);
            }
        }

        private void DisposeTrayIcon()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                    Log.Information("Tray icon disposed");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disposing tray icon.");
            }
        }

        private void UnregisterGlobalHotkey()
        {
            try
            {
                if (_serviceProvider != null)
                {
                    var hotkeyService = _serviceProvider.GetService<GlobalHotkeyService>();
                    hotkeyService?.Unregister();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error unregistering hotkey during shutdown.");
            }
        }

        private bool ConsoleCtrlHandlerRoutine(uint ctrlType)
        {
            Log.Information("Win32 Console Control event detected: {CtrlType}", ctrlType);
            // ctrlType 0 = CTRL_C_EVENT, 1 = CTRL_BREAK_EVENT, 2 = CTRL_CLOSE_EVENT
            if (ctrlType == 0 || ctrlType == 1 || ctrlType == 2)
            {
                Log.Information("Console cancel (Ctrl+C/Break/Close) signal received. Initiating graceful shutdown.");
                // Execute shutdown sequence on the UI thread's Dispatcher asynchronously to avoid deadlocks
                Dispatcher.BeginInvoke(() => RequestShutdown($"Console Ctrl+C signal ({ctrlType})"));
                return true; // Handled the signal, prevent immediate termination
            }
            return false;
        }

        private void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Log.Information("System session ending detected (Windows logoff or shutdown).");
            RequestShutdown("Windows Session Ending");
        }

        private void ConfigureServices(IServiceCollection services, string storagePath)
        {
            // Logging Configuration
            services.AddLogging(builder =>
            {
                builder.AddSerilog(dispose: true);
            });

            // Persistence Stores
            services.AddSingleton<IBootstrapSettingsStore, LocalBootstrapSettingsStore>();
            services.AddSingleton<IAppSettingsStore>(sp => new LocalAppSettingsStore(storagePath));
            services.AddSingleton<IPromptStore>(sp => new JsonPromptStore(storagePath));

            // Core Business Services
            services.AddSingleton<PromptService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<ShelfImportExportService>();
            services.AddSingleton<BackupService>(sp => new BackupService(storagePath));
            services.AddSingleton<GlobalHotkeyService>();
            services.AddSingleton<IRegistryService, SnipDock.Infrastructure.Services.WindowsRegistryService>();
            services.AddSingleton<IStartupLaunchService, SnipDock.Infrastructure.Services.StartupLaunchService>();
            services.AddSingleton<TagManagementService>();

            // ViewModels
            services.AddSingleton<FloatingViewModel>();
            services.AddSingleton<PromptPanelViewModel>();

            // Views
            services.AddSingleton<FloatingWindow>();
            services.AddSingleton<PromptPanelWindow>();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            Log.Fatal(e.Exception, "从 UI 线程捕获到全局未处理异常。");
            var loc = CurrentLoc();
            MessageBox.Show(string.Format(loc["UnexpectedErrorMessage"], e.Exception.Message), loc["UnexpectedErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            Log.Fatal(exception, "AppDomain 捕获到全局致命崩溃异常。IsTerminating={IsTerminating}", e.IsTerminating);
            var loc = CurrentLoc();
            MessageBox.Show(string.Format(loc["FatalErrorMessage"], exception?.Message), loc["FatalErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "TaskScheduler 捕获到未观察的任务异步异常。");
            e.SetObserved();
        }

        private LocalizedStrings CurrentLoc()
        {
            string? language = null;
            try
            {
                language = _serviceProvider?.GetService<IAppSettingsStore>()?.Load().Language;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load language for localized app message. Falling back to system language.");
            }

            return new LocalizationService().CreateStrings(LocalizationService.NormalizeLanguage(language));
        }

        protected override void OnExit(ExitEventArgs e)
        {
            DisposeTrayIcon();
            UnregisterGlobalHotkey();
            Log.Information("====== SnipDock 进程关闭。Goodbye! ======");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
