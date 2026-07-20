using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Tunable stats and AI parameters for the enemy fighter, stored as a standalone asset
    /// (Assets/Resources/EnemyConfig.asset) like <see cref="CubeConfig"/>. Values are ported
    /// from the metal-raptors sibling repo (enemies/fighter.json), rescaled to this world the
    /// same way the player's were: speeds against the live CubeConfig.asset (player 500 px/s
    /// -> 180 m/s), distances against the sibling's 3240 px world height -> 700 m here, with
    /// the ground margins widened to cover this fighter's larger turn radius.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Metal Raptors/Enemy Config")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Stats (fighter.json stats)")]
        [Tooltip("Hit points (sibling: 100). Player fire subtracts CubeConfig.damage per hit.")]
        public float health = 100f;

        [Tooltip("Damage one enemy bullet deals to the player (sibling: 10).")]
        public float damage = 10f;

        [Tooltip("Seconds between two enemy bullets while firing (sibling fires 5/s = 0.2 s).")]
        public float fireRate = 0.2f;

        [Tooltip("Enemy bullet speed in metres/second (matches the player's rounds).")]
        public float bulletSpeed = 400f;

        [Header("Flight (fighter.json flight)")]
        [Tooltip("Constant flight speed in m/s (sibling 350 px/s, player-relative -> 130).")]
        public float flySpeed = 130f;

        [Tooltip("Maximum turn rate in degrees/second (sibling 150, player-relative -> 100).")]
        public float rotationSpeed = 100f;

        [Tooltip("Mass; lighter than the player so turns bite faster (sibling: 1.5).")]
        public float mass = 1.5f;

        [Tooltip("Turn-rate easing, used as turnResponsiveness / mass (sibling physics: 5).")]
        public float turnResponsiveness = 5f;

        [Header("Targeting (ai.targeting)")]
        [Tooltip("Fires only when aimed within this many degrees of the intercept point (sibling: 14).")]
        public float fireAngleThreshold = 14f;

        [Tooltip("How strongly the aim leads the player's velocity; 1 = full intercept (sibling: 1).")]
        public float leadFactor = 1f;

        [Tooltip("Maximum firing distance in metres (sibling: 1400 px).")]
        public float maxFireRange = 500f;

        [Header("Ground avoidance (ai.groundAvoidance)")]
        [Tooltip("Below this height above the ground the AI aborts everything and pulls up.")]
        public float minAltitudeMargin = 160f;

        [Tooltip("The pull-up ends once the enemy is at least this high above the ground.")]
        public float safeAltitudeMargin = 260f;

        [Header("Attack / fly-away cycle (ai.attack, ai.fly)")]
        [Tooltip("Seconds spent chasing and shooting before breaking away (sibling: 3 s).")]
        public float attackDuration = 3f;

        [Tooltip("Seconds spent flying away before attacking again (sibling: 2 s).")]
        public float flyDuration = 2f;

        [Tooltip("Break-away climb altitude as a fraction of the world height (sibling " +
                 "targetYFactor 0.2 from the top = 0.8 of the height from the ground).")]
        public float flyAltitudeFactor = 0.8f;

        [Tooltip("Horizontal weave amplitude in metres while breaking away (sibling: 120 px).")]
        public float weaveAmplitude = 45f;

        [Tooltip("Weave frequency in Hz while breaking away (sibling: 0.4).")]
        public float weaveHz = 0.4f;

        [Header("Evasion when hit (ai.evasion)")]
        [Tooltip("Seconds the jitter phase of an evade lasts (sibling: 2 s).")]
        public float evadeDuration = 2f;

        [Tooltip("Random heading jitter amplitude in degrees while fleeing (sibling: 1.1 rad = 63°).")]
        public float jitterAmplitude = 63f;

        [Tooltip("How many times per second the jitter heading re-rolls (sibling: 16).")]
        public float jitterHz = 16f;

        [Header("Body")]
        [Tooltip("Edge length of the enemy cube in metres (matches the old player cube).")]
        public float cubeScale = 30f;

        [Tooltip("Colour of the enemy cube.")]
        public Color color = new Color(0.62f, 0.14f, 0.12f);
    }
}
