using ArcTriggerUI.Dashboard;

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
        public MainPage()
        {
            InitializeComponent();
        }
        private void OnAutoFetchClicked(object sender, EventArgs e)
        {
            // TODO implement later
        }

        private void OnAddOrderClicked(object sender, EventArgs e)
        {
            var newOrder = new OrderFrame();
            OrdersContainer.Children.Add(newOrder);
        }

        // Symbol Picker
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

        // Call / Put
        private void OnCallOptionCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) Console.WriteLine("Order type: Call");
        }

        private void OnPutOptionCheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value) Console.WriteLine("Order type: Put");
        }

        // Trigger price
        private void OnTriggerPriceTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Trigger price: {e.NewTextValue}");
        }

        private void OnIncreaseTriggerClicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("TriggerEntry") is Entry entry && decimal.TryParse(entry.Text, out decimal value))
            {
                entry.Text = (value + 1).ToString("F2");
            }
        }

        private void OnDecreaseTriggerClicked(object sender, EventArgs e)
        {
            if (this.FindByName<Entry>("TriggerEntry") is Entry entry && decimal.TryParse(entry.Text, out decimal value))
            {
                entry.Text = (value - 1).ToString("F2");
            }
        }

        // Strike Picker
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

        // Expiry Picker
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

        // Position size
        private void OnPositionTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Position size: {e.NewTextValue}");
        }

        private void OnPositionPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("PositionEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("$", "");
            }
        }

        // Stop loss
        private void OnStopLossTextChanged(object sender, TextChangedEventArgs e)
        {
            Console.WriteLine($"Stop loss: {e.NewTextValue}");
        }

        private void OnStopLossPreset(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("StopLossEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("$", "");
            }
        }

        // Final action
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

        private void OnProfitPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("ProfitEntry") is Entry entry)
            {
                entry.Text = btn.Text.Replace("%", "");
                Console.WriteLine($"Profit taking set to {entry.Text}%");
            }
        }

        private void OnTrailClicked(object sender, EventArgs e)
        {
            Console.WriteLine("Invalidate action triggered");
        }

        private void OnBreakevenClicked(object sender, EventArgs e)
        {
            Console.WriteLine("Breakeven action triggered");
        }

        private void OnOffsetPresetClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && this.FindByName<Entry>("OffsetEntry") is Entry entry)
            {
                entry.Text = btn.Text;
                Console.WriteLine($"Offset set to {entry.Text}");
            }
        }

        // Cancel butonunda bu pencereyi kapat
        void OnCancelClicked(object sender, EventArgs e)
        {
            var w = this.Window;
            if (w != null && Application.Current != null)
                Application.Current.CloseWindow(w);
        }
    }
}
