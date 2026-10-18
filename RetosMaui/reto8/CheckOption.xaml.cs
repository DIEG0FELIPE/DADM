namespace reto8
{
    public partial class CheckOption : ContentView
    {
        public event EventHandler? CheckedChanged;

        public CheckOption()
        {
            InitializeComponent();
        }

        public string Text
        {
            get => Caption.Text;
            set => Caption.Text = value;
        }

        public bool IsChecked
        {
            get => Box.IsChecked;
            set => Box.IsChecked = value;
        }

        private void OnBoxCheckedChanged(object? sender, CheckedChangedEventArgs e)
        {
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnCaptionTapped(object? sender, TappedEventArgs e)
        {
            Box.IsChecked = !Box.IsChecked;
        }
    }
}
