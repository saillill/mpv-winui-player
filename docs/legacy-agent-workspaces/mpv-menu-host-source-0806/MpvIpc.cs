using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MpvMenuHost;

/// <summary>mpv JSON IPC 客户端（named pipe）。</summary>
public sealed class MpvIpc : IDisposable
{
    private NamedPipeClientStream? _pipe;

    public static string NormalizePipeName(string arg)
    {
        const string prefix = @"\\.\pipe\";
        return arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? arg[prefix.Length..] : arg;
    }

    public async Task<bool> ConnectAsync(string name, int timeoutMs)
    {
        try
        {
            _pipe = new NamedPipeClientStream(".", NormalizePipeName(name),
                PipeDirection.InOut, PipeOptions.Asynchronous);
            using var cts = new CancellationTokenSource(timeoutMs);
            await _pipe.ConnectAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> SendAsync(string[] command, int requestId, int timeoutMs)
    {
        if (_pipe == null) return null;
        try
        {
            var line = JsonSerializer.Serialize(new { command, request_id = requestId }) + "\n";
            var bytes = Encoding.UTF8.GetBytes(line);
            await _pipe.WriteAsync(bytes, CancellationToken.None);
            await _pipe.FlushAsync(CancellationToken.None);

            using var cts = new CancellationTokenSource(timeoutMs);
            var buf = new byte[4096];
            var ms = new MemoryStream();
            while (true)
            {
                int n = await _pipe.ReadAsync(buf, cts.Token);
                if (n == 0) return null;
                int nl = Array.IndexOf(buf, (byte)'\n', 0, n);
                if (nl >= 0)
                {
                    ms.Write(buf, 0, nl);
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
                ms.Write(buf, 0, n);
            }
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _pipe?.Dispose();
}
