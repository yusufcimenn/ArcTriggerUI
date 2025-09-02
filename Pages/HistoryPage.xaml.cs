    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Microsoft.Maui.Controls;
    using ArcTriggerUI.Const;
    using ArcTriggerUI.Interfaces;

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
                            QuantityText = $"Qty: {qtyTxt}"
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
        private async void OnOrderSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count == 0) return;

            var selectedOrder = e.CurrentSelection[0] as OrderRow;
            if (selectedOrder == null) return;

            // Örnek: DisplayAlert ile detay göster
            await DisplayAlert(
                $"Order: {selectedOrder.Ticker}",
                $"Company: {selectedOrder.CompanyName}\n" +
                $"Side: {selectedOrder.Side}\n" +
                $"Status: {selectedOrder.Status}\n" +
                $"{selectedOrder.QuantityText}\n{selectedOrder.PriceText}",
                "OK");

            // Seçimi temizle
            ((CollectionView)sender).SelectedItem = null;
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
        }
    }
