using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace ArcTriggerUI.Dtos.Orders
{


    public class PostOrderItem
    {
        public long? conid { get; set; }
        public string orderType { get; set; }
        public string side { get; set; }
        public int? quantity { get; set; }  // API: 1000’in katı olmalı
        public decimal? price { get; set; }
        public string tif { get; set; }
    }


    public class CreateOrderResponse
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; }

        [JsonPropertyName("order_status")]
        public string OrderStatus { get; set; }

        [JsonPropertyName("encrypt_message")]
        public string EncryptMessage { get; set; }
    }

    public class ApiErrorResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; set; }
    }

}
