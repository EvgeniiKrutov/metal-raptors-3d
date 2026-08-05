using UnityEngine;

namespace MetalRaptors
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int SelectedPlaneIndex { get; private set; }

        public PlaneModelConfig SelectedPlane =>
            PlaneModels.All[Mathf.Clamp(SelectedPlaneIndex, 0, PlaneModels.All.Length - 1)];

        public static PlaneModelConfig CurrentPlane =>
            Instance != null ? Instance.SelectedPlane : PlaneModels.All[0];

        public float MasterVolume { get; private set; } = 1f;

        public int HighestUnlockedLevel { get; private set; } = 1;

        public Daytime Level1Daytime { get; private set; } = Daytime.Midday;

        public Daytime CampaignDaytime { get; private set; } = Daytime.Midday;

        const string PrefVolume = "mr_master_volume";
        const string PrefUnlocked = "mr_highest_unlocked_level";
        const string PrefPlane = "mr_selected_plane";
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

        public void SetSelectedPlane(int index)
        {
            SelectedPlaneIndex = Mathf.Clamp(index, 0, PlaneModels.All.Length - 1);
            PlayerPrefs.SetInt(PrefPlane, SelectedPlaneIndex);
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
            SelectedPlaneIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefPlane, 0),
                0, PlaneModels.All.Length - 1);
            int daytime = PlayerPrefs.GetInt(PrefLevel1Daytime, (int)Daytime.Midday);
            Level1Daytime = System.Enum.IsDefined(typeof(Daytime), daytime)
                ? (Daytime)daytime : Daytime.Midday;
            int campaign = PlayerPrefs.GetInt(PrefCampaignDaytime, (int)Daytime.Midday);
            CampaignDaytime = System.Enum.IsDefined(typeof(Daytime), campaign)
                ? (Daytime)campaign : Daytime.Midday;
        }
    }
}
