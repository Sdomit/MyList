using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MyList.Models;
using Application = System.Windows.Application;
using MyList.Services;
using MyList.ViewModels;
using MyList.Views;

namespace MyList;

public partial class App : Application
{
    public bool IsShutdownRequested { get; private set; }

    private const string MiniLauncherHotkeyName = "mini-launcher";

    static App()
    {
        AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
        {
            if (args.Exception is System.Windows.Markup.XamlParseException)
            {
                LogService.Instance.Log(args.Exception, "XAML parse exception");
            }
        };
    }

    private StorageService? _storageService;
    private TrayService? _trayService;
    private HotkeyService? _hotkeyService;
    private ThemeService? _themeService;
    private DensityService? _densityService;
    private SkinService? _skinService;
    private NetworkCheckService? _networkCheckService;
    private SystemThemeService? _systemThemeService;
    private AppInstanceService? _appInstanceService;
    private ClipboardAssetService? _clipboardAssetService;
    private MainWindow? _mainWindow;
    private AppCommandPayload? _startupCommandPayload;
    private MiniLauncherWindow? _miniLauncherWindow;
    private LauncherService? _launcherService;

    private MainViewModel? _mainViewModel;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var log = LogService.Instance;
        log.Log("App startup");
        TryParseExplorerAddToMtabArgs(e.Args, out _startupCommandPayload);

        _appInstanceService = new AppInstanceService();
        if (!_appInstanceService.TryAcquirePrimary())
        {
            if (_startupCommandPayload is not null)
            {
                var forwarded = _appInstanceService.TrySendToPrimaryAsync(_startupCommandPayload).GetAwaiter().GetResult();
                if (!forwarded)
                {
                    log.Log("Failed to forward explorer command to running instance.");
                }
            }

            Shutdown();
            return;
        }

        var safeMode = Environment.GetEnvironmentVariable("MYLIST_SAFE_MODE") == "1";

        _clipboardAssetService = new ClipboardAssetService();
        _storageService = new StorageService(_clipboardAssetService);
        _themeService = new ThemeService();
        _densityService = new DensityService();
        _skinService = new SkinService();
        _hotkeyService = new HotkeyService();
        _trayService = new TrayService();
        _networkCheckService = new NetworkCheckService();
        _systemThemeService = new SystemThemeService();
        var iconService = new IconService(_clipboardAssetService);
        var launcherService = new LauncherService();
        _launcherService = launcherService;
        var managedActionRunnerService = new ManagedActionRunnerService();
        var explorerTabAutomationService = new ExplorerTabAutomationService();

        DispatcherUnhandledException += (_, args) =>
        {
            log.Log(args.Exception, "Unhandled UI exception");
            if (RuntimeStatus.DebugMode)
            {
                System.Windows.MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nThe app will keep running. See the log for details.",
                    "MyList",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                log.Log(ex, "Unhandled domain exception");
            }
        };

        var appData = _storageService.Load();
        RuntimeStatus.DebugMode = appData.AppSettings.EnableDebugMode;
        if (RuntimeStatus.DebugMode)
        {
            log.Log("Debug mode enabled on startup.");
        }

        if (appData.AppSettings.FollowSystemTheme)
        {
            appData.AppSettings.Theme = _systemThemeService.GetCurrentTheme();
        }

        _themeService.ApplyTheme(appData.AppSettings.Theme, appData.AppSettings.Accent);
        _skinService.ApplySkin(appData.AppSettings.Skin);

        _mainViewModel = new MainViewModel(
            appData,
            _storageService,
            iconService,
            launcherService,
            managedActionRunnerService,
            explorerTabAutomationService,
            _clipboardAssetService,
            _themeService,
            _densityService,
            _skinService
        );

        _mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        _mainWindow.SourceInitialized += (_, _) =>
        {
            _hotkeyService.Initialize(_mainWindow, _mainViewModel);
            _hotkeyService.RegisterOrUpdateNamedSecondaryHotkey(
                MiniLauncherHotkeyName,
                _mainViewModel.Settings.AppSettings.MiniLauncherHotkey,
                () => Dispatcher.Invoke(ShowMiniLauncher));
            _mainViewModel.MiniLauncherHotkeyChanged += (_, settings) =>
                _hotkeyService.RegisterOrUpdateNamedSecondaryHotkey(
                    MiniLauncherHotkeyName,
                    settings,
                    () => Dispatcher.Invoke(ShowMiniLauncher));
        };
        _mainWindow.Show();
        _mainWindow.RestoreAndActivate();

