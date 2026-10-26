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
        const float GunFlashScale = 0.3f;
        const float CaptureFlashScale = 0.45f;

        const float RifleIntervalMin = 0.3f, RifleIntervalMax = 0.8f;
        const float RifleScatter = 35f;
        const float ShoulderHeight = 8.6f;
        const float RifleLength = 8f;
        const float RifleWidth = 0.55f;
        const float RifleFlashSize = 10f;
        const float RifleFlashShare = 0.4f;

        const float ShardGravity = 110f;
        const float ShardScatter = 45f;
        const float ShardSizeMin = 1.2f, ShardSizeMax = 3.4f;

        const float DefaultZeppelinLength = 600f;
        const float DefaultZeppelinDistance = 520f;

        const float FireballLift = 0.25f;

        const float FocusPriority = 1000f;
        const float ProbeY = -5000f;
        const string PropFolder = "objects/";

        static readonly Quaternion StandUp = Quaternion.Euler(-90f, 0f, 0f);
        static readonly Color BoltColor = new Color(0.96f, 0.76f, 0.38f);
        static readonly Color RifleColor = new Color(0.17f, 0.12f, 0.08f);
        static readonly Color ShardColor = new Color(0.22f, 0.17f, 0.12f);

        static readonly MethodInfo BuildFigure = typeof(BattlefieldPeople).GetMethod("BuildFigure",
            BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo BlastTimer = typeof(Battlefield).GetField("_blastTimer",
            BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FieldPeople = typeof(Battlefield).GetField("_people",
            BindingFlags.NonPublic | BindingFlags.Instance);

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
            public float nextShot;
            public int gunSide = 1;
            public Vector3 Forward => tr.right;
            public Vector3 Up => tr.up;
            public Vector3 Velocity => tr.right * speed;
        }

        class Bolt
        {
            public Transform tr;
            public Vector3 velocity;
            public float life;
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
        }

        class Airship
        {
            public Transform tr;
            public Vector3 velocity;
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
        SquadTracers _rifleTracers;
        GameObject _boltTemplate;
        float _clock;

        readonly List<Flyer> _flyers = new List<Flyer>();
        readonly List<Bolt> _bolts = new List<Bolt>();
        readonly List<Shard> _shards = new List<Shard>();
        readonly List<Rifleman> _riflemen = new List<Rifleman>();
        readonly List<Cue> _cues = new List<Cue>();
        readonly List<Airship> _airships = new List<Airship>();
        readonly HashSet<MuzzleFlash> _aimedFlashes = new HashSet<MuzzleFlash>();

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
                daytime, BattleShape.Story);
            CampaignRun.TakeLaunch();

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

            Stage();
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

        void Stage()
        {
            _set = new GameObject("Shot Set").transform;
            Silence();
            Frame();
            Focus();

            foreach (ShotPlane plane in _spec.planes) _flyers.Add(BuildFlyer(plane));
            foreach (ShotProp prop in _spec.props) BuildProp(prop);
            foreach (ShotZeppelin zeppelin in _spec.zeppelins) BuildZeppelin(zeppelin);
            foreach (ShotSquad squad in _spec.squads) BuildSquad(squad);
            BuildSmoke();
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

            Battlefield field = Battlefield.Current;
            if (field == null) return;

            if (!_spec.ambientBlasts && BlastTimer != null) BlastTimer.SetValue(field, float.MaxValue);
            if (_spec.ambientPeople || field.People == null) return;

            Destroy(field.People.gameObject);
            if (FieldPeople != null) FieldPeople.SetValue(field, null);
        }

        void Frame()
        {
            _cam = Camera.main;
            ShotCamera shot = _spec.camera;
            float y = GroundY(shot.x, shot.z) + shot.height;
            _cam.transform.SetPositionAndRotation(new Vector3(shot.x, y, shot.z),
                Quaternion.Euler(-shot.pitch, shot.yaw, shot.roll));
            _cam.fieldOfView = shot.fov > 0f ? shot.fov : 40f;
            _cam.aspect = _spec.width / (float)_spec.height;
            _cam.farClipPlane = Mathf.Max(_cam.farClipPlane, 2600f);

            if (_spec.fogStart >= 0f) RenderSettings.fogStartDistance = _spec.fogStart;
            if (_spec.fogEnd > 0f) RenderSettings.fogEndDistance = _spec.fogEnd;
        }

        void Focus()
        {
            if (_spec.focus == null || _spec.focus.distance <= 0f) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Shot Focus (runtime)";
            var field = profile.Add<DepthOfField>();
            field.mode.Override(DepthOfFieldMode.Bokeh);
            field.focusDistance.Override(_spec.focus.distance);
            field.focalLength.Override(_spec.focus.focalLength > 0f ? _spec.focus.focalLength : 85f);
            field.aperture.Override(_spec.focus.aperture > 0f ? _spec.focus.aperture : 4f);

            var volume = new GameObject("Shot Focus").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = FocusPriority;
            volume.profile = profile;
            _cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        Flyer BuildFlyer(ShotPlane spec)
        {
            PlaneModelConfig config = PlaneModels.ById(spec.model) ?? PlaneModels.Sopwith;
            var go = new GameObject($"Shot {config.resourceName} {_flyers.Count}");
            Transform model = PlaneFactory.BuildPlaneModel(go.transform, config, false,
                PlaneSkins.Enemy(config, spec.skin));
            model.localScale *= PlaneModelConfig.LevelScale;
            foreach (Collider col in go.GetComponentsInChildren<Collider>()) Destroy(col);

            var flyer = new Flyer
            {
                spec = spec,
                tr = go.transform,
                size = config.OnScreenSize * PlaneModelConfig.LevelScale,
                speed = spec.speed > 0f ? spec.speed : DefaultSpeed,
            };
            if (PlaneFactory.GunMuzzleLocal(go, model, config, out Vector3 gun)) flyer.gun = gun;
            else if (config.gunLift > 0f)
                flyer.gun = PlaneFactory.GunLocal(go, model, config, PlaneModelConfig.LevelScale, false);
            else flyer.gun = PlaneFactory.NoseLocal(go, model, config);
            flyer.gunSpacing = config == PlaneModels.Eindecker ? 0f
                : config == PlaneModels.Sopwith ? CamelGunSpacing : GunSpacing;

            Vector3? engine = PlaneFactory.EngineLocal(go, model, config);
            if (spec.burning || spec.smoking)
            {
                var smoke = go.AddComponent<SmokeTrail>();
                if (engine.HasValue) smoke.Mount = engine.Value;
                if (spec.burning) smoke.Ignite(flyer.size * TrailScale);
                else smoke.Arm(flyer.size * TrailScale);
            }
            if (spec.burning) PlaneFire.Ignite(go, flyer.size, engine);

            float[] screen = spec.screen != null && spec.screen.Length >= 2
                ? spec.screen : new[] { 0.5f, 0.5f };
            Vector3 end = Air(screen, spec.distance > 0f ? spec.distance : 200f);
            Quaternion frame = Quaternion.Euler(0f, _spec.camera.yaw, 0f);
            Vector3 forward = frame * Quaternion.Euler(-spec.climb, spec.course, 0f) * Vector3.forward;
            Vector3 level = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (level.sqrMagnitude < 1e-4f) level = frame * Vector3.back;
            Vector3 up = Quaternion.AngleAxis(-spec.bank, forward) * level.normalized;

            PlanPath(flyer, end, forward, up);
            Place(flyer, _spec.warmup);

            if (spec.hitAge > 0f)
                Schedule(_spec.warmup - spec.hitAge, () => Hit(flyer));
            return flyer;
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

            for (int i = 0; i < flyer.spec.debris; i++)
            {
                var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, flyer.tr.position,
                    new Vector3(Random.Range(ShardSizeMin, ShardSizeMax),
                        Random.Range(ShardSizeMin, ShardSizeMax) * 0.4f,
                        Random.Range(ShardSizeMin, ShardSizeMax)),
                    ShardColor, emissive: false, keepCollider: false);
                go.name = "Shard";
                go.transform.SetParent(_set, true);
                go.transform.rotation = Random.rotation;
                _shards.Add(new Shard
                {
                    tr = go.transform,
                    velocity = flyer.Velocity * Random.Range(0.35f, 0.7f)
                               + Random.insideUnitSphere * ShardScatter,
                    spinAxis = Random.onUnitSphere,
                    spin = Random.Range(180f, 720f),
                });
            }
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

        void BuildProp(ShotProp spec)
        {
            var prefab = Resources.Load<GameObject>(PropFolder + spec.model);
            if (prefab == null)
            {
                Debug.LogError($"ShotDirector: {PropFolder}{spec.model} not found in Resources.");
                return;
            }

            string kind = string.IsNullOrEmpty(spec.kind) ? "tree" : spec.kind;
            bool rubble = kind == "rubble";
            float oversize = kind == "tank" ? 1.15f : rubble ? 1f : 1.5f;
            float scale = BattlefieldProps.MetreScale * oversize * (spec.scale > 0f ? spec.scale : 1f);
            Quaternion standUp = rubble ? Quaternion.identity : StandUp;
            Bounds bounds = Measure(prefab, standUp);

            Vector3 at = Ground(spec.screenX, spec.distance);
            var root = new GameObject(spec.model);
            root.transform.SetParent(_set, false);
            root.transform.SetPositionAndRotation(at - Vector3.up * spec.sink,
                Quaternion.Euler(0f, spec.yaw, 0f));
            root.transform.localScale = Vector3.one * scale;

            var view = Instantiate(prefab, root.transform);
            view.transform.localRotation = standUp;
            view.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            NightReceivers.Adopt(view);
            foreach (var r in view.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;
            if (kind == "tank") TankSkin.Apply(view.transform);

            if (!spec.burning) return;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * scale;
            WreckFire.Begin(_set, at, radius, Random.Range(0, int.MaxValue));
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
            Vector3 goal = spec.target >= 0 && spec.target < _flyers.Count
                ? _flyers[spec.target].positions[0]
                : centre + Vector3.up * 200f;

            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * spread;
                float x = centre.x + jitter.x, z = centre.z + jitter.y;
                var figure = (Transform)BuildFigure.Invoke(null,
                    new object[] { _set, Mathf.Clamp(spec.faction, 0, 1), Random.Range(0, 3) });
                figure.position = new Vector3(x, GroundY(x, z), z);

                Vector3 shoulder = figure.position + Vector3.up * ShoulderHeight;
                Vector3 aim = (goal + Random.insideUnitSphere * RifleScatter - shoulder).normalized;
                Vector3 flat = new Vector3(aim.x, 0f, aim.z);
                figure.rotation = Quaternion.LookRotation(flat.sqrMagnitude > 1e-4f ? flat : Vector3.forward);

                Vector3 side = figure.right * 1.8f;
                var rifle = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    shoulder + side + aim * (RifleLength * 0.35f),
                    new Vector3(RifleWidth, RifleWidth, RifleLength), RifleColor,
                    emissive: false, keepCollider: false);
                rifle.name = "Rifle";
                rifle.transform.SetParent(figure, true);
                rifle.transform.rotation = Quaternion.LookRotation(aim);

                if (!spec.fire) continue;
                _riflemen.Add(new Rifleman
                {
                    muzzle = shoulder + side + aim * (RifleLength * 0.85f),
                    aim = aim,
                    target = spec.target,
                    nextShot = Random.Range(0f, RifleIntervalMax),
                });
            }
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

            foreach (Flyer flyer in _flyers)
            {
                Place(flyer, remaining);
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
                Vector3 goal = TargetPoint(man.target, man.muzzle + man.aim * 300f);
                _rifleTracers.Fire(man.muzzle, goal + Random.insideUnitSphere * RifleScatter);
            }

            MoveBolts(dt);
            MoveShards(dt);
            foreach (Airship ship in _airships) ship.tr.position += ship.velocity * dt;
            if (_spec.clearNear > 0f) ClearNear();

            if (!last) return;
            foreach (Flyer flyer in _flyers)
            {
                if (!flyer.spec.fire) continue;
                float scale = CaptureFlashScale * (flyer.spec.flashScale > 0f ? flyer.spec.flashScale : 1f);
                Flash(GunPoint(flyer), flyer.Forward, flyer.Up, flyer.size * scale);
            }
            foreach (Rifleman man in _riflemen)
                if (Random.value < RifleFlashShare)
                    Flash(man.muzzle, man.aim, Vector3.up, RifleFlashSize);
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

        void Shoot(Flyer flyer)
        {
            flyer.gunSide = -flyer.gunSide;
            Vector3 muzzle = GunPoint(flyer);

            Vector3 aimAt = muzzle + flyer.Forward * 400f;
            float life = BoltLife;
            if (flyer.spec.target >= 0 && flyer.spec.target < _flyers.Count
                && _flyers[flyer.spec.target] != flyer)
            {
                Flyer mark = _flyers[flyer.spec.target];
                float time = Vector3.Distance(muzzle, mark.tr.position) / BoltSpeed;
                aimAt = mark.tr.position + mark.Velocity * (time - flyer.spec.aimLag);
                life = Mathf.Min(BoltLife, Vector3.Distance(muzzle, aimAt) / BoltSpeed + BoltOvershoot);
            }

            Vector3 dir = ((aimAt - muzzle).normalized + Random.insideUnitSphere * BoltSpread).normalized;
            SpawnBolt(muzzle, dir, dir * BoltSpeed + flyer.Velocity, life);
            Flash(muzzle, flyer.Forward, flyer.Up, flyer.size * GunFlashScale);
        }

        void SpawnBolt(Vector3 muzzle, Vector3 dir, Vector3 velocity, float life)
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
            _bolts.Add(new Bolt { tr = bolt.transform, velocity = velocity, life = life });
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

        Vector3 Ground(float screenX, float distance)
        {
            Vector3 dir = _cam.ViewportPointToRay(new Vector3(screenX, 0.5f, 0f)).direction;
            dir.y = 0f;
            Vector3 point = _cam.transform.position + dir.normalized * distance;
            point.y = GroundY(point.x, point.z);
            return point;
        }

        static float GroundY(float x, float z) =>
            Battlefield.Current != null && Battlefield.Current.SampleGround(x, z, out float y)
                ? y : ProceduralTerrain.BaseLevel;
    }
}
