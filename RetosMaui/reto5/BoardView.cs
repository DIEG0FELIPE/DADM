using System.Reflection;
using Microsoft.Maui.Graphics.Platform;
using IImage = Microsoft.Maui.Graphics.IImage;

namespace reto5
{
    /// <summary>
    /// Vista personalizada que dibuja el tablero: las líneas de la cuadrícula y las imágenes de X y O.
    /// Equivale al BoardView (android.view.View) del tutorial.
    /// </summary>
    public class BoardView : GraphicsView, IDrawable
    {
        // Ancho de las líneas de la cuadrícula
        public const int GRID_WIDTH = 6;

        private IImage? mHumanBitmap;
        private IImage? mComputerBitmap;

        private TicTacToeGame? mGame;

        public BoardView()
        {
            Initialize();
        }

        public void Initialize()
        {
            Drawable = this;
            mHumanBitmap = LoadImage("x_img.png");
            mComputerBitmap = LoadImage("o_img.png");
        }

        private static IImage LoadImage(string name)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new FileNotFoundException($"No se encontró el recurso incrustado {name}");
            return PlatformImage.FromStream(stream);
        }

        public void SetGame(TicTacToeGame game)
        {
            mGame = game;
        }

        public float GetBoardCellWidth()
        {
            return (float)Width / 3;
        }

        public float GetBoardCellHeight()
        {
            return (float)Height / 3;
        }

        /// <summary>Se llama automáticamente cada vez que la vista necesita pintarse (onDraw).</summary>
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // Determinar el ancho y alto de la vista
            float boardWidth = (float)Width;
            float boardHeight = (float)Height;

            // Líneas gruesas gris claro
            canvas.Antialias = true;
            canvas.StrokeColor = Colors.LightGray;
            canvas.StrokeSize = GRID_WIDTH;

            // Dibujar las dos líneas verticales
            float cellWidth = boardWidth / 3;
            canvas.DrawLine(cellWidth, 0, cellWidth, boardHeight);
            canvas.DrawLine(cellWidth * 2, 0, cellWidth * 2, boardHeight);

            // Dibujar las dos líneas horizontales
            float cellHeight = boardHeight / 3;
            canvas.DrawLine(0, cellHeight, boardWidth, cellHeight);
            canvas.DrawLine(0, cellHeight * 2, boardWidth, cellHeight * 2);

            // Dibujar todas las imágenes de X y O
            for (int i = 0; i < TicTacToeGame.BOARD_SIZE; i++)
            {
                int col = i % 3;
                int row = i / 3;

                // Límites del rectángulo destino de la imagen: la celda sin la mitad
                // de la línea de cada lado y con un pequeño margen
                float left = col * cellWidth + GRID_WIDTH;
                float top = row * cellHeight + GRID_WIDTH;
                float right = (col + 1) * cellWidth - GRID_WIDTH;
                float bottom = (row + 1) * cellHeight - GRID_WIDTH;

                IImage? image = mGame?.GetBoardOccupant(i) switch
                {
                    TicTacToeGame.HUMAN_PLAYER => mHumanBitmap,
                    TicTacToeGame.COMPUTER_PLAYER => mComputerBitmap,
                    _ => null
                };

                if (image != null)
                    canvas.DrawImage(image, left, top, right - left, bottom - top);
            }
        }
    }
}
