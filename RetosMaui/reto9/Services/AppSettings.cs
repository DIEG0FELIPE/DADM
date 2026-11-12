using reto9.Models;

namespace reto9.Services
{
    /// <summary>
    /// Preferencias de la app (equivalente a SharedPreferences en Android):
    /// el radio de búsqueda en kilómetros y las categorías de lugares que se muestran.
    /// </summary>
    public static class AppSettings
    {
        public const double MinRadiusKm = 0.5;
        public const double MaxRadiusKm = 20;
        public const double DefaultRadiusKm = 2;

        private const string RadiusKey = "radius_km";
        private const string CategoryPrefix = "category_";

        public static double RadiusKm
        {
            get => Math.Clamp(Preferences.Default.Get(RadiusKey, DefaultRadiusKm), MinRadiusKm, MaxRadiusKm);
            set => Preferences.Default.Set(RadiusKey, Math.Clamp(value, MinRadiusKm, MaxRadiusKm));
        }

        public static bool IsEnabled(PoiCategory category) =>
            Preferences.Default.Get(CategoryPrefix + category.Key, category.EnabledByDefault);

        public static void SetEnabled(PoiCategory category, bool enabled) =>
            Preferences.Default.Set(CategoryPrefix + category.Key, enabled);

        public static IReadOnlyList<PoiCategory> EnabledCategories =>
            PoiCategory.All.Where(IsEnabled).ToList();

        /// <summary>Texto que resume la configuración actual, para saber si cambió al volver de Ajustes.</summary>
        public static string Signature =>
            $"{RadiusKm}|{string.Join(",", EnabledCategories.Select(c => c.Key))}";

        public static string FormatRadius(double km) =>
            km < 1 ? $"{km * 1000:0} m" : $"{km:0.#} km";
    }
}
