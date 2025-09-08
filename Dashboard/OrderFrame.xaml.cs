using ArcTriggerUI.Tws.Functions;
using ArcTriggerUI.Tws.Models;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ArcTriggerUI.Dashboard;

public partial class OrderFrame : ContentView
{
    private readonly ContractFunctions _contractFunctions;
    private readonly MarketDataFunctions _marketDataFunctions;
    private readonly OrderFunctions _orderFunctions;

    private readonly ObservableCollection<SymbolMatch> _symbolResults = new();
    private int? _selectedConid;
    private int? _lastOrderId;
    private string _orderMode = "MKT";

    private T? FindParentOfType<T>() where T : Element
    {
        Element? p = this.Parent;
        while (p != null && p is not T) p = p.Parent;
        return p as T;
    }

    private Page? HostPage =>
        Shell.Current?.CurrentPage ??
        Application.Current?.MainPage ??
        FindParentOfType<Page>();

    private Task ShowAlert(string title, string message, string cancel = "OK")
        => HostPage?.DisplayAlert(title, message, cancel) ?? Task.CompletedTask;

    private Task<bool> ShowConfirm(string title, string message, string accept = "OK", string cancel = "Cancel")
        => HostPage?.DisplayAlert(title, message, accept, cancel) ?? Task.FromResult(false);


    public OrderFrame(ContractFunctions contractFunctions,
                      MarketDataFunctions marketDataFunctions,
                      OrderFunctions orderFunctions)
    {
        InitializeComponent();
        _contractFunctions = contractFunctions;
        _marketDataFunctions = marketDataFunctions;
        _orderFunctions = orderFunctions;

        SymbolSuggestions.ItemsSource = _symbolResults;
    }

    private async void OnSymbolSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            _symbolResults.Clear();
            SymbolSuggestions.IsVisible = false;
            return;
        }

        var results = await _contractFunctions.SearchSymbolsAsync(query);
        _symbolResults.Clear();
        foreach (var r in results) _symbolResults.Add(r);

        SymbolSuggestions.IsVisible = _symbolResults.Count > 0;
    }

    private void OnSymbolSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is SymbolMatch sel)
        {
            _selectedConid = sel.ConId;
            SymbolSearchEntry.Text = sel.Symbol;

            int tickerId = _marketDataFunctions.SubscribeMarketData(sel.ConId);
            var snap = _marketDataFunctions.GetSnapshot(tickerId);
            if (snap != null)
                MarketPriceLabel.Text = snap.Last.ToString(CultureInfo.InvariantCulture);

            SymbolSuggestions.IsVisible = false;
        }
    }

    private void OnOrderModeChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return;
        _orderMode = ((RadioButton)sender).Content?.ToString() ?? "MKT";
    }

    private async void OnPlaceOrderClicked(object sender, EventArgs e)
    {
        if (!_selectedConid.HasValue)
        {
            await ShowAlert("Error", "Select symbol first", "OK");
            return;
        }

        int qty = int.Parse(PositionEntry.Text);
        string tif = ExpPicker.SelectedItem?.ToString() ?? "DAY";

        var contract = ContractFunctions.BuildStockContract(_selectedConid.Value);

        if (_orderMode == "MKT")
        {
            _lastOrderId = await _orderFunctions.PlaceMarketBuyAsync(contract, qty, tif);
        }
        else
        {
            double price = double.Parse(TriggerEntry.Text, CultureInfo.InvariantCulture);
            _lastOrderId = await _orderFunctions.PlaceLimitBuyAsync(contract, qty, price, tif);
        }

        await ShowAlert("Success", $"Order placed: {_lastOrderId}", "OK");
    }

    private async void OnCancelOrderClicked(object sender, EventArgs e)
    {
        if (_lastOrderId == null)
        {
            await ShowAlert("Info", "No order to cancel", "OK");
            return;
        }

        await _orderFunctions.CancelOrderAsync(_lastOrderId.Value);
        await ShowAlert("Success", $"Order {_lastOrderId} cancelled", "OK");
    }

    private void OnSymbolChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is not null)
        {
            var selected = picker.SelectedItem.ToString();
            // burada sembol seçildiğinde yapılacak işlemleri koyabilirsin
            Console.WriteLine($"Symbol changed to: {selected}");
        }
    }

    private void AutoRefreshSwitch_Toggled(object sender, ToggledEventArgs e)
    {
        if (e.Value)
        {
            // AutoRefresh açık
            Console.WriteLine("Auto-refresh enabled");
            // burada timer ya da market data refresh başlatabilirsin
        }
        else
        {
            // AutoRefresh kapalı
            Console.WriteLine("Auto-refresh disabled");
            // burada timer ya da market data refresh durdurabilirsin
        }
    }

    private void OnSecTypeChanged(object sender, EventArgs e)
{
    if (sender is Picker picker && picker.SelectedItem is not null)
    {
        var selectedSecType = picker.SelectedItem.ToString();
        Console.WriteLine($"SecType changed to: {selectedSecType}");

        // burada seçilen SecType'a göre mantık ekleyebilirsin
        // örn: STK → Hisse, OPT → Opsiyon, FUT → Futures
    }
}
private void OnMonthChanged(object sender, EventArgs e)
{
    if (sender is Picker picker)
    {
        if (picker.SelectedIndex >= 0)
        {
            var selectedMonth = picker.Items[picker.SelectedIndex];
            Console.WriteLine($"Month changed to: {selectedMonth}");

            // burada seçilen aya göre işlem yapabilirsin
            // Örn: API çağrısı, option chain filtreleme, vs.
        }
    }
}

