using Plugin.Maui.Audio;

namespace reto5
{
    public partial class MainPage : ContentPage
    {
        // Tiempo que espera el computador antes de hacer su jugada
        private static readonly TimeSpan ComputerDelay = TimeSpan.FromSeconds(1);

        private readonly TicTacToeGame mGame = new();
        private bool mGameOver = false;

        // Indica si es el turno del humano; mientras el computador "piensa" se ignoran los toques
        private bool mHumanTurn = true;

        // Cambia con cada partida para descartar la jugada pendiente del computador si se empieza otra
        private int mGameNumber = 0;

        // Efectos de sonido
        private IAudioPlayer? mHumanMediaPlayer;
        private IAudioPlayer? mComputerMediaPlayer;
        private bool mSoundsRequested = false;
        private int mSoundsVersion = 0;

        // Evita reaccionar a CheckedChanged cuando se marca el radio desde el código
        private bool mSyncingDifficulty = false;

        public MainPage()
        {
            InitializeComponent();

            Board.SetGame(mGame);

            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;

            StartNewGame();
        }

        private void StartNewGame()
        {
            mGameNumber++;
            mGame.ClearBoard();
            Board.Invalidate(); // Redibujar el tablero

            mGameOver = false;
            mHumanTurn = true;
            InfoLabel.Text = "You go first.";
        }

        private bool SetMove(char player, int location)
        {
            if (!mGame.SetMove(player, location))
                return false;

            Board.Invalidate(); // Redibujar el tablero

            // Reproducir el efecto de sonido del jugador
            if (player == TicTacToeGame.HUMAN_PLAYER)
                mHumanMediaPlayer?.Play();
            else
                mComputerMediaPlayer?.Play();

            return true;
        }

        // Escucha los toques sobre el tablero
        private void OnBoardTouched(object? sender, TouchEventArgs e)
        {
            if (mGameOver || !mHumanTurn || e.Touches.Length == 0)
                return;

            // Determinar qué celda se tocó
            PointF touch = e.Touches[0];
            int col = Math.Clamp((int)(touch.X / Board.GetBoardCellWidth()), 0, 2);
            int row = Math.Clamp((int)(touch.Y / Board.GetBoardCellHeight()), 0, 2);
            int pos = row * 3 + col;

            if (!SetMove(TicTacToeGame.HUMAN_PLAYER, pos))
                return;

            int winner = mGame.CheckForWinner();
            if (winner != 0)
            {
                EndGame(winner);
                return;
            }

            // Si no hay ganador, el computador juega después de un segundo sin bloquear la interfaz
            mHumanTurn = false;
            InfoLabel.Text = "Android's turn.";

            int gameNumber = mGameNumber;
            Dispatcher.DispatchDelayed(ComputerDelay, () =>
            {
                if (gameNumber != mGameNumber)
                    return; // Se empezó otra partida mientras tanto

                SetMove(TicTacToeGame.COMPUTER_PLAYER, mGame.GetComputerMove());

                int winner = mGame.CheckForWinner();
                if (winner != 0)
                {
                    EndGame(winner);
                    return;
                }

                mHumanTurn = true;
                InfoLabel.Text = "Your turn.";
            });
        }

        private void EndGame(int winner)
        {
            mGameOver = true;
            InfoLabel.Text = winner switch
            {
                1 => "It's a tie.",
                2 => "You won!",
                _ => "Android won!"
            };
        }

        // ----- Efectos de sonido -----
        // Los reproductores ocupan recursos compartidos con otros procesos, así que se cargan cuando
        // la ventana se activa (onResume) y se liberan cuando pasa a segundo plano (onPause).

        private async void OnPageLoaded(object? sender, EventArgs e)
        {
            if (Window != null)
            {
                Window.Activated += OnWindowActivated;
                Window.Deactivated += OnWindowDeactivated;
            }

            await LoadSoundsAsync();
        }

        private void OnPageUnloaded(object? sender, EventArgs e)
        {
            if (Window != null)
            {
                Window.Activated -= OnWindowActivated;
                Window.Deactivated -= OnWindowDeactivated;
            }

            ReleaseSounds();
        }

        private async void OnWindowActivated(object? sender, EventArgs e)
        {
            await LoadSoundsAsync();
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            ReleaseSounds();
        }

        private async Task LoadSoundsAsync()
        {
            if (mSoundsRequested)
                return;

            mSoundsRequested = true;
            int version = ++mSoundsVersion;

            var human = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("human_move.mp3"));
            var computer = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("computer_move.mp3"));

            // Si se liberaron los sonidos mientras se cargaban, descartar estos reproductores
            if (version != mSoundsVersion)
            {
                human.Dispose();
                computer.Dispose();
                return;
            }

            mHumanMediaPlayer = human;
            mComputerMediaPlayer = computer;
        }

        private void ReleaseSounds()
        {
            mSoundsRequested = false;
            mSoundsVersion++;

            mHumanMediaPlayer?.Dispose();
            mComputerMediaPlayer?.Dispose();
            mHumanMediaPlayer = null;
            mComputerMediaPlayer = null;
        }

        // ----- Menú de opciones -----

        private void OnNewGameClicked(object? sender, EventArgs e)
        {
            StartNewGame();
        }

        private void OnDifficultyClicked(object? sender, EventArgs e)
        {
            // El nivel actual debe aparecer seleccionado al abrir el diálogo
            mSyncingDifficulty = true;
            EasyRadio.IsChecked = mGame.Difficulty == TicTacToeGame.DifficultyLevel.Easy;
            HarderRadio.IsChecked = mGame.Difficulty == TicTacToeGame.DifficultyLevel.Harder;
            ExpertRadio.IsChecked = mGame.Difficulty == TicTacToeGame.DifficultyLevel.Expert;
            mSyncingDifficulty = false;

            DifficultyDialog.IsVisible = true;
        }

        private async void OnQuitClicked(object? sender, EventArgs e)
        {
            bool quit = await DisplayAlertAsync(null, "Are you sure you want to quit?", "Yes", "No");
            if (quit)
                Application.Current?.Quit();
        }

        private void OnAboutClicked(object? sender, EventArgs e)
        {
            AboutDialog.IsVisible = true;
        }

        // ----- Diálogo de dificultad -----

        private void OnDifficultyChecked(object? sender, CheckedChangedEventArgs e)
        {
            if (mSyncingDifficulty || !e.Value || sender is not RadioButton radio)
                return;

            mGame.Difficulty = radio == EasyRadio ? TicTacToeGame.DifficultyLevel.Easy
                             : radio == HarderRadio ? TicTacToeGame.DifficultyLevel.Harder
                             : TicTacToeGame.DifficultyLevel.Expert;

            DifficultyDialog.IsVisible = false;
            ShowToast((string)radio.Content);
        }

        private void OnDifficultyDialogDismissed(object? sender, EventArgs e)
        {
            DifficultyDialog.IsVisible = false;
        }

        // ----- Diálogo "Acerca de" -----

        private void OnAboutOkClicked(object? sender, EventArgs e)
        {
            AboutDialog.IsVisible = false;
        }

        /// <summary>Muestra un mensaje breve (Toast nativo en Android).</summary>
        private void ShowToast(string message)
        {
#if ANDROID
            Android.Widget.Toast.MakeText(Platform.AppContext, message, Android.Widget.ToastLength.Short)?.Show();
#else
            _ = DisplayAlertAsync(null, message, "OK");
#endif
        }
    }
}
