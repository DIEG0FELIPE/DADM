namespace reto9.Services
{
    /// <summary>
    /// Cliente HTTP compartido para los servicios de OpenStreetMap. Sus políticas de uso piden
    /// que cada aplicación se identifique con un User-Agent propio.
    /// </summary>
    public static class OsmHttp
    {
        public const string UserAgent = "Reto9-LugaresCercanos/1.0 (app academica .NET MAUI)";

        public static HttpClient Client { get; } = Create();

        private static HttpClient Create()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es");
            return client;
        }
    }

    /// <summary>Error de red o de un servicio, con un mensaje listo para mostrar al usuario.</summary>
    public sealed class ServiceException(string message, Exception? inner = null) : Exception(message, inner);
}
