using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class CampaignLevelController : MonoBehaviour, ICampaignScriptHost, IDevSpawnHost
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

        const float OutroFlySec = 4f;
        const float OutroExitMaxSec = 4f;
        const float OutroExitMargin = 120f;
        const float OutroFadeSec = 1.2f;

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

        LevelHud _hudView;
        GameObject _hud;
        HudCurtain _curtain;

        CampaignEnemies _enemies;
        CompanionFlight _wing;
        SupplyDrop _supply;
        Transform _playerModel;
        CampaignScriptRunner _runner;
        DialogueBar _dialogue;
        LevelIntro _intro;

        float _halfViewHeight;
        float _halfViewWidth;
        Vector3 _camBasePos;
        float _camShake;
        bool _gameOver;
        bool _playerFalling;
        bool _outro;
        bool _camHold;

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
            SkyZeppelin.Begin(_cam, _halfViewWidth, _halfViewHeight, PlayPlaneZ,
                CameraDistance, _level.zeppelins);

            PlaneScrapes.DisablePlanePlaneCollisions();
            PlaneScrapes.SetGroundCollisions(true);
            BuildHud();
            BeginSupply();
            _sound = SoundSystem.Begin(_cube, null, silent: HasBriefing);
            BeginIntro();
            BeginCompanion(config);
            ShowBriefing();

            if (CustomBattle.Requested) DevSpawn.Register(this);
        }

        void OnDestroy() => DevSpawn.Unregister(this);

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

            EnsureEnemies();
            _dialogue = new DialogueBar(_hud.transform);
            _runner = CampaignScriptRunner.Begin(gameObject, script, this, _dialogue);
        }

        void EnsureEnemies()
        {
            if (_enemies != null) return;

            _enemies = new CampaignEnemies(_cube.GetComponent<Rigidbody>(), AiGroundY, WorldTop,
                _level);
        }

        float AiGroundY => Coast ? SeaSurface.Level : ProceduralTerrain.MaxHeight;

        bool Cinematic => IntroActive || _outro || CinematicBars.AnyShowing;

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

            LevelCamera.Frame(_cam, CameraDistance, out _halfViewWidth, out _halfViewHeight);
            CutsceneBlur.Focus(CameraDistance);

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
            if (MenuInput.ReadCancel()) TryPause();
        }

        void TryPause()
        {
            if (_gameOver || GameMenu.IsOpen || LevelBriefing.IsOpen || ScreenFade.IsBusy) return;
            GameMenu.Open(GameMenuKind.Pause, Subtitle, _hud);
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

        string Subtitle => CustomBattle.Requested
            ? $"{CustomBattle.Map.Name} | {DaytimeNames.For(_level.daytime)}"
            : _level.title.ToLowerInvariant();

        const string HudObjective = "no turning back  •  don't hit the ground";

        void LateUpdate()
        {
            if (_cubeTr == null) return;

            if (_camShake > 0f)
                _camShake = Mathf.Max(0f, _camShake - Time.unscaledDeltaTime / CamShakeDuration);
            if (_cam != null && !_camHold) PositionCamera(instant: false);

            if (_cube != null && !IntroActive)
                _cube.SetLeftWall(_camBasePos.x - _halfViewWidth + CubeHalf);

            if (_cube != null) _cube.SetCinematic(Cinematic);
            if (_curtain != null) _curtain.Set(!Cinematic);

            if (_terrain != null) _terrain.UpdateStreaming(_camBasePos.x);
            if (_ceilingBar != null)
                _ceilingBar.position = new Vector3(_camBasePos.x, WorldTop, PlayPlaneZ);

            if (!_gameOver && !Cinematic && _sea != null && PlayPlaneZ >= SeaSurface.NearEdge
                && _cubeTr.position.y <= SeaSurface.Level) Ditch();

            if (!_outro) UpdateHud();
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

        public bool CanDevSpawn =>
            !_gameOver && !_playerFalling && _cube != null && _cam != null && !Cinematic;

        public void DevSpawnPlane(EnemyRole role)
        {
            if (!CanDevSpawn) return;

            EnsureEnemies();
            _enemies.Spawn(new[] { new EnemyGroup(PlaneModels.EnemyFor(role), 1) },
                _camBasePos.x, _halfViewWidth);
        }

        public float WarnIncoming(int planes)
        {
            if (_gameOver || _hud == null || planes <= 0) return 0f;

            EnemyWarning warning = EnemyWarning.Show(_hud.transform, planes);
            if (_curtain != null) _curtain.Adopt(warning.gameObject);
            return EnemyWarning.Seconds;
        }

        public void CompleteLevel()
        {
            if (_gameOver) return;
            _gameOver = true;
            _outro = true;

            StopWeapons();
            if (_enemies != null) _enemies.StandDown();
            if (_supply != null) _supply.StandDown();
            if (_dialogue != null) _dialogue.Hide();
            if (_cube != null) _cube.FlyLevel();

            if (!CustomBattle.Requested) CampaignProgress.Complete(_levelNumber);

            StartCoroutine(FlyOut());
        }

        IEnumerator FlyOut()
        {
            yield return Wait(OutroFlySec);

            _camHold = true;
            if (_wing != null) _wing.StandDown();

            float left = OutroExitMaxSec;
            while (left > 0f && !PlaneGone)
            {
                left -= Time.deltaTime;
                yield return null;
            }

            if (_sound != null) _sound.FadeOut(OutroFadeSec);
            ScreenFade.Swap(ShowGroundScene, OutroFadeSec);
        }

        bool PlaneGone => _cubeTr == null
            || _cubeTr.position.x > _camBasePos.x + _halfViewWidth + OutroExitMargin;

        static IEnumerator Wait(float seconds)
        {
            float left = seconds;
            while (left > 0f)
            {
                left -= Time.deltaTime;
                yield return null;
            }
        }

        void ShowGroundScene()
        {
            if (_cube != null) _cube.Stop();
            LevelOutro.Open(_level.outro, ShowJournal);
        }

        void ShowJournal()
        {
            if (string.IsNullOrEmpty(_level.journal)) { ShowCompleted(); return; }

            LevelBriefing.OpenJournal(LevelOutro.JournalTitle,
                CampaignLevelEntry.DatePart(_level.dateline), _level.journal, ShowCompleted);
        }

        void ShowCompleted()
        {
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

            _hudView = new LevelHud(canvas.transform, HudObjective, _cube, _shooter, _bomber,
                _boost, _searchlight, TryPause);

            _curtain = HudCurtain.Attach(_hud);
            _curtain.Set(false);
        }

        void UpdateHud()
        {
            if (_hudView != null) _hudView.Tick();
        }
    }
}
