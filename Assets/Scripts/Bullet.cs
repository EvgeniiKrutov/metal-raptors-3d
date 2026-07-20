using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// One machine-gun round fired by <see cref="PlaneShooter"/>: flies straight at constant
    /// speed in the XY play-plane and is destroyed by the first solid thing it touches, dealing
    /// its damage if the target is an <see cref="IDamageable"/> (enemies, in the future — the
    /// ground just stops the round). Rounds that hit nothing are removed once they leave the
    /// camera view, so misses never pile up. Triggers (the goal sphere) are ignored: only real
    /// collisions stop a bullet.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        // Safety net: no round outlives this, even one that never re-enters the camera view.
        const float MaxLife = 6f;

        // Both sides fire the same polished-brass round.
        public static readonly Color RoundColor = new Color(0.85f, 0.62f, 0.30f);

        // Rounds carry almost no mass. A round only needs to register the hit and deal its
        // damage — it never has to physically push anything (it self-destructs on contact) —
        // so keeping its momentum near zero means a hit no longer kicks the plane it strikes
        // into a visible jump/shake. A full-mass round at bulletSpeed transfers a huge impulse
        // into the (mass 2.5) plane, which the plane's scripted flight can't fully cancel.
        const float Mass = 0.01f;

        float _damage;
        Camera _cam;
        bool _wasOnScreen; // becomes true once the round is seen, so a miss is culled only after it leaves
        float _age;

        /// <summary>
        /// Builds the one inactive round a gun clones every shot, so all of its bullets share a
        /// single material instead of creating one per shot. A stubby brass slug — thick enough
        /// to read at the camera's ~420 m distance, with only a faint glow so it looks like a
        /// warm metal round catching the light, not a laser bolt. The <paramref name="color"/>
        /// sets the brass tone; both the player's and the enemies' guns fire the same polished
        /// brass round (see <see cref="RoundColor"/>).
        /// </summary>
        public static GameObject BuildTemplate(Color color)
        {
            var template = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(2.4f, 3.5f, 2.4f),
                color, emissive: true);

            // Dress the shared material as brass: metallic and a little glossy so the scene light
            // glints off it, and a much dimmer emission than the old tracers (was color * 2) so it
            // reads as metal rather than glowing harshly.
            var rend = template.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                var mat = rend.sharedMaterial;
                mat.SetFloat("_Metallic", 0.9f);
                mat.SetFloat("_Smoothness", 0.55f);
                mat.SetColor("_EmissionColor", color * 0.75f);
            }

            template.AddComponent<Bullet>(); // RequireComponent pulls the Rigidbody in with it
            template.SetActive(false);
            template.name = "BulletTemplate";
            return template;
        }

        /// <param name="ignore">
        /// The shooter's own collider; ignored so a fresh round can never clip the plane that
        /// fired it (e.g. while turning hard into the muzzle's path).
        /// </param>
        public void Launch(Vector3 direction, float speed, float damage, Collider ignore)
        {
            _damage = damage;
            _cam = Camera.main;

            var rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            // Near-massless so the round deals damage without physically shoving the plane it
            // hits (see the Mass note above): a hit no longer kicks the target into a shake.
            rb.mass = Mass;
            rb.constraints = RigidbodyConstraints.FreezePositionZ;
            // At bulletSpeed the round covers several metres per physics step; continuous
            // detection keeps it from tunnelling through the ground slab or thin terrain.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = direction * speed;

            var col = GetComponent<Collider>();
            if (col != null && ignore != null) Physics.IgnoreCollision(col, ignore);
        }

        void Update()
        {
            // Hard cap so a stray round can never live forever.
            _age += Time.deltaTime;
            if (_age > MaxLife) { Destroy(gameObject); return; }

            if (_cam == null) return;

            // On screen, with a small margin so a round visibly crosses the edge before anything
            // happens. Enemy rounds are often fired from just off-camera and only fly into view on
            // their way to the player, so a round is culled for leaving the view *only once it has
            // actually been seen* — otherwise an incoming tracer would be destroyed before it ever
            // rendered (which is exactly why enemy fire used to hit from nowhere).
            Vector3 vp = _cam.WorldToViewportPoint(transform.position);
            bool onScreen = vp.z > 0f
                         && vp.x > -0.05f && vp.x < 1.05f
                         && vp.y > -0.05f && vp.y < 1.05f;

            if (onScreen) _wasOnScreen = true;
            else if (_wasOnScreen) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            // Future enemies implement IDamageable (possibly on a parent of the collider node);
            // anything else — the ground included — just soaks the round.
            collision.gameObject.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}
