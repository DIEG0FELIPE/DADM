using Plugin.Maui.Audio;

namespace reto6
{
    public partial class MainPage : ContentPage
    {
        // Tiempo que espera el computador antes de hacer su jugada
        private static readonly TimeSpan ComputerDelay = TimeSpan.FromSeconds(1);

        private readonly TicTacToeGame mGame = new();
        private bool mGameOver = false;

        // Marcador y quién empieza la próxima partida
        private int mHumanWins = 0;
        private int mComputerWins = 0;
        private int mTies = 0;
        private char mGoFirst = TicTacToeGame.HUMAN_PLAYER;

        // Claves de las preferencias (equivalen al archivo ttt_prefs de SharedPreferences)
        private const string PrefHumanWins = "mHumanWins";
        private const string PrefComputerWins = "mComputerWins";
        private const string PrefTies = "mTies";
        private const string PrefDifficulty = "mDifficulty";
        private const string PrefBoard = "board";
        private const string PrefGameOver = "mGameOver";
        private const string PrefHumanTurn = "mHumanTurn";
        private const string PrefInfo = "info";
        private const string PrefGoFirst = "mGoFirst";

        // Tamaño del tablero según la orientación
        private const double PortraitBoardSize = 300;
        private const double LandscapeBoardSize = 270;

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

            // Restaurar marcador y dificultad (persisten entre ejecuciones)
            mHumanWins = Preferences.Default.Get(PrefHumanWins, 0);
            mComputerWins = Preferences.Default.Get(PrefComputerWins, 0);
            mTies = Preferences.Default.Get(PrefTies, 0);
            int difficulty = Preferences.Default.Get(PrefDifficulty, (int)TicTacToeGame.DifficultyLevel.Expert);
            mGame.Difficulty = Enum.IsDefined(typeof(TicTacToeGame.DifficultyLevel), difficulty)
                ? (TicTacToeGame.DifficultyLevel)difficulty
                : TicTacToeGame.DifficultyLevel.Expert;

            if (!RestoreGameState())
                StartNewGame();

            DisplayScores();
        }

        // ----- Orientación -----

        // En vertical el tablero va arriba y la información debajo; en horizontal el tablero
        // va a la izquierda (más pequeño) y la información a la derecha.
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            if (width <= 0 || height <= 0)
                return;

            bool landscape = width > height;
            double size = landscape ? LandscapeBoardSize : PortraitBoardSize;

            if (Board.WidthRequest == size && Grid.GetColumn(InfoPanel) == (landscape ? 1 : 0))
                return;

            Board.WidthRequest = size;
            Board.HeightRequest = size;
            Grid.SetRow(InfoPanel, landscape ? 0 : 1);
            Grid.SetColumn(InfoPanel, landscape ? 1 : 0);
            Board.Invalidate();
        }

        private void StartNewGame()
        {
            mGameNumber++;
            mGame.ClearBoard();
            Board.Invalidate(); // Redibujar el tablero

            mGameOver = false;

            // Los turnos de inicio se alternan entre el humano y el computador
            if (mGoFirst == TicTacToeGame.HUMAN_PLAYER)
            {
                mHumanTurn = true;
                InfoLabel.Text = "You go first.";
                mGoFirst = TicTacToeGame.COMPUTER_PLAYER;
            }
            else
            {
                InfoLabel.Text = "Android goes first.";
                mGoFirst = TicTacToeGame.HUMAN_PLAYER;
                ScheduleComputerMove();
            }
        }

        // ----- Marcador y persistencia -----

        private void DisplayScores()
        {
            HumanScoreLabel.Text = mHumanWins.ToString();
            ComputerScoreLabel.Text = mComputerWins.ToString();
            TieScoreLabel.Text = mTies.ToString();
        }

        // Guarda el marcador (equivale a guardar en SharedPreferences dentro de onStop)
        private void SaveScores()
        {
            Preferences.Default.Set(PrefHumanWins, mHumanWins);
            Preferences.Default.Set(PrefComputerWins, mComputerWins);
            Preferences.Default.Set(PrefTies, mTies);
        }

        // Guarda la partida en curso (equivale a onSaveInstanceState)
        private void SaveGameState()
        {
            Preferences.Default.Set(PrefBoard, new string(mGame.GetBoardState()));
            Preferences.Default.Set(PrefGameOver, mGameOver);
            Preferences.Default.Set(PrefHumanTurn, mHumanTurn);
            Preferences.Default.Set(PrefInfo, InfoLabel.Text ?? "");
            Preferences.Default.Set(PrefGoFirst, mGoFirst.ToString());
        }

        // Restaura la partida guardada; retorna false si no hay ninguna
        private bool RestoreGameState()
        {
            string board = Preferences.Default.Get(PrefBoard, "");
            if (board.Length != TicTacToeGame.BOARD_SIZE)
                return false;

            mGame.SetBoardState(board.ToCharArray());
            Board.Invalidate();

            mGameOver = Preferences.Default.Get(PrefGameOver, false);
            mHumanTurn = Preferences.Default.Get(PrefHumanTurn, true);
            InfoLabel.Text = Preferences.Default.Get(PrefInfo, "");
            string goFirst = Preferences.Default.Get(PrefGoFirst, TicTacToeGame.HUMAN_PLAYER.ToString());
            mGoFirst = goFirst.Length > 0 ? goFirst[0] : TicTacToeGame.HUMAN_PLAYER;

            // Si se guardó cuando era el turno del computador, que haga su jugada
            if (!mGameOver && !mHumanTurn)
                ScheduleComputerMove();

            return true;
        }

        // El computador juega después de un segundo sin bloquear la interfaz
        private void ScheduleComputerMove()
        {
            mHumanTurn = false;

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

            // Si no hay ganador, el computador juega
            InfoLabel.Text = "Android's turn.";
            ScheduleComputerMove();
        }

        private void EndGame(int winner)
        {
            mGameOver = true;
            switch (winner)
            {
                case 1:
                    InfoLabel.Text = "It's a tie.";
                    mTies++;
                    break;
                case 2:
                    InfoLabel.Text = "You won!";
                    mHumanWins++;
                    break;
                default:
                    InfoLabel.Text = "Android won!";
                    mComputerWins++;
                    break;
            }

            DisplayScores();
            SaveScores();
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
            SaveGameState();
        }

        private async void OnWindowActivated(object? sender, EventArgs e)
        {
            await LoadSoundsAsync();
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            ReleaseSounds();
            SaveScores();
            SaveGameState();
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

        private void OnResetScoresClicked(object? sender, EventArgs e)
        {
            mHumanWins = 0;
            mComputerWins = 0;
            mTies = 0;
            DisplayScores();
            SaveScores();
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

            Preferences.Default.Set(PrefDifficulty, (int)mGame.Difficulty);

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
