using UnityEngine;

namespace MetalRaptors
{
    public static class PlaneLoadout
    {
        public static PlayerConfig Build(PlayerConfig template, PlaneModelConfig plane)
        {
            PlayerConfig config = template != null
                ? Object.Instantiate(template)
                : ScriptableObject.CreateInstance<PlayerConfig>();

            config.name = plane != null ? $"PlayerConfig ({plane.resourceName})" : "PlayerConfig";

            PlaneStats stats = plane != null ? plane.stats : null;
            if (stats == null) return config;

            config.mass = stats.mass;
            config.rotationSpeed = stats.rotationSpeed;
            config.health = stats.health;
            config.damage = stats.damage;

            if (config.maxSpeedMultiplier > 0f)
                config.flySpeed = stats.maxSpeed / config.maxSpeedMultiplier;

            if (stats.fireRate > 0f)
                config.fireRate = 1f / stats.fireRate;

            return config;
        }
    }
}
