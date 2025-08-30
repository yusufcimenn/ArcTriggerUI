using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArcTriggerUI.Interfaces;

namespace ArcTriggerUI.Services
{
    public class TcpSocketService : ITcpSocketService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly StringBuilder _lineBuffer = new StringBuilder();

        public bool IsConnected => _client?.Connected == true;

        public event Action<string>? LineReceived;

        public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, ct);
            _stream = _client.GetStream();
        }

        public async Task DisconnectAsync()
        {
            try { if (_stream != null) await _stream.FlushAsync(); } catch { }
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }

        public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (_stream is null || !IsConnected) throw new IOException("Bağlı değil.");
            await _stream.WriteAsync(data, ct);
            await _stream.FlushAsync(ct);
        }

        public Task SendJsonAsync(object payload, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(payload) + "\n";
            return SendAsync(Encoding.UTF8.GetBytes(json), ct);
        }

        public Task SubscribeAsync(string[] conids, CancellationToken ct = default)
            => SendJsonAsync(new { op = "subscribe", conids = conids }, ct);

        public Task RequestSnapshotsAsync(string[] conids, CancellationToken ct = default)
            => SendJsonAsync(new { op = "snapshots", conids = conids }, ct);

        public Task UnsubscribeAsync(string[] conids, CancellationToken ct = default)
            => SendJsonAsync(new { op = "unsubscribe", conids = conids }, ct);

        public Task QuitAsync(CancellationToken ct = default)
            => SendJsonAsync(new { op = "quit" }, ct);

        public async Task StartNdjsonLoopAsync(CancellationToken ct = default)
        {
            if (_stream is null) throw new InvalidOperationException("Bağlı değil.");

            var buffer = new byte[8192];

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await _stream.ReadAsync(buffer.AsMemory(), ct);
                    if (read == 0) break;

                    var text = Encoding.UTF8.GetString(buffer, 0, read);
                    _lineBuffer.Append(text);

                    while (true)
                    {
                        var buf = _lineBuffer.ToString();
                        int nl = buf.IndexOf('\n');
                        if (nl < 0) break;

                        var line = buf[..nl].TrimEnd('\r');
                        _lineBuffer.Remove(0, nl + 1);

                        if (!string.IsNullOrWhiteSpace(line))
                            LineReceived?.Invoke(line);
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                await DisconnectAsync();
            }
        }
    }

}