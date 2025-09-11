using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IBApi;
// SymbolMatch, ContractInfo, OptionChainParams modellerin buradaysa
using ArcTriggerUI.Tws.Models;
using ArcTriggerUI.Tws.Utils;
using ArcTriggerUI.Dtos;

namespace ArcTriggerUI.Tws.Services
{
    public sealed class TwsService : BaseService
    {
        // ---- reqId/Task eşlemesi
        private int _nextReqId = 0;
        private int NextReqId() => Interlocked.Increment(ref _nextReqId);

        private readonly ConcurrentDictionary<int, TaskCompletionSource<IReadOnlyList<SymbolMatch>>> _symTcs = new();
        private readonly ConcurrentDictionary<int, (TaskCompletionSource<IReadOnlyList<ContractInfo>> tcs, List<ContractInfo> buf)> _cdTcs = new();
        private readonly ConcurrentDictionary<int, (TaskCompletionSource<IReadOnlyList<OptionChainParams>> tcs, List<OptionChainParams> buf)> _optTcs = new();

        // ---- orderId/ACK yönetimi
        private volatile int _nextOrderId;
        private TaskCompletionSource<int>? _nextOrderIdTcs;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _orderAck = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _cancelAck = new();

        // ---- Bağlantı
        public bool isConnected = false;
        
        public async Task ConnectAsync(string host, int port, int clientId, CancellationToken ct = default)
        {
            if (isConnected == false)
            {
                _nextOrderIdTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Connect(host, port, clientId);
                using var _ = ct.Register(() => _nextOrderIdTcs.TrySetCanceled(ct));
                _nextOrderId=await _nextOrderIdTcs.Task.ConfigureAwait(false); // nextValidId bekle
                isConnected = true;
            }
        }

        public void nextValidId(int orderId)
        {
            Console.WriteLine($"Next valid order id: {orderId}");
            _nextOrderId = orderId;
            _nextOrderIdTcs?.TrySetResult(orderId);
        }

        // =======================
        // CONTRACT API (async)
        // =======================

        public Task<IReadOnlyList<SymbolMatch>> SearchSymbolsAsync(string query, CancellationToken ct = default)
        {
            var reqId = NextReqId();
            var tcs = new TaskCompletionSource<IReadOnlyList<SymbolMatch>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _symTcs[reqId] = tcs;
            using var _ = ct.Register(() => tcs.TrySetCanceled(ct));
            Client.reqMatchingSymbols(reqId, query);
            return tcs.Task;
        }

        public Task<IReadOnlyList<ContractInfo>> GetContractDetailsAsync(Contract key, CancellationToken ct = default)
        {
            var reqId = NextReqId();
            var tcs = new TaskCompletionSource<IReadOnlyList<ContractInfo>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cdTcs[reqId] = (tcs, new List<ContractInfo>(8));
            using var _ = ct.Register(() => tcs.TrySetCanceled(ct));
            Client.reqContractDetails(reqId, key);
            return tcs.Task;
        }

        public Task<IReadOnlyList<OptionChainParams>> GetOptionParamsAsync(
            int underlyingConId, string symbol, string underlyingSecType = "STK", string futFopExchange = "", CancellationToken ct = default)
        {
            var reqId = NextReqId();
            var tcs = new TaskCompletionSource<IReadOnlyList<OptionChainParams>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _optTcs[reqId] = (tcs, new List<OptionChainParams>(4));
            using var _ = ct.Register(() => tcs.TrySetCanceled(ct));
            Client.reqSecDefOptParams(reqId, symbol, futFopExchange, underlyingSecType, underlyingConId);
            return tcs.Task;
        }

        public async Task<int> ResolveOptionConidAsync(
            string symbol, string secType, string exchange, string right, string yyyymmdd, double strike, CancellationToken ct = default)
        {
            var c = new OptionContractBuilder()
                .WithSymbol(symbol)
                .WithSecType(secType)
                .WithExchange(exchange)
                .WithRight(right)
                .WithExpiry(yyyymmdd)
                .WithStrike(strike)
                .WithSecType(secType)
                .Build();

            var list = await GetContractDetailsAsync(c, ct).ConfigureAwait(false);
            var hit = list.FirstOrDefault() ?? throw new InvalidOperationException("Tekilleşmedi: contract bulunamadı.");
            return hit.ConId;
        }

