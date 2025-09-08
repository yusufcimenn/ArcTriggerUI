
using ArcTriggerUI.Dtos.Info;
using ArcTriggerUI.Dtos.Orders;
using System.Threading.Tasks;
using static ArcTriggerUI.Dtos.Portfolio.ResultPortfolio;
using static ArcTriggerUI.Dtos.SecDefs.ResultSecdef;

namespace ArcTriggerUI.Interfaces
{
    public interface IApiService
    {
        // GET işlemleri
        Task<string> GetAsync(string url);
        Task<T> GetAsync<T>(string url);

        // POST işlemleri
        Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest request);

        // Delete İşlemi
        Task DeleteAsync(string url, int id);

        // Özel metotlar
        Task<string> GetSecDefStrikeAsync(string conid, string month, string secType);
        Task<SecDefResponse> GetSecDefAsync(string conids);
        Task<List<PortfolioItem>> GetPortfolioAsync();
        Task<InfoResponse> GetInfoAsync(string conid);
        Task<CreateOrderResponse?> CreateOrderAsync(PostOrderItem request);
        Task<OrderResponseDto?> GetOrderByIdAsync(int id);
        Task<string> SendOrderAsync(OrderRequest order);

        Task<string> SellOrder(OrderSell orderSell);

    }
}
