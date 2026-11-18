using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace MetalRaptors
{
    public class ShotDirector : MonoBehaviour
    {
        const string Flag = "-shot";
        const float StepSec = 1f / 60f;
        const float PathStep = 1f / 240f;
        const float DefaultSpeed = 175f;
        const float DefaultFireFor = 0.7f;
        const float TrailScale = 0.7f;
        const float HitScale = 0.35f;
        const float DefaultSmokeAlpha = 0.6f;
        const float StrikeSpread = 0.15f;
        const int SplinterCount = 4;
        const float SplinterScale = 0.8f;

        const float BoltSpeed = 430f;
        const float BoltLength = 9f;
        const float BoltThickness = 1.3f;
        const float BoltEmission = 2.4f;
        const float BoltLife = 1.2f;
        const float BoltOvershoot = 0.15f;
        const float BoltInterval = 0.05f;
        const float BoltSpread = 0.012f;
        const float GunSpacing = 1.4f;
        const float CamelGunSpacing = 0.8f;
        const float TriplaneGunSpacing = 0.9f;
        const float GunFlashScale = 0.3f;
        const float CaptureFlashScale = 0.45f;

        const float RifleIntervalMin = 0.3f, RifleIntervalMax = 0.8f;
        const float RifleScatter = 35f;
        const float ShoulderHeight = 8.6f;
        const float RifleLength = 8f;
        const float RifleWidth = 0.55f;
        const float RifleFlashSize = 10f;
        const float RifleFlashShare = 0.4f;
        const float ChestHeight = 7.2f;
        const float FoeScatterLow = -2.5f, FoeScatterHigh = 3.5f;
        const float CoverSink = 0.3f;
        const float DefaultCoverAngle = 45f;
        const float WallGap = 4f;
        const float WallPack = 0.9f;
        const float WallTwist = 20f, TopTwist = 30f;
        const float WallTilt = 5f;
        const float WallSlide = 0.5f, WallBulge = 0.6f;
        const float StackLift = 0.75f;
        const float BagSizeJitter = 0.1f;
        const float SoldierGap = 5f;
        const int SoldierTries = 32;

        const float CraterReach = 1.8f;
        const float CraterRim = 0.4f;
        const float CraterRimSigma = 0.32f;
        const float DefaultCraterRadius = 40f;
        const float DefaultCraterDepth = 0.3f;

        const int PropGhosts = 12;
        const float PropArc = 100f;
        const float PropGhostAlpha = 0.4f;
        const float PropGhostFalloff = 1.2f;
        const float PropBladeShare = 0.45f;
        const float PropDiscAlpha = 0.11f;
        const float StreakWidth = 2.1f;
        const float StreakLife = 0.55f;
        const float StreakAlpha = 0.8f;
        const float StreakGlow = 0.9f;
        const float StreakSweepBack = 0.12f;
        const int StreakStride = 4;
        const float PropTipAlpha = 0.09f;
        const float PropTipInner = 0.84f;
        const float PropDiscFit = 0.97f;
        const int PropDiscSegments = 48;

        const float MistMargin = 1600f;
        const float DefaultCloudWidth = 160f;
        const float DefaultCloudAlpha = 0.5f;
        const int CloudBlobs = 7;

        const float ShardGravity = 110f;
        const float ShardScatter = 45f;
        const float ShardSizeMin = 1.2f, ShardSizeMax = 3.4f;

        const float DefaultZeppelinLength = 600f;
        const float DefaultZeppelinDistance = 520f;

        const float FireballLift = 0.25f;
        const float WreckSmokeLift = 0.35f;

        const float RearPivotFromBreech = 0.21f;
        const float RearPivotHeight = 0.54f;
        const float RearMuzzleHeight = 0.84f;
        const float RearMuzzleClearance = 3f;
        const float RearSpacingShare = 0.25f;

        const float GunHold = 999f;
        const float DefaultSplashSize = 80f;
        const float DefaultBunkerSink = 0.6f;
        const float BoatSize = 1.584f;
        const float DefaultBoatSink = 0.08f;
        const float BoatBleach = 0.1f;
        const float GroyneDrop = 6f;
        const float ShoreProbeStep = 5f;
        const float ShoreProbeFar = 700f;

        const string GullModel = "objects/seagull";
        const string GullBeak = "Beak";
        const float GullSize = 3f;
        const float GullJitter = 0.1f;
        const float DefaultGullDistance = 500f;
        const float DefaultGullSpread = 60f;
        const float DefaultGullBank = 30f;
        const float GullYawJitter = 12f, GullBankJitter = 6f, GullPitchJitter = 6f;
        const float GullGap = 0.35f;
        const int GullTries = 12;

        const float CowMaxTilt = 18f;
        const float NormalProbe = 3f;
        const float DefaultGroveSpread = 120f;
        const float GroveGap = 4.2f * PlaneModelConfig.UnitsPerMeter;
        const float GroveClear = 2.2f * PlaneModelConfig.UnitsPerMeter;
        const float GroveJitter = 0.12f;
        const int GroveTries = 24;
        const float ValleyLogReach = 4000f;
        const float DefaultPuffWidth = 50f;
        const float DefaultPuffAlpha = 0.7f;
        const float PuffGlow = 0.25f;
        const float PuffReach = 0.24f;
        const int PuffBlobs = 5;
        const float PuffHaloReach = 0.42f;
        const int PuffHaloBlobs = 5;
        const float PuffHaloAlpha = 0.4f;
        const float PuffHaloLighten = 1.9f;
        const float VillagerTurn = 25f;
        const float MillSink = 0.15f * PlaneModelConfig.UnitsPerMeter;
        const float HouseSink = 0.1f * PlaneModelConfig.UnitsPerMeter;
        const int FloorProbes = 8;
        const float FloorReach = 0.7f;
        static readonly Color PuffColor = new Color(0.16f, 0.15f, 0.15f);

        const float FocusPriority = 1000f;
        const float ArenaAltitude = 3000f;
        const float ProbeY = -5000f;
        const string PropFolder = "objects/";
        const string AerodromeFires = "Aerodrome Fires";
        const uint FillLayer = 1u << 7;
        const float HangarInset = 0.94f;
        const float HangarShadeHeight = 0.56f;
        static readonly Color HangarShade = new Color(0.05f, 0.04f, 0.05f);
        const float NodeMuzzleClearance = 1.5f;
        const float DefaultBoltDistance = 1100f;
        const float DefaultBoltHeight = 900f;
        const float DefaultRainLength = 34f;
        const float DefaultRainWidthPx = 1.6f;
        const float DefaultRainAlpha = 0.25f;
        const float DefaultRainSlant = 0.15f;
        const float RainMargin = 0.08f;
        const float RainSharp = 0.6f;
        const float RainReferenceHeight = 1080f;
        static readonly Color RainColor = new Color(0.80f, 0.83f, 0.88f);
        static readonly Color FireDeep = new Color(1f, 0.2f, 0.02f);
        static readonly Color FireHot = new Color(1f, 0.44f, 0.05f);
        const float FireBase = 0.12f;
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        static readonly Quaternion StandUp = Quaternion.Euler(-90f, 0f, 0f);
        static readonly Color BoltColor = new Color(0.96f, 0.76f, 0.38f);
        static readonly Color RifleColor = new Color(0.17f, 0.12f, 0.08f);
        static readonly Color ShardColor = new Color(0.22f, 0.17f, 0.12f);
        static readonly Color PropDiscColor = new Color(0.28f, 0.2f, 0.13f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly Vector3[] BoxCorners =
        {
            new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
            new Vector3(-1f, 1f, -1f), new Vector3(1f, 1f, -1f),
            new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
            new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, 1f),
        };
        static readonly string[] AerodromeGroups = { "Hangars", "Quarters", "Sheds", "Tower", "Aircraft" };
        static readonly string[] CoverModels =
        {
            "rubble/sandbags/sandbag_0", "rubble/sandbags/sandbag_1", "rubble/sandbags/sandbag_2",
            "rubble/sandbags/sandbag_3", "rubble/sandbags/sandbag_4", "rubble/sandbags/sandbag_5",
        };

        static readonly MethodInfo BuildFigure = typeof(BattlefieldPeople).GetMethod("BuildFigure",
            BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo BuildVillager = typeof(VillagePeople).GetMethod("BuildFigure",
            BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo SmokeFlag = typeof(Battlefield).GetField("_smoke",
            BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo BlastTimer = typeof(Battlefield).GetField("_blastTimer",
            BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FieldPeople = typeof(Battlefield).GetField("_people",
            BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo SeaTimer = typeof(Battlefield).GetField("_seaTimer",
            BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly int SkySunIntensity = Shader.PropertyToID("_SunIntensity");
        static readonly int SkyHaloIntensity = Shader.PropertyToID("_HaloIntensity");
        static readonly FieldInfo VillageArena =typeof(CampaignLevelController).GetField("_villageArena",
            BindingFlags.NonPublic | BindingFlags.Instance);
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        class Flyer
        {
            public ShotPlane spec;
            public Transform tr;
            public float size;
            public float speed;
            public Vector3 gun;
            public float gunSpacing;
            public Vector3[] positions;
            public Vector3[] forwards;
            public Vector3[] ups;
            public Vector3[] tips;
            public float nextShot;
            public int gunSide = 1;
            public int shots;
            public Vector3 center;
            public Transform rearPivot;
            public Vector3[] rearMuzzles;
            public Vector3 rearRest;
            public Vector3 aim;
            public Vector3 aimPoint;
            public bool aimed;
            public Vector3 Forward => tr.right;
            public Vector3 Up => tr.up;
            public Vector3 Velocity => tr.right * speed;
        }

        class Bolt
        {
            public Transform tr;
            public Vector3 velocity;
            public float life;
            public Flyer mark;
        }

        class Shard
        {
            public Transform tr;
            public Vector3 velocity;
            public Vector3 spinAxis;
            public float spin;
        }

        class Rifleman
        {
            public Vector3 muzzle;
            public Vector3 aim;
            public int target;
            public float nextShot;
            public bool atSquad;
            public Vector3 foe;
            public float foeSpread;
        }

        struct Bag
        {
            public string model;
            public float scale, turn, length, depth, height;
        }

        class Airship
        {
            public Transform tr;
            public Vector3 velocity;
        }

        class Vessel
        {
            public Transform root;
            public Vector3 end;
            public float speed;
        }

        class Cue
        {
            public float at;
            public Action run;
        }

        ShotSpec _spec;
        string _output;
        Camera _cam;
        Transform _set;
        Material _streakMaterial;
        SquadTracers _rifleTracers;
        GameObject _boltTemplate;
        ArenaClouds _arenaClouds;
        float _clock;

        readonly List<Flyer> _flyers = new List<Flyer>();
        readonly List<Bolt> _bolts = new List<Bolt>();
        readonly List<Shard> _shards = new List<Shard>();
        readonly List<Rifleman> _riflemen = new List<Rifleman>();
        readonly List<Cue> _cues = new List<Cue>();
        readonly List<Airship> _airships = new List<Airship>();
        readonly List<Vessel> _vessels = new List<Vessel>();
        readonly HashSet<MuzzleFlash> _aimedFlashes = new HashSet<MuzzleFlash>();
        readonly List<Vector4> _structures = new List<Vector4>();
        readonly List<IList> _mutedShells = new List<IList>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            string path = Argument(Flag);
            if (string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path))
            {
                Debug.LogError($"ShotDirector: no shot spec at {path}.");
                return;
            }

            var spec = new ShotSpec();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), spec);

            TerrainKind terrain = Enum.TryParse(spec.terrain, true, out TerrainKind kind)
                ? kind : TerrainKind.Verdun;
            Daytime daytime = Enum.TryParse(spec.daytime, true, out Daytime time)
                ? time : Daytime.Morning;
            CustomBattle.Request(new BattleMap(TerrainNames.For(terrain), spec.seed, terrain),
                daytime, spec.aerodrome ? BattleShape.Battle : BattleShape.Story);
            CampaignRun.TakeLaunch();
            VosgesTerrain.SkipCraters = !spec.ambientCraters;

            Application.runInBackground = true;
            Random.InitState(spec.seed);

            var go = new GameObject("Shot Director");
            DontDestroyOnLoad(go);
            var director = go.AddComponent<ShotDirector>();
            director._spec = spec;
            director._output = OutputPath(spec, path);
        }

        static string Argument(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }

        static string OutputPath(ShotSpec spec, string specPath)
        {
            if (string.IsNullOrEmpty(spec.output)) return Path.ChangeExtension(specPath, ".png");
            if (Path.IsPathRooted(spec.output)) return spec.output;
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(specPath), spec.output));
        }

        IEnumerator Start()
        {
            float ready = Time.realtimeSinceStartup + Mathf.Max(0.5f, _spec.settle);
            while (Time.realtimeSinceStartup < ready || Time.timeScale <= 0f
                   || Camera.main == null || Battlefield.Current == null)
                yield return null;

            if (!TryStage())
            {
                Application.Quit();
                yield break;
            }
            Time.captureDeltaTime = StepSec;

            int frames = Mathf.Max(1, Mathf.RoundToInt(_spec.warmup / StepSec));
            for (int i = 0; i <= frames; i++)
            {
                Tick(i * StepSec, i == frames);
                if (i < frames) yield return null;
            }

            yield return Capture();
            Application.Quit();
        }

        void LateUpdate() => AudioListener.volume = 0f;

        bool TryStage()
        {
            try
            {
                Stage();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        void Stage()
        {
            Random.InitState(_spec.seed);
            _set = new GameObject("Shot Set").transform;
            Silence();
            BuildLand();
            Frame();
            if (_spec.storm.on) ApplyStorm();
            LogValley();
            LogArena();
            if (_spec.hideSun) HideSun();
            ShadeMountains();
            Focus();
            if (_spec.arena.on) BuildArena();

            LogAerodrome();
            SurveyCoast();
            if (_spec.darkHangars) DarkenHangars();
            BuildFill();
            foreach (ShotCrater crater in _spec.craters) StampCrater(crater);
            foreach (ShotGroyne groyne in _spec.groynes) BuildGroyne(groyne);
            foreach (ShotBunker bunker in _spec.bunkers) BuildBunker(bunker);
            if (!_spec.bunkerFire)
                foreach (var gun in FindObjectsByType<BunkerGun>(FindObjectsSortMode.None)) gun.enabled = false;
            foreach (ShotBoat boat in _spec.boats) BuildBoat(boat);
            StageShips();
            foreach (ShotGull gull in _spec.gulls) BuildGulls(gull);
            foreach (ShotPlane plane in _spec.planes) _flyers.Add(BuildFlyer(plane));
            foreach (ShotProp prop in _spec.props) BuildProp(prop);
            foreach (ShotStructure mill in _spec.mills) BuildMill(mill);
            foreach (ShotStructure hut in _spec.huts) BuildHut(hut);
            foreach (ShotGrove grove in _spec.spruces) BuildGrove(grove);
            foreach (ShotCow cow in _spec.cows) BuildCow(cow);
            foreach (ShotZeppelin zeppelin in _spec.zeppelins) BuildZeppelin(zeppelin);
            foreach (ShotCloud cloud in _spec.clouds) BuildCloud(cloud);
            foreach (ShotCloud puff in _spec.puffs) BuildPuff(puff);
            foreach (ShotVillager group in _spec.villagers) BuildVillagers(group);
            foreach (ShotSquad squad in _spec.squads) BuildSquad(squad);
            BuildSmoke();
            BuildMist();
            if (_spec.storm.on) BuildStorm();
            ScheduleBursts();

            foreach (Flyer flyer in _flyers)
                Debug.Log($"ShotDirector: {flyer.tr.name} ends at {flyer.positions[0]}");
        }

        void Silence()
        {
            var controller = FindFirstObjectByType<CampaignLevelController>();
            if (controller != null)
                foreach (var behaviour in controller.GetComponents<MonoBehaviour>())
                    behaviour.enabled = false;

            foreach (var cube in FindObjectsByType<CubeController>(FindObjectsSortMode.None))
                cube.gameObject.SetActive(false);

            if (!_spec.ambientFlak)
                foreach (var flak in FindObjectsByType<SkyFlak>(FindObjectsSortMode.None))
                    flak.enabled = false;

            if (!_spec.ambientZeppelins)
                foreach (var zeppelin in FindObjectsByType<SkyZeppelin>(FindObjectsSortMode.None))
                    Destroy(zeppelin.gameObject);

            if (!_spec.ambientClouds)
                foreach (var clouds in FindObjectsByType<CloudSystem>(FindObjectsSortMode.None))
                    Destroy(clouds.gameObject);

            if (!_spec.ambientGulls)
                foreach (var gulls in FindObjectsByType<Seagulls>(FindObjectsSortMode.None))
                    Destroy(gulls.gameObject);

            if (!_spec.aerodromeFires)
            {
                GameObject fires = GameObject.Find(AerodromeFires);
                if (fires != null) Destroy(fires);
            }

            Battlefield field = Battlefield.Current;
            if (field == null) return;

            if (!_spec.ambientBlasts && BlastTimer != null) BlastTimer.SetValue(field, float.MaxValue);
            if (!_spec.ambientBlasts && SeaTimer != null) SeaTimer.SetValue(field, float.MaxValue);
            if (!_spec.ambientSmoke)
            {
                if (SmokeFlag != null) SmokeFlag.SetValue(field, false);
                foreach (var column in FindObjectsByType<SmokeColumn>(FindObjectsSortMode.None))
                    Destroy(column.gameObject);
            }
            if (_spec.ambientPeople || field.People == null) return;

            Destroy(field.People.gameObject);
            if (FieldPeople != null) FieldPeople.SetValue(field, null);
        }

        void Frame()
        {
            _cam = Camera.main;
            ShotCamera shot = _spec.camera;
            float y = (_spec.arena.on ? ArenaAltitude : GroundY(shot.x, shot.z)) + shot.height;
            _cam.transform.SetPositionAndRotation(new Vector3(shot.x, y, shot.z),
                Quaternion.Euler(-shot.pitch, shot.yaw, shot.roll));
            _cam.fieldOfView = shot.fov > 0f ? shot.fov : 40f;
            _cam.aspect = _spec.width / (float)_spec.height;
            _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, 2600f);

            if (_spec.arena.on) CoastSky.ApplyFog(SpecDaytime, ArenaDistance, 0f);
            if (_spec.fogStart >= 0f) RenderSettings.fogStartDistance = _spec.fogStart;
            if (_spec.fogEnd > 0f) RenderSettings.fogEndDistance = _spec.fogEnd;
        }

        void Focus()
        {
            ShotFocus focus = _spec.focus;
            if (focus == null || (focus.distance <= 0f && focus.farEnd <= 0f)) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Shot Focus (runtime)";
            var field = profile.Add<DepthOfField>();
            if (focus.farEnd > 0f)
            {
                field.mode.Override(DepthOfFieldMode.Gaussian);
                field.gaussianStart.Override(focus.farStart);
                field.gaussianEnd.Override(focus.farEnd);
                field.gaussianMaxRadius.Override(focus.farRadius > 0f ? focus.farRadius : 1f);
                field.highQualitySampling.Override(true);
            }
            else
            {
                field.mode.Override(DepthOfFieldMode.Bokeh);
                field.focusDistance.Override(focus.distance);
                field.focalLength.Override(focus.focalLength > 0f ? focus.focalLength : 85f);
                field.aperture.Override(focus.aperture > 0f ? focus.aperture : 4f);
            }

            var volume = new GameObject("Shot Focus").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = FocusPriority;
            volume.profile = profile;
            _cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        float ArenaDistance => _spec.arena.distance > 0f
            ? _spec.arena.distance : ArenaLevelController.CameraDistance;

        Daytime SpecDaytime => Enum.TryParse(_spec.daytime, true, out Daytime time) ? time : Daytime.Morning;

        Weather SpecWeather => _spec.storm.on ? Weather.Storm : Weather.Calm;

        void ApplyStorm()
        {
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (volume.isGlobal) Destroy(volume);
            foreach (var rays in FindObjectsByType<GodRays>(FindObjectsSortMode.None)) rays.enabled = false;

            CoastSky.Apply(_cam, SpecDaytime, Weather.Storm);
            CoastSky.ApplyFog(SpecDaytime, Weather.Storm, ArenaDistance, 0f);
            if (_spec.fogStart >= 0f) RenderSettings.fogStartDistance = _spec.fogStart;
            if (_spec.fogEnd > 0f) RenderSettings.fogEndDistance = _spec.fogEnd;
        }

        void BuildStorm()
        {
            ShotStorm storm = _spec.storm;
            Transform eye = _cam.transform;
            float halfH = ArenaDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            StormWeather weather = StormWeather.Begin(_cam, eye.position, halfH * _cam.aspect, halfH,
                ArenaDistance, eye.position.z + ArenaDistance, _arenaClouds, 0f);
            weather.transform.SetParent(_set, true);
            weather.Hold();

            Quaternion facing = Quaternion.Euler(0f, _spec.camera.yaw, 0f);
            for (int i = 0; i < storm.bolts.Length; i++)
            {
                if (i >= weather.BoltCount)
                {
                    Debug.LogWarning($"ShotDirector: only {weather.BoltCount} lightning bolts fit; extra ones skipped.");
                    break;
                }

                ShotLightning bolt = storm.bolts[i];
                if (bolt.seed != 0) Random.InitState(bolt.seed);
                float[] screen = bolt.screen != null && bolt.screen.Length >= 2 ? bolt.screen : new[] { 0.5f, 1f };
                Vector3 top = Air(screen, bolt.distance > 0f ? bolt.distance : DefaultBoltDistance);
                weather.PoseBolt(i, top, bolt.height > 0f ? bolt.height : DefaultBoltHeight, bolt.far,
                    bolt.alpha > 0f ? bolt.alpha : 1f, facing);
                Debug.Log($"ShotDirector: lightning {i} top {top}");
            }

            if (storm.flash > 0f) weather.HoldFlash(storm.flash);
            foreach (ShotRain layer in storm.rain) BuildRain(layer);
        }

        void BuildRain(ShotRain layer)
        {
            Shader shader = Shader.Find("Hidden/LightningBolt");
            if (shader == null)
            {
                Debug.LogError("ShotDirector: Hidden/LightningBolt not found; no rain.");
                return;
            }

            var mat = new Material(shader) { name = "Shot Rain (runtime)" };
            mat.SetColor("_Color", RainColor);
            mat.SetFloat("_Alpha", 1f);

            int count = Mathf.Max(1, layer.count);
            float near = layer.near > 0f ? layer.near : 60f;
            float far = layer.far > near ? layer.far : near * 2f;
            float length = layer.length > 0f ? layer.length : DefaultRainLength;
            float widthPx = layer.widthPx > 0f ? layer.widthPx : DefaultRainWidthPx;
            float alpha = layer.alpha > 0f ? layer.alpha : DefaultRainAlpha;
            float slant = layer.slant != 0f ? layer.slant : DefaultRainSlant;

            Transform eye = _cam.transform;
            Vector3 right = Vector3.ProjectOnPlane(eye.right, Vector3.up).normalized;
            Vector3 fall = (Vector3.down - right * slant).normalized;
            float tanY = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            var vertices = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var colors = new Color[count * 4];
            var indices = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                float depth = Random.Range(near, far);
                Vector3 head = _cam.ViewportToWorldPoint(new Vector3(
                    Random.Range(-RainMargin, 1f + RainMargin), Random.Range(-RainMargin, 1f + RainMargin), depth));
                Vector3 tail = head - fall * (length * Random.Range(0.75f, 1.25f));
                Vector3 view = ((head + tail) * 0.5f - eye.position).normalized;
                Vector3 side = Vector3.Cross(fall, view).normalized;
                float half = widthPx * 2f * depth * tanY / RainReferenceHeight * 0.5f;
                float strength = alpha * Random.Range(0.55f, 1f);

                int v = i * 4;
                vertices[v] = tail - side * half;
                vertices[v + 1] = tail + side * half;
                vertices[v + 2] = head + side * half;
                vertices[v + 3] = head - side * half;
                uvs[v] = uvs[v + 3] = new Vector2(0f, RainSharp);
                uvs[v + 1] = uvs[v + 2] = new Vector2(1f, RainSharp);
                colors[v] = colors[v + 1] = new Color(1f, 1f, 1f, 0f);
                colors[v + 2] = colors[v + 3] = new Color(1f, 1f, 1f, strength);

                int t = i * 6;
                indices[t] = v;
                indices[t + 1] = v + 1;
                indices[t + 2] = v + 2;
                indices[t + 3] = v;
                indices[t + 4] = v + 2;
                indices[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "Shot Rain" };
            if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = indices;
            mesh.RecalculateBounds();

            var go = new GameObject("Shot Rain");
            go.transform.SetParent(_set, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void BuildArena()
        {
            ShotArena arena = _spec.arena;
            float fov = arena.fov > 0f ? arena.fov : LevelCamera.BaseFieldOfView;
            float halfH = ArenaDistance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            Vector3 eye = _cam.transform.position;
            if (arena.eye != null && arena.eye.Length >= 3)
                eye += new Vector3(arena.eye[0], arena.eye[1], arena.eye[2]);

            if (arena.seed != 0) Random.InitState(arena.seed);
            Color tint = CoastSky.CloudColor(SpecDaytime, SpecWeather);
            Color glow = CoastSky.CloudGlow(SpecDaytime, SpecWeather);
            if (arena.cloudColor != null && arena.cloudColor.Length >= 3)
            {
                float share = glow.maxColorComponent / Mathf.Max(0.001f, tint.maxColorComponent);
                tint = new Color(arena.cloudColor[0], arena.cloudColor[1], arena.cloudColor[2]);
                glow = tint * share;
            }
            ArenaClouds clouds = ArenaClouds.Begin(eye, halfH * LevelCamera.ReferenceAspect, halfH,
                ArenaDistance, eye.z + ArenaDistance, tint, glow, 0f);
            clouds.transform.SetParent(_set, true);
            _arenaClouds = clouds;
            Debug.Log($"ShotDirector: arena eye {eye}, cloud floor y {clouds.FloorY}");
        }

        Flyer BuildFlyer(ShotPlane spec)
        {
            PlaneModelConfig config = PlaneModels.ById(spec.model) ?? PlaneModels.Sopwith;
            var go = new GameObject($"Shot {config.resourceName} {_flyers.Count}");
            PlaneSkin skin = string.IsNullOrEmpty(spec.skin) && PlaneSkins.RandomPainted(config)
                ? PlaneSkins.RandomEnemy(config, null)
                : PlaneSkins.Enemy(config, spec.skin);
            Transform model = PlaneFactory.BuildPlaneModel(go.transform, config, false, skin);
            model.localScale *= PlaneModelConfig.LevelScale;
            foreach (Collider col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            SpinExtraProps(go.transform, model, config);
            if (_spec.planeFill != null && _spec.planeFill.intensity > 0f && !spec.noFill)
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.renderingLayerMask |= FillLayer;

            var flyer = new Flyer
            {
                spec = spec,
                tr = go.transform,
                size = config.OnScreenSize * PlaneModelConfig.LevelScale,
                speed = spec.speed > 0f ? spec.speed : DefaultSpeed,
                center = BodyCenter(go),
            };
            if (PlaneFactory.GunMuzzleLocal(go, model, config, out Vector3 gun)) flyer.gun = gun;
            else if (config.gunLift > 0f)
                flyer.gun = PlaneFactory.GunLocal(go, model, config, PlaneModelConfig.LevelScale, false);
            else flyer.gun = PlaneFactory.NoseLocal(go, model, config);
            flyer.gunSpacing = config == PlaneModels.Eindecker ? 0f
                : config == PlaneModels.Sopwith ? CamelGunSpacing
                : config == PlaneModels.Fokker ? TriplaneGunSpacing : GunSpacing;
            if (!string.IsNullOrEmpty(spec.gunHinge)) MountNodeGun(flyer, model, config);
            else if (spec.rearGun) MountRearGun(flyer, model, config);
            if (spec.aim != null && spec.aim.Length >= 3)
            {
                flyer.aimPoint = Air(new[] { spec.aim[0], spec.aim[1] }, spec.aim[2]);
                flyer.aimed = true;
            }

            if (spec.boost)
            {
                PlaneFactory.WingTipsLocal(go, model, out Vector3 near, out Vector3 far, config);
                var tips = new List<Vector3>();
                foreach (Vector3 tip in new[] { near, far })
                    if (spec.streakSide == 0 || Mathf.Sign(tip.z) == Mathf.Sign(spec.streakSide))
                        tips.Add(SweptTip(tip));
                flyer.tips = tips.ToArray();
            }

            Vector3? engine = string.IsNullOrEmpty(spec.smokeNode)
                ? PlaneFactory.EngineLocal(go, model, config) : NodeCentre(go.transform, model, spec.smokeNode);
            SmokeTrail smoke = null;
            if (spec.burning || spec.smoking)
            {
                smoke = go.AddComponent<SmokeTrail>();
                if (engine.HasValue) smoke.Mount = engine.Value;
                float[] tint = spec.smokeColor;
                if (tint != null && tint.Length >= 3)
                    smoke.Tint(new Color(tint[0], tint[1], tint[2], tint.Length > 3 ? tint[3] : DefaultSmokeAlpha));
                float trail = flyer.size * (spec.smokeScale > 0f ? spec.smokeScale : TrailScale);
                if (spec.burning) smoke.Ignite(trail);
                else smoke.Arm(trail);
            }
            if (spec.burning)
                PlaneFire.Ignite(go, flyer.size * (spec.fireScale > 0f ? spec.fireScale : 1f), engine);

            float[] screen = spec.screen != null && spec.screen.Length >= 2
                ? spec.screen : new[] { 0.5f, 0.5f };
            Vector3 end = Air(screen, spec.distance > 0f ? spec.distance : 200f);
            Quaternion frame = Quaternion.Euler(0f, _spec.camera.yaw, 0f);
            Vector3 forward = frame * Quaternion.Euler(-spec.climb, spec.course, 0f) * Vector3.forward;
            Vector3 level = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (level.sqrMagnitude < 1e-4f) level = frame * Vector3.back;
            Vector3 up = Quaternion.AngleAxis(-spec.bank, forward) * level.normalized;
            if (spec.slot != null && spec.slot.Length >= 3 && spec.lead >= 0 && spec.lead < _flyers.Count)
                Formation(_flyers[spec.lead], spec.slot, ref end, ref forward, ref up);

            PlanPath(flyer, end, forward, up);
            Place(flyer, _spec.warmup);
            if (smoke != null && spec.smokeDepth != 0f)
                smoke.Mount += flyer.tr.InverseTransformDirection(_cam.transform.forward) * spec.smokeDepth;

            if (spec.hitAge > 0f)
                Schedule(_spec.warmup - spec.hitAge, () => Hit(flyer));
            return flyer;
        }

        static void Formation(Flyer lead, float[] slot, ref Vector3 end, ref Vector3 forward, ref Vector3 up)
        {
            forward = lead.forwards[0];
            up = lead.ups[0];
            Vector3 ahead = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, ahead);
            end = lead.positions[0] + (side * slot[0] + Vector3.up * slot[1] - ahead * slot[2]) * lead.size;
        }

        void MountRearGun(Flyer flyer, Transform model, PlaneModelConfig config)
        {
            Transform body = flyer.tr;
            Transform node = string.IsNullOrEmpty(config.rearGunNode)
                ? null : PlaneFactory.FindDeep(model, config.rearGunNode);
            if (node == null || !MeasureIn(body, node, out Bounds bounds))
            {
                Debug.LogError($"ShotDirector: {config.resourceName} has no rear gun.");
                return;
            }

            float length = bounds.size.x, height = bounds.size.y;
            var pivot = new Vector3(bounds.max.x - length * RearPivotFromBreech,
                bounds.min.y + height * RearPivotHeight, bounds.center.z);
            var muzzle = new Vector3(bounds.min.x - RearMuzzleClearance,
                bounds.min.y + height * RearMuzzleHeight, bounds.center.z);
            var spacing = new Vector3(0f, 0f, bounds.size.z * RearSpacingShare);

            var hinge = new GameObject("Shot Rear Gun").transform;
            hinge.SetParent(model, false);
            hinge.SetPositionAndRotation(body.TransformPoint(pivot), body.rotation);
            node.SetParent(hinge, true);

            flyer.rearPivot = hinge;
            flyer.rearRest = (muzzle - pivot).normalized;
            flyer.rearMuzzles = new[]
            {
                hinge.InverseTransformPoint(body.TransformPoint(muzzle - spacing)),
                hinge.InverseTransformPoint(body.TransformPoint(muzzle + spacing)),
            };
        }

        void MountNodeGun(Flyer flyer, Transform model, PlaneModelConfig config)
        {
            Transform body = flyer.tr;
            ShotPlane spec = flyer.spec;
            Transform node = PlaneFactory.FindDeep(model, spec.gunHinge);
            Transform barrel = string.IsNullOrEmpty(spec.gunBarrel) ? null : PlaneFactory.FindDeep(model, spec.gunBarrel);
            if (node == null || barrel == null || !MeasureIn(body, barrel, out Bounds bounds))
            {
                Debug.LogError($"ShotDirector: {config.resourceName} has no gun {spec.gunHinge} / {spec.gunBarrel}.");
                return;
            }

            Vector3 pivot = body.InverseTransformPoint(node.position);
            int axis = 0;
            for (int i = 1; i < 3; i++)
                if (bounds.extents[i] > bounds.extents[axis]) axis = i;
            Vector3 reach = Vector3.zero;
            reach[axis] = bounds.extents[axis];
            Vector3 a = bounds.center + reach, b = bounds.center - reach;
            Vector3 muzzle = (a - pivot).sqrMagnitude > (b - pivot).sqrMagnitude ? a : b;
            Vector3 rest = (muzzle - pivot).normalized;
            muzzle += rest * NodeMuzzleClearance;

            var hinge = new GameObject("Shot Node Gun").transform;
            hinge.SetParent(model, false);
            hinge.SetPositionAndRotation(body.TransformPoint(pivot), body.rotation);
            node.SetParent(hinge, true);

            Vector3 local = hinge.InverseTransformPoint(body.TransformPoint(muzzle));
            flyer.rearPivot = hinge;
            flyer.rearRest = rest;
            flyer.rearMuzzles = new[] { local, local };
            Debug.Log($"ShotDirector: {spec.gunHinge} pivot {pivot} rest {rest}");
        }

        static void SpinExtraProps(Transform body, Transform model, PlaneModelConfig config)
        {
            if (string.IsNullOrEmpty(config.propPivotNode)) return;

            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(config.propPivotNode + ".")) continue;
                if (t.GetComponent<PropellerSpin>() != null) continue;

                var spin = t.gameObject.AddComponent<PropellerSpin>();
                spin.axisSpace = body;
                spin.axisInSpace = Vector3.right;
                spin.straighten = config.straightenProp;
            }
        }

        static Vector3? NodeCentre(Transform body, Transform model, string name)
        {
            Transform node = PlaneFactory.FindDeep(model, name);
            Renderer[] renderers = node != null ? node.GetComponentsInChildren<Renderer>() : new Renderer[0];
            if (renderers.Length == 0)
            {
                Debug.LogError($"ShotDirector: no smoke node {name}.");
                return null;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return body.InverseTransformPoint(bounds.center);
        }

        static bool MeasureIn(Transform body, Transform node, out Bounds bounds)
        {
            bounds = default;
            var filter = node.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            Bounds local = filter.sharedMesh.bounds;
            for (int i = 0; i < BoxCorners.Length; i++)
            {
                Vector3 point = body.InverseTransformPoint(
                    node.TransformPoint(local.center + Vector3.Scale(local.extents, BoxCorners[i])));
                if (i == 0) bounds = new Bounds(point, Vector3.zero);
                else bounds.Encapsulate(point);
            }
            return bounds.size.x > 1e-4f;
        }

        void PlanPath(Flyer flyer, Vector3 end, Vector3 forward, Vector3 up)
        {
            int steps = Mathf.CeilToInt(_spec.warmup / PathStep) + 2;
            flyer.positions = new Vector3[steps];
            flyer.forwards = new Vector3[steps];
            flyer.ups = new Vector3[steps];

            ShotPlane spec = flyer.spec;
            Vector3 position = end;
            for (int i = 0; i < steps; i++)
            {
                flyer.positions[i] = position;
                flyer.forwards[i] = forward;
                flyer.ups[i] = up;

                Vector3 right = Vector3.Cross(up, forward);
                Quaternion back = Quaternion.AngleAxis(-spec.turn * PathStep, Vector3.up)
                                  * Quaternion.AngleAxis(spec.pitchRate * PathStep, right)
                                  * Quaternion.AngleAxis(spec.rollRate * PathStep, forward);
                Vector3 earlier = (back * forward).normalized;
                up = Vector3.ProjectOnPlane(back * up, earlier).normalized;
                position -= (forward + earlier) * (0.5f * flyer.speed * PathStep);
                forward = earlier;
            }
        }

        void Place(Flyer flyer, float remaining)
        {
            float k = Mathf.Clamp(remaining / PathStep, 0f, flyer.positions.Length - 1.001f);
            int i = Mathf.FloorToInt(k);
            float t = k - i;
            Vector3 position = Vector3.Lerp(flyer.positions[i], flyer.positions[i + 1], t);
            Vector3 forward = Vector3.Slerp(flyer.forwards[i], flyer.forwards[i + 1], t).normalized;
            Vector3 up = Vector3.ProjectOnPlane(
                Vector3.Slerp(flyer.ups[i], flyer.ups[i + 1], t), forward).normalized;
            flyer.tr.SetPositionAndRotation(position, Orientation(forward, up));
        }

        static Quaternion Orientation(Vector3 forward, Vector3 up) =>
            Quaternion.LookRotation(Vector3.Cross(forward, up), up);

        void Hit(Flyer flyer)
        {
            Explosion.Spawn(flyer.tr.position, flyer.size * HitScale, 0f);

            for (int i = 0; i < flyer.spec.debris; i++) SpawnShard(flyer.tr.position, flyer, 1f);
        }

        void Strike(Vector3 point, Flyer mark)
        {
            for (int i = 0; i < SplinterCount; i++) SpawnShard(point, mark, SplinterScale);
        }

        void SpawnShard(Vector3 at, Flyer from, float scale)
        {
            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, at,
                new Vector3(Random.Range(ShardSizeMin, ShardSizeMax),
                    Random.Range(ShardSizeMin, ShardSizeMax) * 0.4f,
                    Random.Range(ShardSizeMin, ShardSizeMax)) * scale,
                ShardColor, emissive: false, keepCollider: false);
            go.name = "Shard";
            go.transform.SetParent(_set, true);
            go.transform.rotation = Random.rotation;
            _shards.Add(new Shard
            {
                tr = go.transform,
                velocity = from.Velocity * Random.Range(0.35f, 0.7f) + Random.insideUnitSphere * ShardScatter,
                spinAxis = Random.onUnitSphere,
                spin = Random.Range(180f, 720f),
            });
        }

        static Vector3 BodyCenter(GameObject body)
        {
            var renderers = body.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return Vector3.zero;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return body.transform.InverseTransformPoint(bounds.center);
        }

        void ShadeMountains()
        {
            if (_spec.mountainShade <= 0f || MountainRange.Current == null) return;

            var shade = new Color(_spec.mountainShade, _spec.mountainShade, _spec.mountainShade, 1f);
            var done = new HashSet<Material>();
            foreach (var r in MountainRange.Current.GetComponentsInChildren<MeshRenderer>())
                if (r.sharedMaterial != null && done.Add(r.sharedMaterial))
                    r.sharedMaterial.SetColor(BaseColorId, shade);
        }

        void BuildLand()
        {
            var land = FindFirstObjectByType<CampaignTerrain>();
            if (land != null) land.BuildAround(_spec.camera.x);
        }

        static VosgesLayout Valley
        {
            get
            {
                var valley = FindFirstObjectByType<VosgesTerrain>();
                return valley != null ? valley.Layout : null;
            }
        }

        static void HideSun()
        {
            Material sky = RenderSettings.skybox;
            if (sky != null)
            {
                if (sky.HasProperty(SkySunIntensity)) sky.SetFloat(SkySunIntensity, 0f);
                if (sky.HasProperty(SkyHaloIntensity)) sky.SetFloat(SkyHaloIntensity, 0f);
            }
            foreach (var rays in FindObjectsByType<GodRays>(FindObjectsSortMode.None)) rays.enabled = false;
        }

        void LogArena()
        {
            var controller = FindFirstObjectByType<CampaignLevelController>();
            if (controller == null || VillageArena == null) return;
            if (VillageArena.GetValue(controller) is VosgesArena arena)
                Debug.Log($"ShotDirector: village arena origin {arena.Origin} footprint {arena.Footprint} " +
                          $"road z {arena.RoadZ} half {arena.RoadHalfWidth:F0} lift {arena.RoadLift:F1}");
        }

        void LogValley()
        {
            VosgesLayout layout = Valley;
            if (layout == null) return;

            float x = _spec.camera.x;
            for (int cell = Mathf.FloorToInt((x - ValleyLogReach) / layout.VillageCell);
                 cell <= Mathf.FloorToInt((x + ValleyLogReach) / layout.VillageCell); cell++)
                if (layout.Village(cell, out var village))
                    Debug.Log($"ShotDirector: village {cell} model {village.model} at ({village.x:F0}, {village.z:F0}) " +
                              $"half ({village.halfX:F0}, {village.halfZ:F0}) yaw {village.rotation.eulerAngles.y:F0} " +
                              $"pad {village.padY:F0}");

            for (int cell = Mathf.FloorToInt((x - ValleyLogReach) / layout.HerdCell);
                 cell <= Mathf.FloorToInt((x + ValleyLogReach) / layout.HerdCell); cell++)
                if (layout.Herd(cell, out var herd))
                    Debug.Log($"ShotDirector: herd {cell} at ({herd.x:F0}, {herd.z:F0}) " +
                              $"radius ({herd.radiusX:F0}, {herd.radiusZ:F0}) cows {herd.cows.Length}");

            for (int cell = Mathf.FloorToInt((x - ValleyLogReach) / layout.MillCell);
                 cell <= Mathf.FloorToInt((x + ValleyLogReach) / layout.MillCell); cell++)
                if (layout.Mill(cell, out var mill))
                    Debug.Log($"ShotDirector: mill {cell} at ({mill.x:F0}, {mill.z:F0})");

            for (int cell = Mathf.FloorToInt((x - ValleyLogReach) / layout.HouseCell);
                 cell <= Mathf.FloorToInt((x + ValleyLogReach) / layout.HouseCell); cell++)
            {
                var houses = layout.Houses(cell);
                if (houses == null) continue;
                foreach (var house in houses)
                    Debug.Log($"ShotDirector: house {cell} model {house.model} at ({house.x:F0}, {house.z:F0})");
            }

            Debug.Log($"ShotDirector: camera ground {GroundY(_spec.camera.x, _spec.camera.z):F1}");
        }

        void BuildCow(ShotCow spec)
        {
            Vector3 at = Ground(spec.screenX, spec.distance);
            VosgesCow.Place(_set, new VosgesLayout.CowSpot
            {
                position = at,
                up = Tilt(GroundNormal(at.x, at.z), CowMaxTilt),
                yaw = _spec.camera.yaw + spec.yaw,
                brown = spec.brown,
                scale = spec.scale > 0f ? spec.scale : 1f,
            });
            Debug.Log($"ShotDirector: cow at {at}");
        }

        void BuildGrove(ShotGrove spec)
        {
            VosgesLayout layout = Valley;
            Vector3 centre = Ground(spec.screenX, spec.distance);
            Vector3 forward = _cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float spread = spec.spread > 0f ? spec.spread : DefaultGroveSpread;
            float depth = spec.depth > 0f ? spec.depth : spread * 0.5f;
            float size = spec.scale > 0f ? spec.scale : 1f;

            var placed = new List<Vector3>();
            for (int i = 0; i < Mathf.Max(1, spec.count); i++)
                for (int tries = 0; tries < GroveTries; tries++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    float reach = Mathf.Sqrt(Random.value);
                    Vector3 p = centre + right * (Mathf.Cos(angle) * reach * spread)
                                       + forward * (Mathf.Sin(angle) * reach * depth);
                    if (Crowded(placed, p)) continue;
                    if (layout != null && (layout.InVillage(p.x, p.z, GroveClear)
                                           || layout.InHouse(p.x, p.z, GroveClear)
                                           || layout.InMill(p.x, p.z, GroveClear))) continue;
                    if (InStructure(p, GroveClear)) continue;

                    p.y = GroundY(p.x, p.z);
                    placed.Add(p);
                    VosgesSpruce.Place(_set, new VosgesLayout.TreeSpot
                    {
                        position = p,
                        yaw = Random.Range(0f, 360f),
                        model = Random.Range(0, VosgesSpruce.Models),
                        fore = spec.fore,
                        scale = size * (1f + Random.Range(-GroveJitter, GroveJitter)),
                    });
                    break;
                }
        }

        void BuildMill(ShotStructure spec)
        {
            VosgesWindmill.Measure();
            if (!VosgesWindmill.Ready) return;

            const float u = PlaneModelConfig.UnitsPerMeter;
            Vector3 at = Ground(spec.screenX, spec.distance);
            var rotation = Quaternion.Euler(0f, VosgesWindmill.FacingYaw + _spec.camera.yaw + spec.yaw, 0f);
            float radius = VosgesWindmill.Radius * u;
            float floor = Floor(at, radius);
            Vector3 offset = rotation * (VosgesWindmill.Centre * u);
            VosgesWindmill.Place(_set, new VosgesLayout.MillSpot
            {
                x = at.x,
                z = at.z,
                rotation = rotation,
                origin = new Vector3(at.x - offset.x, floor - MillSink, at.z - offset.z),
                radius = radius,
                padY = floor,
                phase = Random.Range(0f, 360f),
            });
            _structures.Add(new Vector4(at.x, at.y, at.z, radius));
            Debug.Log($"ShotDirector: mill at {at}");
        }

        void BuildHut(ShotStructure spec)
        {
            VosgesHouse.Measure();
            if (!VosgesHouse.Has(spec.model)) return;

            const float u = PlaneModelConfig.UnitsPerMeter;
            Bounds box = VosgesHouse.Footprint(spec.model);
            Vector3 at = Ground(spec.screenX, spec.distance);
            var rotation = Quaternion.Euler(0f, VosgesHouse.Facing(spec.model) + _spec.camera.yaw + spec.yaw, 0f);
            float ex = box.extents.x * u, ez = box.extents.z * u;
            float radius = Mathf.Sqrt(ex * ex + ez * ez);
            float floor = Floor(at, radius);
            Vector3 offset = rotation * new Vector3(box.center.x * u, 0f, box.center.z * u);
            VosgesHouse.Place(_set, new VosgesLayout.HouseSpot
            {
                model = spec.model,
                x = at.x,
                z = at.z,
                rotation = rotation,
                inverse = Quaternion.Inverse(rotation),
                origin = new Vector3(at.x - offset.x, floor - box.min.y * u - HouseSink, at.z - offset.z),
                halfX = ex,
                halfZ = ez,
                radius = radius,
                padY = floor,
            });
            _structures.Add(new Vector4(at.x, at.y, at.z, radius));
            Debug.Log($"ShotDirector: hut {spec.model} at {at}");
        }

        static float Floor(Vector3 at, float radius)
        {
            float floor = GroundY(at.x, at.z);
            for (int i = 0; i < FloorProbes; i++)
            {
                float angle = i * Mathf.PI * 2f / FloorProbes;
                floor = Mathf.Min(floor, GroundY(at.x + Mathf.Cos(angle) * radius * FloorReach,
                    at.z + Mathf.Sin(angle) * radius * FloorReach));
            }
            return floor;
        }

        bool InStructure(Vector3 p, float margin)
        {
            foreach (Vector4 s in _structures)
            {
                float dx = s.x - p.x, dz = s.z - p.z, reach = s.w + margin;
                if (dx * dx + dz * dz < reach * reach) return true;
            }
            return false;
        }

        static bool Crowded(List<Vector3> placed, Vector3 p)
        {
            foreach (Vector3 q in placed)
            {
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz < GroveGap * GroveGap) return true;
            }
            return false;
        }

        static Vector3 GroundNormal(float x, float z)
        {
            float dx = GroundY(x + NormalProbe, z) - GroundY(x - NormalProbe, z);
            float dz = GroundY(x, z + NormalProbe) - GroundY(x, z - NormalProbe);
            return new Vector3(-dx, 2f * NormalProbe, -dz).normalized;
        }

        static Vector3 Tilt(Vector3 normal, float maxAngle)
        {
            float angle = Vector3.Angle(Vector3.up, normal);
            if (angle <= maxAngle) return normal;
            return Vector3.Slerp(Vector3.up, normal, maxAngle / angle);
        }

        void ClearHerds()
        {
            var scenery = FindFirstObjectByType<VosgesScenery>();
            if (scenery == null) return;

            Vector3 eye = _cam.transform.position;
            float reach = _spec.clearHerds;
            foreach (Transform site in scenery.transform)
            {
                if (!_spec.ambientSpruces && site.name.StartsWith("Spruces"))
                {
                    Destroy(site.gameObject);
                    continue;
                }
                if (!site.name.StartsWith("Herd")) continue;
                Renderer[] renderers = site.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                Vector3 near = bounds.ClosestPoint(new Vector3(eye.x, bounds.center.y, eye.z));
                Vector2 gap = new Vector2(near.x - eye.x, near.z - eye.z);
                if (gap.magnitude < reach) Destroy(site.gameObject);
            }
        }

        void LogAerodrome()
        {
            Aerodrome field = Aerodrome.Current;
            if (field == null) return;

            foreach (string group in AerodromeGroups)
            {
                Transform node = PlaneFactory.FindDeep(field.transform, group);
                if (node == null) continue;
                foreach (Transform part in node)
                {
                    Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) continue;
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                    Debug.Log($"ShotDirector: aerodrome {group}/{part.name} min {bounds.min} max {bounds.max}");
                }
            }
        }

        void DarkenHangars()
        {
            Aerodrome field = Aerodrome.Current;
            Transform hangars = field != null ? PlaneFactory.FindDeep(field.transform, "Hangars") : null;
            if (hangars == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader != null ? shader : NightReceivers.Lit) { name = "Hangar Shade (runtime)" };
            mat.SetColor(BaseColorId, HangarShade);

            foreach (Transform hangar in hangars)
            {
                Renderer[] renderers = hangar.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

                float height = bounds.size.y * HangarShadeHeight;
                var shade = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, bounds.center.z),
                    new Vector3(bounds.size.x * HangarInset, height, bounds.size.z * HangarInset),
                    HangarShade, emissive: false, keepCollider: false);
                shade.name = "Hangar Shade";
                shade.transform.SetParent(_set, true);
                var renderer = shade.GetComponent<Renderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void BuildFill()
        {
            ShotFill fill = _spec.planeFill;
            if (fill == null || fill.intensity <= 0f) return;

            var light = new GameObject("Shot Plane Fill").AddComponent<Light>();
            light.transform.SetParent(_set, false);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(fill.pitch, _spec.camera.yaw + fill.yaw, 0f);
            light.intensity = fill.intensity;
            light.color = fill.color != null && fill.color.Length >= 3
                ? new Color(fill.color[0], fill.color[1], fill.color[2]) : Color.white;
            light.shadows = LightShadows.None;
            light.GetUniversalAdditionalLightData().renderingLayers = FillLayer;
        }

        void BuildZeppelin(ShotZeppelin spec)
        {
            float[] screen = spec.screen != null && spec.screen.Length >= 2
                ? spec.screen : new[] { 0.5f, 0.7f };
            Vector3 end = Air(screen, spec.distance > 0f ? spec.distance : DefaultZeppelinDistance);
            Quaternion frame = Quaternion.Euler(0f, _spec.camera.yaw, 0f);
            Vector3 forward = frame * Quaternion.Euler(0f, spec.course, 0f) * Vector3.forward;
            Vector3 velocity = forward * spec.speed;

            var root = new GameObject("Shot Zeppelin").transform;
            root.SetParent(_set, false);
            root.SetPositionAndRotation(end - velocity * _spec.warmup, Orientation(-forward, Vector3.up));

            Transform model = SkyZeppelin.BuildModel(root,
                spec.length > 0f ? spec.length : DefaultZeppelinLength);
            if (model == null)
            {
                Destroy(root.gameObject);
                return;
            }

            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.Off;
            _airships.Add(new Airship { tr = root, velocity = velocity });
        }

        void SurveyCoast()
        {
            var coast = FindFirstObjectByType<FlandersTerrain>();
            if (coast == null) return;

            Vector3 eye = _cam.transform.position;
            for (float dx = -300f; dx <= 300f; dx += 150f)
            {
                float x = eye.x + dx;
                for (float z = 0f; z < ShoreProbeFar; z += ShoreProbeStep)
                {
                    if (GroundY(x, z) > SeaSurface.Level) continue;
                    Debug.Log($"ShotDirector: waterline at x {x:F0} z {z:F0}");
                    break;
                }
            }

            float reach = _spec.clearCoast * _spec.clearCoast;
            foreach (Transform node in coast.GetComponentsInChildren<Transform>())
            {
                bool groyne = node.name == "Groyne";
                if (!groyne && node.name != "Bunker" && node.name != "Boat") continue;

                Vector3 view = _cam.WorldToViewportPoint(node.position);
                Debug.Log($"ShotDirector: coast {node.name} at {node.position} view ({view.x:F2}, {view.y:F2}, {view.z:F0})");

                Vector3 gap = node.position - eye;
                gap.y = 0f;
                if (gap.sqrMagnitude < reach && (!groyne || _spec.clearGroynes)) Destroy(node.gameObject);
            }
        }

        void BuildGroyne(ShotGroyne spec)
        {
            if (!FlandersGroyne.Measure()) return;
            Vector3 land = Ground(spec.screenX, spec.distance);
            Vector3 run = Quaternion.Euler(0f, _spec.camera.yaw + spec.angle, 0f) * Vector3.forward
                          * FlandersGroyne.Length;
            FlandersGroyne.Place(_set, land, land + run + Vector3.down * GroyneDrop, GroundY);
        }

        void BuildBunker(ShotBunker spec)
        {
            int model = Mathf.Clamp(spec.model, 0, FlandersBunker.Models - 1);
            if (!FlandersBunker.Has(model)) return;

            Vector3 at = Ground(spec.screenX, spec.distance);
            Quaternion rotation = Quaternion.Euler(0f, _spec.camera.yaw + spec.yaw, 0f);
            Bounds box = FlandersBunker.Footprint(model);
            float floor = at.y;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = at + rotation * new Vector3(sx * box.extents.x, 0f, sz * box.extents.z);
                floor = Mathf.Min(floor, GroundY(corner.x, corner.z));
            }

            Vector3 offset = rotation * new Vector3(box.center.x, 0f, box.center.z);
            float sink = spec.sink > 0f ? spec.sink : DefaultBunkerSink;
            FlandersBunker.Place(_set, model, new Vector3(at.x, floor - sink, at.z) - offset, rotation);
            Debug.Log($"ShotDirector: bunker {model} at {at}");
        }

        void BuildBoat(ShotBoat spec)
        {
            int model = spec.wreck ? FlandersBoat.Wreck : FlandersBoat.Intact;
            if (!FlandersBoat.Has(model)) return;

            float scale = PlaneModelConfig.UnitsPerMeter * BoatSize * (spec.scale > 0f ? spec.scale : 1f);
            Bounds hull = FlandersBoat.Hull(model);
            float length = hull.extents.z * scale, beam = hull.extents.x * scale;
            Vector3 at = Ground(spec.screenX, spec.distance);
            Vector3 f = Quaternion.Euler(0f, _spec.camera.yaw + spec.yaw, 0f) * Vector3.forward;
            var r = new Vector3(f.z, 0f, -f.x);

            float bow = GroundY(at.x + f.x * length, at.z + f.z * length);
            float stern = GroundY(at.x - f.x * length, at.z - f.z * length);
            float right = GroundY(at.x + r.x * beam, at.z + r.z * beam);
            float left = GroundY(at.x - r.x * beam, at.z - r.z * beam);

            Vector3 along = f * (2f * length) + Vector3.up * (bow - stern);
            Vector3 across = r * (2f * beam) + Vector3.up * (right - left);
            Quaternion frame = Quaternion.LookRotation(along, Vector3.Cross(along, across));
            float y = Mathf.Min(at.y, Mathf.Min((bow + stern) * 0.5f, (right + left) * 0.5f));

            FlandersBoat.Place(_set, model, new Vector3(at.x, y, at.z), frame, scale, spec.roll, spec.capsized,
                spec.sink > 0f ? spec.sink : DefaultBoatSink, 1f, BoatBleach);
        }

        void StageShips()
        {
            StageFleet(typeof(ThornycroftBoats), "thornycroft", "_boats");
            StageFleet(typeof(S150Boats), "s150", "_boats");
            StageFleet(typeof(LordCliveShips), "lordclive", "_ships");
        }

        void StageFleet(Type type, string kind, string listName)
        {
            var fleet = FindFirstObjectByType(type) as MonoBehaviour;
            if (fleet == null) return;

            var wanted = new List<ShotShip>();
            foreach (ShotShip ship in _spec.ships)
                if (string.Equals(ship.kind, kind, StringComparison.OrdinalIgnoreCase)) wanted.Add(ship);
            if (wanted.Count == 0)
            {
                if (_spec.ships.Length > 0 || !_spec.ambientShips) Destroy(fleet.gameObject);
                return;
            }

            var boats = (IList)type.GetField(listName, Hidden).GetValue(fleet);
            MethodInfo spawn = type.GetMethod("Spawn", Hidden);
            while (boats.Count < wanted.Count) spawn.Invoke(fleet, new object[] { _cam.transform.position, true });

            MethodInfo release = type.GetMethod("Release", BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = boats.Count - 1; i >= wanted.Count; i--)
            {
                release.Invoke(null, new[] { boats[i] });
                boats.RemoveAt(i);
            }

            bool shells = false;
            for (int i = 0; i < wanted.Count; i++)
            {
                StageShip(boats[i], wanted[i]);
                shells |= wanted[i].shells;
            }

            FieldInfo pending = type.GetField("_shells", Hidden);
            if (!shells && pending != null) _mutedShells.Add((IList)pending.GetValue(fleet));
        }

        void StageShip(object boat, ShotShip spec)
        {
            Type type = boat.GetType();
            var root = (Transform)type.GetField("root").GetValue(boat);
            FieldInfo speedField = type.GetField("speed");
            float speed = (float)speedField.GetValue(boat) * (spec.speed > 0f ? spec.speed : 1f);
            speedField.SetValue(boat, speed);

            Vector3 end = Sea(spec.screenX, spec.distance);
            root.position = end + Vector3.right * (speed * _spec.warmup);
            _vessels.Add(new Vessel { root = root, end = end, speed = speed });
            Debug.Log($"ShotDirector: {root.name} ends at {end}");

            FieldInfo gunsField = type.GetField("guns");
            if (gunsField == null) return;
            var all = (IList)gunsField.GetValue(boat);
            var guns = new List<object>();
            for (int i = 0; i < all.Count; i++)
            {
                all[i].GetType().GetField("wait").SetValue(all[i], GunHold);
                if (spec.guns == null || spec.guns.Length == 0 || Array.IndexOf(spec.guns, i) >= 0) guns.Add(all[i]);
            }

            if (spec.volleys == null) return;
            foreach (float age in spec.volleys)
                Schedule(_spec.warmup - Mathf.Max(0f, age) - StepSec * 0.5f, () => Volley(guns));
        }

        static void Volley(IList guns)
        {
            foreach (object gun in guns)
            {
                Type type = gun.GetType();
                type.GetField("wait").SetValue(gun, 0f);
                type.GetField("yawGoal").SetValue(gun, type.GetField("yawNow").GetValue(gun));
                type.GetField("liftGoal").SetValue(gun, type.GetField("liftNow").GetValue(gun));
            }
        }

        void BuildGulls(ShotGull spec)
        {
            var prefab = Resources.Load<GameObject>(GullModel);
            if (prefab == null)
            {
                Debug.LogError($"ShotDirector: {GullModel} not found in Resources.");
                return;
            }

            var probe = Instantiate(prefab);
            probe.transform.position = new Vector3(0f, ProbeY, 0f);
            Renderer[] renderers = probe.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Destroy(probe);
                return;
            }
            Bounds box = renderers[0].bounds;
            foreach (Renderer r in renderers) box.Encapsulate(r.bounds);
            Transform beak = PlaneFactory.FindDeep(probe.transform, GullBeak);
            Quaternion flip = Quaternion.Euler(0f, beak != null && beak.position.z < box.center.z ? 180f : 0f, 0f);
            Vector3 centre = box.center - probe.transform.position;
            Destroy(probe);

            float[] screen = spec.screen != null && spec.screen.Length >= 2 ? spec.screen : new[] { 0.5f, 0.8f };
            Vector3 at = Air(screen, spec.distance > 0f ? spec.distance : DefaultGullDistance);
            int count = spec.count > 0 ? spec.count : 5;
            float spread = spec.spread > 0f ? spec.spread : DefaultGullSpread;
            float bank = spec.bank != 0f ? spec.bank : DefaultGullBank;
            Transform eye = _cam.transform;

            var spots = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                Vector2 spot = Random.insideUnitCircle;
                for (int k = 0; k < GullTries && Crowded(spots, spot); k++) spot = Random.insideUnitCircle;
                spots.Add(spot);

                var root = new GameObject("Shot Gull").transform;
                root.SetParent(_set, false);
                root.localScale = Vector3.one * (PlaneModelConfig.UnitsPerMeter * GullSize
                                                 * Random.Range(1f - GullJitter, 1f + GullJitter));
                Transform view = Instantiate(prefab, root, false).transform;
                view.localPosition = flip * -centre;
                view.localRotation = flip * view.localRotation;
                foreach (Collider col in view.GetComponentsInChildren<Collider>()) Destroy(col);
                foreach (Renderer r in view.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }

                Vector3 offset = eye.right * (spot.x * spread) + Vector3.up * (spot.y * spread * 0.5f)
                                 + eye.forward * (Random.Range(-0.3f, 0.3f) * spread);
                root.SetPositionAndRotation(at + offset, Quaternion.Euler(
                    Random.Range(-GullPitchJitter, GullPitchJitter),
                    _spec.camera.yaw + spec.course + Random.Range(-GullYawJitter, GullYawJitter),
                    bank + Random.Range(-GullBankJitter, GullBankJitter)));
            }
        }

        static bool Crowded(List<Vector2> spots, Vector2 spot)
        {
            foreach (Vector2 other in spots)
                if (Vector2.Distance(other, spot) < GullGap) return true;
            return false;
        }

        void BuildProp(ShotProp spec) =>
            PlaceProp(spec.model, spec.kind, Ground(spec.screenX, spec.distance), spec.yaw,
                spec.scale, spec.sink, spec.pitch, spec.tilt, spec.burning, spec.smokeScale);

        Transform PlaceProp(string model, string kind, Vector3 at, float yaw, float size, float sink,
            float pitch, float tilt, bool burning, float smokeScale = 0f)
        {
            var prefab = Resources.Load<GameObject>(PropFolder + model);
            if (prefab == null)
            {
                Debug.LogError($"ShotDirector: {PropFolder}{model} not found in Resources.");
                return null;
            }

            kind = string.IsNullOrEmpty(kind) ? "tree" : kind;
            bool rubble = kind == "rubble";
            float oversize = kind == "tank" || kind == "vehicle" ? 1.15f : rubble ? 1f : 1.5f;
            float scale = BattlefieldProps.MetreScale * oversize * (size > 0f ? size : 1f);
            Quaternion standUp = rubble ? Quaternion.identity : StandUp;
            Bounds bounds = Measure(prefab, standUp);

            var root = new GameObject(model);
            root.transform.SetParent(_set, false);
            root.transform.SetPositionAndRotation(at - Vector3.up * sink,
                Quaternion.Euler(pitch, yaw, tilt));
            root.transform.localScale = Vector3.one * scale;

            var view = Instantiate(prefab, root.transform);
            view.transform.localRotation = standUp;
            view.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            NightReceivers.Adopt(view);
            foreach (var r in view.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;
            if (kind == "tank") TankSkin.Apply(view.transform);

            if (!burning) return root.transform;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * scale;
            int seed = Random.Range(0, int.MaxValue);
            WreckFire.Begin(_set, at, radius, seed, smokeScale == 0f);
            if (smokeScale > 0f)
                SmokeColumn.Begin(_set, at + Vector3.up * (radius * WreckSmokeLift), seed, smokeScale);
            return root.transform;
        }

        static Bounds Measure(GameObject prefab, Quaternion rotation)
        {
            var probe = Instantiate(prefab);
            probe.transform.SetPositionAndRotation(new Vector3(0f, ProbeY, 0f), rotation);

            var renderers = probe.GetComponentsInChildren<Renderer>();
            var bounds = renderers.Length > 0
                ? renderers[0].bounds
                : new Bounds(new Vector3(0f, ProbeY, 0f), Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Destroy(probe);

            bounds.center -= new Vector3(0f, ProbeY, 0f);
            return bounds;
        }

        void BuildSquad(ShotSquad spec)
        {
            if (BuildFigure == null) return;
            if (_rifleTracers == null) _rifleTracers = SquadTracers.Begin(_set);

            Vector3 centre = Ground(spec.screenX, spec.distance);
            int count = spec.count > 0 ? spec.count : 5;
            float spread = spec.spread > 0f ? spec.spread : 16f;
            bool atSquad = spec.atSquad && spec.target >= 0 && spec.target < _spec.squads.Length;
            ShotSquad foe = atSquad ? _spec.squads[spec.target] : null;
            Vector3 foeCentre = atSquad ? Ground(foe.screenX, foe.distance) : Vector3.zero;
            float foeSpread = atSquad ? (foe.spread > 0f ? foe.spread : 16f) : 0f;
            Vector3 goal = atSquad ? foeCentre + Vector3.up * ChestHeight
                : spec.target >= 0 && spec.target < _flyers.Count
                    ? _flyers[spec.target].positions[0]
                    : centre + Vector3.up * 200f;
            float scatter = atSquad ? foeSpread * 0.5f : RifleScatter;
            var spots = new List<Vector2>();
            if (atSquad && spec.cover > 0)
            {
                Vector3 toward = goal - centre;
                toward.y = 0f;
                toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward;
                Vector3 toCamera = _cam.transform.position - centre;
                toCamera.y = 0f;
                toCamera = toCamera.sqrMagnitude > 1e-4f ? toCamera.normalized : -toward;
                float angle = (spec.coverAngle != 0f ? spec.coverAngle : DefaultCoverAngle) * Mathf.Deg2Rad;
                Vector3 face = Vector3.RotateTowards(toward, toCamera, angle, 0f);
                BuildWall(spec, centre, face, spread);
            }
            Vector3 right = _cam.transform.right;
            var lateral = new Vector2(right.x, right.z).normalized;
            for (int i = 0; i < count; i++) spots.Add(FreeSpot(centre, spread, lateral, spots));

            foreach (Vector2 spot in spots)
            {
                float x = spot.x, z = spot.y;
                var figure = (Transform)BuildFigure.Invoke(null,
                    new object[] { _set, Mathf.Clamp(spec.faction, 0, 1), Random.Range(0, 3) });
                figure.position = new Vector3(x, GroundY(x, z), z);

                Vector3 shoulder = figure.position + Vector3.up * ShoulderHeight;
                Vector3 aim = (goal + Random.insideUnitSphere * scatter - shoulder).normalized;
                Vector3 flat = new Vector3(aim.x, 0f, aim.z);
                figure.rotation = Quaternion.LookRotation(flat.sqrMagnitude > 1e-4f ? flat : Vector3.forward);

                if (spec.unarmed) continue;

                Vector3 hand = figure.right * 1.8f;
                var rifle = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    shoulder + hand + aim * (RifleLength * 0.35f),
                    new Vector3(RifleWidth, RifleWidth, RifleLength), RifleColor,
                    emissive: false, keepCollider: false);
                rifle.name = "Rifle";
                rifle.transform.SetParent(figure, true);
                rifle.transform.rotation = Quaternion.LookRotation(aim);

                if (!spec.fire) continue;
                _riflemen.Add(new Rifleman
                {
                    muzzle = shoulder + hand + aim * (RifleLength * 0.85f),
                    aim = aim,
                    target = spec.target,
                    nextShot = Random.Range(0f, RifleIntervalMax),
                    atSquad = atSquad,
                    foe = foeCentre,
                    foeSpread = foeSpread,
                });
            }
        }

        static Vector2 FreeSpot(Vector3 centre, float spread, Vector2 lateral, List<Vector2> taken)
        {
            Vector2 best = Vector2.zero;
            float bestGap = -1f;
            for (int k = 0; k < SoldierTries && bestGap < SoldierGap; k++)
            {
                Vector2 spot = new Vector2(centre.x, centre.z) + Random.insideUnitCircle * spread;
                float gap = float.MaxValue;
                foreach (Vector2 other in taken) gap = Mathf.Min(gap, Mathf.Abs(Vector2.Dot(other - spot, lateral)));
                if (gap <= bestGap) continue;
                best = spot;
                bestGap = gap;
            }
            return best;
        }

        void BuildWall(ShotSquad spec, Vector3 centre, Vector3 face, float spread)
        {
            string[] models = spec.coverModels != null && spec.coverModels.Length > 0 ? spec.coverModels : CoverModels;
            float size = spec.coverScale > 0f ? spec.coverScale : 1f;
            Vector3 along = Vector3.Cross(Vector3.up, face);
            Vector3 broad = _cam.transform.right;
            broad.y = 0f;
            broad = broad.sqrMagnitude > 1e-4f ? broad.normalized : along;
            float baseYaw = Quaternion.LookRotation(broad).eulerAngles.y;
            float lengthwise = Mathf.Abs(Vector3.Dot(broad, along));
            float crosswise = Mathf.Sqrt(Mathf.Max(0f, 1f - lengthwise * lengthwise));

            var bags = new List<Bag>();
            var steps = new List<float>();
            float thickness = 0f, total = 0f;
            for (int i = 0; i < spec.cover; i++)
            {
                Bag bag = PickBag(models, size);
                bags.Add(bag);
                steps.Add((lengthwise * bag.length + crosswise * 2f * bag.depth) * WallPack);
                total += steps[i];
                thickness = Mathf.Max(thickness, crosswise * bag.length * 0.5f + lengthwise * bag.depth);
            }
            Vector3 mid = centre + face * (spread + WallGap + thickness);

            var joints = new List<float>();
            float offset = -total * 0.5f;
            for (int i = 0; i < bags.Count; i++)
            {
                float step = steps[i];
                LayBag(spec, bags[i], mid + along * (offset + step * 0.5f), along, face, baseYaw, 0f, WallTwist);
                offset += step;
                if (i < bags.Count - 1) joints.Add(offset);
            }

            int top = Mathf.Min(joints.Count, spec.cover / 2);
            for (int n = 0; n < top; n++)
            {
                int i = Random.Range(0, joints.Count);
                float lift = Mathf.Max(bags[i].height, bags[i + 1].height) * StackLift;
                LayBag(spec, PickBag(models, size), mid + along * joints[i], along, face, baseYaw, lift, TopTwist);
                joints.RemoveAt(i);
                bags.RemoveAt(i);
            }
        }

        Bag PickBag(string[] models, float size)
        {
            string model = models[Random.Range(0, models.Length)];
            var prefab = Resources.Load<GameObject>(PropFolder + model);
            float scale = size * Random.Range(1f - BagSizeJitter, 1f + BagSizeJitter);
            if (prefab == null) return new Bag { model = model, scale = scale };

            Vector3 extents = Measure(prefab, Quaternion.identity).extents * (BattlefieldProps.MetreScale * scale);
            bool wide = extents.x > extents.z;
            return new Bag
            {
                model = model,
                scale = scale,
                turn = wide ? 90f : 0f,
                length = 2f * (wide ? extents.x : extents.z),
                depth = wide ? extents.z : extents.x,
                height = 2f * extents.y,
            };
        }

        void LayBag(ShotSquad spec, Bag bag, Vector3 at, Vector3 along, Vector3 across, float baseYaw, float lift, float twist)
        {
            at += along * Random.Range(-WallSlide, WallSlide) + across * Random.Range(-WallBulge, WallBulge);
            at.y = GroundY(at.x, at.z) + lift;
            float yaw = baseYaw + bag.turn + (Random.value < 0.5f ? 0f : 180f) + Random.Range(-twist, twist);
            Transform piece = PlaceProp(bag.model, "rubble", at, yaw, bag.scale, lift > 0f ? 0f : CoverSink,
                Random.Range(-WallTilt, WallTilt), Random.Range(-WallTilt, WallTilt), false);
            if (piece != null && spec.coverTint != null && spec.coverTint.Length >= 3) Tint(piece, spec.coverTint);
        }

        static void Tint(Transform root, float[] tint)
        {
            var block = new MaterialPropertyBlock();
            var color = new Color(tint[0], tint[1], tint[2]);
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(block);
            }
        }

        void StampCrater(ShotCrater spec)
        {
            Vector3 at = Ground(spec.screenX, spec.distance);
            float radius = spec.radius > 0f ? spec.radius : DefaultCraterRadius;
            float depth = radius * (spec.depth > 0f ? spec.depth : DefaultCraterDepth);
            foreach (Terrain terrain in Terrain.activeTerrains)
                if (terrain != null && terrain.terrainData != null) Dig(terrain, at, radius, depth);
            Debug.Log($"ShotDirector: crater at {at}, radius {radius}");
        }

        static void Dig(Terrain terrain, Vector3 at, float radius, float depth)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 size = data.size;
            float reach = radius * CraterReach;

            int res = data.heightmapResolution;
            float stepX = size.x / (res - 1), stepZ = size.z / (res - 1);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((at.x - reach - origin.x) / stepX));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((at.x + reach - origin.x) / stepX));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((at.z - reach - origin.z) / stepZ));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((at.z + reach - origin.z) / stepZ));
            if (x0 > x1 || z0 > z1) return;

            float[,] heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int iz = 0; iz < heights.GetLength(0); iz++)
            {
                float dz = origin.z + (z0 + iz) * stepZ - at.z;
                for (int ix = 0; ix < heights.GetLength(1); ix++)
                {
                    float dx = origin.x + (x0 + ix) * stepX - at.x;
                    float r = Mathf.Sqrt(dx * dx + dz * dz);
                    if (r > reach) continue;
                    float delta = ProceduralTerrain.CraterDelta(r, radius, depth,
                        depth * CraterRim, radius * CraterRimSigma);
                    heights[iz, ix] = Mathf.Clamp01(heights[iz, ix] + delta / size.y);
                }
            }
            data.SetHeights(x0, z0, heights);

            int detailRes = data.detailResolution;
            if (detailRes <= 0 || data.detailPrototypes.Length == 0) return;
            float bare = radius * ProceduralTerrain.CraterBareRadii;
            int d0x = Mathf.Max(0, Mathf.FloorToInt((at.x - bare - origin.x) / size.x * detailRes));
            int d1x = Mathf.Min(detailRes - 1, Mathf.CeilToInt((at.x + bare - origin.x) / size.x * detailRes));
            int d0z = Mathf.Max(0, Mathf.FloorToInt((at.z - bare - origin.z) / size.z * detailRes));
            int d1z = Mathf.Min(detailRes - 1, Mathf.CeilToInt((at.z + bare - origin.z) / size.z * detailRes));
            if (d0x > d1x || d0z > d1z) return;

            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                int[,] grass = data.GetDetailLayer(d0x, d0z, d1x - d0x + 1, d1z - d0z + 1, layer);
                for (int iz = 0; iz < grass.GetLength(0); iz++)
                {
                    float dz = origin.z + (d0z + iz + 0.5f) / detailRes * size.z - at.z;
                    for (int ix = 0; ix < grass.GetLength(1); ix++)
                    {
                        float dx = origin.x + (d0x + ix + 0.5f) / detailRes * size.x - at.x;
                        if (dx * dx + dz * dz < bare * bare) grass[iz, ix] = 0;
                    }
                }
                data.SetDetailLayer(d0x, d0z, layer, grass);
            }
        }

        void BuildCloud(ShotCloud spec)
        {
            float[] screen = spec.screen != null && spec.screen.Length >= 2
                ? spec.screen : new[] { 0.5f, 0.85f };
            Vector3 at = Air(screen, spec.distance > 0f ? spec.distance : 600f);
            float width = spec.width > 0f ? spec.width : DefaultCloudWidth;
            Color tint = _spec.arena.on
                ? CoastSky.CloudColor(SpecDaytime, SpecWeather) : CloudSystem.TintFor(SpecDaytime);
            Color glow = _spec.arena.on
                ? CoastSky.CloudGlow(SpecDaytime, SpecWeather) : CloudSystem.GlowFor(SpecDaytime);
            if (spec.color != null && spec.color.Length >= 3)
            {
                float share = glow.maxColorComponent / Mathf.Max(0.001f, tint.maxColorComponent);
                tint = new Color(spec.color[0], spec.color[1], spec.color[2]);
                glow = tint * share;
            }
            Material mat = CloudSystem.CloudMaterial(tint, glow,
                spec.alpha > 0f ? spec.alpha : DefaultCloudAlpha);

            var root = new GameObject("Shot Cloud").transform;
            root.SetParent(_set, false);
            root.SetPositionAndRotation(at, Quaternion.Euler(0f, _spec.camera.yaw, 0f));
            for (int i = 0; i < CloudBlobs; i++)
            {
                var go = new GameObject("Blob");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(
                    Random.Range(-0.5f, 0.5f), Random.Range(-0.18f, 0.18f), Random.Range(-0.1f, 0.1f)) * width;
                go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float s = width * Random.Range(0.35f, 0.55f);
                go.transform.localScale = new Vector3(s * Random.Range(1.1f, 1.7f),
                    s * Random.Range(0.75f, 1.05f), s * Random.Range(1.1f, 1.7f));
                go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
                var renderer = go.AddComponent<MeshRenderer>();
                if (mat != null) renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        void BuildPuff(ShotCloud spec)
        {
            float[] screen = spec.screen != null && spec.screen.Length >= 2
                ? spec.screen : new[] { 0.5f, 0.85f };
            Vector3 at = Air(screen, spec.distance > 0f ? spec.distance : 500f);
            float width = spec.width > 0f ? spec.width : DefaultPuffWidth;
            Color tint = spec.color != null && spec.color.Length >= 3
                ? new Color(spec.color[0], spec.color[1], spec.color[2]) : PuffColor;
            float alpha = spec.alpha > 0f ? spec.alpha : DefaultPuffAlpha;
            Color halo = Color.Lerp(tint * PuffHaloLighten, Color.white, 0.1f);
            Material core = CloudSystem.CloudMaterial(tint, tint * PuffGlow, alpha);
            Material outer = CloudSystem.CloudMaterial(halo, halo * PuffGlow, alpha * PuffHaloAlpha);

            var root = new GameObject("Shot Puff").transform;
            root.SetParent(_set, false);
            root.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            for (int i = 0; i < PuffBlobs; i++) AddPuffBlob(root, core, width, PuffReach, 0.35f, 0.55f);
            for (int i = 0; i < PuffHaloBlobs; i++) AddPuffBlob(root, outer, width, PuffHaloReach, 0.45f, 0.7f);
        }

        static void AddPuffBlob(Transform root, Material mat, float width, float reach, float min, float max)
        {
            var go = new GameObject("Blob");
            go.transform.SetParent(root, false);
            go.transform.localPosition = Random.insideUnitSphere * (reach * width);
            go.transform.localRotation = Random.rotation;
            float s = width * Random.Range(min, max);
            go.transform.localScale = new Vector3(s * Random.Range(0.9f, 1.2f),
                s * Random.Range(0.85f, 1.1f), s * Random.Range(0.9f, 1.2f));
            go.AddComponent<MeshFilter>().sharedMesh = BlobMesh.Pick();
            var renderer = go.AddComponent<MeshRenderer>();
            if (mat != null) renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void BuildVillagers(ShotVillager spec)
        {
            if (BuildVillager == null) return;

            Vector3 forward = _cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 centre = Ground(spec.screenX, spec.distance);
            var rng = new System.Random(_spec.seed + Mathf.RoundToInt(spec.distance * 10f + spec.screenX * 1000f));

            for (int i = 0; i < Mathf.Max(1, spec.count); i++)
            {
                Vector3 p = centre + right * Random.Range(-spec.spread, spec.spread)
                                   + forward * Random.Range(-spec.spread, spec.spread) * 0.3f;
                p.y = GroundY(p.x, p.z);
                var figure = (Transform)BuildVillager.Invoke(null, new object[] { _set, rng });
                figure.SetPositionAndRotation(p, Quaternion.Euler(0f,
                    _spec.camera.yaw + spec.yaw + Random.Range(-VillagerTurn, VillagerTurn), 0f));
            }
        }

        void LogVillagers()
        {
            foreach (var people in FindObjectsByType<VillagePeople>(FindObjectsSortMode.None))
                foreach (Transform walker in people.transform)
                {
                    Vector3 view = _cam.WorldToViewportPoint(walker.position);
                    if (view.z <= 0f || view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f) continue;
                    Debug.Log($"ShotDirector: villager at {walker.position} view ({view.x:F2}, {view.y:F2}, {view.z:F0})");
                }
        }

        void BuildMist()
        {
            ShotMistSpec spec = _spec.mist;
            if (spec == null || spec.strength <= 0f) return;

            Vector3 eye = _cam.transform.position;
            var area = new Rect(eye.x - MistMargin, eye.z - MistMargin, MistMargin * 2f, MistMargin * 2f);
            Color color = Color.Lerp(RenderSettings.fogColor, Color.white, Mathf.Clamp01(spec.whiten));
            float top = spec.top > 0f ? spec.top : 2f;
            float clear = Mathf.Max(top + 1f, spec.clear > 0f ? spec.clear : 18f);
            float far = spec.far > 0f ? spec.far : 600f;
            float size = spec.patchSize > 0f ? spec.patchSize : 180f;

            ShotMist.Attach(_cam, GroundY, area, color,
                new Vector4(top, clear, Mathf.Clamp01(spec.strength), 0f),
                new Vector4(spec.near, far, Mathf.Clamp01(spec.nearShare), 0f),
                new Vector4(1f / size, Mathf.Clamp01(spec.patchiness), _spec.seed * 0.137f, _spec.seed * 0.291f));
        }

        Vector3 TargetPoint(int index, Vector3 fallback) =>
            index >= 0 && index < _flyers.Count ? _flyers[index].tr.position : fallback;

        void BuildSmoke()
        {
            for (int i = 0; i < _spec.smoke.Length; i++)
            {
                ShotSmoke spec = _spec.smoke[i];
                var column = SmokeColumn.Begin(_set, Ground(spec.screenX, spec.distance),
                    _spec.seed * 31 + i, spec.scale > 0f ? spec.scale : 1f);
                if (spec.dense) column.Thicken();
            }
        }

        void ScheduleBursts()
        {
            foreach (ShotBurst burst in _spec.groundBlasts)
            {
                if (burst.screen == null || burst.screen.Length < 1) continue;
                Vector3 at = Ground(burst.screen[0], burst.distance);
                float size = burst.size > 0f ? burst.size : 60f;
                Schedule(_spec.warmup - burst.age, () => GroundBlast.Spawn(at, size, _cam.transform.position));
            }

            foreach (ShotBurst burst in _spec.flak)
            {
                if (burst.screen == null || burst.screen.Length < 2) continue;
                Vector3 at = Air(burst.screen, burst.distance);
                float size = burst.size > 0f ? burst.size : 40f;
                Schedule(_spec.warmup - burst.age,
                    () => FlakBurst.Spawn(at, size, _cam.transform.position, false));
            }

            foreach (ShotBurst burst in _spec.explosions)
            {
                if (burst.screen == null || burst.screen.Length < (burst.ground ? 1 : 2)) continue;
                float size = burst.size > 0f ? burst.size : 30f;
                Vector3 at = burst.ground
                    ? Ground(burst.screen[0], burst.distance) + Vector3.up * (size * FireballLift)
                    : Air(burst.screen, burst.distance);
                Schedule(_spec.warmup - burst.age, () => Explosion.Spawn(at, size, 0f));
            }

            foreach (ShotBurst burst in _spec.splashes)
            {
                if (burst.screen == null || burst.screen.Length < 1) continue;
                Vector3 at = Sea(burst.screen[0], burst.distance);
                float size = burst.size > 0f ? burst.size : DefaultSplashSize;
                Schedule(_spec.warmup - burst.age, () => WaterSplash.Spawn(at, size, _cam.transform.position));
            }
        }

        void Schedule(float at, Action run) => _cues.Add(new Cue { at = Mathf.Max(0f, at), run = run });

        void Tick(float clock, bool last)
        {
            float dt = clock - _clock;
            _clock = clock;
            float remaining = _spec.warmup - clock;

            for (int i = _cues.Count - 1; i >= 0; i--)
            {
                if (_cues[i].at > clock) continue;
                Action run = _cues[i].run;
                _cues.RemoveAt(i);
                run();
            }

            foreach (IList shells in _mutedShells) shells.Clear();
            foreach (Flyer flyer in _flyers) Place(flyer, remaining);
            foreach (Flyer flyer in _flyers)
            {
                if (flyer.rearPivot != null) AimRearGun(flyer);
                if (!flyer.spec.fire) continue;

                float window = flyer.spec.fireFor > 0f ? flyer.spec.fireFor : DefaultFireFor;
                if (remaining > window || clock < flyer.nextShot) continue;
                flyer.nextShot = clock
                                 + (flyer.spec.fireInterval > 0f ? flyer.spec.fireInterval : BoltInterval);
                Shoot(flyer);
            }

            foreach (Rifleman man in _riflemen)
            {
                if (clock < man.nextShot) continue;
                man.nextShot = clock + Random.Range(RifleIntervalMin, RifleIntervalMax);
                if (man.atSquad)
                {
                    Vector2 spot = Random.insideUnitCircle * man.foeSpread;
                    float x = man.foe.x + spot.x, z = man.foe.z + spot.y;
                    _rifleTracers.Fire(man.muzzle, new Vector3(x,
                        GroundY(x, z) + ChestHeight + Random.Range(FoeScatterLow, FoeScatterHigh), z));
                    continue;
                }
                Vector3 goal = TargetPoint(man.target, man.muzzle + man.aim * 300f);
                _rifleTracers.Fire(man.muzzle, goal + Random.insideUnitSphere * RifleScatter);
            }

            MoveBolts(dt);
            MoveShards(dt);
            foreach (Airship ship in _airships) ship.tr.position += ship.velocity * dt;
            foreach (Vessel vessel in _vessels)
                if (vessel.root != null)
                    vessel.root.position = vessel.end + Vector3.right * (vessel.speed * (remaining + StepSec));
            if (_spec.clearNear > 0f) ClearNear();
            if (_spec.clearHerds > 0f || !_spec.ambientSpruces) ClearHerds();

            if (!last) return;
            LogVillagers();
            foreach (Flyer flyer in _flyers)
                if (!flyer.spec.sharpProp) BlurPropeller(flyer);
            foreach (Flyer flyer in _flyers)
                if (flyer.spec.fireGlow > 0f) GlowFire(flyer);
            foreach (Flyer flyer in _flyers)
                if (flyer.tips != null)
                    foreach (Vector3 tip in flyer.tips) BuildStreak(flyer, tip);
            foreach (Flyer flyer in _flyers)
            {
                if (!flyer.spec.fire) continue;
                float scale = CaptureFlashScale * (flyer.spec.flashScale > 0f ? flyer.spec.flashScale : 1f);
                if (flyer.rearPivot != null) Flash(RearPoint(flyer), flyer.aim, flyer.Up, flyer.size * scale);
                else Flash(GunPoint(flyer), flyer.Forward, flyer.Up, flyer.size * scale);
            }
            foreach (Rifleman man in _riflemen)
                if (Random.value < RifleFlashShare)
                    Flash(man.muzzle, man.aim, Vector3.up, RifleFlashSize);
        }

        static void GlowFire(Flyer flyer)
        {
            float[] tint = flyer.spec.fireColor;
            bool custom = tint != null && tint.Length >= 6;
            Color deep = custom ? new Color(tint[0], tint[1], tint[2]) : FireDeep;
            Color hot = custom ? new Color(tint[3], tint[4], tint[5]) : FireHot;
            int index = 0;
            foreach (PlaneFire fire in flyer.tr.GetComponentsInChildren<PlaneFire>())
            {
                fire.enabled = false;
                foreach (Renderer r in fire.GetComponentsInChildren<Renderer>())
                {
                    Material mat = r.sharedMaterial;
                    if (mat == null) continue;
                    Color color = Color.Lerp(deep, hot, index++ % 3 / 2f);
                    mat.SetColor(BaseColorId, color * FireBase);
                    mat.SetColor(EmissionColorId, color * flyer.spec.fireGlow);
                }
            }
        }

        static Vector3 SweptTip(Vector3 tip) => tip - new Vector3(Mathf.Abs(tip.z) * StreakSweepBack, 0f, 0f);

        void BuildStreak(Flyer flyer, Vector3 tip)
        {
            int reach = Mathf.Min(flyer.positions.Length - 1, Mathf.CeilToInt(StreakLife / PathStep));
            int count = reach / StreakStride + 1;
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int k = Mathf.Min(i * StreakStride, reach);
                points[i] = flyer.positions[k] + Orientation(flyer.forwards[k], flyer.ups[k]) * tip;
            }

            if (_streakMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                _streakMaterial = new Material(shader != null ? shader : NightReceivers.Lit) { name = "Shot Streak (runtime)" };
                bool solid = _spec.focus != null && _spec.focus.farEnd > 0f;
                _streakMaterial.SetColor(BaseColorId, new Color(1f, 1f, 1f, solid ? 1f : StreakAlpha));
                if (solid)
                {
                    _streakMaterial.EnableKeyword("_EMISSION");
                    _streakMaterial.SetColor("_EmissionColor", Color.white * StreakGlow);
                }
                else UIFactory.MakeTransparent(_streakMaterial);
            }

            var line = new GameObject("Shot Wing Streak").AddComponent<LineRenderer>();
            line.transform.SetParent(_set, false);
            line.useWorldSpace = true;
            line.positionCount = count;
            line.SetPositions(points);
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f));
            line.widthMultiplier = flyer.spec.streakWidth > 0f ? flyer.spec.streakWidth : StreakWidth;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.sharedMaterial = _streakMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        void BlurPropeller(Flyer flyer)
        {
            foreach (PropellerSpin spin in flyer.tr.GetComponentsInChildren<PropellerSpin>())
                BlurPropeller(spin);
        }

        void BlurPropeller(PropellerSpin spin)
        {
            spin.enabled = false;

            Vector3 axis = (spin.axisSpace != null
                ? spin.axisSpace.rotation * spin.axisInSpace : spin.transform.right).normalized;
            MeshRenderer[] parts = spin.GetComponentsInChildren<MeshRenderer>();
            if (parts.Length == 0) return;

            Bounds hub = parts[0].bounds;
            foreach (MeshRenderer part in parts) hub.Encapsulate(part.bounds);
            Vector3 centre = hub.center;

            var reach = new float[parts.Length];
            float radius = 0f;
            for (int i = 0; i < parts.Length; i++)
            {
                reach[i] = Reach(parts[i], centre, axis);
                radius = Mathf.Max(radius, reach[i]);
            }
            if (radius <= 0f) return;

            for (int i = 0; i < parts.Length; i++)
            {
                if (reach[i] < radius * PropBladeShare) continue;
                SmearBlade(parts[i], centre, axis);
                parts[i].enabled = false;
            }

            Quaternion facing = Quaternion.LookRotation(axis);
            float disc = radius * PropDiscFit;
            BuildRing(spin.transform, centre, facing, 0f, disc, PropDiscAlpha);
            BuildRing(spin.transform, centre, facing, disc * PropTipInner, disc, PropTipAlpha);
        }

        static float Reach(MeshRenderer part, Vector3 centre, Vector3 axis)
        {
            var filter = part.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return 0f;

            Bounds bounds = filter.sharedMesh.bounds;
            float reach = 0f;
            foreach (Vector3 corner in BoxCorners)
            {
                Vector3 point = part.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, corner));
                reach = Mathf.Max(reach, Vector3.ProjectOnPlane(point - centre, axis).magnitude);
            }
            return reach;
        }

        void SmearBlade(MeshRenderer source, Vector3 centre, Vector3 axis)
        {
            Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;
            Material original = source.sharedMaterial;
            Color tint = original != null && original.HasColor(BaseColorId) ? original.GetColor(BaseColorId) : Color.white;
            Texture map = original != null && original.HasTexture(BaseMapId) ? original.GetTexture(BaseMapId) : null;
            Material fade = FadeMaterial(map);
            var materials = new Material[Mathf.Max(1, source.sharedMaterials.Length)];
            for (int m = 0; m < materials.Length; m++) materials[m] = fade;

            var block = new MaterialPropertyBlock();
            for (int i = 0; i < PropGhosts; i++)
            {
                float t = i / (float)PropGhosts;
                var go = new GameObject("Prop Ghost");
                go.transform.SetParent(source.transform.parent, false);
                go.transform.localScale = source.transform.localScale;
                go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                go.transform.RotateAround(centre, axis, -PropArc * t);

                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                source.GetPropertyBlock(block);
                Color color = tint;
                color.a = PropGhostAlpha * Mathf.Pow(1f - t, PropGhostFalloff);
                block.SetColor(BaseColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }

        void BuildRing(Transform parent, Vector3 centre, Quaternion facing, float inner, float outer, float alpha)
        {
            int n = PropDiscSegments;
            var vertices = new Vector3[n * 4];
            var normals = new Vector3[n * 4];
            var triangles = new int[n * 12];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                for (int side = 0; side < 2; side++)
                {
                    int v = side * n * 2 + i * 2;
                    vertices[v] = dir * inner;
                    vertices[v + 1] = dir * outer;
                    normals[v] = normals[v + 1] = side == 0 ? Vector3.back : Vector3.forward;
                }
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                for (int side = 0; side < 2; side++)
                {
                    int o = side * n * 2;
                    int a0 = o + i * 2, a1 = o + i * 2 + 1, b0 = o + j * 2, b1 = o + j * 2 + 1;
                    int k = side * n * 6 + i * 6;
                    if (side == 0)
                    {
                        triangles[k] = a0; triangles[k + 1] = b0; triangles[k + 2] = a1;
                        triangles[k + 3] = a1; triangles[k + 4] = b0; triangles[k + 5] = b1;
                    }
                    else
                    {
                        triangles[k] = a0; triangles[k + 1] = a1; triangles[k + 2] = b0;
                        triangles[k + 3] = a1; triangles[k + 4] = b1; triangles[k + 5] = b0;
                    }
                }
            }

            var mesh = new Mesh { name = "Prop Disc", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();

            var go = new GameObject("Prop Disc");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(centre, facing);
            go.transform.localScale = Vector3.one;
            go.transform.localScale = new Vector3(1f / go.transform.lossyScale.x,
                1f / go.transform.lossyScale.y, 1f / go.transform.lossyScale.z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            Material mat = FadeMaterial(null);
            Color color = PropDiscColor;
            color.a = alpha;
            mat.SetColor(BaseColorId, color);
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Material FadeMaterial(Texture map)
        {
            var mat = new Material(NightReceivers.Lit) { name = "Prop Blur (runtime)" };
            if (map != null) mat.SetTexture(BaseMapId, map);
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            return mat;
        }

        void ClearNear()
        {
            Vector3 eye = _cam.transform.position;
            float reach = _spec.clearNear * _spec.clearNear;
            foreach (var rubble in FindObjectsByType<BattlefieldRubble>(FindObjectsSortMode.None))
                ClearAround(rubble.transform, eye, reach);
            foreach (var props in FindObjectsByType<BattlefieldProps>(FindObjectsSortMode.None))
                ClearAround(props.transform, eye, reach);
        }

        static void ClearAround(Transform parent, Vector3 eye, float reach)
        {
            foreach (Transform child in parent)
            {
                Vector3 gap = child.position - eye;
                gap.y = 0f;
                if (gap.sqrMagnitude < reach) Destroy(child.gameObject);
            }
        }

        static Vector3 GunPoint(Flyer flyer) =>
            flyer.tr.TransformPoint(flyer.gun + Vector3.forward * (flyer.gunSpacing * flyer.gunSide));

        Vector3 RearPoint(Flyer flyer) =>
            flyer.rearPivot.TransformPoint(flyer.rearMuzzles[flyer.gunSide > 0 ? 1 : 0]);

        bool TryMark(Flyer flyer, out Flyer mark)
        {
            int index = flyer.spec.target;
            mark = index >= 0 && index < _flyers.Count && _flyers[index] != flyer ? _flyers[index] : null;
            return mark != null;
        }

        Vector3 AimPoint(Flyer flyer, Flyer mark, Vector3 muzzle)
        {
            float time = Vector3.Distance(muzzle, mark.tr.position) / BoltSpeed;
            float[] offset = flyer.spec.aimOffset;
            if (offset == null || offset.Length < 3)
                return mark.tr.position + mark.Velocity * (time - flyer.spec.aimLag);

            Transform eye = _cam.transform;
            Vector3 relative = mark.Velocity - flyer.Velocity;
            Vector3 goal = mark.tr.position - mark.Velocity * flyer.spec.aimLag
                           + eye.right * offset[0] + eye.up * offset[1] + eye.forward * offset[2];
            return goal + relative * InterceptTime(goal - muzzle, relative);
        }

        Vector3 StrikePoint(Flyer flyer, Flyer mark, Vector3 muzzle)
        {
            Vector3 local = mark.center + Vector3.right * (Random.Range(-StrikeSpread, StrikeSpread) * mark.size);
            Vector3 relative = mark.Velocity - flyer.Velocity;
            Vector3 goal = mark.tr.TransformPoint(local);
            return goal + relative * InterceptTime(goal - muzzle, relative);
        }

        static float InterceptTime(Vector3 gap, Vector3 relative)
        {
            float a = relative.sqrMagnitude - BoltSpeed * BoltSpeed;
            if (a >= -1f) return gap.magnitude / BoltSpeed;
            float b = 2f * Vector3.Dot(gap, relative);
            return (-b - Mathf.Sqrt(b * b - 4f * a * gap.sqrMagnitude)) / (2f * a);
        }

        void AimRearGun(Flyer flyer)
        {
            Vector3 pivot = flyer.rearPivot.position;
            Vector3 goal = flyer.aimed ? flyer.aimPoint
                : TryMark(flyer, out Flyer mark) ? AimPoint(flyer, mark, pivot) : pivot - flyer.Forward * 400f;
            Vector3 rest = flyer.tr.TransformDirection(flyer.rearRest);
            flyer.aim = (goal - pivot).normalized;
            flyer.rearPivot.rotation = Quaternion.FromToRotation(rest, flyer.aim) * flyer.tr.rotation;
        }

        void Shoot(Flyer flyer)
        {
            flyer.gunSide = -flyer.gunSide;
            bool rear = flyer.rearPivot != null;
            Vector3 muzzle = rear ? RearPoint(flyer) : GunPoint(flyer);

            Vector3 aimAt = flyer.aimed && !rear ? flyer.aimPoint
                : muzzle + (rear ? flyer.aim : flyer.Forward) * 400f;
            float life = BoltLife;
            bool strike = false;
            Flyer mark = null;
            if (!flyer.aimed && TryMark(flyer, out mark))
            {
                strike = flyer.spec.hitEvery > 0 && ++flyer.shots % flyer.spec.hitEvery == 0;
                aimAt = strike ? StrikePoint(flyer, mark, muzzle) : AimPoint(flyer, mark, muzzle);
                float reach = Vector3.Distance(muzzle, aimAt) / BoltSpeed;
                float overshoot = flyer.spec.overshoot > 0f ? flyer.spec.overshoot : BoltOvershoot;
                life = strike ? reach : Mathf.Min(BoltLife, reach + overshoot);
            }

            Vector3 dir = (aimAt - muzzle).normalized;
            if (!strike) dir = (dir + Random.insideUnitSphere * BoltSpread).normalized;
            SpawnBolt(muzzle, dir, dir * BoltSpeed + flyer.Velocity, life, strike ? mark : null);
            Flash(muzzle, rear ? dir : flyer.Forward, flyer.Up, flyer.size * GunFlashScale);
        }

        void SpawnBolt(Vector3 muzzle, Vector3 dir, Vector3 velocity, float life, Flyer mark)
        {
            if (_boltTemplate == null)
            {
                _boltTemplate = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(BoltThickness, BoltLength, BoltThickness), BoltColor,
                    emissive: true, keepCollider: false);
                var mat = _boltTemplate.GetComponent<Renderer>().sharedMaterial;
                mat.SetColor("_EmissionColor", BoltColor * BoltEmission);
                _boltTemplate.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                _boltTemplate.name = "Bolt";
                _boltTemplate.transform.SetParent(_set, true);
                _boltTemplate.SetActive(false);
            }

            var bolt = Instantiate(_boltTemplate, _set);
            bolt.transform.SetPositionAndRotation(muzzle + dir * BoltLength,
                Quaternion.FromToRotation(Vector3.up, velocity));
            bolt.SetActive(true);
            _bolts.Add(new Bolt { tr = bolt.transform, velocity = velocity, life = life, mark = mark });
        }

        void Flash(Vector3 at, Vector3 forward, Vector3 up, float size)
        {
            MuzzleFlash.Spawn(at, forward, size);
            Vector3 square = Vector3.ProjectOnPlane(up, forward);
            if (square.sqrMagnitude < 1e-4f) square = Vector3.ProjectOnPlane(Vector3.forward, forward);

            foreach (var flash in FindObjectsByType<MuzzleFlash>(FindObjectsSortMode.None))
            {
                if (!_aimedFlashes.Add(flash)) continue;
                flash.transform.rotation = Orientation(forward.normalized, square.normalized);
            }
        }

        void MoveBolts(float dt)
        {
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                Bolt bolt = _bolts[i];
                bolt.life -= dt;
                if (bolt.life <= 0f)
                {
                    if (bolt.mark != null) Strike(bolt.tr.position, bolt.mark);
                    Destroy(bolt.tr.gameObject);
                    _bolts.RemoveAt(i);
                    continue;
                }
                bolt.tr.position += bolt.velocity * dt;
            }
        }

        void MoveShards(float dt)
        {
            foreach (Shard shard in _shards)
            {
                shard.velocity += Vector3.down * (ShardGravity * dt);
                shard.tr.position += shard.velocity * dt;
                shard.tr.Rotate(shard.spinAxis, shard.spin * dt, Space.World);
            }
        }

        IEnumerator Capture()
        {
            int factor = Mathf.Clamp(_spec.supersample, 1, 4);
            int width = _spec.width * factor, height = _spec.height * factor;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            _cam.targetTexture = target;
            _cam.aspect = _spec.width / (float)_spec.height;

            yield return new WaitForEndOfFrame();

            RenderTexture.active = target;
            var big = new Texture2D(width, height, TextureFormat.RGB24, false);
            big.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            big.Apply();
            RenderTexture.active = null;
            _cam.targetTexture = null;

            Texture2D image = factor == 1 ? big : Shrink(big, factor);
            string folder = Path.GetDirectoryName(_output);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllBytes(_output, image.EncodeToPNG());
            Debug.Log($"ShotDirector: wrote {_output}");
        }

        static Texture2D Shrink(Texture2D big, int factor)
        {
            int width = big.width / factor, height = big.height / factor;
            Color32[] source = big.GetPixels32();
            var pixels = new Color32[width * height];
            int samples = factor * factor;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < factor; dy++)
                    {
                        int row = (y * factor + dy) * big.width + x * factor;
                        for (int dx = 0; dx < factor; dx++)
                        {
                            Color32 c = source[row + dx];
                            r += c.r;
                            g += c.g;
                            b += c.b;
                        }
                    }
                    pixels[y * width + x] = new Color32(
                        (byte)(r / samples), (byte)(g / samples), (byte)(b / samples), 255);
                }
            }

            var small = new Texture2D(width, height, TextureFormat.RGB24, false);
            small.SetPixels32(pixels);
            small.Apply();
            return small;
        }

        Vector3 Air(float[] screen, float distance) =>
            _cam.ViewportPointToRay(new Vector3(screen[0], screen[1], 0f)).GetPoint(distance);

        Vector3 Sea(float screenX, float distance)
        {
            Vector3 dir = _cam.ViewportPointToRay(new Vector3(screenX, 0.5f, 0f)).direction;
            dir.y = 0f;
            Vector3 point = _cam.transform.position + dir.normalized * distance;
            point.y = SeaSurface.Level;
            return point;
        }

        Vector3 Ground(float screenX, float distance)
        {
            Vector3 dir = _cam.ViewportPointToRay(new Vector3(screenX, 0.5f, 0f)).direction;
            dir.y = 0f;
            Vector3 point = _cam.transform.position + dir.normalized * distance;
            point.y = GroundY(point.x, point.z);
            return point;
        }

        static float GroundY(float x, float z)
        {
            if (Battlefield.Current != null && Battlefield.Current.SampleGround(x, z, out float y)) return y;

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (x < origin.x || x > origin.x + size.x || z < origin.z || z > origin.z + size.z) continue;
                return terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y;
            }
            return ProceduralTerrain.BaseLevel;
        }
    }
}
