
using ArcTriggerUI.Const;
using ArcTriggerUI.Dashboard;
using ArcTriggerUI.Dtos;
using ArcTriggerUI.Dtos.Orders;
using ArcTriggerUI.Interfaces;
using ArcTriggerUI.Services;
using ArcTriggerUI.Tws.Models;
using ArcTriggerUI.Tws.Services;
using ArcTriggerUI.Tws.Utils;
using IBApi;
using Microsoft.Maui.ApplicationModel;         // MainThread
using Microsoft.Maui.Controls;                 // MAUI Controls
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Storage;                  // Preferences
using System;
using System.Collections.Generic; // SECDEF: listeler i�in
// symbols text i�in
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Security.AccessControl;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static ArcTriggerUI.Dtos.Portfolio.ResultPortfolio;
using Layout = Microsoft.Maui.Controls.Layout;
using Order = IBApi.Order;

namespace ArcTriggerUI.Dashboard
{
    public partial class OrderFrame : ContentView
    {
        double xOffset = 0;
        double yOffset = 0;

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

        private int? _selectedConId;
        private string? _selectedSymbol;
        private string? _selectedSectype;
        private double positionSize;
        private double trigger;
        private string? _currentSecType;
        private string? _currentMonth;
        private string? _currentExchange;
        private string? _lastConId;
        private string _currentRight = ""; // Call=C, Put=P
        private string _orderMode = ""; // DEFAULT MKT
        private StrikesResponses _lastStrikes; // month de�i�ti�inde gelen set'i tut

        // symbols text i�in: arama sonucu listesi ve debounce/iptal i�in CTS

        private CancellationTokenSource? _symbolCts;

        // symbols text i�in: sembol -> conid e�lemesi ve se�ilen conid
        private readonly Dictionary<string, long> _symbolConidMap = new(StringComparer.OrdinalIgnoreCase);


