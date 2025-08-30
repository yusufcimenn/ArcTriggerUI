namespace ArcTriggerUI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {

            var window = new Window(new AppShell())
            {
                Width = 1700,     // açılış genişliği
                Height = 900      // açılış yüksekliği
            };
#if NET8_0_OR_GREATER
            // İstersen daralmayı da sınırla (opsiyonel)
            window.MinimumWidth = 1600;
            window.MinimumHeight = 800;
#endif

            return window;
        }
    }
}