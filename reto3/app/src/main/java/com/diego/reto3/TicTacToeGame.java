package com.diego.reto3;

import java.util.Random;

public class TicTacToeGame {
    // Caracteres usados para representar a los jugadores y espacios libres
    public static final char HUMAN_PLAYER = 'X';
    public static final char COMPUTER_PLAYER = 'O';
    public static final char OPEN_SPOT = ' ';
    public static final int BOARD_SIZE = 9;

    private char[] mBoard = new char[BOARD_SIZE];
    private Random mRand;

    public TicTacToeGame() {
        // Inicializar el generador de números aleatorios
        mRand = new Random();
    }

    /** Limpia el tablero configurando todos los espacios como OPEN_SPOT. */
    public void clearBoard() {
        for (int i = 0; i < BOARD_SIZE; i++) {
            mBoard[i] = OPEN_SPOT;
        }
    }

    /** Asigna el jugador a la ubicación dada. */
    public void setMove(char player, int location) {
        if (location >= 0 && location < BOARD_SIZE && mBoard[location] == OPEN_SPOT) {
            mBoard[location] = player;
        }
    }

    /** Retorna la mejor jugada para el computador. */
    /** Retorna la mejor jugada para el computador. */
    public int getComputerMove() {
        int move;

        // 1. Verificar si el computador puede ganar en este turno
        for (int i = 0; i < BOARD_SIZE; i++) {
            if (mBoard[i] == OPEN_SPOT) {
                mBoard[i] = COMPUTER_PLAYER;
                if (checkForWinner() == 3) {
                    mBoard[i] = OPEN_SPOT; // Restaurar el espacio vacío
                    return i;
                }
                mBoard[i] = OPEN_SPOT; // Restaurar el espacio vacío
            }
        }

        // 2. Verificar si el humano está a punto de ganar para bloquearlo
        for (int i = 0; i < BOARD_SIZE; i++) {
            if (mBoard[i] == OPEN_SPOT) {
                mBoard[i] = HUMAN_PLAYER;
                if (checkForWinner() == 2) {
                    mBoard[i] = OPEN_SPOT; // Restaurar el espacio vacío
                    return i;
                }
                mBoard[i] = OPEN_SPOT; // Restaurar el espacio vacío
            }
        }

        // 3. Si no hay jugada para ganar o bloquear, hacer un movimiento aleatorio
        do {
            move = mRand.nextInt(BOARD_SIZE);
        } while (mBoard[move] != OPEN_SPOT);

        return move;
    }

    /** Verifica si hay un ganador. Retorna 0 (ninguno), 1 (empate), 2 (X ganó), 3 (O ganó). */
    public int checkForWinner() {
        // 1. Comprobar las 3 filas horizontales
        for (int i = 0; i <= 6; i += 3) {
            if (mBoard[i] == mBoard[i+1] && mBoard[i+1] == mBoard[i+2] && mBoard[i] != OPEN_SPOT) {
                if (mBoard[i] == HUMAN_PLAYER) return 2;
                else return 3;
            }
        }

        // 2. Comprobar las 3 columnas verticales
        for (int i = 0; i <= 2; i++) {
            if (mBoard[i] == mBoard[i+3] && mBoard[i+3] == mBoard[i+6] && mBoard[i] != OPEN_SPOT) {
                if (mBoard[i] == HUMAN_PLAYER) return 2;
                else return 3;
            }
        }

        // 3. Comprobar las 2 diagonales
        if ((mBoard[0] == mBoard[4] && mBoard[4] == mBoard[8] && mBoard[0] != OPEN_SPOT) ||
                (mBoard[2] == mBoard[4] && mBoard[4] == mBoard[6] && mBoard[2] != OPEN_SPOT)) {
            if (mBoard[4] == HUMAN_PLAYER) return 2;
            else return 3;
        }

        // 4. Comprobar si aún hay espacios vacíos (el juego continúa)
        for (int i = 0; i < BOARD_SIZE; i++) {
            if (mBoard[i] == OPEN_SPOT) {
                return 0; // Juego en curso
            }
        }

        // 5. Si no hubo ganador y no hay espacios vacíos, es un empate
        return 1;
    }

}