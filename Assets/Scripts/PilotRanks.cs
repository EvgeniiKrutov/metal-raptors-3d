using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public readonly struct PilotRank
    {
        public readonly string Name;
        public readonly int Points;

        public PilotRank(string name, int points)
        {
            Name = name;
            Points = points;
        }
    }

    public static class PilotRanks
    {
        const string BadgeFolder = "ui/ranks/rank_";

        public const int EindeckerPoints = 5;
        public const int AlbatrosPoints = 10;
        public const int FokkerPoints = 15;
        public const int TruckPoints = 5;
        public const int TankPoints = 10;
        public const int AaGunPoints = 15;

        public static readonly PilotRank[] All =
        {
            new PilotRank("Cadet", 0),
            new PilotRank("Airman", 150),
            new PilotRank("Sergeant", 400),
            new PilotRank("Flight Sergeant", 800),
            new PilotRank("Pilot Officer", 1300),
            new PilotRank("Flying Officer", 2000),
            new PilotRank("Flight Lieutenant", 2900),
            new PilotRank("Squadron Leader", 4000),
            new PilotRank("Wing Commander", 5300),
            new PilotRank("Ace", 6800),
            new PilotRank("Raptor", 8500),
            new PilotRank("Metal Raptor", 10500),
        };

        static readonly Dictionary<int, Sprite> Badges = new Dictionary<int, Sprite>();

        public static int Last => All.Length - 1;

        public static int IndexFor(int points)
        {
            int index = 0;
            for (int i = 1; i < All.Length; i++)
                if (points >= All[i].Points) index = i;
            return index;
        }

        public static float Progress(int points, int index)
        {
            if (index >= Last) return 1f;

            int from = All[index].Points;
            int to = All[index + 1].Points;
            return Mathf.Clamp01((points - from) / (float)(to - from));
        }

        public static int PointsFor(PlaneModelConfig plane)
        {
            if (plane == PlaneModels.Eindecker) return EindeckerPoints;
            if (plane == PlaneModels.Albatros) return AlbatrosPoints;
            if (plane == PlaneModels.Fokker) return FokkerPoints;
            return 0;
        }

        public static int PointsFor(GroundVehicle vehicle) =>
            vehicle is EnemyTank ? TankPoints : TruckPoints;

        public static Sprite Badge(int index)
        {
            index = Mathf.Clamp(index, 0, Last);
            if (Badges.TryGetValue(index, out Sprite cached)) return cached;

            Sprite sprite = Load(BadgeFolder + (index + 1).ToString("00"));
            Badges[index] = sprite;
            return sprite;
        }

        static Sprite Load(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;

            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
