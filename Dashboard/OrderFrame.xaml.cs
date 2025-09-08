using ArcTriggerUI.Const;
using ArcTriggerUI.Dashboard;
using ArcTriggerUI.Dtos;
using ArcTriggerUI.Dtos.Orders;
using ArcTriggerUI.Interfaces;
using ArcTriggerUI.Services;
using Microsoft.Maui.ApplicationModel;         // MainThread
using Microsoft.Maui.Controls;                 // MAUI Controls
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Storage;                  // Preferences
using System;
using System.Collections.Generic; // SECDEF: listeler için
// symbols text için
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static ArcTriggerUI.Dtos.Portfolio.ResultPortfolio;
using Layout = Microsoft.Maui.Controls.Layout;

namespace ArcTriggerUI.Dashboard
{
    public partial class OrderFrame : ContentView
    {
        double xOffset = 0;
        double yOffset = 0;

        #region Section Config || Buton Bölümleri Ayarlarý
        class SectionConfig
        {
            public string Id = "";
            public SectionMode Mode;
            public List<string> Catalog = new();
            public string[] Selected = new string[3];
            public string PrefKey = "";
            public Button[] Slots = Array.Empty<Button>();
            public Entry TargetEntry = null!;
        }
        readonly Dictionary<string, SectionConfig> sections = new();
        enum SectionMode { KAmount, Dollar, Percent, Decimal }

        // Always 3 slots
        private readonly string[] _selected = new[] { "5K", "10K", "25K" };
        private const string PrefKey = "possize.hotbuttons.v1";
        #endregion

        private readonly IApiService _apiService;
        private double positionSize;
        private double trigger;
        private string? _currentSecType;
        private string? _currentMonth;
        private string? _currentExchange;
        private string? _lastConId;
        private string _currentRight = "C"; // Call=C, Put=P
        private string _orderMode = "MKT"; // DEFAULT MKT
        private StrikesResponses _lastStrikes; // month deðiþtiðinde gelen set'i tut

        // symbols text için: arama sonucu listesi ve debounce/iptal için CTS
        private readonly ObservableCollection<SymbolSearchResponse> _symbolResults = new();
        private CancellationTokenSource? _symbolCts;

        // symbols text için: sembol -> conid eþlemesi ve seçilen conid
        private readonly Dictionary<string, long> _symbolConidMap = new(StringComparer.OrdinalIgnoreCase);
        private long? _selectedConid = null;

        // marketprice için: fiyat isteklerini yönetmek için CTS
        private CancellationTokenSource? _priceCts; // marketprice için

        // symbols text için: öneri öðesi (conid eklendi)
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

        // =========================
        // SECDEF: akýþ için state
        // =========================
        private List<string> _secdefSecTypes = new();
        private List<string> _secdefMonths = new();
        private List<string> _secdefExchanges = new(); // strikes çaðrýsýnda kullanacaðýz

        // SECDEF: hafif DTO'lar
        private class ConidInfoResponse
        {
            public string? conid { get; set; }
            public string? symbol { get; set; }
            public string? description { get; set; }
            public List<string>? secTypes { get; set; }
        }
        private class SecTypeDetailResponse
        {
            public string? conid { get; set; }
            public string? symbol { get; set; }
            public string? secType { get; set; }
            public List<string>? months { get; set; }
            public List<string>? exchanges { get; set; }
        }
        private class StrikesResponse
        {
            public List<decimal>? call { get; set; }
            public List<decimal>? put { get; set; }
        }
        private class SecdefInfoResponse
        {
            public string? conid { get; set; }
            public string? desc2 { get; set; }
            public string? maturityDate { get; set; }
            public bool? showPrips { get; set; }
        }

        public OrderFrame(IApiService apiService)
        {
            InitializeComponent();
            InitHotSections();
            _apiService = apiService;

            SymbolSuggestions.ItemsSource = _symbolResults;
            var panGesture = new PanGestureRecognizer();
            panGesture.PanUpdated += (s, e) =>
            {
                if (e.StatusType == GestureStatus.Running)
                {
                    // ScrollView’u sürükleme
                    Scrolls.ScrollToAsync(xOffset - e.TotalX, yOffset - e.TotalY, false);
                }
                else if (e.StatusType == GestureStatus.Completed)
                {
                    // Son konumu kaydet
                    xOffset = Scrolls.ScrollX;
                    yOffset = Scrolls.ScrollY;
                }
            };

            ContentGrid.GestureRecognizers.Add(panGesture);

            // >>> fiyatý periyodik çek
            StartPriceAutoRefresh(TimeSpan.FromSeconds(3));
        }

        private static IApiService? TryResolveApi()
        {
            try
            {
                return Application.Current?.Handler?.MauiContext?.Services.GetService(typeof(IApiService)) as IApiService;
            }
            catch { return null; }
        }

        // ========= Page-based dialog helpers (ContentView'de Display* yerine) =========

        private Page? HostPage =>
            Shell.Current?.CurrentPage ??
            Application.Current?.MainPage ??
            this.FindParentOfType<Page>();

        private T? FindParentOfType<T>() where T : Element
        {
            Element? p = this.Parent;
            while (p != null && p is not T) p = p.Parent;
            return p as T;
        }

        private Task ShowAlert(string title, string message, string cancel = "OK")
            => HostPage?.DisplayAlert(title, message, cancel) ?? Task.CompletedTask;

        private Task<bool> ShowConfirm(string title, string message, string accept = "OK", string cancel = "Cancel")
            => HostPage?.DisplayAlert(title, message, accept, cancel) ?? Task.FromResult(false);

