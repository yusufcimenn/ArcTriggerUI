using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos.Orders
{
    public class OrderItem
    {
        public string acct { get; set; }
        public string conidex { get; set; }
        public long conid { get; set; }             // number
        public string account { get; set; }
        public long orderId { get; set; }           // number
        public string cashCcy { get; set; }
        public string sizeAndFills { get; set; }
        public string orderDesc { get; set; }
        public string description1 { get; set; }
        public string ticker { get; set; }
        public string secType { get; set; }
        public string listingExchange { get; set; }
        public long remainingQuantity { get; set; } // number
        public long filledQuantity { get; set; }    // number
        public long totalSize { get; set; }         // number
        public string companyName { get; set; }
        public string status { get; set; }
        public string order_ccp_status { get; set; }
        public bool outsideRTH { get; set; }
        public string origOrderType { get; set; }
        public string supportsTaxOpt { get; set; }
        public string lastExecutionTime { get; set; }
        public string orderType { get; set; }
        public string bgColor { get; set; }
        public string fgColor { get; set; }
        public string isEventTrading { get; set; }
        public decimal price { get; set; }          // number
        public string timeInForce { get; set; }
        public long lastExecutionTime_r { get; set; } // number
        public string side { get; set; }
        public decimal avgPrice { get; set; }       // number
    }

}
