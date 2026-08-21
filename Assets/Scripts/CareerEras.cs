namespace MetalRaptors
{
    public readonly struct CareerEra
    {
        public readonly string Title;
        public readonly string Description;
        public readonly EraEmblem Emblem;
        public readonly bool Unlocked;

        public CareerEra(string title, string description, EraEmblem emblem, bool unlocked)
        {
            Title = title;
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
            new CareerEra("CHAPTER 1", Placeholder, EraEmblem.Biplane, true),
            new CareerEra("CHAPTER 2", Placeholder, EraEmblem.Fighter, false),
            new CareerEra("CHAPTER 3", Placeholder, EraEmblem.Jet, false),
            new CareerEra("FINAL CHAPTER", Placeholder, EraEmblem.Delta, false),
        };
    }
}