        // marketprice için: fiyat isteklerini yönetmek için CTS
        private CancellationTokenSource? _priceCts; // marketprice için

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
        private class SecdefInfoResponse
        {
            public string? conid { get; set; }
            public string? desc2 { get; set; }
            public string? maturityDate { get; set; }
            public bool? showPrips { get; set; }
        }
        private readonly TwsService _twsService;
        public class SymbolDisplay
        {
            public string Display { get; set; }
        }
        private ObservableCollection<SymbolDisplay> _symbolResults = new();
        private List<SymbolMatch> _allSymbolMatches = new();
        private List<string> SecTypeStk = new();
        public OrderFrame(TwsService twsService)
        {

            InitializeComponent();
            InitHotSections();

            _twsService = twsService;
            twsService.ConnectAsync("127.0.0.1", 7497, 0);

            SymbolSuggestions.ItemsSource = _symbolResults;

            // Se�im yap�ld���nda Entry'ye yaz
            SymbolSuggestions.SelectionChanged += (s, e) =>
            {
                if (e.CurrentSelection.FirstOrDefault() is SymbolDisplay selected)
                {
                    SymbolSearchEntry.Text = selected.Display;
                    SymbolSuggestions.IsVisible = false; // se�im sonras� listeyi kapat

                  
                }
            };


            var panGesture = new PanGestureRecognizer();
            panGesture.PanUpdated += (s, e) =>
            {
                if (e.StatusType == GestureStatus.Running)
                {
                    // ScrollView�u s�r�kleme
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

            // >>> fiyat� periyodik �ek

        }


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

        #region Buton Yard�mc� Metotlar� || Button Helper Methods
        private string _selectedOrderType = "Call";   // Default
        private string _selectedOrderMode = "MKT";    // Default

        // OrderType (Call/Put)


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

        #region Se�ili Sembol || Selected Symbol
        private void OnSymbolChanged(object sender, EventArgs e)
        {
            //if (sender is Picker picker && picker.SelectedIndex != -1)
            //{
            //    var symbol = picker.Items[picker.SelectedIndex];
            //    Console.WriteLine($"Selected symbol: {symbol}");

            //    // symbols text i�in: se�ilen sembol�n conid�sini s�zl�kten set et
            //    if (_symbolConidMap.TryGetValue(symbol, out var cid))
            //    {
            //        _selectedConid = cid;
            //    }
            //    else
            //    {
            //        _selectedConid = null; // << yoksa stale conid kalmas�n
            //    }

            //    // Window ba�l���na yans�t
            //    if (this.Window != null)
            //        this.Window.Title = symbol;

            //    // Picker Title'�n� g�ncelle
            //    picker.Title = symbol;

            //    // DisplayLabel g�ncelle
            //    if (this.FindByName<Label>("DisplayLabel") is Label label)
            //    {
            //        var currentText = label.Text ?? "";
            //        var lines = currentText.Split(',', StringSplitOptions.None)
            //                               .Select(l => l.Trim())
            //                               .ToList();

            //        bool stockUpdated = false;
            //        for (int i = 0; i < lines.Count; i++)
            //        {
            //            if (lines[i].StartsWith("Symbol:"))
            //            {
            //                lines[i] = $"Symbol: {symbol}";
            //                stockUpdated = true;
            //                break;
            //            }
            //        }
            //        if (!stockUpdated)
            //        {
            //            lines.Insert(0, $"Symbol: {symbol}");
            //        }

            //        label.Text = string.Join(", ", lines);
            //    }

            //    // marketprice i�in: sembol de�i�ince fiyat� g�ncelle
            //    _ = UpdateMarketPriceAsync(); // marketprice i�in

            //    // SECDEF: yeni sembolde secTypes'� y�kle
            //    _ = LoadSecTypesForCurrentAsync();
            //}
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
            QuantityCalculated();
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

        #region Api Request || Api �stek 


        private async Task SymbolAPI(string value)
        {
            _symbolResults.Clear();
            _allSymbolMatches.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                SymbolSuggestions.IsVisible = false;
                return;
            }

            try
            {
                var results = await _twsService.SearchSymbolsAsync(value);

                foreach (var r in results)
                {
                    _symbolResults.Add(new SymbolDisplay
                    {
                        Display = r.Symbol + " " + r.Description
                    });
                    _allSymbolMatches.Add(r);
                    SecTypeStk.Add(r.SecType);
                }

                // Listeyi g�ster / gizle
                SymbolSuggestions.IsVisible = _symbolResults.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Hata: " + ex.Message);
                SymbolSuggestions.IsVisible = false;
            }
        }

        private List<string> _selectedDerivativeSecTypes = new();
        private int? _currentTickerId = null;

        private void SymbolSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is SymbolDisplay selectedDisplay)
            {
                SymbolSearchEntry.Text = selectedDisplay.Display;
                SymbolSuggestions.IsVisible = false;

                var symbolText = selectedDisplay.Display.Split(' ')[0];
                var match = _allSymbolMatches.FirstOrDefault(s => s.Symbol == symbolText);
                if (match != null)
                {
                    _selectedConId = match.ConId;
                    _selectedSymbol = match.Symbol;
                    _selectedSectype = match.SecType;
                    _selectedDerivativeSecTypes = match.DerivativeSecTypes.ToList();
                    if (!_selectedDerivativeSecTypes.Contains(_selectedSectype))
                        _selectedDerivativeSecTypes.Insert(0, _selectedSectype);

                    MonthsPicker.ItemsSource = _selectedDerivativeSecTypes;

                    // --- Market Data iste ---
                    RequestSymbolMarketData(_selectedConId.Value, _selectedSectype, "SMART");
                }
            }
        }


        // Market data geldiğinde UI'ı güncelle



        private async void LoadOptionParams_Clicked(object sender, EventArgs e)
        {
            if (_selectedConId == null || string.IsNullOrEmpty(_selectedSymbol))
            {
                await ShowErrorAsync("Hata", "Lütfen önce bir symbol seçin.");
                return;
            }

            if (SecTypePicker.SelectedItem == null)
                return;

            // Kullanıcının seçtiği secType’ı al
            _selectedSectype = SecTypePicker.SelectedItem.ToString();

            try
            {
                // Dayanak tipler için expiration ve strike al
                if (_selectedSectype == "STK" || _selectedSectype == "FUT" || _selectedSectype == "IND")
                {
                    var optionParams = await _twsService.GetOptionParamsAsync(
                        _selectedConId.Value,
                        _selectedSymbol,
                        _selectedSectype
                    );

                    // Expiration listesi
                    var expirations = optionParams
                        .SelectMany(p => p.Expirations)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();

                    MaturityDateLabel.ItemsSource = expirations;
                    if (expirations.Count > 0)
                        MaturityDateLabel.SelectedIndex = 0;

                    // Strike listesi
                    var strikes = optionParams
                        .SelectMany(p => p.Strikes)
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList();

                    StrikesPicker.ItemsSource = strikes;
                    if (strikes.Count > 0)
                        StrikesPicker.SelectedIndex = 0;
                }
                else
                {
                    // OPT/WAR/IOPT gibi derivative tipler için expiration/strike yok
                    MaturityDateLabel.ItemsSource = null;
                    StrikesPicker.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Hata", ex.Message);
            }
        }

        // ContentView içinde yardımcı metod
        private async Task ShowErrorAsync(string title, string message)
        {
            if (this.Parent is Page parentPage)
            {
                await parentPage.DisplayAlert(title, message, "OK");
            }
        }

        private int _tickerId = -1;
        private CancellationTokenSource? _cts;
        private async Task UpdatePricesLoop(int tickerId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var data = _twsService.GetLatestData(tickerId);
                if (data != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var lasttPrice = $"Last Price: {data.Last}";
                        var bidPrice = $"Bid: {data.Bid}";
                        var askPrice = $"Ask: {data.Ask}";
                    });
                }

