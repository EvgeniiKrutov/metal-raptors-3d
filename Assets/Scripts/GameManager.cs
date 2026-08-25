using UnityEngine;
using UnityEngine.SceneManagement;

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

        public static PlaneSkin CurrentSkin =>
            Instance != null ? Instance.SkinFor(Instance.SelectedPlane)
                             : PlaneSkins.Default(PlaneModels.All[0]);

        public float MasterVolume => AudioOptions.Master;

        public int HighestUnlockedLevel { get; private set; } = 1;

        public int CampaignLevelsCompleted { get; private set; }

        public Daytime Level1Daytime { get; private set; } = Daytime.Midday;

        public Daytime CampaignDaytime { get; private set; } = Daytime.Midday;

        const string PrefUnlocked = "mr_highest_unlocked_level";
        const string PrefCampaign = "mr_campaign_progress";
        const string PrefPlane = "mr_selected_plane";
        const string PrefSkinPrefix = "mr_plane_skin_";
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
            GraphicsOptions.Apply();
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => GraphicsOptions.Rescan();

        public void SetSelectedPlane(int index)
        {
            SelectedPlaneIndex = Mathf.Clamp(index, 0, PlaneModels.All.Length - 1);
            PlayerPrefs.SetInt(PrefPlane, SelectedPlaneIndex);
            PlayerPrefs.Save();
        }

        public PlaneSkin SkinFor(PlaneModelConfig plane)
        {
            if (plane == null) return null;

            string id = PlayerPrefs.GetString(PrefSkinPrefix + plane.resourceName, string.Empty);
            return PlaneSkins.ById(plane, id) ?? PlaneSkins.Default(plane);
        }

        public void SetSkin(PlaneModelConfig plane, PlaneSkin skin)
        {
            if (plane == null || skin == null) return;

            PlayerPrefs.SetString(PrefSkinPrefix + plane.resourceName, skin.id);
            PlayerPrefs.Save();
        }

        public void SetMasterVolume(float volume) => AudioOptions.SetMaster(volume);

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

        public void CompleteCampaignLevel(int level)
        {
            if (level <= CampaignLevelsCompleted) return;
            CampaignLevelsCompleted = Mathf.Clamp(level, 0, CampaignRun.LastLevel);
            PlayerPrefs.SetInt(PrefCampaign, CampaignLevelsCompleted);
            PlayerPrefs.Save();
        }

        public void UnlockLevel(int level)
        {
            if (level <= HighestUnlockedLevel) return;
            HighestUnlockedLevel = level;
            PlayerPrefs.SetInt(PrefUnlocked, HighestUnlockedLevel);
            PlayerPrefs.Save();
        }

        void ApplyAudio() => AudioOptions.Apply();

        void Load()
        {
            HighestUnlockedLevel = PlayerPrefs.GetInt(PrefUnlocked, 1);
            CampaignLevelsCompleted = Mathf.Clamp(PlayerPrefs.GetInt(PrefCampaign, 0),
                0, CampaignRun.LastLevel);
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
