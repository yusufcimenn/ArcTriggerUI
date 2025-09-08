using System;
using ArcTriggerUI.Tws.Models;
using ArcTriggerUI.Tws.Services;

namespace ArcTriggerUI.Tws.Functions
{
    public class MarketDataFunctions(TwsService tws)
    {
        private readonly TwsService _tws = tws;

        public int SubscribeMarketData(int conId, string secType = "STK", string exchange = "SMART", string currency = "USD", int type = 3)
        {
            return _tws.RequestMarketData(conId, secType, exchange, currency, type);
        }

        public int SubscribeMarketDataBySymbol(string symbol, string secType = "STK", string exchange = "SMART", string currency = "USD", int type = 3)
        {
            return _tws.RequestMarketDataBySymbol(symbol, secType, exchange, currency, type);
        }

        public MarketData? GetSnapshot(int tickerId) => _tws.GetLatestData(tickerId);

        public void CancelMarketData(int tickerId) => _tws.CancelMarketData(tickerId);

        public void OnMarketData(Action<MarketData> handler)
        {
            _tws.OnMarketData += handler;
        }
    }
}
