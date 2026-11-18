using UnityEngine;

namespace MetalRaptors
{
    public static class FlightPace
    {
        public const float Fast = 1.15f;

        public static void Apply(PlayerConfig config, float pace)
        {
            if (config == null || Mathf.Approximately(pace, 1f)) return;

            config.flySpeed *= pace;
            config.bulletSpeed *= pace;
            config.rotationSpeed *= pace;
            config.energyGravity *= pace * pace;
            config.diveAcceleration *= pace * pace;
            config.energyDrag *= pace;
            config.speedDrag *= pace;
        }

        public static void Apply(EnemyConfig config, float pace)
        {
            if (config == null || Mathf.Approximately(pace, 1f)) return;

            config.flySpeed *= pace;
            config.bulletSpeed *= pace;
            config.rotationSpeed *= pace;
            config.diveAcceleration *= pace * pace;
            config.speedDrag *= pace;
        }

        public static PlayerConfig Copy(PlayerConfig template, float pace)
        {
            PlayerConfig config = template != null
                ? Object.Instantiate(template)
                : ScriptableObject.CreateInstance<PlayerConfig>();
            Apply(config, pace);
            return config;
        }
    }
}
