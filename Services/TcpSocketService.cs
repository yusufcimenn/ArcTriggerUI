using ArcTriggerUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Services
{
    public class TcpSocketService : ITcpSocketService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;

        // NDJSON satır biriktirme buffer'ı
        private readonly StringBuilder _lineBuffer = new StringBuilder();

        // Sunucuya bağlı mı?
        public bool IsConnected => _client?.Connected == true;

        // Alttaki ham Socket
        public Socket? RawSocket => _client?.Client
            ?? throw new InvalidOperationException("Henüz bağlı değil.");

        // Aktif bağlantının NetworkStream'i
        public NetworkStream Stream => _stream
            ?? throw new InvalidOperationException("Henüz bağlı değil.");

        // Host:Port'a bağlan
        public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
        {
            _client = new TcpClient
            {
                NoDelay = true,                          // Paket gecikmesini azaltır
                LingerState = new LingerOption(false, 0) // Kapanırken bekleme yapmaz
            };

            await _client.ConnectAsync(host, port, ct);
            _stream = _client.GetStream();

            // İsteğe bağlı: TCP KeepAlive
            try { RawSocket!.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true); }
            catch { /* bazı platformlarda desteklenmeyebilir */ }
        }

        // Bağlantıyı kapat
        public async Task DisconnectAsync()
        {
            try { if (_stream != null) await _stream.FlushAsync(); } catch { }
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;
        }

        // Ham byte[] gönder
        public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (_stream is null || !IsConnected) throw new IOException("Bağlı değil veya bağlantı koptu.");
            await _stream.WriteAsync(data, ct);
            await _stream.FlushAsync(ct);
        }

        // Metin gönder (varsayılan UTF8)
        public Task SendStringAsync(string text, Encoding? encoding = null, CancellationToken ct = default)
        {
            encoding ??= Encoding.UTF8;
            return SendAsync(encoding.GetBytes(text), ct);
        }

        // Ham chunk bazlı okuma döngüsü (framing/parsing yapmaz)
        public async Task StartReceiveLoopAsync(Func<ReadOnlyMemory<byte>, Task> onChunk, CancellationToken ct = default)
        {
            if (_stream is null) throw new InvalidOperationException("Bağlı değil.");
            var buffer = new byte[8192];

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await _stream.ReadAsync(buffer.AsMemory(), ct);
                    if (read == 0) break; // bağlantı kapandı
                    await onChunk(buffer.AsMemory(0, read));
                }
            }
            catch (OperationCanceledException) { /* iptal normal */ }
            finally
            {
                await DisconnectAsync();
            }
        }



        // JSON objesini serialize edip '\n' ekleyerek gönder
        public Task SendJsonAsync(object payload, CancellationToken ct = default)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload) + "\n";
            return SendStringAsync(json, Encoding.UTF8, ct);
        }

        // NDJSON satır geldiğinde tetiklenen event
        public event Action<string>? LineReceived;

        // Stream'i satır satır oku; newline gördükçe LineReceived tetikle
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

                        var line = buf[..nl].TrimEnd('\r');   // \r\n desteği
                        _lineBuffer.Remove(0, nl + 1);

                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            try { LineReceived?.Invoke(line); } catch { /* yut */ }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* iptal normal */ }
            finally
            {
                await DisconnectAsync();
            }
        }

        // Kolaylık: {"op":"subscribe","conids":[...]}
        public Task SubscribeAsync(IEnumerable<string> conids, CancellationToken ct = default)
            => SendJsonAsync(new { op = "subscribe", conids = conids.ToArray() }, ct);

        // Kolaylık: {"op":"snapshots","conids":[...]}
        public Task RequestSnapshotsAsync(IEnumerable<string> conids, CancellationToken ct = default)
            => SendJsonAsync(new { op = "snapshots", conids = conids.ToArray() }, ct);

        // Kolaylık: {"op":"unsubscribe","conids":[...] }
        public Task UnsubscribeAsync(IEnumerable<string> conids, CancellationToken ct = default)
            => SendJsonAsync(new { op = "unsubscribe", conids = conids.ToArray() }, ct);

        // Kolaylık: {"op":"quit"}
        public Task QuitAsync(CancellationToken ct = default)
            => SendJsonAsync(new { op = "quit" }, ct);

        // Dispose: bağlantıyı kapat
        public async ValueTask DisposeAsync() => await DisconnectAsync();

    }
}
