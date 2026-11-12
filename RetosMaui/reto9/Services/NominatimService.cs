using System.Globalization;
using System.Text.Json;

namespace reto9.Services
{
    /// <summary>
    /// Geocodificación con Nominatim (el buscador de OpenStreetMap):
    /// de una dirección a coordenadas y de coordenadas a una dirección.
    /// </summary>
    public static class NominatimService
    {
        private const string BaseUrl = "https://nominatim.openstreetmap.org";

        public sealed record Place(string Name, double Latitude, double Longitude);

        /// <summary>Busca una dirección o un lugar por su nombre. Devuelve hasta 5 coincidencias.</summary>
        public static async Task<IReadOnlyList<Place>> SearchAsync(string text, CancellationToken ct)
        {
            string url = $"{BaseUrl}/search?format=jsonv2&addressdetails=1&limit=5&q={Uri.EscapeDataString(text)}";
            using var doc = await GetJsonAsync(url, ct);

            return doc.RootElement.EnumerateArray()
                .Select(el => new Place(
                    ShortAddress(el) ?? el.GetProperty("display_name").GetString() ?? text,
                    ParseDouble(el.GetProperty("lat")),
                    ParseDouble(el.GetProperty("lon"))))
                .ToList();
        }

        /// <summary>Dirección aproximada de unas coordenadas, o null si no se encuentra.</summary>
        public static async Task<string?> ReverseAsync(double lat, double lon, CancellationToken ct)
        {
            string url = string.Create(CultureInfo.InvariantCulture,
                $"{BaseUrl}/reverse?format=jsonv2&addressdetails=1&zoom=18&lat={lat:0.######}&lon={lon:0.######}");
            using var doc = await GetJsonAsync(url, ct);

            if (doc.RootElement.TryGetProperty("error", out _))
                return null;
            return ShortAddress(doc.RootElement) ?? doc.RootElement.GetProperty("display_name").GetString();
        }

        private static async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
        {
            try
            {
                using var response = await OsmHttp.Client.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                throw new ServiceException("No se pudo consultar el servicio de direcciones. Revise la conexión a Internet.", ex);
            }
        }

        /// <summary>
        /// "Carrera 45 # 26-85, Teusaquillo, Bogotá" en lugar del display_name completo,
        /// que incluye región, código postal y país.
        /// </summary>
        private static string? ShortAddress(JsonElement el)
        {
            if (!el.TryGetProperty("address", out var a))
                return null;

            string? Get(params string[] keys) => keys
                .Select(k => a.TryGetProperty(k, out var v) ? v.GetString() : null)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            var name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
            var road = Get("road", "pedestrian", "footway");
            var number = Get("house_number");
            var area = Get("neighbourhood", "suburb", "quarter");
            var city = Get("city", "town", "village", "municipality", "state");

            var street = road is null ? null : number is null ? road : $"{road} # {number}";
            var parts = new[] { string.IsNullOrWhiteSpace(name) ? null : name, street, area, city }
                .Where(p => p is not null).Distinct().ToList();
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        private static double ParseDouble(JsonElement el) =>
            el.ValueKind == JsonValueKind.Number ? el.GetDouble() : double.Parse(el.GetString()!, CultureInfo.InvariantCulture);
    }
}
