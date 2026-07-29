using UnityEngine;

namespace MetalRaptors
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public readonly string[] AvailableMechs = { "Raptor MK-I", "Raptor MK-II", "Raptor MK-III" };

        public readonly Color[] CubeColors =
        {
            new Color(0.85f, 0.25f, 0.20f),
            new Color(0.30f, 0.78f, 0.35f),
            new Color(0.25f, 0.50f, 0.92f),
        };

        public int SelectedMechIndex { get; private set; }
        public string SelectedMech => AvailableMechs[Mathf.Clamp(SelectedMechIndex, 0, AvailableMechs.Length - 1)];
        public Color SelectedColor => CubeColors[Mathf.Clamp(SelectedMechIndex, 0, CubeColors.Length - 1)];

        public float MasterVolume { get; private set; } = 1f;

        public int HighestUnlockedLevel { get; private set; } = 1;

        public Daytime Level1Daytime { get; private set; } = Daytime.Midday;

        public Daytime CampaignDaytime { get; private set; } = Daytime.Midday;

        const string PrefVolume = "mr_master_volume";
        const string PrefUnlocked = "mr_highest_unlocked_level";
        const string PrefMech = "mr_selected_mech";
        const string PrefLevel1Daytime = "mr_level1_daytime";
        const string PrefCampaignDaytime = "mr_campaign_daytime";

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

        public void SetSelectedMech(int index)
        {
            SelectedMechIndex = Mathf.Clamp(index, 0, AvailableMechs.Length - 1);
            PlayerPrefs.SetInt(PrefMech, SelectedMechIndex);
            PlayerPrefs.Save();
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            ApplyAudio();
            PlayerPrefs.SetFloat(PrefVolume, MasterVolume);
            PlayerPrefs.Save();
        }

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
