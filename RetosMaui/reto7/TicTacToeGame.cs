namespace reto7
{
    public class TicTacToeGame
    {
        // Caracteres usados para representar a los jugadores y espacios libres.
        // El anfitrión del juego es X y el invitado es O.
        public const char PLAYER_X = 'X';
        public const char PLAYER_O = 'O';
        public const char OPEN_SPOT = ' ';
        public const int BOARD_SIZE = 9;

        // Resultados de CheckForWinner
        public const int IN_PROGRESS = 0;
        public const int TIE = 1;
        public const int X_WON = 2;
        public const int O_WON = 3;

        private readonly char[] mBoard = new char[BOARD_SIZE];

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

        /// <summary>Retorna el ocupante (PLAYER_X, PLAYER_O u OPEN_SPOT) de la ubicación dada.</summary>
        public char GetBoardOccupant(int location)
        {
            return mBoard[location];
        }

        /// <summary>Retorna el tablero como un texto de 9 caracteres (así se guarda en la nube).</summary>
        public string GetBoardState()
        {
            return new string(mBoard);
        }

        /// <summary>
        /// Asigna el estado del tablero a partir de un texto de 9 caracteres.
        /// Un texto inválido deja el tablero vacío.
        /// </summary>
        public void SetBoardState(string? board)
        {
            if (board == null || board.Length != BOARD_SIZE)
            {
                ClearBoard();
                return;
            }

            for (int i = 0; i < BOARD_SIZE; i++)
                mBoard[i] = board[i] == PLAYER_X || board[i] == PLAYER_O ? board[i] : OPEN_SPOT;
        }

        /// <summary>Asigna el jugador a la ubicación dada. Retorna false si no se pudo.</summary>
        public bool SetMove(char player, int location)
        {
            if (!IsOpen(location))
                return false;

            mBoard[location] = player;
            return true;
        }

        /// <summary>Verifica si hay un ganador. Retorna IN_PROGRESS, TIE, X_WON u O_WON.</summary>
        public int CheckForWinner()
        {
            // 1. Comprobar las 3 filas horizontales
            for (int i = 0; i <= 6; i += 3)
            {
                if (mBoard[i] != OPEN_SPOT && mBoard[i] == mBoard[i + 1] && mBoard[i + 1] == mBoard[i + 2])
                    return mBoard[i] == PLAYER_X ? X_WON : O_WON;
            }

            // 2. Comprobar las 3 columnas verticales
            for (int i = 0; i <= 2; i++)
            {
                if (mBoard[i] != OPEN_SPOT && mBoard[i] == mBoard[i + 3] && mBoard[i + 3] == mBoard[i + 6])
                    return mBoard[i] == PLAYER_X ? X_WON : O_WON;
            }

            // 3. Comprobar las 2 diagonales
            if ((mBoard[0] != OPEN_SPOT && mBoard[0] == mBoard[4] && mBoard[4] == mBoard[8]) ||
                (mBoard[2] != OPEN_SPOT && mBoard[2] == mBoard[4] && mBoard[4] == mBoard[6]))
                return mBoard[4] == PLAYER_X ? X_WON : O_WON;

            // 4. Comprobar si aún hay espacios vacíos (el juego continúa)
            for (int i = 0; i < BOARD_SIZE; i++)
            {
                if (mBoard[i] == OPEN_SPOT)
                    return IN_PROGRESS;
            }

            // 5. Si no hubo ganador y no hay espacios vacíos, es un empate
            return TIE;
        }
    }
}