        // =======================
        // TRADE API (async)
        // =======================

        public async Task<int> PlaceOrderAsync(Contract contract, Order order, CancellationToken ct = default)
        {
            var id = GetNextOrderId();
            var ack = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _orderAck[id] = ack;
            using var _ = ct.Register(() => ack.TrySetCanceled(ct));
            Client.placeOrder(id, contract, order);
            await ack.Task.ConfigureAwait(false); // ilk status/openOrder bekle
            return id;
        }

        public async Task CancelOrderAsync(int orderId, CancellationToken ct = default)
        {
            var ack = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cancelAck[orderId] = ack;
            using var _ = ct.Register(() => ack.TrySetCanceled(ct));
            Client.cancelOrder(orderId);
            await ack.Task.ConfigureAwait(false); // "Cancelled" status bekle
        }

        // --- Basit SELL helper'lar ---

        public Task<int> PlaceProfitTakingAsync(
            Contract contract, int qty, double limitPrice,
            string tif = "DAY", string? account = null, bool close = true, CancellationToken ct = default)
        {
            var ob = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("LMT")
                .WithQuantity(qty)
                .WithLimitPrice(limitPrice)
                .WithTif(tif)
                .WithOpenClose(close ? "C" : "O");
            if (!string.IsNullOrWhiteSpace(account)) ob.WithAccount(account);

            return PlaceOrderAsync(contract, ob.Build(), ct);
        }

        public Task<int> PlaceBreakevenAsync(
            Contract contract, int qty,
            string tif = "DAY", string? account = null, bool close = true, CancellationToken ct = default)
        {
            if (qty <= 0)
                throw new ArgumentOutOfRangeException(nameof(qty), "Quantity must be positive.");

            var ob = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("MKT")
                .WithQuantity(qty)
                .WithTif(tif)
                .WithOpenClose(close ? "C" : "O");
            if (!string.IsNullOrWhiteSpace(account)) ob.WithAccount(account);

            return PlaceOrderAsync(contract, ob.Build(), ct);
        }

        public Task<int> PlaceProfitAsync(
            Contract contract, double percent, int totalQty, string tif = "DAY", string? account = null, bool close = true, CancellationToken ct = default)
        {
            if (percent <= 0 || percent > 1)
                throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be between 0 and 1.");

            if (totalQty <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalQty), "Quantity must be positive.");

            var qty = (int)Math.Round(totalQty * percent, MidpointRounding.AwayFromZero);
            if (qty == 0)
                throw new InvalidOperationException("Calculated quantity is zero. Adjust percent or totalQty.");

            var order = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("MKT")
                .WithQuantity(qty)
                .WithTif(tif)
                .WithOpenClose(close ? "C" : "O");

            if (!string.IsNullOrWhiteSpace(account))
                order.WithAccount(account);

            return PlaceOrderAsync(contract, order.Build(), ct);
        }


        public Task<int> PlaceStopMarketAsync(
            Contract contract, int qty, double stopTrigger,
            string tif = "DAY", bool outsideRth = false, string? account = null, bool close = true, CancellationToken ct = default)
        {
            var ob = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("STP")
                .WithQuantity(qty)
                .WithStopPrice(stopTrigger)     // AuxPrice
                .WithTif(tif)
                .WithOutsideRth(outsideRth)
                .WithOpenClose(close ? "C" : "O");
            if (!string.IsNullOrWhiteSpace(account)) ob.WithAccount(account);

            return PlaceOrderAsync(contract, ob.Build(), ct);
        }

