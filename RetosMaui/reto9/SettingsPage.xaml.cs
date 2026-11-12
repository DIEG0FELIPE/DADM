using Microsoft.Maui.Controls.Shapes;
using reto9.Models;
using reto9.Services;

namespace reto9
{
    /// <summary>
    /// Preferencias: el radio en kilómetros y las categorías de lugares. Cada cambio se guarda
    /// de inmediato con <see cref="AppSettings"/> (Preferences); el mapa vuelve a buscar al regresar.
    /// </summary>
    public partial class SettingsPage : ContentPage
    {
        private const double Step = 0.5;
        private readonly Dictionary<PoiCategory, CheckOption> _options = [];

        public SettingsPage()
        {
            InitializeComponent();

            RadiusSlider.Maximum = AppSettings.MaxRadiusKm;
            RadiusSlider.Minimum = AppSettings.MinRadiusKm;
            MinLabel.Text = AppSettings.FormatRadius(AppSettings.MinRadiusKm);
            MaxLabel.Text = AppSettings.FormatRadius(AppSettings.MaxRadiusKm);

            foreach (var category in PoiCategory.All)
            {
                var option = new CheckOption { Text = category.Name };
                option.CheckedChanged += (_, _) => AppSettings.SetEnabled(category, option.IsChecked);
                _options[category] = option;

                var dot = new Ellipse { Fill = new SolidColorBrush(category.Color), WidthRequest = 12, HeightRequest = 12, VerticalOptions = LayoutOptions.Center };
                CategoriesList.Add(new HorizontalStackLayout { Spacing = 4, Children = { option, dot } });
            }

            LoadValues();
        }

        private void LoadValues()
        {
            RadiusSlider.Value = AppSettings.RadiusKm;
            RadiusLabel.Text = AppSettings.FormatRadius(AppSettings.RadiusKm);
            foreach (var (category, option) in _options)
                option.IsChecked = AppSettings.IsEnabled(category);
        }

        private void OnRadiusChanged(object? sender, ValueChangedEventArgs e)
        {
            // El Slider es continuo; se redondea a pasos de 500 m
            double km = Math.Round(e.NewValue / Step) * Step;
            if (Math.Abs(RadiusSlider.Value - km) > 0.001)
            {
                RadiusSlider.Value = km;
                return;
            }
            AppSettings.RadiusKm = km;
            RadiusLabel.Text = AppSettings.FormatRadius(km);
        }

        private void OnResetClicked(object? sender, EventArgs e)
        {
            AppSettings.RadiusKm = AppSettings.DefaultRadiusKm;
            foreach (var category in PoiCategory.All)
                AppSettings.SetEnabled(category, category.EnabledByDefault);
            LoadValues();
            Toast.Show(this, "Valores restablecidos");
        }
    }
}
