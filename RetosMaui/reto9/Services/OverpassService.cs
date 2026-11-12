using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using reto9.Models;

namespace reto9.Services
{
    /// <summary>
    /// Busca puntos de interés cercanos con Overpass API, el servicio de consultas sobre
    /// los datos de OpenStreetMap. No necesita clave.
    /// </summary>
    public static class OverpassService
    {
        /// <summary>Máximo de lugares que se dibujan (los más cercanos).</summary>
        public const int MaxResults = 300;

        // Si el servidor principal está saturado (es gratuito y a veces responde 429/504) se prueba el siguiente.
        private static readonly string[] Endpoints =
        [
            "https://overpass-api.de/api/interpreter",
            "https://overpass.private.coffee/api/interpreter",
            "https://overpass.kumi.systems/api/interpreter",
        ];

        public sealed record SearchResult(IReadOnlyList<PointOfInterest> Places, bool Truncated);

        public static async Task<SearchResult> SearchAsync(double lat, double lon, double radiusKm,
            IReadOnlyList<PoiCategory> categories, CancellationToken ct)
        {
            if (categories.Count == 0)
                return new SearchResult([], false);

            string query = BuildQuery(lat, lon, radiusKm, categories);
            string json = await PostWithFallbackAsync(query, ct);
            var places = Parse(json, lat, lon, categories);

            var nearest = places.OrderBy(p => p.DistanceKm).Take(MaxResults).ToList();
            return new SearchResult(nearest, places.Count > MaxResults);
        }

        /// <summary>
        /// Arma la consulta en Overpass QL. Por cada etiqueta de cada categoría pide nodos, vías y
        /// relaciones (nwr) que tengan nombre y estén dentro del radio; "out center" da un punto
        /// central para las vías y relaciones (un hospital suele estar dibujado como un polígono).
        /// </summary>
        public static string BuildQuery(double lat, double lon, double radiusKm, IReadOnlyList<PoiCategory> categories)
        {
            var inv = CultureInfo.InvariantCulture;
            string around = string.Create(inv, $"(around:{radiusKm * 1000:0},{lat:0.######},{lon:0.######})");

            var sb = new StringBuilder("[out:json][timeout:25];(");
            foreach (var (tag, values) in categories.SelectMany(c => c.Filters))
                sb.Append($"nwr[\"{tag}\"~\"^({string.Join('|', values)})$\"][\"name\"]{around};");
            sb.Append(");out center tags 1500;");
            return sb.ToString();
        }

        private static async Task<string> PostWithFallbackAsync(string query, CancellationToken ct)
        {
            Exception? last = null;
            foreach (var endpoint in Endpoints)
            {
                try
                {
                    using var content = new FormUrlEncodedContent([new("data", query)]);
                    using var response = await OsmHttp.Client.PostAsync(endpoint, content, ct);
                    if (response.IsSuccessStatusCode)
                        return await response.Content.ReadAsStringAsync(ct);

                    last = new HttpRequestException($"{endpoint} respondió {(int)response.StatusCode}", null, response.StatusCode);
                    // 400 = la consulta está mal; otro servidor respondería igual
                    if (response.StatusCode == HttpStatusCode.BadRequest)
                        break;
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    last = ex; // sin conexión o tiempo agotado: probar el siguiente servidor
                }
            }

            throw new ServiceException("No se pudieron consultar los lugares cercanos. Revise la conexión a Internet e inténtelo de nuevo.", last);
        }

        public static List<PointOfInterest> Parse(string json, double originLat, double originLon, IReadOnlyList<PoiCategory> categories)
        {
            var result = new List<PointOfInterest>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("elements", out var elements))
                return result;

            foreach (var el in elements.EnumerateArray())
            {
                if (!el.TryGetProperty("tags", out var tagsEl))
                    continue;
                var tags = tagsEl.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");

                // Los nodos traen lat/lon; las vías y relaciones, un "center"
                var pos = el.TryGetProperty("center", out var center) ? center : el;
                if (!pos.TryGetProperty("lat", out var latEl) || !pos.TryGetProperty("lon", out var lonEl))
                    continue;

                var category = categories.FirstOrDefault(c => c.Matches(tags));
                if (category is null || !tags.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                    continue;

                double lat = latEl.GetDouble(), lon = lonEl.GetDouble();
                // El valor de la etiqueta que hizo coincidir la categoría ("museum", "clinic"...)
                var kindValue = category.Filters
                    .Select(f => tags.TryGetValue(f.Tag, out var v) && f.Values.Contains(v) ? v : null)
                    .First(v => v is not null)!;

                result.Add(new PointOfInterest
                {
                    OsmId = el.GetProperty("id").GetInt64(),
                    Name = name.Trim(),
                    Latitude = lat,
                    Longitude = lon,
                    Category = category,
                    Kind = PoiCategory.DescribeKind(kindValue),
                    DistanceKm = GeoMath.DistanceKm(originLat, originLon, lat, lon),
                    Address = BuildAddress(tags),
                    Phone = First(tags, "phone", "contact:phone"),
                    Website = First(tags, "website", "contact:website", "url"),
                    OpeningHours = First(tags, "opening_hours"),
                });
            }

            return RemoveDuplicates(result);
        }

        /// <summary>
        /// Un mismo lugar a veces está en OSM dos veces (como punto y como polígono).
        /// Si tienen el mismo nombre y categoría y están a menos de 150 m, se deja uno.
        /// </summary>
        private static List<PointOfInterest> RemoveDuplicates(List<PointOfInterest> places)
        {
            var kept = new List<PointOfInterest>();
            foreach (var p in places.OrderBy(p => p.DistanceKm))
            {
                bool duplicate = kept.Any(k => k.Category == p.Category &&
                                               string.Equals(k.Name, p.Name, StringComparison.OrdinalIgnoreCase) &&
                                               GeoMath.DistanceKm(k.Latitude, k.Longitude, p.Latitude, p.Longitude) < 0.15);
                if (!duplicate)
                    kept.Add(p);
            }
            return kept;
        }

        private static string? BuildAddress(Dictionary<string, string> tags)
        {
            if (tags.TryGetValue("addr:full", out var full))
                return full;
            var street = tags.GetValueOrDefault("addr:street");
            if (street is null)
                return null;
            var number = tags.GetValueOrDefault("addr:housenumber");
            var city = tags.GetValueOrDefault("addr:city");
            var line = number is null ? street : $"{street} # {number}";
            return city is null ? line : $"{line}, {city}";
        }

        private static string? First(Dictionary<string, string> tags, params string[] keys) =>
            keys.Select(k => tags.GetValueOrDefault(k)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
