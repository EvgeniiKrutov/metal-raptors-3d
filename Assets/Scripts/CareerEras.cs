namespace MetalRaptors
{
    public readonly struct CareerEra
    {
        public readonly string Title;
        public readonly string Years;
        public readonly string Description;
        public readonly EraEmblem Emblem;
        public readonly bool Unlocked;

        public CareerEra(string title, string years, string description, EraEmblem emblem, bool unlocked)
        {
            Title = title;
            Years = years;
            Description = description;
            Emblem = emblem;
            Unlocked = unlocked;
        }
    }

    public static class CareerEras
    {
        const string Placeholder =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor " +
            "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
            "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";

        public static readonly CareerEra[] All =
        {
            new CareerEra("WORLD WAR 1", "1914 – 1918", Placeholder, EraEmblem.Biplane, true),
            new CareerEra("WORLD WAR 2", "1939 – 1945", Placeholder, EraEmblem.Fighter, false),
            new CareerEra("COLD WAR", "1947 – 1991", Placeholder, EraEmblem.Jet, false),
            new CareerEra("MODERN TIMES", "1991 – present", Placeholder, EraEmblem.Delta, false),
        };
    }
}
