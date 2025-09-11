using System;
using IBApi;

namespace ArcTriggerUI.Tws.Services
{
    public abstract class BaseService : EWrapper, IAsyncDisposable
    {
        public readonly EClientSocket Client;
        private readonly EReaderSignal _signal;
        private int _nextOrderId;
        protected EReader? _reader;
        private Thread? _readerThread;
        private TaskCompletionSource<int>? _nextOrderIdTcs;

        protected BaseService()
        {
            _signal = new EReaderMonitorSignal();
            Client = new EClientSocket(this, _signal);
        }

        public void Connect(string host, int port, int clientId)
        {
            Client.eConnect(host, port, clientId, false);
            if (!Client.IsConnected()) throw new Exception("TWS'e bağlanamadı.");

            _reader = new EReader(Client, _signal);
            _reader.Start();

            _readerThread = new Thread(() =>
            {
                while (Client.IsConnected())
                {
                    _signal.waitForSignal();
                    try { _reader.processMsgs(); }
                    catch { /* swallow to avoid crash */ }
                }
            })
            { IsBackground = true, Name = "IB_EReader" };
            _readerThread.Start();
        }

        public async Task ConnectAsync(string host, int port, int clientId, CancellationToken ct = default)
        {
            _nextOrderIdTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Connect(host, port, clientId);

            // Paper için gecikmeli-donmuş tickler (Delayed-Frozen)
            Client.reqMarketDataType(3);

            using var _ = ct.Register(() => _nextOrderIdTcs!.TrySetCanceled(ct));
            await _nextOrderIdTcs!.Task.ConfigureAwait(false); // nextValidId bekle
        }


        public void Disconnect()
        {
            if (Client.IsConnected())
                Client.eDisconnect();
        }

        public int GetNextOrderId()
        {
            if (_nextOrderId <= 0)
                throw new InvalidOperationException("Henüz geçerli bir orderId alınmadı (nextValidId gelmedi).");

            return _nextOrderId++;
        }


        // --------------------
        // EWrapper Zorunluları
        // --------------------

        public virtual void nextValidId(int orderId)
        {
            Console.WriteLine($"Next valid order id: {orderId}");
            _nextOrderId = orderId;                   // <-- KRİTİK: orderId'ı burada set et
            _nextOrderIdTcs?.TrySetResult(orderId);
        }

        public virtual void error(Exception e) =>
            Console.WriteLine($"Exception: {e.Message}");

        public virtual void error(string str) =>
            Console.WriteLine($"Error: {str}");

        public virtual void error(int id, int errorCode, string errorMsg) =>
            Console.WriteLine($"Error. Id: {id}, Code: {errorCode}, Msg: {errorMsg}");

        public virtual void connectionClosed() =>
            Console.WriteLine("Connection closed.");

        // Gereksiz metodları boş geçelim, alt sınıflar override edebilir.
        public virtual void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
        {

        }
        public virtual void tickSize(int tickerId, int field, int size)
        {

        }
        public virtual void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
        {

        }

        public void currentTime(long time)
        {

        }

        public void tickString(int tickerId, int field, string value)
        {

        }

        public void tickGeneric(int tickerId, int field, double value)
        {

        }

        public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate)
        {

        }

