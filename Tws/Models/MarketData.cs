namespace ArcTriggerUI.Tws.Models
{
    public class MarketData
    {
        public int ConId { get; set; }
        public int TickerId { get; set; }
        public double Price { get; set; }
        public double Bid { get; set; }
        public int BidSize { get; set; }
        public double Ask { get; set; }
        public int AskSize { get; set; }
        public double Last { get; set; }
        public int LastSize { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low  { get; set; }
        public double Open { get; set; }
        public int Volume { get; set; }

        public int Field { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public override string ToString() =>
            $"[{Timestamp:HH:mm:ss.fff}] ConId={ConId} Last={Last} Bid={Bid} Ask={Ask} Close={Close} O={Open} H={High} L={Low} Vol={Volume}";
    }
}
