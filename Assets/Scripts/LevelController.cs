using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// Shared controller — and composer — for the playable "Air Fights" levels. The scene sets
    /// only <see cref="levelNumber"/> in the inspector; everything else about the level comes
    /// from its <see cref="LevelDefinition"/> in the <see cref="Levels"/> registry, composed
    /// from parts: the terrain to build, the daytime sky to fly under, the weather (calm-only
    /// for now), and the enemy roster to spawn.
    ///
    /// Builds the definition's width x 700 m flight arena at runtime: ground at the bottom (the
    /// flat placeholder slab or the procedural Verdun-style land, per the terrain part — see
    /// <see cref="ProceduralTerrain"/>), a hard ceiling at the top, and soft boundaries on the
    /// sides that steer the cube back toward the middle. The player cube (coloured by the Garage
    /// selection) flies with the sibling repo's physics via <see cref="CubeController"/>. A
    /// perspective camera follows the cube, giving a 2.5D platformer feel. Touching the ground
    /// fails the level; the level is won by shooting down every enemy fighter.
    /// The roster's enemy fighters (see <see cref="EnemyController"/>) spawn at
    /// random spots outside the camera and hunt the player; their fire wears down the health
    /// shown on the HUD, and at zero the plane falls out of the sky.
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        [Tooltip("Which level this scene represents (1 for Level1, 2 for Level2, ...). " +
                 "Everything else — terrain, daytime, weather, enemies — comes from this " +
                 "level's definition in the Levels registry.")]
        [SerializeField] int levelNumber = 1;

        // The parts this level is composed from, resolved from Levels in Start.
        LevelDefinition _level;

        // ---- World geometry (metres). X is centred on 0; Y runs from the ground up. ----
        const float WorldHeight = 700f;
        const float GroundY = 0f;              // top surface of the flat ground
        const float WorldTop = WorldHeight;    //  700, the hard ceiling
        float WorldWidth => _level.terrain.width;
        float MinX => -WorldWidth / 2f;
        float MaxX => WorldWidth / 2f;

        // Whether this level flies over the procedural Verdun land (as opposed to the flat
        // slab) — the land whose height field, camera reveal and atmosphere need special care.
        bool VerdunLand => _level.terrain.kind == TerrainKind.Verdun;

        // Width of the soft-boundary band inside each side edge. Once the cube noses into this
        // band heading toward the edge, it is steered back toward the centre (see CubeController).
        const float EdgeMargin = 220f;

        const float CubeScale = 30f;
        const float CubeHalf = CubeScale / 2f;

        // Plane-to-plane scrape hitbox radius: far smaller than the model's 60 m span, so only a
        // real fuselage overlap counts — a wingtip clipping a tail slips past untouched. Two planes
        // scrape when their centres come within twice this (~30 m).
        const float PlaneHitboxRadius = 15f;

        // Dedicated physics layer for every plane's collider. Its self-collisions are switched off
        // (DisablePlanePlaneCollisions) so two planes never physically hit — they pass cleanly
        // through one another and the scrape is detected by distance in CheckPlaneScrapes, instead
        // of the fragile per-pair Physics.IgnoreCollision that let a contact through and ram-exploded
        // both planes. Any unused layer works: nothing in the project filters by layer, and
        // plane-vs-ground and plane-vs-bullet stay on (those colliders live on the Default layer,
        // which this layer still collides with).
        const int PlaneLayer = 8;

        // Camera kick on a player scrape: a short, decaying jitter of the follow position.
        const float CamShakeMagnitude = 7f;   // metres of jitter at full strength
        const float CamShakeDuration = 0.3f;  // seconds to decay back to steady
        const float ModelPitchDeg = -10f;       // cosmetic nose-down tilt of the model about the view axis
                                               // (visual only; the flight heading is driven by the parent)
        const float CameraDistance = 420f;     // camera sits this far back on -Z of the play plane
        const float PlayPlaneZ = 100f;         // the flight plane sits this far into the land (+Z), so a
                                               // falling cube lands on the land, not on its front cut edge
        const float CamZ = PlayPlaneZ - CameraDistance; // world Z the camera rides at
        const float BackdropZ = PlayPlaneZ + 150f; // shadow-receiving wall sits this far behind the play plane

        CubeController _cube;
        PlaneShooter _shooter;
        Transform _cubeTr;
        Camera _cam;

        EnemyConfig _enemyConfig;
        readonly List<EnemyController> _enemies = new List<EnemyController>();

        // Player health readout on the HUD (bar + number, top-left).
        const float HudBarWidth = 400f;
        const float HudBarHeight = 38f;
        const float HudBarPadding = 4f;
        Image _healthFill;
        Text _healthText;

        float _halfViewHeight;   // half the world height visible on screen (for camera clamping)
        bool _gameOver;
        float _camShake;         // 1 -> 0 decaying camera-shake strength, kicked by a player scrape
        Vector3 _camBasePos;     // un-shaken follow position, so the shake offset never feeds back into the smoothing
        readonly List<EnemyController> _scrapeScratch = new List<EnemyController>(); // per-step live-enemy snapshot

        void Start()
        {
            _level = Levels.ForNumber(levelNumber);

            var config = Resources.Load<PlayerConfig>("PlayerConfig");
            if (config == null) config = ScriptableObject.CreateInstance<PlayerConfig>(); // safety fallback

            ConfigureShadows();
            BuildWorld();
            SpawnPlayer(config);
            SetupCamera();   // before SpawnEnemies: spawn points must be outside the camera view
            SpawnEnemies();
            DisablePlanePlaneCollisions(); // planes pass through each other; scrapes are detected in FixedUpdate
            BuildHud();
        }

        /// <summary>
        /// The active URP asset ships with a 50 m shadow distance, but the camera sits
        /// <see cref="CameraDistance"/> (~420 m) back from the play plane, so the cube is far
        /// beyond that cutoff and never casts a visible shadow. Push the shadow distance out to
        /// cover the whole camera-to-play-plane depth (plus margin) so the main directional light
        /// actually shadows the cube. This is set at runtime so we don't disturb the shared RP
        /// asset used by every other scene.
        /// </summary>
        void ConfigureShadows()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                // Distance from the camera to the play plane, with headroom for the cube's climb.
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, CameraDistance + 200f);
            }
        }

        // ---------------------------------------------------------------- world

        void BuildWorld()
        {
            if (VerdunLand)
            {
                // Verdun-style land; its TerrainCollider drives the fail on contact. Weather
                // rides along so the land's atmosphere (fog, grass wind) can follow it later.
                ProceduralTerrain.Build(_level.terrain.seed, WorldWidth,
                    CameraDistance, PlayPlaneZ, _level.daytime, _level.weather);
            }
            else
            {
                // Solid flat ground at the bottom (its collider drives the fail on contact).
                UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(0f, GroundY - 10f, 0f),
                    new Vector3(WorldWidth + 200f, 20f, 400f),
                    new Color(0.20f, 0.22f, 0.16f));

                // Backdrop wall a little behind the play plane. The main light shines into +Z,
                // so it projects the cube's silhouette onto this wall, giving the slab levels a
                // clearly visible drop-shadow. Purely visual: it receives shadows but casts none
                // and has no collider (the camera looks straight down +Z, so it never occludes
                // the cube).
                var backdrop = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(0f, WorldHeight / 2f, BackdropZ),
                    new Vector3(WorldWidth + 400f, WorldHeight + 200f, 10f),
                    new Color(0.16f, 0.17f, 0.20f), keepCollider: false);
                var backdropRenderer = backdrop.GetComponent<Renderer>();
                if (backdropRenderer != null)
                    backdropRenderer.shadowCastingMode = ShadowCastingMode.Off; // receive only
            }

            // Ceiling bar (visual only) so the player can see the hard cap.
            UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                new Vector3(0f, WorldTop, PlayPlaneZ),
                new Vector3(WorldWidth + 200f, 8f, 60f),
                new Color(0.55f, 0.6f, 0.7f), emissive: true, keepCollider: false);
        }

        void SpawnPlayer(PlayerConfig config)
        {
            // The physics body is a bare GameObject that CubeController yaws to the heading each
            // frame (it writes transform.rotation directly). The visible Sopwith Camel hangs off it
            // as a child so the plane's own orientation (the +90° X stand-up fix, see below)
            // composes with the heading instead of being overwritten.
            var go = new GameObject("PlayerPlane");
            // Spawn on the left side of the map, just inside the soft-boundary band so the plane
            // starts at the left edge without being immediately steered back toward the centre.
            go.transform.position = new Vector3(MinX + EdgeMargin, 150f, PlayPlaneZ);

            // The player flies the Sopwith Camel (the enemies keep the Fokker Dr.1, see SpawnEnemies).
            var planeModel = PlaneModels.Sopwith;
            var plane = BuildPlaneModel(go.transform, planeModel);

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnShotDown += OnShotDown;

            // Start heading straight to the right (velocity +X => angle 0), ceiling clamped for the plane's size.
            _cube.Initialize(config, 0f, MinX, MaxX, WorldTop - CubeHalf, EdgeMargin);

            SetupGuns(config, go, plane, planeModel);
        }

        /// <summary>
        /// Mounts the machine guns: a muzzle transform just ahead of the propeller disc, at the
        /// height of the Dr.1's twin Spandaus (atop the cowling, slightly above the prop hub —
        /// the model has no gun nodes, so the offset is derived from the prop's bounds), plus a
        /// <see cref="PlaneShooter"/> that fires from it while F is held.
        /// </summary>
        void SetupGuns(PlayerConfig config, GameObject body, Transform model, PlaneModelConfig plane)
        {
            const float MuzzleClearance = 2f;    // ahead of the prop disc, so rounds spawn clear of it
            const float GunHeightAboveHub = 2.5f; // Spandaus sit on the cowling above the hub

            // At spawn the body's heading is 0 (identity rotation), so world offsets from the
            // body can be stored directly as the muzzle's local position.
            Transform prop = FindDeep(model, plane.propBladesNode) ?? FindDeep(model, plane.propPivotNode) ?? model;
            var renderers = prop.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0
                ? renderers[0].bounds
                : new Bounds(body.transform.position, Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(body.transform, false);
            muzzle.localPosition = new Vector3(
                bounds.max.x + MuzzleClearance - body.transform.position.x,
                bounds.center.y + GunHeightAboveHub - body.transform.position.y,
                0f); // stay exactly on the play plane

            _shooter = body.AddComponent<PlaneShooter>();
            _shooter.Initialize(config, muzzle, body.GetComponentInChildren<Collider>());
        }

        // ---------------------------------------------------------------- enemies

        /// <summary>
        /// Spawns the definition's enemy roster — each <see cref="EnemyGroup"/>'s count of its
        /// aircraft — at random positions outside the camera view, wired to the same world
        /// bounds the player flies in.
        /// </summary>
        void SpawnEnemies()
        {
            _enemyConfig = Resources.Load<EnemyConfig>("EnemyConfig");
            if (_enemyConfig == null) _enemyConfig = ScriptableObject.CreateInstance<EnemyConfig>();

            var playerBody = _cube.GetComponent<Rigidbody>();
            // The AI measures altitude from the terrain's highest crest, so its ground
            // margins hold over every hill; the flat slab's top is simply GroundY.
            float aiGroundY = VerdunLand ? ProceduralTerrain.MaxHeight : GroundY;

            foreach (var group in _level.enemies)
                for (int i = 0; i < group.count; i++)
                {
                    // Same bare-body-plus-model rig as the player: a physics body the
                    // EnemyController yaws to the heading, carrying the group's aircraft as a
                    // child. The model is mirrored because the enemies attack from the right
                    // and mostly fly left (see BuildPlaneModel).
                    var go = new GameObject("Enemy");
                    go.transform.position = RandomEnemySpawn(aiGroundY);
                    BuildPlaneModel(go.transform, group.plane, mirrored: true);

                    var enemy = go.AddComponent<EnemyController>();
                    enemy.Initialize(_enemyConfig, playerBody,
                        MinX, MaxX, aiGroundY, WorldTop - group.plane.onScreenSize / 2f, EdgeMargin);
                    enemy.OnDestroyed += OnEnemyDestroyed;
                    _enemies.Add(enemy);
                }
        }

        /// <summary>
        /// A random spot inside the world's soft boundaries but outside what the camera shows,
        /// at an altitude the AI already considers safe (so it never spawns into a pull-up).
        /// </summary>
        Vector3 RandomEnemySpawn(float aiGroundY)
        {
            float halfViewWidth = _halfViewHeight * (_cam != null ? _cam.aspect : 16f / 9f);
            float camX = _cam != null ? _cam.transform.position.x : 0f;

            float minY = aiGroundY + _enemyConfig.safeAltitudeMargin;
            float maxY = Mathf.Max(minY, WorldTop - 120f);

            // Sample X until it lands off screen; every current level has far more world than
            // screen, so this exits almost immediately (the last sample wins regardless).
            float x = 0f;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                x = Random.Range(MinX + EdgeMargin, MaxX - EdgeMargin);
                if (Mathf.Abs(x - camX) > halfViewWidth + 60f) break;
            }
            return new Vector3(x, Random.Range(minY, maxY), PlayPlaneZ);
        }

        /// <summary>The fight is over (either way): the survivors cease fire and cruise.</summary>
        void StandDownEnemies()
        {
            foreach (var enemy in _enemies)
                if (enemy != null) enemy.StandDown();
        }

        /// <summary>
        /// An enemy fighter was destroyed. Clearing the last one wins the level — but only while
        /// the player is still flying, so a plane shot down by that enemy's last burst still
        /// crashes into a failure rather than a win.
        /// </summary>
        void OnEnemyDestroyed(EnemyController enemy)
        {
            _enemies.Remove(enemy);

            if (!_gameOver && _enemies.Count == 0 && _cube != null && _cube.CurrentHealth > 0f)
                WinLevel();
        }

        /// <summary>
        /// Instantiates the aircraft model described by <paramref name="plane"/> under
        /// <paramref name="parent"/> (the physics body): tips it upright per the config's
        /// stand-up rotation, scales it to the config's on-screen size, gives it a tight convex
        /// mesh collider so ground crashes and the goal trigger still fire, turns on shadow casting,
        /// and starts the propeller spinning. Pass <paramref name="mirrored"/> for the enemy plane,
        /// which flies left instead of right.
        ///
        /// Everything model-specific (which FBX, its rest orientation, its size, its propeller node
        /// names) comes from <paramref name="plane"/>, so a new aircraft is a new
        /// <see cref="PlaneModelConfig"/> entry rather than an edit here.
        /// </summary>
        Transform BuildPlaneModel(Transform parent, PlaneModelConfig plane, bool mirrored = false)
        {
            var prefab = Resources.Load<GameObject>(plane.resourceName);
            if (prefab == null)
            {
                // Should never happen once the FBX is under Resources; fall back to a cube so the
                // level is still playable rather than crashing on a null model.
                Debug.LogError($"LevelController: {plane.resourceName} model not found in Resources.");
                var fallback = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    Vector3.zero, Vector3.one * CubeScale, Color.white);
                fallback.transform.SetParent(parent, false);
                return fallback.transform;
            }

            var model = Instantiate(prefab);
            model.name = mirrored ? $"{plane.resourceName} (enemy)" : plane.resourceName;
            model.transform.SetParent(parent, false);

            // The model is exported lying flat, so stand it up before anything else touches its
            // orientation. plane.standUpEuler does that per-model: for the Fokker it's (90, -90, 0)
            // — +90° X tips it upright, -90° Y swings the nose to lead along the heading (a plain
            // +90° puts a wing forward, the opposite 180° leaves the tail forward). The extra 180°
            // roll about the nose (X, the heading/flight axis) flips the plane wheels-down instead
            // of wheels-up, when the config asks for it.
            //
            // The enemy is the mirror image: it attacks from the right and flies LEFT, and a heading
            // of ~180° is a 180° spin about the view axis — which would leave a right-upright plane
            // belly-up. So for the enemy we DROP that wheels-down roll (leaving it belly-up at
            // heading 0), and the parent's 180° heading spin then lands it belly-down and upright
            // while flying left. The cosmetic nose pitch is flipped to match, so the nose still dips
            // toward the flight direction. (These planes are left-right symmetric, so this reads as a
            // true mirror without the inside-out normals a negative scale would cause.)
            //
            // The outer Z rotation (the camera's view axis, the plane's pitch axis on screen) drops
            // the nose down a touch. It's applied to the model only, so it's purely visual — the
            // flight direction comes from the parent's heading and is unaffected.
            Quaternion wheelsDown = (plane.rollWheelsDown && !mirrored)
                ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity;
            float pitch = mirrored ? -ModelPitchDeg : ModelPitchDeg;
            model.transform.localRotation = Quaternion.Euler(0f, 0f, pitch)
                                          * wheelsDown
                                          * Quaternion.Euler(plane.standUpEuler);

            // Scale the model down to the config's on-screen size, measured from its combined
            // renderer bounds so we don't depend on the FBX's own unit scale.
            NormalizeSize(model.transform, plane.onScreenSize);

            // Cast a shadow onto the terrain/ground below, matching the old cube (ConfigureShadows
            // extends the shadow distance so it actually shows at this camera depth).
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;

            AddPlaneCollider(model.transform);
            StartPropeller(model.transform, plane);
            model.AddComponent<ShakeEffect>(); // scrape feedback: shivers the model, never the body
            return model.transform;
        }

        /// <summary>
        /// Uniformly scales <paramref name="model"/> so the longest side of its combined
        /// world-space renderer bounds equals <paramref name="targetSize"/>.
        /// </summary>
        void NormalizeSize(Transform model, float targetSize)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0.0001f)
                model.localScale *= targetSize / longest;
        }

        /// <summary>
        /// Gives the plane a single tight convex mesh collider built from its largest mesh (the
        /// fuselage) so <see cref="CubeController"/> still detects ground crashes and the goal
        /// trigger. Convex is required for a moving Rigidbody and for trigger overlaps.
        /// </summary>
        void AddPlaneCollider(Transform model)
        {
            MeshFilter biggest = null;
            float biggestSize = 0f;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                float size = mf.sharedMesh.bounds.size.sqrMagnitude;
                if (size > biggestSize) { biggestSize = size; biggest = mf; }
            }
            if (biggest == null) return;

            var col = biggest.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = biggest.sharedMesh;
            col.convex = true;

            // Put the plane's one collider on the plane layer, whose self-collisions are off, so it
            // passes through other planes (scrape is detected in CheckPlaneScrapes) while still
            // hitting the ground and bullets. See PlaneLayer / DisablePlanePlaneCollisions.
            biggest.gameObject.layer = PlaneLayer;
        }

        /// <summary>
        /// Finds the propeller pivot node (parent of the blades) named by the config and attaches
        /// <see cref="PropellerSpin"/> so the blades turn. Falls back to the blades node itself if
        /// the pivot is missing, so the animation runs either way.
        /// </summary>
        void StartPropeller(Transform model, PlaneModelConfig plane)
        {
            Transform spinner = FindDeep(model, plane.propPivotNode) ?? FindDeep(model, plane.propBladesNode);
            if (spinner != null) spinner.gameObject.AddComponent<PropellerSpin>();
            else Debug.LogWarning("LevelController: propeller node not found on the plane model.");
        }

        /// <summary>Depth-first search for a descendant transform by name.</summary>
        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null) return;

            _cam.orthographic = false; // perspective, per the chosen 2.5D look
            _cam.transform.rotation = Quaternion.identity; // look straight down +Z at the play plane

            if (VerdunLand)
            {
                // The level's chosen atmosphere: gradient skybox, key light, ambient and post
                // FX (the fog was already built to match in ProceduralTerrain.Build). Weather
                // rides along as the future modulation seam (Calm changes nothing). Draw
                // distance must still reach the terrain's fogged far edge.
                if (_level.daytime == Daytime.Midday) MiddaySky.Apply(_cam, _level.weather);
                else MorningSky.Apply(_cam, _level.weather);
                _cam.farClipPlane = 2200f;
            }

            // How much world height fits on screen at the play plane (z = 0), for vertical clamping.
            _halfViewHeight = CameraDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            PositionCamera(instant: true);
        }

        // ---------------------------------------------------------------- loop

        void FixedUpdate()
        {
            if (_gameOver) return;
            CheckPlaneScrapes();
        }

        void LateUpdate()
        {
            if (_cam != null && _cubeTr != null) PositionCamera(instant: false);
            if (_camShake > 0f) _camShake = Mathf.Max(0f, _camShake - Time.deltaTime / CamShakeDuration);
            UpdateHealthHud();
        }

        // ---------------------------------------------------------------- plane-to-plane scrapes

        /// <summary>
        /// Switches off physical collisions between planes by disabling self-collision on their
        /// shared <see cref="PlaneLayer"/>, so they pass cleanly through one another instead of
        /// jamming and juddering — two script-driven rigidbodies can never settle a contact,
        /// because their velocity is overwritten every step. This filters in the physics broadphase,
        /// so (unlike the per-pair Physics.IgnoreCollision it replaces) it can't be defeated by the
        /// timing of runtime-created colliders — which is what was letting a contact through and
        /// ram-exploding both planes. Ground and bullet collisions (Default layer) are untouched;
        /// the scrape itself is detected in <see cref="CheckPlaneScrapes"/>.
        /// </summary>
        void DisablePlanePlaneCollisions()
        {
            Physics.IgnoreLayerCollision(PlaneLayer, PlaneLayer, true);
        }

        /// <summary>
        /// Stands in for the physical plane-to-plane collisions we switched off: each physics step,
        /// any two planes whose small fuselage hitboxes overlap take a scrape — a little damage and
        /// a model shiver — and keep flying through each other. A scrape the player is part of also
        /// kicks the camera. The play plane is flat (Z frozen), so the test is a plain 2D distance.
        /// </summary>
        void CheckPlaneScrapes()
        {
            float reach = PlaneHitboxRadius * 2f;
            float reachSq = reach * reach;

            // Snapshot the live enemies first: a scrape can drop one to zero health and pull it out
            // of _enemies mid-check (via OnEnemyDestroyed), which would wreck a live iteration.
            _scrapeScratch.Clear();
            foreach (var enemy in _enemies)
                if (enemy != null) _scrapeScratch.Add(enemy);

            // Player against each enemy.
            if (_cube != null && _cube.CurrentHealth > 0f && _cubeTr != null)
            {
                Vector2 playerPos = _cubeTr.position;
                foreach (var enemy in _scrapeScratch)
                {
                    if (enemy == null) continue;
                    if (((Vector2)enemy.transform.position - playerPos).sqrMagnitude > reachSq) continue;

                    bool playerHit = _cube.Scrape();
                    enemy.Scrape();
                    if (playerHit) _camShake = 1f;
                }
            }

            // Enemy against enemy (rare, but keeps the rule consistent — no camera kick).
            for (int i = 0; i < _scrapeScratch.Count; i++)
                for (int j = i + 1; j < _scrapeScratch.Count; j++)
                {
                    var a = _scrapeScratch[i];
                    var b = _scrapeScratch[j];
                    if (a == null || b == null) continue;
                    if (((Vector2)a.transform.position - (Vector2)b.transform.position).sqrMagnitude > reachSq)
                        continue;

                    a.Scrape();
                    b.Scrape();
                }
        }

        void PositionCamera(bool instant)
        {
            Vector3 cubePos = _cubeTr.position;

            // Clamp Y so the view never shows past the ground or the ceiling. With terrain,
            // the camera may sink low enough to reveal the dirt cut below the surface line.
            float minCamY = (VerdunLand ? ProceduralTerrain.CutRevealY : GroundY) + _halfViewHeight;
            float maxCamY = WorldTop - _halfViewHeight;
            if (minCamY > maxCamY) minCamY = maxCamY = (GroundY + WorldTop) * 0.5f;
            float targetY = Mathf.Clamp(cubePos.y, minCamY, maxCamY);

            var target = new Vector3(cubePos.x, targetY, CamZ);

            if (instant)
            {
                _camBasePos = target;
            }
            else
            {
                // Smooth follow (matches the sibling's camera lerp feel). Smooth the un-shaken base
                // so a scrape's jitter can't feed back into the follow and drift the framing.
                float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
                _camBasePos = new Vector3(
                    Mathf.Lerp(_camBasePos.x, target.x, t),
                    Mathf.Lerp(_camBasePos.y, target.y, t),
                    CamZ);
            }

            // Scrape shake: a short decaying jitter laid on top of the follow position.
            Vector3 pos = _camBasePos;
            if (_camShake > 0f)
            {
                Vector2 j = Random.insideUnitCircle * (CamShakeMagnitude * _camShake);
                pos += new Vector3(j.x, j.y, 0f);
            }
            _cam.transform.position = pos;
        }

        // ---------------------------------------------------------------- outcomes

        /// <summary>Health hit zero: the guns fall silent while the plane falls; the crash
        /// (and its MISSION FAILED overlay) comes when it hits the ground.</summary>
        void OnShotDown()
        {
            if (_shooter != null) _shooter.Stop();
        }

        /// <summary>The level was won — every enemy is down — so freeze the fight, unlock the
        /// next level, and show the win overlay.</summary>
        void WinLevel()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();
            if (_shooter != null) _shooter.Stop();
            StandDownEnemies();

            if (GameManager.Instance != null)
                GameManager.Instance.UnlockLevel(levelNumber + 1);

            var canvas = NewOverlay(new Color(0f, 0.08f, 0.02f, 0.8f));
            UIFactory.CreateText(canvas.transform, "MISSION COMPLETE", 90,
                new Vector2(0, 200), new Vector2(1400, 160), TextAnchor.MiddleCenter, FontStyle.Bold)
                .color = new Color(0.6f, 1f, 0.6f);

            float y = 0f;
            if (levelNumber == 1)
            {
                UIFactory.CreateButton(canvas.transform, "NEXT LEVEL", new Vector2(0, y),
                    () => SceneManager.LoadScene(SceneNames.Level2));
                y -= 110f;
            }
            UIFactory.CreateButton(canvas.transform, "BACK TO MENU", new Vector2(0, y),
                () => SceneManager.LoadScene(SceneNames.MainMenu));
        }

        void OnCrashed()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();
            if (_shooter != null) _shooter.Stop();
            StandDownEnemies();

            var canvas = NewOverlay(new Color(0.12f, 0f, 0f, 0.82f));
            UIFactory.CreateText(canvas.transform, "MISSION FAILED", 96,
                new Vector2(0, 200), new Vector2(1400, 170), TextAnchor.MiddleCenter, FontStyle.Bold)
                .color = new Color(1f, 0.45f, 0.4f);

            UIFactory.CreateButton(canvas.transform, "RETRY", new Vector2(0, 0f),
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            UIFactory.CreateButton(canvas.transform, "BACK TO MENU", new Vector2(0, -110f),
                () => SceneManager.LoadScene(SceneNames.MainMenu));
        }

        // ---------------------------------------------------------------- ui

        void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas($"Level{levelNumber} HUD");

            UIFactory.CreateText(canvas.transform, $"LEVEL {levelNumber}", 52,
                new Vector2(0, 480), new Vector2(1000, 90), TextAnchor.MiddleCenter, FontStyle.Bold);

            string mech = GameManager.Instance != null ? GameManager.Instance.SelectedMech : "Unknown";
            UIFactory.CreateText(canvas.transform, $"Piloting: {mech}", 30,
                new Vector2(0, 420), new Vector2(1200, 50));

            UIFactory.CreateText(canvas.transform,
                "A / D to steer  •  F to fire  •  destroy the enemy  •  don't hit the ground", 28,
                new Vector2(0, -500), new Vector2(1600, 50));

            BuildHealthBar(canvas.transform);
        }

        /// <summary>
        /// The player's health readout, top-left: a dark plate, a fill that shrinks leftward
        /// and shades from green to red as damage comes in, and the number on top.
        /// </summary>
        void BuildHealthBar(Transform parent)
        {
            var plate = new GameObject("HealthBar", typeof(Image));
            plate.transform.SetParent(parent, false);
            var plateImg = plate.GetComponent<Image>();
            plateImg.color = new Color(0f, 0f, 0f, 0.55f);
            plateImg.raycastTarget = false;
            var rt = plateImg.rectTransform;
            rt.sizeDelta = new Vector2(HudBarWidth, HudBarHeight);
            rt.anchoredPosition = new Vector2(-660f, 480f);

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(plate.transform, false);
            _healthFill = fillGo.GetComponent<Image>();
            _healthFill.raycastTarget = false;
            var fillRt = _healthFill.rectTransform;
            fillRt.anchorMin = new Vector2(0f, 0.5f); // pinned to the plate's left edge so the
            fillRt.anchorMax = new Vector2(0f, 0.5f); // bar drains right-to-left
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchoredPosition = new Vector2(HudBarPadding, 0f);
            fillRt.sizeDelta = new Vector2(HudBarWidth - HudBarPadding * 2f,
                HudBarHeight - HudBarPadding * 2f);

            _healthText = UIFactory.CreateText(plate.transform, "", 24, Vector2.zero,
                new Vector2(HudBarWidth, HudBarHeight), TextAnchor.MiddleCenter, FontStyle.Bold);

            UpdateHealthHud();
        }

        void UpdateHealthHud()
        {
            if (_cube == null || _healthFill == null) return;

            float frac = _cube.MaxHealth > 0f
                ? Mathf.Clamp01(_cube.CurrentHealth / _cube.MaxHealth) : 0f;

            var size = _healthFill.rectTransform.sizeDelta;
            size.x = (HudBarWidth - HudBarPadding * 2f) * frac;
            _healthFill.rectTransform.sizeDelta = size;
            _healthFill.color = Color.Lerp(
                new Color(0.9f, 0.25f, 0.15f), new Color(0.35f, 0.85f, 0.3f), frac);

            _healthText.text =
                $"{Mathf.CeilToInt(_cube.CurrentHealth)} / {Mathf.CeilToInt(_cube.MaxHealth)}";
        }

        Canvas NewOverlay(Color dimColor)
        {
            var canvas = UIFactory.CreateCanvas($"Level{levelNumber} Overlay");
            canvas.sortingOrder = 100;
            UIFactory.CreateBackground(canvas.transform, dimColor);
            return canvas;
        }
    }
}
