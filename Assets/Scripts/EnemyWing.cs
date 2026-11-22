using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MetalRaptors
{
    public enum ApproachSector { None, AboveFront, AboveBehind, HeadOn }

    public static class EnemyWing
    {
        public const float TailHandover = 60f;

        static readonly List<EnemyController> Members = new List<EnemyController>();
        static readonly Dictionary<EnemyController, ApproachSector> Sectors =
            new Dictionary<EnemyController, ApproachSector>();
        static readonly List<EnemyController> Stale = new List<EnemyController>();
        static readonly ApproachSector[] Pool = new ApproachSector[3];

        static EnemyController _tail;
        static EnemyController _bait;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            Clear();
            SceneManager.sceneUnloaded -= OnUnloaded;
            SceneManager.sceneUnloaded += OnUnloaded;
        }

        static void OnUnloaded(Scene scene) => Clear();

        public static void Join(EnemyController enemy)
        {
            if (enemy != null && !Members.Contains(enemy)) Members.Add(enemy);
        }

        public static void Leave(EnemyController enemy)
        {
            Release(enemy);
            Members.Remove(enemy);
        }

        public static bool ClaimTail(EnemyController enemy)
        {
            if (enemy == null) return false;
            if (_tail != null && (!Valid(_tail) || !_tail.HoldsTail)) _tail = null;

            if (_tail == enemy) return true;
            if (_tail != null && enemy.TargetRange >= _tail.TargetRange - TailHandover) return false;

            _tail = enemy;
            return true;
        }

        public static bool HasTail(EnemyController enemy) => enemy != null && _tail == enemy;

        public static void ReleaseTail(EnemyController enemy)
        {
            if (enemy != null && _tail == enemy) _tail = null;
        }

        public static EnemyController Bait => Valid(_bait) ? _bait : null;

        public static bool ClaimBait(EnemyController enemy)
        {
            if (enemy == null) return false;
            if (_bait != null && !Valid(_bait)) _bait = null;

            if (_bait == enemy) return true;
            if (_bait != null) return false;

            _bait = enemy;
            return true;
        }

        public static void HandBait(EnemyController enemy)
        {
            if (Valid(enemy)) _bait = enemy;
        }

        public static void ReleaseBait(EnemyController enemy)
        {
            if (enemy != null && _bait == enemy) _bait = null;
        }

        public static int Count(PilotLevel level)
        {
            int count = 0;
            for (int i = Members.Count - 1; i >= 0; i--)
            {
                EnemyController member = Members[i];
                if (member == null)
                {
                    Members.RemoveAt(i);
                    continue;
                }
                if (member.Free && member.Pilot == level) count++;
            }
            return count;
        }

        public static ApproachSector ClaimSector(EnemyController enemy, bool headOn)
        {
            if (enemy == null) return ApproachSector.None;

            Sectors.Remove(enemy);
            Prune();

            int count = 0;
            if (!Taken(ApproachSector.AboveFront)) Pool[count++] = ApproachSector.AboveFront;
            if (!Taken(ApproachSector.AboveBehind)) Pool[count++] = ApproachSector.AboveBehind;
            if (headOn && !Taken(ApproachSector.HeadOn)) Pool[count++] = ApproachSector.HeadOn;

            if (count == 0)
            {
                Pool[count++] = ApproachSector.AboveFront;
                Pool[count++] = ApproachSector.AboveBehind;
                if (headOn) Pool[count++] = ApproachSector.HeadOn;
            }

            ApproachSector pick = Pool[Random.Range(0, count)];
            Sectors[enemy] = pick;
            return pick;
        }

        public static void ReleaseSector(EnemyController enemy)
        {
            if (enemy != null) Sectors.Remove(enemy);
        }

        public static void Release(EnemyController enemy)
        {
            ReleaseTail(enemy);
            ReleaseBait(enemy);
            ReleaseSector(enemy);
        }

        public static void Clear()
        {
            _tail = null;
            _bait = null;
            Sectors.Clear();
            Members.Clear();
        }

        static bool Taken(ApproachSector sector)
        {
            foreach (KeyValuePair<EnemyController, ApproachSector> pair in Sectors)
                if (pair.Value == sector && Valid(pair.Key)) return true;
            return false;
        }

        static void Prune()
        {
            Stale.Clear();
            foreach (KeyValuePair<EnemyController, ApproachSector> pair in Sectors)
                if (!Valid(pair.Key)) Stale.Add(pair.Key);
            foreach (EnemyController enemy in Stale) Sectors.Remove(enemy);
            Stale.Clear();
        }

        static bool Valid(EnemyController enemy) => enemy != null && enemy.Free;
    }
}
