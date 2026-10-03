using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class NightLightingBench : MonoBehaviour
    {
        const float CameraDistance = 420f;
        const float PlayPlaneZ = 100f;
        const float WorldWidth = 2000f;
        const int TerrainSeed = 1916;
        const float FlightY = 230f;
        const float FlightBand = 70f;
        const float FlightSpeed = 140f;
        const int PlaneCount = 4;
        const int MaxSearchlights = 24;
        const float SearchlightY = 40f;
        const float SweepAngle = 35f;
        const float FlashInterval = 0.08f;
        const float TransitionSeconds = 2f;

        static readonly string[] TierNames = { "Mobile Low", "Mobile High", "Desktop" };
        static readonly Color SearchlightColor = new Color(0.867f, 0.910f, 1f);
        static readonly Color ExplosionColor = new Color(1f, 0.541f, 0.239f);

        Camera _cam;
        float _halfW;
        float _halfH;
        Transform[] _planes;
        float[] _phase;
        NightLight2D[] _searchlights;
        int _searchlightCount = 8;
        bool _flashes = true;
        float _flashTimer;
        int _shownBlend = -1;
        NightLightingTier _shownTier;
        Text _label;

        void Start()
        {
            _cam = Camera.main;
            if (_cam == null) return;

            _cam.orthographic = false;
            _cam.transform.rotation = Quaternion.identity;

            NightLights.PlaneZ = PlayPlaneZ;
            ProceduralTerrain.Build(TerrainSeed, WorldWidth, CameraDistance, PlayPlaneZ, Daytime.Night, Weather.Calm);
            NightSky.Apply(_cam, Weather.Calm);
            _cam.farClipPlane = 2200f;
            LevelCamera.Frame(_cam, CameraDistance, out _halfW, out _halfH);
            _cam.transform.position = new Vector3(0f, FlightY, PlayPlaneZ - CameraDistance);

            BuildPlanes();
            BuildSearchlights();
            BuildLabel();
            ApplySearchlightCount();
        }

        void BuildPlanes()
        {
            _planes = new Transform[PlaneCount];
            _phase = new float[PlaneCount];

            for (int i = 0; i < PlaneCount; i++)
            {
                var go = new GameObject(i == 0 ? "Bench Player" : $"Bench Plane {i}");
                go.transform.position = new Vector3(-_halfW + i * _halfW * 0.5f, FlightY, PlayPlaneZ);

                PlaneModelConfig config = PlaneModels.All[i % PlaneModels.All.Length];
                Transform model = PlaneFactory.BuildPlaneModel(go.transform, config);

                if (i == 0)
                {
                    PlaneSearchlight.Mount(go, PlaneFactory.NoseLocal(go, model, config), Daytime.Night);
                }
                else
                {
                    var cone = new GameObject("Bench Headlight").AddComponent<NightLight2D>();
                    cone.transform.SetParent(go.transform, false);
                    cone.transform.localPosition = PlaneFactory.NoseLocal(go, model, config);
                    cone.color = new Color(1f, 0.851f, 0.627f);
                    cone.intensity = 1.2f;
                    cone.range = NightLights.ViewWidth * 0.4f;
                    cone.innerAngle = 12f;
                    cone.outerAngle = 28f;
                    cone.sourceRadius = 6f;
                    cone.localDirection = Vector3.right;
                    cone.priority = 40;
                }

                _planes[i] = go.transform;
                _phase[i] = i * 1.7f;
            }
        }

        void BuildSearchlights()
        {
            _searchlights = new NightLight2D[MaxSearchlights];
            for (int i = 0; i < MaxSearchlights; i++)
            {
                var light = new GameObject($"Bench Searchlight {i}").AddComponent<NightLight2D>();
                float x = Mathf.Lerp(-_halfW, _halfW, (i + 0.5f) / MaxSearchlights);
                light.transform.position = new Vector3(x, SearchlightY, PlayPlaneZ);
                light.type = NightLightType.Cone;
                light.color = SearchlightColor;
                light.intensity = 1.4f;
                light.range = _halfH * 3f;
                light.innerAngle = 4f;
                light.outerAngle = 12f;
                light.sourceRadius = 3f;
                light.falloff = 1.2f;
                light.localDirection = Vector3.up;
                light.priority = 60;
                light.flicker = new Vector2(0.05f, 3f);
                _searchlights[i] = light;
            }
        }

        void BuildLabel()
        {
            Canvas canvas = UIFactory.CreateCanvas("Night Bench HUD");
            _label = UIFactory.CreateText(canvas.transform, "", 22, Vector2.zero,
                new Vector2(900f, 140f), TextAnchor.UpperLeft);
            RectTransform rt = _label.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
        }

        void ApplySearchlightCount()
        {
            for (int i = 0; i < MaxSearchlights; i++)
                _searchlights[i].gameObject.SetActive(i < _searchlightCount);
            RefreshLabel();
        }

        void Update()
        {
            if (_cam == null) return;

            ReadKeys();
            FlyPlanes();
            SweepSearchlights();
            SpawnFlashes();

            NightLightingController night = NightLightingController.Current;
            int blend = night != null ? Mathf.RoundToInt(night.Blend * 20f) : -1;
            NightLightingTier tier = night != null ? night.Tier : null;
            if (blend != _shownBlend || tier != _shownTier) RefreshLabel();
        }

        void ReadKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            NightLightingController night = NightLightingController.Current;
            if (night != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) night.SetTier(TierNames[0]);
                if (kb.digit2Key.wasPressedThisFrame) night.SetTier(TierNames[1]);
                if (kb.digit3Key.wasPressedThisFrame) night.SetTier(TierNames[2]);
                if (kb.nKey.wasPressedThisFrame) night.SetNight(night.Blend < 0.5f, TransitionSeconds);
            }

            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
            {
                _searchlightCount = Mathf.Min(MaxSearchlights, _searchlightCount + 4);
                ApplySearchlightCount();
            }
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
            {
                _searchlightCount = Mathf.Max(0, _searchlightCount - 4);
                ApplySearchlightCount();
            }
            if (kb.gKey.wasPressedThisFrame)
            {
                _flashes = !_flashes;
                RefreshLabel();
            }
            if (kb.oKey.wasPressedThisFrame)
                NightLightingController.OverlayEnabled = !NightLightingController.OverlayEnabled;
        }

        void FlyPlanes()
        {
            float t = Time.time;
            for (int i = 0; i < _planes.Length; i++)
            {
                Transform plane = _planes[i];
                Vector3 p = plane.position;
                float wave = Mathf.Sin(t * 0.6f + _phase[i]);
                float climb = Mathf.Cos(t * 0.6f + _phase[i]) * 0.6f;

                p.x += FlightSpeed * Time.deltaTime;
                if (p.x > _halfW * 1.2f) p.x = -_halfW * 1.2f;
                p.y = FlightY + wave * FlightBand;
                plane.position = p;
                plane.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(climb * FlightBand, FlightSpeed) * Mathf.Rad2Deg);
            }
        }

        void SweepSearchlights()
        {
            float t = Time.time;
            for (int i = 0; i < _searchlightCount; i++)
            {
                float speed = 10f + 10f * Mathf.PerlinNoise(i * 3.1f, 0.5f);
                float angle = Mathf.Sin(t * speed * Mathf.Deg2Rad * 3f + i) * SweepAngle
                              + (Mathf.PerlinNoise(t * 0.2f, i) - 0.5f) * 8f;
                _searchlights[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void SpawnFlashes()
        {
            if (!_flashes) return;

            _flashTimer += Time.deltaTime;
            while (_flashTimer >= FlashInterval)
            {
                _flashTimer -= FlashInterval;
                var at = new Vector3(Random.Range(-_halfW, _halfW),
                    FlightY + Random.Range(-_halfH, _halfH) * 0.8f, PlayPlaneZ);
                bool big = Random.value < 0.25f;
                NightLights.Flash(at, ExplosionColor, big ? 2.5f : 1.2f,
                    NightLights.ViewWidth * (big ? 0.12f : 0.04f), big ? 0.35f : 0.06f, big ? 50 : 10);
            }
        }

        void RefreshLabel()
        {
            NightLightingController night = NightLightingController.Current;
            _shownBlend = night != null ? Mathf.RoundToInt(night.Blend * 20f) : -1;
            _shownTier = night != null ? night.Tier : null;
            if (_label == null) return;

            string tier = night != null && night.Tier != null ? night.Tier.name : "-";
            float blend = night != null ? night.Blend : 0f;
            int max = night != null && night.Tier != null ? night.Tier.maxLights : 0;

            _label.text = $"NIGHT BENCH   tier {tier} (max {max})   blend {blend:0.00}\n" +
                          $"searchlights {_searchlightCount}   flashes {(_flashes ? "on" : "off")}   registered {NightLights.Active.Count}\n" +
                          "1/2/3 tier   +/- searchlights   G flashes   N day/night   O mask overlay   Tab frame stats";
        }
    }
}
