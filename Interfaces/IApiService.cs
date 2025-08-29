
using System.Threading.Tasks;

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
    }
}
