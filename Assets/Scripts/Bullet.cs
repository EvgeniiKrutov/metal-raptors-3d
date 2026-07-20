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
        float _damage;
        Camera _cam;

        /// <summary>
        /// Builds the one inactive round a gun clones every shot, so all of its bullets share a
        /// single material instead of creating one per shot. An 8 m emissive cylinder —
        /// tracer-sized, so it still reads at the camera's ~420 m distance. The player's guns
        /// use yellow; enemy guns use red so both sides' fire is tellable apart.
        /// </summary>
        public static GameObject BuildTemplate(Color color)
        {
            var template = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(1.2f, 4f, 1.2f),
                color, emissive: true);
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
            // Out of the camera view, with a small margin so the round visibly leaves the
            // screen before it disappears.
            if (_cam == null) return;
            Vector3 vp = _cam.WorldToViewportPoint(transform.position);
            if (vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f)
                Destroy(gameObject);
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
