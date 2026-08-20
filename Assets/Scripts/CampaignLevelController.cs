using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class CampaignLevelController : MonoBehaviour, ICampaignScriptHost
    {
        const float WorldTop = 650f;
        const float CubeHalf = 15f;
        const float CameraDistance = 420f;
        const float PlayPlaneZ = 100f;
        const float CamZ = PlayPlaneZ - CameraDistance;
        const float StartX = 0f;
        const float SpawnY = 150f;
        const float CamResponse = 8f;
        const float FallCamResponse = 3.3f;
        const float CamShakeMagnitude = 7f;
        const float CamShakeDuration = 0.3f;

        const float TaskLeft = -860f;
        const float TaskTop = 321f;

        const float DitchSplashSize = 75f;
        const float SinkSpeed = 26f;
        const float SinkDriftKeep = 0.15f;
        const float SinkDuration = 2f;

        CampaignDefinition _level;
        int _levelNumber;
        CubeController _cube;
        PlaneShooter _shooter;
        PlaneBomber _bomber;
        PlaneBoost _boost;
        PlaneSearchlight _searchlight;
        Transform _cubeTr;
        Camera _cam;
        CampaignTerrain _terrain;
        SeaSurface _sea;
        Transform _ceilingBar;
        SoundSystem _sound;

        HealthBar _healthBar;
        SearchlightIndicator _lightIndicator;
        CooldownSquare _bombSquare;
        CooldownSquare _boostSquare;
        Text _distanceText;
        GameObject _hud;
        HudCurtain _curtain;

        CampaignEnemies _enemies;
        CompanionFlight _wing;
        SupplyDrop _supply;
        Transform _playerModel;
        CampaignScriptRunner _runner;
        DialogueBar _dialogue;
        LevelTask _task;
        LevelIntro _intro;

        Vector2 _taskCorner;
        float _halfViewHeight;
        float _halfViewWidth;
        Vector3 _camBasePos;
        float _camShake;
        bool _gameOver;
        bool _playerFalling;
        float _furthestX = StartX;

        float Distance => Mathf.Max(0f, _furthestX - StartX);

        public bool IsOver => _gameOver;

        public int EnemiesAlive => _enemies != null ? _enemies.AliveCount : 0;

        public bool CompanionReady => _wing == null || _wing.Formed;

        bool Coast => _level.terrain == TerrainKind.Flanders;

        bool Alpine => _level.terrain == TerrainKind.Dolomites;

        void Start()
        {
            _levelNumber = CampaignRun.Level;
            _level = CustomBattle.Requested
                ? CampaignLevels.Custom(CustomBattle.Map, CustomBattle.Daytime)
                : CampaignLevels.ForNumber(_levelNumber);

            var config = Resources.Load<PlayerConfig>("PlayerConfig");
            if (config == null) config = ScriptableObject.CreateInstance<PlayerConfig>();

            ConfigureShadows();
            _terrain = CampaignTerrain.Begin(_level.terrain, _level.seed, _level.daytime,
                _level.weather, CameraDistance, PlayPlaneZ, StartX);
            SpawnPlayer(config);
            SetupCamera();

            if (Coast)
            {
                _sea = SeaSurface.Begin(_cam, _level.daytime);
                Battlefield.BeginCoast(_cam, _halfViewWidth, _level.seed,
                    SeaSurface.Level, SeaSurface.NearEdge);
            }
            else if (Alpine)
            {
                MountainRange.Begin(_cam, _level.seed, _level.daytime);
                Battlefield.BeginValley(_cam, _halfViewWidth, _level.seed, _terrain.InCrater,
                    DolomitesTerrain.ValleyZMax);
            }
            else
            {
                Battlefield.Begin(_cam, _halfViewWidth, _level.seed, _terrain.InCrater);
            }

            SkyFlak.Begin(_cam, _cubeTr, _halfViewWidth, _halfViewHeight, PlayPlaneZ, _level.flak);

            PlaneScrapes.DisablePlanePlaneCollisions();
            PlaneScrapes.SetGroundCollisions(true);
            BuildHud();
            BeginSupply();
            _sound = SoundSystem.Begin(_cube, null, silent: HasBriefing);
            BeginIntro();
            BeginCompanion(config);
            ShowBriefing();
        }

        bool HasBriefing => !CustomBattle.Requested && !string.IsNullOrEmpty(_level.title);

        bool IntroActive => _intro != null && _intro.Active;

        void BeginIntro()
        {
            _intro = LevelIntro.Begin(gameObject, _cube, _shooter, _bomber, _boost, StartX,
                _halfViewWidth, BeginScript);
        }

        void BeginSupply()
        {
            _supply = SupplyDrop.Begin(gameObject, _level, _cube, _playerModel,
                PlayPlaneZ, AiGroundY);
        }

        void BeginCompanion(PlayerConfig config)
        {
            if (CustomBattle.Requested) return;

            _wing = CompanionFlight.Begin(_level, config, _cubeTr, PlayPlaneZ, AiGroundY, WorldTop,
                CameraDistance);
        }

        void ShowBriefing()
        {
            if (!HasBriefing) return;

            LevelBriefing.Open($"LEVEL {_levelNumber}", _level.title, _level.dateline, _level.lore,
                _hud, ArmSound);
        }

        void ArmSound()
        {
            if (_sound != null) _sound.Arm();
        }

        void BeginScript()
        {
            if (CustomBattle.Requested || string.IsNullOrEmpty(_level.script)) return;

            CampaignScript script = CampaignScript.Load(_level.script);
            if (script == null) return;

            _enemies = new CampaignEnemies(_cube.GetComponent<Rigidbody>(), AiGroundY, WorldTop,
                _level);
            _dialogue = new DialogueBar(_hud.transform);
            _runner = CampaignScriptRunner.Begin(gameObject, script, this, _dialogue);
        }

        float AiGroundY => Coast ? SeaSurface.Level : ProceduralTerrain.MaxHeight;

        bool Cinematic => IntroActive || CinematicBars.AnyShowing;

        void ConfigureShadows()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, CameraDistance + 200f);
        }

        void SpawnPlayer(PlayerConfig config)
        {
            var go = new GameObject("PlayerPlane");
            go.transform.position = new Vector3(StartX, SpawnY, PlayPlaneZ);

            var planeModel = GameManager.CurrentPlane;
            var model = PlaneFactory.BuildPlaneModel(go.transform, planeModel,
                skin: GameManager.CurrentSkin);
            _playerModel = model;

            PlayerConfig flight = PlaneLoadout.Build(config, planeModel);

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnShotDown += OnShotDown;
            _cube.OnDamaged += OnPlayerDamaged;
            _cube.OnScraped += OnPlayerScraped;

            _cube.Initialize(flight, 0f, float.MinValue, float.MaxValue,
                WorldTop - CubeHalf, 0f, hardLeftWall: true);

            var muzzle = PlaneFactory.MountMuzzle(go, model, planeModel, out var flashPoint);
            var hitbox = go.GetComponentInChildren<Collider>();

            _shooter = go.AddComponent<PlaneShooter>();
            _shooter.Initialize(flight, muzzle, flashPoint, hitbox);

            _bomber = go.AddComponent<PlaneBomber>();
            _bomber.Initialize(flight, hitbox);
            _bomber.OnDetonated += OnBombDetonated;

            _boost = go.AddComponent<PlaneBoost>();
            _boost.Initialize(flight, _cube, model);

            _searchlight = PlaneSearchlight.Mount(go,
                PlaneFactory.NoseLocal(go, model, planeModel), _level.daytime);
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null) return;

            _cam.orthographic = false;
            _cam.transform.rotation = Quaternion.identity;

            if (Coast)
            {
                CoastSky.Apply(_cam, _level.daytime, _level.weather);
            }
            else if (Alpine)
            {
                DolomitesSky.Apply(_cam, _level.daytime, _level.weather);
            }
            else
            {
                switch (_level.daytime)
                {
                    case Daytime.Midday: MiddaySky.Apply(_cam, _level.weather); break;
                    case Daytime.Evening: EveningSky.Apply(_cam, _level.weather); break;
                    case Daytime.Night: NightSky.Apply(_cam, _level.weather); break;
                    default: MorningSky.Apply(_cam, _level.weather); break;
                }
            }
            _cam.farClipPlane = 2600f;

            _halfViewHeight = CameraDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _halfViewWidth = _halfViewHeight * _cam.aspect;

            PositionCamera(instant: true);

            if (_level.clouds == null) return;

            if (Coast)
                CloudSystem.Begin(_cam, CoastSky.CloudColor(_level.daytime),
                    CoastSky.CloudGlow(_level.daytime), _level.clouds, PlayPlaneZ);
            else if (Alpine)
                CloudSystem.Begin(_cam, DolomitesSky.CloudColor(_level.daytime),
                    DolomitesSky.CloudGlow(_level.daytime), _level.clouds, PlayPlaneZ);
            else
                CloudSystem.Begin(_cam, _level.daytime, _level.weather, _level.clouds, PlayPlaneZ);
        }

        void Update()
        {
            if (_gameOver || GameMenu.IsOpen || LevelBriefing.IsOpen || ScreenFade.IsBusy) return;
            if (MenuInput.ReadCancel()) GameMenu.Open(GameMenuKind.Pause, Subtitle, _hud);
        }

        void FixedUpdate()
        {
            if (_gameOver) return;
            PlaneScrapes.Check(_cube, _cubeTr, _enemies != null ? _enemies.Live : null);
            if (_wing != null && _wing.CheckBump(_cubeTr)) OnCompanionBump();
        }

        void OnCompanionBump()
        {
            _camShake = 1f;
            if (_cube != null) _cube.Bump();
        }

        string MapName => TerrainNames.For(_level.terrain);

        string Subtitle => CustomBattle.Requested
            ? $"{CustomBattle.Map.Name} | {DaytimeNames.For(_level.daytime)}"
            : $"level {_levelNumber} | {MapName}";

        string HudTitle => CustomBattle.Requested
            ? $"CUSTOM BATTLE — {MapName.ToUpperInvariant()}"
            : $"CAMPAIGN — LEVEL {_levelNumber}";

        string HudHint => "A / D to steer  •  F to fire  •  H to bomb  •  R to boost  •  "
            + "no turning back  •  don't hit the ground";

        void LateUpdate()
        {
            if (_cubeTr == null) return;

            if (!_playerFalling) _furthestX = Mathf.Max(_furthestX, _cubeTr.position.x);
            if (_camShake > 0f)
                _camShake = Mathf.Max(0f, _camShake - Time.deltaTime / CamShakeDuration);
            if (_cam != null) PositionCamera(instant: false);

            if (_cube != null && !IntroActive)
                _cube.SetLeftWall(_camBasePos.x - _halfViewWidth + CubeHalf);

            if (_cube != null) _cube.SetCinematic(Cinematic);
            if (_curtain != null) _curtain.Set(!Cinematic);

            if (_terrain != null) _terrain.UpdateStreaming(_camBasePos.x);
            if (_ceilingBar != null)
                _ceilingBar.position = new Vector3(_camBasePos.x, WorldTop, PlayPlaneZ);

            if (!_gameOver && !Cinematic && _sea != null && PlayPlaneZ >= SeaSurface.NearEdge
                && _cubeTr.position.y <= SeaSurface.Level) Ditch();

            UpdateHud();
            if (!_gameOver && _supply != null)
                _supply.Tick(_camBasePos, _halfViewWidth, _halfViewHeight, Cinematic);
            if (_enemies != null) _enemies.SetWindow(_camBasePos.x, _halfViewWidth);

            if (_wing == null) return;
            _wing.SetWindow(_camBasePos, _halfViewWidth, _halfViewHeight);
            _wing.SetCinematic(Cinematic);
            _wing.Tick(Time.deltaTime);
        }

        void PositionCamera(bool instant)
        {
            Vector3 cubePos = _cubeTr.position;

            float minCamY = CamFloorY;
            float maxCamY = WorldTop - _halfViewHeight;
            if (minCamY > maxCamY) minCamY = maxCamY = WorldTop * 0.5f;
            float targetY = Mathf.Clamp(cubePos.y, minCamY, maxCamY);

            if (instant)
            {
                _camBasePos = new Vector3(Mathf.Max(StartX, cubePos.x), targetY, CamZ);
            }
            else
            {
                float response = _playerFalling ? FallCamResponse : CamResponse;
                float t = 1f - Mathf.Exp(-response * Time.deltaTime);
                float x = Mathf.Max(_camBasePos.x, Mathf.Lerp(_camBasePos.x, cubePos.x, t));
                _camBasePos = new Vector3(x, Mathf.Lerp(_camBasePos.y, targetY, t), CamZ);
            }

            Vector3 pos = _camBasePos;
            if (_camShake > 0f)
            {
                Vector2 j = Random.insideUnitCircle * (CamShakeMagnitude * _camShake);
                pos += new Vector3(j.x, j.y, 0f);
            }
            _cam.transform.position = pos;
        }

        public void SpawnWave(EnemyGroup[] groups)
        {
            if (_gameOver || _enemies == null) return;
            _enemies.Spawn(groups, _camBasePos.x, _halfViewWidth);
        }

        public float WarnIncoming(int planes)
        {
            if (_gameOver || _hud == null || planes <= 0) return 0f;

            EnemyWarning warning = EnemyWarning.Show(_hud.transform, planes);
            if (_curtain != null) _curtain.Adopt(warning.gameObject);
            return EnemyWarning.Seconds;
        }

        public void ShowTask(string text)
        {
            if (_gameOver || _hud == null) return;

            if (_task != null) Destroy(_task.gameObject);
            _task = LevelTask.Create(_hud.transform, _taskCorner, text);
            if (_curtain != null) _curtain.Adopt(_task.gameObject);
        }

        public float CompleteTask()
        {
            if (_task == null || _task.IsCompleting) return 0f;
            return _task.Complete();
        }

        public void CompleteLevel()
        {
            if (_gameOver) return;
            _gameOver = true;

            _cube.Stop();
            StopWeapons();
            if (_enemies != null) _enemies.StandDown();
            if (_wing != null) _wing.StandDown();
            if (_supply != null) _supply.StandDown();
            if (_dialogue != null) _dialogue.Hide();
            if (_sound != null) _sound.EnterGameOver();

            bool hasNext = _levelNumber < CampaignRun.LastLevel;
            GameMenu.Open(GameMenuKind.Completed, Subtitle, _hud,
                hasNext ? SceneNames.CampaignLevel1 : null,
                hasNext ? (System.Action)(() => CampaignRun.Request(_levelNumber + 1)) : null);
        }

        void StopScript()
        {
            if (_runner != null) _runner.Stop();
            if (_enemies != null) _enemies.StandDown();
            if (_wing != null) _wing.StandDown();
            if (_supply != null) _supply.StandDown();
        }

        void Ditch()
        {
            _gameOver = true;
            StopScript();

            Vector3 pos = _cubeTr.position;
            WaterSplash.Spawn(new Vector3(pos.x, SeaSurface.Level, pos.z), DitchSplashSize,
                _cam != null ? _cam.transform.position : pos);

            _cube.Sink(SinkSpeed, SinkDriftKeep);
            StopWeapons();
            if (_sound != null) _sound.EnterGameOver();

            StartCoroutine(ShowFailScreenAfter(SinkDuration));
        }

        float CamFloorY
        {
            get
            {
                float reveal = _playerFalling
                    ? ProceduralTerrain.WallBottomY : ProceduralTerrain.CutRevealY;
                return reveal + _halfViewHeight;
            }
        }

        void OnShotDown()
        {
            _playerFalling = true;
            StopScript();
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

        void OnCrashed()
        {
            if (_gameOver) return;
            _gameOver = true;
            StopScript();
            _cube.Stop();
            StopWeapons();
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
            var canvas = UIFactory.CreateCanvas("Campaign HUD");
            _hud = canvas.gameObject;

            UIFactory.CreateText(canvas.transform, HudTitle, 52,
                new Vector2(0, 480), new Vector2(1000, 90), TextAnchor.MiddleCenter, FontStyle.Bold);

            UIFactory.CreateText(canvas.transform, HudHint, 28,
                new Vector2(0, -500), new Vector2(1600, 50));

            _healthBar = new HealthBar(canvas.transform, new Vector2(-660f, 480f));
            _bombSquare = new CooldownSquare(canvas.transform, new Vector2(-832f, 425f), "H",
                CooldownSquare.BombTint);
            _boostSquare = new CooldownSquare(canvas.transform, new Vector2(-832f, 361f), "R",
                CooldownSquare.BoostTint);
            if (_searchlight != null)
                _lightIndicator = new SearchlightIndicator(canvas.transform, new Vector2(-719f, 425f));
            _distanceText = UIFactory.CreateText(canvas.transform, "0 m", 40,
                new Vector2(660f, 480f), new Vector2(500, 60), TextAnchor.MiddleRight, FontStyle.Bold);

            _taskCorner = new Vector2(TaskLeft, TaskTop);
            _curtain = HudCurtain.Attach(_hud);
            _curtain.Set(false);
            UpdateHud();
        }

        void UpdateHud()
        {
            if (_lightIndicator != null && _searchlight != null)
                _lightIndicator.Set(_searchlight.IsOn);
            if (_bombSquare != null && _bomber != null)
                _bombSquare.Set(_bomber.Charge, _bomber.IsReady);
            if (_boostSquare != null && _boost != null)
                _boostSquare.Set(_boost.Charge, _boost.IsReady || _boost.IsRunning);
            if (_cube != null && _healthBar != null)
                _healthBar.Set(_cube.CurrentHealth, _cube.MaxHealth);
            if (_distanceText != null)
                _distanceText.text = $"{Mathf.FloorToInt(Distance)} m";
        }
    }
}