        public Task<int> PlaceStopLimitAsync(
            Contract contract, int qty, double stopTrigger, double limitPrice,
            string tif = "DAY", bool outsideRth = false, string? account = null, bool close = true, CancellationToken ct = default)
        {
            var ob = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("STP LMT")
                .WithQuantity(qty)
                .WithStopPrice(stopTrigger)     // AuxPrice
                .WithLimitPrice(limitPrice)     // LmtPrice
                .WithTif(tif)
                .WithOutsideRth(outsideRth)
                .WithOpenClose(close ? "C" : "O");
            if (!string.IsNullOrWhiteSpace(account)) ob.WithAccount(account);

            return PlaceOrderAsync(contract, ob.Build(), ct);
        }

        // --- Parent/Child (Breakout + protective stop) ---

        private static double R2(double x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);

        public async Task<(int parentId, int childId)> PlaceBreakoutBuyStopLimitWithProtectiveStopAsync(
            int conId,
            double triggerPrice, double offset, double stopLoss,
            int quantity, string tif = "DAY", bool outsideRth = false,
            string? account = null, double stopLimitOffset = 0.05, CancellationToken ct = default)
        {
            var aux_stop = R2(triggerPrice);
            var limit_cap = R2(triggerPrice + offset);
            var stop_abs = R2(triggerPrice - stopLoss);
            var stop_limit = R2(stop_abs - stopLimitOffset);

            var c = new OptionContractBuilder()
                .WithConId(conId)
                .Build();

            var parentB = new OrderBuilder()
                .WithAction("BUY")
                .WithOrderType("STP LMT")
                .WithQuantity(quantity)
                .WithStopPrice(aux_stop)
                .WithLimitPrice(limit_cap)
                .WithTif(tif)
                .WithOutsideRth(outsideRth)
                .WithTransmit(false);
            if (!string.IsNullOrWhiteSpace(account)) parentB.WithAccount(account);

            var parentId = await PlaceOrderAsync(c, parentB.Build(), ct).ConfigureAwait(false);

            var childB = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("STP LMT")
                .WithQuantity(quantity)
                .WithStopPrice(stop_abs)
                .WithLimitPrice(stop_limit)
                .WithTif(tif)
                .WithOpenClose("C")
                .WithParentId(parentId)
                .WithTransmit(true);
            if (!string.IsNullOrWhiteSpace(account)) childB.WithAccount(account);

            var childId = await PlaceOrderAsync(c, childB.Build(), ct).ConfigureAwait(false);

            return (parentId, childId);
        }

        public async Task<(int parentId, int childId)> PlaceMarketBuyWithProtectiveStopAsync(
            int conId,
            double triggerPrice, double stopLoss,
            int quantity, string tif = "DAY", bool outsideRth = false,
            string? account = null, double stopLimitOffset = 0.05, CancellationToken ct = default)
        {
            var stop_abs = R2(triggerPrice - stopLoss);
            var stop_limit = R2(stop_abs - stopLimitOffset);

            var c = new OptionContractBuilder()
                .WithConId(conId)
                .Build();

            var parentB = new OrderBuilder()
                .WithAction("BUY")
                .WithOrderType("MKT")
                .WithQuantity(quantity)
                .WithTif(tif)
                .WithOutsideRth(outsideRth)
                .WithTransmit(false);
            if (!string.IsNullOrWhiteSpace(account)) parentB.WithAccount(account);

            var parentId = await PlaceOrderAsync(c, parentB.Build(), ct).ConfigureAwait(false);

            var childB = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("STP LMT")
                .WithQuantity(quantity)
                .WithStopPrice(stop_abs)
                .WithLimitPrice(stop_limit)
                .WithTif(tif)
                .WithOpenClose("C")
                .WithParentId(parentId)
                .WithTransmit(true);
            if (!string.IsNullOrWhiteSpace(account)) childB.WithAccount(account);

            var childId = await PlaceOrderAsync(c, childB.Build(), ct).ConfigureAwait(false);

            return (parentId, childId);
        }

