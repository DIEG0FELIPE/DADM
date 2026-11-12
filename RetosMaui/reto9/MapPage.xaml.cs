using Mapsui;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Tiling;
using NetTopologySuite.Geometries;
using reto9.Models;
using reto9.Services;
using LabelStyle = Mapsui.Styles.LabelStyle;
using MBrush = Mapsui.Styles.Brush;
using MColor = Mapsui.Styles.Color;
using MFont = Mapsui.Styles.Font;
using MOffset = Mapsui.Styles.Offset;
using MPen = Mapsui.Styles.Pen;
using SymbolStyle = Mapsui.Styles.SymbolStyle;
using SymbolType = Mapsui.Styles.SymbolType;
using VectorStyle = Mapsui.Styles.VectorStyle;

namespace reto9
{
    public partial class MapPage : ContentPage
    {
        /// <summary>De dónde salió el punto alrededor del cual se buscan lugares.</summary>
        private enum OriginSource { Gps, Address, MapPoint }

        private sealed record Origin(double Latitude, double Longitude, OriginSource Source);

        // Las etiquetas con el nombre se dibujan solo con zoom de barrio (resolución en metros por píxel)
        private const double LabelMaxResolution = 40;

        private readonly MemoryLayer _areaLayer = new("Radio") { Style = null };
        private readonly MemoryLayer _placesLayer = new("Lugares") { Style = null };
        private readonly MemoryLayer _selectionLayer = new("Selección") { Style = null };
        private readonly MemoryLayer _originLayer = new("Origen") { Style = null };

        private Origin? _origin;
        private IReadOnlyList<PointOfInterest> _places = [];
        private PointOfInterest? _selected;
        private string _settingsSignature = AppSettings.Signature;
        private bool _started;
        private CancellationTokenSource? _searchCts;

        public MapPage()
        {
            InitializeComponent();

            var map = MapView.Map;
            map.Layers.Add(OpenStreetMap.CreateTileLayer(OsmHttp.UserAgent));
            map.Layers.Add([_areaLayer, _placesLayer, _selectionLayer, _originLayer]);
            map.Tapped += OnMapTapped;

            // Vista inicial mientras llega la ubicación: Colombia completa
            var (x, y) = SphericalMercator.FromLonLat(-74.08, 4.6);
            map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 2500);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (!_started)
            {
                _started = true;
                await UseMyLocationAsync();
                return;
            }

            // Al volver de Ajustes: si cambió el radio o las categorías, se repite la búsqueda
            if (_settingsSignature != AppSettings.Signature)
            {
                _settingsSignature = AppSettings.Signature;
                if (_origin is not null)
                    await SearchAroundAsync(_origin, resolveAddress: false);
            }
        }

        // ----------------------------------------------------------------- Origen: GPS, dirección o punto del mapa

