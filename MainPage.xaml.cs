using ArcTriggerUI.Dashboard;
using ArcTriggerUI.Dtos;
using ArcTriggerUI.Interfaces;
using ArcTriggerUI.Services;
using System.Globalization;
using System.Resources;
using System.Text.Json;
using static ArcTriggerUI.Dtos.Portfolio.ResultPortfolio;

namespace ArcTriggerUI
{

    public class Order
    {
        public string Symbol { get; set; }
        public decimal TriggerPrice { get; set; }
        public string OrderType { get; set; } // Call / Put
        public string OrderMode { get; set; } // MKT / LMT
        public decimal Offset { get; set; }
        public string Strike { get; set; }
        public string Expiry { get; set; }
        public decimal PositionSize { get; set; }
        public decimal StopLoss { get; set; }
        public decimal ProfitTaking { get; set; }
        public bool AlphaFlag { get; set; }

        public override string ToString()
        {
            return $"Symbol: {Symbol}, Trigger: {TriggerPrice}, Type: {OrderType}, Mode: {OrderMode}, Offset: {Offset}, Strike: {Strike}, Expiry: {Expiry}, Position: {PositionSize}, StopLoss: {StopLoss}, Profit: {ProfitTaking}, Alpha: {AlphaFlag}";
        }
    }

    public partial class MainPage : ContentPage

    {
        #region Section Config || Buton Bölümleri Ayarları
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

        public MainPage(IApiService apiService)
        {
            InitializeComponent();
            InitHotSections();
            var saved = Preferences.Get("ui.theme", "Unspecified");
            if (Enum.TryParse<AppTheme>(saved, out var savedTheme))
            {
                Application.Current.UserAppTheme = savedTheme;
                var item = this.ToolbarItems?.FirstOrDefault();
                if (item != null)
                    item.Text = savedTheme == AppTheme.Dark ? "Light" : "Dark";
            }
            _apiService = apiService;
        }



        #region Order Add Section || Sipariş Ekleme Bölümü
        private void OnSendClicked(object sender, EventArgs e)
        {
            if (int.TryParse(numberEntry.Text, out int start))
            {
                if (numberEntry.Text == null)
                {
                    var newOrder = new OrderFrame();
                    OrdersContainer.Children.Add(newOrder);
                }
                else
                {
                    for (int i = 0; i < start; i++)
                    {
                        var newOrder = new OrderFrame();
                        OrdersContainer.Children.Add(newOrder);
                    }
                }
            }

        }
        #endregion

        #region Button Sections || Özelleştirilebilir Buton Bölümleri
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

        #region Buton Event Handlers || Buton Olay İşleyicileri
        void OnHotPresetClicked(object sender, EventArgs e)
        {
            if (sender is not Button b || b.CommandParameter is not string id) return;
            if (!sections.TryGetValue(id, out var s)) return;

            var valForEntry = ValueForEntry(b.Text, s.Mode);
            s.TargetEntry.Text = valForEntry;
        }
        #endregion

        #region HotAdd Event Handler || HotAdd Olay İşleyicisi
        async void OnHotAddClicked(object sender, EventArgs e)
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

            var choice = await DisplayActionSheet(title, "Cancel", null, options.ToArray());
            if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

            if (choice == "Custom...")
            {
                var input = await DisplayPromptAsync(title, prompt, "OK", "Cancel");
                if (string.IsNullOrWhiteSpace(input)) return;
                choice = input.Trim();
            }

            s.Selected[0] = NormalizeForMode(choice, s.Mode); // ilk slotu degistir
            SaveSection(s);
            ApplySectionButtons(s);
        }

        #endregion

        #region Buton Yardımcı Metotları || Button Helper Methods
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

        #region Buton Format Yardımcı Metotları || Button Format Helper Methods
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

        #region Buton Değer Yardımcı Metotları || Button Value Helper Methods
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

        #region Buton Değer Dönüştürme Metodu || Button Value Conversion Method
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


