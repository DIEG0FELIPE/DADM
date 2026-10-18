namespace reto7
{
    /// <summary>Pantalla inicial: lista de juegos disponibles, con la opción de crear uno nuevo.</summary>
    public partial class LobbyPage : ContentPage
    {
        private IDisposable? mLobbySubscription;

        // Evita crear o unirse dos veces si se toca el botón varias veces seguidas
        private bool mBusy = false;

        public LobbyPage()
        {
            InitializeComponent();
            ShowPlayerName();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (Window != null)
            {
                Window.Activated -= OnWindowActivated;
                Window.Deactivated -= OnWindowDeactivated;
                Window.Activated += OnWindowActivated;
                Window.Deactivated += OnWindowDeactivated;
            }

            // La primera vez se pide el nombre que verán los demás jugadores
            if (!PlayerProfile.HasName)
                await AskNameAsync();

            StartObserving();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (Window != null)
            {
                Window.Activated -= OnWindowActivated;
                Window.Deactivated -= OnWindowDeactivated;
            }

            StopObserving();
        }

        // ----- Lista de juegos en tiempo real -----

        private void StartObserving()
        {
            mLobbySubscription ??= GameService.Instance.ObserveLobby(OnLobbyChanged);
        }

        private void StopObserving()
        {
            mLobbySubscription?.Dispose();
            mLobbySubscription = null;
        }

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            StartObserving();
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            StopObserving();
        }

        private void OnLobbyChanged(List<OnlineGame> games)
        {
            GamesList.ItemsSource = games;
            Refresher.IsRefreshing = false;
        }

        // Deslizar hacia abajo vuelve a leer la lista
        private async void OnRefreshing(object? sender, EventArgs e)
        {
            StopObserving();
            StartObserving();

            // Si no hay conexión no llega ninguna lista nueva; ocultar el indicador de todos modos
            await Task.Delay(1500);
            Refresher.IsRefreshing = false;
        }

        // ----- Crear y unirse -----

        private async void OnCreateClicked(object? sender, EventArgs e)
        {
            if (mBusy)
                return;

            mBusy = true;
            CreateButton.IsEnabled = false;
            try
            {
                string gameId = await GameService.Instance.CreateGameAsync(PlayerProfile.ToPlayerInfo());
                await Navigation.PushAsync(new GamePage(gameId, TicTacToeGame.PLAYER_X));
            }
            catch (Exception)
            {
                Toast.Show(this, "Could not create the game. Check your connection.");
            }
            finally
            {
                mBusy = false;
                CreateButton.IsEnabled = true;
            }
        }

        private async void OnJoinClicked(object? sender, EventArgs e)
        {
            if (mBusy || (sender as BindableObject)?.BindingContext is not OnlineGame game)
                return;

            mBusy = true;
            try
            {
                var result = await GameService.Instance.JoinGameAsync(game.Id, PlayerProfile.ToPlayerInfo());
                if (result == JoinResult.Taken)
                {
                    Toast.Show(this, "That game was just taken by another player.");
                    StopObserving();
                    StartObserving(); // actualizar la lista
                    return;
                }

                await Navigation.PushAsync(new GamePage(game.Id, TicTacToeGame.PLAYER_O));
            }
            catch (Exception)
            {
                Toast.Show(this, "Could not join the game. Check your connection.");
            }
            finally
            {
                mBusy = false;
            }
        }

        // ----- Nombre del jugador -----

        private void ShowPlayerName()
        {
            PlayerLabel.Text = PlayerProfile.HasName ? $"Playing as {PlayerProfile.Name}" : "";
        }

        private async Task AskNameAsync()
        {
            string? name = await DisplayPromptAsync("Your name", "Other players will see this name:",
                "OK", "Cancel", "Name", PlayerProfile.MaxNameLength, Keyboard.Text, PlayerProfile.Name);

            name = name?.Trim();
            if (!string.IsNullOrEmpty(name))
                PlayerProfile.Name = name;
            else if (!PlayerProfile.HasName)
                PlayerProfile.Name = "Player" + Random.Shared.Next(100, 1000); // sin nombre no se puede jugar

            ShowPlayerName();
        }

        // ----- Menú de opciones -----

        private async void OnChangeNameClicked(object? sender, EventArgs e)
        {
            await AskNameAsync();
        }

        private void OnAboutClicked(object? sender, EventArgs e)
        {
            AboutDialog.IsVisible = true;
        }
    }
}
