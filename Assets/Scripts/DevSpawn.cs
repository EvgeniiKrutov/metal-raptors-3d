namespace MetalRaptors
{
    public interface IDevSpawnHost
    {
        bool CanDevSpawn { get; }

        void DevSpawnPlane(EnemyRole role);

        void DevSpawnTruck();

        void DevSpawnTank();

        bool OffersZeppelin { get; }

        bool ZeppelinAloft { get; }

        void DevSpawnZeppelin();
    }

    public static class DevSpawn
    {
        public const float Delay = 1.5f;

        static IDevSpawnHost _host;

        public static bool Available => _host != null && _host.CanDevSpawn;

        public static bool ZeppelinOffered => Available && _host.OffersZeppelin;

        public static bool ZeppelinReady => ZeppelinOffered && !_host.ZeppelinAloft;

        public static void Register(IDevSpawnHost host) => _host = host;

        public static void Unregister(IDevSpawnHost host)
        {
            if (_host == host) _host = null;
        }

        public static void Spawn(EnemyRole role)
        {
            if (Available) _host.DevSpawnPlane(role);
        }

        public static void SpawnTruck()
        {
            if (Available) _host.DevSpawnTruck();
        }

        public static void SpawnTank()
        {
            if (Available) _host.DevSpawnTank();
        }

        public static void SpawnZeppelin()
        {
            if (ZeppelinReady) _host.DevSpawnZeppelin();
        }
    }

    public static class DevSkip
    {
        static System.Action _skip;

        public static bool Available => _skip != null;

        public static string Caption { get; private set; }

        public static void Offer(string caption, System.Action skip)
        {
            Caption = caption;
            _skip = skip;
        }

        public static void Withdraw(System.Action skip)
        {
            if (_skip == skip) _skip = null;
        }

        public static void Run()
        {
            System.Action skip = _skip;
            _skip = null;
            skip?.Invoke();
        }
    }

    public static class DevJump
    {
        static System.Action _jump;

        public static bool Available => _jump != null;

        public static string Caption { get; private set; }

        public static void Offer(string caption, System.Action jump)
        {
            Caption = caption;
            _jump = jump;
        }

        public static void Withdraw(System.Action jump)
        {
            if (_jump == jump) _jump = null;
        }

        public static void Run()
        {
            System.Action jump = _jump;
            _jump = null;
            jump?.Invoke();
        }
    }

    public static class DevFinalJump
    {
        static System.Action _jump;

        public static bool Available => _jump != null;

        public static string Caption { get; private set; }

        public static void Offer(string caption, System.Action jump)
        {
            Caption = caption;
            _jump = jump;
        }

        public static void Withdraw(System.Action jump)
        {
            if (_jump == jump) _jump = null;
        }

        public static void Run()
        {
            System.Action jump = _jump;
            _jump = null;
            jump?.Invoke();
        }
    }

    public static class DevCareerReset
    {
        static System.Action _reset;

        public static bool Available => _reset != null;

        public static void Offer(System.Action reset) => _reset = reset;

        public static void Withdraw(System.Action reset)
        {
            if (_reset == reset) _reset = null;
        }

        public static void Run() => _reset?.Invoke();
    }

    public static class DevCheats
    {
        public static bool Invulnerable { get; set; }

        public static bool HighDamage { get; set; }

        public static bool FastSpeed { get; set; }

        public static bool FastReload { get; set; }

        public static bool ShowHitboxes { get; set; }

        public static float DamageFactor => HighDamage ? 10f : 1f;

        public static float SpeedFactor => FastSpeed ? 1.5f : 1f;

        public static float FireIntervalFactor => FastReload ? 0.5f : 1f;

        public static float CooldownFactor => FastReload ? 4f : 1f;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Invulnerable = false;
            HighDamage = false;
            FastSpeed = false;
            FastReload = false;
            ShowHitboxes = false;
        }
    }

    public static class DevFreeze
    {
        static System.Func<bool> _host;

        public static bool Active { get; private set; }

        public static bool CanFreeze => _host != null && _host() && !LevelOutro.IsOpen;

        public static void Offer(System.Func<bool> allowed) => _host = allowed;

        public static void Withdraw(System.Func<bool> allowed)
        {
            if (_host == allowed) _host = null;
        }

        public static void Begin()
        {
            Active = true;
            UnityEngine.Time.timeScale = 0f;
        }

        public static void End()
        {
            if (!Active) return;

            Active = false;
            if (!GameMenu.IsOpen && !LevelBriefing.IsOpen && !LevelOutro.IsOpen) CutscenePause.Restore();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear()
        {
            Active = false;
            _host = null;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() =>
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => Active = false;
    }
}
