using ArcTriggerUI.Const;
using ArcTriggerUI.Dashboard;
using ArcTriggerUI.Dtos;
using ArcTriggerUI.Dtos.Orders;
using ArcTriggerUI.Interfaces;
using ArcTriggerUI.Services;
using System.Globalization;
using System.Resources;
using System.Text.Json;
using static ArcTriggerUI.Dtos.Portfolio.ResultPortfolio;

// symbols text i�in
using System.Collections.ObjectModel;
using System.Threading;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic; // SECDEF: listeler i�in

namespace ArcTriggerUI.Dashboard
{
    public partial class OrderFrame : ContentView
    {
        // ==== ContentView i�inde Page metodlar� i�in �imler ====
        // �mzalar� ContentPage ile ayn� tuttum ki mevcut �a�r�lar de�i�meden �al��s�n.
        Task<string> DisplayActionSheet(string title, string cancel, string destruction, params string[] buttons)
            => Application.Current?.MainPage?.DisplayActionSheet(title, cancel, destruction, buttons) ?? Task.FromResult<string>(cancel);

        Task<string> DisplayPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel",
                                        string placeholder = null, int maxLength = -1, Keyboard keyboard = null, string initialValue = null)
            => Application.Current?.MainPage?.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, keyboard, initialValue)
               ?? Task.FromResult<string>(initialValue ?? "");

        Task DisplayAlert(string title, string message, string cancel)
            => Application.Current?.MainPage?.DisplayAlert(title, message, cancel) ?? Task.CompletedTask;

        // ContentView�te ToolbarItems yok; ayn� isimle proxy veriyorum.
        public IList<ToolbarItem> ToolbarItems =>
            (Application.Current?.MainPage as Page)?.ToolbarItems ?? new List<ToolbarItem>();

        // XAML�de varsa ba�lan�r; yoksa null-safe kulland�m.
        ToolbarItem btnDarkMode;

        #region Section Config || Buton B�l�mleri Ayarlar�
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

        private readonly IApiService? _apiService;

        // symbols text i�in: arama sonucu listesi ve debounce/iptal i�in CTS
        private readonly ObservableCollection<SymbolSearchResponse> _symbolResults = new();
        private CancellationTokenSource? _symbolCts;

        // symbols text i�in: sembol -> conid e�lemesi ve se�ilen conid
        private readonly Dictionary<string, long> _symbolConidMap = new(StringComparer.OrdinalIgnoreCase);
        private long? _selectedConid = null;

        // marketprice i�in: fiyat isteklerini y�netmek i�in CTS
        private CancellationTokenSource? _priceCts; // marketprice i�in

        // symbols text i�in: �neri ��esi (conid eklendi)
        public class SymbolSearchResponse
        {
            public string symbol { get; set; } = "";
            public string? name { get; set; }
            public long? conid { get; set; }      // << eklendi
            public string? companyHeader { get; set; } // companyheader i�in

            // companyheader i�in: companyHeader varsa �nce onu g�ster, yoksa name, yoksa symbol
            public string Display =>
                !string.IsNullOrWhiteSpace(companyHeader) ? $"{symbol} � {companyHeader}" :
                string.IsNullOrWhiteSpace(name) ? symbol : $"{symbol} � {name}";
        }

        // =========================
        // SECDEF: ak�� i�in state
        // =========================
        private List<string> _secdefSecTypes = new();
        private List<string> _secdefMonths = new();
        private List<string> _secdefExchanges = new(); // strikes �a�r�s�nda kullanaca��z

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

        public OrderFrame()
        {
            InitializeComponent();

            // IApiService ��z�mleme (opsiyonel)
            _apiService = TryResolveApi();

            InitHotSections();
            var saved = Preferences.Get("ui.theme", "Unspecified");
            if (Enum.TryParse<AppTheme>(saved, out var savedTheme))
            {
                Application.Current.UserAppTheme = savedTheme;
                var item = this.ToolbarItems?.FirstOrDefault();
                if (item != null)
                    item.Text = savedTheme == AppTheme.Dark ? "Light" : "Dark";
            }

            // SymbolSuggestions CollectionView�in ItemsSource�u
            if (SymbolSuggestions != null)
                SymbolSuggestions.ItemsSource = _symbolResults;
        }

        private static IApiService? TryResolveApi()
        {
            try
            {
                return Application.Current?.Handler?.MauiContext?.Services.GetService(typeof(IApiService)) as IApiService;
            }
            catch { return null; }
        }

        #region Order Add Section || Sipari� Ekleme B�l�m�
        private void OnSendClicked(object sender, EventArgs e)
        {
            if (int.TryParse(numberEntry?.Text, out int start))
            {
                if (numberEntry.Text == null)
                {
                    var newOrder = new OrderFrame();
                    OrdersContainer?.Children.Add(newOrder);
                }
                else
                {
                    for (int i = 0; i < start; i++)
                    {
                        var newOrder = new OrderFrame();
                        OrdersContainer?.Children.Add(newOrder);
                    }
                }
            }
        }
        #endregion

        #region Button Sections || �zelle�tirilebilir Buton B�l�mleri
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

        #region Buton Event Handlers || Buton Olay ��leyicileri
        void OnHotPresetClicked(object sender, EventArgs e)
        {
            if (sender is not Button b || b.CommandParameter is not string id) return;
            if (!sections.TryGetValue(id, out var s)) return;

            var valForEntry = ValueForEntry(b.Text, s.Mode);
            if (s.TargetEntry != null)
                s.TargetEntry.Text = valForEntry;
        }
        #endregion

        #region HotAdd Event Handler || HotAdd Olay ��leyicisi
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

        #region Buton Yard�mc� Metotlar� || Button Helper Methods
        private string _selectedOrderType = "Call";   // Default
        private string _selectedOrderMode = "MKT";    // Default

        // OrderType (Call/Put)
        private void OnOrderTypeCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) // sadece se�ilen RadioButton tetiklenir
            {
                var radio = sender as RadioButton;
                _selectedOrderType = radio?.Content?.ToString();
            }
        }

        // OrderMode (MKT/LMT)
        private void OnOrderModeCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value)
            {
                var radio = sender as RadioButton;
                _selectedOrderMode = radio?.Content?.ToString();
            }
        }
        void ApplySectionButtons(SectionConfig s)
        {
            for (int i = 0; i < s.Slots.Length && i < s.Selected.Length; i++)
                if (s.Slots[i] != null)
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

        #region Buton Format Yard�mc� Metotlar� || Button Helper Methods
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

        #region Buton De�er Yard�mc� Metotlar� || Button Value Helper Methods
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

        #region Buton De�er D�n��t�rme Metodu || Button Value Conversion Method
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

        #region Number Entry Text Changed || Say� Giri�i Metin De�i�ikli�i
        private void OnNumberEntryTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.NewTextValue))
                return;

            // Girilen karakteri kontrol et
            if (!int.TryParse(e.NewTextValue, out int value) || value < 1 || value > 9)
            {
                // Ge�ersizse eski de�eri geri y�kle
                ((Entry)sender).Text = e.OldTextValue;
            }
        }
        #endregion

        #region Order Add Button || Sipari� Ekleme Butonu
        private void OnAddOrderClicked(object sender, EventArgs e)
        {
            if (int.TryParse(numberEntry?.Text, out int start))
            {
                for (int i = 0; i < start; i++)
                {
                    var newOrder = new OrderFrame();
                    OrdersContainer?.Children.Add(newOrder);
                }
            }
            else
            {
                var newOrder = new OrderFrame();
                OrdersContainer?.Children.Add(newOrder);
            }
        }
        #endregion

        #region Se�ili Sembol || Selected Symbol
        private void OnSymbolChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedIndex != -1)
            {
                var symbol = picker.Items[picker.SelectedIndex];
                Console.WriteLine($"Selected symbol: {symbol}");

                // symbols text i�in: se�ilen sembol�n conid�sini s�zl�kten set et
                if (_symbolConidMap.TryGetValue(symbol, out var cid))
                {
                    _selectedConid = cid;
                }
                else
                {
                    _selectedConid = null; // << yoksa stale conid kalmas�n
                }

                // Window ba�l���na yans�t
                if (this.Window != null)
                    this.Window.Title = symbol;

                // Picker Title'�n� g�ncelle
                picker.Title = symbol;

                // DisplayLabel g�ncelle
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

                // marketprice i�in: sembol de�i�ince fiyat� g�ncelle
                _ = UpdateMarketPriceAsync(); // marketprice i�in

                // SECDEF: yeni sembolde secTypes'� y�kle
                _ = LoadSecTypesForCurrentAsync();
            }
        }
        #endregion

        #region Order Mode || Sipari� Modu
        private void OnCallOptionCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) Console.WriteLine("Order type: Call");
        }

        private void OnPutOptionCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) Console.WriteLine("Order type: Put");
        }
        #endregion

        #region Order Price Mode || Sipari� Fiyat Modu
        private void OnTriggerPriceTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Trigger price: {e.NewTextValue}");
        }
        #endregion

        #region Offset Text Changed || Offset Metin De�i�ikli�i
        private void OnIncreaseTriggerClicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("TriggerEntry") is Entry entry && decimal.TryParse(entry.Text, out decimal value))
            {
                entry.Text = (value + 1).ToString("F2");
            }
        }
        #endregion

        #region Decrease Trigger Clicked || Tetikleyici Azaltma T�kland�
        private void OnDecreaseTriggerClicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("TriggerEntry") is Entry entry && decimal.TryParse(entry.Text, out decimal value))
            {
                entry.Text = (value - 1).ToString("F2");
            }
        }
        #endregion

        #region Se�ili Strike || Selected Strike
        private void OnStrikeChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedIndex != -1)
            {
                var strike = picker.Items[picker.SelectedIndex];
                Console.WriteLine($"Strike: {strike}");

                if (this.Window != null)
                    this.Window.Title = strike;

                picker.Title = strike; // Picker Title g�ncelle
            }
        }
        #endregion

        #region Expiry Picker Changed || Vade Se�ici De�i�ti
        private void OnExpirationChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedIndex != -1)
            {
                var expiry = picker.Items[picker.SelectedIndex];
                Console.WriteLine($"Expiration: {expiry}");

                if (this.Window != null)
                    this.Window.Title = expiry;

                picker.Title = expiry; // Picker Title g�ncelle
            }
        }
        #endregion

        #region Position Size Text Changed || Pozisyon Boyutu Metin De�i�ikli�i
        private void OnPositionTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Position size: {e.NewTextValue}");
        }
        #endregion

        #region Stop Loss Text Changed || Stop Loss Metin De�i�ikli�i
        private void OnStopLossTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Stop loss: {e.NewTextValue}");
        }
        #endregion

        #region Stop Loss Preset Clicked || Stop Loss �n Ayar� T�kland�
        private void OnStopLossPreset(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("StopLossEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("$", "");
            }
        }
        #endregion

        #region Profit Text Changed || Kar Metin De�i�ikli�i
        private void OnProfitPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("ProfitEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("%", "");
                Console.WriteLine($"Profit taking set to {entry.Text}%");
            }
        }
        #endregion

        #region Trail Text Changed || Trail Metin De�i�ikli�i
        private void OnTrailClicked(object sender, EventArgs e)
        {
            Console.WriteLine("Invalidate action triggered");
        }
        #endregion

        #region Breakeven Clicked || Breakeven T�kland�
        private void OnBreakevenClicked(object sender, EventArgs e)
        {

        }
        #endregion

        #region Offset Preset Clicked || Offset �n Ayar� T�kland�
        private void OnOffsetPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("OffsetEntry") is Entry entry)
            {
                entry.Text = btn.Text;
                Console.WriteLine($"Offset set to {entry.Text}");
            }
        }
        #endregion

        #region Cancel Clicked || �ptal T�kland�
        void OnCancelClicked(object sender, EventArgs e)
        {
            var w = this.Window;
            if (w != null && Application.Current != null)
                Application.Current.CloseWindow(w);
        }
        #endregion

        #region Order Temizleme || Order Clearing
        private void OnClearOrdersClicked(object sender, EventArgs e)
        {
            OrdersContainer?.Children.Clear();
        }
        #endregion

        #region Dark Mode || Karanl�k Mod
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
                if (btnDarkMode != null)
                {
                    btnDarkMode.IconImageSource = "lightt.png";
                    btnDarkMode.Text = "Light";
                }

                imageDarkandLight = true;
            }
            else
            {
                if (app is null) return;

                app.UserAppTheme = app.UserAppTheme == AppTheme.Dark
                    ? AppTheme.Light
                    : AppTheme.Dark;

                if (btnDarkMode != null)
                {
                    btnDarkMode.IconImageSource = "theme_toggle.png";
                    btnDarkMode.Text = "Dark";
                }
            }
        }
        #endregion

        #region Api Request || Api �stek 
        private async void OnGetTickleClicked(object sender, EventArgs e)
        {
            if (_apiService is null) return;

            try
            {
                string url = Configs.BaseUrl + "/tickle";
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
            if (_apiService is null) return;

            try
            {
                string url = Configs.BaseUrl + "/status";
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
            if (_apiService is null) return;

            try
            {
                string url = Configs.BaseUrl + "/getSymbol";

                var request = new ResultSymbols
                {
                    symbol = "AAPL",
                    name = true,
                    secType = "STK"
                };

                // TResponse art�k ResultSymbols olmal�, string de�il
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

        private async void OnDeleteOrderClicked(object sender, EventArgs e)
        {
            if (_apiService is null) return;

            int symbolId = 265598; // veya kullan�c�dan alabilirsiniz

            bool confirm = await (Application.Current?.MainPage?.DisplayAlert(
                "Delete Confirmation",
                $"Are you sure you want to delete order '{symbolId}'?",
                "OK",
                "Cancel"
            ) ?? Task.FromResult(false));

            if (!confirm)
                return;

            try
            {
                string url = Configs.BaseUrl + "/orders/";
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
            if (_apiService is null) return;

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
            if (_apiService is null) return;

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
                    await DisplayAlert("SecDef", "Hi� kay�t yok.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnGetInfoClicked(object sender, EventArgs e)
        {
            if (_apiService is null) return;

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

        // �rnek buton click event
        private async void OnGetPortfolioClicked(object sender, EventArgs e)
        {
            if (_apiService is null) return;

            try
            {
                // ApiService instance'�n� kullan
                var portfolioList = await _apiService.GetPortfolioAsync();

                if (portfolioList != null && portfolioList.Count > 0)
                {
                    // �lk portfolio item'� g�sterelim
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
                    await DisplayAlert("Portfolio", "Hi� kay�t bulunamad�.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void LoadOrders(object sender, EventArgs e)
        {
            if (_apiService is null) return;

            try
            {
                string url = Configs.BaseUrl + "/orders";

                // Raw JSON olarak �ekiyoruz
                string json = await _apiService.GetAsync(url);

                // JSON'u dinamik olarak parse ediyoruz (tip hatas� olmayacak)
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
                string alertMessage = "Ba�ar�l�";
                await DisplayAlert("Order Info", alertMessage, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnPostOrderClicked(object sender, EventArgs e)
        {
            if (_apiService is null) return;

            try
            {
                var request = new PostOrderItem
                {
                    conid = 46639520,
                    orderType = "LMT",
                    price = 1700,
                    quantity = 1000, // art�k API kabul eder
                    side = "BUY",
                    tif = "DAY"
                };

                var result = await _apiService.CreateOrderAsync(request);

                if (result != null)
                {
                    await DisplayAlert("Success",
                        $"Order ID: {result.OrderId ?? "(null)"}\n" +
                        $"Status: {result.OrderStatus ?? "(null)"}\n" +
                        $"Encrypt: {result.EncryptMessage ?? "(null)"}",
                        "OK");
                }
                else
                {
                    await DisplayAlert("Info", "No order returned from API.", "OK");
                }
            }
            catch (Exception ex)
            {
                // API hatas� veya network hatas� buraya gelir
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }

        private async void OnGetOrdersClicked(object sender, EventArgs e)
        {
            if (_apiService is null) return;

            try
            {
                int orderId = 760072067; // �rnek order ID
                var order = await _apiService.GetAsync<OrderResponseDto>(Configs.BaseUrl + $"/orders/by-id/{orderId}");

                if (order == null)
                {
                    await DisplayAlert("Hata", "Servisten veri gelmedi veya hata olu�tu.", "Tamam");
                    return;
                }

                if (!order.Bulunan)
                {
                    await DisplayAlert("Bilgi", "Order bulunamad�.", "Tamam");
                    return;
                }

                // Order bulundu, DisplayAlert ile g�ster
                string message =
                    $"Order ID: {order.Order?.Id}\n" +
                    $"�r�n: {order.Order?.ProductName}\n" +
                    $"Adet: {order.Order?.Quantity}\n" +
                    $"Fiyat: {order.Order?.Price}\n" +
                    $"Durum: {order.Order?.Status}";

                await DisplayAlert("Order Detaylar�", message, "Tamam");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Hata", $"Order �ekilirken bir hata olu�tu: {ex.Message}", "Tamam");
            }
        }

        #endregion

        #region Sa� T�k D�zenleme || Right Click Edit
        private async void OnPresetRightClick(object sender, TappedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (!TryFindSectionAndIndex(btn, out var s, out var slotIndex)) return; // a�a��daki helper

            // mevcut de�eri parse et (virg�l/nokta toleransl�)
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

            // 2) Se�ili slotu g�ncelle + kaydet + butonlar� yenile
            s.Selected[slotIndex] = normalized;
            SaveSection(s);
            ApplySectionButtons(s); // -> "$7.5K" gibi do�ru metni basar

            // 3) �lgili Entry'ye ham de�eri yaz (7500 gibi)
            var displayText = DisplayForButton(normalized, s.Mode);
            if (s.TargetEntry != null)
                s.TargetEntry.Text = ValueForEntry(displayText, s.Mode);
        }
        #endregion

        #region Parse & Format Helpers || Ayr��t�rma ve Bi�imlendirme Yard�mc�lar�
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
            var order = new OrderRequest
            {
                Conid = "265598",
                Trigger = 229.15,
                OrderMode = "LMT",
                Offset = 0.05,
                PositionSize = 10,
                StopLoss = 225,
                Tif = "DAY",
                ProfitTaking = 240
            };

            var result = await _apiService.SendOrderAsync(order);
            await DisplayAlert("Order Result", result, "OK");
        }
        #endregion

        // =======================
        // symbols text i�in: Arama Entry�si & �neri CollectionView handler�lar�
        // =======================

        // XAML: TextChanged="OnSymbolSearchTextChanged"
        private async void OnSymbolSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var query = e.NewTextValue?.Trim() ?? string.Empty;

                // bo�sa listeyi gizle
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    _symbolCts?.Cancel();
                    _symbolResults.Clear();
                    if (SymbolSuggestions != null) SymbolSuggestions.IsVisible = false;
                    return;
                }

                // debounce + �nceki iste�i iptal
                _symbolCts?.Cancel();
                _symbolCts = new CancellationTokenSource();
                var token = _symbolCts.Token;

                // k���k bir gecikme (debounce)
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
                if (SymbolSuggestions != null) SymbolSuggestions.IsVisible = false;
            }
        }

        // XAML: SelectionChanged="OnSymbolSuggestionSelected"
        private void OnSymbolSuggestionSelected(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.CurrentSelection?.FirstOrDefault() is SymbolSearchResponse sel)
                {
                    // Entry�ye se�ilen sembol� yaz
                    if (SymbolSearchEntry != null)
                        SymbolSearchEntry.Text = sel.symbol;

                    // StockPicker�� se�ime g�re g�ncelle
                    if (StockPicker != null)
                    {
                        StockPicker.Items.Clear();
                        StockPicker.Items.Add(sel.symbol);
                        StockPicker.SelectedIndex = 0;
                    }

                    // conid�i do�rudan se�ilen objeden al; yoksa s�zl�kten dene
                    if (sel.conid.HasValue)
                        _selectedConid = sel.conid.Value;
                    else if (_symbolConidMap.TryGetValue(sel.symbol, out var cid))
                        _selectedConid = cid;
                    else
                        _selectedConid = null;

                    // �neri listesini gizle
                    if (SymbolSuggestions != null)
                    {
                        SymbolSuggestions.IsVisible = false;
                        SymbolSuggestions.SelectedItem = null;
                    }

                    // marketprice i�in: se�imden sonra fiyat� g�ncelle
                    _ = UpdateMarketPriceAsync(); // marketprice i�in

                    // SECDEF: yeni sembolde secTypes'� y�kle
                    _ = LoadSecTypesForCurrentAsync();
                }
            }
            catch { /* yoksay */ }
        }

        // symbols text i�in: API�den �neri �ekme + conid + companyHeader yakalama
        private async Task FetchAndBindSymbolSuggestionsAsync(string query, CancellationToken token)
        {
            if (_apiService is null) return;

            // API: /getSymbol -> request body: ResultSymbols { symbol, name(bool), secType }
            var url = Configs.BaseUrl + "/getSymbol";
            var request = new ResultSymbols
            {
                symbol = query,
                name = true,     // sunucudan isim bilgisini d�nd�r (DTO bool, response�da string olabilir)
                secType = "STK"
            };

            // Response �emas� garanti de�il (DTO�da name bool), bu y�zden JsonElement ile esnek parse
            List<JsonElement> rawList = await _apiService.PostAsync<ResultSymbols, List<JsonElement>>(url, request);

            // token iptal edildiyse d�n
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
                    // baz� servisler "name": true/false d�nd�rebilir, o zaman null b�rak
                }

                // companyheader i�in: companyHeader yakala
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
                        _symbolConidMap[symbol] = conid.Value; // mevcut davran�� dursun

                    list.Add(new SymbolSearchResponse
                    {
                        symbol = symbol,
                        name = displayName,
                        conid = conid,
                        companyHeader = header // companyheader i�in
                    });
                }
            }

            // UI�ya bas
            _symbolResults.Clear();
            foreach (var it in list.Take(200)) // g�venli s�n�r
                _symbolResults.Add(it);

            if (SymbolSuggestions != null)
                SymbolSuggestions.IsVisible = _symbolResults.Count > 0;
        }

        // marketprice i�in: se�ili sembol/conid�e g�re fiyat� �ek ve UI�a yaz
        private async Task UpdateMarketPriceAsync() // marketprice i�in
        {
            if (_apiService is null) { if (MarketPriceLabel != null) MarketPriceLabel.Text = "N/A"; return; }

            try
            {
                // �ncelik: conid varsa onu kullan, yoksa sembol string�i kullan
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
                    if (MarketPriceLabel != null) MarketPriceLabel.Text = "�";
                    return;
                }

                // /api/snapshot?symbol={conid or symbol}
                var url = Configs.BaseUrl.TrimEnd('/') + "/snapshot?symbol=" + Uri.EscapeDataString(symbolParam);

                // debounce/iptal
                _priceCts?.Cancel();
                _priceCts = new CancellationTokenSource();
                var token = _priceCts.Token;

                var response = await _apiService.GetAsync(url);
                if (token.IsCancellationRequested) return;

                // Backend bazen d�z string (�r: "123.45") d�nebilir, bazen JSON olabilir.
                // 1) D�z string say� ise direkt yaz
                var raw = (response ?? "").Trim();
                if (decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var direct))
                {
                    if (MarketPriceLabel != null) MarketPriceLabel.Text = direct.ToString("0.00", CultureInfo.InvariantCulture);
                    return;
                }

                // 2) JSON parse etmeyi dene ve yayg�n anahtarlar� ara
                if (raw.StartsWith("{") || raw.StartsWith("["))
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;

                    string[] priceKeys = { "marketPrice", "last", "lastPrice", "mark", "mid", "close", "price", "p" };

                    if (TryExtractDecimal(root, priceKeys, out var price) ||
                        (root.TryGetProperty("data", out var dataObj) && TryExtractDecimal(dataObj, priceKeys, out price)) ||
                        (root.TryGetProperty("quote", out var quoteObj) && TryExtractDecimal(quoteObj, priceKeys, out price)) ||
                        (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 && TryExtractDecimal(root[0], priceKeys, out price)))
                    {
                        if (MarketPriceLabel != null) MarketPriceLabel.Text = price.ToString("0.00", CultureInfo.InvariantCulture);
                        return;
                    }

                    // Fiyat yoksa bid/ask'tan mid hesaplamay� dene
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
                    if (!bid.HasValue && root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        bid ??= TryExtractOneOf(root[0], "bid", "bestBid", "b");
                        ask ??= TryExtractOneOf(root[0], "ask", "bestAsk", "a");
                    }

                    if (bid.HasValue && ask.HasValue)
                    {
                        var mid = (bid.Value + ask.Value) / 2m;
                        if (MarketPriceLabel != null) MarketPriceLabel.Text = mid.ToString("0.00", CultureInfo.InvariantCulture) + " (mid)";
                        return;
                    }
                }

                // Hi�biri olmad�ysa
                if (MarketPriceLabel != null) MarketPriceLabel.Text = "N/A (snapshot price yok)";
            }
            catch
            {
                if (MarketPriceLabel != null) MarketPriceLabel.Text = "N/A";
            }

            // ---- helpers (marketprice i�in) ----
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
        }

        // ===========================
        // SECDEF: symbol -> secTypes
        // ===========================
        private async Task LoadSecTypesForCurrentAsync()
        {
            if (_apiService is null) return;

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

                SecTypePicker?.Items.Clear();
                foreach (var st in _secdefSecTypes)
                    SecTypePicker?.Items.Add(st);

                if (_secdefSecTypes.Count > 0 && SecTypePicker != null)
                    SecTypePicker.SelectedIndex = 0;

                MonthsPicker?.Items.Clear();
                _secdefMonths.Clear();
                _secdefExchanges.Clear();
            }
            catch
            {
                SecTypePicker?.Items.Clear();
                MonthsPicker?.Items.Clear();
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
            if (_apiService is null) return;

            try
            {
                if (SecTypePicker == null || SecTypePicker.SelectedIndex < 0) return;
                if (!_selectedConid.HasValue) return;

                var secType = SecTypePicker.Items[SecTypePicker.SelectedIndex];
                SecTypePicker.Title = secType;

                var symbolText = StockPicker?.SelectedIndex >= 0
                    ? StockPicker.Items[StockPicker.SelectedIndex]
                    : (SymbolSearchEntry?.Text ?? string.Empty);

                if (string.IsNullOrWhiteSpace(symbolText)) return;

                var url = Configs.BaseUrl.TrimEnd('/') + "/secdef/sectype/info";
                var req = new { conid = _selectedConid.Value.ToString(CultureInfo.InvariantCulture), symbol = symbolText, secType = secType };

                var resp = await _apiService.PostAsync<object, SecTypeDetailResponse>(url, req);

                _secdefMonths = (resp?.months ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                _secdefExchanges = (resp?.exchanges ?? new List<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                MonthsPicker?.Items.Clear();
                foreach (var m in _secdefMonths)
                    MonthsPicker?.Items.Add(m);

                if (_secdefMonths.Count > 0 && MonthsPicker != null)
                    MonthsPicker.SelectedIndex = 0;
            }
            catch
            {
                MonthsPicker?.Items.Clear();
                _secdefMonths.Clear();
                _secdefExchanges.Clear();
            }
        }

        // ==========================
        // SECDEF: month -> strikes
        // ==========================
        // XAML -> MonthsPicker.SelectedIndexChanged="OnMonthChanged"
        private async void OnMonthChanged(object sender, EventArgs e)
        {
            if (_apiService is null) return;

            try
            {
                if (MonthsPicker == null || MonthsPicker.SelectedIndex < 0) return;
                if (SecTypePicker == null || SecTypePicker.SelectedIndex < 0) return;
                if (!_selectedConid.HasValue) return;

                var month = MonthsPicker.Items[MonthsPicker.SelectedIndex];
                MonthsPicker.Title = month;
                var secType = SecTypePicker.Items[SecTypePicker.SelectedIndex];
                var exchange = _secdefExchanges.FirstOrDefault() ?? string.Empty;
                var conid = _selectedConid.Value.ToString(CultureInfo.InvariantCulture);
                var url = Configs.BaseUrl.TrimEnd('/') + "/secdef/strikes?conid=" + conid + "&secType=" + secType + "&month=" + month + "&exchange=" + exchange;

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

                var callData = strikesData?.Call ?? new List<decimal>();
                StrikesPicker?.Items.Clear();
                foreach (var item in callData)
                {
                    StrikesPicker?.Items.Add(item.ToString());
                }

                //ApplyStrikesToUI(strikes);
            }
            catch (Exception)
            {

            }
        }
        private void StrikesPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (StrikesPicker != null && StrikesPicker.SelectedIndex >= 0)
                StrikesPicker.Title = StrikesPicker.SelectedItem.ToString();
        }

        public class StrikesResponses
        {
            public List<decimal> Call { get; set; } = new();
            public List<decimal> Put { get; set; } = new();
        }

        // SECDEF: StrikeEntry ve 2 preset butonu doldur
      
    }
}
