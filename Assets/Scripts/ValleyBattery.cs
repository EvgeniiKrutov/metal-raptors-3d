using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class ValleyBattery
    {
        const float FlakGapMin = 2f, FlakGapMax = 3f;
        const float EntryMargin = 120f;
        const float VillageGap = 90f;
        const float InViewShare = 0.8f;
        const int SeedStep = 211;

        const float MoundTop = 40f, MoundFalloff = 55f, MoundRise = 22f;
        const float EdgeMargin = 110f;

        public const float HillGap = 260f;
        public const float HillHalfX = 150f, HillHalfZ = 60f, HillFalloff = 130f;
        const float HillRise = 22f;
        public const float HillGunSpacing = 95f;
        const int HillGuns = 3;
        const int HillSeedShift = 100;
        const int GroundProbes = 3;

        const float HillGapMin = 2f, HillGapMax = 4f;
        const float BurstFromShare = 0.25f, BurstToShare = 0.55f;
        const float BurstLiftMin = 80f, BurstLiftMax = 200f;
        const float BurstSpread = 60f;
        const float BurstSizeMin = 40f, BurstSizeMax = 70f;
        const float BurstSoundChance = 0.5f;

        const float EscortGapMin = 0.8f, EscortGapMax = 1.6f;
        const float EscortBack = -120f, EscortAhead = 340f;
        const float EscortSide = 280f;
        const float EscortLow = -20f, EscortHigh = 170f;
        const float EscortClear = 110f;
        const float EscortGround = 80f;
        const int EscortTries = 6;

        readonly Transform _player;
        readonly CampaignTerrain _terrain;
        readonly float _playZ;
        readonly float _cruise;
        readonly float _planeLength;
        readonly int _seed;
        readonly List<AaGun> _guns = new List<AaGun>();
        readonly List<AaGun> _hill = new List<AaGun>();
        readonly List<float> _hillIn = new List<float>();
        int _placed;
        bool _armed;
        bool _hillArmed;
        bool _hillHeld;

        public ValleyBattery(Transform player, CampaignTerrain terrain, float playZ, float cruise,
            float planeLength, int seed)
        {
            _player = player;
            _terrain = terrain;
            _playZ = playZ;
            _cruise = cruise;
            _planeLength = planeLength;
            _seed = seed;
        }

        public bool Active => _guns.Count > 0;

        public IReadOnlyList<AaGun> Hill => _hill;

        public void HoldHill(bool on) => _hillHeld = on;

        public float CentreX { get; private set; }

        public bool Down
        {
            get
            {
                foreach (AaGun gun in _guns)
                    if (gun != null && !gun.Down) return false;
                return true;
            }
        }

        public bool InView(float camX, float halfWidth)
        {
            foreach (AaGun gun in _guns)
                if (gun != null && gun.X - camX > halfWidth * InViewShare) return false;
            return true;
        }

        public void Arm()
        {
            _armed = true;
            if (_hill.Count > 0) _hillArmed = true;
        }

        public void PlaceAhead(float camX, float halfWidth)
        {
            Forget();
            float x = Place(AaGun.ClearSpot(_terrain, camX + halfWidth + EntryMargin, _playZ));
            CentreX = x;
        }

        public static float HillReach => new Vector2(HillHalfX, HillHalfZ).magnitude + HillFalloff;

        public void PlaceBeside(float villageLeft, float villageRight, float villageZ, float arenaHalf,
            Vector2? hill = null)
        {
            Forget();
            CentreX = (villageLeft + villageRight) * 0.5f;
            float min = CentreX - arenaHalf + EdgeMargin;
            float max = CentreX + arenaHalf - EdgeMargin;
            PlaceOnMound(villageLeft - VillageGap, min, max);
            PlaceOnMound(villageRight + VillageGap, min, max);
            if (hill.HasValue) RaiseHill(hill.Value.x, hill.Value.y, true);
            else RaiseHill(villageRight + HillGap, (_playZ + villageZ) * 0.5f, false);
        }

        void PlaceOnMound(float x, float min, float max)
        {
            float spot = AaGun.ClearSpot(_terrain, Mathf.Clamp(x, min, max), _playZ, MoundTop + MoundFalloff,
                min, max);
            Mound(spot, _playZ, MoundTop, MoundTop, true, MoundFalloff, MoundRise);
            Place(spot);
        }

        void RaiseHill(float x, float z, bool exact)
        {
            if (_hill.Count > 0) return;

            float spot = exact ? x : AaGun.ClearSpot(_terrain, x, z, HillReach);
            Mound(spot, z, HillHalfX, HillHalfZ, false, HillFalloff, HillRise);

            for (int i = 0; i < HillGuns; i++)
            {
                float gx = spot + (i - (HillGuns - 1) * 0.5f) * HillGunSpacing;
                AaGun gun = AaGun.Place(gx, z, _player, _cruise, _planeLength,
                    _seed + (HillSeedShift + i) * SeedStep, lamp: false);
                gun.WreckLeft();
                _hill.Add(gun);
                _hillIn.Add(Random.Range(HillGapMin, HillGapMax) * (i + 1) * 0.5f);
            }
        }

        void Mound(float x, float z, float halfX, float halfZ, bool round, float falloff, float rise)
        {
            if (!(_terrain is VosgesTerrain valley) || valley.Layout == null) return;

            float ground = float.MinValue;
            for (int i = 0; i < GroundProbes; i++)
                for (int j = 0; j < GroundProbes; j++)
                {
                    float u = GroundProbes > 1 ? (float)i / (GroundProbes - 1) * 2f - 1f : 0f;
                    float v = GroundProbes > 1 ? (float)j / (GroundProbes - 1) * 2f - 1f : 0f;
                    ground = Mathf.Max(ground, valley.Layout.Ground(x + u * halfX, z + v * halfZ));
                }

            valley.Raise(new VosgesLayout.Pad
            {
                x = x,
                z = z,
                inverse = Quaternion.identity,
                halfX = halfX,
                halfZ = halfZ,
                round = round,
                falloff = falloff,
                y = ground + rise,
            });
        }

        float Place(float spot)
        {
            _placed++;
            AaGun gun = AaGun.Place(spot, _playZ, _player, _cruise, _planeLength,
                _seed + _placed * SeedStep, lamp: false);
            gun.ArmFlak(FlakGapMin, FlakGapMax);
            _guns.Add(gun);
            return spot;
        }

        public void OnBomb(Vector3 point, float radius)
        {
            foreach (AaGun gun in _guns)
            {
                if (gun == null || gun.Down || !gun.InBlast(point, radius)) continue;

                if (gun.Hurt)
                {
                    gun.Blow();
                    PilotProfile.Score(PilotRanks.AaGunPoints);
                }
                else
                {
                    gun.Damage();
                }
            }
        }

        public void BlowAll(bool quiet = false)
        {
            foreach (AaGun gun in _guns)
                if (gun != null && !gun.Down) gun.Blow(quiet);
        }

        public void Tick(bool quiet)
        {
            foreach (AaGun gun in _guns)
                if (gun != null) gun.Silence(quiet || !_armed);
        }

        public void TickHill(float dt, Vector3 listener, bool quiet, IReadOnlyList<Transform> escort = null)
        {
            if (!_hillArmed || _hillHeld || quiet || dt <= 0f) return;

            bool around = escort != null && escort.Count > 0 && escort[0] != null;
            for (int i = 0; i < _hill.Count; i++)
            {
                AaGun gun = _hill[i];
                if (gun == null || !gun.Grounded) continue;

                _hillIn[i] -= dt;
                if (_hillIn[i] > 0f) continue;
                _hillIn[i] = around
                    ? Random.Range(EscortGapMin, EscortGapMax)
                    : Random.Range(HillGapMin, HillGapMax);

                Vector3 at;
                if (around)
                {
                    if (!Around(escort, out at)) continue;
                    if (!gun.Shoot()) continue;
                }
                else
                {
                    if (!gun.Shoot()) continue;

                    Vector3 from = gun.Muzzle;
                    Vector3 to = _player != null ? _player.position : from + Vector3.up * BurstLiftMax;
                    at = Vector3.Lerp(from, to, Random.Range(BurstFromShare, BurstToShare))
                         + Vector3.up * Random.Range(BurstLiftMin, BurstLiftMax)
                         + Random.insideUnitSphere * BurstSpread;
                }

                FlakBurst.Spawn(at, Random.Range(BurstSizeMin, BurstSizeMax), listener,
                    Random.value < BurstSoundChance);
            }
        }

        bool Around(IReadOnlyList<Transform> planes, out Vector3 at)
        {
            Vector3 centre = Vector3.zero;
            int count = 0;
            foreach (Transform plane in planes)
            {
                if (plane == null) continue;
                centre += plane.position;
                count++;
            }

            at = Vector3.zero;
            if (count == 0) return false;
            centre /= count;

            Vector3 nose = planes[0].right;
            nose.y = 0f;
            nose = nose.sqrMagnitude > 1e-4f ? nose.normalized : Vector3.right;
            var side = new Vector3(-nose.z, 0f, nose.x);

            for (int tries = 0; tries < EscortTries; tries++)
            {
                at = centre
                     + nose * Random.Range(EscortBack, EscortAhead)
                     + side * Random.Range(-EscortSide, EscortSide)
                     + Vector3.up * Random.Range(EscortLow, EscortHigh);

                if (_terrain != null && _terrain.SampleHeight(at.x, at.z, out float ground))
                    at.y = Mathf.Max(at.y, ground + EscortGround);
                if (Clear(at, planes)) return true;
            }
            return false;
        }

        static bool Clear(Vector3 at, IReadOnlyList<Transform> planes)
        {
            foreach (Transform plane in planes)
                if (plane != null && (plane.position - at).sqrMagnitude < EscortClear * EscortClear) return false;
            return true;
        }

        public void Forget()
        {
            _armed = false;
            Tick(true);
            _guns.Clear();
        }
    }
}
