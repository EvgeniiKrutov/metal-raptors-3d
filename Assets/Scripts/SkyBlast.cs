using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class SkyBlast : MonoBehaviour
    {
        const int PuffMin = 4, PuffMax = 6;
        const float Scatter = 0.45f;
        const float SizeMin = 0.55f, SizeMax = 0.85f;
        const float GrowFrom = 0.25f;
        const float SpinMin = 20f, SpinMax = 70f;
        const float Pulse = 0.07f;
        const float PulseRate = 14f;
        const float Opacity = 0.55f;
        const float FadeSec = 0.35f;
        const float BoomVolume = 1f;

        static readonly Color SmokeColor = new Color(0.36f, 0.35f, 0.34f, Opacity);

        class Puff
        {
            public Transform body;
            public Vector3 offset;
            public float size;
            public Vector3 axis;
            public float spin;
        }

        readonly List<Puff> _puffs = new List<Puff>();
        Material _mat;
        Transform _target;
        IDamageable _victim;
        CubeController _player;
        System.Action _onHit;
        float _radius;
        float _damage;
        float _warn;
        float _age;
        float _fade = 1f;
        bool _blown;

        public static SkyBlast Arm(Vector3 at, float radius, float warnSec, Transform target,
            float damage, System.Action onHit = null)
        {
            var blast = new GameObject("Sky Blast").AddComponent<SkyBlast>();
            blast.transform.position = at;
            blast._radius = Mathf.Max(1f, radius);
            blast._warn = Mathf.Max(0.05f, warnSec);
            blast._target = target;
            blast._victim = target != null ? target.GetComponent<IDamageable>() : null;
            blast._player = target != null ? target.GetComponent<CubeController>() : null;
            blast._damage = damage;
            blast._onHit = onHit;
            blast.Build();
            return blast;
        }

        void Build()
        {
            var shader = NightReceivers.Lit;
            if (shader != null)
            {
                _mat = new Material(shader);
                _mat.SetColor("_BaseColor", SmokeColor);
                UIFactory.MakeTransparent(_mat);
                NightReceivers.SetResponse(_mat, NightReceivers.SmokeResponse);
            }

            int count = Random.Range(PuffMin, PuffMax + 1);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Warning Puff";
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    Destroy(col);
                }

                var renderer = go.GetComponent<Renderer>();
                if (_mat != null) renderer.sharedMaterial = _mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                go.transform.SetParent(transform, false);
                go.transform.rotation = Random.rotation;

                Vector2 spread = Random.insideUnitCircle * (_radius * Scatter);
                var puff = new Puff
                {
                    body = go.transform,
                    offset = new Vector3(spread.x, spread.y, 0f),
                    size = _radius * Random.Range(SizeMin, SizeMax),
                    axis = Random.onUnitSphere,
                    spin = Random.Range(SpinMin, SpinMax) * (Random.value < 0.5f ? -1f : 1f),
                };
                _puffs.Add(puff);
            }
            Pose(GrowFrom, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;

            if (!_blown)
            {
                float k = Mathf.Clamp01(_age / _warn);
                float grow = Mathf.Lerp(GrowFrom, 1f, Mathf.SmoothStep(0f, 1f, k));
                grow *= 1f + Pulse * k * Mathf.Sin(_age * PulseRate);
                Pose(grow, dt);
                if (_age >= _warn) Blow();
                return;
            }

            _fade = Mathf.Max(0f, _fade - dt / FadeSec);
            if (_mat != null)
            {
                Color c = SmokeColor;
                c.a *= _fade;
                _mat.SetColor("_BaseColor", c);
            }
            Pose(1f + (1f - _fade) * 0.4f, dt);
            if (_fade <= 0f) Destroy(gameObject);
        }

        void Pose(float grow, float dt)
        {
            for (int i = 0; i < _puffs.Count; i++)
            {
                Puff puff = _puffs[i];
                if (puff.body == null) continue;

                puff.body.localPosition = puff.offset;
                puff.body.localScale = Vector3.one * (puff.size * grow);
                puff.body.Rotate(puff.axis, puff.spin * dt, Space.World);
            }
        }

        void Blow()
        {
            _blown = true;
            Explosion.Spawn(transform.position, _radius, BoomVolume * AudioOptions.Sfx);

            if (_victim == null || _target == null) return;
            if (_player != null && _player.Evading) return;
            if (((Vector2)_target.position - (Vector2)transform.position).sqrMagnitude
                > _radius * _radius) return;

            _victim.TakeDamage(_damage);
            _onHit?.Invoke();
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
