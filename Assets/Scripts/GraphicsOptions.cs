using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public enum QualityTier { Off = 0, Low = 1, High = 2 }

    public static class GraphicsOptions
    {
        public static readonly string[] SwitchLabels = { "off", "on" };
        public static readonly string[] ShadowLabels = { "off", "low", "high" };
        public static readonly string[] BloomLabels = { "off", "low", "full" };
        public static readonly string[] DetailLabels = { "low", "medium", "high" };
        public static readonly string[] FrameCapLabels = { "30", "60", "120", "off" };

        static readonly int[] FrameCapValues = { 30, 60, 120, -1 };

        const string PrefGodRays = "mr_gfx_god_rays";
        const string PrefShadows = "mr_gfx_shadows";
        const string PrefBloom = "mr_gfx_bloom";
        const string PrefDetail = "mr_gfx_detail";
        const string PrefFrameCap = "mr_gfx_frame_cap";

        const int BloomLowIterations = 3;

        public static event Action Changed;

        static readonly List<Light> _casters = new List<Light>();
        static readonly List<LightShadows> _authored = new List<LightShadows>();
        static readonly List<Bloom> _blooms = new List<Bloom>();

        static bool _loaded;
        static bool _godRays = true;
        static QualityTier _shadows = QualityTier.High;
        static QualityTier _bloom = QualityTier.High;
        static int _detail = 2;
        static int _frameCap = 3;

        static int _baseCascades = -1;
        static int _baseShadowmap = -1;

        public static bool Mobile => Application.isMobilePlatform;

        public static bool GodRays { get { Load(); return _godRays; } }

        public static QualityTier Shadows { get { Load(); return _shadows; } }

        public static QualityTier BloomTier { get { Load(); return _bloom; } }

        public static int GroundDetail { get { Load(); return _detail; } }

        public static int FrameCap { get { Load(); return _frameCap; } }

        public static float PeopleScale =>
            GroundDetail == 0 ? 0.35f : GroundDetail == 1 ? 0.65f : 1f;

        public static int PeopleGroupCap =>
            GroundDetail == 0 ? 4 : GroundDetail == 1 ? 6 : int.MaxValue;

        public static float TreeCellScale =>
            GroundDetail == 0 ? 2.2f : GroundDetail == 1 ? 1.5f : 1f;

        public static void SetGodRays(int index)
        {
            Load();
            _godRays = index != 0;
            PlayerPrefs.SetInt(PrefGodRays, _godRays ? 1 : 0);
            Commit();
        }

        public static void SetShadows(int tier)
        {
            Load();
            _shadows = Clamp(tier);
            PlayerPrefs.SetInt(PrefShadows, (int)_shadows);
            Apply();
            Commit();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() => Application.quitting += RestorePipeline;

        static void RestorePipeline()
        {
            if (_baseCascades < 0) return;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;

            urp.shadowCascadeCount = _baseCascades;
            urp.mainLightShadowmapResolution = _baseShadowmap;
        }

        public static void SetBloom(int tier)
        {
            Load();
            _bloom = Clamp(tier);
            PlayerPrefs.SetInt(PrefBloom, (int)_bloom);
            ApplyBloom();
            Commit();
        }

        public static void SetGroundDetail(int level)
        {
            Load();
            _detail = Mathf.Clamp(level, 0, 2);
            PlayerPrefs.SetInt(PrefDetail, _detail);
            Commit();
        }

        public static void SetFrameCap(int index)
        {
            Load();
            _frameCap = Mathf.Clamp(index, 0, FrameCapValues.Length - 1);
            PlayerPrefs.SetInt(PrefFrameCap, _frameCap);
            ApplyFrameCap();
            Commit();
        }

        public static void TrackBloom(Bloom bloom)
        {
            if (bloom == null) return;
            Load();

            if (!_blooms.Contains(bloom)) _blooms.Add(bloom);
            ApplyBloom();
        }

        static void ApplyBloom()
        {
            for (int i = _blooms.Count - 1; i >= 0; i--)
            {
                Bloom bloom = _blooms[i];
                if (bloom == null) { _blooms.RemoveAt(i); continue; }

                bloom.active = _bloom != QualityTier.Off;
                bool low = _bloom == QualityTier.Low;
                bloom.downscale.Override(low ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
                bloom.maxIterations.Override(low ? BloomLowIterations : 6);
                bloom.highQualityFiltering.Override(false);
            }
        }

        static void ApplyFrameCap()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameCapValues[_frameCap];
        }

        public static void Track(Light light, LightShadows authored)
        {
            if (light == null) return;
            Load();

            int index = _casters.IndexOf(light);
            if (index < 0)
            {
                _casters.Add(light);
                _authored.Add(authored);
            }
            else _authored[index] = authored;

            light.shadows = Downgrade(authored);
        }

        public static void Rescan()
        {
            Load();
            _casters.Clear();
            _authored.Clear();
            _blooms.Clear();

            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.shadows != LightShadows.None) Track(light, light.shadows);

            Apply();
        }

        public static void Apply()
        {
            Load();
            ApplyPipeline();
            ApplyFrameCap();

            for (int i = _casters.Count - 1; i >= 0; i--)
            {
                if (_casters[i] == null)
                {
                    _casters.RemoveAt(i);
                    _authored.RemoveAt(i);
                    continue;
                }
                _casters[i].shadows = Downgrade(_authored[i]);
            }
        }

        static LightShadows Downgrade(LightShadows authored)
        {
            switch (_shadows)
            {
                case QualityTier.Off: return LightShadows.None;
                case QualityTier.Low: return LightShadows.Hard;
                default: return authored;
            }
        }

        static void ApplyPipeline()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;

            if (_baseCascades < 0)
            {
                _baseCascades = urp.shadowCascadeCount;
                _baseShadowmap = urp.mainLightShadowmapResolution;
            }

            bool low = _shadows == QualityTier.Low;
            urp.shadowCascadeCount = low ? Mathf.Clamp(_baseCascades, 1, 2) : _baseCascades;
            urp.mainLightShadowmapResolution =
                low ? Mathf.Max(256, _baseShadowmap / 2) : _baseShadowmap;
        }

        static QualityTier Clamp(int tier) =>
            (QualityTier)Mathf.Clamp(tier, (int)QualityTier.Off, (int)QualityTier.High);

        static void Commit()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            if (Mobile)
            {
                _godRays = false;
                _shadows = QualityTier.High;
                _bloom = QualityTier.Off;
                _detail = 0;
                _frameCap = 1;
                return;
            }

            _godRays = PlayerPrefs.GetInt(PrefGodRays, 1) != 0;
            _shadows = Clamp(PlayerPrefs.GetInt(PrefShadows, (int)QualityTier.High));
            _bloom = Clamp(PlayerPrefs.GetInt(PrefBloom, (int)QualityTier.High));
            _detail = Mathf.Clamp(PlayerPrefs.GetInt(PrefDetail, 2), 0, 2);
            _frameCap = Mathf.Clamp(PlayerPrefs.GetInt(PrefFrameCap, 3),
                0, FrameCapValues.Length - 1);
        }
    }
}
