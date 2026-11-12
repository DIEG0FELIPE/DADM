using reto9.Models;

namespace reto9
{
    /// <summary>Los lugares encontrados, del más cercano al más lejano. Tocar uno lo muestra en el mapa.</summary>
    public partial class PlacesPage : ContentPage
    {
        private readonly Action<PointOfInterest> _onSelected;

        public PlacesPage(IReadOnlyList<PointOfInterest> places, Action<PointOfInterest> onSelected)
        {
            InitializeComponent();
            _onSelected = onSelected;
            PlacesView.ItemsSource = places;
            CountLabel.Text = places.Count == 1 ? "1 lugar, ordenado por distancia" : $"{places.Count} lugares, ordenados por distancia";
        }

        private async void OnPlaceSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not PointOfInterest place)
                return;
            await Navigation.PopAsync();
            _onSelected(place);
        }
    }
}