private void StrikesPicker_SelectedIndexChanged(object sender, EventArgs e)
{
    if (sender is Picker picker)
    {
        if (picker.SelectedIndex >= 0)
        {
            var selectedStrike = picker.Items[picker.SelectedIndex];
            Console.WriteLine($"Strike changed to: {selectedStrike}");

            // burada strike seçildiğinde yapılacak işlemleri yazabilirsin
            // Örn: seçilen strike için option conid resolve et, fiyat datası çek, vs.
        }
    }
}
private void MaturityDateLabel_SelectedIndexChanged(object sender, EventArgs e)
{
    if (sender is Picker picker)
    {
        if (picker.SelectedIndex >= 0)
        {
            var selectedMaturity = picker.Items[picker.SelectedIndex];
            Console.WriteLine($"Maturity changed to: {selectedMaturity}");

            // burada seçilen vade için işlem yapabilirsin
            // Örn: option chain filtrele, contract resolve et, vs.
        }
    }
}
private void OnTriggerPriceTextChanged(object sender, TextChangedEventArgs e)
{
    if (double.TryParse(e.NewTextValue, out var newValue))
    {
        Console.WriteLine($"Trigger changed: {newValue}");
        // burada trigger seviyesini modeline aktarabilirsin
    }
    else
    {
        // Sayı değilse placeholder bırak
        TriggerEntry.Text = "";
    }
}

private void OnIncreaseTriggerClicked(object sender, EventArgs e)
{
    if (double.TryParse(TriggerEntry.Text, out var value))
    {
        value += 0.1; // adım (step) artırma
        TriggerEntry.Text = value.ToString("F2");
    }
    else
    {
        TriggerEntry.Text = "0.00";
    }
}

private void OnDecreaseTriggerClicked(object sender, EventArgs e)
{
    if (double.TryParse(TriggerEntry.Text, out var value))
    {
        value = Math.Max(0, value - 0.1); // negatif olmasın
        TriggerEntry.Text = value.ToString("F2");
    }
    else
    {
        TriggerEntry.Text = "0.00";
    }
}
private void OnOrderTypeCheckedChanged(object sender, CheckedChangedEventArgs e)
{
    if (sender is RadioButton rb && e.Value)
    {
        var type = rb.Content?.ToString();
        Console.WriteLine($"Order Type selected: {type}");

        // Örn: ViewModel'e yaz
        // SelectedOrderType = type; // "Call" veya "Put"
    }
}

