using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos.Info
{
    public class InfoResponse
    {
        public long conid { get; set; }
        public string ticker { get; set; }
        public string secType { get; set; }
        public string listingExchange { get; set; }
        public string exchange { get; set; }
        public string companyName { get; set; }
        public string currency { get; set; }
        public string validExchanges { get; set; }
        public string priceRendering { get; set; } // nullable string
        public string maturityDate { get; set; }   // nullable string
        public string right { get; set; }
        public double strike { get; set; }
    }
}
