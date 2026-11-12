namespace reto9.Models
{
    /// <summary>Un lugar cercano encontrado en OpenStreetMap.</summary>
    public sealed class PointOfInterest
    {
        public required long OsmId { get; init; }
        public required string Name { get; init; }
        public required double Latitude { get; init; }
        public required double Longitude { get; init; }
        public required PoiCategory Category { get; init; }

        /// <summary>Tipo concreto en español: "Museo", "Clínica"...</summary>
        public required string Kind { get; init; }

        /// <summary>Distancia en kilómetros desde el punto de búsqueda.</summary>
        public double DistanceKm { get; init; }

        public string? Address { get; init; }
        public string? Phone { get; init; }
        public string? Website { get; init; }
        public string? OpeningHours { get; init; }

        public Brush CategoryBrush => new SolidColorBrush(Category.Color);

        public string DistanceText => FormatDistance(DistanceKm);

        /// <summary>"Museo · 350 m"</summary>
        public string Summary => $"{Kind} · {DistanceText}";

        public static string FormatDistance(double km) =>
            km < 1 ? $"{km * 1000:0} m" : $"{km:0.0} km";
    }
}
