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
}
