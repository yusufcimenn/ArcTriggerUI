using ArcTriggerUI.Const;
using ArcTriggerUI.Dtos.Info;
using ArcTriggerUI.Dtos.Orders;
using ArcTriggerUI.Dtos.Portfolio;
using ArcTriggerUI.Dtos.SecDefs;
using ArcTriggerUI.Interfaces;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ArcTriggerUI.Dtos.Portfolio.ResultPortfolio;
using static ArcTriggerUI.Dtos.SecDefs.ResultSecdef;

namespace ArcTriggerUI.Services
{
    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task DeleteAsync(string url, int id)
        {
            var response = await _httpClient.DeleteAsync($"{url}/{id}");
            response.EnsureSuccessStatusCode();


        }
        // Ham GET
        public async Task<string> GetAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        // GET + deserialize
        public async Task<T> GetAsync<T>(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json);
        }

        public async Task<InfoResponse> GetInfoAsync(string conid)
        {
            string url = Configs.BaseUrl + "/info/{conid}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<InfoResponse>(json);
        }

        public async Task<List<PortfolioItem>> GetPortfolioAsync()
        {

            var response = await _httpClient.GetAsync(Configs.BaseUrl + "/portfolio");
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<PortfolioItem>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }


        public async Task<SecDefResponse> GetSecDefAsync(string conids)
        {
            string url = Configs.BaseUrl + "/secdef?conids={conids}";
            return await GetAsync<SecDefResponse>(url);
        }

        public async Task<string> GetSecDefStrikeAsync(string conid, string month, string secType)
        {
            // URL’e query parametreleri ekle
            string url = Configs.BaseUrl + "/secdefStrike?conid={conid}&month={month}&secType={secType}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();  // 200-299 arası değilse exception fırlatır

            return await response.Content.ReadAsStringAsync();
        }



        // POST + generic request/response
        public async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest request)
        {
            string json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(responseJson);
        }


        public async Task<CreateOrderResponse?> CreateOrderAsync(PostOrderItem request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string url = Configs.BaseUrl + "/orders";
            string json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            string responseJson = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Response JSON: " + responseJson);

            // Hata kontrolü
            if (responseJson.Contains("\"error\""))
            {
                var error = JsonSerializer.Deserialize<ApiErrorResponse>(responseJson);
                throw new Exception(error?.Error ?? "Unknown API error");
            }

            return JsonSerializer.Deserialize<CreateOrderResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
    }

}
