using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class NightLightingController : MonoBehaviour
    {
        const float DevTransitionSeconds = 3f;
        const float AirBehindPlane = 25f;

        static readonly GlobalKeyword NightKeyword = GlobalKeyword.Create("_NIGHT_LIGHTING");

        static readonly int BlendId = Shader.PropertyToID("_NightBlend");
        static readonly int SkyTopId = Shader.PropertyToID("_NightSkyTop");
        static readonly int SkyHorizonId = Shader.PropertyToID("_NightSkyHorizon");
        static readonly int AmbientSkyId = Shader.PropertyToID("_NightAmbientSky");
        static readonly int AmbientGroundId = Shader.PropertyToID("_NightAmbientGround");
        static readonly int MoonColorId = Shader.PropertyToID("_NightMoonColor");
        static readonly int MoonDirId = Shader.PropertyToID("_NightMoonDir");
        static readonly int MoonShadowId = Shader.PropertyToID("_NightMoonShadow");
        static readonly int DesaturationId = Shader.PropertyToID("_NightDesaturation");
        static readonly int UnlitAmbientId = Shader.PropertyToID("_NightUnlitAmbient");
        static readonly int FogColorId = Shader.PropertyToID("_NightFogColor");
        static readonly int FogDensityId = Shader.PropertyToID("_NightFogDensity");
        static readonly int RimColorId = Shader.PropertyToID("_NightRimColor");
        static readonly int RimPowerId = Shader.PropertyToID("_NightRimPower");
        static readonly int HazeId = Shader.PropertyToID("_NightHaze");

        public static NightLightingController Current { get; private set; }

        public static bool Running => Current != null && Current.IsActive;

        public static string TierOverride;

        public static bool OverlayEnabled;

        NightLightingSettings _settings;
        NightPalette _palette;
        NightLightingTier _tier;
        Camera _camera;
        Material _shapeMaterial;
        NightLightMaskPass _mainPass;
        NightLightMaskPass _lightPass;
        Material _casterMaterial;
        NightLightShadowPass _shadowPass;
        Material _airMaterial;
        Mesh _airMesh;
        MeshRenderer _air;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Material _debugMaterial;
        NightLightDebugPass _debugPass;
#endif
        float _blend;
        float _target;
        float _speed;
        bool _atmosphere;
        bool _keywordOn;
        Color _dayFog;
        int _uiMask;

        public float Blend => _blend;

        public bool IsActive => Blend > 0f;

        public NightPalette Palette => _palette;

        public NightLightingTier Tier => _tier;

        public static NightLightingController Launch(Camera cam, TerrainKind map)
        {
            NightLightingController controller = Ensure(cam, map, false);
            NightLights.ClearFlashes();
            controller.SetNight(true);
            return controller;
        }

        public static NightLightingController Ensure(Camera cam, TerrainKind map, bool atmosphere)
        {
            NightLightingController controller = Current;
            if (controller == null)
            {
                var go = new GameObject("Night Lighting");
                controller = go.AddComponent<NightLightingController>();
            }
            controller.Configure(cam, map, atmosphere);
            return controller;
        }

        public static void Shutdown()
        {
            NightLightingController controller = Current;
            if (controller == null) return;
            controller.gameObject.SetActive(false);
            Destroy(controller.gameObject);
        }

        public static void DevToggle()
        {
            NightLightingController controller = Current;
            if (controller == null)
            {
                controller = Ensure(Camera.main, TerrainKind.Verdun, true);
                controller.SetNight(true, DevTransitionSeconds);
                return;
            }
            controller.SetNight(controller._target < 0.5f, DevTransitionSeconds);
        }

        public void SetNight(bool on, float duration = 0f)
        {
            _target = on ? 1f : 0f;
            if (duration <= 0f)
            {
                _blend = _target;
                Apply();
                return;
            }
            _speed = 1f / duration;
        }

        public void SetTier(string tierName)
        {
            TierOverride = tierName;
            _tier = _settings.ResolveTier(TierOverride);
            if (_mainPass != null) _mainPass.Tier = _tier;
        }

        void Configure(Camera cam, TerrainKind map, bool atmosphere)
        {
            _camera = cam != null ? cam : Camera.main;
            _palette = _settings.PaletteFor(map);
            _tier = _settings.ResolveTier(TierOverride);
            if (_mainPass != null) _mainPass.Tier = _tier;

            if (atmosphere && !_atmosphere) _dayFog = RenderSettings.fogColor;
            _atmosphere = atmosphere;
        }

        void Awake()
        {
            _settings = NightLightingSettings.Load();
            _uiMask = 1 << LayerMask.NameToLayer("UI");

            var shape = Shader.Find("Hidden/NightLightShape");
            if (shape != null)
            {
                _shapeMaterial = new Material(shape) { name = "Night Light Shape (runtime)", hideFlags = HideFlags.HideAndDontSave };
                _mainPass = new NightLightMaskPass(_shapeMaterial, false);
                _lightPass = new NightLightMaskPass(_shapeMaterial, true);
            }
            else
            {
                Debug.LogWarning("NightLightingController: Hidden/NightLightShape not found; night lamps are off.");
            }

            var caster = Shader.Find("Hidden/NightLightShadowCaster");
            if (caster != null)
            {
                _casterMaterial = new Material(caster) { name = "Night Light Shadow Caster (runtime)", hideFlags = HideFlags.HideAndDontSave };
                _shadowPass = new NightLightShadowPass(_casterMaterial);
            }

            var air = Shader.Find("Hidden/NightLightAir");
            if (air != null) BuildAir(air);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var debug = Shader.Find("Hidden/NightLightMaskDebug");
            if (debug != null)
            {
                _debugMaterial = new Material(debug) { name = "Night Light Mask Debug (runtime)", hideFlags = HideFlags.HideAndDontSave };
                _debugPass = new NightLightDebugPass(_debugMaterial);
            }
#endif
        }

        void OnEnable()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            Shader.SetGlobalTexture(NightLightMaskPass.MaskId, Texture2D.blackTexture);
            Shader.SetGlobalVector(NightLightShadowPass.ShadowParamsId, Vector4.zero);
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (Current != this) return;
            Current = null;
            TurnOff();
        }

        void BuildAir(Shader shader)
        {
            _airMaterial = new Material(shader) { name = "Night Light Air (runtime)", hideFlags = HideFlags.HideAndDontSave };

            _airMesh = new Mesh { name = "Night Light Air", hideFlags = HideFlags.HideAndDontSave };
            _airMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            _airMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            _airMesh.RecalculateBounds();

            var go = new GameObject("Night Light Air", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = _airMesh;

            _air = go.GetComponent<MeshRenderer>();
            _air.sharedMaterial = _airMaterial;
            _air.shadowCastingMode = ShadowCastingMode.Off;
            _air.receiveShadows = false;
            _air.lightProbeUsage = LightProbeUsage.Off;
            _air.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _air.enabled = false;
        }

        void UpdateAir()
        {
            if (_air == null) return;

            bool on = _blend > 0f && _camera != null;
            if (_air.enabled != on) _air.enabled = on;
            if (!on) return;

            Rect area = NightLights.PlaneArea(_camera, NightLights.PlaneZ, NightLightMaskPass.GuardScale);
            Transform t = _air.transform;
            t.SetPositionAndRotation(new Vector3(area.center.x, area.center.y, NightLights.PlaneZ + AirBehindPlane),
                Quaternion.identity);
            t.localScale = new Vector3(area.width, area.height, 1f);
            _airMaterial.SetFloat(HazeId, _settings.airHaze);
        }

        void OnDestroy()
        {
            if (_shapeMaterial != null) Destroy(_shapeMaterial);
            if (_airMaterial != null) Destroy(_airMaterial);
            if (_casterMaterial != null) Destroy(_casterMaterial);
            if (_airMesh != null) Destroy(_airMesh);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_debugMaterial != null) Destroy(_debugMaterial);
#endif
        }

        void Update()
        {
            if (_blend != _target) _blend = Mathf.MoveTowards(_blend, _target, _speed * Time.deltaTime);
        }

        void LateUpdate()
        {
            Apply();
            UpdateAir();
        }

        void Apply()
        {
            bool on = _blend > 0f;
            if (on != _keywordOn)
            {
                if (on) Shader.EnableKeyword(NightKeyword);
                else Shader.DisableKeyword(NightKeyword);
                _keywordOn = on;
            }

            Shader.SetGlobalFloat(BlendId, _blend);

            if (!on)
            {
                Shader.SetGlobalFloat(NightLightMaskPass.DecodeId, 0f);
                if (_atmosphere) RenderSettings.fogColor = _dayFog;
                return;
            }

            NightPalette p = _palette ?? NightPalette.Verdun();
            Shader.SetGlobalVector(SkyTopId, Linear(p.skyTop));
            Shader.SetGlobalVector(SkyHorizonId, Linear(p.skyHorizon));
            Shader.SetGlobalVector(AmbientSkyId, Linear(p.ambientSky));
            Shader.SetGlobalVector(AmbientGroundId, Linear(p.ambientGround));
            Shader.SetGlobalVector(MoonColorId, Linear(p.moonColor) * p.moonIntensity);
            Shader.SetGlobalVector(MoonDirId, MoonDirection(p));
            Shader.SetGlobalFloat(MoonShadowId, p.moonShadow);
            Shader.SetGlobalFloat(DesaturationId, p.desaturation);
            Shader.SetGlobalVector(UnlitAmbientId, Linear(p.unlitAmbient));
            Shader.SetGlobalVector(FogColorId, Linear(p.fogColor));
            Shader.SetGlobalFloat(FogDensityId, p.fogDensity);
            Shader.SetGlobalVector(RimColorId, Linear(p.rimColor));
            Shader.SetGlobalFloat(RimPowerId, p.rimPower);

            if (_atmosphere) RenderSettings.fogColor = Color.Lerp(_dayFog, p.fogColor, _blend);
        }

        void TurnOff()
        {
            if (_keywordOn)
            {
                Shader.DisableKeyword(NightKeyword);
                _keywordOn = false;
            }
            Shader.SetGlobalFloat(BlendId, 0f);
            Shader.SetGlobalFloat(NightLightMaskPass.DecodeId, 0f);
            Shader.SetGlobalVector(NightLightShadowPass.ShadowParamsId, Vector4.zero);
            if (_atmosphere) RenderSettings.fogColor = _dayFog;
        }

        static Vector4 MoonDirection(NightPalette p)
        {
            Light sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional && sun.isActiveAndEnabled)
                return -sun.transform.forward;

            Vector3 dir = p.moonDir.sqrMagnitude > 1e-6f ? p.moonDir.normalized : Vector3.up;
            return dir;
        }

        static Vector4 Linear(Color c)
        {
            Color l = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
            return new Vector4(l.r, l.g, l.b, 0f);
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (_blend <= 0f || _mainPass == null || cam == null) return;
            if ((cam.cullingMask & ~_uiMask) == 0) return;

            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.renderType == CameraRenderType.Overlay) return;

            ScriptableRenderer renderer = data.scriptableRenderer;
            if (renderer == null) return;

            bool main = cam == _camera
                || (cam.cameraType == CameraType.SceneView && _settings.previewInSceneView);

            bool shadow = main && cam == _camera && _shadowPass != null
                && _shadowPass.Prepare(cam, _tier, NightLights.ShadowCaster());
            if (shadow) renderer.EnqueuePass(_shadowPass);
            _mainPass.ShadowOn = shadow;

            renderer.EnqueuePass(main ? _mainPass : _lightPass);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (main && _debugPass != null && (OverlayEnabled || _settings.debugOverlay))
                renderer.EnqueuePass(_debugPass);
#endif
        }
    }
}
