using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Tunable flight parameters for the player cube, stored as a standalone asset
    /// (Assets/Resources/CubeConfig.asset) so the numbers live in a separate file and
    /// can be edited without touching code. Values are ported from the metal-raptors
    /// sibling repo (player.json + physics.json), with <see cref="flySpeed"/> rescaled
    /// for this 1000 m world.
    /// </summary>
    [CreateAssetMenu(fileName = "CubeConfig", menuName = "Metal Raptors/Cube Config")]
    public class CubeConfig : ScriptableObject
    {
        [Tooltip("Cube mass. Higher mass = heavier, more sluggish turns (sibling: 2.5).")]
        public float mass = 2.5f;

        [Tooltip("Maximum turn rate in degrees/second (sibling turnSpeed: 180).")]
        public float rotationSpeed = 180f;

        [Tooltip("Constant flight speed in metres/second. The cube never stops.")]
        public float flySpeed = 120f;

        [Tooltip("How quickly the turn rate eases toward the target (sibling: 5.0). " +
                 "Used together with mass as turnResponsiveness / mass.")]
        public float turnResponsiveness = 5f;

        [Tooltip("Hit points of the plane (sibling: 100). Future enemy fire subtracts from this.")]
        public float health = 100f;

        [Tooltip("Damage one bullet deals to whatever it hits (sibling: 10). Applied to enemies " +
                 "(future IDamageable targets) when a bullet collides with them.")]
        public float damage = 10f;

        [Tooltip("Time in seconds between two bullets while the trigger is held " +
                 "(sibling fires 5 shots/s = 0.2 s).")]
        public float fireRate = 0.2f;

        [Tooltip("Bullet speed in metres/second. Keep it well above flySpeed so rounds " +
                 "visibly pull away from the plane.")]
        public float bulletSpeed = 400f;
    }
}
