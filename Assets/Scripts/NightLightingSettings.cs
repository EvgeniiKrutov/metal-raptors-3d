using System;
using UnityEngine;

namespace MetalRaptors
{
    [Serializable]
    public class NightPalette
    {
        public TerrainKind map = TerrainKind.Verdun;
        public Color skyTop = new Color(0.045f, 0.033f, 0.151f);
        public Color skyHorizon = new Color(0.201f, 0.137f, 0.411f);
        public Color ambientSky = new Color(0.261f, 0.220f, 0.625f);
        public Color ambientGround = new Color(0.153f, 0.106f, 0.289f);
        public Color moonColor = new Color(0.509f, 0.497f, 1.000f);
        public float moonIntensity = 2.18f;
        public Vector3 moonDir = new Vector3(0.156f, 0.766f, -0.624f);
        [Range(0f, 1f)] public float moonShadow = 0.85f;
        [Range(0f, 1f)] public float desaturation = 0.12f;
        public Color unlitAmbient = new Color(0.55f, 0.50f, 0.95f);
        public Color fogColor = new Color(0.201f, 0.137f, 0.411f);
        public float fogDensity;
        public Color rimColor = new Color(0.38f, 0.42f, 0.78f);
        [Range(0.5f, 8f)] public float rimPower = 3f;

        public static NightPalette Verdun() => new NightPalette();

        public static NightPalette Coast() => new NightPalette
        {
            map = TerrainKind.Flanders,
            skyTop = new Color(0.032f, 0.061f, 0.156f),
            skyHorizon = new Color(0.134f, 0.183f, 0.305f),
            ambientSky = new Color(0.206f, 0.271f, 0.523f),
            ambientGround = new Color(0.124f, 0.150f, 0.251f),
            moonColor = new Color(0.478f, 0.610f, 1.000f),
            moonIntensity = 1.73f,
            moonDir = new Vector3(0.139f, 0.743f, -0.654f),
            unlitAmbient = new Color(0.48f, 0.56f, 0.90f),
            fogColor = new Color(0.134f, 0.183f, 0.305f),
            rimColor = new Color(0.34f, 0.44f, 0.72f),
        };

        public static NightPalette Alpine() => new NightPalette
        {
            map = TerrainKind.Dolomites,
            skyTop = new Color(0.033f, 0.052f, 0.145f),
            skyHorizon = new Color(0.148f, 0.198f, 0.351f),
            ambientSky = new Color(0.220f, 0.288f, 0.544f),
            ambientGround = new Color(0.137f, 0.164f, 0.268f),
            moonColor = new Color(0.482f, 0.614f, 1.000f),
            moonIntensity = 1.86f,
            moonDir = new Vector3(0.128f, 0.788f, -0.602f),
            unlitAmbient = new Color(0.50f, 0.58f, 0.92f),
            fogColor = new Color(0.148f, 0.198f, 0.351f),
            rimColor = new Color(0.35f, 0.45f, 0.74f),
        };
    }

    [Serializable]
    public class NightLightingTier
    {
        public string name;
        public string qualityLevel;
        [Range(0.1f, 1f)] public float maskScale = 0.35f;
        public bool hdrMask;
        [Range(1, NightLights.MaxLights)] public int maxLights = 20;
        public bool facing;
        public int occlusionSteps;
        public int shadowMapSize;

        public static NightLightingTier[] Defaults() => new[]
        {
            new NightLightingTier
            {
                name = "Mobile Low", qualityLevel = "", maskScale = 0.25f, hdrMask = false,
                maxLights = 12, facing = false, occlusionSteps = 0, shadowMapSize = 0,
            },
            new NightLightingTier
            {
                name = "Mobile High", qualityLevel = "Mobile", maskScale = 0.35f, hdrMask = false,
                maxLights = 20, facing = true, occlusionSteps = 8, shadowMapSize = 512,
            },
            new NightLightingTier
            {
                name = "Desktop", qualityLevel = "PC", maskScale = 0.5f, hdrMask = true,
                maxLights = 32, facing = true, occlusionSteps = 16, shadowMapSize = 1024,
            },
        };
    }

    [CreateAssetMenu(menuName = "Metal Raptors/Night Lighting Settings", fileName = "NightLightingSettings")]
    public class NightLightingSettings : ScriptableObject
    {
        public const string ResourceName = "NightLightingSettings";

        public NightPalette[] palettes = { NightPalette.Verdun(), NightPalette.Coast(), NightPalette.Alpine() };
        public NightLightingTier[] tiers = NightLightingTier.Defaults();
        [Range(0f, 1f)] public float airHaze = 0.4f;
        public string tierOverride = "";
        public bool previewInSceneView;
        public bool debugOverlay;

        static NightLightingSettings _loaded;

        public static NightLightingSettings Load()
        {
            if (_loaded != null) return _loaded;
            _loaded = Resources.Load<NightLightingSettings>(ResourceName);
            if (_loaded == null)
            {
                _loaded = CreateInstance<NightLightingSettings>();
                _loaded.name = ResourceName + " (defaults)";
            }
            return _loaded;
        }

        public NightPalette PaletteFor(TerrainKind map)
        {
            if (palettes == null || palettes.Length == 0) return NightPalette.Verdun();
            foreach (NightPalette palette in palettes)
                if (palette != null && palette.map == map) return palette;
            return palettes[0] ?? NightPalette.Verdun();
        }

        public NightLightingTier ResolveTier(string runtimeOverride)
        {
            if (tiers == null || tiers.Length == 0) tiers = NightLightingTier.Defaults();

            NightLightingTier picked = Find(runtimeOverride);
            if (picked == null) picked = Find(tierOverride);
            if (picked != null) return picked;

            string quality = QualitySettings.names.Length > 0
                ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                : "";
            foreach (NightLightingTier tier in tiers)
                if (tier != null && !string.IsNullOrEmpty(tier.qualityLevel) && tier.qualityLevel == quality)
                    return tier;

            picked = Find(Application.isMobilePlatform ? "Mobile High" : "Desktop");
            return picked ?? tiers[tiers.Length - 1];
        }

        NightLightingTier Find(string tierName)
        {
            if (string.IsNullOrEmpty(tierName)) return null;
            foreach (NightLightingTier tier in tiers)
                if (tier != null && tier.name == tierName) return tier;
            return null;
        }
    }
}
