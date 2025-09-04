using ArcTriggerUI.Const;
using ArcTriggerUI.Interfaces;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

using static System.Net.Mime.MediaTypeNames;


namespace ArcTriggerUI.Pages
{
    public partial class HistoryPage : ContentPage
    {
        private readonly IApiService _api;

       
       
        public ObservableCollection<OrderRow> Orders { get; } = new();
        public bool IsBusy { get; set; }

        public ICommand RefreshCommand { get; }

        public HistoryPage(IApiService api)
        {
            InitializeComponent();
            _api = api;
            BindingContext = this;
            RefreshCommand = new Command(async () => await LoadAsync());
            
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (Orders.Count == 0)
                await LoadAsync();
        }

        private async Task LoadAsync()
        {

            if (IsBusy) return;
            try
            {
                IsBusy = true;
                OnPropertyChanged(nameof(IsBusy));

                string url = Configs.BaseUrl + "/orders";
                string json = await _api.GetAsync(url);

                Orders.Clear();

                var arr = JsonSerializer.Deserialize<List<JsonElement>>(json);
                if (arr == null) return;

                foreach (var order in arr)
                {
                    string ticker = order.TryGetProperty("ticker", out var el) ? el.GetString() ?? "" : "";
                    string companyName = order.TryGetProperty("companyName", out el) ? el.GetString() ?? "" : "";
                    string side = order.TryGetProperty("side", out el) ? el.GetString() ?? "" : "";
                    string status = order.TryGetProperty("status", out el) ? el.GetString() ?? "" : "";
                    _orderType = order.TryGetProperty("orderType", out el) ? el.GetString() ?? "" : "";
                    _conid= order.TryGetProperty("conid", out el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : 0;
                    _orderId= order.TryGetProperty("orderId", out el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : 0;
                    _percent = order.TryGetProperty("quantity", out el) && el.ValueKind == JsonValueKind.Number ? el.GetDouble() : 0.0;
                    string priceTxt = "-";
                    if (order.TryGetProperty("price", out el))
                        priceTxt = el.ValueKind == JsonValueKind.Number
                            ? el.GetDouble().ToString("0.##", CultureInfo.InvariantCulture)
                            : el.GetString() ?? "-";

                    string qtyTxt = "-";
                    if (order.TryGetProperty("totalSize", out el))
                        qtyTxt = el.ValueKind == JsonValueKind.Number
                            ? el.GetDouble().ToString("0.##", CultureInfo.InvariantCulture)
                            : el.GetString() ?? "-";

                    Orders.Add(new OrderRow
                    {
                        Ticker = ticker,
                        CompanyName = companyName,
                        Side = side,
                        Status = status,
                        PriceText = $"Price: {priceTxt}",
                        QuantityText = $"Qty: {qtyTxt}",
                        Conid = _conid,
                        OrderId = _orderId,
                        Percent = (int)_percent,
                        OrderType = _orderType
                        

                    });

                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Orders could not be loaded:\n{ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        private async void OnReloadClicked(object sender, EventArgs e) => await LoadAsync();
        public int _conid { get; set; } = 0;
        public int _orderId { get; set; } = 0;
        public double _percent { get; set; } = 0;
        public string _orderType { get; set; } = "";
        private async void OnOrderSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0) return;

            var selectedOrder = e.CurrentSelection[0] as OrderRow;
            _conid = selectedOrder.Conid;
            _orderId = selectedOrder.OrderId;
            _orderType = selectedOrder.OrderType;
            _percent = selectedOrder.Percent;
            if (selectedOrder == null) return;
            
            // Örnek: DisplayAlert ile detay göster
            await DisplayAlert(
                $"Order: {selectedOrder.Ticker}",
                $"Company: {selectedOrder.CompanyName}\n" +
                $"Side: {selectedOrder.Side}\n" +
                $"Status: {selectedOrder.Status}\n" +
                $"Conid: {_conid}\n" +
                $"OrderId: {_orderId}\n"+
                $"Percent: {_percent}\n" +
                $"OrderType: {_orderType}\n" +
                $"{selectedOrder.QuantityText}\n{selectedOrder.PriceText}",
                "OK");

            // Seçimi temizle
            ((CollectionView)sender).SelectedItem = null;
        }

        private async void OnBreakevenClicked(object sender, EventArgs e)
        {
            //SellQuantityAsync(_conid,_orderId,_orderType,percent:1);
            try
            {
                var button = sender as Button;
                if (button == null) return;

                // O satýrýn binding context’i (OrderRow)
                var selectedOrder = button.BindingContext as OrderRow;
                if (selectedOrder == null) return;

                _conid = selectedOrder.Conid;
                _orderId = selectedOrder.OrderId;
                _orderType = selectedOrder.OrderType;
                _percent = selectedOrder.Percent;

                string conid = _conid.ToString(CultureInfo.InvariantCulture);
                string orderId = _orderId.ToString(CultureInfo.InvariantCulture);
                int percent = 1;
                string orderType = _orderType.ToString(CultureInfo.InvariantCulture);
                if (_orderType == "Market")
                {
                    _orderType = "MKT";
                }
                else
                {
                    _orderType = "LMT";
                }

                var url = Configs.BaseUrl + $"/sell/quantity" +
                          $"?conid={conid}" +
                          $"&orderId={orderId}" +
                          $"&percent={percent}" +
                          $"&orderType={_orderType}";

                var response = await _api.PostAsync<object, object>(url, null);

                // Response’u JSON formatýnda göster
                var jsonString = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                await DisplayAlert("API Response", jsonString, "Tamam");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Exception", ex.Message, "Tamam");
            }
        }
        private async Task<string> SellQuantityAsync(long conid, long orderId, string orderType, int percent = 1)
        {
            if (_orderType=="Market")
            {
                _orderType = "MKT";
            }
            else
            {
                _orderType = "LMT";
            }
            try
            {
                string url = Configs.BaseUrl + $"/sell/quantity" +
                        $"?conid={conid.ToString(CultureInfo.InvariantCulture)}" +
                        $"&orderId={orderId.ToString(CultureInfo.InvariantCulture)}" +
                        $"&percent={percent}" +
                        $"&orderType={orderType}";

                var response = await _api.PostAsync<object, object>(url, null);

                // JSON string döndür
                var jsonString = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
                await DisplayAlert("API Response", jsonString, "Tamam");
            }
            catch (Exception ex)
            {

                await DisplayAlert("Exception", ex.Message, "Tamam");
            }
            string okey = "Ok";
            return okey ; 

        }
      
    }


    public class OrderRow
    {
        public string Ticker { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Side { get; set; } = "";
        public string Status { get; set; } = "";
        public string PriceText { get; set; } = "";
        public string QuantityText { get; set; } = "";
        public int Conid { get; set; }
        public int OrderId { get; set; }
        public int Percent { get; set; }
        public string OrderType { get; set; }

    }

}
