using IBApi;
using System;
using System.Collections.Generic;

namespace ArcTriggerUI.Services
{
    public class TwsService : EWrapper
    {
        private int nextOrderId;

        // Expose events so UI/services can subscribe
        public event Action<int> OnNextValidId;
        public event Action<string> OnManagedAccounts;
        public event Action<string> OnErrorMessage;
        public event Action<int, string, double, double> OnOrderStatus;
        public event Action<int, ContractDetails> OnContractDetails;
        public event Action<int> OnContractDetailsEnd;
        public event Action<int, ContractDescription[]> OnSymbolSamples;
        public event Action<int, string, string, string, string, string> OnAccountSummary;
        public event Action<int> OnAccountSummaryEnd;
        public event Action<Contract, double, double, double, double, double, double, string> OnUpdatePortfolio;
        public event Action<string, Contract, double, double> OnPosition;
        public event Action OnPositionEnd;

        // ====== CORE IMPLEMENTATIONS ======

        public void nextValidId(int orderId)
        {
            nextOrderId = orderId;
            Console.WriteLine($"NextValidId: {orderId}");
            OnNextValidId?.Invoke(orderId);
        }

        public void managedAccounts(string accountsList)
        {
            Console.WriteLine($"ManagedAccounts: {accountsList}");
            OnManagedAccounts?.Invoke(accountsList);
        }

        public void error(Exception e)
        {
            Console.WriteLine($"Error (Exception): {e.Message}");
            OnErrorMessage?.Invoke(e.Message);
        }

        public void error(string str)
        {
            Console.WriteLine($"Error (String): {str}");
            OnErrorMessage?.Invoke(str);
        }

        public void error(int id, int errorCode, string errorMsg)
        {
            Console.WriteLine($"Error. Id={id}, Code={errorCode}, Msg={errorMsg}");
            OnErrorMessage?.Invoke($"Id={id}, Code={errorCode}, Msg={errorMsg}");
        }

        public void orderStatus(int orderId, string status, double filled, double remaining,
                                double avgFillPrice, int permId, int parentId,
                                double lastFillPrice, int clientId, string whyHeld,
                                double mktCapPrice)
        {
            Console.WriteLine($"OrderStatus: Id={orderId}, Status={status}, Filled={filled}, Remaining={remaining}");
            OnOrderStatus?.Invoke(orderId, status, filled, remaining);
        }

        public void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {
            Console.WriteLine($"OpenOrder: Id={orderId}, {contract.Symbol} {order.Action} {order.TotalQuantity} {order.OrderType}");
        }

        public void openOrderEnd()
        {
            Console.WriteLine("OpenOrderEnd");
        }

        public void contractDetails(int reqId, ContractDetails contractDetails)
        {
            Console.WriteLine($"ContractDetails: ReqId={reqId}, Symbol={contractDetails.UnderSymbol}");
            OnContractDetails?.Invoke(reqId, contractDetails);
        }

        public void contractDetailsEnd(int reqId)
        {
            Console.WriteLine($"ContractDetailsEnd: ReqId={reqId}");
            OnContractDetailsEnd?.Invoke(reqId);
        }

        public void symbolSamples(int reqId, ContractDescription[] contractDescriptions)
        {
            Console.WriteLine($"SymbolSamples: ReqId={reqId}, Count={contractDescriptions.Length}");
            OnSymbolSamples?.Invoke(reqId, contractDescriptions);
        }

        public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)
        {
            Console.WriteLine($"SecDefOptionParam: ReqId={reqId}, Exchange={exchange}, Expirations={expirations.Count}, Strikes={strikes.Count}");
        }

        public void securityDefinitionOptionParameterEnd(int reqId)
        {
            Console.WriteLine($"SecDefOptionParamEnd: ReqId={reqId}");
        }

        public void accountSummary(int reqId, string account, string tag, string value, string currency)
        {
            Console.WriteLine($"AccountSummary: {account} {tag}={value} {currency}");
            OnAccountSummary?.Invoke(reqId, account, tag, value, currency, "");
        }

        public void accountSummaryEnd(int reqId)
        {
            Console.WriteLine($"AccountSummaryEnd: ReqId={reqId}");
            OnAccountSummaryEnd?.Invoke(reqId);
        }

        public void updatePortfolio(Contract contract, double position, double marketPrice, double marketValue,
                                    double averageCost, double unrealizedPNL, double realizedPNL, string accountName)
        {
            Console.WriteLine($"UpdatePortfolio: {contract.Symbol} Pos={position}, Price={marketPrice}");
            OnUpdatePortfolio?.Invoke(contract, position, marketPrice, marketValue, averageCost, unrealizedPNL, realizedPNL, accountName);
        }

        public void position(string account, Contract contract, double pos, double avgCost)
        {
            Console.WriteLine($"Position: {account}, {contract.Symbol}, Pos={pos}, AvgCost={avgCost}");
            OnPosition?.Invoke(account, contract, pos, avgCost);
        }

        public void positionEnd()
        {
            Console.WriteLine("PositionEnd");
            OnPositionEnd?.Invoke();
        }

