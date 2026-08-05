using System;
using UnityEngine;

namespace MetalRaptors
{
    public class PlaneStats
    {
        public float maxSpeed;
        public float rotationSpeed;
        public float mass;
        public float fireRate;
        public float damage;
        public float health;
    }

    public class PlaneType
    {
        public string label;
        public Color color;
    }

    public static class PlaneTypes
    {
        public static readonly PlaneType Fighter = new PlaneType
        {
            label = "fighter",
            color = Parse("#9E4A3C"),
        };

        public static readonly PlaneType Bomber = new PlaneType
        {
            label = "bomber",
            color = Parse("#4E6E8A"),
        };

        public static readonly PlaneType Recon = new PlaneType
        {
            label = "recon",
            color = Parse("#6E7A4A"),
        };

        static Color Parse(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }

    public class PlaneStatBar
    {
        public string label;
        public float ceiling;
        public Func<PlaneStats, float> read;
    }

    public static class PlaneStatBars
    {
        public static readonly PlaneStatBar[] All =
        {
            new PlaneStatBar { label = "max speed",      ceiling = 280f, read = s => s.maxSpeed },
            new PlaneStatBar { label = "rotation speed", ceiling = 260f, read = s => s.rotationSpeed },
            new PlaneStatBar { label = "mass",           ceiling = 4f,   read = s => s.mass },
            new PlaneStatBar { label = "fire rate",      ceiling = 8f,   read = s => s.fireRate },
            new PlaneStatBar { label = "damage",         ceiling = 15f,  read = s => s.damage },
            new PlaneStatBar { label = "health",         ceiling = 150f, read = s => s.health },
        };
    }
}
