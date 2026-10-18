using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace reto7
{
    /// <summary>
    /// Cliente mínimo de la API REST de Firebase Realtime Database
    /// (https://firebase.google.com/docs/reference/rest/database). No usa el SDK de Firebase.
    /// </summary>
    public class FirebaseClient
    {
        private static readonly TimeSpan StreamRetryDelay = TimeSpan.FromSeconds(2);

        private readonly HttpClient mHttp = new() { Timeout = TimeSpan.FromSeconds(15) };

        // La conexión de eventos queda abierta indefinidamente, por eso no puede tener tiempo límite
        private readonly HttpClient mStreamHttp = new() { Timeout = Timeout.InfiniteTimeSpan };

        // Diferencia entre el reloj del servidor y el local, calculada con la cabecera Date de las respuestas
        private long mServerOffsetMs;

        /// <summary>Hora actual del servidor en milisegundos (misma escala que ServerValue.TIMESTAMP).</summary>
        public long ServerNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Interlocked.Read(ref mServerOffsetMs);

        private static string BuildUrl(string path, string? query)
        {
            string url = $"{FirebaseConfig.DatabaseUrl.TrimEnd('/')}/{path}.json";
            return query == null ? url : $"{url}?{query}";
        }

        private void UpdateServerTime(HttpResponseMessage response)
        {
            if (response.Headers.Date is { } date)
                Interlocked.Exchange(ref mServerOffsetMs, date.ToUnixTimeMilliseconds() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string path, JsonNode? body)
        {
            var request = new HttpRequestMessage(method, BuildUrl(path, null));
            if (body != null)
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            return request;
        }

        /// <summary>Lee un nodo. Retorna null si no existe.</summary>
        public async Task<T?> GetAsync<T>(string path, JsonTypeInfo<T> typeInfo, string? query = null, CancellationToken ct = default)
        {
            using var response = await mHttp.GetAsync(BuildUrl(path, query), ct);
            UpdateServerTime(response);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, ct);
        }

        /// <summary>Reemplaza el contenido del nodo.</summary>
        public Task PutAsync(string path, JsonNode body, CancellationToken ct = default)
        {
            return SendAsync(HttpMethod.Put, path, body, ct);
        }

        /// <summary>Actualiza solo los campos indicados (admite rutas como "a/b").</summary>
        public Task PatchAsync(string path, JsonNode body, CancellationToken ct = default)
        {
            return SendAsync(HttpMethod.Patch, path, body, ct);
        }

        public Task DeleteAsync(string path, CancellationToken ct = default)
        {
            return SendAsync(HttpMethod.Delete, path, null, ct);
        }

        /// <summary>
        /// Escribe solo si la ubicación está vacía (cabecera if-match: null_etag).
        /// Retorna false si otro ya había escrito allí (el servidor responde 412).
        /// </summary>
        public async Task<bool> PutIfAbsentAsync(string path, JsonNode body, CancellationToken ct = default)
        {
            using var request = CreateRequest(HttpMethod.Put, path, body);
            request.Headers.TryAddWithoutValidation("if-match", "null_etag");

            using var response = await mHttp.SendAsync(request, ct);
            UpdateServerTime(response);
            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                return false;

            response.EnsureSuccessStatusCode();
            return true;
        }

        private async Task SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct)
        {
            using var request = CreateRequest(method, path, body);
            using var response = await mHttp.SendAsync(request, ct);
            UpdateServerTime(response);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Escucha los cambios de un nodo con Server-Sent Events y llama a onChange cada vez que hay uno
        /// (también al conectarse). No entrega los datos: quien escucha vuelve a leer el nodo.
        /// Si la conexión se cae, reintenta hasta que se cancele el token.
        /// </summary>
        public async Task StreamAsync(string path, string? query, Action onChange, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(path, query));
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                    using var response = await mStreamHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();

                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var reader = new StreamReader(stream);

                    // Cada evento llega como "event: <nombre>", "data: <json>" y una línea en blanco
                    string? eventName = null;
                    while (await reader.ReadLineAsync(ct) is { } line)
                    {
                        if (line.StartsWith("event:"))
                        {
                            eventName = line["event:".Length..].Trim();
                        }
                        else if (line.Length == 0)
                        {
                            if (eventName is "put" or "patch")
                                onChange();
                            else if (eventName is "cancel" or "auth_revoked")
                                break; // el servidor cerró el stream: reconectar

                            eventName = null;
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    // Sin red o conexión cerrada: se reintenta después de una pausa
                }

                try
                {
                    await Task.Delay(StreamRetryDelay, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
