using System;
using System.Threading.Tasks;
using System.Windows.Input;
using MyList.Helpers;
using MyList.Models;
using MyList.Services;

namespace MyList.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly ThemeService _themeService;
    private readonly DensityService _densityService;
    private readonly Action _queueSave;
    private readonly Action<HotkeySettings> _hotkeyChanged;
    private readonly Func<Task> _exportAction;
    private readonly Func<Task> _importAction;
    private readonly Func<Task> _restoreAction;
    private readonly Action _openDuplicateManagerAction;
    private readonly Action _openDiagnosticsAction;
    private readonly ExplorerIntegrationService _explorerIntegrationService;
    private readonly StartupService _startupService = new();
    private string _debugCopyStatus = string.Empty;
    private string _explorerIntegrationStatusText = "Explorer integration status unknown.";
    private bool _isExplorerIntegrationInstalled;
    public event EventHandler<bool>? FollowSystemThemeChanged;

    public SettingsViewModel(
        AppSettings settings,
        ThemeService themeService,
        DensityService densityService,
        Action queueSave,
        Action<HotkeySettings> hotkeyChanged,
        Func<Task> exportAction,
        Func<Task> importAction,
        Func<Task> restoreAction,
        Action openDuplicateManagerAction,
        Action openDiagnosticsAction,
        ExplorerIntegrationService explorerIntegrationService)
    {
        _settings = settings;
        _themeService = themeService;
        _densityService = densityService;
        _queueSave = queueSave;
        _hotkeyChanged = hotkeyChanged;
        _exportAction = exportAction;
        _importAction = importAction;
        _restoreAction = restoreAction;
        _openDuplicateManagerAction = openDuplicateManagerAction;
        _openDiagnosticsAction = openDiagnosticsAction;
        _explorerIntegrationService = explorerIntegrationService;

        ExportCommand = new AsyncRelayCommand(_exportAction);
        ImportCommand = new AsyncRelayCommand(_importAction);
        RestoreBackupCommand = new AsyncRelayCommand(_restoreAction);
        OpenDuplicateManagerCommand = new RelayCommand(_openDuplicateManagerAction);
        OpenDiagnosticsCommand = new RelayCommand(_openDiagnosticsAction);
        CopyDebugReportCommand = new RelayCommand(CopyDebugReport);
        InstallExplorerIntegrationCommand = new RelayCommand(InstallExplorerIntegration);
        UninstallExplorerIntegrationCommand = new RelayCommand(UninstallExplorerIntegration);
        RepairExplorerIntegrationCommand = new RelayCommand(RepairExplorerIntegration);

        LayoutModes = Enum.GetValues<LayoutMode>();
        CollectionsLayouts = Enum.GetValues<CollectionsLayout>();
        DensityModes = Enum.GetValues<UiDensity>();
        RuntimeStatus.DebugMode = _settings.EnableDebugMode;
        RefreshExplorerIntegrationStatus();
    }

    public AppSettings AppSettings => _settings;

    public Array LayoutModes { get; }

    public Array CollectionsLayouts { get; }

    public Array DensityModes { get; }

    public ThemeMode Theme
    {
        get => _settings.Theme;
        set
        {
            if (_settings.Theme != value)
            {
                _settings.Theme = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDarkMode));
                _themeService.ApplyTheme(value);
                _queueSave();
            }
        }
    }

    public bool FollowSystemTheme
    {
        get => _settings.FollowSystemTheme;
        set
        {
            if (_settings.FollowSystemTheme != value)
            {
                _settings.FollowSystemTheme = value;
                OnPropertyChanged();
                FollowSystemThemeChanged?.Invoke(this, value);
                _queueSave();
            }
        }
    }

    public bool IsDarkMode
    {
        get => Theme == ThemeMode.Dark;
        set => Theme = value ? ThemeMode.Dark : ThemeMode.Light;
    }

    public ViewMode ViewMode
    {
        get => _settings.ViewMode;
        set
        {
            if (_settings.ViewMode != value)
            {
                _settings.ViewMode = value;
                OnPropertyChanged();
                _queueSave();
            }
        }
    }

    public LayoutMode LayoutMode
    {
        get => _settings.LayoutMode;
        set
        {
            if (_settings.LayoutMode != value)
            {
                _settings.LayoutMode = value;
                OnPropertyChanged();
                _queueSave();
            }
        }
    }

    public CollectionsLayout CollectionsLayout
    {
        get => _settings.CollectionsLayout;
        set
        {
            if (_settings.CollectionsLayout != value)
            {
                _settings.CollectionsLayout = value;
                OnPropertyChanged();
                _queueSave();
            }
        }
    }

    public bool AlwaysOnTop
    {
        get => _settings.AlwaysOnTop;
        set
        {
            if (_settings.AlwaysOnTop != value)
            {
                _settings.AlwaysOnTop = value;
                OnPropertyChanged();
                _queueSave();
            }
        }
    }

    public bool AutoHide
    {
        get => _settings.AutoHide;
        set
        {
            if (_settings.AutoHide != value)
            {
                _settings.AutoHide = value;
                OnPropertyChanged();
                _queueSave();
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _settings.MinimizeToTray;
        set
        {
            if (_settings.MinimizeToTray != value)
            {
                _settings.MinimizeToTray = value;
                OnPropertyChanged();
                _queueSave();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set
        {
            if (_settings.StartWithWindows != value)
            {
                _settings.StartWithWindows = value;
                OnPropertyChanged();
                _startupService.SetStartup(value);
                _queueSave();
            }
        }
    }

    public bool EnableDebugMode
    {
        get => _settings.EnableDebugMode;
        set
        {
            if (_settings.EnableDebugMode != value)
            {
                _settings.EnableDebugMode = value;
                RuntimeStatus.DebugMode = value;
                LogService.Instance.Log($"Debug mode {(value ? "enabled" : "disabled")}.");
                DebugCopyStatus = value
                    ? "Debug mode enabled. Reproduce the issue, then click 'Copy Debug Report'."
                    : string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DebugModeStatusText));
                _queueSave();
            }
        }
    }

    public string DebugModeStatusText
        => $"Verbose Mtab diagnostics: {(EnableDebugMode ? "ON" : "OFF")} | Log: {RuntimeStatus.DebugLogFilePath}";

    public string DebugCopyStatus
    {
        get => _debugCopyStatus;
        private set => SetProperty(ref _debugCopyStatus, value);
    }

    public string ExplorerIntegrationStatusText
    {
        get => _explorerIntegrationStatusText;
        private set => SetProperty(ref _explorerIntegrationStatusText, value);
    }

    public bool IsExplorerIntegrationInstalled
    {
        get => _isExplorerIntegrationInstalled;
        private set => SetProperty(ref _isExplorerIntegrationInstalled, value);
    }

    public HotkeySettings GlobalHotkey
    {
        get => _settings.GlobalHotkey;
        set
        {
            _settings.GlobalHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GlobalHotkeyDisplay));
            _hotkeyChanged(value);
            OnPropertyChanged(nameof(HotkeyStatusMessage));
            _queueSave();
        }
    }

    public string GlobalHotkeyDisplay => _settings.GlobalHotkey.ToString();

    public string HotkeyStatusMessage => RuntimeStatus.HotkeyStatusMessage;

    public UiDensity UiDensity
    {
        get => _settings.UiDensity;
        set
        {
            if (_settings.UiDensity != value)
            {
                _settings.UiDensity = value;
                _densityService.ApplyDensity(value);
                OnPropertyChanged();
                _queueSave();
            }
        }
    }

    public double ItemScale
    {
        get => ClampItemScale(_settings.ItemScale);
        set
        {
            var clamped = ClampItemScale(value);
            if (Math.Abs(_settings.ItemScale - clamped) > 0.001)
            {
                _settings.ItemScale = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ItemScalePercentText));
                _queueSave();
            }
        }
    }

    public string ItemScalePercentText => $"{Math.Round(ItemScale * 100):0}%";

    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand OpenDuplicateManagerCommand { get; }
    public ICommand OpenDiagnosticsCommand { get; }
    public ICommand CopyDebugReportCommand { get; }
    public ICommand InstallExplorerIntegrationCommand { get; }
    public ICommand UninstallExplorerIntegrationCommand { get; }
    public ICommand RepairExplorerIntegrationCommand { get; }

    public void UpdateHotkey(HotkeySettings settings)
    {
        GlobalHotkey = settings;
    }

    public void ReloadFromSettings()
    {
        _themeService.ApplyTheme(_settings.Theme);
        _densityService.ApplyDensity(_settings.UiDensity);
        RuntimeStatus.DebugMode = _settings.EnableDebugMode;

        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(FollowSystemTheme));
        OnPropertyChanged(nameof(IsDarkMode));
        OnPropertyChanged(nameof(ViewMode));
        OnPropertyChanged(nameof(LayoutMode));
        OnPropertyChanged(nameof(CollectionsLayout));
        OnPropertyChanged(nameof(AlwaysOnTop));
        OnPropertyChanged(nameof(AutoHide));
        OnPropertyChanged(nameof(MinimizeToTray));
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(ItemScale));
        OnPropertyChanged(nameof(ItemScalePercentText));
        OnPropertyChanged(nameof(GlobalHotkey));
        OnPropertyChanged(nameof(GlobalHotkeyDisplay));
        OnPropertyChanged(nameof(HotkeyStatusMessage));
        OnPropertyChanged(nameof(UiDensity));
        OnPropertyChanged(nameof(EnableDebugMode));
        OnPropertyChanged(nameof(DebugModeStatusText));
        OnPropertyChanged(nameof(DebugCopyStatus));
        RefreshExplorerIntegrationStatus();
    }

    private static double ClampItemScale(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1.0;
        }

        return Math.Clamp(value, 0.75, 1.5);
    }

    private void CopyDebugReport()
    {
        try
        {
            var report = LogService.Instance.BuildDebugReport();
            System.Windows.Clipboard.SetText(report);
            DebugCopyStatus = $"Debug report copied ({DateTime.Now:HH:mm:ss}). Paste it in chat.";
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, "Failed to copy debug report.");
            DebugCopyStatus = "Could not copy debug report. Check clipboard access.";
        }
    }

    private void InstallExplorerIntegration()
    {
        try
        {
            _explorerIntegrationService.Install();
            RefreshExplorerIntegrationStatus();
            ExplorerIntegrationStatusText = "Explorer menu installed successfully.";
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, "Explorer integration install failed.");
            ExplorerIntegrationStatusText = $"Install failed: {ex.Message}";
        }
    }

    private void UninstallExplorerIntegration()
    {
        try
        {
            _explorerIntegrationService.Uninstall();
            RefreshExplorerIntegrationStatus();
            ExplorerIntegrationStatusText = "Explorer menu removed.";
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, "Explorer integration uninstall failed.");
            ExplorerIntegrationStatusText = $"Uninstall failed: {ex.Message}";
        }
    }

    private void RepairExplorerIntegration()
    {
        try
        {
            _explorerIntegrationService.Repair();
            RefreshExplorerIntegrationStatus();
            ExplorerIntegrationStatusText = "Explorer menu repaired.";
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, "Explorer integration repair failed.");
            ExplorerIntegrationStatusText = $"Repair failed: {ex.Message}";
        }
    }

    private void RefreshExplorerIntegrationStatus()
    {
        var status = _explorerIntegrationService.GetStatus();
        IsExplorerIntegrationInstalled = status.IsInstalled;
        ExplorerIntegrationStatusText = status.IsInstalled
            ? "Installed for current user."
            : "Not installed for current user.";

        RuntimeStatus.ExplorerMenuInstalled = status.IsInstalled;
        RuntimeStatus.ExplorerMenuStatusMessage = status.StatusMessage;
    }

}
