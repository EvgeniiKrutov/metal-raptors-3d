using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class PlaneSnapshot
    {
        public PlaneModelConfig plane;
        public Vector3 position;
        public float heading;
        public float health;
    }

    public class VehicleSnapshot
    {
        public EnemyKind kind;
        public float x;
        public float health;
    }

    public class ZeppelinSnapshot
    {
        public Vector3 position;
        public float baseY;
        public float bobTime;
        public bool hanging;
        public float health;
    }

    public class CampaignSnapshot
    {
        public int level;
        public int step;
        public bool warnedFirst;
        public bool warnedPair;

        public Vector3 playerPosition;
        public float playerHeading;
        public float playerHealth;
        public float bombCooldown;
        public float boostCooldown;
        public float rollCooldown;

        public Vector3 camera;

        public float airfieldHealth = Airfield.MaxHealth;
        public int suppliesLeft;
        public bool supplyOpen;
        public PlaneModelConfig companionFoe;

        public readonly List<PlaneSnapshot> enemies = new List<PlaneSnapshot>();
        public readonly List<VehicleSnapshot> vehicles = new List<VehicleSnapshot>();
        public ZeppelinSnapshot zeppelin;
    }

    public static class CampaignCheckpoint
    {
        public const float RestoreOutSec = 0.5f;
        public const float RestoreHoldSec = 1f;
        public const float RestoreInSec = 0.6f;

        static CampaignSnapshot _saved;
        static bool _restore;
        static bool _replay;

        public static bool InCareer { get; set; }

        public static bool Available =>
            InCareer && _saved != null && _saved.level == CampaignRun.Level;

        public static void Save(CampaignSnapshot snapshot) => _saved = snapshot;

        public static void Clear() => _saved = null;

        public static void RequestRestore()
        {
            _restore = true;
            _replay = true;
        }

        public static void RequestReplay()
        {
            _restore = false;
            _replay = true;
            _saved = null;
        }

        public static CampaignSnapshot TakeRestore(int level)
        {
            bool wanted = _restore;
            _restore = false;
            return wanted && _saved != null && _saved.level == level ? _saved : null;
        }

        public static bool TakeReplay()
        {
            bool wanted = _replay;
            _replay = false;
            return wanted;
        }
    }
}