        // ====== LEAVE THE REST UNCHANGED ======
        public void accountDownloadEnd(string account) { throw new NotImplementedException(); }
        public void accountUpdateMulti(int requestId, string account, string modelCode, string key, string value, string currency) { throw new NotImplementedException(); }
        public void accountUpdateMultiEnd(int requestId) { throw new NotImplementedException(); }
        public void bondContractDetails(int reqId, ContractDetails contract) { throw new NotImplementedException(); }
        public void commissionReport(CommissionReport commissionReport) { throw new NotImplementedException(); }
        public void connectAck() { throw new NotImplementedException(); }
        public void connectionClosed() { throw new NotImplementedException(); }
        public void currentTime(long time) { throw new NotImplementedException(); }
        public void deltaNeutralValidation(int reqId, UnderComp underComp) { throw new NotImplementedException(); }
        public void displayGroupList(int reqId, string groups) { throw new NotImplementedException(); }
        public void displayGroupUpdated(int reqId, string contractInfo) { throw new NotImplementedException(); }
        public void execDetails(int reqId, Contract contract, Execution execution) { throw new NotImplementedException(); }
        public void execDetailsEnd(int reqId) { throw new NotImplementedException(); }
        public void familyCodes(FamilyCode[] familyCodes) { throw new NotImplementedException(); }
        public void fundamentalData(int reqId, string data) { throw new NotImplementedException(); }
        public void headTimestamp(int reqId, string headTimestamp) { throw new NotImplementedException(); }
        public void histogramData(int reqId, HistogramEntry[] data) { throw new NotImplementedException(); }
        public void historicalData(int reqId, Bar bar) { throw new NotImplementedException(); }
        public void historicalDataEnd(int reqId, string start, string end) { throw new NotImplementedException(); }
        public void historicalDataUpdate(int reqId, Bar bar) { throw new NotImplementedException(); }
        public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { throw new NotImplementedException(); }
        public void historicalNewsEnd(int requestId, bool hasMore) { throw new NotImplementedException(); }
        public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { throw new NotImplementedException(); }
        public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { throw new NotImplementedException(); }
        public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { throw new NotImplementedException(); }
        public void marketDataType(int reqId, int marketDataType) { throw new NotImplementedException(); }
        public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) { throw new NotImplementedException(); }
        public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) { throw new NotImplementedException(); }
        public void newsArticle(int requestId, int articleType, string articleText) { throw new NotImplementedException(); }
        public void newsProviders(NewsProvider[] newsProviders) { throw new NotImplementedException(); }
        //public void openOrderEnd() { throw new NotImplementedException(); }
        public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) { throw new NotImplementedException(); }
        public void pnlSingle(int reqId, int pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) { throw new NotImplementedException(); }
        public void positionMulti(int requestId, string account, string modelCode, Contract contract, double pos, double avgCost) { throw new NotImplementedException(); }
        public void positionMultiEnd(int requestId) { throw new NotImplementedException(); }
        public void realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double WAP, int count) { throw new NotImplementedException(); }
        public void receiveFA(int faDataType, string faXmlData) { throw new NotImplementedException(); }
        public void rerouteMktDataReq(int reqId, int conId, string exchange) { throw new NotImplementedException(); }
        public void rerouteMktDepthReq(int reqId, int conId, string exchange) { throw new NotImplementedException(); }
        public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) { throw new NotImplementedException(); }
        public void scannerDataEnd(int reqId) { throw new NotImplementedException(); }
        public void scannerParameters(string xml) { throw new NotImplementedException(); }
        public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) { throw new NotImplementedException(); }
        public void softDollarTiers(int reqId, SoftDollarTier[] tiers) { throw new NotImplementedException(); }
        public void tickByTickAllLast(int reqId, int tickType, long time, double price, int size, TickAttrib attribs, string exchange, string specialConditions) { throw new NotImplementedException(); }
        public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, int bidSize, int askSize, TickAttrib attribs) { throw new NotImplementedException(); }
        public void tickByTickMidPoint(int reqId, long time, double midPoint) { throw new NotImplementedException(); }
        public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) { throw new NotImplementedException(); }
        public void tickGeneric(int tickerId, int field, double value) { throw new NotImplementedException(); }
        public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { throw new NotImplementedException(); }
        public void tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) { throw new NotImplementedException(); }
        public void tickPrice(int tickerId, int field, double price, TickAttrib attribs) { throw new NotImplementedException(); }
        public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { throw new NotImplementedException(); }
        public void tickSize(int tickerId, int field, int size) { throw new NotImplementedException(); }
        public void tickSnapshotEnd(int tickerId) { throw new NotImplementedException(); }
        public void tickString(int tickerId, int field, string value) { throw new NotImplementedException(); }
        public void updateAccountTime(string timestamp) { throw new NotImplementedException(); }
        public void updateAccountValue(string key, string value, string currency, string accountName) { throw new NotImplementedException(); }
        public void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size) { throw new NotImplementedException(); }
        public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size) { throw new NotImplementedException(); }
        public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) { throw new NotImplementedException(); }
        public void verifyAndAuthCompleted(bool isSuccessful, string errorText) { throw new NotImplementedException(); }
        public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) { throw new NotImplementedException(); }
        public void verifyCompleted(bool isSuccessful, string errorText) { throw new NotImplementedException(); }
        public void verifyMessageAPI(string apiData) { throw new NotImplementedException(); }
    }
}
