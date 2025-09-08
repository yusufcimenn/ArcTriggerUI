using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos.Orders
{
    public class OrderSell
    {
        public int conid { get; set; }
        public int orderId  { get; set; }
        public decimal percent  { get; set; }
        public string orderType  { get; set; }
    }
}
