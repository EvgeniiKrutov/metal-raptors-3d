namespace MetalRaptors
{
    public interface IDevSpawnHost
    {
        bool CanDevSpawn { get; }

        void DevSpawnPlane(EnemyRole role);
    }

    public static class DevSpawn
    {
        public const float Delay = 1.5f;

        static IDevSpawnHost _host;

        public static bool Available => _host != null && _host.CanDevSpawn;

        public static void Register(IDevSpawnHost host) => _host = host;

        public static void Unregister(IDevSpawnHost host)
        {
            if (_host == host) _host = null;
        }

        public static void Spawn(EnemyRole role)
        {
            if (Available) _host.DevSpawnPlane(role);
        }
    }
}
