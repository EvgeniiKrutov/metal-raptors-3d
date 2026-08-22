using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class LevelController : MonoBehaviour
    {
        [Tooltip("Which level this scene represents (1 for Level1, 2 for Level2, ...). " +
                 "Everything else — terrain, daytime, weather, enemies — comes from this " +
                 "level's definition in the Levels registry.")]
        [SerializeField] int levelNumber = 1;

        LevelDefinition _level;

        const float WorldHeight = 650f;
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

        const float CamResponse = 8f;
        const float FallCamResponse = 3.3f;
        const float CamShakeMagnitude = 7f;
        const float CamShakeDuration = 0.3f;
        const float CameraDistance = 420f;
        const float PlayPlaneZ = 100f;
        const float CamZ = PlayPlaneZ - CameraDistance;
        const float BackdropZ = PlayPlaneZ + 150f;

        CubeController _cube;
        PlaneShooter _shooter;
        PlaneBomber _bomber;
        PlaneBoost _boost;
        PlaneSearchlight _searchlight;
        Transform _cubeTr;
        Camera _cam;

        EnemyConfig _enemyConfig;
        readonly List<EnemyController> _enemies = new List<EnemyController>();
        SoundSystem _sound;

        LevelHud _hudView;
        GameObject _hud;

        float _halfViewHeight;
        float _halfViewWidth;
        bool _gameOver;
        bool _playerFalling;
        float _camShake;
        Vector3 _camBasePos;
        System.Func<float, float, bool> _inCrater;

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
            SkyFlak.Begin(_cam, _cubeTr, _halfViewWidth, _halfViewHeight, PlayPlaneZ, _level.flak);
            PlaneScrapes.DisablePlanePlaneCollisions();
            PlaneScrapes.SetGroundCollisions(true);
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
                var flat = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(0f, GroundY - 10f, 0f),
                    new Vector3(WorldWidth + 200f, 20f, 400f),
                    new Color(0.20f, 0.22f, 0.16f));
                flat.layer = ProceduralTerrain.GroundLayer;

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
            var plane = PlaneFactory.BuildPlaneModel(go.transform, planeModel,
                skin: GameManager.CurrentSkin);

            PlayerConfig flight = PlaneLoadout.Build(config, planeModel);

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnShotDown += OnShotDown;
            _cube.OnDamaged += OnPlayerDamaged;
            _cube.OnScraped += OnPlayerScraped;

            _cube.Initialize(flight, 0f, MinX, MaxX, WorldTop - CubeHalf, EdgeMargin);

            SetupGuns(flight, go, plane, planeModel);

            _searchlight = PlaneSearchlight.Mount(go,
                PlaneFactory.NoseLocal(go, plane, planeModel), _level.daytime);
        }

        void SetupGuns(PlayerConfig flight, GameObject body, Transform model, PlaneModelConfig plane)
        {
            var muzzle = PlaneFactory.MountMuzzle(body, model, plane, out var flashPoint);
            var hitbox = body.GetComponentInChildren<Collider>();

            _shooter = body.AddComponent<PlaneShooter>();
            _shooter.Initialize(flight, muzzle, flashPoint, hitbox);

            _bomber = body.AddComponent<PlaneBomber>();
            _bomber.Initialize(flight, hitbox);
            _bomber.OnDetonated += OnBombDetonated;

            _boost = body.AddComponent<PlaneBoost>();
            _boost.Initialize(flight, _cube, model);
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
                    PlaneFactory.BuildPlaneModel(go.transform, group.plane, mirrored: true,
                        skin: PlaneSkins.Default(group.plane));

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
                StartCoroutine(WinAfterWreck(enemy));
        }

        IEnumerator WinAfterWreck(EnemyController wreck)
        {
            while (wreck != null) yield return null;

            if (_cube != null && _cube.CurrentHealth <= 0f) yield break;
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
            if (MenuInput.ReadCancel()) TryPause();
        }

        void TryPause()
        {
            if (_gameOver || GameMenu.IsOpen || ScreenFade.IsBusy) return;
            GameMenu.Open(GameMenuKind.Pause, Subtitle, _hud);
        }

        string Subtitle => $"level {levelNumber} | {TerrainNames.For(_level.terrain.kind)}";

        void FixedUpdate()
        {
            if (_gameOver) return;
            PlaneScrapes.Check(_cube, _cubeTr, _enemies);
        }

        void LateUpdate()
        {
            if (_cam != null && _cubeTr != null) PositionCamera(instant: false);
            if (_camShake > 0f) _camShake = Mathf.Max(0f, _camShake - Time.deltaTime / CamShakeDuration);
            UpdateHealthHud();
        }

        void PositionCamera(bool instant)
        {
            Vector3 cubePos = _cubeTr.position;

            float minCamY = CamFloorY;
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
                float response = _playerFalling ? FallCamResponse : CamResponse;
                float t = 1f - Mathf.Exp(-response * Time.deltaTime);
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

        float CamFloorY
        {
            get
            {
                if (!VerdunLand) return GroundY + _halfViewHeight;

                float reveal = _playerFalling
                    ? ProceduralTerrain.WallBottomY : ProceduralTerrain.CutRevealY;
                return reveal + _halfViewHeight;
            }
        }

        void OnShotDown()
        {
            _playerFalling = true;
            StopWeapons();
            if (_sound != null) _sound.EnterGameOver();
        }

        void OnPlayerDamaged()
        {
            if (_sound != null) _sound.ReportPlayerDamaged();
        }

        void OnPlayerScraped() => _camShake = 1f;

        void OnBombDetonated(Vector3 position, float radius)
        {
            float reach = radius * Bomb.ShakeRadii;
            float distance = new Vector2(position.x - _camBasePos.x,
                position.y - _camBasePos.y).magnitude;

            _camShake = Mathf.Max(_camShake, 1f - Mathf.Clamp01(distance / reach));
        }

        void StopWeapons()
        {
            if (_shooter != null) _shooter.Stop();
            if (_bomber != null) _bomber.Stop();
            if (_boost != null) _boost.Stop();
        }

        void WinLevel()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();
            StopWeapons();
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
            StopWeapons();
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

            Text piloting = UIFactory.CreateText(canvas.transform,
                $"Piloting: {GameManager.CurrentPlane.displayName}", 30, Vector2.zero, Vector2.zero);
            var rt = piloting.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1200f, HudTheme.BarHeight);
            rt.anchoredPosition = new Vector2(0f, -HudTheme.ColumnTop);

            _hudView = new LevelHud(canvas.transform,
                "destroy the enemy  •  don't hit the ground",
                _cube, _shooter, _bomber, _boost, _searchlight, TryPause);
        }

        void UpdateHealthHud()
        {
            if (_hudView != null) _hudView.Tick();
        }
    }
}
