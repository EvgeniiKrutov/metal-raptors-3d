using UnityEngine;

namespace MetalRaptors
{
    [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Metal Raptors/Enemy Config")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Role")]
        [Tooltip("Which behaviour set this config drives (docs/enemies.md).")]
        public EnemyRole role = EnemyRole.Fighter;

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

        [Header("Engagement boost (both roles)")]
        [Tooltip("Beyond this distance a player flying away is chased with the catch-up speed.")]
        public float engageRange = 450f;

        [Tooltip("Catch-up speed as a multiple of the player's own speed; ignores flySpeed.")]
        public float engageFactor = 1.15f;

        [Tooltip("How quickly the catch-up speed eases in and out, per second.")]
        public float engageResponse = 2f;

        [Header("Scout: deck flying")]
        [Tooltip("Metres above the terrain contour the scout's corridor is capped at.")]
        public float deckCeilingMargin = 260f;

        [Tooltip("Seconds of the player staying out of reach before the scout climbs to mid.")]
        public float pressDelay = 5f;

        [Tooltip("Seconds the scout stays in the mid band before dropping back to the deck.")]
        public float pressDuration = 8f;

        [Header("Scout: turn bias")]
        [Tooltip("Turn rate when swinging the nose to the right, as a fraction of the left.")]
        public float turnBias = 0.65f;

        [Header("Scout: depth dodge")]
        [Tooltip("Health fraction at or below which the player's aim can trigger the dodge. 1 = always.")]
        public float dodgeHealthFraction = 1f;

        [Tooltip("Half-angle of the player's fire cone that triggers the dodge, degrees.")]
        public float dodgeAimCone = 22f;

        [Tooltip("How far down that cone the trigger reaches, metres.")]
        public float dodgeAimRange = 600f;

        [Tooltip("Metres slid away from the camera, out of the player's plane of fire.")]
        public float dodgeDepth = 120f;

        [Tooltip("Seconds spent rolling onto the wing, and rolling level again. Four per dodge.")]
        public float dodgeRoll = 0.35f;

        [Tooltip("Seconds sliding out, wing down.")]
        public float dodgeOut = 0.8f;

        [Tooltip("Seconds flown straight and level out there before it comes back.")]
        public float dodgeHold = 2.5f;

        [Tooltip("Seconds sliding back, wing down.")]
        public float dodgeBack = 0.8f;

        [Tooltip("Bank held through each slide, degrees. Negate to roll the other way.")]
        public float dodgeBank = 75f;

        [Tooltip("Seconds before another dodge, counted from the moment it returns.")]
        public float dodgeCooldown = 14f;

        [Header("Fighter: loop reversal")]
        [Tooltip("Heading change in degrees that becomes a loop instead of a normal turn.")]
        public float reversalAngle = 120f;

        [Tooltip("Seconds one wide 180 degree loop takes; speed is kept throughout.")]
        public float loopSeconds = 1.5f;

        [Header("Fighter: dive energy")]
        [Tooltip("Gravity pull along the flight path in m/s squared, as PlayerConfig.")]
        public float diveAcceleration = 90f;

        [Tooltip("Fraction of the speed above flySpeed shed per second.")]
        public float speedDrag = 0.9f;

        [Tooltip("Hard cap on speed as a multiple of flySpeed.")]
        public float maxSpeedMultiplier = 1.6f;

        [Tooltip("Speed floor while the diving pass is running, as a multiple of flySpeed. " +
                 "Capped by maxSpeedMultiplier; the dive-energy model may still push above it.")]
        public float diveSpeedMultiplier = 1.5f;

        [Header("Fighter: diving pass")]
        [Tooltip("Seconds before another diving pass, counted from the moment it ends.")]
        public float diveCooldown = 10f;

        [Tooltip("The player counts as low, and the pass is run, once they are within this many " +
                 "metres of the ground. Above it the fighter flies the ordinary attack cycle.")]
        public float diveTriggerHeight = 180f;

        [Tooltip("Maximum seconds spent climbing to the top corner; this is the telegraph.")]
        public float diveClimbSeconds = 3f;

        [Tooltip("Maximum seconds of the diagonal run before it pulls out.")]
        public float diveRunSeconds = 6f;

        [Header("Body (legacy)")]
        [Tooltip("Unused: the enemy is now the Albatros D.III model, sized to the player's plane. " +
                 "Kept only so the existing asset still deserializes cleanly.")]
        public float cubeScale = 30f;

        [Tooltip("Unused: the enemy plane uses the model's own materials, not a flat colour.")]
        public Color color = new Color(0.62f, 0.14f, 0.12f);
    }

    public static class EnemyConfigs
    {
        public const string ScoutAsset = "EnemyScoutConfig";
        public const string FighterAsset = "EnemyFighterConfig";

        public static EnemyConfig Load(EnemyRole role)
        {
            var asset = Resources.Load<EnemyConfig>(
                role == EnemyRole.Scout ? ScoutAsset : FighterAsset);

            EnemyConfig config = asset != null
                ? Object.Instantiate(asset)
                : ScriptableObject.CreateInstance<EnemyConfig>();

            config.role = role;
            return config;
        }

        public static void Scale(EnemyConfig config, float health, float rotation)
        {
            if (config == null) return;
            if (health > 0f) config.health = Mathf.Max(1f, config.health * health);
            if (rotation > 0f)
                config.rotationSpeed = Mathf.Max(1f, config.rotationSpeed * rotation);
        }

        public static EnemyConfig For(PlaneModelConfig plane, EnemyConfig scout,
            EnemyConfig fighter)
        {
            return plane != null && plane.enemyRole == EnemyRole.Scout ? scout : fighter;
        }

        public static void SpawnBand(EnemyConfig config, float groundY, float ceilingY,
            out float minY, out float maxY)
        {
            if (config.role == EnemyRole.Scout)
            {
                minY = groundY + config.safeAltitudeMargin;
                maxY = Mathf.Min(groundY + config.deckCeilingMargin,
                    AltitudeBands.Ceiling(AltitudeBand.Mid, groundY, ceilingY));
            }
            else
            {
                minY = Mathf.Max(AltitudeBands.Floor(AltitudeBand.High, groundY, ceilingY),
                    groundY + config.safeAltitudeMargin);
                maxY = AltitudeBands.Ceiling(AltitudeBand.High, groundY, ceilingY);
            }

            maxY = Mathf.Max(minY, maxY);
        }
    }
}
