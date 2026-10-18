using Plugin.Maui.Audio;

namespace reto7
{
    /// <summary>
    /// Tablero de un juego online. El estado de la partida vive en Firebase: cada jugador envía
    /// sus jugadas y recibe las del rival en tiempo real.
    /// </summary>
    public partial class GamePage : ContentPage
    {
        // Tamaño del tablero según la orientación
        private const double PortraitBoardSize = 300;
        private const double LandscapeBoardSize = 270;

        private readonly string mGameId;
        private readonly char mMyMark; // el anfitrión juega X y el invitado O
        private readonly TicTacToeGame mGame = new();

        // Último estado conocido del juego en la nube
        private OnlineGame? mState;

        private IDisposable? mGameSubscription;
        private IDisposable? mHeartbeat;

        // Hay una jugada (o revancha) propia en camino a la nube: mientras tanto se ignoran toques y avisos
        private bool mSending = false;

        // El usuario decidió salir, o el juego terminó para siempre (se borró o el rival salió)
        private bool mLeaving = false;
        private bool mEnded = false;

        // Efectos de sonido
        private IAudioPlayer? mMyMediaPlayer;
        private IAudioPlayer? mOpponentMediaPlayer;
        private bool mSoundsRequested = false;
        private int mSoundsVersion = 0;

