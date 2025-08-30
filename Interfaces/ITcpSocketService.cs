using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Interfaces
{
    public interface ITcpSocketService : IAsyncDisposable
    {
        // Ham TCP + NDJSON kolaylıkları içeren servis arayüzü
     
            // Sunucuya bağlı mı?
            bool IsConnected { get; }

            // Alttaki ham Socket (gelişmiş ayarlar için)
            Socket? RawSocket { get; }

            // Aktif bağlantının NetworkStream'i (protokol üst katmanları kullanabilir)
            NetworkStream Stream { get; }

            // Host:Port'a bağlan
            Task ConnectAsync(string host, int port, CancellationToken ct = default);

            // Bağlantıyı kapat
            Task DisconnectAsync();

            // Ham byte[] gönder
            Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

            // Metin gönder (varsayılan UTF8)
            Task SendStringAsync(string text, Encoding? encoding = null, CancellationToken ct = default);

            // Sürekli okuma döngüsü (ham chunk bazlı). Mesaj sınırı/framing yapmaz.
            // Protokol framing gerekiyorsa callback içinde biriktirme/parsing yapılmalıdır.
            Task StartReceiveLoopAsync(Func<ReadOnlyMemory<byte>, Task> onChunk, CancellationToken ct = default);



            // NDJSON: JSON objesini serialize edip '\n' ekleyerek gönder
            Task SendJsonAsync(object payload, CancellationToken ct = default);

            // NDJSON: satır geldiğinde tetiklenen event (ham JSON satırı)
            event Action<string>? LineReceived;

            // NDJSON: stream'i satır satır oku, her satırda LineReceived tetikle
            Task StartNdjsonLoopAsync(CancellationToken ct = default);

            // Kolaylık: subscribe/snapshots/unsubscribe/quit mesajlarını gönder
            Task SubscribeAsync(IEnumerable<string> conids, CancellationToken ct = default);
            Task RequestSnapshotsAsync(IEnumerable<string> conids, CancellationToken ct = default);
            Task UnsubscribeAsync(IEnumerable<string> conids, CancellationToken ct = default);
            Task QuitAsync(CancellationToken ct = default);
        }
    
}
