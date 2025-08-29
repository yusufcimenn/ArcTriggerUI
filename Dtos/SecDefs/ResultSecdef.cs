using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos.SecDefs
{
    public class ResultSecdef
    {
        public class IncrementRule
        {
            public double lowerEdge { get; set; }
            public double increment { get; set; }
        }

        public class DisplayRuleStep
        {
            public int decimalDigits { get; set; }
            public double lowerEdge { get; set; }
            public int wholeDigits { get; set; }
        }

        public class DisplayRule
        {
            public int magnification { get; set; }
            public List<DisplayRuleStep> displayRuleStep { get; set; }
        }

        public class SecDef
        {
            public List<IncrementRule> incrementRules { get; set; }
            public DisplayRule displayRule { get; set; }
            public long conid { get; set; }
            public string currency { get; set; }
            public int time { get; set; }
            public string chineseName { get; set; }
            public string allExchanges { get; set; }
            public string listingExchange { get; set; }
            public string countryCode { get; set; }
            public string name { get; set; }
            public string assetClass { get; set; }
            public string expiry { get; set; }
            public string lastTradingDay { get; set; }
            public string group { get; set; }
            public string putOrCall { get; set; }
            public string sector { get; set; }
            public string sectorGroup { get; set; }
            public string strike { get; set; }
            public string ticker { get; set; }
            public long undConid { get; set; }
            public double multiplier { get; set; }
            public string type { get; set; }
            public bool hasOptions { get; set; }
            public string fullName { get; set; }
            public bool isUS { get; set; }
            public bool isEventContract { get; set; }
        }

        public class SecDefResponse
        {
            public List<SecDef> secdef { get; set; }
        }

    }
}