        private Task<string?> PromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel",
                                         string? placeholder = null, int maxLength = -1, Keyboard? keyboard = null, string? initialValue = null)
            => HostPage?.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, keyboard, initialValue)
               ?? Task.FromResult<string?>(null);

        private Task<string?> ActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
            => HostPage?.DisplayActionSheet(title, cancel, destruction, buttons) ?? Task.FromResult<string?>(null);

        #region Button Sections || Özelleþtirilebilir Buton Bölümleri
        void InitHotSections()
        {
            sections["pos"] = new SectionConfig
            {
                Id = "pos",
                Mode = SectionMode.KAmount,
                Catalog = new() { "500", "750", "1K", "1.5K", "2K", "2.5K", "5K", "10K", "25K", "50K" },
                Selected = new[] { "5K", "10K", "25K" },
                PrefKey = "hot.pos.v1",
                Slots = new[] { BtnPos1, BtnPos2, BtnPos3 },
                TargetEntry = PositionEntry
            };

            sections["stop"] = new SectionConfig
            {
                Id = "stop",
                Mode = SectionMode.Dollar,
                Catalog = new() { "0.10", "0.15", "0.20", "0.25", "0.50", "0.75", "1.00", "1.50", "2.00" },
                Selected = new[] { "0.20", "0.50", "1.00" },
                PrefKey = "hot.stop.v1",
                Slots = new[] { BtnStop1, BtnStop2, BtnStop3 },
                TargetEntry = StopLossEntry
            };

            sections["prof"] = new SectionConfig
            {
                Id = "prof",
                Mode = SectionMode.Percent,
                Catalog = new() { "5%", "10%", "15%", "20%", "25%", "30%", "40%", "50%" },
                Selected = new[] { "10%", "20%", "30%" },
                PrefKey = "hot.prof.v1",
                Slots = new[] { BtnProf1, BtnProf2, BtnProf3 },
                TargetEntry = ProfitEntry
            };
            sections["off"] = new SectionConfig
            {
                Id = "off",
                Mode = SectionMode.Decimal,
                Catalog = new() { "0.01", "0.02", "0.03", "0.05", "0.10", "0.15", "0.20", "0.25" },
                Selected = new[] { "0.05", "0.10", "0.15" },   // default 3 slot
                PrefKey = "hot.off.v1",
                Slots = new[] { BtnOff1, BtnOff2 },
                TargetEntry = OffsetEntry
            };

            foreach (var s in sections.Values)
            {
                LoadSection(s);
                ApplySectionButtons(s);
            }
        }
        #endregion

        #region Buton Event Handlers || Buton Olay Ýþleyicileri
        void OnHotPresetClicked(object sender, EventArgs e)
        {
            if (sender is not Button b || b.CommandParameter is not string id) return;
            if (!sections.TryGetValue(id, out var s)) return;

            var valForEntry = ValueForEntry(b.Text, s.Mode);
            s.TargetEntry.Text = valForEntry;
        }
        #endregion

        #region HotAdd Event Handler || HotAdd Olay Ýþleyicisi
        public async void OnHotAddClicked(object sender, EventArgs e)
        {
            if (sender is not Button b || b.CommandParameter is not string id) return;
            if (!sections.TryGetValue(id, out var s)) return;

            var used = new HashSet<string>(s.Selected, StringComparer.OrdinalIgnoreCase);
            var options = s.Catalog.Where(x => !used.Contains(x)).ToList();
            options.Add("Custom...");

            var title = id == "pos" ? "Choose position size"
                     : id == "stop" ? "Choose stop loss"
                     : "Choose profit percent";
            var prompt = id == "pos" ? "Enter like 1.5K or 1500"
                     : id == "stop" ? "Enter dollars like 0.75"
                     : "Enter percent like 12.5 or 12.5%";

            var choice = await ActionSheetAsync(title, "Cancel", null, options.ToArray());
            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

            if (choice == "Custom...")
            {
                var input = await PromptAsync(title, prompt, "OK", "Cancel");
                if (string.IsNullOrWhiteSpace(input)) return;
                choice = input.Trim();
            }

            s.Selected[0] = NormalizeForMode(choice, s.Mode); // ilk slotu degistir
            SaveSection(s);
            ApplySectionButtons(s);
        }

        #endregion

        #region Buton Yardýmcý Metotlarý || Button Helper Methods
        private string _selectedOrderType = "Call";   // Default
        private string _selectedOrderMode = "MKT";    // Default

        // OrderType (Call/Put)
        private void OnOrderTypeCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (!e.Value) return;
            var radio = sender as RadioButton;
            _selectedOrderType = radio?.Content?.ToString();

            // right paramý (Call -> C, Put -> P)
            _currentRight = string.Equals(_selectedOrderType, "Put", StringComparison.OrdinalIgnoreCase) ? "P" : "C";
            ClearMaturityUI();

            // right deðiþtiðinde strike listesi de right’a göre yeniden kurulsun
            if (_lastStrikes != null)
                RebuildStrikesPicker();
            // strike seçiliyse maturity’yi yeniden çek
            if (StrikesPicker.SelectedIndex >= 0)
                _ = LoadMaturityDateForSelectionAsync();
        }

        // OrderMode (MKT/LMT)
        private void OnOrderModeCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (MktRadioButton.IsChecked)
                _orderMode = "MKT";
            else if (LmtRadioButton.IsChecked)
                _orderMode = "LMT";
        }

        void ApplySectionButtons(SectionConfig s)
        {
            for (int i = 0; i < s.Slots.Length && i < s.Selected.Length; i++)
                s.Slots[i].Text = DisplayForButton(s.Selected[i], s.Mode);
        }

        void SaveSection(SectionConfig s)
        {
            Preferences.Set(s.PrefKey, JsonSerializer.Serialize(s.Selected));
        }

        void LoadSection(SectionConfig s)
        {
            var json = Preferences.Get(s.PrefKey, "");
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var arr = JsonSerializer.Deserialize<string[]>(json);
                if (arr == null) return;
                var n = Math.Min(arr.Length, s.Selected.Length);
                for (int i = 0; i < n; i++)
                    s.Selected[i] = arr[i];
            }
            catch { }
        }
        #endregion

        #region Buton Format Yardýmcý Metotlarý || Button Helper Methods
        static string NormalizeForMode(string input, SectionMode mode)
        {
            var t = input?.Trim().ToUpperInvariant() ?? "";

            switch (mode)
            {
                case SectionMode.KAmount:
                    // "7.5K" gibi gelirse standardize et
                    if (t.EndsWith("K") && TryParseFlexible(t[..^1], out var kIn))
                        return FormatK(kIn * 1000m);
                    // "7500" gibi gelirse -> "7.5K"
                    if (TryParseFlexible(t, out var raw))
                        return raw >= 1000m ? FormatK(raw)
                                            : raw.ToString("0", CultureInfo.InvariantCulture);
                    return t;

                case SectionMode.Dollar:
                    return TryParseFlexible(t, out var d)
                        ? d.ToString("0.00", CultureInfo.InvariantCulture)
                        : t;

                case SectionMode.Percent:
                    return TryParseFlexible(t, out var p)
                        ? p.ToString("0.#", CultureInfo.InvariantCulture) + "%"
                        : (t.EndsWith("%") ? t : t + "%");

                case SectionMode.Decimal:
                    return TryParseFlexible(t, out var dec)
                        ? dec.ToString("0.00", CultureInfo.InvariantCulture)
                        : t;
            }
            return t;
        }


        #endregion

        #region Buton Deðer Yardýmcý Metotlarý || Button Value Helper Methods
        static string DisplayForButton(string normalized, SectionMode mode)
        {
            return mode switch
            {
                SectionMode.KAmount => "$" + normalized,
                SectionMode.Dollar => "$" + normalized,
                SectionMode.Percent => normalized,
                SectionMode.Decimal => normalized,
                _ => normalized
            };
        }
        #endregion

        #region Buton Deðer Dönüþtürme Metodu || Button Value Conversion Method
        static string ValueForEntry(string buttonText, SectionMode mode)
        {
            var s = (buttonText ?? "").Trim().ToUpperInvariant()
                    .Replace("$", "").Replace("%", "").Replace(" ", "").Replace(',', '.');

            switch (mode)
            {
                case SectionMode.KAmount:
                    if (s.EndsWith("K"))
                    {
                        var n = s[..^1];
                        return TryParseFlexible(n, out var k)
                            ? Math.Round(k * 1000m).ToString(CultureInfo.InvariantCulture)
                            : "";
                    }
                    return TryParseFlexible(s, out var raw)
                        ? raw.ToString("0", CultureInfo.InvariantCulture)
                        : "";

                case SectionMode.Dollar:
                    return TryParseFlexible(s, out var d)
                        ? d.ToString("0.00", CultureInfo.InvariantCulture)
                        : "";

                case SectionMode.Percent:
                    return TryParseFlexible(s, out var p)
                        ? p.ToString("0.#", CultureInfo.InvariantCulture)
                        : "";

                case SectionMode.Decimal:
                    return TryParseFlexible(s, out var dec)
                        ? dec.ToString("0.00", CultureInfo.InvariantCulture)
                        : "";
            }
            return "";
        }


        #endregion


        #region Number Entry Text Changed || Sayý Giriþi Metin Deðiþikliði

        private void OnNumberEntryTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue))
                return;

            // Girilen karakteri kontrol et
            if (!int.TryParse(e.NewTextValue, out int value) || value < 1 || value > 9)
            {
                // Geçersizse eski deðeri geri yükle
                ((Entry)sender).Text = e.OldTextValue;
            }
        }
        #endregion

        #region Seçili Sembol || Selected Symbol
        private void OnSymbolChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedIndex != -1)
            {
                var symbol = picker.Items[picker.SelectedIndex];
                Console.WriteLine($"Selected symbol: {symbol}");

                // symbols text için: seçilen sembolün conid’sini sözlükten set et
                if (_symbolConidMap.TryGetValue(symbol, out var cid))
                {
                    _selectedConid = cid;
                }
                else
                {
                    _selectedConid = null; // << yoksa stale conid kalmasýn
                }

                // Window baþlýðýna yansýt
                if (this.Window != null)
                    this.Window.Title = symbol;

                // Picker Title'ýný güncelle
                picker.Title = symbol;

                // DisplayLabel güncelle
                if (this.FindByName<Label>("DisplayLabel") is Label label)
                {
                    var currentText = label.Text ?? "";
                    var lines = currentText.Split(',', StringSplitOptions.None)
                                           .Select(l => l.Trim())
                                           .ToList();

                    bool stockUpdated = false;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].StartsWith("Symbol:"))
                        {
                            lines[i] = $"Symbol: {symbol}";
                            stockUpdated = true;
                            break;
                        }
                    }
                    if (!stockUpdated)
                    {
                        lines.Insert(0, $"Symbol: {symbol}");
                    }

                    label.Text = string.Join(", ", lines);
                }

                // marketprice için: sembol deðiþince fiyatý güncelle
                _ = UpdateMarketPriceAsync(); // marketprice için

                // SECDEF: yeni sembolde secTypes'ý yükle
                _ = LoadSecTypesForCurrentAsync();
            }
        }
        #endregion

        #region Order Mode || Sipariþ Modu
        private void OnCallOptionCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) Console.WriteLine("Order type: Call");
        }

        private void OnPutOptionCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) Console.WriteLine("Order type: Put");
        }
        #endregion

        #region Order Price Mode || Sipariþ Fiyat Modu
        private void OnTriggerPriceTextChanged(object sender, TextChangedEventArgs e)
        {
            QuantityCalculated();
            Console.WriteLine($"Trigger price: {e.NewTextValue}");
        }
        #endregion

        #region Offset Text Changed || Offset Metin Deðiþikliði
        private void OnIncreaseTriggerClicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("TriggerEntry") is Entry entry && decimal.TryParse(entry.Text, out decimal value))
            {
                entry.Text = (value + 1).ToString("F2");
            }
        }
        #endregion

        #region Decrease Trigger Clicked || Tetikleyici Azaltma Týklandý
        private void OnDecreaseTriggerClicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("TriggerEntry") is Entry entry && decimal.TryParse(entry.Text, out decimal value))
            {
                entry.Text = (value - 1).ToString("F2");
            }
        }
        #endregion

        #region Seçili Strike || Selected Strike
        private void OnStrikeChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedIndex != -1)
            {
                var strike = picker.Items[picker.SelectedIndex];
                Console.WriteLine($"Strike: {strike}");

                if (this.Window != null)
                    this.Window.Title = strike;

                picker.Title = strike; // Picker Title güncelle
            }
        }
        #endregion

        #region Expiry Picker Changed || Vade Seçici Deðiþti
        private void OnExpirationChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedIndex != -1)
            {
                var expiry = picker.Items[picker.SelectedIndex];
                Console.WriteLine($"Expiration: {expiry}");

                if (this.Window != null)
                    this.Window.Title = expiry;

                picker.Title = expiry; // Picker Title güncelle
            }
        }
        #endregion

        #region Position Size Text Changed || Pozisyon Boyutu Metin Deðiþikliði

        private void OnPositionTextChanged(object sender, TextChangedEventArgs e)
        {
            QuantityCalculated();

        }
        private void QuantityCalculated()
        {
            if (!string.IsNullOrWhiteSpace(TriggerEntry.Text) &&
    !string.IsNullOrWhiteSpace(PositionEntry.Text))
            {
                if (decimal.TryParse(TriggerEntry.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var trigger) &&
                    decimal.TryParse(PositionEntry.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var position) &&
                    trigger != 0)
                {
                    var quantity = (int)Math.Round(position / trigger, MidpointRounding.AwayFromZero);
                    lblQuantity.Text = quantity.ToString();
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
        #endregion


        #region Stop Loss Text Changed || Stop Loss Metin Deðiþikliði
        private void OnStopLossTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Stop loss: {e.NewTextValue}");
        }
        #endregion

        #region Stop Loss Preset Clicked || Stop Loss Ön Ayarý Týklandý
        private void OnStopLossPreset(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("StopLossEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("$", "");
            }
        }
        #endregion



        #region Profit Text Changed || Kar Metin Deðiþikliði
        private void OnProfitPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("ProfitEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("%", "");
                Console.WriteLine($"Profit taking set to {entry.Text}%");
            }
        }
        #endregion

        #region Trail Text Changed || Trail Metin Deðiþikliði
        private void OnTrailClicked(object sender, EventArgs e)
        {
            Console.WriteLine("Invalidate action triggered");
        }
        #endregion

        #region Breakeven Clicked || Breakeven Týklandý
        private void OnBreakevenClicked(object sender, EventArgs e)
        {

        }
        #endregion

        #region Offset Preset Clicked || Offset Ön Ayarý Týklandý
        private void OnOffsetPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("OffsetEntry") is Entry entry)
            {
                entry.Text = btn.Text;
                Console.WriteLine($"Offset set to {entry.Text}");
            }
        }
        #endregion

        #region Cancel Clicked || Ýptal Týklandý
        private void OnCancelClicked(object sender, EventArgs e)
        {
            Element cursor = this;
            while (cursor?.Parent is not null &&
                   cursor.Parent is not Microsoft.Maui.Controls.Layout)
            {
                cursor = cursor.Parent;
            }

            var container = cursor?.Parent as Microsoft.Maui.Controls.Layout;
            if (container is not null)
                container.Children.Remove(this);
        }
        #endregion

        #region Order Temizleme || Order Clearing
        private void OnClearOrdersClicked(object sender, EventArgs e)
        {
            OrdersContainer.Children.Clear();
        }

        #endregion
        #region Api Request || Api Ýstek 
        private async void OnGetTickleClicked(object sender, EventArgs e)
        {

            try
            {
                string url = Configs.BaseUrl + "/tickle";
                string result = await _apiService.GetAsync(url);
                await ShowAlert("Auto Call", $"API Response: {result}", "OK");
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", ex.Message, "OK");
            }

        }

        private async void OnGetStatusClicked(object sender, EventArgs e)
        {
            try
            {
                string url = Configs.BaseUrl + "/status";
                string result = await _apiService.GetAsync(url);
                await ShowAlert("Auto Call", $"API Response: {result}", "OK");

            }
            catch (Exception ex)
            {

                await ShowAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnPostSymbolClicked(object sender, EventArgs e)
        {
            try
            {
                string url = Configs.BaseUrl + "/getSymbol";

                var request = new ResultSymbols
                {
                    symbol = "AAPL",
                    name = true,
                    secType = "STK"
                };

                // TResponse artýk ResultSymbols olmalý, string deðil
                List<ResultSymbols> result = await _apiService.PostAsync<ResultSymbols, List<ResultSymbols>>(url, request);

                var first = result.FirstOrDefault();
                if (first != null)
                {
                    await ShowAlert("Success",
                        $"Symbol: {first.symbol}, Name: {first.name}, SecType: {first.secType}", "OK");
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", ex.Message, "OK");
            }
        }


        private async void OnDeleteOrderClicked(object sender, EventArgs e)
        {
            int symbolId = 265598; // veya kullanýcýdan alabilirsiniz

            bool confirm = await ShowConfirm(
                "Delete Confirmation",
                $"Are you sure you want to delete order '{symbolId}'?",
                "OK",
                "Cancel"
            );

            if (!confirm)
                return;

            try
            {
                string url = Configs.BaseUrl + "/orders/";
                await _apiService.DeleteAsync(url, symbolId);

                await ShowAlert("Success", $"Symbol '{symbolId}' deleted successfully!", "OK");
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnSecdefStrikeClicked(object sender, EventArgs e)
        {
            try
            {
                string result = await _apiService.GetSecDefStrikeAsync("46639520", "ASDA", "STK");
                await ShowAlert("Success", $"API Response: {result}", "OK");
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", ex.Message, "OK");
            }
        }

        private async void OngGetSecdef(object sender, EventArgs e)
        {
            try
            {
                var response = await _apiService.GetSecDefAsync("46639520");

                if (response?.secdef != null && response.secdef.Count > 0)
                {
                    var first = response.secdef[0];
                    string msg = $"Name: {first.name}\nTicker: {first.ticker}\nCurrency: {first.currency}\nHasOptions: {first.hasOptions}";
                    await ShowAlert("SecDef Item", msg, "OK");
                }
                else
                {
                    await ShowAlert("SecDef", "Hiç kayýt yok.", "OK");
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnGetInfoClicked(object sender, EventArgs e)
        {
            try
            {
                var info = await _apiService.GetInfoAsync("46639520");
                await ShowAlert("Info",
                    $"Conid: {info.conid}\nTicker: {info.ticker}\nCompany: {info.companyName}\nCurrency: {info.currency}",
                    "OK");
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", ex.Message, "OK");
            }
        }


        // Örnek buton click event
        private async void OnGetPortfolioClicked(object sender, EventArgs e)
        {
            try
            {
                // ApiService instance'ýný kullan
                var portfolioList = await _apiService.GetPortfolioAsync();

                if (portfolioList != null && portfolioList.Count > 0)
                {
                    // Ýlk portfolio item'ý gösterelim
                    var firstItem = portfolioList[0];

                    string message =
                        $"ID: {firstItem.id}\n" +
                        $"DisplayName: {firstItem.displayName}\n" +
                        $"AccountId: {firstItem.accountId}\n" +
                        $"Currency: {firstItem.currency}\n" +
                        $"Type: {firstItem.type}";

                    await ShowAlert("Portfolio Item", message, "OK");
                }
                else
                {
                    await ShowAlert("Portfolio", "Hiç kayýt bulunamadý.", "OK");
                }
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", ex.Message, "OK");
            }
        }

        private async void LoadOrders(object sender, EventArgs e)
        {
            try
            {
                string url = Configs.BaseUrl + "/orders";

                // Raw JSON olarak çekiyoruz
                string json = await _apiService.GetAsync(url);

                // JSON'u dinamik olarak parse ediyoruz (tip hatasý olmayacak)
                var orders = System.Text.Json.JsonSerializer.Deserialize<List<JsonElement>>(json);

                foreach (var order in orders)
                {
                    string ticker = order.GetProperty("ticker").GetString();
                    string companyName = order.GetProperty("companyName").GetString();
                    string side = order.GetProperty("side").GetString();
                    string status = order.GetProperty("status").GetString();
                    string price = order.GetProperty("price").GetRawText(); // number olabilir
                    string totalSize = order.GetProperty("totalSize").GetRawText(); // number olabilir

                    string message = $"Ticker: {ticker}\n" +
                                     $"Company: {companyName}\n" +
                                     $"Side: {side}\n" +
                                     $"Price: {price}\n" +
                                     $"Status: {status}\n" +
                                     $"Quantity: {totalSize}";
                }
                string alertMessage = "Baþarýlý";
                await ShowAlert("Order Info", alertMessage, "OK");
            }
            catch (Exception ex)
            {
                await ShowAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnPostOrderClicked(object sender, EventArgs e)
        {
            try
            {
                var request = new PostOrderItem
                {
                    conid = 46639520,
                    orderType = "LMT",
                    price = 1700,
                    quantity = 1000, // artýk API kabul eder
                    side = "BUY",
                    tif = "DAY"
                };

                var result = await _apiService.CreateOrderAsync(request);

                if (result != null)
                {
                    await ShowAlert("Success",
                        $"Order ID: {result.OrderId ?? "(null)"}\n" +
                        $"Status: {result.OrderStatus ?? "(null)"}\n" +
                        $"Encrypt: {result.EncryptMessage ?? "(null)"}",
                        "OK");
                }
                else
                {
                    await ShowAlert("Info", "No order returned from API.", "OK");
                }
            }
            catch (Exception ex)
            {
                // API hatasý veya network hatasý buraya gelir
                await ShowAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnGetOrdersClicked(object sender, EventArgs e)
        {
            try
            {
                int orderId = 760072067; // Örnek order ID
                var order = await _apiService.GetAsync<OrderResponseDto>(Configs.BaseUrl + $"/orders/by-id/{orderId}");

                if (order == null)
                {
                    await ShowAlert("Hata", "Servisten veri gelmedi veya hata oluþtu.", "Tamam");
                    return;
                }

                if (!order.Bulunan)
                {
                    await ShowAlert("Bilgi", "Order bulunamadý.", "Tamam");
                    return;
                }

                // Order bulundu, DisplayAlert ile göster
                string message =
                    $"Order ID: {order.Order?.Id}\n" +
                    $"Ürün: {order.Order?.ProductName}\n" +
                    $"Adet: {order.Order?.Quantity}\n" +
                    $"Fiyat: {order.Order?.Price}\n" +
                    $"Durum: {order.Order?.Status}";

                await ShowAlert("Order Detaylarý", message, "Tamam");
            }
            catch (Exception ex)
            {
                await ShowAlert("Hata", $"Order çekilirken bir hata oluþtu: {ex.Message}", "Tamam");
            }
        }

        #endregion

        #region Sað Týk Düzenleme || Right Click Edit
        private async void OnPresetRightClick(object sender, TappedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (!TryFindSectionAndIndex(btn, out var s, out var slotIndex)) return; // aþaðýdaki helper

            // mevcut deðeri parse et (virgül/nokta toleranslý)
            string old = btn.Text?.Trim() ?? "";
            old = old.Replace("$", "").Replace("%", "").Trim().Replace(',', '.');

            string? input = await PromptAsync(
                "Edit Preset",
                s.Mode switch
                {
                    SectionMode.KAmount => "New value (e.g. 1.5K or 1500):",
                    SectionMode.Dollar => "New value ($):",
                    SectionMode.Percent => "New value (%):",
                    _ => "New value:"
                },
                "Save", "Cancel",
                initialValue: old,
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(input)) return;

            // 1) Normalizasyon (K, %, $ vb.)
            var normalized = NormalizeForMode(input!, s.Mode);

            // 2) Seçili slotu güncelle + kaydet + butonlarý yenile
            s.Selected[slotIndex] = normalized;
            SaveSection(s);
            ApplySectionButtons(s); // -> "$7.5K" gibi doðru metni basar

            // 3) Ýlgili Entry'ye ham deðeri yaz (7500 gibi)
            var displayText = DisplayForButton(normalized, s.Mode);
            s.TargetEntry.Text = ValueForEntry(displayText, s.Mode);
        }

        #endregion

        #region Parse & Format Helpers || Ayrýþtýrma ve Biçimlendirme Yardýmcýlarý
        static bool TryParseFlexible(string text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.Trim().Replace("$", "").Replace("%", "").Replace(" ", "").Replace(',', '.');
            return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
        static string FormatK(decimal raw) // raw = 7500 -> "7.5K"
        {
            var k = raw / 1000m;
            var body = (k % 1m == 0m) ? k.ToString("0", CultureInfo.InvariantCulture)
                                      : k.ToString("0.#", CultureInfo.InvariantCulture);
            return body + "K";
        }
        private bool TryFindSectionAndIndex(Button btn, out SectionConfig s, out int slotIndex)
        {
            s = null; slotIndex = -1;
            var id = (btn.CommandParameter as string)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(id) || !sections.TryGetValue(id, out s))
                return false;

            slotIndex = Array.IndexOf(s.Slots, btn);
            if (slotIndex < 0) slotIndex = 0;
            return true;
        }



        private async void OnCreateOrdersClicked(object sender, EventArgs e)
        {
            try
            {
                string oldconid = _selectedConid.Value.ToString(CultureInfo.InvariantCulture);
                string conid = _lastConId.ToString(CultureInfo.InvariantCulture);

                // Ondalýk deðerleri parse ederken InvariantCulture kullan
                double trigger = double.Parse(TriggerEntry.Text, CultureInfo.InvariantCulture);
                double offset = double.Parse(OffsetEntry.Text, CultureInfo.InvariantCulture);
                double positionSize = double.Parse(PositionEntry.Text, CultureInfo.InvariantCulture);
                double stopLoss = double.Parse(StopLossEntry.Text, CultureInfo.InvariantCulture);

                string orderMode = _orderMode;
                string tif = ExpPicker.SelectedItem.ToString();


                // Query string oluþtur
                var url = Configs.BaseUrl + $"/orderUI?" +
                          $"oldconid={oldconid}" +
                          $"&conid={conid}" +
                          $"&trigger={trigger.ToString(CultureInfo.InvariantCulture)}" +
                          $"&orderMode={orderMode}" +
                          $"&offset={offset.ToString(CultureInfo.InvariantCulture)}" +
                          $"&positionSize={positionSize.ToString(CultureInfo.InvariantCulture)}" +
                          $"&stopLoss={stopLoss.ToString(CultureInfo.InvariantCulture)}" +
                          $"&tif={tif}";

                // POST request (body null)
                var response = await _apiService.PostAsync<object, object>(url, null);

                // Response’u JSON string olarak göster
                var jsonString = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });

                await ShowAlert("API Response", jsonString, "Tamam");


            }

            catch (Exception ex)
            {
                await ShowAlert("Exception", ex.Message, "Tamam");
            }

        }


        #endregion


        // =======================
        // symbols text için: Arama Entry’si & Öneri CollectionView handler’larý
        // =======================

        // XAML: TextChanged="OnSymbolSearchTextChanged"
        private async void OnSymbolSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var query = e.NewTextValue?.Trim() ?? string.Empty;

                // boþsa listeyi gizle
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    _symbolCts?.Cancel();
                    _symbolResults.Clear();
                    SymbolSuggestions.IsVisible = false;
                    return;
                }

                // debounce + önceki isteði iptal
                _symbolCts?.Cancel();
                _symbolCts = new CancellationTokenSource();
                var token = _symbolCts.Token;

                // küçük bir gecikme (debounce)
                await Task.Delay(250, token);

                await FetchAndBindSymbolSuggestionsAsync(query, token);
            }
            catch (TaskCanceledException)
            {
                // yoksay
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Symbol search error: " + ex.Message);
                _symbolResults.Clear();
                SymbolSuggestions.IsVisible = false;
            }
        }

        // XAML: SelectionChanged="OnSymbolSuggestionSelected"
        private void OnSymbolSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.CurrentSelection?.FirstOrDefault() is SymbolSearchResponse sel)
                {
                    // Entry’ye seçilen sembolü yaz
                    SymbolSearchEntry.Text = sel.symbol;

                    // StockPicker’ý seçime göre güncelle
                    StockPicker.Items.Clear();
                    StockPicker.Items.Add(sel.symbol);
                    StockPicker.SelectedIndex = 0;

                    // conid’i doðrudan seçilen objeden al; yoksa sözlükten dene
                    if (sel.conid.HasValue)
                        _selectedConid = sel.conid.Value;
                    else if (_symbolConidMap.TryGetValue(sel.symbol, out var cid))
                        _selectedConid = cid;
                    else
                        _selectedConid = null;

                    // öneri listesini gizle
                    SymbolSuggestions.IsVisible = false;
                    SymbolSuggestions.SelectedItem = null;

                    // marketprice için: seçimden sonra fiyatý güncelle
                    _ = UpdateMarketPriceAsync(); // marketprice için

                    // SECDEF: yeni sembolde secTypes'ý yükle
                    _ = LoadSecTypesForCurrentAsync();
                }
            }
            catch { /* yoksay */ }
        }

        // symbols text için: API’den öneri çekme + conid + companyHeader yakalama
        private async Task FetchAndBindSymbolSuggestionsAsync(string query, CancellationToken token)
        {
            // API: /getSymbol -> request body: ResultSymbols { symbol, name(bool), secType }
            var url = Configs.BaseUrl + "/getSymbol";
            var request = new ResultSymbols
            {
                symbol = query,
                name = true,     // sunucudan isim bilgisini döndür (DTO bool, response’da string olabilir)
                secType = "STK"
            };

            // Response þemasý garanti deðil (DTO’da name bool), bu yüzden JsonElement ile esnek parse
            List<JsonElement> rawList = await _apiService.PostAsync<ResultSymbols, List<JsonElement>>(url, request);

            // token iptal edildiyse dön
            if (token.IsCancellationRequested) return;

            var list = new List<SymbolSearchResponse>();

            foreach (var item in rawList)
            {
                // symbol
                string symbol = item.TryGetProperty("symbol", out var sEl) ? (sEl.GetString() ?? "") : "";

                // name (string olabilir / olmayabilir)
                string? displayName = null;
                if (item.TryGetProperty("name", out var nEl))
                {
                    if (nEl.ValueKind == JsonValueKind.String)
                        displayName = nEl.GetString();
                    // bazý servisler "name": true/false döndürebilir, o zaman null býrak
                }

                // companyheader için: companyHeader yakala
                string? header = null;
                if (item.TryGetProperty("companyHeader", out var hEl) && hEl.ValueKind == JsonValueKind.String)
                {
                    header = hEl.GetString();
                }

                // conid (number veya string gelebilir)
                long? conid = null;
                if (item.TryGetProperty("conid", out var cEl))
                {
                    switch (cEl.ValueKind)
                    {
                        case JsonValueKind.Number:
                            if (cEl.TryGetInt64(out var cnum))
                                conid = cnum;
                            break;
                        case JsonValueKind.String:
                            if (long.TryParse(cEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cstr))
                                conid = cstr;
                            break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    if (conid.HasValue)
                        _symbolConidMap[symbol] = conid.Value; // mevcut davranýþ dursun

                    list.Add(new SymbolSearchResponse
                    {
                        symbol = symbol,
                        name = displayName,
                        conid = conid,
                        companyHeader = header // companyheader için
                    });
                }
            }

            // UI’ya bas
            _symbolResults.Clear();
            foreach (var it in list.Take(200)) // güvenli sýnýr
                _symbolResults.Add(it);

            SymbolSuggestions.IsVisible = _symbolResults.Count > 0;
        }

        // marketprice için: seçili sembol/conid’e göre fiyatý çek ve UI’a yaz
        private async Task UpdateMarketPriceAsync() // marketprice için
        {
            try
            {
                // Öncelik: conid varsa onu kullan, yoksa sembol string’i kullan
                string? symbolParam = null;
                if (_selectedConid.HasValue)
                {
                    symbolParam = _selectedConid.Value.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    if (StockPicker?.SelectedIndex >= 0 && StockPicker.SelectedIndex < StockPicker.Items.Count)
                        symbolParam = StockPicker.Items[StockPicker.SelectedIndex];
                    else
                        symbolParam = SymbolSearchEntry?.Text;
                }

                if (string.IsNullOrWhiteSpace(symbolParam))
                {
                    ApplyPriceUI(null, null);
                    return;
                }

                // /snapshot?symbol={conid or symbol}
                var url = Configs.BaseUrl.TrimEnd('/') + "/snapshot?symbol=" + Uri.EscapeDataString(symbolParam);

                // debounce/iptal
                _priceCts?.Cancel();
                _priceCts = new CancellationTokenSource();
                var token = _priceCts.Token;

                var response = await _apiService.GetAsync(url);
                if (token.IsCancellationRequested) return;

                var raw = (response ?? "").Trim();

                // 1) Düz sayý string’i (örn "123.45")
                if (decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var direct))
                {
                    ApplyPriceUI(direct, false);  // kapalý deðil varsayýyoruz (flag yok)
                    return;
                }

                // 2) JSON parse (object veya array)
                if (raw.StartsWith("{") || raw.StartsWith("["))
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;

                    // --- ÖZEL DÝZÝ FORMATI: [ <number>, { ... } ] ---
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        decimal? arrayPrice = null;
                        bool? arrayMarketClosed = null;

                        // Ýlk eleman sayý ise
                        var first = root[0];
                        if (first.ValueKind == JsonValueKind.Number && first.TryGetDecimal(out var arrNum))
                            arrayPrice = arrNum;

                        // Ýkinci eleman object ise (flag ve/veya last vb burada)
                        if (root.GetArrayLength() >= 2 && root[1].ValueKind == JsonValueKind.Object)
                        {
                            var second = root[1];

                            // market_closed bayraðý
                            arrayMarketClosed = TryExtractBool(second, "market_closed", "marketClosed", "isMarketClosed");

                            // fiyat anahtarlarý
                            string[] priceKeys = { "marketPrice", "last", "lastPrice", "mark", "mid", "close", "price", "p" };
                            if (TryExtractDecimal(second, priceKeys, out var objPrice))
                            {
                                arrayPrice = objPrice;
                            }
                            else
                            {
                                // bid/ask’tan mid dene
                                var bid = TryExtractOneOf(second, "bid", "bestBid", "b");
                                var ask = TryExtractOneOf(second, "ask", "bestAsk", "a");
                                if (bid.HasValue && ask.HasValue)
                                    arrayPrice = (bid.Value + ask.Value) / 2m;
                                else if (bid.HasValue && !ask.HasValue)
                                    arrayPrice ??= bid.Value; // en azýndan bid’i göster
                            }
                        }

                        ApplyPriceUI(arrayPrice, arrayMarketClosed);
                        return;
                    }

                    // --- Genel durum: object (veya farklý yapý) ---
                    {
                        bool marketClosed =
                            TryExtractBool(root, "market_closed", "marketClosed", "isMarketClosed") ||
                            (root.TryGetProperty("data", out var dEl) && TryExtractBool(dEl, "market_closed", "marketClosed", "isMarketClosed")) ||
                            (root.TryGetProperty("quote", out var qEl) && TryExtractBool(qEl, "market_closed", "marketClosed", "isMarketClosed"));

                        string[] priceKeys = { "marketPrice", "last", "lastPrice", "mark", "mid", "close", "price", "p" };

                        if (TryExtractDecimal(root, priceKeys, out var price) ||
                            (root.TryGetProperty("data", out var dataObj) && TryExtractDecimal(dataObj, priceKeys, out price)) ||
                            (root.TryGetProperty("quote", out var quoteObj) && TryExtractDecimal(quoteObj, priceKeys, out price)))
                        {
                            ApplyPriceUI(price, marketClosed);
                            return;
                        }

                        // Fiyat yoksa bid/ask’tan mid dene
                        decimal? bid = TryExtractOneOf(root, "bid", "bestBid", "b");
                        decimal? ask = TryExtractOneOf(root, "ask", "bestAsk", "a");

                        if (!bid.HasValue && root.TryGetProperty("data", out var d1))
                        {
                            bid ??= TryExtractOneOf(d1, "bid", "bestBid", "b");
                            ask ??= TryExtractOneOf(d1, "ask", "bestAsk", "a");
                        }
                        if (!bid.HasValue && root.TryGetProperty("quote", out var q1))
                        {
                            bid ??= TryExtractOneOf(q1, "bid", "bestBid", "b");
                            ask ??= TryExtractOneOf(q1, "ask", "bestAsk", "a");
                        }

                        if (bid.HasValue && ask.HasValue)
                        {
                            var mid = (bid.Value + ask.Value) / 2m;
                            ApplyPriceUI(mid, marketClosed);
                            return;
                        }
                        if (bid.HasValue) // en azýndan bid göster
                        {
                            ApplyPriceUI(bid.Value, marketClosed);
                            return;
                        }
                    }
                }

                // Hiçbiri olmadýysa
                ApplyPriceUI(null, null);
            }
            catch
            {
                ApplyPriceUI(null, null);
            }

            // ---- helpers (marketprice için) ----
            static bool TryExtractDecimal(JsonElement el, string[] names, out decimal val)
            {
                foreach (var n in names)
                {
                    if (el.TryGetProperty(n, out var p))
                    {
                        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out val))
                            return true;
                        if (p.ValueKind == JsonValueKind.String &&
                            decimal.TryParse((p.GetString() ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out val))
                            return true;
                    }
                }
                val = 0m; return false;
            }

            static decimal? TryExtractOneOf(JsonElement el, params string[] names)
            {
                foreach (var n in names)
                {
                    if (el.TryGetProperty(n, out var p))
                    {
                        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d)) return d;
                        if (p.ValueKind == JsonValueKind.String &&
                            decimal.TryParse((p.GetString() ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds))
                            return ds;
                    }
                }
                return null;
            }

            static bool TryExtractBool(JsonElement el, params string[] names)
            {
                foreach (var n in names)
                {
                    if (el.TryGetProperty(n, out var p))
                    {
                        if (p.ValueKind == JsonValueKind.True) return true;
                        if (p.ValueKind == JsonValueKind.False) return false;
                        if (p.ValueKind == JsonValueKind.String)
                        {
                            var s = p.GetString();
                            if (bool.TryParse(s, out var b)) return b;
                            if (string.Equals(s, "1") || string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase)) return true;
                            if (string.Equals(s, "0") || string.Equals(s, "no", StringComparison.OrdinalIgnoreCase)) return false;
                        }
                        if (p.ValueKind == JsonValueKind.Number)
                        {
                            if (p.TryGetInt32(out var num)) return num != 0;
                        }
                    }
                }
                return false;
            }
        }

        // ===========================
        // SECDEF: symbol -> secTypes
        // ===========================
        private async Task LoadSecTypesForCurrentAsync()
        {
            try
            {
                if (!_selectedConid.HasValue) return;

                var symbolText = StockPicker?.SelectedIndex >= 0
                    ? StockPicker.Items[StockPicker.SelectedIndex]
                    : (SymbolSearchEntry?.Text ?? string.Empty);

                if (string.IsNullOrWhiteSpace(symbolText)) return;

                var url = Configs.BaseUrl.TrimEnd('/') + "/secdef/conid/info";
                var req = new { conid = _selectedConid.Value.ToString(CultureInfo.InvariantCulture), symbol = symbolText };

                var resp = await _apiService.PostAsync<object, ConidInfoResponse>(url, req);

                _secdefSecTypes = (resp?.secTypes ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                SecTypePicker.Items.Clear();
                foreach (var st in _secdefSecTypes)
                    SecTypePicker.Items.Add(st);

                if (_secdefSecTypes.Count > 0)
                    SecTypePicker.SelectedIndex = 0;

                MonthsPicker.Items.Clear();
                _secdefMonths.Clear();
                _secdefExchanges.Clear();
            }
            catch
            {
                SecTypePicker.Items.Clear();
                MonthsPicker.Items.Clear();
                _secdefSecTypes.Clear();
                _secdefMonths.Clear();
                _secdefExchanges.Clear();
            }
        }

        // ==================================
        // SECDEF: secType -> months/exchanges
        // ==================================
        // XAML -> SecTypePicker.SelectedIndexChanged="OnSecTypeChanged"
        private async void OnSecTypeChanged(object sender, EventArgs e)
        {
            try
            {
                if (SecTypePicker.SelectedIndex < 0) return;
                if (!_selectedConid.HasValue) return;

                var secType = SecTypePicker.Items[SecTypePicker.SelectedIndex];
                SecTypePicker.Title = secType;
                _currentSecType = secType;

                var symbolText = StockPicker?.SelectedIndex >= 0
                    ? StockPicker.Items[StockPicker.SelectedIndex]
                    : (SymbolSearchEntry?.Text ?? string.Empty);

                if (string.IsNullOrWhiteSpace(symbolText)) return;

                var url = Configs.BaseUrl.TrimEnd('/') + "/secdef/sectype/info";
                var req = new { conid = _selectedConid.Value.ToString(CultureInfo.InvariantCulture), symbol = symbolText, secType = secType };

                var resp = await _apiService.PostAsync<object, SecTypeDetailResponse>(url, req);

                _secdefMonths = (resp?.months ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                _secdefExchanges = (resp?.exchanges ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                MonthsPicker.Items.Clear();
                foreach (var m in _secdefMonths)
                    MonthsPicker.Items.Add(m);

                if (_secdefMonths.Count > 0)
                    MonthsPicker.SelectedIndex = 0;


            }
            catch
            {
                MonthsPicker.Items.Clear();
                _secdefMonths.Clear();
                _secdefExchanges.Clear();

            }
            _currentExchange = PickBestExchange(_secdefExchanges);
        }
        private string PickBestExchange(List<string> exchanges)
        {
            if (exchanges == null || exchanges.Count == 0) return string.Empty;
            var prios = new[] { "SMART", "GLOBEX", "CBOE", "ISE", "ARCA", "BOX", "NYSE" };
            var best = exchanges.FirstOrDefault(x => prios.Contains(x?.ToUpperInvariant()));
            return string.IsNullOrWhiteSpace(best) ? (exchanges.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty) : best;
        }

        // ==========================
        // SECDEF: month -> strikes
        // ==========================
        // XAML -> MonthsPicker.SelectedIndexChanged="OnMonthChanged"
        private async void OnMonthChanged(object sender, EventArgs e)
        {
            try
            {
                if (MonthsPicker.SelectedIndex < 0) return;
                if (SecTypePicker.SelectedIndex < 0) return;
                if (!_selectedConid.HasValue) return;

                var month = MonthsPicker.Items[MonthsPicker.SelectedIndex];
                MonthsPicker.Title = month;
                _currentMonth = month;
                ClearMaturityUI();


                // her ay deðiþtiðinde strike listesini temizleyelim ki üst üste eklenmesin
                StrikesPicker.Items.Clear();
                var secType = SecTypePicker.Items[SecTypePicker.SelectedIndex];
                var exchange = _currentExchange ?? string.Empty;
                _currentExchange = exchange;
                var conid = _selectedConid.Value.ToString(CultureInfo.InvariantCulture);
                var url = Configs.BaseUrl.TrimEnd('/') + "/secdef/strikes"
    + $"?conid={conid}&secType={Uri.EscapeDataString(secType)}&month={Uri.EscapeDataString(month)}";
                if (!string.IsNullOrWhiteSpace(exchange))
                    url += $"&exchange={Uri.EscapeDataString(exchange)}";

                var resp = await _apiService.GetAsync(url);
                var json = resp;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var strikesData = JsonSerializer.Deserialize<StrikesResponses>(json, options);
                var strikes = new List<decimal>();
                if (strikesData != null)
                {
                    strikes.AddRange(strikesData.Call);
                    strikes.AddRange(strikesData.Put);

                }
                _lastStrikes = strikesData ?? new StrikesResponses();
                RebuildStrikesPicker();

                //ApplyStrikesToUI(strikes);
            }
            catch (Exception)
            {
                // yoksay
            }
        }
        private void RebuildStrikesPicker()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StrikesPicker.Items.Clear();
                if (_lastStrikes == null)
                {
                    ClearMaturityUI();
                    return;
                }

                IEnumerable<decimal> list = _currentRight == "P" ? _lastStrikes.Put : _lastStrikes.Call;

                if (list != null)
                {
                    foreach (var s in list)
                        StrikesPicker.Items.Add(s.ToString(CultureInfo.InvariantCulture));
                }

                // varsa ilkini seç ve maturity çek
                if (StrikesPicker.Items.Count > 0)
                {
                    StrikesPicker.SelectedIndex = 0;
                    _ = LoadMaturityDateForSelectionAsync();
                }
                else
                {
                    // hiç yoksa picker’ý temizle
                    StrikesPicker.Title = "—";
                    ClearMaturityUI();
                }
            });
        }
        private async Task LoadMaturityDateForSelectionAsync()
        {
            try
            {
                if (!_selectedConid.HasValue) return;
                if (string.IsNullOrWhiteSpace(_currentSecType)) return;
                if (string.IsNullOrWhiteSpace(_currentMonth)) return;
                if (StrikesPicker.SelectedIndex < 0) return;

                var conid = _selectedConid.Value.ToString(CultureInfo.InvariantCulture);
                var secType = _currentSecType;
                var month = _currentMonth;
                var exchange = _currentExchange ?? string.Empty;

                // strike normalize
                var strikeText = StrikesPicker.SelectedItem?.ToString();
                if (string.IsNullOrWhiteSpace(strikeText)) return;

                if (!decimal.TryParse(strikeText, NumberStyles.Any, CultureInfo.InvariantCulture, out var strikeDec) &&
                    !decimal.TryParse(strikeText, NumberStyles.Any, new CultureInfo("tr-TR"), out strikeDec))
                    return;

                var strikeParam = strikeDec.ToString(CultureInfo.InvariantCulture);

                // URL
                var qs = new List<string>
        {
            "conid="   + Uri.EscapeDataString(conid),
            "secType=" + Uri.EscapeDataString(secType),
            "month="   + Uri.EscapeDataString(month),
            "strike="  + Uri.EscapeDataString(strikeParam),
            "right="   + Uri.EscapeDataString(_currentRight)
        };
                if (!string.IsNullOrWhiteSpace(exchange))
                    qs.Add("exchange=" + Uri.EscapeDataString(exchange));

                var url = Configs.BaseUrl.TrimEnd('/') + "/secdef/info?" + string.Join("&", qs);

                var raw = await _apiService.GetAsync(url);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    SetMaturityUI(Array.Empty<string>());
                    return;
                }

                var maturities = new List<string>();
                var lastConids = new List<string>();

                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in root.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                // maturityDate
                                if (item.TryGetProperty("maturityDate", out var m) && m.ValueKind == JsonValueKind.String)
                                {
                                    var s = m.GetString();
                                    if (!string.IsNullOrWhiteSpace(s))
                                        maturities.Add(s!);
                                }

                                // conid (lastConid)
                                if (item.TryGetProperty("conid", out var c) &&
                                    (c.ValueKind == JsonValueKind.Number || c.ValueKind == JsonValueKind.String))
                                {
                                    string conidValue = c.ValueKind == JsonValueKind.Number ? c.GetInt64().ToString() : c.GetString()!;
                                    lastConids.Add(conidValue);
                                    _lastConId = conidValue;
                                }
                            }
                        }
                    }

                    // fallback
                    if (maturities.Count == 0)
                    {
                        string? one = ExtractMaturityAny(root)
                                      ?? (root.TryGetProperty("data", out var dataEl) ? ExtractMaturityAny(dataEl) : null)
                                      ?? (root.TryGetProperty("quote", out var quoteEl) ? ExtractMaturityAny(quoteEl) : null);
                        if (!string.IsNullOrWhiteSpace(one))
                            maturities.Add(one!);
                    }
                }
                catch
                {
                    var t = raw.Trim('"');
                    if (!string.IsNullOrWhiteSpace(t)) maturities.Add(t);
                }

                // normalize + distinct + sýralama
                var normalizedMaturities = maturities
                    .Select(NormalizeMaturityDate)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x)
                    .ToList();

                var normalizedConids = lastConids.Distinct().ToList();

                SetMaturityUI(normalizedMaturities);
            }
            catch
            {
                SetMaturityUI(Array.Empty<string>());
            }

            // ---- helpers ----
            static string? ExtractMaturityAny(JsonElement el)
            {
                string[] keys = { "maturityDate", "maturity", "expiryDate", "expiry", "expirationDate", "expiration" };
                foreach (var k in keys)
                {
                    if (el.TryGetProperty(k, out var p))
                    {
                        if (p.ValueKind == JsonValueKind.String) return p.GetString();
                        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var num))
                        {
                            var dt = (num > 9999999999)
                                ? DateTimeOffset.FromUnixTimeMilliseconds(num).UtcDateTime
                                : DateTimeOffset.FromUnixTimeSeconds(num).UtcDateTime;
                            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        }
                    }
                }
                return null;
            }

            static string? NormalizeMaturityDate(string? raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return null;
                var t = raw.Trim();

                if (DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                    return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                if (t.Length == 8 && t.All(char.IsDigit))
                {
                    var y = int.Parse(t[..4]);
                    var m = int.Parse(t.Substring(4, 2));
                    var d = int.Parse(t.Substring(6, 2));
                    return new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                if (long.TryParse(t, out var num))
                {
                    var dt2 = (num > 9999999999)
                        ? DateTimeOffset.FromUnixTimeMilliseconds(num).UtcDateTime
                        : DateTimeOffset.FromUnixTimeSeconds(num).UtcDateTime;
                    return dt2.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                }

                return t;
            }

            void SetMaturityUI(IEnumerable<string> maturities)
            {
                var matList = maturities?.ToList() ?? new List<string>();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MaturityDateLabel.Items.Clear();

                    if (matList.Count == 0)
                    {
                        MaturityDateLabel.Title = "—";
                        MaturityDateLabel.SelectedIndex = -1;
                    }
                    else
                    {
                        foreach (var m in matList)
                        {
                            MaturityDateLabel.Items.Add(m);
                        }

                        MaturityDateLabel.SelectedIndex = 0;
                        MaturityDateLabel.Title = matList[0];
                    }
                });
            }

        }

        private async void StrikesPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (StrikesPicker.SelectedIndex >= 0)
                StrikesPicker.Title = StrikesPicker.SelectedItem.ToString();
            await LoadMaturityDateForSelectionAsync();
        }
        private void MaturityDateLabel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MaturityDateLabel.SelectedIndex >= 0)
                MaturityDateLabel.Title = MaturityDateLabel.Items[MaturityDateLabel.SelectedIndex];
        }
        private void ClearMaturityUI()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MaturityDateLabel.Items.Clear();
                MaturityDateLabel.Title = "—";
                MaturityDateLabel.SelectedIndex = -1;
            });
        }

        public class StrikesResponses
        {
            public List<decimal> Call { get; set; } = new();
            public List<decimal> Put { get; set; } = new();
        }

        private CancellationTokenSource? _autoPriceCts;

        public void StartPriceAutoRefresh(TimeSpan? interval = null)
        {
            // tekrar baþlatýlýrsa önce eskisini durdur
            StopPriceAutoRefresh();

            _autoPriceCts = new CancellationTokenSource();
            var token = _autoPriceCts.Token;
            var delay = interval ?? TimeSpan.FromSeconds(2); // her 3 sn’de bir

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // UpdateMarketPriceAsync UI’yý güncelliyor, ana threade geçelim
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await UpdateMarketPriceAsync();
                        });
                    }
                    catch { /* yut gitsin */ }

                    try
                    {
                        await Task.Delay(delay, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }

        public void StopPriceAutoRefresh()
        {
            try
            {
                _autoPriceCts?.Cancel();
                _autoPriceCts?.Dispose();
            }
            catch { /* yut gitsin */ }
            finally
            {
                _autoPriceCts = null;
            }
        }
        protected override void OnParentSet()
        {
            base.OnParentSet();
            if (Parent == null)
            {
                // görünüm kaldýrýldý
                StopPriceAutoRefresh();
            }
        }
        private void AutoRefreshSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            if (e.Value) StartPriceAutoRefresh(TimeSpan.FromSeconds(2));
            else StopPriceAutoRefresh();
        }
        // Price paint state
        private decimal? _prevPrice;
        private bool? _prevMarketClosed;
        // fiyat + market status durumuna göre UI renkleri uygula
        // fiyat + market status durumuna göre UI renkleri uygula (null-safe)
        private void ApplyPriceUI(decimal? price, bool? marketClosed)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var suffix = marketClosed == true ? " (market closed)" : string.Empty;

                if (MarketPriceLabel != null)
                    MarketPriceLabel.Text = price.HasValue
                        ? MarketPriceLabel.Text = price.Value.ToString(CultureInfo.InvariantCulture) + suffix
                        : "—" + suffix;

                // Eðer XAML'de rozet (Frame) yoksa sadece yazý rengini ayarla ve çýk
                var hasBadge = this.FindByName<Frame>("MarketPriceBadge") != null;
                if (!hasBadge)
                {
                    if (MarketPriceLabel != null)
                    {
                        if (marketClosed == true)
                        {
                            // kapalýyken gri ton (arka plan yoksa text'i soluk yapalým)
                            MarketPriceLabel.TextColor = Colors.Gray;
                        }
                        else
                        {
                            // açýkken: yükseliþ/düþüþe göre text rengi
                            if (_prevPrice.HasValue && price.HasValue)
                            {
                                if (price.Value > _prevPrice.Value) MarketPriceLabel.TextColor = Colors.Green;
                                else if (price.Value < _prevPrice.Value) MarketPriceLabel.TextColor = Colors.Red;
                                else MarketPriceLabel.TextColor = Colors.White;
                            }
                            else
                            {
                                MarketPriceLabel.TextColor = Colors.White;
                            }
                        }
                    }

                    _prevPrice = price;
                    _prevMarketClosed = marketClosed;
                    return;
                }

                // Rozet var ise hem arka planý hem text rengini ayarla
                var badge = this.FindByName<Frame>("MarketPriceBadge");
                if (badge != null)
                {
                    if (marketClosed == true)
                    {
                        // piyasa kapalý ? gri
                        badge.BackgroundColor = Colors.LightGray;
                        if (MarketPriceLabel != null) MarketPriceLabel.TextColor = Colors.Black;
                    }
                    else
                    {
                        // önceki fiyatla karþýlaþtýr
                        if (_prevPrice.HasValue && price.HasValue)
                        {
                            if (price.Value > _prevPrice.Value)
                            {
                                // yükseldi ? yeþil
                                badge.BackgroundColor = Colors.Green;
                                if (MarketPriceLabel != null) MarketPriceLabel.TextColor = Colors.White;
                            }
                            else if (price.Value < _prevPrice.Value)
                            {
                                // düþtü ? kýrmýzý
                                badge.BackgroundColor = Colors.Red;
                                if (MarketPriceLabel != null) MarketPriceLabel.TextColor = Colors.White;
                            }
                            else
                            {
                                // deðiþmedi ? nötr
                                badge.BackgroundColor = Colors.Transparent;
                                if (MarketPriceLabel != null) MarketPriceLabel.TextColor = Colors.DarkGray;
                            }
                        }
                        else
                        {
                            // ilk fiyat ? nötr
                            badge.BackgroundColor = Colors.Transparent;
                            if (MarketPriceLabel != null) MarketPriceLabel.TextColor = Colors.DarkGray;
                        }
                    }
                }

                _prevPrice = price;
                _prevMarketClosed = marketClosed;
            });
        }

    }
}