                await Task.Delay(500, ct); // yarım saniye aralıklarla güncelle
            }
        }
        private async void StartMarketPrice(object sender, EventArgs e)
        {
            try
            {


                if (_cts == null || _cts.IsCancellationRequested)
                    _cts = new CancellationTokenSource();

                int tickerId = _twsService.RequestMarketDataBySymbol(_selectedSymbol);
                await UpdatePricesLoop(tickerId, _cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Hata", ex.Message, "OK");
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

            // 2) Se�ili slotu g�ncelle + kaydet + butonlar� yenile
            s.Selected[slotIndex] = normalized;
            SaveSection(s);
            ApplySectionButtons(s); // -> "$7.5K" gibi do�ru metni basar

            // 3) �lgili Entry'ye ham de�eri yaz (7500 gibi)
            var displayText = DisplayForButton(normalized, s.Mode);
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




        #endregion



        // XAML: TextChanged="OnSymbolSearchTextChanged"
        private async void OnSymbolSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            SymbolAPI(SymbolSearchEntry.Text);

        }

        private string PickBestExchange(List<string> exchanges)
        {
            if (exchanges == null || exchanges.Count == 0) return string.Empty;
            var prios = new[] { "SMART", "GLOBEX", "CBOE", "ISE", "ARCA", "BOX", "NYSE" };
            var best = exchanges.FirstOrDefault(x => prios.Contains(x?.ToUpperInvariant()));
            return string.IsNullOrWhiteSpace(best) ? (exchanges.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty) : best;
        }


        private void MaturityDateLabel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MaturityDateLabel.SelectedIndex >= 0)
                MaturityDateLabel.Title = MaturityDateLabel.Items[MaturityDateLabel.SelectedIndex];
        }


        public class StrikesResponses
        {
            public List<decimal> Call { get; set; } = new();
            public List<decimal> Put { get; set; } = new();
        }


        private sealed record OptionOrderPreview(
    int UnderlyingConId,
    string Symbol,
    string SecType,
    string Right,
    string ExpiryYyyymmdd,
    double Strike,
    string Exchange,
    int Quantity,
    string OrderMode,     // "MKT" / "LMT"
    double? LimitPrice,
    int OptionConId
);
        private async void SendOrderClicked(object sender, EventArgs e)
        {
            try
            {
                // --- 1) UI doğrulamaları
                if (string.IsNullOrWhiteSpace(_selectedSymbol))
                {
                    await ShowAlert("Uyarı", "Lütfen bir sembol seçin.");
                    return;
                }

                if (_selectedConId is null || string.IsNullOrWhiteSpace(_selectedSectype))
                {
                    await ShowAlert("Uyarı", "Önce sembolü seçip SecType/Option Params yükleyin.");
                    return;
                }

                if (MaturityDateLabel?.SelectedItem is not string expiry || string.IsNullOrWhiteSpace(expiry))
                {
                    await ShowAlert("Uyarı", "Lütfen bir vade (expiration) seçin.");
                    return;
                }

                if (!double.TryParse(StrikesPicker?.SelectedItem?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var strike))
                {
                    await ShowAlert("Uyarı", "Lütfen bir strike seçin.");
                    return;
                }

                if (!int.TryParse(lblQuantity?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                {
                    await ShowAlert("Uyarı", "Miktar (adet) geçersiz.");
                    return;
                }

                var orderMode = (LmtRadioButton?.IsChecked ?? false) ? "LMT" : "MKT";
                double? limitPrice = null;
                if (orderMode == "LMT")
                {
                    if (!TryParseDouble(TriggerEntry?.Text, out var lim) || lim <= 0)
                    {
                        await ShowAlert("Uyarı", "Limit emir için fiyat (Trigger) gerekli.");
                        return;
                    }
                    limitPrice = lim;
                }

                var right = (PutRadioButton?.IsChecked ?? false) ? "P" : "C";

                // --- 2) Contract oluştur
                var contract = new Contract
                {
                    ConId = _selectedConId.Value,
                    Symbol = _selectedSymbol!,
                    SecType = _selectedSectype!,
                    Exchange = "SMART",
                    Currency = "USD",
                    Right = right,
                    LastTradeDateOrContractMonth = expiry,
                    Strike = strike
                };

                // --- 3) Order oluştur
                var order = new Order
                {
                    Action = "BUY",
                    OrderType = orderMode,
                    TotalQuantity = qty
                };

                if (orderMode == "LMT")
                    order.LmtPrice = limitPrice.Value;

                // --- 4) IB'e bağlan ve order gönder


                var orderId = await _twsService.PlaceOrderAsync(contract, order);

                await ShowAlert("Başarılı", $"Order gönderildi! OrderId: {orderId}");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await ShowAlert("Hata", ex.Message);
            }
        }
        private async void OnPostOrderClicked(object sender, EventArgs e)
        {
            try
            {

                // --- UI'dan verileri oku
                var rightCode = (PutRadioButton?.IsChecked ?? false) ? "P" : "C";
                var orderMode = (LmtRadioButton?.IsChecked ?? false) ? "LMT" : "MKT";

                if (string.IsNullOrWhiteSpace(_selectedSymbol))
                {
                    await ShowAlert("Uyarı", "Lütfen bir sembol seçin.");
                    return;
                }

                if (_selectedConId is null || string.IsNullOrWhiteSpace(_selectedSectype))
                {
                    await ShowAlert("Uyarı", "Önce sembolü seçip SecType/Option Params yükleyin.");
                    return;
                }

                if (MaturityDateLabel?.SelectedItem is not string expiry || string.IsNullOrWhiteSpace(expiry))
                {
                    await ShowAlert("Uyarı", "Lütfen bir vade seçin.");
                    return;
                }

                if (!double.TryParse(StrikesPicker?.SelectedItem?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var strike))
                {
                    await ShowAlert("Uyarı", "Lütfen bir strike seçin.");
                    return;
                }

                if (!int.TryParse(lblQuantity?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                {
                    await ShowAlert("Uyarı", "Miktar geçersiz.");
                    return;
                }

                double? limitPrice = null;
                if (orderMode == "LMT")
                {
                    if (!TryParseDouble(TriggerEntry?.Text, out var lim) || lim <= 0)
                    {
                        await ShowAlert("Uyarı", "Limit emir için fiyat gerekli.");
                        return;
                    }
                    limitPrice = lim;
                }

                // --- OptionConId çöz
                var optionConId = await _twsService.ResolveOptionConidAsync(
                    symbol: _selectedSymbol,
                    secType: "OPT",
                    exchange: "SMART",
                    right: rightCode,
                    yyyymmdd: expiry,
                    strike: strike
                );

                // --- Contract ve Order oluştur
                var contract = new Contract
                {
                    ConId = optionConId,
                    Symbol = _selectedSymbol,
                    SecType = _selectedSectype,
                    Exchange = "SMART",
                    Currency = "USD",
                    LastTradeDateOrContractMonth = expiry,
                    Strike = strike,
                    Right = rightCode
                };

                var order = new Order
                {
                    Action = "BUY", // veya "SELL"
                    OrderType = orderMode,
                    TotalQuantity = qty,
                    LmtPrice = limitPrice ?? 0
                };

                // --- Order gönder
                var orderId = await _twsService.PlaceOrderAsync(contract, order);

                await ShowAlert("Başarılı", $"Order gönderildi. OrderId: {orderId}");
            }
            catch (Exception ex)
            {
                await ShowAlert("Hata", ex.Message);
            }
        }


        // Küçük yardımcı
        private static bool TryParseDouble(string? s, out double v) =>
            double.TryParse((s ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v);



        private void RequestSymbolMarketData(int conId, string secType, string exchange)
        {
            // Önce varsa eski ticker'ı iptal et
            if (_currentTickerId.HasValue)
                _twsService.CancelMarketData(_currentTickerId.Value);

            // Snapshot delayed data iste
            _currentTickerId = _twsService.RequestMarketData(
                conId: conId,
                secType: secType,
                exchange: exchange,
                currency: "USD",
                marketDataType: 3 // delayed
            );

            // MarketData event'ini yakala
            _twsService.OnMarketData += MarketDataReceived;
        }

        private void MarketDataReceived(MarketData data)
        {
            if (_currentTickerId.HasValue && data.TickerId == _currentTickerId.Value)
            {
                // Örnek: UI Label güncelle
                MainThread.BeginInvokeOnMainThread(() =>
                {
                   var LastPriceLabel = data.Last > 0 ? data.Last.ToString("F2") : "N/A";
                  var  BidLabel = data.Bid > 0 ? data.Bid.ToString("F2") : "N/A";
                   var AskLabel = data.Ask > 0 ? data.Ask.ToString("F2") : "N/A";
                });
            }
        }


    }
}
