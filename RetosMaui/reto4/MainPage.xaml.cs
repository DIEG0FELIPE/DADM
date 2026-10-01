namespace reto4
{
    public partial class MainPage : ContentPage
    {
        private static readonly Color HumanColor = Color.FromRgb(0, 200, 0);
        private static readonly Color ComputerColor = Color.FromRgb(200, 0, 0);

        private readonly TicTacToeGame mGame = new();
        private readonly Button[] mBoardButtons;
        private bool mGameOver = false;

        // Evita reaccionar a CheckedChanged cuando se marca el radio desde el código
        private bool mSyncingDifficulty = false;

        public MainPage()
        {
            InitializeComponent();

            mBoardButtons = [One, Two, Three, Four, Five, Six, Seven, Eight, Nine];

            StartNewGame();
        }

        private void StartNewGame()
        {
            mGame.ClearBoard();
            mGameOver = false;

            foreach (var button in mBoardButtons)
                button.Text = "";

            InfoLabel.Text = "You go first.";
        }

        private void SetMove(char player, int location)
        {
            if (!mGame.SetMove(player, location))
                return;

            mBoardButtons[location].Text = player.ToString();
            mBoardButtons[location].TextColor = player == TicTacToeGame.HUMAN_PLAYER ? HumanColor : ComputerColor;
        }

        private void OnBoardButtonClicked(object? sender, EventArgs e)
        {
            int location = Array.IndexOf(mBoardButtons, sender);
            if (mGameOver || !mGame.IsOpen(location))
                return;

            SetMove(TicTacToeGame.HUMAN_PLAYER, location);

            int winner = mGame.CheckForWinner();
            if (winner == 0)
            {
                InfoLabel.Text = "Android's turn.";
                SetMove(TicTacToeGame.COMPUTER_PLAYER, mGame.GetComputerMove());
                winner = mGame.CheckForWinner();
            }

            if (winner == 0)
            {
                InfoLabel.Text = "Your turn.";
            }
            else
            {
                mGameOver = true;
                InfoLabel.Text = winner switch
                {
                    1 => "It's a tie.",
                    2 => "You won!",
                    _ => "Android won!"
                };
            }
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
