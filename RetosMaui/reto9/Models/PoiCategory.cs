namespace reto9.Models
{
    /// <summary>
    /// Tipo de punto de interés que se puede buscar. Cada categoría sabe qué etiquetas de
    /// OpenStreetMap la identifican (por ejemplo amenity=hospital) y con qué color se dibuja.
    /// </summary>
    public sealed class PoiCategory
    {
        private PoiCategory(string key, string name, string color, bool enabledByDefault, params (string Tag, string[] Values)[] filters)
        {
            Key = key;
            Name = name;
            ColorHex = color;
            EnabledByDefault = enabledByDefault;
            Filters = filters;
        }

        /// <summary>Clave estable; se usa para guardar la preferencia.</summary>
        public string Key { get; }

        public string Name { get; }

        /// <summary>Color del marcador en el mapa y del punto en la lista (#RRGGBB).</summary>
        public string ColorHex { get; }

        public Color Color => Color.FromArgb(ColorHex);

        public bool EnabledByDefault { get; }

        /// <summary>Etiquetas de OSM: el lugar pertenece a la categoría si alguna coincide.</summary>
        public IReadOnlyList<(string Tag, string[] Values)> Filters { get; }

        /// <summary>Indica si un elemento de OSM, por sus etiquetas, pertenece a esta categoría.</summary>
        public bool Matches(IReadOnlyDictionary<string, string> tags) =>
            Filters.Any(f => tags.TryGetValue(f.Tag, out var value) && f.Values.Contains(value));

        public static readonly IReadOnlyList<PoiCategory> All =
        [
            new("health", "Hospitales y clínicas", "#D32F2F", true,
                ("amenity", ["hospital", "clinic", "doctors"]),
                ("healthcare", ["hospital", "clinic"])),
            new("tourism", "Lugares turísticos", "#7B1FA2", true,
                ("tourism", ["attraction", "museum", "viewpoint", "gallery", "zoo", "theme_park", "aquarium"]),
                ("historic", ["monument", "castle", "ruins", "archaeological_site"])),
            new("pharmacy", "Farmacias", "#2E7D32", false,
                ("amenity", ["pharmacy"])),
            new("park", "Parques", "#558B2F", false,
                ("leisure", ["park", "garden", "nature_reserve"])),
            new("food", "Restaurantes y cafés", "#EF6C00", false,
                ("amenity", ["restaurant", "cafe", "fast_food"])),
            new("lodging", "Hoteles", "#1565C0", false,
                ("tourism", ["hotel", "hostel", "guest_house", "motel"])),
            new("education", "Educación", "#6D4C41", false,
                ("amenity", ["school", "university", "college", "library"])),
            new("emergency", "Policía y bomberos", "#37474F", false,
                ("amenity", ["police", "fire_station"])),
        ];

        /// <summary>Nombre en español del tipo de lugar (el valor de la etiqueta de OSM).</summary>
        public static string DescribeKind(string value) => value switch
        {
            "hospital" => "Hospital",
            "clinic" => "Clínica",
            "doctors" => "Consultorio médico",
            "attraction" => "Atracción turística",
            "museum" => "Museo",
            "viewpoint" => "Mirador",
            "gallery" => "Galería",
            "zoo" => "Zoológico",
            "theme_park" => "Parque de diversiones",
            "aquarium" => "Acuario",
            "monument" => "Monumento",
            "castle" => "Castillo",
            "ruins" => "Ruinas",
            "archaeological_site" => "Sitio arqueológico",
            "pharmacy" => "Farmacia",
            "park" => "Parque",
            "garden" => "Jardín",
            "nature_reserve" => "Reserva natural",
            "restaurant" => "Restaurante",
            "cafe" => "Café",
            "fast_food" => "Comida rápida",
            "hotel" => "Hotel",
            "hostel" => "Hostal",
            "guest_house" => "Casa de huéspedes",
            "motel" => "Motel",
            "school" => "Colegio",
            "university" => "Universidad",
            "college" => "Instituto",
            "library" => "Biblioteca",
            "police" => "Policía",
            "fire_station" => "Estación de bomberos",
            _ => value,
        };
    }
}
