namespace reto9.Services
{
    /// <summary>Cálculos sobre la esfera terrestre (latitud y longitud en grados).</summary>
    public static class GeoMath
    {
        private const double EarthRadiusKm = 6371.0088;

        /// <summary>Distancia en kilómetros entre dos puntos (fórmula del haversine).</summary>
        public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }

        /// <summary>
        /// Puntos (lon, lat) de un círculo de <paramref name="radiusKm"/> alrededor del centro.
        /// Se calculan sobre la esfera, así el círculo mide lo mismo en el terreno a cualquier latitud.
        /// </summary>
        public static IEnumerable<(double Lon, double Lat)> Circle(double lat, double lon, double radiusKm, int segments = 72)
        {
            double angular = radiusKm / EarthRadiusKm;
            double lat1 = ToRad(lat), lon1 = ToRad(lon);
            for (int i = 0; i <= segments; i++)
            {
                double bearing = 2 * Math.PI * (i % segments) / segments;
                double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(angular) +
                                        Math.Cos(lat1) * Math.Sin(angular) * Math.Cos(bearing));
                double lon2 = lon1 + Math.Atan2(Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(lat1),
                                                Math.Cos(angular) - Math.Sin(lat1) * Math.Sin(lat2));
                yield return (ToDeg(lon2), ToDeg(lat2));
            }
        }

        public static string FormatCoordinates(double lat, double lon) =>
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{lat:0.00000}, {lon:0.00000}");

        private static double ToRad(double deg) => deg * Math.PI / 180;
        private static double ToDeg(double rad) => rad * 180 / Math.PI;
    }
}
