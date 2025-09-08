using System.Threading;
using System.Threading.Tasks;
using ArcTriggerUI.Tws.Services;
using ArcTriggerUI.Tws.Utils;
using IBApi;

namespace ArcTriggerUI.Tws.Functions
{
    public class OrderFunctions(TwsService tws)
    {
        private readonly TwsService _tws = tws;

        public Task<int> PlaceMarketBuyAsync(Contract contract, int qty, string tif = "DAY", string? account = null, CancellationToken ct = default)
        {
            var order = new OrderBuilder()
                .WithAction("BUY")
                .WithOrderType("MKT")
                .WithQuantity(qty)
                .WithTif(tif)
                .WithAccount(account ?? string.Empty)
                .Build();

            return _tws.PlaceOrderAsync(contract, order, ct);
        }

        public Task<int> PlaceLimitBuyAsync(Contract contract, int qty, double price, string tif = "DAY", string? account = null, CancellationToken ct = default)
        {
            var order = new OrderBuilder()
                .WithAction("BUY")
                .WithOrderType("LMT")
                .WithQuantity(qty)
                .WithLimitPrice(price)
                .WithTif(tif)
                .WithAccount(account ?? string.Empty)
                .Build();

            return _tws.PlaceOrderAsync(contract, order, ct);
        }

        public Task CancelOrderAsync(int orderId, CancellationToken ct = default)
            => _tws.CancelOrderAsync(orderId, ct);

        // Wrap edilmiş protective stop örneği
        public Task<(int parentId, int childId)> PlaceBreakoutBuyWithStopAsync(int conId, double trigger, double offset, double stopLoss, int qty, CancellationToken ct = default)
            => _tws.PlaceBreakoutBuyStopLimitWithProtectiveStopAsync(conId, trigger, offset, stopLoss, qty, ct: ct);
    }
}