        // Defer non-critical service init until after first paint so the window
        // appears as fast as possible. Tray, network health, explorer-integration
        // status, and system-theme watching all run after the dispatcher idles.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            var explorerIntegrationService = new ExplorerIntegrationService();
            var explorerIntegrationStatus = explorerIntegrationService.GetStatus();
            RuntimeStatus.ExplorerMenuInstalled = explorerIntegrationStatus.IsInstalled;
            RuntimeStatus.ExplorerMenuStatusMessage = explorerIntegrationStatus.StatusMessage;

            if (!safeMode)
            {
                _trayService.Initialize(_mainWindow, _mainViewModel);
                _networkCheckService.Initialize(_mainViewModel);
                _mainViewModel.AttachNetworkCheckService(_networkCheckService);

                if (appData.AppSettings.FollowSystemTheme)
                {
                    _systemThemeService.StartWatching(theme =>
                    {
                        Dispatcher.Invoke(() => _mainViewModel.Settings.Theme = theme);
                    });
                }

                _mainViewModel.Settings.FollowSystemThemeChanged += (_, enabled) =>
                {
                    if (enabled)
                    {
                        var theme = _systemThemeService.GetCurrentTheme();
                        _mainViewModel.Settings.Theme = theme;
                        _systemThemeService.StartWatching(newTheme =>
                        {
                            Dispatcher.Invoke(() => _mainViewModel.Settings.Theme = newTheme);
                        });
                    }
                    else
                    {
                        _systemThemeService.StopWatching();
                    }
                };
            }
        }));

        _appInstanceService.StartServer(HandleIncomingAppCommandAsync);

        if (_startupCommandPayload is not null)
        {
            _ = Dispatcher.InvokeAsync(async () => await RouteAppCommandAsync(_startupCommandPayload));
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _appInstanceService?.Dispose();
        _networkCheckService?.Dispose();
        _trayService?.Dispose();
        _hotkeyService?.Dispose();
        _systemThemeService?.Dispose();
        _miniLauncherWindow?.Close();
    }

    private void ShowMiniLauncher()
    {
        if (_mainViewModel is null || _launcherService is null)
        {
            return;
        }

        if (_miniLauncherWindow is null)
        {
            var source = new OrbitSourceService(
                () => _mainViewModel.AllItems,
                () => _mainViewModel.Collections
                    .Where(collection => !collection.IsSmart)
                    .Select(collection => new OrbitUserCollection(
                        collection.Id, collection.Name, collection.Items.ToList()))
                    .ToList());
            var viewModel = new MiniLauncherViewModel(
                source,
                _launcherService,
                () => _miniLauncherWindow?.Hide(),
                ActivateMainWindow,
                item => _mainViewModel.DeleteItemGloballyCommand.Execute(item),
                collectionId =>
                {
                    var target = _mainViewModel.Collections.FirstOrDefault(c => c.Id == collectionId);
                    if (target is not null)
                    {
                        _mainViewModel.DeleteCollectionCommand.Execute(target);
                    }
                });
            _miniLauncherWindow = new MiniLauncherWindow(viewModel);
        }

        _miniLauncherWindow.Summon();
    }

    private async Task HandleIncomingAppCommandAsync(AppCommandPayload payload)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            ActivateMainWindow();
            await RouteAppCommandAsync(payload);
        });
    }

    private async Task RouteAppCommandAsync(AppCommandPayload payload)
    {
        if (_mainViewModel is null || payload is null)
        {
            return;
        }

        if (payload.CommandType == AppCommandType.ExplorerAddToMtab)
        {
            LogService.Instance.Log($"Incoming app command: {payload.CommandType}, source={payload.Source}, paths={payload.Paths.Count}.");
            await _mainViewModel.HandleExplorerAddToMtabAsync(payload.Paths);
        }
    }

    private void ActivateMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.RestoreAndActivate();
    }

    public void RequestShutdown()
    {
        IsShutdownRequested = true;
        Shutdown();
    }

    private static bool TryParseExplorerAddToMtabArgs(string[] args, out AppCommandPayload? payload)
    {
        payload = null;
        if (args is null || args.Length == 0)
        {
            return false;
        }

        var commandIndex = Array.FindIndex(args, arg => string.Equals(arg, "--explorer-add-to-mtab", StringComparison.OrdinalIgnoreCase));
        if (commandIndex < 0)
        {
            return false;
        }

        var separatorIndex = Array.IndexOf(args, "--", commandIndex + 1);
        var rawPaths = separatorIndex >= 0
            ? args.Skip(separatorIndex + 1)
            : args.Skip(commandIndex + 1);

        var paths = rawPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim().Trim('"'))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        if (paths.Count == 0)
        {
            return false;
        }

        payload = new AppCommandPayload
        {
            CommandType = AppCommandType.ExplorerAddToMtab,
            Source = "ExplorerContextMenu",
            Paths = paths
        };

        return true;
    }
}
