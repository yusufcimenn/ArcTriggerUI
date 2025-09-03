using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos.Orders
{
    public class OrderRequest
    {
        public string Conid { get; set; } = "";
        public double Trigger { get; set; }
        public string OrderMode { get; set; } = "";
        public double Offset { get; set; }
        public double PositionSize { get; set; }
        public double StopLoss { get; set; }
        public string Tif { get; set; } = "";
        public double? ProfitTaking { get; set; }
    }
}
