namespace ArcTriggerUI.Dtos
{
    public sealed record ComplateOrderDto(
            int OptionConId,
            string Right,
            double Strike,
            int Quantity,
            double Trigger,
            string OrderMode,
            double StopLoss,
            double? LimitPrice,
            string Tif = "DAY",
            double StopLossOffset = 0.05,
            double Offset = 0.05,
            bool OutsideRth = false,
            string? Account = null
        );
}