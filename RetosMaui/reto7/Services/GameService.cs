using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace reto7
{
    public enum JoinResult { Joined, Taken }

    /// <summary>
    /// Operaciones del juego online sobre Firebase: crear, unirse, jugar, revancha y salir,
    /// y la observación en tiempo real de la lista de juegos y de un juego en particular.
    /// </summary>
    public class GameService
    {
        public static GameService Instance { get; } = new();

        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan LobbyRefreshInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        // Un juego en espera sin señal de vida durante este tiempo se oculta de la lista
        private const long HiddenAfterMs = 30_000;

        // Un juego en espera sin señal de vida durante este tiempo se borra de la base de datos
        private const long DeletedAfterMs = 10 * 60_000;

        // Solo interesan los juegos que esperan invitado
        private const string WaitingQuery = "orderBy=%22status%22&equalTo=%22waiting%22";

        private readonly FirebaseClient mClient = new();

        // ----- Crear, unirse y salir -----

        /// <summary>Crea un juego en espera y retorna su id.</summary>
        public async Task<string> CreateGameAsync(PlayerInfo host)
        {
            string id = "g" + Guid.NewGuid().ToString("N");
            var game = new JsonObject
            {
                ["host"] = PlayerNode(host),
                ["board"] = new string(TicTacToeGame.OPEN_SPOT, TicTacToeGame.BOARD_SIZE),
                ["turn"] = "X",
                ["firstMover"] = "X",
                ["status"] = GameStatus.Waiting,
                ["winner"] = "",
                ["scores"] = ScoresNode(new OnlineScores()),
                ["heartbeat"] = ServerTimestamp(),
            };

            await mClient.PutAsync($"games/{id}", game);
            return id;
        }

        /// <summary>Se une como invitado. Si otro jugador llegó primero, retorna Taken.</summary>
        public async Task<JoinResult> JoinGameAsync(string gameId, PlayerInfo guest)
        {
            // Escritura condicional: solo uno de varios jugadores simultáneos logra ocupar el lugar
            if (!await mClient.PutIfAbsentAsync($"games/{gameId}/guest", PlayerNode(guest)))
                return JoinResult.Taken;

            try
            {
                await mClient.PatchAsync($"games/{gameId}", new JsonObject { ["status"] = GameStatus.Playing });
            }
            catch
            {
                // No quedó el juego iniciado: liberar el lugar para que no quede a medias
                try { await mClient.DeleteAsync($"games/{gameId}/guest"); } catch { }
                throw;
            }

            return JoinResult.Joined;
        }

        /// <summary>
        /// Sale del juego. Si nadie más está (esperando o el rival ya salió) se borra; si no,
        /// se marca como abandonado para avisarle al rival.
        /// </summary>
        public Task LeaveGameAsync(string gameId, string playerId, OnlineGame? last)
        {
            if (last?.Host == null || last.Status == GameStatus.Waiting || last.Status == GameStatus.Abandoned)
                return mClient.DeleteAsync($"games/{gameId}");

            return mClient.PatchAsync($"games/{gameId}", new JsonObject
            {
                ["status"] = GameStatus.Abandoned,
                ["leftBy"] = playerId,
            });
        }

        public Task DeleteGameAsync(string gameId)
        {
            return mClient.DeleteAsync($"games/{gameId}");
        }

        /// <summary>Lee un juego; retorna null si ya no existe.</summary>
        public async Task<OnlineGame?> GetGameAsync(string gameId, CancellationToken ct = default)
        {
            var game = await mClient.GetAsync($"games/{gameId}", FirebaseJsonContext.Default.OnlineGame, null, ct);
            if (game != null)
                game.Id = gameId;
            return game;
        }

        // ----- Jugadas -----

        /// <summary>
        /// Envía una jugada: el tablero nuevo y el turno del rival. Si la partida terminó se indica
        /// el ganador ("X", "O" o "tie") y el marcador actualizado.
        /// </summary>
        public Task SubmitMoveAsync(string gameId, string board, char nextTurn, string? winner = null, OnlineScores? scores = null)
        {
            var patch = new JsonObject
            {
                ["board"] = board,
                ["turn"] = nextTurn.ToString(),
            };

            if (winner != null && scores != null)
            {
                patch["status"] = GameStatus.Over;
                patch["winner"] = winner;
                patch["scores"] = ScoresNode(scores);
            }

            return mClient.PatchAsync($"games/{gameId}", patch);
        }

        /// <summary>Empieza otra partida en el mismo juego con el tablero limpio.</summary>
        public Task RematchAsync(string gameId, char firstMover)
        {
            return mClient.PatchAsync($"games/{gameId}", new JsonObject
            {
                ["board"] = new string(TicTacToeGame.OPEN_SPOT, TicTacToeGame.BOARD_SIZE),
                ["turn"] = firstMover.ToString(),
                ["firstMover"] = firstMover.ToString(),
                ["status"] = GameStatus.Playing,
                ["winner"] = "",
            });
        }

        // ----- Observación en tiempo real -----

        /// <summary>
        /// Avisa (en el hilo principal) con la lista de juegos disponibles cada vez que cambia.
        /// Se excluyen los propios y los del anfitrión que dejó de dar señales de vida.
        /// </summary>
        public IDisposable ObserveLobby(Action<List<OnlineGame>> onChanged)
        {
            return Watch("games", WaitingQuery, LobbyRefreshInterval, async ct =>
            {
                var games = await mClient.GetAsync("games", FirebaseJsonContext.Default.DictionaryStringOnlineGame, WaitingQuery, ct);
                long now = mClient.ServerNowMs;
                string myId = PlayerProfile.Id;

                var available = new List<OnlineGame>();
                if (games != null)
                {
                    foreach (var (id, game) in games)
                    {
                        if (game.Host == null || game.Guest != null)
                            continue;

                        game.Id = id;
                        long age = now - game.Heartbeat;
                        bool mine = game.Host.Id == myId;

                        if (age > DeletedAfterMs || (mine && age > HiddenAfterMs))
                        {
                            _ = TryDeleteAsync(id); // juego huérfano: limpiarlo
                            continue;
                        }

                        if (!mine && age <= HiddenAfterMs)
                            available.Add(game);
                    }
                }

                available.Sort((a, b) => string.Compare(a.Host!.Name, b.Host!.Name, StringComparison.CurrentCultureIgnoreCase));

                ct.ThrowIfCancellationRequested();
                MainThread.BeginInvokeOnMainThread(() => onChanged(available));
            });
        }

        /// <summary>Avisa (en el hilo principal) cada vez que el juego cambia; null si dejó de existir.</summary>
        public IDisposable ObserveGame(string gameId, Action<OnlineGame?> onChanged)
        {
            return Watch($"games/{gameId}", null, null, async ct =>
            {
                var game = await GetGameAsync(gameId, ct);
                ct.ThrowIfCancellationRequested();
                MainThread.BeginInvokeOnMainThread(() => onChanged(game));
            });
        }

        /// <summary>Mientras no se libere, el anfitrión renueva su señal de vida para seguir apareciendo en la lista.</summary>
        public IDisposable StartHeartbeat(string gameId)
        {
            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(HeartbeatInterval, cts.Token);
                        await mClient.PutAsync($"games/{gameId}/heartbeat", ServerTimestamp(), cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        // Sin red: se intenta de nuevo en el siguiente ciclo
                    }
                }
            });

            return new Subscription(cts);
        }

        /// <summary>
        /// Vuelve a leer los datos cada vez que el stream avisa un cambio (y cada cierto intervalo
        /// si se indica). Las señales que llegan mientras se lee se agrupan en una sola lectura.
        /// </summary>
        private IDisposable Watch(string path, string? query, TimeSpan? pollInterval, Func<CancellationToken, Task> refresh)
        {
            var cts = new CancellationTokenSource();
            var signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

            _ = mClient.StreamAsync(path, query, () => signal.Writer.TryWrite(true), cts.Token);

            _ = Task.Run(async () =>
            {
                signal.Writer.TryWrite(true); // primera lectura
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        // Esperar una señal del stream o, si hay intervalo, a que se cumpla
                        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                        if (pollInterval != null)
                            wait.CancelAfter(pollInterval.Value);

                        try
                        {
                            await signal.Reader.ReadAsync(wait.Token);
                        }
                        catch (OperationCanceledException) when (!cts.IsCancellationRequested)
                        {
                            // Se cumplió el intervalo
                        }

                        await refresh(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        // Sin red o respuesta inválida: reintentar después de una pausa
                        try { await Task.Delay(RetryDelay, cts.Token); } catch (OperationCanceledException) { return; }
                    }
                }
            });

            return new Subscription(cts);
        }

        private async Task TryDeleteAsync(string gameId)
        {
            try { await mClient.DeleteAsync($"games/{gameId}"); } catch { }
        }

        // ----- Nodos JSON -----

        private static JsonObject PlayerNode(PlayerInfo player)
        {
            return new JsonObject { ["id"] = player.Id, ["name"] = player.Name };
        }

        private static JsonObject ScoresNode(OnlineScores scores)
        {
            return new JsonObject { ["host"] = scores.Host, ["guest"] = scores.Guest, ["ties"] = scores.Ties };
        }

        // Pide a Firebase que escriba la hora del servidor
        private static JsonObject ServerTimestamp()
        {
            return new JsonObject { [".sv"] = "timestamp" };
        }

        // Al liberarla cancela el stream y la lectura periódica
        private sealed class Subscription(CancellationTokenSource cts) : IDisposable
        {
            public void Dispose()
            {
                cts.Cancel();
            }
        }
    }
}
