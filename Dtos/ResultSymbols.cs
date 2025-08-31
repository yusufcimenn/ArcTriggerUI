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
        public string? name { get; set; } = null;
        public long? conid { get; set; }   // << eklendi
        // Picker’da görünecek metin 
        public string Display => string.IsNullOrWhiteSpace(name) ? symbol : $"{name} ({symbol})";
    }
}

