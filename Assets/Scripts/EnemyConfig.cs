using UnityEngine;

namespace MetalRaptors
{
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Metal Raptors/Enemy Config")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Stats (fighter.json stats)")]
        [Tooltip("Hit points (sibling: 100). Player fire subtracts PlayerConfig.damage per hit.")]
        public float health = 100f;

        [Tooltip("Damage one enemy bullet deals to the player (sibling: 10, scaled to 0.6).")]
        public float damage = 6f;

        [Tooltip("Seconds between two enemy bullets while firing (sibling fires 5/s = 0.2 s; "
                 + "eased off to 4/s here).")]
        public float fireRate = 0.25f;

        [Tooltip("Enemy bullet speed in metres/second (matches the player's rounds).")]
        public float bulletSpeed = 400f;

        [Header("Flight (fighter.json flight)")]
        [Tooltip("Constant flight speed in m/s. Below the player's 180 cruise so a player " +
                 "who commits to running away can still break contact.")]
        public float flySpeed = 150f;

        [Tooltip("Maximum turn rate in degrees/second.")]
        public float rotationSpeed = 105f;

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
        [Tooltip("Seconds spent chasing and shooting before breaking away.")]
        public float attackDuration = 3.5f;

        [Tooltip("Seconds spent flying away before attacking again.")]
        public float flyDuration = 1.6f;

        [Tooltip("Metres above the player the break-away aims for. Relative to the player " +
                 "rather than to the world so the fighter perches just above the fight " +
                 "instead of climbing out of it.")]
        public float flyPerchHeight = 90f;

        [Tooltip("Horizontal weave amplitude in metres while breaking away (sibling: 120 px).")]
        public float weaveAmplitude = 45f;

        [Tooltip("Weave frequency in Hz while breaking away (sibling: 0.4).")]
        public float weaveHz = 0.4f;

        [Header("Evasion")]
        [Tooltip("Seconds one break-and-jink evade lasts.")]
        public float evadeDuration = 1.3f;

        [Tooltip("Seconds of attacking that must pass after an evade before the fighter " +
                 "will break off again. Stops a steady stream of hits from pinning it in " +
                 "evasion for the whole fight.")]
        public float evadeCooldown = 3f;

        [Tooltip("How far off the line away from the player the break turn goes, in degrees. " +
                 "0 would run straight down the attacker's gunsight; ~65° crosses it.")]
        public float evadeBreakAngle = 65f;

        [Tooltip("Random heading jitter amplitude in degrees while breaking away.")]
        public float jitterAmplitude = 45f;

        [Tooltip("How many times per second the jitter heading re-rolls.")]
        public float jitterHz = 9f;

        [Header("Threat reaction (evading player fire)")]
        [Tooltip("The player has to be inside this many metres to count as a threat.")]
        public float threatRange = 420f;

        [Tooltip("How close the player's nose has to point at the fighter, in degrees, " +
                 "for their guns to count as tracking it.")]
        public float threatCone = 18f;

        [Tooltip("How far behind the fighter the player has to be, in degrees off its nose, " +
                 "before it breaks. Above 90° only a genuine tail chase provokes an evade — " +
                 "a head-on merge is fought, not dodged.")]
        public float threatTailAngle = 95f;

        [Header("Body (legacy)")]
        [Tooltip("Unused: the enemy is now the Fokker Dr.1 model, sized to the player's plane. " +
                 "Kept only so the existing asset still deserializes cleanly.")]
        public float cubeScale = 30f;

        [Tooltip("Unused: the enemy plane uses the model's own materials, not a flat colour.")]
        public Color color = new Color(0.62f, 0.14f, 0.12f);
    }
}
