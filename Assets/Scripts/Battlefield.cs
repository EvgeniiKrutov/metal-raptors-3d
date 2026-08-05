using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class Battlefield : MonoBehaviour
    {
        const float BlastZMin = 15f, BlastZMax = 700f;
        const float BlastIntervalMin = 1.8f, BlastIntervalMax = 3.2f;
        const float BlastSizeMin = 45f, BlastSizeMax = 90f;
        const float BlastSpread = 1.15f;
        const float BlastKillRadii = 1f;

        const float SmokeCellSize = 600f;
        const float SmokeCellChance = 0.75f;
        const float SmokeZMin = 140f, SmokeZMax = 380f;
        const float SmokeMargin = 500f;

        Camera _cam;
        float _halfViewWidth;
        float _minX, _maxX;
        int _seed;
        float _blastTimer;

        readonly List<Terrain> _terrains = new List<Terrain>();
        readonly Dictionary<int, SmokeColumn> _columns = new Dictionary<int, SmokeColumn>();
        readonly List<int> _columnScratch = new List<int>();
        BattlefieldPeople _people;
        BattlefieldProps _props;
        System.Func<float, float, bool> _inCrater;

        public float HalfViewWidth => _halfViewWidth;
        public float CameraX => _cam != null ? _cam.transform.position.x : 0f;
        public float MinX => _minX;
        public float MaxX => _maxX;
        public bool Bounded => !float.IsInfinity(_minX) && !float.IsInfinity(_maxX);
        public BattlefieldProps Props => _props;

        public static Battlefield Begin(Camera cam, float halfViewWidth, int seed,
            System.Func<float, float, bool> inCrater)
            => Begin(cam, halfViewWidth, seed, float.NegativeInfinity, float.PositiveInfinity, inCrater);

        public static Battlefield Begin(Camera cam, float halfViewWidth, int seed,
            float minX, float maxX, System.Func<float, float, bool> inCrater)
        {
            if (cam == null) return null;

            var field = new GameObject("Battlefield").AddComponent<Battlefield>();
            field._cam = cam;
            field._halfViewWidth = halfViewWidth;
            field._minX = minX;
            field._maxX = maxX;
            field._seed = seed;
            field._inCrater = inCrater;
            field._blastTimer = Random.Range(0f, BlastIntervalMax);

            field.RefreshTerrains();
            field.UpdateColumns(field.CameraX);
            field._props = BattlefieldProps.Begin(field, seed);
            field._people = BattlefieldPeople.Begin(field);
            return field;
        }

        public bool InCrater(float x, float z) => _inCrater != null && _inCrater(x, z);

        public bool SampleGround(float x, float z, out float y)
        {
            for (int i = 0; i < _terrains.Count; i++)
            {
                var terrain = _terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (x < origin.x || x > origin.x + size.x) continue;
                if (z < origin.z || z > origin.z + size.z) continue;

                y = terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
                return true;
            }

            y = 0f;
            return false;
        }

        void LateUpdate()
        {
            if (_cam == null) return;

            RefreshTerrains();

            float camX = _cam.transform.position.x;
            UpdateColumns(camX);
            TickBlasts(camX);
            if (_props != null) _props.Tick(camX);
            if (_people != null) _people.Tick(camX, Time.deltaTime);
        }

        void RefreshTerrains() => Terrain.GetActiveTerrains(_terrains);

        void TickBlasts(float camX)
        {
            _blastTimer -= Time.deltaTime;
            if (_blastTimer > 0f) return;
            _blastTimer = Random.Range(BlastIntervalMin, BlastIntervalMax);

            float x = camX + Random.Range(-1f, 1f) * _halfViewWidth * BlastSpread;
            float z = Random.Range(BlastZMin, BlastZMax);
            if (!SampleGround(x, z, out float y)) return;

            float size = Random.Range(BlastSizeMin, BlastSizeMax);
            var position = new Vector3(x, y, z);

            GroundBlast.Spawn(position, size, _cam.transform.position);
            if (_people != null) _people.KillWithin(position, size * BlastKillRadii);
        }

        void UpdateColumns(float camX)
        {
            int first = Mathf.FloorToInt((camX - _halfViewWidth - SmokeMargin) / SmokeCellSize);
            int last = Mathf.FloorToInt((camX + _halfViewWidth + SmokeMargin) / SmokeCellSize);

            _columnScratch.Clear();
            foreach (var kv in _columns)
                if (kv.Key < first || kv.Key > last) _columnScratch.Add(kv.Key);

            foreach (int cell in _columnScratch)
            {
                var column = _columns[cell];
                if (column != null) Destroy(column.gameObject);
                _columns.Remove(cell);
            }

            for (int cell = first; cell <= last; cell++)
            {
                if (_columns.ContainsKey(cell)) continue;

                int hash = Hash(_seed, cell);
                var rng = new System.Random(hash);
                bool wanted = rng.NextDouble() <= SmokeCellChance;
                float x = (cell + (float)rng.NextDouble()) * SmokeCellSize;
                float z = Mathf.Lerp(SmokeZMin, SmokeZMax, (float)rng.NextDouble());

                if (!wanted) { _columns[cell] = null; continue; }
                if (!SampleGround(x, z, out float y)) continue;

                _columns[cell] = SmokeColumn.Begin(transform, new Vector3(x, y, z), hash);
            }
        }

        static int Hash(int seed, int cell)
        {
            unchecked
            {
                int h = seed;
                h = h * 486187739 + cell;
                h = h * 486187739 + 7;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
