using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos
{
    public class ResultSymbols
    {
        public string symbol { get; set; }
        public bool name { get; set; }
        public string secType { get; set; }
    }
    public class SymbolSearchResponse
    {
        public string symbol { get; set; } = "";
        public string? name { get; set; }
        public long? conid { get; set; }      // << eklendi
        public string? companyHeader { get; set; } // companyheader için

        // companyheader için: companyHeader varsa önce onu göster, yoksa name, yoksa symbol
        public string Display =>
            !string.IsNullOrWhiteSpace(companyHeader) ? $"{symbol} — {companyHeader}" :
            string.IsNullOrWhiteSpace(name) ? symbol : $"{symbol} — {name}";
    }
}

