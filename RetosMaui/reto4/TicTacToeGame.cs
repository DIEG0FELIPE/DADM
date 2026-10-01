namespace reto4
{
    public class TicTacToeGame
    {
        // Niveles de dificultad del computador
        public enum DifficultyLevel { Easy, Harder, Expert }

        // Caracteres usados para representar a los jugadores y espacios libres
        public const char HUMAN_PLAYER = 'X';
        public const char COMPUTER_PLAYER = 'O';
        public const char OPEN_SPOT = ' ';
        public const int BOARD_SIZE = 9;

        private readonly char[] mBoard = new char[BOARD_SIZE];
        private readonly Random mRand = new();

        // Nivel de dificultad actual
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Expert;

        public TicTacToeGame()
        {
            ClearBoard();
        }

        /// <summary>Limpia el tablero configurando todos los espacios como OPEN_SPOT.</summary>
        public void ClearBoard()
        {
            for (int i = 0; i < BOARD_SIZE; i++)
                mBoard[i] = OPEN_SPOT;
        }

        /// <summary>Indica si la ubicación dada está libre.</summary>
        public bool IsOpen(int location)
        {
            return location >= 0 && location < BOARD_SIZE && mBoard[location] == OPEN_SPOT;
        }

        /// <summary>Asigna el jugador a la ubicación dada. Retorna false si no se pudo.</summary>
        public bool SetMove(char player, int location)
        {
            if (!IsOpen(location))
                return false;

            mBoard[location] = player;
            return true;
        }

        /// <summary>Retorna la jugada del computador según el nivel de dificultad.</summary>
        public int GetComputerMove()
        {
            int move = -1;

            if (Difficulty == DifficultyLevel.Easy)
            {
                move = GetRandomMove();
            }
            else if (Difficulty == DifficultyLevel.Harder)
            {
                move = GetWinningMove();
                if (move == -1)
                    move = GetRandomMove();
            }
            else if (Difficulty == DifficultyLevel.Expert)
            {
                // Intentar ganar; si no es posible, bloquear; si tampoco, mover a cualquier lado
                move = GetWinningMove();
                if (move == -1)
                    move = GetBlockingMove();
                if (move == -1)
                    move = GetRandomMove();
            }

            return move;
        }

        /// <summary>Retorna una ubicación libre al azar, o -1 si el tablero está lleno.</summary>
        public int GetRandomMove()
        {
            var openSpots = new List<int>();
            for (int i = 0; i < BOARD_SIZE; i++)
            {
                if (mBoard[i] == OPEN_SPOT)
                    openSpots.Add(i);
            }

            return openSpots.Count == 0 ? -1 : openSpots[mRand.Next(openSpots.Count)];
        }

        /// <summary>Retorna una jugada con la que el computador gana, o -1 si no existe.</summary>
        public int GetWinningMove()
        {
            return FindCompletingMove(COMPUTER_PLAYER, 3);
        }

        /// <summary>Retorna la jugada que bloquea la victoria del humano, o -1 si no existe.</summary>
        public int GetBlockingMove()
        {
            return FindCompletingMove(HUMAN_PLAYER, 2);
        }

        /// <summary>
        /// Prueba cada espacio libre con el jugador dado y retorna el primero que produce
        /// el resultado esperado. El tablero queda en el mismo estado en que estaba.
        /// </summary>
        private int FindCompletingMove(char player, int expectedWinner)
        {
            for (int i = 0; i < BOARD_SIZE; i++)
            {
                if (mBoard[i] != OPEN_SPOT)
                    continue;

                mBoard[i] = player;
                bool wins = CheckForWinner() == expectedWinner;
                mBoard[i] = OPEN_SPOT; // Restaurar el espacio vacío

                if (wins)
                    return i;
            }

            return -1;
        }

        /// <summary>Verifica si hay un ganador. Retorna 0 (ninguno), 1 (empate), 2 (X ganó), 3 (O ganó).</summary>
        public int CheckForWinner()
        {
            // 1. Comprobar las 3 filas horizontales
            for (int i = 0; i <= 6; i += 3)
            {
                if (mBoard[i] != OPEN_SPOT && mBoard[i] == mBoard[i + 1] && mBoard[i + 1] == mBoard[i + 2])
                    return mBoard[i] == HUMAN_PLAYER ? 2 : 3;
            }

            // 2. Comprobar las 3 columnas verticales
            for (int i = 0; i <= 2; i++)
            {
                if (mBoard[i] != OPEN_SPOT && mBoard[i] == mBoard[i + 3] && mBoard[i + 3] == mBoard[i + 6])
                    return mBoard[i] == HUMAN_PLAYER ? 2 : 3;
            }

            // 3. Comprobar las 2 diagonales
            if ((mBoard[0] != OPEN_SPOT && mBoard[0] == mBoard[4] && mBoard[4] == mBoard[8]) ||
                (mBoard[2] != OPEN_SPOT && mBoard[2] == mBoard[4] && mBoard[4] == mBoard[6]))
                return mBoard[4] == HUMAN_PLAYER ? 2 : 3;

            // 4. Comprobar si aún hay espacios vacíos (el juego continúa)
            for (int i = 0; i < BOARD_SIZE; i++)
            {
                if (mBoard[i] == OPEN_SPOT)
                    return 0;
            }

            // 5. Si no hubo ganador y no hay espacios vacíos, es un empate
            return 1;
        }
    }
}
