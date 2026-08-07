using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class LevelController : MonoBehaviour
    {
        [Tooltip("Which level this scene represents (1 for Level1, 2 for Level2, ...). " +
                 "Everything else — terrain, daytime, weather, enemies — comes from this " +
                 "level's definition in the Levels registry.")]
        [SerializeField] int levelNumber = 1;

        LevelDefinition _level;

        const float WorldHeight = 900f;
        const float GroundY = 0f;
        const float WorldTop = WorldHeight;
        const float SkyHeadroom = 400f;
        float WorldWidth => _level.terrain.width;
        float MinX => -WorldWidth / 2f;
        float MaxX => WorldWidth / 2f;

        bool VerdunLand => _level.terrain.kind == TerrainKind.Verdun;

        const float EdgeMargin = 220f;

        const float CubeScale = 30f;
        const float CubeHalf = CubeScale / 2f;

        const float PlaneHitboxRadius = 15f;

        const float CamShakeMagnitude = 7f;
        const float CamShakeDuration = 0.3f;
        const float CameraDistance = 420f;
        const float PlayPlaneZ = 100f;
        const float CamZ = PlayPlaneZ - CameraDistance;
        const float BackdropZ = PlayPlaneZ + 150f;

        CubeController _cube;
        PlaneShooter _shooter;
        PlaneSearchlight _searchlight;
        Transform _cubeTr;
        Camera _cam;

        EnemyConfig _enemyConfig;
        readonly List<EnemyController> _enemies = new List<EnemyController>();
        SoundSystem _sound;

        HealthBar _healthBar;
        SearchlightIndicator _lightIndicator;
        GameObject _hud;

        float _halfViewHeight;
        float _halfViewWidth;
        bool _gameOver;
        float _camShake;
        Vector3 _camBasePos;
        System.Func<float, float, bool> _inCrater;
        readonly List<EnemyController> _scrapeScratch = new List<EnemyController>();

        void Start()
        {
            _level = Levels.ForNumber(levelNumber);

            var config = Resources.Load<PlayerConfig>("PlayerConfig");
            if (config == null) config = ScriptableObject.CreateInstance<PlayerConfig>();

            ConfigureShadows();
            BuildWorld();
            SpawnPlayer(config);
            SetupCamera();
            SpawnEnemies();
            if (VerdunLand)
                Battlefield.Begin(_cam, _halfViewWidth, _level.terrain.seed, MinX, MaxX, _inCrater);
            DisablePlanePlaneCollisions();
            BuildHud();
            _sound = SoundSystem.Begin(_cube, _enemies);
        }

        void ConfigureShadows()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, CameraDistance + 200f);
            }
        }

        void BuildWorld()
        {
            if (VerdunLand)
            {
                _inCrater = ProceduralTerrain.Build(_level.terrain.seed, WorldWidth,
                    CameraDistance, PlayPlaneZ, _level.daytime, _level.weather);
            }
            else
            {
                UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(0f, GroundY - 10f, 0f),
                    new Vector3(WorldWidth + 200f, 20f, 400f),
                    new Color(0.20f, 0.22f, 0.16f));

                const float backdropBottomY = -100f;
                float backdropTopY = WorldTop + SkyHeadroom;
                var backdrop = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(0f, (backdropTopY + backdropBottomY) * 0.5f, BackdropZ),
                    new Vector3(WorldWidth + 400f, backdropTopY - backdropBottomY, 10f),
                    new Color(0.16f, 0.17f, 0.20f), keepCollider: false);
                var backdropRenderer = backdrop.GetComponent<Renderer>();
                if (backdropRenderer != null)
                    backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void SpawnPlayer(PlayerConfig config)
        {
            var go = new GameObject("PlayerPlane");
            go.transform.position = new Vector3(MinX + EdgeMargin, 150f, PlayPlaneZ);

            var planeModel = GameManager.CurrentPlane;
            var plane = PlaneFactory.BuildPlaneModel(go.transform, planeModel);

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnShotDown += OnShotDown;
            _cube.OnDamaged += OnPlayerDamaged;
            _cube.OnScraped += OnPlayerScraped;

            _cube.Initialize(config, 0f, MinX, MaxX, WorldTop - CubeHalf, EdgeMargin);

            SetupGuns(config, go, plane, planeModel);

            _searchlight = PlaneSearchlight.Mount(go,
                PlaneFactory.NoseLocal(go, plane, planeModel), _level.daytime);
        }

        void SetupGuns(PlayerConfig config, GameObject body, Transform model, PlaneModelConfig plane)
        {
            var muzzle = PlaneFactory.MountMuzzle(body, model, plane, out var flashPoint);
            _shooter = body.AddComponent<PlaneShooter>();
            _shooter.Initialize(config, muzzle, flashPoint, body.GetComponentInChildren<Collider>());
        }

        void SpawnEnemies()
        {
            _enemyConfig = Resources.Load<EnemyConfig>("EnemyConfig");
            if (_enemyConfig == null) _enemyConfig = ScriptableObject.CreateInstance<EnemyConfig>();

            var playerBody = _cube.GetComponent<Rigidbody>();
            float aiGroundY = VerdunLand ? ProceduralTerrain.MaxHeight : GroundY;

            foreach (var group in _level.enemies)
                for (int i = 0; i < group.count; i++)
                {
                    var go = new GameObject("Enemy");
                    go.transform.position = RandomEnemySpawn(aiGroundY);
                    PlaneFactory.BuildPlaneModel(go.transform, group.plane, mirrored: true);

                    var enemy = go.AddComponent<EnemyController>();
                    enemy.Initialize(_enemyConfig, playerBody,
                        MinX, MaxX, aiGroundY, WorldTop - group.plane.onScreenSize / 2f, EdgeMargin);
                    enemy.OnDestroyed += OnEnemyDestroyed;
                    _enemies.Add(enemy);
                }
        }

        Vector3 RandomEnemySpawn(float aiGroundY)
        {
            float halfViewWidth = _cam != null ? _halfViewWidth : _halfViewHeight * (16f / 9f);
            float camX = _cam != null ? _cam.transform.position.x : 0f;

            float minY = aiGroundY + _enemyConfig.safeAltitudeMargin;
            float maxY = Mathf.Max(minY, WorldTop - 120f);

            float x = 0f;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                x = Random.Range(MinX + EdgeMargin, MaxX - EdgeMargin);
                if (Mathf.Abs(x - camX) > halfViewWidth + 60f) break;
            }
            return new Vector3(x, Random.Range(minY, maxY), PlayPlaneZ);
        }

        void StandDownEnemies()
        {
            foreach (var enemy in _enemies)
                if (enemy != null) enemy.StandDown();
        }

        void OnEnemyDestroyed(EnemyController enemy)
        {
            _enemies.Remove(enemy);

            if (!_gameOver && _enemies.Count == 0 && _cube != null && _cube.CurrentHealth > 0f)
                WinLevel();
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null) return;

            _cam.orthographic = false;
            _cam.transform.rotation = Quaternion.identity;

            if (VerdunLand)
            {
                switch (_level.daytime)
                {
                    case Daytime.Midday: MiddaySky.Apply(_cam, _level.weather); break;
                    case Daytime.Evening: EveningSky.Apply(_cam, _level.weather); break;
                    case Daytime.Night: NightSky.Apply(_cam, _level.weather); break;
                    default: MorningSky.Apply(_cam, _level.weather); break;
                }
                _cam.farClipPlane = 2200f;
            }

            _halfViewHeight = CameraDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _halfViewWidth = _halfViewHeight * _cam.aspect;

            PositionCamera(instant: true);

            if (_level.clouds != null)
                CloudSystem.Begin(_cam, _level.daytime, _level.weather, _level.clouds, PlayPlaneZ);
        }

        void Update()
        {
            if (_gameOver || GameMenu.IsOpen) return;
            if (MenuInput.ReadCancel()) GameMenu.Open(GameMenuKind.Pause, Subtitle, _hud);
        }

        string Subtitle => $"level {levelNumber} | {TerrainNames.For(_level.terrain.kind)}";

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

        void DisablePlanePlaneCollisions()
        {
            Physics.IgnoreLayerCollision(PlaneFactory.PlaneLayer, PlaneFactory.PlaneLayer, true);
        }

        void CheckPlaneScrapes()
        {
            float reach = PlaneHitboxRadius * 2f;
            float reachSq = reach * reach;

            _scrapeScratch.Clear();
            foreach (var enemy in _enemies)
                if (enemy != null) _scrapeScratch.Add(enemy);

            if (_cube != null && _cube.CurrentHealth > 0f && _cubeTr != null)
            {
                Vector2 playerPos = _cubeTr.position;
                foreach (var enemy in _scrapeScratch)
                {
                    if (enemy == null) continue;
                    if (((Vector2)enemy.transform.position - playerPos).sqrMagnitude > reachSq) continue;

                    _cube.Scrape();
                    enemy.Scrape();
                }
            }

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
                float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
                _camBasePos = new Vector3(
                    Mathf.Lerp(_camBasePos.x, target.x, t),
                    Mathf.Lerp(_camBasePos.y, target.y, t),
                    CamZ);
            }

            Vector3 pos = _camBasePos;
            if (_camShake > 0f)
            {
                Vector2 j = Random.insideUnitCircle * (CamShakeMagnitude * _camShake);
                pos += new Vector3(j.x, j.y, 0f);
            }
            _cam.transform.position = pos;
        }

        void OnShotDown()
        {
            if (_shooter != null) _shooter.Stop();
            if (_sound != null) _sound.EnterGameOver();
        }

        void OnPlayerDamaged()
        {
            if (_sound != null) _sound.ReportPlayerDamaged();
        }

        void OnPlayerScraped() => _camShake = 1f;

        void WinLevel()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();
            if (_shooter != null) _shooter.Stop();
            StandDownEnemies();
            if (_sound != null) _sound.EnterGameOver();

            if (GameManager.Instance != null)
                GameManager.Instance.UnlockLevel(levelNumber + 1);

            GameMenu.Open(GameMenuKind.Completed, Subtitle, _hud, NextScene);
        }

        string NextScene => levelNumber == 1 ? SceneNames.Level2 : null;

        void OnCrashed()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();
            if (_shooter != null) _shooter.Stop();
            StandDownEnemies();
            if (_sound != null) _sound.EnterGameOver();

            StartCoroutine(ShowFailScreenAfter(Explosion.Duration));
        }

        IEnumerator ShowFailScreenAfter(float delay)
        {
            yield return new WaitForSeconds(delay);

            GameMenu.Open(GameMenuKind.Failed, Subtitle, _hud);
        }

        void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas($"Level{levelNumber} HUD");
            _hud = canvas.gameObject;

            UIFactory.CreateText(canvas.transform, $"LEVEL {levelNumber}", 52,
                new Vector2(0, 480), new Vector2(1000, 90), TextAnchor.MiddleCenter, FontStyle.Bold);

            UIFactory.CreateText(canvas.transform, $"Piloting: {GameManager.CurrentPlane.displayName}", 30,
                new Vector2(0, 420), new Vector2(1200, 50));

            UIFactory.CreateText(canvas.transform,
                "A / D to steer  •  F to fire  •  destroy the enemy  •  don't hit the ground", 28,
                new Vector2(0, -500), new Vector2(1600, 50));

            _healthBar = new HealthBar(canvas.transform, new Vector2(-660f, 480f));
            if (_searchlight != null)
                _lightIndicator = new SearchlightIndicator(canvas.transform, new Vector2(-785f, 435f));
            UpdateHealthHud();
        }

        void UpdateHealthHud()
        {
            if (_lightIndicator != null && _searchlight != null)
                _lightIndicator.Set(_searchlight.IsOn);
            if (_cube == null || _healthBar == null) return;
            _healthBar.Set(_cube.CurrentHealth, _cube.MaxHealth);
        }
    }
}
