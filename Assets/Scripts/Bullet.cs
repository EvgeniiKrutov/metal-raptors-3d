using UnityEngine;

namespace MetalRaptors
{
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        const float MaxLife = 6f;

        public static readonly Color RoundColor = new Color(0.85f, 0.62f, 0.30f);

        const float Mass = 0.01f;

        float _damage;
        Camera _cam;
        bool _wasOnScreen;
        float _age;

        public static GameObject BuildTemplate(Color color)
        {
            var template = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(2.4f, 3.5f, 2.4f),
                color, emissive: true);

            var rend = template.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                var mat = rend.sharedMaterial;
                mat.SetFloat("_Metallic", 0.9f);
                mat.SetFloat("_Smoothness", 0.55f);
                mat.SetColor("_EmissionColor", color * 0.75f);
            }

            template.AddComponent<Bullet>();
            template.SetActive(false);
            template.name = "BulletTemplate";
            return template;
        }

        public void Launch(Vector3 direction, float speed, float damage, Collider ignore)
        {
            _damage = damage;
            _cam = Camera.main;

            var rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = Mass;
            rb.constraints = RigidbodyConstraints.FreezePositionZ;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = direction * speed;

            var col = GetComponent<Collider>();
            if (col != null && ignore != null) Physics.IgnoreCollision(col, ignore);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age > MaxLife) { Destroy(gameObject); return; }

            if (_cam == null) return;

            Vector3 vp = _cam.WorldToViewportPoint(transform.position);
            bool onScreen = vp.z > 0f
                         && vp.x > -0.05f && vp.x < 1.05f
                         && vp.y > -0.05f && vp.y < 1.05f;

            if (onScreen) _wasOnScreen = true;
            else if (_wasOnScreen) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            collision.gameObject.GetComponentInParent<IDamageable>()?.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}
