using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class VosgesScenery : MonoBehaviour
    {
        const float WindowMargin = 300f;
        const float FieldOfViewSlack = 1.15f;

        class Band
        {
            public string name;
            public float cell;
            public float farZ;
            public bool heavy;
            public System.Func<int, GameObject> spawn;
            public readonly Dictionary<int, GameObject> live = new Dictionary<int, GameObject>();
            public readonly List<int> scratch = new List<int>();
        }

        Camera _cam;
        VosgesLayout _layout;
        bool _day;
        Band[] _bands;
        readonly List<GameObject> _sources = new List<GameObject>();

        public static VosgesScenery Begin(Camera cam, VosgesLayout layout, bool day)
        {
            if (cam == null || layout == null) return null;

            var scenery = new GameObject("Vosges Scenery").AddComponent<VosgesScenery>();
            scenery._cam = cam;
            scenery._layout = layout;
            scenery._day = day;
            var bands = new List<Band>
            {
                new Band
                {
                    name = "Villages", cell = layout.VillageCell, farZ = VosgesLayout.VillageBackZ,
                    heavy = true, spawn = scenery.SpawnVillage,
                },
                new Band
                {
                    name = "Windmills", cell = layout.MillCell, farZ = VosgesLayout.MillZMax,
                    spawn = scenery.SpawnMill,
                },
                new Band
                {
                    name = "Houses", cell = layout.HouseCell, farZ = VosgesLayout.HouseZMax,
                    heavy = true, spawn = scenery.SpawnHouses,
                },
                new Band
                {
                    name = "Herds", cell = layout.HerdCell, farZ = VosgesLayout.HerdZMax,
                    spawn = scenery.SpawnHerd,
                },
            };

            for (int b = 0; b < VosgesLayout.GroveBands; b++)
            {
                int band = b;
                bands.Add(new Band
                {
                    name = $"Spruces {band}", cell = layout.GroveCell(band), farZ = layout.BandFarZ(band),
                    heavy = true, spawn = cell => scenery.SpawnGroves(band, cell),
                });
            }
            scenery._bands = bands.ToArray();

            scenery.Refresh(true);
            return scenery;
        }

        void LateUpdate() => Refresh(false);

        void Refresh(bool all)
        {
            if (_cam == null) return;

            Vector3 eye = _cam.transform.position;
            float slope = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * _cam.aspect * FieldOfViewSlack;
            bool spawnedHeavy = false;

            foreach (var band in _bands)
            {
                float half = (band.farZ - eye.z) * slope + WindowMargin;
                int first = Mathf.FloorToInt((eye.x - half) / band.cell) - 1;
                int last = Mathf.FloorToInt((eye.x + half) / band.cell) + 1;

                band.scratch.Clear();
                foreach (var kv in band.live)
                    if (kv.Key < first - 1 || kv.Key > last + 1) band.scratch.Add(kv.Key);
                foreach (int cell in band.scratch)
                {
                    if (band.live[cell] != null) Destroy(band.live[cell]);
                    band.live.Remove(cell);
                }

                for (int cell = first; cell <= last; cell++)
                {
                    if (band.live.ContainsKey(cell)) continue;
                    if (band.heavy && spawnedHeavy && !all) continue;

                    band.live[cell] = band.spawn(cell);
                    if (band.heavy) spawnedHeavy = true;
                }
            }
        }

        Transform Site(string name, int cell)
        {
            var site = new GameObject($"{name} {cell}");
            site.layer = BattlefieldProps.Layer;
            site.transform.SetParent(transform, false);
            return site.transform;
        }

        GameObject SpawnVillage(int cell)
        {
            if (!_layout.Village(cell, out var spot)) return null;

            Transform site = Site("Village", cell);
            Transform village = VosgesVillage.Place(site, spot);
            if (village == null) return site.gameObject;

            if (village.childCount > 0)
            {
                _sources.Clear();
                _sources.Add(village.GetChild(0).gameObject);
                MeshMerge.Collapse(village, _sources, "Village");
            }

            if (_day) VillagePeople.Begin(site, village, VosgesVillage.Graph(spot.model), spot.seed);
            return site.gameObject;
        }

        GameObject SpawnMill(int cell)
        {
            if (!_layout.Mill(cell, out var spot)) return null;

            Transform mill = VosgesWindmill.Place(transform, spot);
            return mill != null ? mill.gameObject : null;
        }

        GameObject SpawnHouses(int cell)
        {
            var houses = _layout.Houses(cell);
            if (houses == null || houses.Length == 0) return null;

            Transform site = Site("Houses", cell);
            foreach (var house in houses) VosgesHouse.Place(site, house);
            CollapseChildren(site, "Houses");
            return site.gameObject;
        }

        GameObject SpawnHerd(int cell)
        {
            if (!_day || !_layout.Herd(cell, out var herd) || herd.cows == null) return null;

            Transform site = Site("Herd", cell);
            foreach (var cow in herd.cows) VosgesCow.Place(site, cow);
            CollapseChildren(site, "Herd");
            return site.gameObject;
        }

        GameObject SpawnGroves(int band, int cell)
        {
            var groves = _layout.Groves(band, cell);
            if (groves == null || groves.Length == 0) return null;

            Transform site = Site($"Spruces {band}", cell);
            foreach (var grove in groves)
                if (grove.trees != null)
                    foreach (var tree in grove.trees) VosgesSpruce.Place(site, tree);

            CollapseChildren(site, "Spruces");
            return site.gameObject;
        }

        void CollapseChildren(Transform site, string name)
        {
            _sources.Clear();
            for (int i = 0; i < site.childCount; i++) _sources.Add(site.GetChild(i).gameObject);
            if (_sources.Count > 0) MeshMerge.Collapse(site, _sources, name);
            _sources.Clear();
        }
    }
}
