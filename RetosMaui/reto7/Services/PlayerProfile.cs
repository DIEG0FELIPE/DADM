namespace reto7
{
    /// <summary>Identidad del jugador en este dispositivo: un id único y el nombre que ven los demás.</summary>
    public static class PlayerProfile
    {
        public const int MaxNameLength = 20;

        private const string PrefId = "playerId";
        private const string PrefName = "playerName";

        public static string Id
        {
            get
            {
                string id = Preferences.Default.Get(PrefId, "");
                if (id.Length == 0)
                {
                    id = Guid.NewGuid().ToString("N");
                    Preferences.Default.Set(PrefId, id);
                }
                return id;
            }
        }

        public static string Name
        {
            get => Preferences.Default.Get(PrefName, "");
            set => Preferences.Default.Set(PrefName, value.Trim());
        }

        public static bool HasName => Name.Length > 0;

        public static PlayerInfo ToPlayerInfo()
        {
            return new PlayerInfo { Id = Id, Name = Name };
        }
    }
}
