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
    private readonly SkinService _skinService;
    private readonly Action _queueSave;
    private readonly Action<HotkeySettings> _hotkeyChanged;
    private readonly Action<HotkeySettings> _miniLauncherHotkeyChanged;
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
    private string? _pendingSectionAnchor;
    public event EventHandler<bool>? FollowSystemThemeChanged;

    public SettingsViewModel(
        AppSettings settings,
        ThemeService themeService,
        DensityService densityService,
        SkinService skinService,
        Action queueSave,
        Action<HotkeySettings> hotkeyChanged,
        Action<HotkeySettings> miniLauncherHotkeyChanged,
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
        _skinService = skinService;
        _queueSave = queueSave;
        _hotkeyChanged = hotkeyChanged;
        _miniLauncherHotkeyChanged = miniLauncherHotkeyChanged;
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
        SkinModes = Enum.GetValues<UiSkin>();
        RuntimeStatus.DebugMode = _settings.EnableDebugMode;
        RefreshExplorerIntegrationStatus();
    }

    public AppSettings AppSettings => _settings;

    public Array LayoutModes { get; }

    public Array CollectionsLayouts { get; }

    public Array DensityModes { get; }

    public Array SkinModes { get; }

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
                _themeService.ApplyTheme(value, _settings.Accent);
                _queueSave();
            }
        }
    }

    public AccentPalette Accent
    {
        get => _settings.Accent;
        set
        {
            if (_settings.Accent != value)
            {
                _settings.Accent = value;
                OnPropertyChanged();
                _themeService.ApplyTheme(_settings.Theme, value);
                _queueSave();
            }
        }
    }

    public Array AccentPalettes { get; } = Enum.GetValues<AccentPalette>();

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

    public HotkeySettings MiniLauncherHotkey
    {
        get => _settings.MiniLauncherHotkey;
        set
        {
            _settings.MiniLauncherHotkey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MiniLauncherHotkeyDisplay));
            _miniLauncherHotkeyChanged(value);
            _queueSave();
        }
    }

    public string MiniLauncherHotkeyDisplay => _settings.MiniLauncherHotkey.ToString();

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

    public UiSkin Skin
    {
        get => _settings.Skin;
        set
        {
            if (_settings.Skin != value)
            {
                _settings.Skin = value;
                _skinService.ApplySkin(value);
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

    public string? PendingSectionAnchor
    {
        get => _pendingSectionAnchor;
        set => SetProperty(ref _pendingSectionAnchor, value);
    }

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

    public void UpdateMiniLauncherHotkey(HotkeySettings settings)
    {
        MiniLauncherHotkey = settings;
    }

    public void ReloadFromSettings()
    {
        _themeService.ApplyTheme(_settings.Theme, _settings.Accent);
        _densityService.ApplyDensity(_settings.UiDensity);
        _skinService.ApplySkin(_settings.Skin);
        RuntimeStatus.DebugMode = _settings.EnableDebugMode;

        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(Accent));
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
        OnPropertyChanged(nameof(MiniLauncherHotkey));
        OnPropertyChanged(nameof(MiniLauncherHotkeyDisplay));
        OnPropertyChanged(nameof(UiDensity));
        OnPropertyChanged(nameof(Skin));
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
