namespace ArcTriggerUI.Tws.Models
{
    public record SymbolMatch(string Symbol, string SecType, string? Description, int ConId, IReadOnlyList<string> DerivativeSecTypes);

    public record ContractInfo(int ConId, string Symbol, string SecType, string Exchange, string Currency, string LocalSymbol, int? UnderConId, string TradingClass, string Multiplier, string LongName, string Description);

    public record OptionChainParams(
        string Exchange,
        int UnderlyingConId,
        string TradingClass,
        string Multiplier,
        IReadOnlyCollection<string> Expirations,   // "YYYYMM" veya "YYYYMMDD"
        IReadOnlyCollection<double> Strikes
    );

}