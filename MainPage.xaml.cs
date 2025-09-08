using ArcTriggerUI.Dashboard;
using ArcTriggerUI.Interfaces;
using ArcTriggerUI.Tws.Services;
using ArcTriggerUI.Tws.Functions;
using Microsoft.Maui;
using ArcTriggerUI.Dtos.Orders;

namespace ArcTriggerUI
{
    public partial class MainPage : ContentPage
    {
        // private readonly IApiService _apiService;

        private readonly TwsService _tws;

        private readonly ContractFunctions _contractFunctions;
        private readonly MarketDataFunctions _marketDataFunctions;
        private readonly OrderFunctions _orderFunctions;
        private bool _isDark = false; // dark/light toggle state

        public MainPage(TwsService twsService)
        {
            InitializeComponent();
            // _apiService = apiService;

            _tws = twsService;

            _contractFunctions = new ContractFunctions(_tws);
            _marketDataFunctions = new MarketDataFunctions(_tws);
            _orderFunctions = new OrderFunctions(_tws);

            // Başlangıçta temaya göre toolbar ayarla
            var app = Application.Current;
            if (app is not null)
            {
                _isDark = app.UserAppTheme == AppTheme.Dark;
                UpdateDarkModeButton();
            }
        }
        private void NumberEntry_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(NumberEntry.Text))
            {
                // sadece rakam girilsin
                if (!int.TryParse(NumberEntry.Text, out int value) || value < 1 || value > 9)
                {
                    NumberEntry.Text = string.Empty; // geçersizse temizle
                }
                else if (NumberEntry.Text.Length > 1) // 2 haneli olmasın
                {
                    NumberEntry.Text = NumberEntry.Text[0].ToString();
                }
            }
        }


        private void OnAddOrderClicked(object sender, EventArgs e)
        {
            int count = 1; // default
            if (int.TryParse(NumberEntry.Text, out var n) && n > 0)
                count = n;

            for (int i = 0; i < count; i++)
            {
                var order = new OrderFrame(_contractFunctions, _marketDataFunctions, _orderFunctions);
                OrdersContainer.Children.Add(order);
            }
        }

        private void OnToggleThemeClicked(object sender, EventArgs e)
        {
            var app = Application.Current;
            if (app is null) return;

            _isDark = !_isDark;
            app.UserAppTheme = _isDark ? AppTheme.Dark : AppTheme.Light;

            UpdateDarkModeButton();
        }

        private void UpdateDarkModeButton()
        {
            if (_isDark)
            {
                btnDarkMode.IconImageSource = "day_mode.png";
                btnDarkMode.Text = "Light";
            }
            else
            {
                btnDarkMode.IconImageSource = "night_mode.png";
                btnDarkMode.Text = "Dark";
            }
        }
    }
}