        public GamePage(string gameId, char myMark)
        {
            InitializeComponent();

            mGameId = gameId;
            mMyMark = myMark;

            Board.SetGame(mGame);
            RoleLabel.Text = $"You play {myMark}";
            UpdateUi();

            // La flecha de la barra superior también pide confirmación antes de salir
            Shell.SetBackButtonBehavior(this, new BackButtonBehavior
            {
                Command = new Command(async () => await ConfirmLeaveAsync())
            });

            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
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

        // ----- Sincronización con la nube -----

        private void StartSync()
        {
            mGameSubscription ??= GameService.Instance.ObserveGame(mGameId, ApplyState);

            // El anfitrión avisa que sigue vivo mientras espera, para seguir apareciendo en la lista
            if (mMyMark == TicTacToeGame.PLAYER_X && mHeartbeat == null && (mState == null || mState.Guest == null))
                mHeartbeat = GameService.Instance.StartHeartbeat(mGameId);
        }

        private void StopSync()
        {
            mGameSubscription?.Dispose();
            mGameSubscription = null;
            mHeartbeat?.Dispose();
            mHeartbeat = null;
        }

        // Vuelve a leer el juego (después de enviar algo, para quedar con lo que realmente hay en la nube)
        private async Task RefreshStateAsync()
        {
            try
            {
                ApplyState(await GameService.Instance.GetGameAsync(mGameId));
            }
            catch (Exception)
            {
                // Sin red: el stream traerá el estado cuando vuelva
            }
        }

        // Aplica un estado recibido de la nube (siempre en el hilo principal)
        private void ApplyState(OnlineGame? game)
        {
            if (mLeaving || mEnded || mSending)
                return;

            if (game?.Host == null)
            {
                // Sin anfitrión: el juego se borró (o quedó a medias porque el anfitrión salió justo al unirse)
                _ = EndAsync("The game no longer exists.", deleteNode: game != null);
                return;
            }

            if (game.Status == GameStatus.Abandoned)
            {
                _ = EndAsync($"{OpponentName(game)} left the game.", deleteNode: true);
                return;
            }

            // Si el rival hizo una jugada nueva, suena su efecto
            bool opponentMoved = mState != null
                && game.TurnMark == mMyMark
                && CountMarks(game.Board) > CountMarks(mGame.GetBoardState());

            mGame.SetBoardState(game.Board);
            Board.Invalidate();
            if (opponentMoved)
                mOpponentMediaPlayer?.Play();

            mState = game;

            // Con invitado ya no hace falta la señal de vida
            if (game.Guest != null)
            {
                mHeartbeat?.Dispose();
                mHeartbeat = null;
            }

            UpdateUi();
        }

        private static int CountMarks(string board)
        {
            return board.Count(c => c != TicTacToeGame.OPEN_SPOT);
        }

        private string OpponentName(OnlineGame game)
        {
            string? name = mMyMark == TicTacToeGame.PLAYER_X ? game.Guest?.Name : game.Host?.Name;
            return string.IsNullOrEmpty(name) ? "Opponent" : name;
        }

        // Muestra el mensaje, el marcador y el menú según el estado del juego
        private void UpdateUi()
        {
            var state = mState;
            if (state == null)
            {
                InfoLabel.Text = "Connecting...";
                WaitingIndicator.IsVisible = WaitingIndicator.IsRunning = false;
                NewGameItem.IsEnabled = false;
                return;
            }

            bool waiting = state.Guest == null;
            string opponent = OpponentName(state);
            bool iAmHost = mMyMark == TicTacToeGame.PLAYER_X;

            WaitingIndicator.IsVisible = WaitingIndicator.IsRunning = waiting;
            OpponentNameLabel.Text = waiting ? "Opponent" : opponent;
            MyScoreLabel.Text = (iAmHost ? state.Scores.Host : state.Scores.Guest).ToString();
            OpponentScoreLabel.Text = (iAmHost ? state.Scores.Guest : state.Scores.Host).ToString();
            TieScoreLabel.Text = state.Scores.Ties.ToString();

            if (waiting)
                InfoLabel.Text = "Waiting for an opponent...";
            else if (state.Status == GameStatus.Over)
                InfoLabel.Text = state.Winner == "tie" ? "It's a tie."
                               : state.Winner == mMyMark.ToString() ? "You won!"
                               : $"{opponent} won!";
            else
                InfoLabel.Text = state.TurnMark == mMyMark ? "Your turn." : $"{opponent}'s turn.";

            NewGameItem.IsEnabled = state.Status == GameStatus.Over;
        }

        // El juego terminó para siempre: avisar y volver a la lista
        private async Task EndAsync(string message, bool deleteNode)
        {
            mEnded = true;
            StopSync();

            if (deleteNode)
            {
                try { await GameService.Instance.DeleteGameAsync(mGameId); } catch (Exception) { }
            }

            await DisplayAlertAsync("Tic-Tac-Toe", message, "OK");
            await Navigation.PopAsync();
        }

        // ----- Jugadas -----

        // Escucha los toques sobre el tablero
        private async void OnBoardTouched(object? sender, TouchEventArgs e)
        {
            var state = mState;
            if (state == null || state.Guest == null || state.Status != GameStatus.Playing
                || state.TurnMark != mMyMark || mSending || mLeaving || mEnded || e.Touches.Length == 0)
                return;

            // Determinar qué celda se tocó
            PointF touch = e.Touches[0];
            int col = Math.Clamp((int)(touch.X / Board.GetBoardCellWidth()), 0, 2);
            int row = Math.Clamp((int)(touch.Y / Board.GetBoardCellHeight()), 0, 2);

            if (!mGame.SetMove(mMyMark, row * 3 + col))
                return;

            Board.Invalidate();
            mMyMediaPlayer?.Play();

            // Quien juega calcula el resultado y lo guarda junto con la jugada
            char nextTurn = mMyMark == TicTacToeGame.PLAYER_X ? TicTacToeGame.PLAYER_O : TicTacToeGame.PLAYER_X;
            string board = mGame.GetBoardState();
            int result = mGame.CheckForWinner();

            string? winner = null;
            OnlineScores? scores = null;
            if (result != TicTacToeGame.IN_PROGRESS)
            {
                winner = result == TicTacToeGame.X_WON ? "X" : result == TicTacToeGame.O_WON ? "O" : "tie";
                scores = new OnlineScores
                {
                    Host = state.Scores.Host + (result == TicTacToeGame.X_WON ? 1 : 0),
                    Guest = state.Scores.Guest + (result == TicTacToeGame.O_WON ? 1 : 0),
                    Ties = state.Scores.Ties + (result == TicTacToeGame.TIE ? 1 : 0),
                };
            }

            // Reflejar la jugada de inmediato, sin esperar a la nube
            state.Board = board;
            state.Turn = nextTurn.ToString();
            if (winner != null && scores != null)
            {
                state.Status = GameStatus.Over;
                state.Winner = winner;
                state.Scores = scores;
            }
            UpdateUi();

            mSending = true;
            try
            {
                await GameService.Instance.SubmitMoveAsync(mGameId, board, nextTurn, winner, scores);
            }
            catch (Exception)
            {
                Toast.Show(this, "Could not send your move. Check your connection.");
            }
            finally
            {
                mSending = false;
            }

            // Quedar con lo que realmente hay en la nube (si la jugada falló, se deshace)
            await RefreshStateAsync();
        }

        // ----- Salir del juego -----

        protected override bool OnBackButtonPressed()
        {
            _ = ConfirmLeaveAsync();
            return true;
        }

        private async Task ConfirmLeaveAsync()
        {
            if (mLeaving || mEnded)
                return;

            // Esperando rival no hay nadie a quien afectar: se sale sin preguntar
            if (mState?.Guest != null)
            {
                bool leave = await DisplayAlertAsync("Leave game", "Do you want to leave this game?", "Leave", "Stay");
                if (!leave)
                    return;
            }

            await LeaveAsync();
        }

        private async Task LeaveAsync()
        {
            if (mLeaving || mEnded)
                return;

            mLeaving = true;
            StopSync();

            try
            {
                await GameService.Instance.LeaveGameAsync(mGameId, PlayerProfile.Id, mState);
            }
            catch (Exception)
            {
                // Sin red: el rival podrá salir con su propio botón
            }

            await Navigation.PopAsync();
        }

        // ----- Efectos de sonido -----
        // Los reproductores ocupan recursos compartidos con otros procesos, así que se cargan cuando
        // la ventana se activa (onResume) y se liberan cuando pasa a segundo plano (onPause).
        // Lo mismo se hace con la conexión a la nube.

        private async void OnPageLoaded(object? sender, EventArgs e)
        {
            if (Window != null)
            {
                Window.Activated -= OnWindowActivated;
                Window.Deactivated -= OnWindowDeactivated;
                Window.Activated += OnWindowActivated;
                Window.Deactivated += OnWindowDeactivated;
            }

            StartSync();
            await LoadSoundsAsync();
        }

        private void OnPageUnloaded(object? sender, EventArgs e)
        {
            if (Window != null)
            {
                Window.Activated -= OnWindowActivated;
                Window.Deactivated -= OnWindowDeactivated;
            }

            StopSync();
            ReleaseSounds();
        }

        private async void OnWindowActivated(object? sender, EventArgs e)
        {
            if (!mLeaving && !mEnded)
                StartSync(); // la primera lectura trae el estado actual

            await LoadSoundsAsync();
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            StopSync();
            ReleaseSounds();
        }

        private async Task LoadSoundsAsync()
        {
            if (mSoundsRequested)
                return;

            mSoundsRequested = true;
            int version = ++mSoundsVersion;

            var mine = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("human_move.mp3"));
            var opponent = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("computer_move.mp3"));