        public async Task<(int parentId, int childId)> ComplateOrder(
            OptionOrderPreview orderPreview,
            double stopLoss,
            string tif = "DAY",
            double stopLimitOffset = 0.05,
            bool outsideRth = false,
            string? account = null,
            CancellationToken ct = default)
        {
            if (orderPreview.OrderMode.Equals("MKT", StringComparison.OrdinalIgnoreCase))
            {
                return await PlaceMarketBuyWithProtectiveStopAsync(
                    conId: orderPreview.OptionConId,
                    triggerPrice: orderPreview.LimitPrice ?? 0, // triggerPrice için LimitPrice ya da 0
                    stopLoss: stopLoss,
                    quantity: orderPreview.Quantity,
                    tif: tif,
                    outsideRth: outsideRth,
                    account: account,
                    stopLimitOffset: stopLimitOffset,
                    ct: ct
                    );
            }
            else if (orderPreview.OrderMode.Equals("LMT", StringComparison.OrdinalIgnoreCase))
            {
                // LMT için offset = (LimitPrice - triggerPrice)
                if (orderPreview.LimitPrice is null)
                    throw new ArgumentException("LimitPrice boş olamaz (LMT için).");

                var triggerPrice = orderPreview.LimitPrice.Value;
                var offset = 0.0; // ister LimitPrice’ı doğrudan kullan, offset 0 kabul et

                return await PlaceBreakoutBuyStopLimitWithProtectiveStopAsync(
                    conId: orderPreview.OptionConId,
                    triggerPrice: triggerPrice,
                    offset: offset,
                    stopLoss: stopLoss,
                    quantity: orderPreview.Quantity,
                    tif: tif,
                    outsideRth: outsideRth,
                    account: account,
                    stopLimitOffset: stopLimitOffset,
                    ct: ct
                    );
            }
            else
            {
                throw new NotSupportedException($"Desteklenmeyen OrderMode: {orderPreview.OrderMode}");
            }
        }


        // Snapshot

        private int _nextTickerId = 1;
        private readonly ConcurrentDictionary<int, MarketData> _marketData = new();

        // Her tick’te çalışacak event
        public event Action<MarketData>? OnMarketData;

        // ---- Market Data API ----
        public int RequestMarketData(
            int conId, string secType = "STK", string exchange = "SMART",
            string currency = "USD", int marketDataType = 3
        ) // default: delayed (3)
        {
            Client.reqMarketDataType(marketDataType);
            int tickerId = Interlocked.Increment(ref _nextTickerId);
            var contract = new Contract { ConId = conId, SecType = secType, Exchange = exchange, Currency = currency };
            Client.reqMktData(tickerId, contract, string.Empty, false, false, null);
            _marketData[tickerId] = new MarketData { ConId = conId, TickerId = tickerId };
            return tickerId;
        }

        public int RequestMarketDataBySymbol(
            string symbol, string secType = "STK", string exchange = "SMART",
            string currency = "USD", int marketDataType = 3)
        {
            Client.reqMarketDataType(marketDataType);
            int tickerId = Interlocked.Increment(ref _nextTickerId);
            var contract = new Contract { Symbol = symbol, SecType = secType, Exchange = exchange, Currency = currency };
            Client.reqMktData(tickerId, contract, string.Empty, false, false, null);
            _marketData[tickerId] = new MarketData { TickerId = tickerId };
            return tickerId;
        }

        public MarketData? GetLatestData(int tickerId) =>
            _marketData.TryGetValue(tickerId, out var d) ? d : null;

        public void CancelMarketData(int tickerId)
        {
            if (_marketData.ContainsKey(tickerId))
            {
                Client.cancelMktData(tickerId);
                _marketData.TryRemove(tickerId, out _);
            }
        }

        // =======================
        // EWrapper overrides
        // =======================
        public override void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
        {
            if (price <= 0) return;

            if (!_marketData.TryGetValue(tickerId, out var d))
                return;

            switch (field)
            {
                case 1: d.Bid = price; break;
                case 2: d.Ask = price; break;
                case 4: d.Last = price; break;
                case 6: d.High = price; break;
                case 7: d.Low = price; break;
                case 9: d.Close = price; break;
                case 14: d.Open = price; break;
            }

            d.Price = price;
            d.Field = field;

            d.Timestamp = DateTime.UtcNow;

            // Debug
            Console.WriteLine(
                $"[{d.Timestamp:HH:mm:ss}] Tick {field} Price={price} " +
                $"ConId={d.ConId} Last={d.Last} Bid={d.Bid} Ask={d.Ask} O={d.Open} H={d.High} L={d.Low} C={d.Close}"
            );

            OnMarketData?.Invoke(d);
        }

