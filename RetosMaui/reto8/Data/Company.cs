using SQLite;

namespace reto8
{
    /// <summary>Clasificación de una empresa. Puede tener varias a la vez, por eso es un conjunto de banderas.</summary>
    [Flags]
    public enum CompanyType
    {
        None = 0,
        Consulting = 1,
        CustomDevelopment = 2,
        SoftwareFactory = 4,
    }

    public static class CompanyTypes
    {
        public static readonly CompanyType[] All =
        {
            CompanyType.Consulting,
            CompanyType.CustomDevelopment,
            CompanyType.SoftwareFactory,
        };

        public static string LabelOf(CompanyType type)
        {
            return type switch
            {
                CompanyType.Consulting => "Consultoría",
                CompanyType.CustomDevelopment => "Desarrollo a la medida",
                CompanyType.SoftwareFactory => "Fábrica de software",
                _ => "Sin clasificación",
            };
        }

        /// <summary>Texto con todas las clasificaciones marcadas, separadas por un punto medio.</summary>
        public static string Describe(CompanyType types)
        {
            if (types == CompanyType.None)
                return LabelOf(CompanyType.None);

            return string.Join(" · ", All.Where(t => types.HasFlag(t)).Select(LabelOf));
        }
    }

    /// <summary>Una empresa del directorio; cada propiedad es una columna de la tabla Companies.</summary>
    [Table("Companies")]
    public class Company
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull, MaxLength(80)]
        public string Name { get; set; } = "";

        public string Website { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";

        // Productos y servicios que ofrece la empresa (texto libre)
        public string Products { get; set; } = "";

        // Se guarda como entero: 1 = consultoría, 2 = desarrollo a la medida, 4 = fábrica de software (sumados si son varias)
        public CompanyType Classification { get; set; }

        [Ignore]
        public string ClassificationText => CompanyTypes.Describe(Classification);

        // Para ocultar la línea del teléfono en la lista cuando no se registró
        [Ignore]
        public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
    }
}
