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
        private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _orderAck = new();
        private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _cancelAck = new();

        // ---- Bağlantı
        public bool isConnected = false;

        public async Task ConnectAsync(string host, int port, int clientId, CancellationToken ct = default)
        {
            if (!isConnected)
            {
                await base.ConnectAsync(host, port, clientId, ct).ConfigureAwait(false); // <-- base'i bekle
                                                                                         // (opsiyonel) IB bazen nextValidId'ı geciktirir, garanti olsun:
                Client.reqIds(-1);
                isConnected = true;
            }
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
                .Build();

            var list = await GetContractDetailsAsync(c, ct).ConfigureAwait(false);
            var hit = list.FirstOrDefault() ?? throw new InvalidOperationException("Not unique: contract not found.");
            return hit.ConId;
        }

        // =======================
        // TRADE API (async)
        // =======================

        private int GetNextOrderId()
        {
            return (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue);
        }



        public async Task<int> PlaceOrderAsync(Contract contract, Order order, CancellationToken ct = default)
        {
            var id = NextOrderId();

            var ack = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _orderAck[id] = ack;

            using var cancelReg = ct.Register(() => ack.TrySetCanceled(ct));

            Client.placeOrder(id, contract, order);

            if (!order.Transmit)
                return id; // parent/child paketliyorsanız ACK beklemeyin

            // ACK ya da kısa bir timeout (ör. 3 sn)
            Task finished = await Task.WhenAny(ack.Task, Task.Delay(TimeSpan.FromSeconds(3), ct)).ConfigureAwait(false);

            if (finished == ack.Task)
            {
                // hata geldiyse burada fırlar
                _ = await ack.Task.ConfigureAwait(false);
            }
            else
            {
                // timeout: memory leak olmasın
                _orderAck.TryRemove(id, out _);
                Console.WriteLine($"[WARN] No ACK for order {id} within timeout, continuing.");
            }

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
            Contract contract, int qty, double stopTrigger, string action = "SELL",
            string tif = "DAY", bool outsideRth = false, string? account = null, bool close = true, CancellationToken ct = default)
        {
            var validAux = MakeValidPrice(contract, stopTrigger, "STP", "SELL", isStopTrigger: true);

            var ob = new OrderBuilder()
                .WithAction(action)
                .WithOrderType("STP")
                .WithQuantity(qty)
                .WithStopPrice(validAux)     // AuxPrice
                .WithTif(tif)
                .WithOutsideRth(outsideRth)
                .WithOpenClose(close ? "C" : "O");
            if (!string.IsNullOrWhiteSpace(account)) ob.WithAccount(account);

            return PlaceOrderAsync(contract, ob.Build(), ct);
        }
        // TwsService içine ekle:
        public double AdjustPriceForTicks(Contract c, double raw, string orderType, string action, bool isStopTrigger = false)
            => MakeValidPrice(c, raw, orderType, action, isStopTrigger);


        public Task<int> PlaceStopLimitAsync(
            Contract contract, int qty, double stopTrigger, double limitPrice,
            string tif = "DAY", bool outsideRth = false, string? account = null, bool close = true, CancellationToken ct = default)
        {
            var validAux = MakeValidPrice(contract, stopTrigger, "STP LMT", "SELL", isStopTrigger: true);
            var validLimit = MakeValidPrice(contract, limitPrice, "STP LMT", "SELL", isStopTrigger: false);

            var ob = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("STP LMT")
                .WithQuantity(qty)
                .WithStopPrice(validAux)     // AuxPrice
                .WithLimitPrice(validLimit)     // LmtPrice
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
                .WithOrderType("LMT")
                .WithQuantity(quantity)
                .WithAuxPrice(aux_stop)
                .WithLimitPrice(limit_cap)
                .WithTif(tif)
                .WithOutsideRth(outsideRth)
                .WithTransmit(false);
            if (!string.IsNullOrWhiteSpace(account)) parentB.WithAccount(account);

            var parentId = await PlaceOrderAsync(c, parentB.Build(), ct).ConfigureAwait(false);

            var childB = new OrderBuilder()
                .WithAction("SELL")
                .WithOrderType("LMT")
                .WithQuantity(quantity)
                .WithAuxPrice(stop_abs)
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
                .WithAuxPrice(stop_abs)
                .WithLimitPrice(stop_limit)
                .WithTif(tif)
                .WithOpenClose("C")
                .WithParentId(parentId)
                .WithTransmit(true);
            if (!string.IsNullOrWhiteSpace(account)) childB.WithAccount(account);

            var childId = await PlaceOrderAsync(c, childB.Build(), ct).ConfigureAwait(false);

            return (parentId, childId);
        }
        public class TwsPosition
        {
            public Contract Contract { get; set; } = null!;
            public int Quantity { get; set; }
            public double AveragePrice { get; set; }
            public int? StopOrderId { get; set; } // Stop emri ID'si takibi
        }

        // Event tanımı
        public event Action<TwsPosition>? OnPositionReceived;

        // TaskCompletionSource yönetimi
        private readonly ConcurrentDictionary<int, TaskCompletionSource<TwsPosition?>> _positionRequests = new();
        private readonly ConcurrentDictionary<int, TwsPosition> _positions = new(); // conId -> pozisyon

        // Pozisyon talebi
        public Task<TwsPosition?> GetPositionAsync(int conId, int timeoutMs = 3000)
        {
            if (_positions.TryGetValue(conId, out var existing))
                return Task.FromResult<TwsPosition?>(existing);

            var tcs = new TaskCompletionSource<TwsPosition?>(TaskCreationOptions.RunContinuationsAsynchronously);
            int reqId = NextReqId();
            _positionRequests[reqId] = tcs;

            // Timeout
            var cts = new CancellationTokenSource(timeoutMs);
            cts.Token.Register(() =>
            {
                _positionRequests.TryRemove(reqId, out _);
                tcs.TrySetResult(null);
            });

            // TWS'e pozisyon isteği gönder
            Client.reqPositions();

            return tcs.Task;
        }

        // TWS'den gelen pozisyonu handle et
        public void HandleIncomingPosition(string account, Contract contract, double pos, double avgCost)
        {
            var twsPos = new TwsPosition
            {
                Contract = contract,
                Quantity = (int)pos,
                AveragePrice = avgCost
            };

            _positions[contract.ConId] = twsPos;

            OnPositionReceived?.Invoke(twsPos);

            // İlgili TaskCompletionSource varsa tamamla
            foreach (var kvp in _positionRequests)
            {
                if (kvp.Value.Task.IsCompleted) continue;
                kvp.Value.TrySetResult(twsPos);
                _positionRequests.TryRemove(kvp.Key, out _);
                break;
            }
        }

        public override void position(string account, Contract contract, double pos, double avgCost)
        {
            HandleIncomingPosition(account, contract, pos, avgCost);
        }

        public override void positionEnd()
        {
            try { Client.cancelPositions(); } catch { /* ignore */ }

            foreach (var kvp in _positionRequests.ToArray())
                if (_positionRequests.TryRemove(kvp.Key, out var tcs))
                    tcs.TrySetResult(null);
        }

        // StopOrderId takibi ekle / güncelle
        public void UpdateStopOrderId(int conId, int stopOrderId)
        {
            if (_positions.TryGetValue(conId, out var pos))
                pos.StopOrderId = stopOrderId;
        }

        // Stop emrini iptal et ve pozisyonu güncelle
        public async Task CancelStopAsync(int conId, CancellationToken ct = default)
        {
            if (_positions.TryGetValue(conId, out var pos) && pos.StopOrderId.HasValue)
            {
                await CancelOrderAsync(pos.StopOrderId.Value, ct);
                pos.StopOrderId = null;
            }
        }

        public async Task<(int parentId, int childId)> ComplateOrder(ComplateOrderDto orderDto, CancellationToken ct = default)
        {
            if (orderDto.OrderMode.Equals("MKT", StringComparison.OrdinalIgnoreCase))
            {
                return await PlaceMarketBuyWithProtectiveStopAsync(
                    conId: orderDto.OptionConId,
                    triggerPrice: orderDto.Trigger, // triggerPrice için LimitPrice ya da 0
                    stopLoss: orderDto.StopLoss,
                    quantity: orderDto.Quantity,
                    tif: orderDto.Tif,
                    outsideRth: orderDto.OutsideRth,
                    stopLimitOffset: orderDto.StopLossOffset,
                    ct: ct
                    );
            }
            else if (orderDto.OrderMode.Equals("LMT", StringComparison.OrdinalIgnoreCase))
            {
                // LMT için offset = (LimitPrice - triggerPrice)
                if (orderDto.LimitPrice is null)
                    throw new ArgumentException("LimitPrice cannot be null (for LMT).");

                return await PlaceBreakoutBuyStopLimitWithProtectiveStopAsync(
                    conId: orderDto.OptionConId,
                    triggerPrice: orderDto.LimitPrice.Value,
                    offset: orderDto.Offset,
                    stopLoss: orderDto.StopLoss,
                    quantity: orderDto.Quantity,
                    tif: orderDto.Tif,
                    outsideRth: orderDto.OutsideRth,
                    stopLimitOffset: orderDto.StopLossOffset,
                    ct: ct
                    );
            }
            else
            {
                throw new NotSupportedException($"Unsupported OrderMode: {orderDto.OrderMode}");
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

            // Market rule bilgilerini hazırla (async fire-and-forget)
            _ = EnsureMarketRuleLoadedAsync(
                cd.Contract.ConId,
                // IB C#: cd.MarketRuleIds örn "26" ya da "26,57" (multi). İlkini alıyoruz.
                int.TryParse((cd.MarketRuleIds ?? "").Split(',').FirstOrDefault(), out var rid) ? rid : (int?)null,
                cd.MinTick > 0 ? cd.MinTick : (double?)null
            );
        }

        public override void marketRule(int marketRuleId, PriceIncrement[] priceIncrements)
        {
            if (priceIncrements != null && priceIncrements.Length > 0)
                _marketRuleIncrements[marketRuleId] = priceIncrements.OrderBy(p => p.LowEdge).ToList();
        }

        // (opsiyonel) tickReqParams ile minTick düşebilir; istersen burada da cache’le
        public override void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions)
        {
            if (_marketData.TryGetValue(tickerId, out var d) && minTick > 0)
                _conIdToMinTick[d.ConId] = minTick;
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

        public sealed class OrderUpdate
        {
            public int OrderId { get; init; }
            public string Status { get; init; } = "";
            public double Filled { get; init; }
            public double Remaining { get; init; }
            public double AvgFillPrice { get; init; }
            public int ParentId { get; init; }
        }

        public event Action<OrderUpdate>? OnOrderStatusUpdated;


        public override void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice,
            int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
        {
            if (_orderAck.TryRemove(orderId, out var tcs))
                tcs.TrySetResult(status);

            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                _cancelAck.TryRemove(orderId, out var cts))
                cts.TrySetResult(status);

            // >>> EKLE: dışarıya bildir
            OnOrderStatusUpdated?.Invoke(new OrderUpdate
            {
                OrderId = orderId,
                Status = status ?? "",
                Filled = filled,
                Remaining = remaining,
                AvgFillPrice = avgFillPrice,
                ParentId = parentId
            });
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

        public async Task UpdateStopOrderQtyAsync(
        Contract contract,
        int stopOrderId,
        int newQty,
        string tif,
        bool outsideRth,
        string action,
        bool isStopLimit,
        double? auxStop,
        double? limitAfterStop,
        int? parentId,          // YENİ
        string? openClose,      // YENİ ("C" olmalı)
        string? account,        // YENİ
        CancellationToken ct = default)
        {
            if (newQty <= 0)
                throw new ArgumentOutOfRangeException(nameof(newQty));

            var order = new Order
            {
                OrderId = stopOrderId,
                Action = action,                          // SELL / BUY — ilk emirde neyse o
                OrderType = isStopLimit ? "STP LMT" : "STP", // ilk emirde neyse o
                TotalQuantity = newQty,
                Tif = tif,
                OutsideRth = outsideRth,
                Transmit = true,
                OpenClose = string.IsNullOrEmpty(openClose) ? "C" : openClose
            };

            if (parentId.HasValue) order.ParentId = parentId.Value;  // çocuk olarak kalması için
            if (!string.IsNullOrWhiteSpace(account)) order.Account = account;

            if (auxStop.HasValue) order.AuxPrice = auxStop.Value;              // STP tetik
            if (isStopLimit && limitAfterStop.HasValue) order.LmtPrice = limitAfterStop.Value; // STP LMT limit

            var ack = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _orderAck[stopOrderId] = ack;
            using var _ = ct.Register(() => ack.TrySetCanceled(ct));

            Client.placeOrder(stopOrderId, contract, order);   // modify
            await ack.Task.ConfigureAwait(false);
        }



        private readonly ConcurrentDictionary<int, List<PriceIncrement>> _marketRuleIncrements = new(); // marketRuleId -> increments
        private readonly ConcurrentDictionary<int, int> _conIdToMarketRule = new(); // conId -> marketRuleId
        private readonly ConcurrentDictionary<int, double> _conIdToMinTick = new(); // conId -> minTick (fallback)

        // Price’ı fiyata göre geçerli artıma yuvarla
        private static double RoundToIncrement(double price, double increment, MidpointRounding mode = MidpointRounding.AwayFromZero)
        {
            if (increment <= 0) return price;
            var p = (decimal)price;
            var inc = (decimal)increment;
            var roundedUnits = Math.Round(p / inc, 0, mode);
            return (double)(roundedUnits * inc);
        }

        // Aralık bazlı market rule: price hangi dilimdeyse o dilimin increment’ını kullan
        private static double MakeValidByRule(double price, IEnumerable<PriceIncrement> incs, MidpointRounding mode)
        {
            // IB increments lowEdge artan gelir; price >= lowEdge olan son dilimi seç
            PriceIncrement? match = null;
            foreach (var pi in incs)
                if (price >= pi.LowEdge) match = pi;

            var inc = (match?.Increment > 0) ? match!.Increment : 0.0;
            return inc > 0 ? RoundToIncrement(price, inc, mode) : price;
        }

        // Piyasa kurallarını çek ve cache’le (contractDetails çağrısından sonra)
        private async Task EnsureMarketRuleLoadedAsync(int conId, int? marketRuleIdMaybe, double? minTickMaybe, CancellationToken ct = default)
        {
            if (marketRuleIdMaybe.HasValue && marketRuleIdMaybe.Value > 0 && !_marketRuleIncrements.ContainsKey(marketRuleIdMaybe.Value))
            {
                // IB: reqMarketRule only by id
                Client.reqMarketRule(marketRuleIdMaybe.Value);
                // basit bekleme: increments gelince marketRule() callback’i dolduracak
                // ufak bir timeout ile pas geçebiliriz
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(2) && !_marketRuleIncrements.ContainsKey(marketRuleIdMaybe.Value))
                    await Task.Delay(50, ct).ConfigureAwait(false);
            }
            if (marketRuleIdMaybe.HasValue)
                _conIdToMarketRule[conId] = marketRuleIdMaybe.Value;

            if (minTickMaybe.HasValue && minTickMaybe.Value > 0)
                _conIdToMinTick[conId] = minTickMaybe.Value;
        }

        // Yön ve emir tipine göre güvenli yuvarlama modu seç
        private static MidpointRounding ModeFor(string orderType, string action, bool isStopTrigger)
        {
            // SELL stop (tetik aşağıda olmalı) → aşağı yuvarla; BUY stop → yukarı
            if (isStopTrigger)
                return (action?.Equals("SELL", StringComparison.OrdinalIgnoreCase) == true)
                    ? MidpointRounding.ToZero // floor için decimal trük: pozitif fiyatlarda ToZero aşağıya çeker
                    : MidpointRounding.AwayFromZero; // BUY stop yukarı
                                                     // Limit: SELL limit yukarı, BUY limit aşağı (genelde “fiyata zarar verme” mantığı)
            if (orderType?.IndexOf("LMT", StringComparison.OrdinalIgnoreCase) >= 0)
                return (action?.Equals("SELL", StringComparison.OrdinalIgnoreCase) == true)
                    ? MidpointRounding.AwayFromZero
                    : MidpointRounding.ToZero;
            // Varsayılan
            return MidpointRounding.AwayFromZero;
        }

        // Dış API: Verilen fiyatı bu contract için geçerli tick’e çevir
        private double MakeValidPrice(Contract c, double raw, string orderType, string action, bool isStopTrigger)
        {
            var mode = ModeFor(orderType, action, isStopTrigger);

            // 1) MarketRule varsa onu kullan
            if (_conIdToMarketRule.TryGetValue(c.ConId, out var mrId) && _marketRuleIncrements.TryGetValue(mrId, out var incs) && incs?.Count > 0)
                return MakeValidByRule(raw, incs, mode);

            // 2) Yoksa minTick fallback
            if (_conIdToMinTick.TryGetValue(c.ConId, out var mt) && mt > 0)
                return RoundToIncrement(raw, mt, mode);

            // 3) Hiçbiri yoksa olduğu gibi
            return raw;
        }


    }
}