private void OnOrderModeCheckedChanged(object sender, CheckedChangedEventArgs e)
{
    if (sender is RadioButton rb && e.Value)
    {
        var mode = rb.Content?.ToString();
        Console.WriteLine($"Order Mode selected: {mode}");

        // Örn: ViewModel'e yaz
        // SelectedOrderMode = mode; // "MKT" veya "LMT"
    }
}
// Preset butonuna sol tıklama
private void OnHotPresetClicked(object sender, EventArgs e)
{
    if (sender is Button btn)
    {
        var value = btn.Text; // "0.05" veya "0.10"
        OffsetEntry.Text = value;

        Console.WriteLine($"Offset preset selected: {value}");
    }
}

// Preset butonuna sağ tıklama
private void OnPresetRightClick(object sender, TappedEventArgs e)
{
    if (sender is Button btn)
    {
        Console.WriteLine($"Right-clicked preset: {btn.Text}");

        // Burada örn. context menu açabilirsin
        // veya bu preset değerini kullanıcıya düzenletirsin
    }
}

// Yeni preset ekleme
private void OnHotAddClicked(object sender, EventArgs e)
{
    Console.WriteLine("Add new offset preset clicked");

    // Burada yeni bir offset değeri ekletip listeye push edebilirsin
    // Örn: kullanıcıdan input alıp BtnOff3 diye yeni buton oluşturabilirsin
}
// Pozisyon entry değiştiğinde quantity hesapla
private void OnPositionTextChanged(object sender, TextChangedEventArgs e)
{
    if (double.TryParse(PositionEntry.Text?.Replace("$", "").Replace("K", "000"), out double positionUsd))
    {
        // Örn: contract price'ı 100$ farz edelim
        double contractPrice = GetCurrentContractPrice();

        if (contractPrice > 0)
        {
            int qty = (int)Math.Floor(positionUsd / contractPrice);
            lblQuantity.Text = qty.ToString();
        }
        else
        {
            lblQuantity.Text = "0";
        }
    }
    else
    {
        lblQuantity.Text = "0";
    }
}

// Preset butonuna tıklanınca
// private void OnHotPresetClicked(object sender, EventArgs e)
// {
//     if (sender is Button btn)
//     {
//         var type = (string)btn.CommandParameter; // "pos" veya "off" gelebilir
//         var text = btn.Text;

//         if (type == "pos")
//         {
//             // $5K gibi text'i numeric'e çevir
//             string cleaned = text.Replace("$", "").Replace("K", "000");
//             PositionEntry.Text = cleaned;
//         }
//         else if (type == "off")
//         {
//             OffsetEntry.Text = text;
//         }
//     }
// }

// Sağ tık → preset değiştir
// private void OnPresetRightClick(object sender, TappedEventArgs e)
// {
//     if (sender is Button btn)
//     {
//         Console.WriteLine($"Right click on {btn.Text} (param: {btn.CommandParameter})");
//         // Burada context menu açıp yeni değer alabilirsin
//     }
// }

// // Yeni preset ekle
// private void OnHotAddClicked(object sender, EventArgs e)
// {
//     if (sender is Button btn)
//     {
//         var type = (string)btn.CommandParameter;

//         if (type == "pos")
//         {
//             Console.WriteLine("Add new Position Size preset...");
//         }
//         else if (type == "off")
//         {
//             Console.WriteLine("Add new Offset preset...");
//         }
//     }
// }

// Şu anki kontrat fiyatını al (örnek - sen bunu MarketData'dan çekeceksin)
private double GetCurrentContractPrice()
{
    return 100.0; // şimdilik sabit, ileride canlı fiyat gelecek
}
// Stop loss entry değiştiğinde (örnek: ileride risk hesaplama bağlanabilir)
private void OnStopLossTextChanged(object sender, TextChangedEventArgs e)
{
    if (double.TryParse(StopLossEntry.Text?.Replace("$", ""), out double stopValue))
    {
        Console.WriteLine($"Stop Loss set: {stopValue}");
        // Burada risk hesabı veya kontrat qty güncellemesi yapılabilir
    }
    else
    {
        Console.WriteLine("Invalid Stop Loss input");
    }
}

// Hot preset tıklama (Position / Offset / Stop / Profit hepsi buradan geliyor)
// private void OnHotPresetClicked(object sender, EventArgs e)
// {
//     if (sender is Button btn)
//     {
//         var type = (string)btn.CommandParameter;
//         var text = btn.Text;