            // Si se liberaron los sonidos mientras se cargaban, descartar estos reproductores
            if (version != mSoundsVersion)
            {
                mine.Dispose();
                opponent.Dispose();
                return;
            }

            mMyMediaPlayer = mine;
            mOpponentMediaPlayer = opponent;
        }

        private void ReleaseSounds()
        {
            mSoundsRequested = false;
            mSoundsVersion++;

            mMyMediaPlayer?.Dispose();
            mOpponentMediaPlayer?.Dispose();
            mMyMediaPlayer = null;
            mOpponentMediaPlayer = null;
        }

        // ----- Menú de opciones -----

        // Nueva partida en el mismo juego: sólo cuando la actual terminó. Empieza el otro jugador.
        private async void OnNewGameClicked(object? sender, EventArgs e)
        {
            if (mSending || mLeaving || mEnded)
                return;

            mSending = true;
            try
            {
                // Confirmar con la nube que sigue terminada (el rival pudo haber empezado ya otra partida)
                var current = await GameService.Instance.GetGameAsync(mGameId);
                if (current?.Status == GameStatus.Over)
                {
                    char firstMover = current.FirstMover == "X" ? TicTacToeGame.PLAYER_O : TicTacToeGame.PLAYER_X;
                    await GameService.Instance.RematchAsync(mGameId, firstMover);
                }
            }
            catch (Exception)
            {
                Toast.Show(this, "Could not start a new game. Check your connection.");
            }
            finally
            {
                mSending = false;
            }

            await RefreshStateAsync();
        }

        private async void OnLeaveGameClicked(object? sender, EventArgs e)
        {
            await ConfirmLeaveAsync();
        }

        private void OnAboutClicked(object? sender, EventArgs e)
        {
            AboutDialog.IsVisible = true;
        }
    }
}
