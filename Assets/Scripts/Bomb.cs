using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    [RequireComponent(typeof(Rigidbody))]
    public class Bomb : MonoBehaviour
    {
        public const int Layer = 10;
        public const float ShakeRadii = 3f;

        const float Length = 16f, Girth = 6f;
        const float Mass = 8f;
        const float Gravity = 200f;
        const float AlignResponse = 4f;
        const float MaxLife = 12f;
        const float FloorY = -300f;
        const float GroundSweepPad = 4f;
        const float ProbeTop = 1200f;
        const float ProbeDepth = 1600f;
        const int GroundMask = 1 << ProceduralTerrain.GroundLayer;

        static readonly Color BodyColor = new Color(0.19f, 0.20f, 0.22f);
        static readonly List<IDamageable> Struck = new List<IDamageable>();

        float _damage;
        float _radius;
        float _age;
        float _angle;
        bool _spent;

        Rigidbody _rb;
        Camera _cam;
        Action<Vector3, float> _onDetonated;

        public static GameObject BuildTemplate()
        {
            var template = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, Vector3.zero,
                new Vector3(Length, Girth, Girth), BodyColor);

            var rend = template.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                var mat = rend.sharedMaterial;
                mat.SetFloat("_Metallic", 0.7f);
                mat.SetFloat("_Smoothness", 0.35f);
            }

            template.layer = Layer;
            template.AddComponent<Bomb>();
            template.SetActive(false);
            template.name = "BombTemplate";
            return template;
        }

        public void Launch(Vector3 velocity, float damage, float radius, Collider ignore,
            Action<Vector3, float> onDetonated)
        {
            _damage = damage;
            _radius = Mathf.Max(1f, radius);
            _onDetonated = onDetonated;
            _cam = Camera.main;
            _angle = velocity.x < 0f ? 180f : 0f;

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.mass = Mass;
            _rb.constraints = RigidbodyConstraints.FreezePositionZ
                            | RigidbodyConstraints.FreezeRotationX
                            | RigidbodyConstraints.FreezeRotationY;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearVelocity = velocity;

            var col = GetComponent<Collider>();
            if (col != null && ignore != null) Physics.IgnoreCollision(col, ignore);

            ApplyRotation();
        }

        void FixedUpdate()
        {
            if (_spent || _rb == null) return;

            float dt = Time.fixedDeltaTime;

            _age += dt;
            if (_age > MaxLife || _rb.position.y < FloorY) { Destroy(gameObject); return; }

            Vector3 v = _rb.linearVelocity;
            v.y -= Gravity * dt;
            _rb.linearVelocity = v;

            if (SweepGround(_rb.position, v * dt, out Vector3 ground))
            {
                Detonate(ground, airburst: false);
                return;
            }

            if (v.sqrMagnitude < 0.01f) return;

            float target = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            _angle = Mathf.LerpAngle(_angle, target, 1f - Mathf.Exp(-AlignResponse * dt));
            ApplyRotation();
        }

        void ApplyRotation()
        {
            var rotation = Quaternion.Euler(0f, 0f, _angle);
            if (_rb != null) _rb.rotation = rotation;
            else transform.rotation = rotation;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (_spent) return;
            if (collision.gameObject.GetComponent<Bullet>() != null) return;

            Vector3 point = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;

            Detonate(point, collision.gameObject.GetComponentInParent<EnemyController>() != null);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_spent || other.gameObject.layer != BattlefieldProps.Layer) return;
            Detonate(transform.position, airburst: false);
        }

        void Detonate(Vector3 point, bool airburst)
        {
            _spent = true;
            Vector3 listener = _cam != null ? _cam.transform.position : point;

            if (!airburst && InWater(point))
            {
                WaterSplash.Spawn(new Vector3(point.x, SeaSurface.Level, point.z), _radius, listener);
            }
            else
            {
                Explosion.Spawn(point, _radius);
                ApplyBlast(point);

                if (!airburst)
                {
                    GroundBlast.Spawn(point, _radius, listener);
                    if (Battlefield.Current != null)
                        Battlefield.Current.KillPeopleWithin(point, _radius);
                }
            }

            _onDetonated?.Invoke(point, _radius);
            Destroy(gameObject);
        }

        bool SweepGround(Vector3 from, Vector3 step, out Vector3 point)
        {
            point = from;

            float distance = step.magnitude + GroundSweepPad;
            if (distance > GroundSweepPad
                && Physics.Raycast(from, step.normalized, out RaycastHit info, distance,
                    GroundMask, QueryTriggerInteraction.Ignore))
            {
                point = info.point;
                return true;
            }

            Vector3 next = from + step;
            if (!GroundHeight(next, out float deck) || next.y - Belly() > deck) return false;

            point = new Vector3(next.x, deck, next.z);
            return true;
        }

        float Belly()
        {
            float radians = _angle * Mathf.Deg2Rad;
            return Mathf.Abs(Mathf.Sin(radians)) * Length * 0.5f
                 + Mathf.Abs(Mathf.Cos(radians)) * Girth * 0.5f;
        }

        static bool GroundHeight(Vector3 at, out float y)
        {
            if (Physics.Raycast(new Vector3(at.x, ProbeTop, at.z), Vector3.down,
                    out RaycastHit info, ProbeDepth, GroundMask, QueryTriggerInteraction.Ignore))
            {
                y = info.point.y;
                return true;
            }

            y = 0f;
            return false;
        }

        static bool InWater(Vector3 point)
        {
            return SeaSurface.Current != null
                && point.z >= SeaSurface.NearEdge
                && point.y <= SeaSurface.Level;
        }

        void ApplyBlast(Vector3 centre)
        {
            var hits = Physics.OverlapSphere(centre, _radius, ~0, QueryTriggerInteraction.Ignore);

            Struck.Clear();
            foreach (var col in hits)
            {
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || Struck.Contains(target)) continue;
                Struck.Add(target);

                var body = target as Component;
                Transform tr = body != null ? body.transform : col.transform;

                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(centre, tr.position) / _radius);
                if (falloff <= 0f) continue;

                target.TakeDamage(_damage * falloff);
            }
            Struck.Clear();
        }
    }
}
