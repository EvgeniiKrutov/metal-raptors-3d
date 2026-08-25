using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class SupplyCrate : MonoBehaviour
    {
        public const float OnScreenSize = 56f;

        const string ModelResource = "objects/supply_crate";
        const string BoxNode = "Crate";

        const float FallSpeed = 80f;
        const float SwayAmplitude = 22f;
        const float SwayHz = 0.4f;
        const float SwayTiltDeg = 12f;

        const float LeadFraction = 0.72f;
        const float SpawnLiftFactor = 1.3f;
        const float CatchRadius = 42f;
        const float HangFraction = 0.74f;
        const float BoxHalfFraction = 0.22f;

        const float GroundLift = 200f;
        const float GroundProbe = 4000f;

        const float ModelYaw = 24f;

        const float ChimeSeconds = 0.42f;
        const float ChimeLowHz = 622f;
        const float ChimeHighHz = 932f;
        const float ChimeDecay = 9f;
        const float ChimeGain = 0.5f;
        const float ChimeVolume = 0.45f;
        const int ChimeRate = 44100;

        const float FallbackCubeFraction = 0.55f;

        static readonly Color FallbackColor = new Color(0.45f, 0.31f, 0.17f);

        static AudioClip _chime;

        Transform _player;
        System.Action _onCaught;
        Vector3 _boxLocal;
        float _worldX;
        float _floorY;
        float _phase;

        Vector3 BoxCentre => transform.TransformPoint(_boxLocal);

        public static SupplyCrate Spawn(Vector3 camPos, float halfWidth, float halfHeight,
            float z, float floorY, Transform player, System.Action onCaught)
        {
            var go = new GameObject("Supply Crate");

            var crate = go.AddComponent<SupplyCrate>();
            crate._player = player;
            crate._onCaught = onCaught;
            crate._worldX = camPos.x + halfWidth * LeadFraction;
            crate._floorY = floorY;
            crate._phase = Random.Range(0f, Mathf.PI * 2f);

            go.transform.position = new Vector3(crate._worldX,
                camPos.y + halfHeight + OnScreenSize * SpawnLiftFactor, z);

            crate.BuildModel();
            return crate;
        }

        void BuildModel()
        {
            _boxLocal = Vector3.down * (OnScreenSize * HangFraction);

            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"SupplyCrate: {ModelResource} not found in Resources.");
                var fallback = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, transform.position,
                    Vector3.one * (OnScreenSize * FallbackCubeFraction), FallbackColor,
                    emissive: false, keepCollider: false);
                fallback.transform.SetParent(transform, false);
                fallback.transform.localPosition = _boxLocal;
                return;
            }

            var instance = Instantiate(prefab);
            instance.name = "supply_crate";

            Transform model = instance.transform;
            model.SetParent(transform, false);
            model.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            Hang(model, OnScreenSize);
            _boxLocal = BoxLocal(model);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.On;

            foreach (Collider col in model.GetComponentsInChildren<Collider>())
                Destroy(col);
        }

        Vector3 BoxLocal(Transform model)
        {
            Transform box = Find(model, BoxNode);
            if (box != null && Measure(box, out Bounds bounds))
                return transform.InverseTransformPoint(bounds.center);

            return Vector3.down * (OnScreenSize * HangFraction);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(node.name, name, System.StringComparison.OrdinalIgnoreCase))
                    return node;

            return null;
        }

        static void Hang(Transform model, float targetSize)
        {
            if (!Measure(model, out Bounds bounds)) return;

            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0.0001f) model.localScale *= targetSize / longest;

            if (!Measure(model, out bounds)) return;

            Transform parent = model.parent;
            var top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            model.localPosition -= parent != null ? parent.InverseTransformPoint(top) : top;
        }

        static bool Measure(Transform model, out Bounds bounds)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            bounds = new Bounds(model.position, Vector3.zero);
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        public void Tick(float camX, float halfWidth, float dt)
        {
            _phase += dt * SwayHz * 2f * Mathf.PI;

            float swing = Mathf.Sin(_phase);
            Vector3 pos = transform.position;
            pos.x = _worldX + swing * SwayAmplitude;
            pos.y -= FallSpeed * dt;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, 0f, -swing * SwayTiltDeg);

            Vector3 box = BoxCentre;

            if (Caught(box))
            {
                PlayChime(box);
                Burst(box);
                if (_onCaught != null) _onCaught();
                return;
            }

            if (box.y - OnScreenSize * BoxHalfFraction <= GroundY(box))
            {
                Burst(box);
                return;
            }

            if (box.x < camX - halfWidth - OnScreenSize) Destroy(gameObject);
        }

        bool Caught(Vector3 box)
        {
            if (_player == null) return false;

            Vector2 gap = (Vector2)_player.position - (Vector2)box;
            return gap.sqrMagnitude <= CatchRadius * CatchRadius;
        }

        float GroundY(Vector3 box)
        {
            var origin = new Vector3(box.x, box.y + GroundLift, box.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbe,
                1 << ProceduralTerrain.GroundLayer, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return _floorY;
        }

        void Burst(Vector3 box)
        {
            CrateBurst.Spawn(box, OnScreenSize);
            Destroy(gameObject);
        }

        static void PlayChime(Vector3 position)
        {
            AudioClip clip = Chime();
            if (clip == null) return;

            var go = new GameObject("SupplyPickupSound");
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.PlayOneShot(clip, ChimeVolume * AudioOptions.Sfx);
            Destroy(go, clip.length + 0.1f);
        }

        static AudioClip Chime()
        {
            if (_chime != null) return _chime;

            int frames = Mathf.RoundToInt(ChimeRate * ChimeSeconds);
            int half = frames / 2;
            var data = new float[frames];

            for (int i = 0; i < frames; i++)
            {
                bool second = i >= half;
                float freq = second ? ChimeHighHz : ChimeLowHz;
                float t = (i - (second ? half : 0)) / (float)ChimeRate;

                float env = Mathf.Exp(-t * ChimeDecay) * Mathf.Min(1f, t * 220f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t)
                           + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.25f) * env * ChimeGain;
            }

            _chime = AudioClip.Create("SupplyPickup", frames, 1, ChimeRate, false);
            _chime.SetData(data, 0);
            return _chime;
        }
    }
}
