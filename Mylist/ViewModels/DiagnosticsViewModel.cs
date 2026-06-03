using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using MyList.Helpers;
using MyList.Services;

namespace MyList.ViewModels;

public sealed class DiagnosticsViewModel : ViewModelBase
{
    private readonly DispatcherTimer _refreshTimer;
    private string _hotkeyStatus = "Unknown";
    private string _effectiveHotkey = "None";
    private string _trayStatus = "Unknown";
    private string _networkStatus = "Unknown";
    private string _debugStatus = "Debug mode: Off";
    private string _explorerIntegrationStatus = "Explorer integration: Unknown";

    public DiagnosticsViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh);
        LogTail = new ObservableCollection<string>();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += (_, _) => Refresh();
        _refreshTimer.Start();

        Refresh();
    }

    public ICommand RefreshCommand { get; }

    public ObservableCollection<string> LogTail { get; }

    public string HotkeyStatus
    {
        get => _hotkeyStatus;
        private set => SetProperty(ref _hotkeyStatus, value);
    }

    public string EffectiveHotkey
    {
        get => _effectiveHotkey;
        private set => SetProperty(ref _effectiveHotkey, value);
    }

    public string TrayStatus
    {
        get => _trayStatus;
        private set => SetProperty(ref _trayStatus, value);
    }

    public string NetworkStatus
    {
        get => _networkStatus;
        private set => SetProperty(ref _networkStatus, value);
    }

    public string DebugStatus
    {
        get => _debugStatus;
        private set => SetProperty(ref _debugStatus, value);
    }

    public string ExplorerIntegrationStatus
    {
        get => _explorerIntegrationStatus;
        private set => SetProperty(ref _explorerIntegrationStatus, value);
    }

    public void Refresh()
    {
        HotkeyStatus = RuntimeStatus.HotkeyStatusMessage;
        EffectiveHotkey = RuntimeStatus.EffectiveHotkey.ToString();
        TrayStatus = RuntimeStatus.TrayInitialized
            ? (RuntimeStatus.TrayVisible ? "Initialized / Visible" : "Initialized / Hidden")
            : "Not initialized";

        NetworkStatus = RuntimeStatus.NetworkLastRunUtc is null
            ? "No checks yet"
            : $"Last: {RuntimeStatus.NetworkLastRunUtc:HH:mm:ss} UTC, pending: {RuntimeStatus.NetworkPendingCount}";
        DebugStatus = RuntimeStatus.DebugMode
            ? $"Debug mode ON | {RuntimeStatus.DebugLogFilePath}"
            : "Debug mode OFF";
        ExplorerIntegrationStatus = RuntimeStatus.ExplorerMenuInstalled
            ? $"Explorer menu installed | {RuntimeStatus.ExplorerMenuStatusMessage} | Last: {FormatUtc(RuntimeStatus.ExplorerMenuLastOperationUtc)}"
            : $"Explorer menu not installed | {RuntimeStatus.ExplorerMenuStatusMessage} | Last: {FormatUtc(RuntimeStatus.ExplorerMenuLastOperationUtc)}";

        LogTail.Clear();
        foreach (var line in LogService.Instance.ReadTail(30))
        {
            LogTail.Add(line);
        }
    }

    private static string FormatUtc(DateTime? value)
    {
        return value is null ? "-" : $"{value:yyyy-MM-dd HH:mm:ss} UTC";
    }
}