//         switch (type)
//         {
//             case "pos":
//                 PositionEntry.Text = text.Replace("$", "").Replace("K", "000");
//                 break;

//             case "off":
//                 OffsetEntry.Text = text.Replace("$", "");
//                 break;

//             case "stop":
//                 StopLossEntry.Text = text.Replace("$", "");
//                 break;

//             case "prof":
//                 ProfitEntry.Text = text.Replace("%", "");
//                 break;
//         }
//     }
// }

// Sağ tık preset değiştir
// private void OnPresetRightClick(object sender, TappedEventArgs e)
// {
//     if (sender is Button btn)
//     {
//         Console.WriteLine($"Right click on {btn.Text} for {btn.CommandParameter}");
//         // Burada context menü açıp kullanıcıya yeni değer girmesini sağlayabilirsin
//     }
// }

// // Yeni preset ekleme
// private void OnHotAddClicked(object sender, EventArgs e)
// {
//     if (sender is Button btn)
//     {
//         var type = (string)btn.CommandParameter;
//         Console.WriteLine($"Add new preset for {type}");
//         // Burada popup açıp kullanıcıdan değer alıp preset listesine ekleyebilirsin
//     }
// }
// Profit Taking entry değiştiğinde
private void OnProfitTextChanged(object sender, TextChangedEventArgs e)
{
    if (double.TryParse(ProfitEntry.Text?.Replace("%", ""), out double profitPerc))
    {
        Console.WriteLine($"Profit target set: {profitPerc}%");
        // Burada Take Profit seviyesini hesaplayabilirsin
        // Örn: EntryPrice * (1 + profitPerc / 100)
    }
    else
    {
        Console.WriteLine("Invalid profit input");
    }
}
private void OnExpirationChanged(object sender, EventArgs e)
{
    if (ExpPicker.SelectedItem is string selectedTif)
    {
        Console.WriteLine($"Expiry (TIF) seçildi: {selectedTif}");
        // burada Order DTO içine set edebilirsin
        // örn: _currentOrder.Tif = selectedTif;
    }
}
private async void OnCreateOrdersClicked(object sender, EventArgs e)
{
    try
    {
        // Trigger, Offset, StopLoss vs. UI’dan oku
        double.TryParse(TriggerEntry.Text, out double trigger);
        double.TryParse(OffsetEntry.Text, out double offset);
        double.TryParse(StopLossEntry.Text, out double stopLoss);
        int.TryParse(lblQuantity.Text, out int qty);
        string tif = ExpPicker.SelectedItem?.ToString() ?? "DAY";

        // Burada TwsService üzerinden order gönderebilirsin
        var contract = ContractFunctions.BuildStockContract("AAPL");
        var orderId = await _orderFunctions.PlaceMarketBuyAsync(contract, qty, tif);

        await ShowAlert("Order", $"Order placed with ID: {orderId}", "OK");
    }
    catch (Exception ex)
    {
        await ShowAlert("Error", ex.Message, "OK");
    }
}
private void OnTrailClicked(object sender, EventArgs e)
{
    Console.WriteLine("Trail/Invalidate clicked.");
    // Burada stop loss’u trailing’e çevirebilirsin
}
private void OnBreakevenClicked(object sender, EventArgs e)
{
    Console.WriteLine("Breakeven clicked.");
    // Burada stop seviyesini entry fiyatına taşırsın
}
private async void OnCancelClicked(object sender, EventArgs e)
{
    try
    {
        // Burada aktif orderId üzerinden iptal
        int orderId = 123; // aktif orderId set edilmeli
        await _orderFunctions.CancelOrderAsync(orderId);
        await ShowAlert("Cancelled", $"Order {orderId} cancelled.", "OK");
    }
    catch (Exception ex)
    {
        await ShowAlert("Error", ex.Message, "OK");
    }
}
private void AddOrderToContainer(string text)
{
    var label = new Label
    {
        Text = text,
        FontSize = 13,
        TextColor = Colors.White
    };

    OrdersContainer.Children.Add(new Frame
    {
        Content = label,
        Padding = 6,
        Margin = new Thickness(0, 2),
        CornerRadius = 6,
        BackgroundColor = Colors.DarkGreen
    });
}

}
