using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Text;

namespace ArcTriggerUI.Interfaces
{
    public interface ITcpSocketService : IAsyncDisposable
    {
        bool IsConnected { get; }

        Task ConnectAsync(string host, int port, CancellationToken ct = default);
        Task DisconnectAsync();

        Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
        Task SendJsonAsync(object payload, CancellationToken ct = default);

        Task SubscribeAsync(string[] conids, CancellationToken ct = default);
        Task RequestSnapshotsAsync(string[] conids, CancellationToken ct = default);
        Task UnsubscribeAsync(string[] conids, CancellationToken ct = default);
        Task QuitAsync(CancellationToken ct = default);

        Task StartNdjsonLoopAsync(CancellationToken ct = default);

        event Action<string>? LineReceived;
    }
}
