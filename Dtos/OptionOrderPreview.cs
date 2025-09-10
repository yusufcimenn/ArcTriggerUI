namespace ArcTriggerUI.Dtos
{
    public sealed record OptionOrderPreview(
            int UnderlyingConId,
            string Symbol,
            string SecType,
            string Right,
            string ExpiryYyyymmdd,
            double Strike,
            string Exchange,
            int Quantity,
            string OrderMode,     // "MKT" / "LMT"
            double? LimitPrice,
            int OptionConId
        );
}