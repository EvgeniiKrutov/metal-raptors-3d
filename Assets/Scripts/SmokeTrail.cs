using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class SmokeTrail : MonoBehaviour
    {
        const float EmitInterval = 0.05f;
        const float BurnEmitInterval = 0.028f;
        const float BurnSizeFactor = 0.45f;

        const float LifeMin = 0.9f;
        const float LifeMax = 1.5f;
        const float StartSizeFactor = 0.28f;
        const float StartSizeJitter = 0.4f;
        const float MinScaleFactor = 0.2f;
        const float DriftSpeedFactor = 0.25f;
        const float RiseSpeed = 8f;
        const float SpinMin = 40f;
        const float SpinMax = 140f;
        const float Opacity = 0.6f;

        static readonly Color SmokeColor = new Color(0.10f, 0.10f, 0.11f, Opacity);

        bool _armed;
        bool _burning;
        bool _cleared;
        float _size;
        float _emitTimer;
        readonly List<SmokeTrail> _puffs = new List<SmokeTrail>();

        bool _isPuff;
        SmokeTrail _emitter;
        Vector3 _velocity;
        Vector3 _spinAxis;
        float _spinRate;
        float _age;
        float _life;
        float _startScale;
        Material _mat;

        public void Arm(float planeSize)
        {
            _armed = true;
            _size = Mathf.Max(1f, planeSize);
        }

        public void Ignite(float planeSize)
        {
            Arm(planeSize);
            _burning = true;
        }

        public void Clear()
        {
            _armed = false;
            _cleared = true;
            for (int i = _puffs.Count - 1; i >= 0; i--)
                if (_puffs[i] != null) Destroy(_puffs[i].gameObject);
            _puffs.Clear();
        }

        void Update()
        {
            if (_isPuff) { AnimatePuff(); return; }
            if (!_armed || _cleared) return;

            _emitTimer -= Time.deltaTime;
            if (_emitTimer <= 0f)
            {
                _emitTimer = _burning ? BurnEmitInterval : EmitInterval;
                EmitPuff();
            }
        }

        void OnDestroy()
        {
            if (!_isPuff) Clear();
            else if (_emitter != null) _emitter.Unregister(this);
        }

        void Unregister(SmokeTrail puff) => _puffs.Remove(puff);

        void EmitPuff()
        {
            Vector3 back = -transform.right;
            Vector3 spawn = transform.position;

            float sizeFactor = _burning ? BurnSizeFactor : StartSizeFactor;
            float scale = _size * sizeFactor * Random.Range(1f - StartSizeJitter, 1f + StartSizeJitter);
            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                spawn, Vector3.one * scale, SmokeColor, emissive: false, keepCollider: false);
            go.name = "Smoke";
            var renderer = go.GetComponent<Renderer>();
            UIFactory.MakeTransparent(renderer.sharedMaterial);
            go.transform.rotation = Random.rotation;

            var puff = go.AddComponent<SmokeTrail>();
            puff._isPuff = true;
            puff._emitter = this;
            puff._startScale = scale;
            puff._life = Random.Range(LifeMin, LifeMax);
            puff._mat = renderer.sharedMaterial;
            _puffs.Add(puff);
            Vector2 drift = new Vector2(back.x, back.y).normalized * (_size * DriftSpeedFactor)
                            * Random.Range(0.7f, 1.3f);
            puff._velocity = new Vector3(drift.x, drift.y + RiseSpeed, 0f);
            puff._spinAxis = Random.onUnitSphere;
            puff._spinRate = Random.Range(SpinMin, SpinMax) * (Random.value < 0.5f ? -1f : 1f);
        }

        void AnimatePuff()
        {
            _age += Time.deltaTime;
            if (_age >= _life)
            {
                Destroy(gameObject);
                return;
            }

            float t = _age / _life;

            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(_spinAxis, _spinRate * Time.deltaTime, Space.World);
            float scale = Mathf.Lerp(_startScale, _startScale * 0.1f, t);
            if (scale <= _startScale * MinScaleFactor)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = Vector3.one * scale;

            if (_mat != null)
            {
                var c = SmokeColor;
                c.a = Opacity * (1f - t);
                _mat.SetColor("_BaseColor", c);
            }
        }
    }
}
