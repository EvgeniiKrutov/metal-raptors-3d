using UnityEngine;

namespace MetalRaptors
{
    public enum AltitudeBand
    {
        Deck,
        Mid,
        High,
    }

    public static class AltitudeBands
    {
        public const float DeckTop = 0.15f;
        public const float MidTop = 0.55f;

        public static float Floor(AltitudeBand band, float groundY, float worldTop)
        {
            switch (band)
            {
                case AltitudeBand.Mid: return At(groundY, worldTop, DeckTop);
                case AltitudeBand.High: return At(groundY, worldTop, MidTop);
                default: return groundY;
            }
        }

        public static float Ceiling(AltitudeBand band, float groundY, float worldTop)
        {
            switch (band)
            {
                case AltitudeBand.Deck: return At(groundY, worldTop, DeckTop);
                case AltitudeBand.Mid: return At(groundY, worldTop, MidTop);
                default: return worldTop;
            }
        }

        static float At(float groundY, float worldTop, float fraction) =>
            groundY + Mathf.Max(0f, worldTop - groundY) * fraction;
    }
}
