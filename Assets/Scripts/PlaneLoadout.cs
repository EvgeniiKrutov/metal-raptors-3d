using UnityEngine;

namespace MetalRaptors
{
    public static class PlaneLoadout
    {
        public static PlayerConfig Build(PlayerConfig template, PlaneModelConfig plane, float pace = 1f)
        {
            PlayerConfig config = template != null
                ? Object.Instantiate(template)
                : ScriptableObject.CreateInstance<PlayerConfig>();

            config.name = plane != null ? $"PlayerConfig ({plane.resourceName})" : "PlayerConfig";

            PlaneStats stats = plane != null ? plane.stats : null;
            if (stats != null)
            {
                config.mass = stats.mass;
                config.rotationSpeed = stats.rotationSpeed;
                config.health = stats.health;
                config.damage = stats.damage;

                if (config.maxSpeedMultiplier > 0f)
                    config.flySpeed = stats.maxSpeed / config.maxSpeedMultiplier;

                if (stats.fireRate > 0f)
                    config.fireRate = 1f / stats.fireRate;
            }

            FlightPace.Apply(config, pace);
            return config;
        }
    }
}
