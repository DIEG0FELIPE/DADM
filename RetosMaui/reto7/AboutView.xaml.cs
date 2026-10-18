namespace reto7
{
    public partial class AboutView : ContentView
    {
        public AboutView()
        {
            InitializeComponent();
        }

        private void OnOkClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
        }
    }
}
