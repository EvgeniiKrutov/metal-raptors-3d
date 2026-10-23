using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class BoatSpray
    {
        const int DropCap = 220, MistCap = 40;
        const float Gravity = 9.8f;
        const float Drag = 0.9f;

        const float BowAhead = 0.18f, BowSpreadBack = 0.2f, BowSpreadFore = 0.05f;
        const float BowRate = 90f, SternRate = 30f;
        const float BowMistRate = 9f, SternMistRate = 6f;
        const float Carry = 0.15f;

        const float BowOutMin = 4f, BowOutMax = 9f;
        const float BowUpMin = 3f, BowUpMax = 6.5f;
        const float SternSpread = 0.4f, SternOut = 2.5f;
        const float SternUpMin = 3.5f, SternUpMax = 8f;

        const float DropSizeMin = 0.08f, DropSizeMax = 0.2f;
        const float DropLifeMin = 1.4f, DropLifeMax = 2.2f;
        const float ShrinkFrom = 0.7f;
        const float SpinMin = 60f, SpinMax = 240f;

        const float MistSizeMin = 0.5f, MistSizeMax = 1.2f;
        const float MistGrowth = 2.2f;
        const float MistLifeMin = 0.9f, MistLifeMax = 1.5f;
        const float MistOpacity = 0.35f;
        const float MistDrift = 0.35f;

        static readonly Color DropColor = new Color(0.84f, 0.88f, 0.87f);
        static readonly Color MistColor = new Color(0.78f, 0.82f, 0.81f, MistOpacity);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        static Material _dropMaterial, _mistMaterial;
        static MaterialPropertyBlock _block;

        class Piece
        {
            public Transform tr;
            public Renderer renderer;
            public Vector3 velocity, axis;
            public float age, life, size, spin;
        }

        readonly Transform _parent;
        readonly float _scale, _length, _beam, _bowAhead;
        readonly List<Piece> _drops = new List<Piece>();
        readonly List<Piece> _mists = new List<Piece>();
        readonly Stack<Piece> _freeDrops = new Stack<Piece>();
        readonly Stack<Piece> _freeMists = new Stack<Piece>();
        readonly List<Piece> _all = new List<Piece>();
        float _bowDebt, _sternDebt, _bowMistDebt, _sternMistDebt;

        public BoatSpray(Transform parent, float scale, float length, float beam, float bowAhead = BowAhead)
        {
            _parent = parent;
            _scale = scale;
            _length = length;
            _beam = beam;
            _bowAhead = bowAhead;
        }

        public bool Idle => _drops.Count == 0 && _mists.Count == 0;

        public void Tick(float dt, float time, bool emitting, Transform root, float speed)
        {
            if (emitting) Emit(dt, root, speed);

            AdvanceDrops(dt, time);
            AdvanceMists(dt);
        }

        public void Release()
        {
            foreach (Piece p in _all)
                if (p.tr != null) Object.Destroy(p.tr.gameObject);
            _all.Clear();
            _drops.Clear();
            _mists.Clear();
            _freeDrops.Clear();
            _freeMists.Clear();
        }

        void Emit(float dt, Transform root, float speed)
        {
            Vector3 ahead = root.right;
            Vector3 across = root.forward;
            Vector3 centre = root.position;
            Vector3 travel = ahead * (speed * Carry);
            Vector3 bow = centre + ahead * (_length * _bowAhead);
            Vector3 stern = centre - ahead * (_length * 0.5f);
            float side = _beam * 0.5f;

            _bowDebt += BowRate * dt;
            while (_bowDebt >= 1f)
            {
                _bowDebt -= 1f;
                float sign = Random.value < 0.5f ? -1f : 1f;
                Vector3 origin = BowOrigin(bow, ahead, across, sign * side);
                Launch(false, origin, travel
                    + across * (sign * Random.Range(BowOutMin, BowOutMax) * _scale)
                    + Vector3.up * (Random.Range(BowUpMin, BowUpMax) * _scale));
            }

            _bowMistDebt += BowMistRate * dt;
            while (_bowMistDebt >= 1f)
            {
                _bowMistDebt -= 1f;
                float sign = Random.value < 0.5f ? -1f : 1f;
                Vector3 origin = BowOrigin(bow, ahead, across, sign * side);
                Launch(true, origin, (travel
                    + across * (sign * Random.Range(BowOutMin, BowOutMax) * _scale)
                    + Vector3.up * (Random.Range(BowUpMin, BowUpMax) * _scale)) * MistDrift);
            }

            _sternDebt += SternRate * dt;
            while (_sternDebt >= 1f)
            {
                _sternDebt -= 1f;
                Vector3 origin = stern + across * (Random.Range(-SternSpread, SternSpread) * side);
                Launch(false, origin, travel * 0.5f
                    + across * (Random.Range(-SternOut, SternOut) * _scale)
                    + Vector3.up * (Random.Range(SternUpMin, SternUpMax) * _scale));
            }

            _sternMistDebt += SternMistRate * dt;
            while (_sternMistDebt >= 1f)
            {
                _sternMistDebt -= 1f;
                Vector3 origin = stern + across * (Random.Range(-SternSpread, SternSpread) * side);
                Launch(true, origin, (travel * 0.5f
                    + Vector3.up * (Random.Range(SternUpMin, SternUpMax) * _scale)) * MistDrift);
            }
        }

        Vector3 BowOrigin(Vector3 bow, Vector3 ahead, Vector3 across, float offset) =>
            bow + ahead * (_length * Random.Range(-BowSpreadBack, BowSpreadFore)) + across * offset;

        void Launch(bool mist, Vector3 origin, Vector3 velocity)
        {
            Piece p = Take(mist);
            if (p == null) return;

            p.age = 0f;
            p.velocity = velocity;
            p.axis = Random.onUnitSphere;
            p.spin = Random.Range(SpinMin, SpinMax) * (Random.value < 0.5f ? -1f : 1f);
            p.life = mist ? Random.Range(MistLifeMin, MistLifeMax) : Random.Range(DropLifeMin, DropLifeMax);
            p.size = _scale * (mist
                ? Random.Range(MistSizeMin, MistSizeMax)
                : Random.Range(DropSizeMin, DropSizeMax));

            p.tr.SetPositionAndRotation(origin, Random.rotation);
            p.tr.localScale = Vector3.one * p.size;
            p.renderer.enabled = true;
            if (mist) Fade(p, 0f);

            (mist ? _mists : _drops).Add(p);
        }

        Piece Take(bool mist)
        {
            Stack<Piece> free = mist ? _freeMists : _freeDrops;
            if (free.Count > 0) return free.Pop();

            int count = mist ? _mists.Count : _drops.Count;
            if (count >= (mist ? MistCap : DropCap)) return null;

            Material material = mist ? MistMaterial() : DropMaterial();
            if (material == null) return null;

            var go = new GameObject(mist ? "Boat Mist" : "Boat Spray");
            go.transform.SetParent(_parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var p = new Piece { tr = go.transform, renderer = renderer };
            _all.Add(p);
            return p;
        }

        void AdvanceDrops(float dt, float time)
        {
            float fall = Gravity * _scale * dt;
            float keep = Mathf.Exp(-Drag * dt);

            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                Piece p = _drops[i];
                p.age += dt;
                p.velocity.x *= keep;
                p.velocity.z *= keep;
                p.velocity.y -= fall;

                Vector3 pos = p.tr.position + p.velocity * dt;
                bool sunk = p.velocity.y < 0f && pos.y < SeaSurface.HeightAt(pos.x, pos.z, time);
                if (sunk || p.age >= p.life)
                {
                    Retire(p, _drops, i, _freeDrops);
                    continue;
                }

                p.tr.position = pos;
                p.tr.Rotate(p.axis, p.spin * dt, Space.World);

                float t = p.age / p.life;
                float shrink = t < ShrinkFrom ? 1f : 1f - (t - ShrinkFrom) / (1f - ShrinkFrom);
                p.tr.localScale = Vector3.one * (p.size * shrink);
            }
        }

        void AdvanceMists(float dt)
        {
            float keep = Mathf.Exp(-Drag * dt);

            for (int i = _mists.Count - 1; i >= 0; i--)
            {
                Piece p = _mists[i];
                p.age += dt;
                if (p.age >= p.life)
                {
                    Retire(p, _mists, i, _freeMists);
                    continue;
                }

                p.velocity *= keep;
                p.tr.position += p.velocity * dt;
                p.tr.Rotate(p.axis, p.spin * 0.2f * dt, Space.World);

                float t = p.age / p.life;
                p.tr.localScale = Vector3.one * (p.size * Mathf.Lerp(1f, MistGrowth, t));
                Fade(p, t);
            }
        }

        static void Fade(Piece p, float t)
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            Color color = MistColor;
            color.a = MistOpacity * (1f - t) * (1f - t);
            _block.SetColor(BaseColorId, color);
            p.renderer.SetPropertyBlock(_block);
        }

        static void Retire(Piece p, List<Piece> live, int index, Stack<Piece> free)
        {
            p.renderer.enabled = false;
            live.RemoveAt(index);
            free.Push(p);
        }

        static Material DropMaterial()
        {
            if (_dropMaterial != null) return _dropMaterial;

            var shader = NightReceivers.Lit;
            if (shader == null) return null;

            _dropMaterial = new Material(shader) { name = "Boat Spray" };
            _dropMaterial.SetColor(BaseColorId, DropColor);
            _dropMaterial.SetFloat("_Smoothness", 0.25f);
            return _dropMaterial;
        }

        static Material MistMaterial()
        {
            if (_mistMaterial != null) return _mistMaterial;

            var shader = NightReceivers.Lit;
            if (shader == null) return null;

            _mistMaterial = new Material(shader) { name = "Boat Mist" };
            _mistMaterial.SetColor(BaseColorId, MistColor);
            _mistMaterial.SetFloat("_Smoothness", 0.1f);
            UIFactory.MakeTransparent(_mistMaterial);
            return _mistMaterial;
        }
    }
}