        private async Task UseMyLocationAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                ShowOriginMessage("Sin permiso de ubicación",
                    "Busque una dirección arriba o mantenga presionado un punto del mapa.");
                return;
            }

            ShowBusy("Obteniendo su ubicación…");
            try
            {
                var location = await Geolocation.Default.GetLocationAsync(
                                   new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15)))
                               ?? await Geolocation.Default.GetLastKnownLocationAsync();

                if (location is null)
                {
                    ShowOriginMessage("No se pudo obtener la ubicación",
                        "Intente de nuevo con el botón ◎, busque una dirección o mantenga presionado el mapa.");
                    return;
                }

                await SearchAroundAsync(new Origin(location.Latitude, location.Longitude, OriginSource.Gps), resolveAddress: true);
            }
            catch (FeatureNotEnabledException)
            {
                ShowOriginMessage("La ubicación está desactivada",
                    "Active la ubicación (GPS) del dispositivo y toque el botón ◎.");
            }
            catch (Exception ex) when (ex is FeatureNotSupportedException or PermissionException)
            {
                ShowOriginMessage("No se pudo obtener la ubicación", ex.Message);
            }
            finally
            {
                HideBusy();
            }
        }

        private async void OnMyLocationClicked(object? sender, EventArgs e) => await UseMyLocationAsync();

        private async void OnAddressSearchPressed(object? sender, EventArgs e)
        {
            var text = AddressSearch.Text?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            AddressSearch.Unfocus();
            ShowBusy("Buscando la dirección…");
            try
            {
                var matches = await NominatimService.SearchAsync(text, CancellationToken.None);
                HideBusy();

                if (matches.Count == 0)
                {
                    await DisplayAlertAsync("Sin resultados", $"No se encontró \"{text}\". Pruebe agregando la ciudad.", "OK");
                    return;
                }

                // Si hay varias coincidencias, el usuario elige
                var place = matches[0];
                if (matches.Count > 1)
                {
                    var choice = await DisplayActionSheetAsync("¿Cuál de estos lugares?", "Cancelar", null,
                        matches.Select(m => m.Name).ToArray());
                    var chosen = matches.FirstOrDefault(m => m.Name == choice);
                    if (chosen is null)
                        return;
                    place = chosen;
                }

                await SearchAroundAsync(new Origin(place.Latitude, place.Longitude, OriginSource.Address),
                    resolveAddress: false, knownAddress: place.Name);
            }
            catch (ServiceException ex)
            {
                await DisplayAlertAsync("Error", ex.Message, "OK");
            }
            finally
            {
                HideBusy();
            }
        }

        private void OnMapTapped(object? sender, MapEventArgs e)
        {
            switch (e.GestureType)
            {
                // Mantener presionado: buscar alrededor de ese punto
                case GestureType.LongPress:
                    var (lon, lat) = SphericalMercator.ToLonLat(e.WorldPosition.X, e.WorldPosition.Y);
                    e.Handled = true;
                    MainThread.BeginInvokeOnMainThread(async () =>
                        await SearchAroundAsync(new Origin(lat, lon, OriginSource.MapPoint), resolveAddress: true));
                    break;

                // Tocar un marcador: ver el lugar; tocar el mapa vacío: cerrar el detalle
                case GestureType.SingleTap:
                    var info = e.GetMapInfo([_placesLayer]);
                    var place = info.Feature?.Data as PointOfInterest;
                    e.Handled = place is not null;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (place is not null)
                            SelectPlace(place, center: false);
                        else if (_selected is not null)
                            ClearSelection();
                    });
                    break;
            }
        }

        // ----------------------------------------------------------------- Búsqueda de lugares

        /// <summary>Fija el punto de búsqueda, dibuja el radio y consulta los lugares cercanos.</summary>
        private async Task SearchAroundAsync(Origin origin, bool resolveAddress, string? knownAddress = null)
        {
            _searchCts?.Cancel();
            var cts = _searchCts = new CancellationTokenSource();

            bool sameOrigin = _origin is not null && _origin.Latitude == origin.Latitude && _origin.Longitude == origin.Longitude;
            _origin = origin;
            double radiusKm = AppSettings.RadiusKm;
            var categories = AppSettings.EnabledCategories;

            ClearSelection();
            ShowPlaces([]);
            ResultsLabel.IsVisible = false;
            DrawOrigin(origin, radiusKm);

            OriginTitle.Text = origin.Source switch
            {
                OriginSource.Gps => "Su ubicación actual (GPS)",
                OriginSource.Address => "Dirección buscada",
                _ => "Punto elegido en el mapa",
            };
            OriginCoordinates.Text = GeoMath.FormatCoordinates(origin.Latitude, origin.Longitude);
            OriginCoordinates.IsVisible = true;
            if (!sameOrigin || knownAddress is not null)
            {
                OriginAddress.Text = knownAddress ?? (resolveAddress ? "Buscando la dirección…" : "");
                OriginAddress.IsVisible = !string.IsNullOrEmpty(OriginAddress.Text);
            }

            if (resolveAddress)
                _ = ResolveAddressAsync(origin, cts.Token);

            if (categories.Count == 0)
            {
                ShowResults("No hay categorías seleccionadas. Elija qué lugares mostrar en Ajustes.");
                return;
            }

            ShowBusy($"Buscando lugares a menos de {AppSettings.FormatRadius(radiusKm)}…");
            try
            {
                var result = await OverpassService.SearchAsync(origin.Latitude, origin.Longitude, radiusKm, categories, cts.Token);
                if (cts.IsCancellationRequested)
                    return;

                ShowPlaces(result.Places);
                string where = $"a menos de {AppSettings.FormatRadius(radiusKm)}";
                ShowResults(result.Places.Count switch
                {
                    0 => $"No se encontraron lugares {where}. Aumente el radio o elija otras categorías en Ajustes.",
                    1 => $"1 lugar {where}. Toque el marcador para ver los detalles.",
                    _ when result.Truncated => $"Se muestran los {result.Places.Count} lugares más cercanos {where}. Toque un marcador para ver los detalles.",
                    _ => $"{result.Places.Count} lugares {where}. Toque un marcador para ver los detalles.",
                });
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Una búsqueda más nueva reemplazó a esta
            }
            catch (Exception ex) when (ex is ServiceException or System.Text.Json.JsonException)
            {
                ShowResults(ex is ServiceException ? ex.Message : "La respuesta del servicio de lugares no es válida. Inténtelo de nuevo.");
                Toast.Show(this, "No se pudieron cargar los lugares");
            }
            finally
            {
                if (_searchCts == cts)
                    HideBusy();
            }
        }

        private async Task ResolveAddressAsync(Origin origin, CancellationToken ct)
        {
            string? address;
            try
            {
                address = await NominatimService.ReverseAsync(origin.Latitude, origin.Longitude, ct);
            }
            catch (Exception ex) when (ex is ServiceException or OperationCanceledException or System.Text.Json.JsonException)
            {
                address = null;
            }

            if (ct.IsCancellationRequested || _origin != origin)
                return;
            OriginAddress.Text = address ?? "Dirección no disponible";
            OriginAddress.IsVisible = true;
        }

        // ----------------------------------------------------------------- Dibujo en el mapa

        private static MPoint ToWorld(double lat, double lon)
        {
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            return new MPoint(x, y);
        }

        private static MColor ToMapColor(Color color, float alpha = 1) =>
            MColor.FromArgb((int)(alpha * 255), (int)(color.Red * 255), (int)(color.Green * 255), (int)(color.Blue * 255));

        /// <summary>Marca el punto de búsqueda y el círculo del radio, y ajusta el zoom al círculo.</summary>
        private void DrawOrigin(Origin origin, double radiusKm)
        {
            var blue = MColor.FromArgb(255, 25, 118, 210);

            var dot = new PointFeature(ToWorld(origin.Latitude, origin.Longitude));
            dot.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.6,
                Fill = new MBrush(blue),
                Outline = new MPen(MColor.White, 3),
            });
            _originLayer.Features = [dot];

            var ring = GeoMath.Circle(origin.Latitude, origin.Longitude, radiusKm)
                .Select(p => { var (x, y) = SphericalMercator.FromLonLat(p.Lon, p.Lat); return new Coordinate(x, y); })
                .ToArray();
            var area = new GeometryFeature(new Polygon(new LinearRing(ring)));
            area.Styles.Add(new VectorStyle
            {
                Fill = new MBrush(MColor.FromArgb(40, 25, 118, 210)),
                Outline = new MPen(MColor.FromArgb(200, 25, 118, 210), 2),
            });
            _areaLayer.Features = [area];

            _areaLayer.DataHasChanged();
            _originLayer.DataHasChanged();

            // Que el círculo completo quepa en pantalla, con un margen
            MapView.Map.Navigator.ZoomToBox(area.Extent!.Grow(area.Extent.Width * 0.08), MBoxFit.Fit, duration: 400);
        }

        /// <summary>Un marcador de color por lugar, con su nombre como etiqueta.</summary>
        private void ShowPlaces(IReadOnlyList<PointOfInterest> places)
        {
            _places = places;
            _placesLayer.Features = places.Select(p =>
            {
                var feature = new PointFeature(ToWorld(p.Latitude, p.Longitude)) { Data = p };
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    SymbolScale = 0.5,
                    Fill = new MBrush(ToMapColor(p.Category.Color)),
                    Outline = new MPen(MColor.White, 2),
                });
                feature.Styles.Add(new LabelStyle
                {
                    Text = p.Name,
                    Font = new MFont { Size = 12 },
                    ForeColor = MColor.FromArgb(255, 33, 33, 33),
                    BackColor = new MBrush(MColor.FromArgb(215, 255, 255, 255)),
                    CornerRounding = 4,
                    Offset = new MOffset(0, -18),
                    CollisionDetection = true,   // si dos nombres se tapan, se dibuja solo uno
                    MaxWidth = 14,
                    WordWrap = LabelStyle.LineBreakMode.TailTruncation,
                    MaxVisible = LabelMaxResolution,
                });
                return (IFeature)feature;
            }).ToList();
            _placesLayer.DataHasChanged();
        }

        // ----------------------------------------------------------------- Lugar seleccionado

        private void SelectPlace(PointOfInterest place, bool center)
        {
            _selected = place;

            var ring = new PointFeature(ToWorld(place.Latitude, place.Longitude));
            ring.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.9,
                Fill = new MBrush(MColor.Transparent),
                Outline = new MPen(ToMapColor(place.Category.Color), 4),
            });
            ring.Styles.Add(new LabelStyle
            {
                Text = place.Name,
                Font = new MFont { Size = 13, Bold = true },
                ForeColor = MColor.White,
                BackColor = new MBrush(ToMapColor(place.Category.Color)),
                CornerRounding = 4,
                Offset = new MOffset(0, -24),
            });
            _selectionLayer.Features = [ring];
            _selectionLayer.DataHasChanged();

            if (center)
                MapView.Map.Navigator.CenterOnAndZoomTo(ToWorld(place.Latitude, place.Longitude),
                    Math.Min(MapView.Map.Navigator.Viewport.Resolution, 4), duration: 400);

            PlaceColor.Fill = new SolidColorBrush(place.Category.Color);
            PlaceName.Text = place.Name;
            PlaceSummary.Text = $"{place.Kind} · a {place.DistanceText}";
            SetOptional(PlaceAddress, place.Address, "📍 ");
            SetOptional(PlacePhone, place.Phone, "☎ ");
            SetOptional(PlaceHours, place.OpeningHours, "🕒 ");
            SetOptional(PlaceWebsite, place.Website, "");

            OriginPanel.IsVisible = false;
            PlacePanel.IsVisible = true;
        }

        private void ClearSelection()
        {
            _selected = null;
            _selectionLayer.Features = [];
            _selectionLayer.DataHasChanged();
            PlacePanel.IsVisible = false;
            OriginPanel.IsVisible = true;
        }

        private static void SetOptional(Label label, string? value, string prefix)
        {
            label.Text = value is null ? null : prefix + value;
            label.IsVisible = value is not null;
        }

        private void OnClosePlaceClicked(object? sender, EventArgs e) => ClearSelection();

        private async void OnDirectionsClicked(object? sender, EventArgs e)
        {
            if (_selected is null)
                return;
            try
            {
                // Abre la app de mapas del teléfono con la ruta hasta el lugar
                await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(_selected.Latitude, _selected.Longitude,
                    new MapLaunchOptions { Name = _selected.Name, NavigationMode = NavigationMode.Walking });
            }
            catch (Exception)
            {
                Toast.Show(this, "No hay una aplicación de mapas instalada");
            }
        }

        private async void OnWebsiteTapped(object? sender, TappedEventArgs e)
        {
            var site = _selected?.Website;
            if (site is null)
                return;
            if (!site.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                site = "https://" + site;
            try
            {
                await Browser.Default.OpenAsync(site, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception)
            {
                Toast.Show(this, "No se pudo abrir la página");
            }
        }

        // ----------------------------------------------------------------- Barra de herramientas

        private async void OnListClicked(object? sender, EventArgs e)
        {
            if (_places.Count == 0)
            {
                Toast.Show(this, "No hay lugares para mostrar");
                return;
            }
            await Navigation.PushAsync(new PlacesPage(_places, place => SelectPlace(place, center: true)));
        }

        private async void OnSettingsClicked(object? sender, EventArgs e) =>
            await Navigation.PushAsync(new SettingsPage());

        // ----------------------------------------------------------------- Mensajes

        private void ShowOriginMessage(string title, string detail)
        {
            ClearSelection();
            OriginTitle.Text = title;
            OriginAddress.Text = detail;
            OriginAddress.IsVisible = true;
            OriginCoordinates.IsVisible = false;
            ResultsLabel.IsVisible = false;
        }

        private void ShowResults(string text)
        {
            ResultsLabel.Text = text + "\nMantenga presionado el mapa para buscar en otro punto.";
            ResultsLabel.IsVisible = true;
        }

        private void ShowBusy(string text)
        {
            BusyLabel.Text = text;
            BusyBox.IsVisible = true;
        }

        private void HideBusy() => BusyBox.IsVisible = false;
    }
}
