using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Pupstrap.Core;

namespace Pupstrap.Integrations;

public sealed class DiscordRpc : IDisposable
{
    private readonly string _clientId;
    private NamedPipeClientStream? _pipe;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DiscordRpc(string clientId) => _clientId = clientId;

    private async Task<bool> EnsureConnectedAsync(CancellationToken token)
    {
        if (_pipe is { IsConnected: true }) return true;
        _pipe?.Dispose();
        _pipe = null;
        for (var i = 0; i < 10; i++)
        {
            var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(300, token);
                _pipe = pipe;
                await SendAsync(0, new { v = 1, client_id = _clientId }, token);
                await ReadFrameAsync(token);
                Logger.Info($"Connected to Discord on discord-ipc-{i}");
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException)
            {
                pipe.Dispose();
                _pipe = null;
            }
        }
        return false;
    }

    public async Task SetActivityAsync(object? activity, CancellationToken token = default)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (!await EnsureConnectedAsync(token)) return;
            await SendAsync(1, new
            {
                cmd = "SET_ACTIVITY",
                args = new { pid = Environment.ProcessId, activity },
                nonce = Guid.NewGuid().ToString(),
            }, token);
            await ReadFrameAsync(token);
        }
        catch (IOException ex)
        {
            Logger.Warn($"Discord presence update failed: {ex.Message}");
            _pipe?.Dispose();
            _pipe = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SendAsync(int opcode, object payload, CancellationToken token)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        var frame = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0), opcode);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), body.Length);
        body.CopyTo(frame, 8);
        await _pipe!.WriteAsync(frame, token);
        await _pipe.FlushAsync(token);
    }

    private async Task<string> ReadFrameAsync(CancellationToken token)
    {
        var header = new byte[8];
        await _pipe!.ReadExactlyAsync(header, token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (length is < 0 or > 1 << 20) throw new IOException("Bad frame from Discord.");
        var body = new byte[length];
        await _pipe.ReadExactlyAsync(body, token);
        return Encoding.UTF8.GetString(body);
    }

    public void Dispose()
    {
        _pipe?.Dispose();
        _gate.Dispose();
    }
}
