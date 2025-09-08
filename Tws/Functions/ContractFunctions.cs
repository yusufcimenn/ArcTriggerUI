using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArcTriggerUI.Tws.Models;
using ArcTriggerUI.Tws.Services;
using ArcTriggerUI.Tws.Utils;
using IBApi;

namespace ArcTriggerUI.Tws.Functions
{
    public class ContractFunctions(TwsService tws)
    {
        private readonly TwsService _tws = tws;

        public Task<IReadOnlyList<SymbolMatch>> SearchSymbolsAsync(string query, CancellationToken ct = default)
            => _tws.SearchSymbolsAsync(query, ct);

        public Task<IReadOnlyList<ContractInfo>> GetContractDetailsAsync(
            int conId, string symbol, string secType, string exchange = "SMART", string currency = "USD",
            CancellationToken ct = default)
        {
            var contract = new Contract
            {
                ConId = conId,
                Symbol = symbol,
                SecType = secType,
                Exchange = exchange,
                Currency = currency
            };

            return _tws.GetContractDetailsAsync(contract, ct);
        }

        public Task<IReadOnlyList<OptionChainParams>> GetOptionParamsAsync(int conId, string symbol, string secType = "STK", string futFopExchange = "", CancellationToken ct = default)
            => _tws.GetOptionParamsAsync(conId, symbol, secType, futFopExchange, ct);

        public Task<int> ResolveOptionConidAsync(string symbol, string exchange, string right, string yyyymmdd, double strike, string? tradingClass = null, string? multiplier = null, CancellationToken ct = default)
            => _tws.ResolveOptionConidAsync(symbol, exchange, right, yyyymmdd, strike, tradingClass, multiplier, ct);

        // Helper: Builder ile Contract oluştur

        public static Contract BuildStockContract(int conid)
        {
            return new ContractBuilder()
                .WithConId(conid)
                .Build();
        }

        public static Contract BuildStockContract(string symbol, string exchange = "SMART", string currency = "USD")
        {
            return new ContractBuilder()
                .WithSymbol(symbol)
                .WithSecType("STK")
                .WithExchange(exchange)
                .WithCurrency(currency)
                .Build();
        }
    }
}
