using IBApi;

namespace ArcTriggerUI.Tws.Utils
{
    public class ContractBuilder
    {
        private readonly Contract _contract;

        public ContractBuilder()
        {
            _contract = new Contract
            {
                Exchange = "SMART",   // default
                Currency = "USD"      // default
            };
        }

        public ContractBuilder WithConId(int conId)
        {
            _contract.ConId = conId;
            return this;
        }

        public ContractBuilder WithSymbol(string symbol)
        {
            _contract.Symbol = symbol;
            return this;
        }

        public ContractBuilder WithSecType(string secType)
        {
            _contract.SecType = secType;
            return this;
        }

        public ContractBuilder WithExchange(string exchange)
        {
            _contract.Exchange = exchange;
            return this;
        }

        public ContractBuilder WithCurrency(string currency)
        {
            _contract.Currency = currency;
            return this;
        }

        public ContractBuilder WithExpiry(string expiry)
        {
            // format: YYYYMMDD veya YYYYMM
            _contract.LastTradeDateOrContractMonth = expiry;
            return this;
        }

        public ContractBuilder WithStrike(double strike)
        {
            _contract.Strike = strike;
            return this;
        }

        public ContractBuilder WithRight(string right)
        {
            // "C" veya "P"
            _contract.Right = right;
            return this;
        }

        public Contract Build() => _contract;
    }

    public class OptionContractBuilder
    {
        private readonly Contract _contract;

        public OptionContractBuilder()
        {
            _contract = new Contract
            {
                SecType = "OPT",   // sadece opsiyon
                Exchange = "SMART",
                Currency = "USD"
            };
        }

        public OptionContractBuilder WithConId(int conId)
        {
            _contract.ConId = conId;
            return this;
        }

        public OptionContractBuilder WithSymbol(string symbol)
        {
            _contract.Symbol = symbol;
            return this;
        }

        public OptionContractBuilder WithExpiry(string expiry)
        {
            // Format: YYYYMMDD (örn. 20251219)
            _contract.LastTradeDateOrContractMonth = expiry;
            return this;
        }

        public OptionContractBuilder WithStrike(double strike)
        {
            _contract.Strike = strike;
            return this;
        }

        public OptionContractBuilder WithRight(string right)
        {
            // "C" = Call, "P" = Put
            _contract.Right = right;
            return this;
        }

        public OptionContractBuilder WithExchange(string exchange)
        {
            _contract.Exchange = exchange;
            return this;
        }

        public OptionContractBuilder WithCurrency(string currency)
        {
            _contract.Currency = currency;
            return this;
        }

        public Contract Build() => _contract;
    }
}
