using Microsoft.Extensions.Logging;

namespace reto8
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // La base de datos SQLite vive en la carpeta privada de la app y se comparte entre las pantallas
            builder.Services.AddSingleton(_ => new CompanyRepository(Path.Combine(FileSystem.AppDataDirectory, "companies.db3")));
            builder.Services.AddTransient<CompaniesPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
