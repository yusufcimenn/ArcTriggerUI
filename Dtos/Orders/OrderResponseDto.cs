using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArcTriggerUI.Dtos.Orders
{
    public class OrderResponseDto
    {
        public bool Bulunan { get; set; }      // Order bulunmuş mu
        public OrderDto? Order { get; set; }   // Order detayları, bulunmazsa null
    }

    // Order detayları
    public class OrderDto
    {
        public int Id { get; set; }            // Order ID
        public string ProductName { get; set; } = string.Empty; // Ürün adı
        public int Quantity { get; set; }      // Ürün adedi

        // Gerekirse endpointten gelen diğer alanlar da eklenebilir
        public decimal Price { get; set; }     // Örnek: fiyat
        public string Status { get; set; } = string.Empty; // Örnek: durum
    } 
}