        public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract)
        {

        }

        public void tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        {

        }

        public void tickSnapshotEnd(int tickerId)
        {

        }

        public void managedAccounts(string accountsList)
        {

        }

        public void accountSummary(int reqId, string account, string tag, string value, string currency)
        {

        }

        public void accountSummaryEnd(int reqId)
        {

        }

        public void bondContractDetails(int reqId, ContractDetails contract)
        {

        }

        public void updateAccountValue(string key, string value, string currency, string accountName)
        {

        }

        public void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName)
        {

        }

        public void updateAccountTime(string timestamp)
        {

        }

        public void accountDownloadEnd(string account)
        {

        }

        public virtual void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {

        }

        public virtual void openOrderEnd()
        {

        }

        public virtual void contractDetails(int reqId, ContractDetails contractDetails)
        {

        }

        public virtual void contractDetailsEnd(int reqId)
        {

        }

        public void execDetails(int reqId, Contract contract, Execution execution)
        {

        }

        public void execDetailsEnd(int reqId)
        {

        }

        public void commissionReport(CommissionReport commissionReport)
        {

        }

        public void fundamentalData(int reqId, string data)
        {

        }

        public void historicalData(int reqId, Bar bar)
        {

        }

        public void historicalDataUpdate(int reqId, Bar bar)
        {

        }

        public void historicalDataEnd(int reqId, string start, string end)
        {

        }

        public virtual void marketDataType(int reqId, int marketDataType)
        {

        }

        public void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size)
        {

        }

        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size, bool isSmartDepth)
        {

        }

        public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange)
        {

        }

        public void position(string account, Contract contract, double pos, double avgCost)
        {

        }

        public void positionEnd()
        {

        }

        public void realtimeBar(int reqId, long date, double open, double high, double low, double close, long volume, double WAP, int count)
        {

        }

        public void scannerParameters(string xml)
        {

        }

        public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)
        {

        }

        public void scannerDataEnd(int reqId)
        {

        }

        public void receiveFA(int faDataType, string faXmlData)
        {

        }

        public void verifyMessageAPI(string apiData)
        {

        }

        public void verifyCompleted(bool isSuccessful, string errorText)
        {

        }

        public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge)
        {

        }

        public void verifyAndAuthCompleted(bool isSuccessful, string errorText)
        {

        }

        public void displayGroupList(int reqId, string groups)
        {

        }

        public void displayGroupUpdated(int reqId, string contractInfo)
        {

        }

        public void connectAck()
        {

        }

        public void positionMulti(int requestId, string account, string modelCode, Contract contract, double pos, double avgCost)
        {

        }

        public void positionMultiEnd(int requestId)
        {

        }

        public void accountUpdateMulti(int requestId, string account, string modelCode, string key, string value, string currency)
        {

        }

        public void accountUpdateMultiEnd(int requestId)
        {

        }

        public virtual void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)
        {

        }

        public virtual void securityDefinitionOptionParameterEnd(int reqId)
        {

        }

        public void softDollarTiers(int reqId, SoftDollarTier[] tiers)
        {

        }

        public void familyCodes(FamilyCode[] familyCodes)
        {

        }

        public virtual void symbolSamples(int reqId, ContractDescription[] contractDescriptions)
        {

        }

        public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions)
        {

        }

        public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData)
        {

        }

        public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap)
        {

        }

        public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions)
        {

        }

        public void newsProviders(NewsProvider[] newsProviders)
        {

        }

        public void newsArticle(int requestId, int articleType, string articleText)
        {

        }

        public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline)
        {

        }

        public void historicalNewsEnd(int requestId, bool hasMore)
        {

        }

        public void headTimestamp(int reqId, string headTimestamp)
        {

        }

        public void histogramData(int reqId, HistogramEntry[] data)
        {

        }

        public void rerouteMktDataReq(int reqId, int conId, string exchange)
        {

        }

        public void rerouteMktDepthReq(int reqId, int conId, string exchange)
        {

        }

        public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements)
        {

        }

        public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL)
        {

        }

        public void pnlSingle(int reqId, int pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value)
        {

        }

        public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done)
        {

        }

        public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done)
        {

        }

        public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done)
        {

        }

        public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttribLast tickAttriblast, string exchange, string specialConditions)
        {

        }

        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttribBidAsk tickAttribBidAsk)
        {

        }

        public void tickByTickMidPoint(int reqId, long time, double midPoint)
        {

        }

        public void orderBound(long orderId, int apiClientId, int apiOrderId)
        {

        }

        public void completedOrder(Contract contract, Order order, OrderState orderState)
        {

        }

        public void completedOrdersEnd()
        {

        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        // ... diğer EWrapper metodları boş kalabilir
    }
}
