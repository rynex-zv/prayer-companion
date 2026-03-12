namespace Pray_Ad_Free {
    public partial class MainPage : ContentPage {
        int count = 0;

        public MainPage() {
            InitializeComponent();
        }

        private void OnCounterClicked( object? sender , EventArgs e ) {
            count++;

            if (count == 1)
                CounterBtn.Text = string.Format(Services.LocalizationManager.Translate("MainPageClickedOne"), count);
            else
                CounterBtn.Text = string.Format(Services.LocalizationManager.Translate("MainPageClickedMany"), count);

            SemanticScreenReader.Announce( CounterBtn.Text );
        }
    }
}
