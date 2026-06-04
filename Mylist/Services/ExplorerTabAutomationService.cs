using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MyList.Helpers;
using MyList.Models;
using SendKeys = System.Windows.Forms.SendKeys;

namespace MyList.Services;

public sealed class ExplorerTabAutomationService
{
    private const int SwRestore = 9;
    private static readonly TimeSpan NewTabTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan NavigationVerificationTimeout = TimeSpan.FromSeconds(4);
    private readonly LogService _log = LogService.Instance;

    private static bool IsDebugModeEnabled => RuntimeStatus.DebugMode;

    private void DebugLog(string message)
    {
        if (!IsDebugModeEnabled)
        {
            return;
        }

        _log.Log($"[MTAB DEBUG] {message}");
    }

    public async Task<ExplorerMtabOpenResult> OpenMtabAsync(IReadOnlyList<string> folderPaths, CancellationToken ct = default)
    {
        var distinctPaths = folderPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        DebugLog($"OpenMtab start. Requested={folderPaths.Count}, distinct={distinctPaths.Count}, paths=[{string.Join(" | ", distinctPaths)}]");

        var result = new ExplorerMtabOpenResult
        {
            RequestedCount = distinctPaths.Count
        };

        System.Windows.IDataObject? previousClipboard = null;
        try
        {
            previousClipboard = System.Windows.Clipboard.GetDataObject();
        }
        catch
        {
            // Clipboard may be unavailable; continue without restore.
        }

        if (distinctPaths.Count == 0)
        {
            return result;
        }

        if (distinctPaths.Count == 1)
        {
            OpenExplorerWindow(distinctPaths[0]);
            result.OpenedAsWindowsCount = 1;
            DebugLog($"Single-path mtab. Opened in one window: {distinctPaths[0]}");
            return result;
        }
        try
        {
            var existingHandles = GetExplorerWindowHandles();
            DebugLog($"Existing Explorer windows before open: {existingHandles.Count}");
            OpenExplorerWindow(distinctPaths[0]);
            DebugLog($"Opened anchor Explorer window path: {distinctPaths[0]}");

            var targetHandle = await WaitForNewExplorerWindowAsync(existingHandles, TimeSpan.FromSeconds(5), ct).ConfigureAwait(true);
            if (targetHandle == IntPtr.Zero)
            {
                result.TabsSupported = false;
                result.OpenedAsWindowsCount = 1;
                var remainingPaths = distinctPaths.Skip(1).ToList();
                await OpenAsSeparateWindowsAsync(remainingPaths).ConfigureAwait(true);
                result.OpenedAsWindowsCount += remainingPaths.Count;
                result.FallbackPaths.AddRange(remainingPaths);
                DebugLog($"Failed to detect target Explorer handle for tab automation. Fallback windows opened: {remainingPaths.Count}");
                return result;
            }

            DebugLog($"Target Explorer handle detected: 0x{targetHandle.ToInt64():X}");

            result.TabsSupported = true;
            result.OpenedAsTabsCount = 1;

            foreach (var path in distinctPaths.Skip(1))
            {
                ct.ThrowIfCancellationRequested();
                DebugLog($"Attempting tab open for path: {path}");
                var openedAsTab = await TryOpenPathInTabAsync(targetHandle, path, ct).ConfigureAwait(true);
                if (openedAsTab)
                {
                    result.OpenedAsTabsCount++;
                    DebugLog($"Tab open success: {path}");
                    continue;
                }

                result.FailedPaths.Add(path);
                DebugLog($"Tab open failed. Attempting home-tab cleanup for path: {path}");
                if (await TryCloseActiveTabAsync(targetHandle, ct).ConfigureAwait(true))
                {
                    result.CleanedHomeTabsCount++;
                    DebugLog($"Home tab cleaned after failure: {path}");
                }

                OpenExplorerWindow(path);
                result.OpenedAsWindowsCount++;
                result.FallbackPaths.Add(path);
                DebugLog($"Fallback window opened for path: {path}");
            }

            DebugLog($"OpenMtab complete. {result.BuildSummary()}");
            return result;
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Explorer tab automation failed, falling back to separate windows.");
            await OpenAsSeparateWindowsAsync(distinctPaths).ConfigureAwait(true);
            result.TabsSupported = false;
            result.OpenedAsTabsCount = 0;
            result.OpenedAsWindowsCount = distinctPaths.Count;
            result.FallbackPaths = distinctPaths.ToList();
            DebugLog($"OpenMtab hard fallback. {result.BuildSummary()}");
            return result;
        }
        finally
        {
            if (previousClipboard is not null)
            {
                try
                {
                    System.Windows.Clipboard.SetDataObject(previousClipboard, true);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    public async Task OpenAsSeparateWindowsAsync(IReadOnlyList<string> folderPaths)
    {
        foreach (var path in folderPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            OpenExplorerWindow(path);
            await Task.Delay(80).ConfigureAwait(true);
        }
    }

    public IReadOnlyList<string> CaptureOpenFolderPaths()
    {
        var paths = new List<string>();
        foreach (var handle in GetExplorerWindowHandles())
        {
            var snapshots = GetShellTabSnapshotsByHwnd(handle);
            try
            {
                foreach (var snapshot in snapshots)
                {
                    if (TryGetPathFromLocationUrl(snapshot.LocationUrl, out var path, out _))
                    {
                        paths.Add(path);
                    }
                }
            }
            finally
            {
                DisposeSnapshots(snapshots);
            }
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<bool> TryOpenPathInTabAsync(IntPtr explorerHandle, string path, CancellationToken ct)
    {
        if (!PathNormalizationHelper.TryNormalizeUserPath(path, out _, out var expectedPathKey))
        {
            _log.Log($"Skipping tab open for invalid path: {path}");
            return false;
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (!await ActivateExplorerWindowAsync(explorerHandle, ct).ConfigureAwait(true))
            {
                DebugLog($"Activate failed. Handle=0x{explorerHandle.ToInt64():X}, path={path}, attempt={attempt}");
                await Task.Delay(120, ct).ConfigureAwait(true);
                continue;
            }

            var preOpenSnapshots = GetShellTabSnapshotsByHwnd(explorerHandle);
            var preOpenIdentitySet = preOpenSnapshots
                .Select(snapshot => snapshot.ComIdentity.ToInt64())
                .ToHashSet();
            DebugLog($"Pre-open tab snapshot count={preOpenSnapshots.Count}, path={path}, attempt={attempt}");
            DisposeSnapshots(preOpenSnapshots);

            ExplorerTabSnapshot? newTabSnapshot = null;
            try
            {
                SendKeys.SendWait("^t");
                await Task.Delay(220, ct).ConfigureAwait(true);

                newTabSnapshot = await WaitForNewTabSnapshot(explorerHandle, preOpenIdentitySet, NewTabTimeout, ct).ConfigureAwait(true);
                if (newTabSnapshot is null)
                {
                    _log.Log($"Unable to capture newly created explorer tab for '{path}' (attempt {attempt}), continuing with active-tab keyboard navigation.");
                    DebugLog($"No new tab snapshot detected. path={path}, attempt={attempt}. Will continue with active tab navigation.");
                }
                else
                {
                    DebugLog($"New tab snapshot detected. identity=0x{newTabSnapshot.ComIdentity.ToInt64():X}, initialUrl='{newTabSnapshot.LocationUrl}', path={path}, attempt={attempt}");
                }

                if (!await TryNavigateActiveTabWithAddressBarAsync(explorerHandle, path, ct).ConfigureAwait(true))
                {
                    DebugLog($"Keyboard navigation command failed. path={path}, attempt={attempt}");
                    continue;
                }

                if (await TryVerifyPathInExplorerWindowAsync(explorerHandle, expectedPathKey, NavigationVerificationTimeout, ct).ConfigureAwait(true))
                {
                    DebugLog($"Keyboard navigation verification success. path={path}, attempt={attempt}");
                    return true;
                }

                DebugLog($"Keyboard navigation verification failed. path={path}, attempt={attempt}");
            }
            catch (Exception ex)
            {
                _log.Log(ex, $"Failed to open explorer tab for path: {path}. Attempt: {attempt}.");
                await Task.Delay(120, ct).ConfigureAwait(true);
            }
            finally
            {
                newTabSnapshot?.Dispose();
            }
        }

        return false;
    }

    private static void DisposeSnapshots(IEnumerable<ExplorerTabSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            snapshot.Dispose();
        }
    }

    private async Task<ExplorerTabSnapshot?> WaitForNewTabSnapshot(
        IntPtr explorerHandle,
        IReadOnlyCollection<long> existingIdentitySet,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var snapshots = GetShellTabSnapshotsByHwnd(explorerHandle);
            ExplorerTabSnapshot? discovered = null;
            foreach (var snapshot in snapshots)
            {
                if (existingIdentitySet.Contains(snapshot.ComIdentity.ToInt64()))
                {
                    continue;
                }

                discovered = snapshot;
                break;
            }

            if (discovered is not null)
            {
                foreach (var snapshot in snapshots)
                {
                    if (!ReferenceEquals(snapshot, discovered))
                    {
                        snapshot.Dispose();
                    }
                }

                DebugLog($"WaitForNewTabSnapshot success. Handle=0x{explorerHandle.ToInt64():X}, identity=0x{discovered.ComIdentity.ToInt64():X}, location='{discovered.LocationUrl}'");
                return discovered;
            }

            DisposeSnapshots(snapshots);
            await Task.Delay(80, ct).ConfigureAwait(true);
        }

        return null;
    }

    private async Task<bool> TryNavigateActiveTabWithAddressBarAsync(IntPtr explorerHandle, string path, CancellationToken ct)
    {
        try
        {
            if (!await ActivateExplorerWindowAsync(explorerHandle, ct).ConfigureAwait(true))
            {
                return false;
            }

            System.Windows.Clipboard.SetText(path);
            SendKeys.SendWait("%d");
            await Task.Delay(120, ct).ConfigureAwait(true);
            SendKeys.SendWait("^a");
            await Task.Delay(80, ct).ConfigureAwait(true);
            SendKeys.SendWait("^v");
            await Task.Delay(120, ct).ConfigureAwait(true);
            SendKeys.SendWait("{ENTER}");
            await Task.Delay(280, ct).ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(ex, $"Keyboard tab navigation failed for path: {path}");
            return false;
        }
    }

    private async Task<bool> TryVerifyPathInExplorerWindowAsync(
        IntPtr explorerHandle,
        string expectedPathKey,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var snapshots = GetShellTabSnapshotsByHwnd(explorerHandle);
            try
            {
                var matched = false;
                foreach (var snapshot in snapshots)
                {
                    var pathKey = ToPathKeyFromLocationUrl(snapshot.LocationUrl);
                    if (string.IsNullOrWhiteSpace(pathKey))
                    {
                        continue;
                    }

                    if (string.Equals(pathKey, expectedPathKey, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    return true;
                }
            }
            finally
            {
                DisposeSnapshots(snapshots);
            }

            await Task.Delay(120, ct).ConfigureAwait(true);
        }

        DebugLog($"Path not found in Explorer tabs before timeout. handle=0x{explorerHandle.ToInt64():X}, expectedPathKey={expectedPathKey}");
        return false;
    }

    private async Task<bool> TryCloseActiveTabAsync(IntPtr explorerHandle, CancellationToken ct)
    {
        if (explorerHandle == IntPtr.Zero)
        {
            return false;
        }

        if (!await ActivateExplorerWindowAsync(explorerHandle, ct).ConfigureAwait(true))
        {
            return false;
        }

        var beforeCount = GetShellTabCount(explorerHandle);
        if (beforeCount <= 1)
        {
            return false;
        }

        try
        {
            SendKeys.SendWait("^w");
            await Task.Delay(240, ct).ConfigureAwait(true);
            var afterCount = GetShellTabCount(explorerHandle);
            DebugLog($"TryCloseActiveTab result. handle=0x{explorerHandle.ToInt64():X}, before={beforeCount}, after={afterCount}");
            return afterCount >= 0 && afterCount < beforeCount;
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to close unresolved explorer tab.");
            return false;
        }
    }

    private int GetShellTabCount(IntPtr explorerHandle)
    {
        var snapshots = GetShellTabSnapshotsByHwnd(explorerHandle);
        var count = snapshots.Count;
        DisposeSnapshots(snapshots);
        return count;
    }

    private List<ExplorerTabSnapshot> GetShellTabSnapshotsByHwnd(IntPtr explorerHandle)
    {
        var snapshots = new List<ExplorerTabSnapshot>();
        object? shellObject = null;
        object? windowsObject = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
            {
                return snapshots;
            }

            shellObject = Activator.CreateInstance(shellType);
            if (shellObject is null)
            {
                return snapshots;
            }

            dynamic shell = shellObject;
            windowsObject = shell.Windows();
            if (windowsObject is null)
            {
                return snapshots;
            }

            dynamic windows = windowsObject;
            var count = (int)windows.Count;
            for (var i = 0; i < count; i++)
            {
                object? windowObject = null;
                var keepWindowObject = false;
                try
                {
                    windowObject = windows.Item(i);
                    if (windowObject is null)
                    {
                        continue;
                    }

                    if (!TryGetWindowHandle(windowObject, out var hwnd) || hwnd != explorerHandle)
                    {
                        continue;
                    }

                    var identity = Marshal.GetIUnknownForObject(windowObject);
                    var locationUrl = GetLocationUrl(windowObject);
                    snapshots.Add(new ExplorerTabSnapshot(hwnd, identity, locationUrl, windowObject));
                    keepWindowObject = true;
                }
                catch (Exception ex)
                {
                    _log.Log(ex, "Failed to snapshot explorer tab.");
                }
                finally
                {
                    if (!keepWindowObject && windowObject is not null && Marshal.IsComObject(windowObject))
                    {
                        Marshal.FinalReleaseComObject(windowObject);
                    }
                }
            }

            DebugLog($"Shell snapshots for handle 0x{explorerHandle.ToInt64():X}: {snapshots.Count}");
            return snapshots;
        }
        catch (Exception ex)
        {
            _log.Log(ex, "Failed to enumerate explorer shell tabs.");
            DisposeSnapshots(snapshots);
            return new List<ExplorerTabSnapshot>();
        }
        finally
        {
            if (windowsObject is not null && Marshal.IsComObject(windowsObject))
            {
                Marshal.FinalReleaseComObject(windowsObject);
            }

            if (shellObject is not null && Marshal.IsComObject(shellObject))
            {
                Marshal.FinalReleaseComObject(shellObject);
            }
        }
    }

    private static bool TryGetWindowHandle(object windowObject, out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        try
        {
            dynamic window = windowObject;
            hwnd = new IntPtr(Convert.ToInt64(window.HWND));
            return hwnd != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    private static string GetLocationUrl(object windowObject)
    {
        try
        {
            dynamic window = windowObject;
            return (string?)window.LocationURL ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? ToPathKeyFromLocationUrl(string? locationUrl)
    {
        return TryGetPathFromLocationUrl(locationUrl, out _, out var pathKey)
            ? pathKey
            : null;
    }

    private static bool TryGetPathFromLocationUrl(string? locationUrl, out string normalizedPath, out string pathKey)
    {
        normalizedPath = string.Empty;
        pathKey = string.Empty;
        if (string.IsNullOrWhiteSpace(locationUrl))
        {
            return false;
        }

        var trimmedLocation = locationUrl.Trim();
        string? pathCandidate = null;

        if (Uri.TryCreate(trimmedLocation, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            pathCandidate = uri.IsUnc
                ? @"\\" + uri.Host + Uri.UnescapeDataString(uri.AbsolutePath).Replace('/', '\\')
                : Uri.UnescapeDataString(uri.LocalPath);
        }
        else if (trimmedLocation.StartsWith(@"\\", StringComparison.Ordinal)
                 || trimmedLocation.IndexOf(':') > 0)
        {
            pathCandidate = trimmedLocation;
        }

        if (string.IsNullOrWhiteSpace(pathCandidate))
        {
            return false;
        }

        return PathNormalizationHelper.TryNormalizeUserPath(pathCandidate, out normalizedPath, out pathKey);
    }

    private static void OpenExplorerWindow(string path)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
        {
            UseShellExecute = true
        });
    }

    private async Task<IntPtr> WaitForNewExplorerWindowAsync(
        IReadOnlyCollection<IntPtr> existingHandles,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var currentHandles = GetExplorerWindowHandles();
            var candidate = currentHandles.FirstOrDefault(handle => !existingHandles.Contains(handle));
            if (candidate != IntPtr.Zero)
            {
                return candidate;
            }

            await Task.Delay(120, ct).ConfigureAwait(true);
        }

        return IntPtr.Zero;
    }

    private async Task<bool> ActivateExplorerWindowAsync(IntPtr handle, CancellationToken ct)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (IsIconic(handle))
            {
                ShowWindowAsync(handle, SwRestore);
            }

            SetForegroundWindow(handle);
            BringWindowToTop(handle);
            await Task.Delay(80, ct).ConfigureAwait(true);

            if (GetForegroundWindow() == handle)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyCollection<IntPtr> GetExplorerWindowHandles()
    {
        var handles = new List<IntPtr>();
        EnumWindows((hWnd, _) =>
        {
            if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd))
            {
                return true;
            }

            var className = new StringBuilder(256);
            _ = GetClassName(hWnd, className, className.Capacity);
            if (!string.Equals(className.ToString(), "CabinetWClass", StringComparison.Ordinal))
            {
                return true;
            }

            handles.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        return handles;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    private sealed class ExplorerTabSnapshot : IDisposable
    {
        private bool _disposed;

        public ExplorerTabSnapshot(IntPtr hwnd, IntPtr comIdentity, string locationUrl, object windowObject)
        {
            Hwnd = hwnd;
            ComIdentity = comIdentity;
            LocationUrl = locationUrl;
            WindowObject = windowObject;
        }

        public IntPtr Hwnd { get; }
        public IntPtr ComIdentity { get; }
        public string LocationUrl { get; }
        public object WindowObject { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (ComIdentity != IntPtr.Zero)
            {
                Marshal.Release(ComIdentity);
            }

            if (Marshal.IsComObject(WindowObject))
            {
                Marshal.FinalReleaseComObject(WindowObject);
            }
        }
    }
}
