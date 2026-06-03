using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyList.Models;

namespace MyList.Services;

public sealed class AppInstanceService : IDisposable
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly LogService _log = LogService.Instance;
    private Mutex? _mutex;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public AppInstanceService()
    {
        var sid = WindowsIdentity.GetCurrent()?.User?.Value ?? Environment.UserName;
        var safeSid = sid.Replace("\\", "_").Replace(":", "_").Replace("-", "_");
        _mutexName = $@"Local\MyList.AppInstance.v1.{safeSid}";
        _pipeName = $"MyList.AppCommand.v1.{safeSid}";
    }

    public bool TryAcquirePrimary()
    {
        _mutex = new Mutex(true, _mutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public void StartServer(Func<AppCommandPayload, Task> onCommand)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => RunServerLoopAsync(onCommand, _cts.Token));
    }

    public async Task<bool> TrySendToPrimaryAsync(AppCommandPayload payload, int timeoutMs = 2000)
    {
        var json = JsonSerializer.Serialize(payload);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                await client.ConnectAsync(timeoutMs).ConfigureAwait(false);
                await using var writer = new StreamWriter(client) { AutoFlush = true };
                await writer.WriteAsync(json).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                await Task.Delay(120).ConfigureAwait(false);
            }
            catch (IOException)
            {
                await Task.Delay(120).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Log(ex, "Failed to forward app command to primary instance.");
                await Task.Delay(120).ConfigureAwait(false);
            }
        }

        return false;
    }

    private async Task RunServerLoopAsync(Func<AppCommandPayload, Task> onCommand, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var payloadJson = await reader.ReadToEndAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(payloadJson))
                {
                    continue;
                }

                var payload = JsonSerializer.Deserialize<AppCommandPayload>(payloadJson);
                if (payload is null)
                {
                    continue;
                }

                await onCommand(payload).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Log(ex, "Named-pipe app command server loop error.");
            }
        }
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _serverTask?.Wait(500);
        }
        catch
        {
            // ignored
        }
        finally
        {
            _cts?.Dispose();
            _mutex?.Dispose();
            _cts = null;
            _mutex = null;
            _serverTask = null;
        }
    }
}