        #region Number Entry Text Changed || Sayı Girişi Metin Değişikliği

        private void OnNumberEntryTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue))
                return;

            // Girilen karakteri kontrol et
            if (!int.TryParse(e.NewTextValue, out int value) || value < 1 || value > 9)
            {
                // Geçersizse eski değeri geri yükle
                ((Entry)sender).Text = e.OldTextValue;
            }
        }
        #endregion

        #region Order Add Button || Sipariş Ekleme Butonu
        private void OnAddOrderClicked(object sender, EventArgs e)
        {
            if (int.TryParse(numberEntry.Text, out int start))
            {

                for (int i = 0; i < start; i++)
                {
                    var newOrder = new OrderFrame();
                    OrdersContainer.Children.Add(newOrder);
                }
            }
            else
            {
                var newOrder = new OrderFrame();
                OrdersContainer.Children.Add(newOrder);
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

                // Window başlığına yansıt
                if (this.Window != null)
                    this.Window.Title = symbol;

                // Picker Title'ını güncelle
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
            }
        }
        #endregion

        #region Order Mode || Sipariş Modu
        private void OnCallOptionCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) Console.WriteLine("Order type: Call");
        }

        private void OnPutOptionCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) Console.WriteLine("Order type: Put");
        }
        #endregion

        #region Order Price Mode || Sipariş Fiyat Modu
        private void OnTriggerPriceTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Trigger price: {e.NewTextValue}");
        }
        #endregion

        #region Offset Text Changed || Offset Metin Değişikliği
        private void OnIncreaseTriggerClicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("TriggerEntry") is Entry entry && decimal.TryParse(entry.Text, out decimal value))
            {
                entry.Text = (value + 1).ToString("F2");
            }
        }
        #endregion

        #region Decrease Trigger Clicked || Tetikleyici Azaltma Tıklandı
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

        #region Expiry Picker Changed || Vade Seçici Değişti
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

        #region Position Size Text Changed || Pozisyon Boyutu Metin Değişikliği
        private void OnPositionTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Position size: {e.NewTextValue}");
        }
        #endregion


        #region Stop Loss Text Changed || Stop Loss Metin Değişikliği
        private void OnStopLossTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Stop loss: {e.NewTextValue}");
        }
        #endregion

        #region Stop Loss Preset Clicked || Stop Loss Ön Ayarı Tıklandı
        private void OnStopLossPreset(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("StopLossEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("$", "");
            }
        }
        #endregion

        #region Create Order Clicked || Sipariş Oluşturma Tıklandı
        private void OnCreateOrderClicked(object sender, EventArgs e)
        {
            var symbol = (StockPicker.SelectedIndex != -1) ? StockPicker.Items[StockPicker.SelectedIndex] : "";
            var triggerPrice = decimal.TryParse(TriggerEntry.Text, out var t) ? t : 0;
            var orderType = CallRadioButton.IsChecked ? "Call" : "Put";
            var orderMode = MktRadioButton.IsChecked ? "MKT" : "LMT";
            var offset = decimal.TryParse(OffsetEntry.Text, out var o) ? o : 0;
            var strike = (StrikePicker.SelectedIndex != -1) ? StrikePicker.Items[StrikePicker.SelectedIndex] : "";
            var expiry = (ExpPicker.SelectedIndex != -1) ? ExpPicker.Items[ExpPicker.SelectedIndex] : "";
            var position = decimal.TryParse(PositionEntry.Text, out var p) ? p : 0;
            var stopLoss = decimal.TryParse(StopLossEntry.Text, out var s) ? s : 0;
            var profit = decimal.TryParse(ProfitEntry.Text, out var pr) ? pr : 0;
            var alpha = AlphaCheck.IsChecked;

            var order = new Order
            {
                Symbol = symbol,
                TriggerPrice = triggerPrice,
                OrderType = orderType,
                OrderMode = orderMode,
                Offset = offset,
                Strike = strike,
                Expiry = expiry,
                PositionSize = position,
                StopLoss = stopLoss,
                ProfitTaking = profit,
                AlphaFlag = alpha
            };

            Console.WriteLine("Order created: " + order.ToString());

            // Ekrana yazdır
            if (this.FindByName<Label>("DisplayLabel") is Label label)
            {
                label.Text = order.ToString();
            }
        }
        #endregion

        #region Profit Text Changed || Kar Metin Değişikliği
        private void OnProfitPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("ProfitEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("%", "");
                Console.WriteLine($"Profit taking set to {entry.Text}%");
            }
        }
        #endregion

        #region Trail Text Changed || Trail Metin Değişikliği
        private void OnTrailClicked(object sender, EventArgs e)
        {
            Console.WriteLine("Invalidate action triggered");
        }
        #endregion

        #region Breakeven Clicked || Breakeven Tıklandı
        private void OnBreakevenClicked(object sender, EventArgs e)
        {

        }
        #endregion

        #region Offset Preset Clicked || Offset Ön Ayarı Tıklandı
        private void OnOffsetPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("OffsetEntry") is Entry entry)
            {
                entry.Text = btn.Text;
                Console.WriteLine($"Offset set to {entry.Text}");
            }
        }
        #endregion

        #region Cancel Clicked || İptal Tıklandı
        void OnCancelClicked(object sender, EventArgs e)
        {
            var w = this.Window;
            if (w != null && Application.Current != null)
                Application.Current.CloseWindow(w);
        }
        #endregion

        private void OnClearOrdersClicked(object sender, EventArgs e)
        {
            OrdersContainer.Children.Clear();
        }

        private bool imageDarkandLight = false;
        private void OnToggleThemeClicked(object sender, EventArgs e)
        {
            var app = Application.Current;

            if (imageDarkandLight == false)
            {
                if (app is null) return;

                app.UserAppTheme = app.UserAppTheme == AppTheme.Dark
                    ? AppTheme.Light
                    : AppTheme.Dark;
                btnDarkMode.IconImageSource = "lightt.png";
                btnDarkMode.Text = "Light";

                imageDarkandLight = true;
            }
            else
            {
                if (app is null) return;

                app.UserAppTheme = app.UserAppTheme == AppTheme.Dark
                    ? AppTheme.Light
                    : AppTheme.Dark;
                btnDarkMode.IconImageSource = "theme_toggle.png";
                btnDarkMode.Text = "Dark";
                imageDarkandLight = false;
            }

        }

        #region Api Request || Api İstek 4
        private async void OnGetTickleClicked(object sender, EventArgs e)
        {

            try
            {
                string url = "http://192.168.1.112:8000/api/tickle";
                string result = await _apiService.GetAsync(url);
                await DisplayAlert("Auto Call", $"API Response: {result}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }

        }

        private async void OnGetStatusClicked(object sender, EventArgs e)
        {
            try
            {
                string url = "http://192.168.1.112:8000/api/status";
                string result = await _apiService.GetAsync(url);
                await DisplayAlert("Auto Call", $"API Response: {result}", "OK");

            }
            catch (Exception ex)
            {

                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnPostSymbolClicked(object sender, EventArgs e)
        {
            try
            {
                string url = "http://192.168.1.112:8000/api/getSymbol";

                var request = new ResultSymbols
                {
                    symbol = "AAPL",
                    name = true,
                    secType = "STK"
                };

                // TResponse artık ResultSymbols olmalı, string değil
                List<ResultSymbols> result = await _apiService.PostAsync<ResultSymbols, List<ResultSymbols>>(url, request);

                var first = result.FirstOrDefault();
                if (first != null)
                {
                    await DisplayAlert("Success",
                        $"Symbol: {first.symbol}, Name: {first.name}, SecType: {first.secType}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        //protected override async void OnAppearing()
        //{
        //    base.OnAppearing();
        //    try
        //    {
        //        string url = "http://192.168.1.112:8000/api/getSymbol";

        //        var request = new ResultSymbols
        //        {
        //            symbol = "AAPL",
        //            name = true,
        //            //secType = "STK"
        //        };

        //        // TResponse artık ResultSymbols olmalı, string değil
        //        List<ResultSymbols> result = await _apiService.PostAsync<ResultSymbols, List<ResultSymbols>>(url, request);

        //        var first = result.FirstOrDefault();
        //        if (first != null)
        //        {
        //            await DisplayAlert("Success",
        //                $"Symbol: {first.symbol}, Name: {first.name}, SecType: {first.secType}", "OK");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        await DisplayAlert("Error", ex.Message, "OK");
        //    }
        //}

        private async void OnDeleteSymbolClicked(object sender, EventArgs e)
        {
            int symbolId = 265598; // veya kullanıcıdan alabilirsiniz

            bool confirm = await DisplayAlert(
                "Delete Confirmation",
                $"Are you sure you want to delete order '{symbolId}'?",
                "OK",
                "Cancel"
            );

            if (!confirm)
                return;

            try
            {
                string url = "http://192.168.1.112:8000/api/orders/";
                await _apiService.DeleteAsync(url, symbolId);

                await DisplayAlert("Success", $"Symbol '{symbolId}' deleted successfully!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnSecdefStrikeClicked(object sender, EventArgs e)
        {
            try
            {
                string result = await _apiService.GetSecDefStrikeAsync("46639520", "ASDA", "STK");
                await DisplayAlert("Success", $"API Response: {result}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
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
                    await DisplayAlert("SecDef Item", msg, "OK");
                }
                else
                {
                    await DisplayAlert("SecDef", "Hiç kayıt yok.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnGetInfoClicked(object sender, EventArgs e)
        {
            try
            {
                var info = await _apiService.GetInfoAsync("46639520");
                await DisplayAlert("Info",
                    $"Conid: {info.conid}\nTicker: {info.ticker}\nCompany: {info.companyName}\nCurrency: {info.currency}",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }


        // Örnek buton click event
        private async void OnGetPortfolioClicked(object sender, EventArgs e)
        {
            try
            {
                // ApiService instance'ını kullan
                var portfolioList = await _apiService.GetPortfolioAsync();

                if (portfolioList != null && portfolioList.Count > 0)
                {
                    // İlk portfolio item'ı gösterelim
                    var firstItem = portfolioList[0];

                    string message =
                        $"ID: {firstItem.id}\n" +
                        $"DisplayName: {firstItem.displayName}\n" +
                        $"AccountId: {firstItem.accountId}\n" +
                        $"Currency: {firstItem.currency}\n" +
                        $"Type: {firstItem.type}";

                    await DisplayAlert("Portfolio Item", message, "OK");
                }
                else
                {
                    await DisplayAlert("Portfolio", "Hiç kayıt bulunamadı.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        #endregion

        private async void OnPresetRightClick(object sender, TappedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (!TryFindSectionAndIndex(btn, out var s, out var slotIndex)) return; // aşağıdaki helper

            // mevcut değeri parse et (virgül/nokta toleranslı)
            string old = btn.Text?.Trim() ?? "";
            old = old.Replace("$", "").Replace("%", "").Trim().Replace(',', '.');

            string input = await DisplayPromptAsync(
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
            var normalized = NormalizeForMode(input, s.Mode);

            // 2) Seçili slotu güncelle + kaydet + butonları yenile
            s.Selected[slotIndex] = normalized;
            SaveSection(s);
            ApplySectionButtons(s); // -> "$7.5K" gibi doğru metni basar

            // 3) İlgili Entry'ye ham değeri yaz (7500 gibi)
            var displayText = DisplayForButton(normalized, s.Mode);
            s.TargetEntry.Text = ValueForEntry(displayText, s.Mode);
        }

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



    }
}
