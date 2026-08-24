using UnityEngine;

namespace MetalRaptors
{
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        public const int Layer = 12;

        const float MaxLife = 6f;
        const float HitRadius = 3f;

        public static readonly Color RoundColor = new Color(0.85f, 0.62f, 0.30f);

        const float Mass = 0.01f;

        static readonly RaycastHit[] Sweep = new RaycastHit[8];

        float _damage;
        Camera _cam;
        bool _wasOnScreen;
        bool _spent;
        bool _fromEnemy;
        float _age;
        Rigidbody _rb;
        Collider _ignore;

        public static GameObject BuildTemplate(Color color)
        {
            var template = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder,
                Vector3.zero, new Vector3(2.4f, 3.5f, 2.4f),
                color, emissive: true);

            template.layer = Layer;
            Physics.IgnoreLayerCollision(Layer, PlaneFactory.PlaneLayer, true);
            Physics.IgnoreLayerCollision(Layer, Layer, true);

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

        public void Launch(Vector3 direction, float speed, float damage, Collider ignore,
            bool fromEnemy)
        {
            _damage = damage;
            _cam = Camera.main;
            _ignore = ignore;
            _fromEnemy = fromEnemy;

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.mass = Mass;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearVelocity = direction * speed;
        }

        void FixedUpdate()
        {
            if (_spent || _rb == null) return;

            Vector3 step = _rb.linearVelocity * Time.fixedDeltaTime;
            float reach = step.magnitude;
            if (reach < 0.0001f) return;

            int count = Physics.SphereCastNonAlloc(_rb.position, HitRadius, step / reach, Sweep,
                reach, 1 << PlaneFactory.PlaneLayer, QueryTriggerInteraction.Ignore);

            IDamageable nearest = null;
            float best = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = Sweep[i].collider;
                if (col == null || col == _ignore || Sweep[i].distance >= best) continue;

                var plane = col.GetComponentInParent<IDamageable>();
                if (!Hostile(plane)) continue;

                best = Sweep[i].distance;
                nearest = plane;
            }

            if (nearest != null) Hit(nearest);
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
            if (!_spent) Hit(null);
        }

        bool Hostile(IDamageable target)
        {
            return target != null && _fromEnemy != (target is EnemyController);
        }

        void Hit(IDamageable target)
        {
            _spent = true;
            target?.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}
