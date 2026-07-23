namespace MetalRaptors
{
    /// <summary>
    /// Central list of scene names so navigation never relies on magic strings.
    /// These MUST match the scene file names in Assets/Scenes and the entries
    /// registered in File &gt; Build Profiles &gt; Scene List.
    /// </summary>
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Level1 = "Level1";
        public const string Level2 = "Level2";
        public const string CampaignLevel1 = "CampaignLevel1";
        public const string Battlefield = "Battlefield";
        public const string Garage = "Garage";
    }
}
