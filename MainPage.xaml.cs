using ArcTriggerUI.Dashboard;
using ArcTriggerUI.Interfaces;
using Microsoft.Maui;

namespace ArcTriggerUI
{
    public partial class MainPage : ContentPage
    {
        private readonly IApiService _apiService;
        private bool _isDark = false; // dark/light toggle state

        public MainPage(IApiService apiService)
        {
            InitializeComponent();
            _apiService = apiService;

            // Başlangıçta temaya göre toolbar ayarla
            var app = Application.Current;
            if (app is not null)
            {
                _isDark = app.UserAppTheme == AppTheme.Dark;
                UpdateDarkModeButton();
            }
        }

        private void OnAddOrderClicked(object sender, EventArgs e)
        {
            int count = 1; // default
            if (int.TryParse(NumberEntry.Text, out var n) && n > 0)
                count = n;

            for (int i = 0; i < count; i++)
            {
                var order = new OrderFrame(_apiService);
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
