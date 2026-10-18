using System.Text.Json.Serialization;

namespace reto7
{
    // Estados posibles de un juego en la nube
    public static class GameStatus
    {
        public const string Waiting = "waiting";     // el anfitrión espera a un invitado
        public const string Playing = "playing";
        public const string Over = "over";           // la partida terminó (se puede pedir revancha)
        public const string Abandoned = "abandoned"; // uno de los jugadores salió
    }

    public class PlayerInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class OnlineScores
    {
        public int Host { get; set; }
        public int Guest { get; set; }
        public int Ties { get; set; }
    }

    /// <summary>Un juego tal como se guarda en /games/{id}. El anfitrión juega X y el invitado O.</summary>
    public class OnlineGame
    {
        // Clave del nodo en Firebase (no forma parte del contenido)
        [JsonIgnore]
        public string Id { get; set; } = "";

        public PlayerInfo? Host { get; set; }
        public PlayerInfo? Guest { get; set; }

        // Tablero de 9 caracteres (X, O o espacio)
        public string Board { get; set; } = new string(TicTacToeGame.OPEN_SPOT, TicTacToeGame.BOARD_SIZE);

        // "X" u "O": a quién le toca
        public string Turn { get; set; } = "X";
        public string FirstMover { get; set; } = "X";

        public string Status { get; set; } = GameStatus.Waiting;

        // "", "X", "O" o "tie"
        public string Winner { get; set; } = "";

        public OnlineScores Scores { get; set; } = new();

        // Marca de tiempo (ms, hora del servidor) de la última señal de vida del anfitrión
        public long Heartbeat { get; set; }

        // Id del jugador que abandonó el juego
        public string? LeftBy { get; set; }

        [JsonIgnore]
        public char TurnMark => Turn.Length > 0 ? Turn[0] : TicTacToeGame.PLAYER_X;
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(OnlineGame))]
    [JsonSerializable(typeof(Dictionary<string, OnlineGame>))]
    internal partial class FirebaseJsonContext : JsonSerializerContext
    {
    }
}
