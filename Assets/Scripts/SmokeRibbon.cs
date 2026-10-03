using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class SmokeRibbon : MonoBehaviour
    {
        const float Spacing = 8f;
        const float SizeMin = 22f;
        const float SizeMax = 34f;
        const float Scatter = 5f;
        const float GrowSec = 0.3f;
        const float GrowFrom = 0.35f;
        const float SpinMin = 15f;
        const float SpinMax = 60f;
        const float Opacity = 0.6f;
        const float FadeSec = 1.6f;
        const float Reach = 22f;
        const float TickSec = 0.25f;

        static readonly Color SmokeColor = new Color(0.12f, 0.12f, 0.13f, Opacity);

        class Puff
        {
            public Transform body;
            public Vector2 at;
            public Vector2 offset;
            public float size;
            public float age;
            public Vector3 axis;
            public float spin;
        }

        readonly List<Puff> _puffs = new List<Puff>();
        Material _mat;
        Transform _target;
        IDamageable _victim;
        float _drift;
        float _leftX;
        float _z;
        float _damage;
        Vector2 _last;
        bool _started;
        bool _fading;
        float _fade = 1f;
        float _tick;

        public static SmokeRibbon Lay(Transform target, float drift, float leftX, float z,
            float damagePerSec)
        {
            var ribbon = new GameObject("Smoke Ribbon").AddComponent<SmokeRibbon>();
            ribbon._target = target;
            ribbon._victim = target != null ? target.GetComponent<IDamageable>() : null;
            ribbon._drift = drift;
            ribbon._leftX = leftX;
            ribbon._z = z;
            ribbon._damage = damagePerSec;

            var shader = NightReceivers.Lit;
            if (shader != null)
            {
                ribbon._mat = new Material(shader);
                ribbon._mat.SetColor("_BaseColor", SmokeColor);
                UIFactory.MakeTransparent(ribbon._mat);
                NightReceivers.SetResponse(ribbon._mat, NightReceivers.SmokeResponse);
            }
            return ribbon;
        }

        public void Feed(Vector2 at)
        {
            if (_fading) return;

            if (!_started)
            {
                _started = true;
                _last = at;
                Spawn(at);
                return;
            }

            while ((at - _last).sqrMagnitude >= Spacing * Spacing)
            {
                _last = Vector2.MoveTowards(_last, at, Spacing);
                Spawn(_last);
            }
        }

        public void Fade() => _fading = true;

        void Spawn(Vector2 at)
        {
            if (at.x + SizeMax < _leftX) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Smoke";
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

            var puff = new Puff
            {
                body = go.transform,
                at = at,
                offset = Random.insideUnitCircle * Scatter,
                size = Random.Range(SizeMin, SizeMax),
                axis = Random.onUnitSphere,
                spin = Random.Range(SpinMin, SpinMax) * (Random.value < 0.5f ? -1f : 1f),
            };
            _puffs.Add(puff);
            Pose(puff, 0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float dx = _drift * dt;
            _last.x -= dx;

            for (int i = _puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = _puffs[i];
                puff.at.x -= dx;
                puff.age += dt;

                if (puff.at.x + puff.size < _leftX)
                {
                    if (puff.body != null) Destroy(puff.body.gameObject);
                    _puffs.RemoveAt(i);
                    continue;
                }
                Pose(puff, dt);
            }

            if (!_fading)
            {
                Hurt(dt);
                return;
            }

            _fade = Mathf.Max(0f, _fade - dt / FadeSec);
            if (_mat != null)
            {
                Color c = SmokeColor;
                c.a *= _fade;
                _mat.SetColor("_BaseColor", c);
            }
            if (_fade <= 0f) Destroy(gameObject);
        }

        void Pose(Puff puff, float dt)
        {
            if (puff.body == null) return;

            float grow = Mathf.Lerp(GrowFrom, 1f, Mathf.SmoothStep(0f, 1f, puff.age / GrowSec));
            puff.body.position = new Vector3(puff.at.x + puff.offset.x, puff.at.y + puff.offset.y, _z);
            puff.body.localScale = Vector3.one * (puff.size * grow);
            puff.body.Rotate(puff.axis, puff.spin * dt, Space.World);
        }

        void Hurt(float dt)
        {
            _tick = Mathf.Max(0f, _tick - dt);
            if (_tick > 0f || _victim == null || _target == null) return;
            if (!Touches(_target.position)) return;

            _tick = TickSec;
            _victim.TakeDamage(_damage * TickSec);
        }

        bool Touches(Vector2 at)
        {
            float reach = Reach * Reach;
            for (int i = 0; i < _puffs.Count; i++)
                if ((_puffs[i].at - at).sqrMagnitude <= reach) return true;
            return false;
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
