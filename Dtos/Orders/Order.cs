using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos.Orders
{
    public class Order
    {
        public string Symbol { get; set; }
        public decimal TriggerPrice { get; set; }
        public string OrderType { get; set; }
        public string OrderMode { get; set; }
        public decimal Offset { get; set; }
        public string Strike { get; set; }
        public string Expiry { get; set; }
        public decimal PositionSize { get; set; }
        public decimal StopLoss { get; set; }
        public decimal ProfitTaking { get; set; }

        //public override string ToString()
        //{
        //    return $"Symbol: {Symbol}, Trigger: {TriggerPrice}, Type: {OrderType}, Mode: {OrderMode}, Offset: {Offset}, Strike: {Strike}, Expiry: {Expiry}, Position: {PositionSize}, StopLoss: {StopLoss}, Profit: {ProfitTaking}, Alpha: {AlphaFlag}";
        //}

    }

    public class OrderResponse
    {
        public bool Bulunan { get; set; }
        public Order Order { get; set; }
    }
}
