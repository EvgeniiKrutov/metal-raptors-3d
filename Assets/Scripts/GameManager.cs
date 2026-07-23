using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Persistent game state that survives scene loads (DontDestroyOnLoad).
    ///
    /// Holds three kinds of cross-scene data, matching the design:
    ///   * Selected loadout  - the mech chosen in the Garage, read in Battlefield/levels.
    ///                         Kept in memory + remembered via PlayerPrefs.
    ///   * Audio settings    - master volume, applied to the AudioListener + persisted.
    ///   * Progress / unlocks- which levels are unlocked, persisted via PlayerPrefs.
    ///
    /// It bootstraps itself before the first scene loads, so it exists no matter which
    /// scene you press Play in (menu, a level, the garage, whatever).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ---- Loadout (selected in the Garage, read in Battlefield/levels) ----
        public readonly string[] AvailableMechs = { "Raptor MK-I", "Raptor MK-II", "Raptor MK-III" };

        // Each mech is a differently coloured cube; the chosen colour flies in the levels.
        public readonly Color[] CubeColors =
        {
            new Color(0.85f, 0.25f, 0.20f), // red
            new Color(0.30f, 0.78f, 0.35f), // green
            new Color(0.25f, 0.50f, 0.92f), // blue
        };

        public int SelectedMechIndex { get; private set; }
        public string SelectedMech => AvailableMechs[Mathf.Clamp(SelectedMechIndex, 0, AvailableMechs.Length - 1)];
        public Color SelectedColor => CubeColors[Mathf.Clamp(SelectedMechIndex, 0, CubeColors.Length - 1)];

        // ---- Audio / settings (persisted) ----
        public float MasterVolume { get; private set; } = 1f;

        // ---- Progress / unlocks (persisted) ----
        public int HighestUnlockedLevel { get; private set; } = 1;

        // ---- Level 1 weather/daytime (picked on level select, persisted) ----
        public Daytime Level1Daytime { get; private set; } = Daytime.Midday;

        // ---- Campaign weather/daytime (picked on the campaign panel, persisted) ----
        public Daytime CampaignDaytime { get; private set; } = Daytime.Midday;

        const string PrefVolume = "mr_master_volume";
        const string PrefUnlocked = "mr_highest_unlocked_level";
        const string PrefMech = "mr_selected_mech";
        const string PrefLevel1Daytime = "mr_level1_daytime";
        const string PrefCampaignDaytime = "mr_campaign_daytime";

        // Ensures a GameManager exists even when you press Play directly in a non-menu scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
            ApplyAudio();
        }

        // ---- Loadout API ----
        public void SetSelectedMech(int index)
        {
            SelectedMechIndex = Mathf.Clamp(index, 0, AvailableMechs.Length - 1);
            PlayerPrefs.SetInt(PrefMech, SelectedMechIndex);
            PlayerPrefs.Save();
        }

        // ---- Audio API ----
        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            ApplyAudio();
            PlayerPrefs.SetFloat(PrefVolume, MasterVolume);
            PlayerPrefs.Save();
        }

        // ---- Level 1 weather API ----
        public void SetLevel1Daytime(Daytime daytime)
        {
            Level1Daytime = daytime;
            PlayerPrefs.SetInt(PrefLevel1Daytime, (int)daytime);
            PlayerPrefs.Save();
        }

        public void SetCampaignDaytime(Daytime daytime)
        {
            CampaignDaytime = daytime;
            PlayerPrefs.SetInt(PrefCampaignDaytime, (int)daytime);
            PlayerPrefs.Save();
        }

        // ---- Progress API ----
        public bool IsLevelUnlocked(int level) => level <= HighestUnlockedLevel;

        public void UnlockLevel(int level)
        {
            if (level <= HighestUnlockedLevel) return;
            HighestUnlockedLevel = level;
            PlayerPrefs.SetInt(PrefUnlocked, HighestUnlockedLevel);
            PlayerPrefs.Save();
        }

        void ApplyAudio() => AudioListener.volume = MasterVolume;

        void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(PrefVolume, 1f);
            HighestUnlockedLevel = PlayerPrefs.GetInt(PrefUnlocked, 1);
            SelectedMechIndex = PlayerPrefs.GetInt(PrefMech, 0);
            int daytime = PlayerPrefs.GetInt(PrefLevel1Daytime, (int)Daytime.Midday);
            Level1Daytime = System.Enum.IsDefined(typeof(Daytime), daytime)
                ? (Daytime)daytime : Daytime.Midday;
            int campaign = PlayerPrefs.GetInt(PrefCampaignDaytime, (int)Daytime.Midday);
            CampaignDaytime = System.Enum.IsDefined(typeof(Daytime), campaign)
                ? (Daytime)campaign : Daytime.Midday;
        }
    }
}