        public override void tickSize(int tickerId, int field, int size)
        {
            if (!_marketData.TryGetValue(tickerId, out var d)) return;
            if (size <= 0) return;

            if (field == 0 || field == 3)  // Bid/Ask Size
                d.Volume = size;

            if (field == 8) // Volume
                d.Volume = size;

            d.Timestamp = DateTime.UtcNow;

            //Debug
            Console.WriteLine($"[{d.Timestamp:HH:mm:ss}] TickSize {field} Size={size}");
        }

        public override void symbolSamples(int reqId, ContractDescription[] descs)
        {
            if (_symTcs.TryRemove(reqId, out var tcs))
            {
                var list = descs.Select(d => new SymbolMatch(
                    d.Contract.Symbol,
                    d.Contract.SecType,
                    d.Contract.PrimaryExch,
                    d.Contract.ConId,
                    (d.DerivativeSecTypes ?? Array.Empty<string>()).ToList()
                )).ToList();
                tcs.TrySetResult(list);
            }
        }

        public override void contractDetails(int reqId, ContractDetails cd)
        {
            if (_cdTcs.TryGetValue(reqId, out var box))
            {
                box.buf.Add(new ContractInfo(
                    cd.Contract.ConId,
                    cd.Contract.Symbol,
                    cd.Contract.SecType,
                    cd.Contract.Exchange,
                    cd.Contract.Currency,
                    cd.Contract.LocalSymbol,
                    cd.UnderConId == 0 ? null : cd.UnderConId,
                    cd.Contract.TradingClass,
                    cd.Contract.Multiplier,
                    cd.LongName,
                    cd.Contract.PrimaryExch
                ));
            }
        }

        public override void contractDetailsEnd(int reqId)
        {
            if (_cdTcs.TryRemove(reqId, out var box))
                box.tcs.TrySetResult(box.buf);
        }

        public override void securityDefinitionOptionParameter(
            int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier,
            HashSet<string> expirations, HashSet<double> strikes)
        {
            if (_optTcs.TryGetValue(reqId, out var box))
            {
                box.buf.Add(new OptionChainParams(
                    exchange,
                    underlyingConId,
                    tradingClass,
                    multiplier,
                    expirations ?? new HashSet<string>(),
                    strikes ?? new HashSet<double>()
                ));
            }
        }

        public override void securityDefinitionOptionParameterEnd(int reqId)
        {
            if (_optTcs.TryRemove(reqId, out var box))
                box.tcs.TrySetResult(box.buf);
        }

        public override void openOrder(int orderId, Contract c, Order o, OrderState s)
        {
            if (_orderAck.TryRemove(orderId, out var tcs))
                tcs.TrySetResult(s?.Status ?? "open");
        }

        public override void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice,
            int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
        {
            if (_orderAck.TryRemove(orderId, out var tcs))
                tcs.TrySetResult(status);

            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                _cancelAck.TryRemove(orderId, out var cts))
                cts.TrySetResult(status);
        }

        public override void error(int id, int errorCode, string errorMsg)
        {
            // Request TCS’leri
            if (_symTcs.TryRemove(id, out var t1)) { t1.TrySetException(new Exception($"[{errorCode}] {errorMsg}")); return; }
            if (_cdTcs.TryRemove(id, out var t2)) { t2.tcs.TrySetException(new Exception($"[{errorCode}] {errorMsg}")); return; }
            if (_optTcs.TryRemove(id, out var t3)) { t3.tcs.TrySetException(new Exception($"[{errorCode}] {errorMsg}")); return; }

            // Order ACK
            if (_orderAck.TryRemove(id, out var o1)) { o1.TrySetException(new Exception($"[{errorCode}] {errorMsg}")); return; }
            if (_cancelAck.TryRemove(id, out var o2)) { o2.TrySetException(new Exception($"[{errorCode}] {errorMsg}")); return; }

            base.error(id, errorCode, errorMsg);
        }
    }
}
