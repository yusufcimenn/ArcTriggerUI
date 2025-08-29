using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos.Portfolio
{
    public class ResultPortfolio
    {

        public class PortfolioParent
        {
            public List<object> mmc { get; set; } = new();
            public string accountId { get; set; }
            public bool isMParent { get; set; }
            public bool isMChild { get; set; }
            public bool isMultiplex { get; set; }
        }

        public class PortfolioItem
        {
            public string id { get; set; }
            public bool PrepaidCryptoZ { get; set; }
            public bool PrepaidCryptoP { get; set; }
            public bool brokerageAccess { get; set; }
            public string accountId { get; set; }
            public string accountVan { get; set; }
            public string accountTitle { get; set; }
            public string displayName { get; set; }
            public string accountAlias { get; set; }
            public long accountStatus { get; set; }
            public string currency { get; set; }
            public string type { get; set; }
            public string tradingType { get; set; }
            public string businessType { get; set; }
            public string category { get; set; }
            public string ibEntity { get; set; }
            public bool faclient { get; set; }
            public string clearingStatus { get; set; }
            public bool covestor { get; set; }
            public bool noClientTrading { get; set; }
            public bool trackVirtualFXPortfolio { get; set; }
            public string acctCustType { get; set; }
            public PortfolioParent parent { get; set; }
            public string desc { get; set; }
        }
    }

}

