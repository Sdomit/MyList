using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using MyList.Models;
using MyList.ViewModels;

namespace MyList.Services;

public sealed class NetworkCheckService : IDisposable
{
    private readonly DispatcherTimer _timer = new();
    private CancellationTokenSource? _cts;
    private MainViewModel? _viewModel;

    public void Initialize(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        _timer.Interval = TimeSpan.FromSeconds(30);
        _timer.Tick += OnTimerTick;
        _timer.Start();
        _ = CheckAsync();
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            await CheckAsync();
        }
        catch (Exception ex)
        {
            LogService.Instance.Log(ex, "Network check tick failed.");
        }
    }

    private async Task CheckAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var items = _viewModel.AllItems.ToList();
        RuntimeStatus.NetworkPendingCount = items.Count;

        foreach (var item in items)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            var state = await EvaluatePathStateAsync(item, token).ConfigureAwait(false);
            await _viewModel.Dispatcher.InvokeAsync(() => item.HealthState = state);
            RuntimeStatus.NetworkPendingCount = Math.Max(0, RuntimeStatus.NetworkPendingCount - 1);
        }

        RuntimeStatus.NetworkLastRunUtc = DateTime.UtcNow;
    }

    public async Task RetryCheckAsync(ItemModel item)
    {
        var state = await EvaluatePathStateAsync(item, CancellationToken.None).ConfigureAwait(false);
        if (_viewModel is not null)
        {
            await _viewModel.Dispatcher.InvokeAsync(() => item.HealthState = state);
        }
        else
        {
            item.HealthState = state;
        }

        RuntimeStatus.NetworkLastRunUtc = DateTime.UtcNow;
    }

    private static async Task<ItemHealthState> EvaluatePathStateAsync(ItemModel item, CancellationToken token)
    {
        if (item.IsClipboardText || item.IsClipboardImage || item.IsMtab || item.IsActionItem)
        {
            return ItemHealthState.Healthy;
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return ItemHealthState.Missing;
        }

        var timeout = item.Path.StartsWith("\\\\", StringComparison.Ordinal) ? 1500 : 500;
        var checkTask = Task.Run<ItemHealthState>(() =>
        {
            try
            {
                var exists = item.Type == ItemType.Folder
                    ? Directory.Exists(item.Path)
                    : File.Exists(item.Path);

                return exists ? ItemHealthState.Healthy : ItemHealthState.Missing;
            }
            catch (UnauthorizedAccessException)
            {
                return ItemHealthState.PermissionDenied;
            }
            catch (IOException)
            {
                return ItemHealthState.Offline;
            }
            catch
            {
                return ItemHealthState.Offline;
            }
        }, token);

        var completed = await Task.WhenAny(checkTask, Task.Delay(timeout, token)).ConfigureAwait(false);
        if (completed != checkTask)
        {
            return ItemHealthState.Offline;
        }

        return checkTask.Status == TaskStatus.RanToCompletion
            ? checkTask.Result
            : ItemHealthState.Offline;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
