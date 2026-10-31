using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class ArenaLevelController : MonoBehaviour, ICampaignScriptHost, IBossHost,
        IDangerHost
    {
        public const float CameraDistance = 420f;
        const float PlayPlaneZ = 100f;
        const float CamZ = PlayPlaneZ - CameraDistance;
        const float EyeY = 500f;

        const float SideMargin = 40f;
        const float TopMargin = 28f;
        const float FloorSkim = 0.03f;
        const float EnemyFloorDrop = 110f;

        const float CloudSpeedFactor = 1.2f;
        const float HoverSpeedFactor = 1.17f;
        const float PlaneScale = 1.4f;
        const float RollSpinScale = 1.3f;
        const float RollCooldown = 3f;
        const float StageTwoRollFactor = 0.5f;

        const float BossEntryMargin = 180f;
        const float BossAhead = 115f;
        const float BossAbove = 55f;
        const float BossPad = 30f;
        const float CenterEase = 60f;
        const float BossLaneResponse = 4f;
        const float BossStartSpeed = 105f;
        const float BossTopSpeed = 294f;
        const float BossAccel = 182f;
        const float BossBrake = 210f;
        const float BossArrive = 1f;
        const float BossSpeedFactor = 1.125f;

        const int EscapeBlasts = 2;
        const float EscapeBlastGapSec = 0.7f;
        const float EscapeBlastScale = 0.7f;
        const float EscapeBoomVolume = 1f;
        const float EscapeSinkTop = 0.32f;
        const float EscapeSinkAccel = 0.12f;
        const float EscapeDriftRatio = 0.4f;
        const float EscapeSpinRate = 260f;

        const float EntryMargin = 120f;
        const float HoldFraction = 0.5f;
        const float SettleSec = 0.7f;

        const float CamShakeMagnitude = 7f;
        const float CamShakeDuration = 0.3f;
        const float ScrapeShakeCooldown = 1.2f;
        const float HitShakeMagnitude = 12f;
        const float HitShakeDuration = 0.45f;

        const float FailMaxSec = 3.5f;
        const float FailDrop = 140f;

        const float OutroExitMaxSec = 6f;
        const float OutroExitMargin = 120f;
        const float OutroFadeSec = 1.2f;

        const string Objective = "no way out  •  clear the sky";
        const string StageTwoSkipCaption = "SKIP TO STAGE 2";
        const string EndingSkipCaption = "SKIP TO ENDING";

        CampaignDefinition _level;
        int _levelNumber;

        Camera _cam;
        Vector3 _camPos;
        float _halfW;
        float _halfH;
        float _floorY;
        float _ceilingY;
        float _minX;
        float _maxX;

        CubeController _cube;
        Transform _cubeTr;
        PlaneShooter _shooter;
        PlaneBarrelRoll _roll;
        PlayerConfig _flight;
        float _cruise;
        float _hoverSpeed;

        GameObject _hud;
        LevelHud _hudView;
        HudCurtain _curtain;
        DialogueBar _dialogue;
        SoundSystem _sound;
        ArenaClouds _clouds;
        CampaignEnemies _enemies;
        CampaignScriptRunner _runner;
        RavenMoveset _moves;

        EnemyController _boss;
        BossHealthBar _bossBar;
        CinematicBars _entryBars;
        CinematicBars _rageBars;
        PlaneModelConfig _bossPlane;
        PlaneSkin _bossSkin;
        float _bossHealth;
        float _bossMax = 1f;
        bool _bossEntry;
        bool _bossReady;
        bool _bossGone;
        bool _finale;
        string _bossName;

        bool _entering = true;
        bool _gameOver;
        bool _outro;
        bool _failed;
        float _camShake;
        float _shakeSize = CamShakeMagnitude;
        float _shakeSec = CamShakeDuration;
        float _lastScrapeShake = -999f;

        public bool IsOver => _gameOver;

        public int EnemiesAlive => _enemies != null ? _enemies.AliveCount : 0;

        public int CountAlive(EnemyKind kind, PlaneModelConfig plane) =>
            kind == EnemyKind.Plane && _enemies != null ? _enemies.CountAlive(plane) : 0;

        public bool CompanionReady => true;

        public bool ZeppelinHanging => false;

        bool Cinematic => _entering || _outro || _bossEntry || CinematicBars.AnyShowing;

        public bool BossReady => _bossReady;

        public bool BossGone => _bossGone;

        string Subtitle => _level.title.ToLowerInvariant();

        public static ArenaLevelController Begin(GameObject owner, CampaignDefinition level,
            int levelNumber)
        {
            var arena = owner.AddComponent<ArenaLevelController>();
            arena._level = level;
            arena._levelNumber = levelNumber;
            arena.Build();
            return arena;
        }

        void Build()
        {
            CampaignCheckpoint.InCareer = true;

            var config = Resources.Load<PlayerConfig>("PlayerConfig");
            if (config == null) config = ScriptableObject.CreateInstance<PlayerConfig>();

            Firelight.Clear();
            NightLightingController.Shutdown();
            NightLights.PlaneZ = PlayPlaneZ;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, CameraDistance + 200f);

            SetupCamera();

            _clouds = ArenaClouds.Begin(_camPos, _halfW, _halfH, CameraDistance, PlayPlaneZ,
                CoastSky.CloudColor(_level.daytime), CoastSky.CloudGlow(_level.daytime),
                ScrollSpeed);
            SmokeTrail.Wind = Vector2.left * ScrollSpeed;

            _floorY = _clouds.FloorY + FloorSkim * _halfH;
            _ceilingY = _camPos.y + _halfH - TopMargin;
            _minX = _camPos.x - _halfW + SideMargin;
            _maxX = _camPos.x + _halfW - SideMargin;

            SpawnPlayer(config);
            _cubeTr.gameObject.SetActive(false);
            PlaneScrapes.DisablePlanePlaneCollisions();
            BuildHud();

            _enemies = new CampaignEnemies(_cube.GetComponent<Rigidbody>(),
                _floorY - EnemyFloorDrop, _camPos.y + _halfH, _level);
            _enemies.ModelScale = PlaneScale;

            if (HasBossMusic) MusicPlayer.Instance.Prewarm(_level.bossMusic);
            StartCoroutine(Load());
        }

        IEnumerator Load()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            Physics.SyncTransforms();

            _cubeTr.gameObject.SetActive(true);
            _sound = SoundSystem.Begin(_cube, null);
            _sound.Track(_enemies.Live, boss: true);
            StartCoroutine(FlyIn());

            ScreenFade.EndLoading();
        }

        void OnDestroy()
        {
            CampaignCheckpoint.InCareer = false;
            SmokeTrail.Wind = Vector2.zero;
            WithdrawSkips();
        }

        static float ScrollSpeed => DuelPlane.CruiseSpeed * CloudSpeedFactor;

        bool HasBossMusic => MusicPlayer.Instance != null && !string.IsNullOrEmpty(_level.bossMusic);

        static void StopBossMusic(float fadeSec)
        {
            if (MusicPlayer.Instance != null) MusicPlayer.Instance.FadeOutAndStop(fadeSec);
        }

        void SetupCamera()
        {
            _camPos = new Vector3(0f, EyeY, CamZ);
            _cam = Camera.main;

            if (_cam == null)
            {
                _halfH = CameraDistance * Mathf.Tan(LevelCamera.BaseFieldOfView * 0.5f * Mathf.Deg2Rad);
                _halfW = _halfH * LevelCamera.ReferenceAspect;
                return;
            }

            _cam.orthographic = false;
            _cam.transform.rotation = Quaternion.identity;
            CoastSky.Apply(_cam, _level.daytime, _level.weather);
            CoastSky.ApplyFog(_level.daytime, CameraDistance, PlayPlaneZ);
            _cam.farClipPlane = 2600f;

            LevelCamera.Frame(_cam, CameraDistance, out _halfW, out _halfH);
            CutsceneBlur.Focus(CameraDistance);
            _cam.transform.position = _camPos;
        }

        void SpawnPlayer(PlayerConfig config)
        {
            var go = new GameObject("PlayerPlane");
            go.transform.position = new Vector3(_camPos.x - _halfW - EntryMargin,
                (_floorY + _ceilingY) * 0.5f, PlayPlaneZ);

            var planeModel = _level.PlayerPlane;
            var model = PlaneFactory.BuildPlaneModel(go.transform, planeModel,
                skin: _level.PlayerSkin);
            model.localScale *= PlaneScale;

            PlayerConfig flight = PlaneLoadout.Build(config, planeModel);
            flight.rollCooldown = RollCooldown;
            _flight = flight;
            _cruise = flight.flySpeed;
            _hoverSpeed = _cruise * HoverSpeedFactor;

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnShotDown += OnShotDown;
            _cube.OnDamaged += OnPlayerDamaged;
            _cube.OnScraped += OnPlayerScraped;

            _cube.Initialize(flight, 0f, float.MinValue, float.MaxValue, _ceilingY, 0f,
                hardWalls: true);
            _cube.SetControlled(false);
            _cube.MountEngine(PlaneFactory.EngineLocal(go, model, planeModel));
            _cube.EnginePitch = planeModel.enginePitch;

            var muzzle = PlaneFactory.MountMuzzle(go, model, planeModel, out var flashPoint);
            var hitbox = go.GetComponentInChildren<Collider>();

            _shooter = go.AddComponent<PlaneShooter>();
            _shooter.Initialize(flight, muzzle, flashPoint, hitbox);
            _shooter.Stop();

            _roll = go.AddComponent<PlaneBarrelRoll>();
            _roll.Initialize(flight, _cube, model, RollSpinScale, streaks: false);
            _roll.Stop();
        }

        void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas("Arena HUD");
            _hud = canvas.gameObject;

            _hudView = new LevelHud(canvas.transform, Objective, _cube, _shooter, null, null, _roll,
                null, null, TryPause, arena: true);

            _curtain = HudCurtain.Attach(_hud);
            _curtain.Set(false);
        }

        IEnumerator FlyIn()
        {
            float holdX = _camPos.x - _halfW * HoldFraction;
            while (_cubeTr != null && !_gameOver && _cubeTr.position.x < holdX) yield return null;
            if (_cube == null || _gameOver) yield break;

            _cube.SetWalls(_minX, _maxX);
            _cube.BeginHover(_hoverSpeed, _floorY);

            for (float t = 0f; t < SettleSec; t += Time.deltaTime) yield return null;
            if (_gameOver) yield break;

            _entering = false;
            _cube.SetControlled(true);
            if (_shooter != null) _shooter.Resume();
            if (_roll != null) _roll.Resume();
            BeginScript();
        }

        void BeginScript()
        {
            if (string.IsNullOrEmpty(_level.arenaScript)) return;

            CampaignScript script = CampaignScript.Load(_level.arenaScript);
            if (script == null) return;

            _dialogue = new DialogueBar(_hud.transform);
            _runner = CampaignScriptRunner.Begin(gameObject, script, this, _dialogue);
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
            if (_gameOver || _cube == null || _bossEntry) return;
            PlaneScrapes.Check(_cube, _cubeTr, _enemies != null ? _enemies.Live : null, null,
                PlaneScrapes.HitboxRadius * _level.PlayerPlane.hitboxScale);
        }

        void LateUpdate()
        {
            if (_cubeTr == null) return;

            if (_camShake > 0f)
                _camShake = Mathf.Max(0f, _camShake - Time.unscaledDeltaTime / _shakeSec);
            PlaceCamera();

            if (_cube != null) _cube.SetCinematic(Cinematic);
            if (_curtain != null) _curtain.Set(!Cinematic);
            if (!_outro && _hudView != null) _hudView.Tick();
            if (_enemies != null) _enemies.SetWindow(_camPos.x, _halfW);

            if (_entryBars != null && _dialogue != null)
            {
                if (_finale) _entryBars.gameObject.SetActive(!(_dialogue.IsOpen && _dialogue.IsReady));
                else if (_dialogue.IsReady)
                {
                    Destroy(_entryBars.gameObject);
                    _entryBars = null;
                }
            }
            if (_bossBar != null) _bossBar.Set(_boss != null ? _boss.CurrentHealth : 0f, _bossMax);
        }

        void PlaceCamera()
        {
            if (_cam == null) return;

            Vector3 pos = _camPos;
            if (_camShake > 0f)
            {
                Vector2 j = Random.insideUnitCircle * (_shakeSize * _camShake);
                pos += new Vector3(j.x, j.y, 0f);
            }
            _cam.transform.position = pos;
        }

        public void SpawnWave(EnemyGroup[] groups, bool spread = false)
        {
            if (_gameOver || groups == null || _enemies == null) return;

            if (spread) _enemies.SpawnSpread(groups, _camPos, _halfW, _halfH);
            else _enemies.Spawn(groups, _camPos.x, _halfW);
        }

        public float WarnIncoming(int planes)
        {
            if (_gameOver || _hud == null || planes <= 0) return 0f;

            EnemyWarning warning = EnemyWarning.Show(_hud.transform, EnemyWarning.TextFor(planes));
            if (_curtain != null) _curtain.Adopt(warning.gameObject);
            return EnemyWarning.Seconds;
        }

        public float WarnDanger(ScreenEdge edge, float seconds)
        {
            if (_gameOver || _hud == null) return 0f;

            DangerSign sign = DangerSign.Show(_hud.transform, edge, seconds, () => DangerAlong(edge));
            if (_curtain != null) _curtain.Adopt(sign.gameObject);
            return sign.Seconds;
        }

        float DangerAlong(ScreenEdge edge)
        {
            Vector2 target = BossTarget();
            return edge == ScreenEdge.Left || edge == ScreenEdge.Right
                ? (target.y - (_camPos.y - _halfH)) / (2f * _halfH)
                : (target.x - (_camPos.x - _halfW)) / (2f * _halfW);
        }

        public void ArmSupply(int crates) { }

        public void SetCompanionFoe(PlaneModelConfig plane) { }

        public bool SpawnZeppelin() => false;

        public float WarnZeppelin() => 0f;

        public void GatherFormation() { }

        public bool FormationReady => true;

        public void EnterArena() { }

        public void EnterBoss(PlaneModelConfig plane, string skin, float health, string name,
            bool burning)
        {
            if (_gameOver || _enemies == null || _cube == null || plane == null || _boss != null)
            {
                _bossReady = true;
                return;
            }

            _bossEntry = true;
            _bossReady = false;
            _finale = burning;
            _bossName = name;
            _cube.SetControlled(false);
            if (_shooter != null) _shooter.Stop();
            if (_roll != null) _roll.Stop();
            StartCoroutine(CenterPlayer());

            _entryBars = CinematicBars.Create(_hud.transform);
            _entryBars.Raise();

            _bossPlane = plane;
            _bossSkin = PlaneSkins.ById(plane, skin);
            _bossHealth = health;

            var start = new Vector2(_camPos.x - _halfW - BossEntryMargin, BossTarget().y);
            _boss = SpawnBoss(start, 0f);
            if (_boss == null)
            {
                _bossReady = true;
                return;
            }

            _bossMax = _boss.MaxHealth;
            _boss.HideHealthBar();
            if (burning)
            {
                _boss.EnginePitch = RavenMoveset.RagePitch;
                _boss.Blaze();
            }
            StartCoroutine(FlyBossIn(start));
        }

        EnemyController SpawnBoss(Vector2 at, float heading)
        {
            EnemyController boss = _enemies.SpawnBoss(_bossPlane, _bossSkin, _bossHealth, at,
                _camPos.x, _halfW, _cruise * BossSpeedFactor);
            if (boss == null) return null;

            boss.BeginScript();
            boss.ScriptTo(at, heading);
            return boss;
        }

        EnemyController RespawnBoss(Vector2 at, float heading)
        {
            if (_gameOver || _enemies == null || _boss == null) return _boss;

            EnemyController old = _boss;
            EnemyController fresh = SpawnBoss(at, heading);
            if (fresh == null) return old;

            fresh.Restore(heading, old.CurrentHealth);
            fresh.HealthFloor = old.HealthFloor;
            fresh.EnginePitch = old.EnginePitch;
            if (old.Blazing) fresh.Blaze();
            fresh.HideHealthBar();
            _enemies.Dismiss(old);
            _boss = fresh;
            return fresh;
        }

        Vector2 PlayerMark => new Vector2(_camPos.x, Mathf.Clamp(_camPos.y, _floorY, _ceilingY));

        Vector2 BossTarget()
        {
            Vector2 mark = PlayerMark;
            float x = Mathf.Min(mark.x + BossAhead, _maxX);
            float y = mark.y + BossAbove;
            if (y > _ceilingY - BossPad) y = Mathf.Max(_floorY + BossPad, mark.y - BossAbove);
            return new Vector2(x, y);
        }

        IEnumerator CenterPlayer()
        {
            while (_bossEntry && !_gameOver && _cube != null && _cubeTr != null)
            {
                Vector2 to = PlayerMark - (Vector2)_cubeTr.position;
                _cube.SetHoverInput(Vector2.ClampMagnitude(to / CenterEase, 1f));
                yield return null;
            }

            if (_cube != null && !_outro) _cube.SetHoverInput(Vector2.zero);
        }

        IEnumerator FlyBossIn(Vector2 start)
        {
            Vector2 pos = start;
            float speed = BossStartSpeed;
            if (_boss != null) _boss.Throttling = true;

            while (_boss != null && !_gameOver)
            {
                float dt = Time.deltaTime;
                Vector2 target = BossTarget();
                float left = target.x - pos.x;
                if (left <= BossArrive) break;

                float cap = Mathf.Min(BossTopSpeed, Mathf.Sqrt(2f * BossBrake * left));
                speed = Mathf.MoveTowards(speed, cap, (cap > speed ? BossAccel : BossBrake) * dt);
                pos.x = Mathf.Min(target.x, pos.x + speed * dt);
                pos.y += (target.y - pos.y) * (1f - Mathf.Exp(-BossLaneResponse * dt));

                _boss.ScriptTo(pos, 0f);
                yield return null;
            }

            if (_boss != null) _boss.Throttling = false;
            _bossReady = true;
        }

        public void BeginDuel()
        {
            if (_gameOver) return;

            _bossEntry = false;
            if (_entryBars != null)
            {
                Destroy(_entryBars.gameObject);
                _entryBars = null;
            }

            if (_cube != null) _cube.SetControlled(true);
            if (_shooter != null) _shooter.Resume();
            if (_roll != null) _roll.Resume();
            if (_boss == null) return;

            _bossBar = new BossHealthBar(_hud.transform, _hudView.BossSlot, _bossName);
            _hudView.ShowBossSlot();
            _bossBar.Set(_boss.CurrentHealth, _bossMax);
            if (HasBossMusic) MusicPlayer.Instance.Play(_level.bossMusic);

            _moves = RavenMoveset.Begin(gameObject, _boss, Stage());
            if (_moves != null) DevSkip.Offer(StageTwoSkipCaption, SkipToStageTwo);
        }

        void SkipToStageTwo()
        {
            if (_gameOver || _moves == null) return;
            _moves.SkipToRage();
        }

        void SkipToEnding()
        {
            if (_gameOver || _moves == null) return;
            _moves.SkipToEnd();
        }

        void WithdrawSkips()
        {
            DevSkip.Withdraw(SkipToStageTwo);
            DevSkip.Withdraw(SkipToEnding);
        }

        void BossBeaten()
        {
            WithdrawSkips();
            if (_bossBar != null) _bossBar.Remove();
            _bossBar = null;
        }

        public void BeginEscape()
        {
            _bossGone = _gameOver || _boss == null;
            if (!_bossGone) StartCoroutine(Escape());
        }

        bool Escaping => _boss != null && !_gameOver;

        IEnumerator Escape()
        {
            _boss.Featured = true;

            for (int i = 0; i < EscapeBlasts; i++)
            {
                for (float t = 0f; t < EscapeBlastGapSec && Escaping; t += Time.deltaTime)
                    yield return null;
                if (!Escaping) break;

                Explosion.Spawn(_boss.NosePoint, _boss.ModelSize * EscapeBlastScale,
                    EscapeBoomVolume * AudioOptions.Sfx);
                Shake(HitShakeMagnitude, HitShakeDuration);
            }

            Vector2 pos = _boss != null ? (Vector2)_boss.transform.position : Vector2.zero;
            float hideY = _camPos.y - _halfH - (_boss != null ? _boss.ModelSize : 0f);
            float sink = 0f;
            float spin = 0f;

            while (Escaping && pos.y > hideY)
            {
                float dt = Time.deltaTime;
                sink = Mathf.MoveTowards(sink, EscapeSinkTop * _halfH, EscapeSinkAccel * _halfH * dt);
                float drift = sink * EscapeDriftRatio;
                pos += new Vector2(-drift, -sink) * dt;
                spin += EscapeSpinRate * dt;

                _boss.Spin = spin;
                _boss.ScriptTo(pos, Mathf.Atan2(-sink, ScrollSpeed - drift));
                yield return null;
            }

            if (_boss != null) _enemies.Dismiss(_boss);
            _boss = null;
            _bossGone = true;
        }

        void BossEnraged()
        {
            DevSkip.Withdraw(SkipToStageTwo);
            DevSkip.Offer(EndingSkipCaption, SkipToEnding);
            if (_flight == null) return;

            _flight.rollCooldown = RollCooldown * StageTwoRollFactor;
            if (_roll != null) _roll.Cooldown = Mathf.Min(_roll.Cooldown, _flight.rollCooldown);
        }

        BossStage Stage() => new BossStage
        {
            camPos = _camPos,
            halfW = _halfW,
            halfH = _halfH,
            floorY = _floorY,
            ceilingY = _ceilingY,
            playerSpeed = _hoverSpeed,
            player = _cubeTr,
            hud = _hud.transform,
            curtain = _curtain,
            sound = _sound,
            enemies = _enemies,
            escort = PlaneModels.Albatros,
            scrollSpeed = ScrollSpeed,
            over = () => _gameOver,
            respawn = RespawnBoss,
            pin = PinLeftWall,
            ahead = BossTarget,
            cinematic = BossCinematic,
            roof = PinCeiling,
            enraged = BossEnraged,
            beaten = BossBeaten,
            shake = () => Shake(HitShakeMagnitude, HitShakeDuration),
        };

        void BossCinematic(bool on)
        {
            if (on)
            {
                if (_gameOver || _cube == null || _bossEntry) return;

                _bossEntry = true;
                _cube.SetControlled(false);
                if (_shooter != null) _shooter.Stop();
                if (_roll != null) _roll.Stop();
                StartCoroutine(CenterPlayer());

                _rageBars = CinematicBars.Create(_hud.transform);
                _rageBars.Raise();
                return;
            }

            _bossEntry = false;
            if (_rageBars != null)
            {
                _rageBars.Lower();
                Destroy(_rageBars.gameObject, CinematicBars.SlideSec);
                _rageBars = null;
            }
            if (_gameOver || _cube == null) return;

            _cube.SetControlled(true);
            if (_shooter != null) _shooter.Resume();
            if (_roll != null) _roll.Resume();
        }

        void PinLeftWall(float x)
        {
            if (_cube != null) _cube.SetWalls(Mathf.Max(_minX, x), _maxX);
        }

        void PinCeiling(float y)
        {
            if (_cube != null) _cube.SetCeiling(Mathf.Min(_ceilingY, y));
        }

        public void Checkpoint(int step, bool warnedFirst, bool warnedPair) { }

        public void CompleteLevel()
        {
            if (_gameOver) return;
            _gameOver = true;
            _outro = true;

            if (_shooter != null) _shooter.Stop();
            if (_roll != null) _roll.Stop();
            if (_moves != null) _moves.Stop();
            if (_enemies != null) _enemies.StandDown();
            if (_dialogue != null) _dialogue.Hide();
            WithdrawSkips();
            if (_cube != null)
            {
                _cube.ClearWalls();
                _cube.SetControlled(false);
                _cube.SetHoverInput(_finale ? Vector2.zero : Vector2.right);
            }

            CampaignProgress.Complete(_levelNumber);
            CampaignCheckpoint.Clear();

            StartCoroutine(FlyOut());
        }

        IEnumerator FlyOut()
        {
            float left = _finale ? 0f : OutroExitMaxSec;
            while (left > 0f && !PlaneGone)
            {
                left -= Time.deltaTime;
                yield return null;
            }

            if (_sound != null) _sound.FadeOut(OutroFadeSec);
            StopBossMusic(OutroFadeSec);
            ScreenFade.Swap(ShowGroundScene, OutroFadeSec);
        }

        bool PlaneGone => _cubeTr == null
            || _cubeTr.position.x > _camPos.x + _halfW + OutroExitMargin;

        void ShowGroundScene()
        {
            SmokeTrail.Wind = Vector2.zero;
            if (_cube != null) _cube.Stop();
            if (_entryBars != null) Destroy(_entryBars.gameObject);
            _entryBars = null;
            CampaignFinale.Play(_level, _levelNumber, _hud, Subtitle);
        }

        void StopScript()
        {
            WithdrawSkips();
            if (_runner != null) _runner.Stop();
            if (_moves != null) _moves.Stop();
            if (_enemies != null) _enemies.StandDown();
        }

        void OnShotDown()
        {
            if (_gameOver) return;
            _gameOver = true;

            StopScript();
            if (_shooter != null) _shooter.Stop();
            if (_roll != null) _roll.Stop();
            if (_sound != null) _sound.EnterGameOver();
            StopBossMusic(MusicPlayer.FadeOutSec);

            StartCoroutine(FailWhenGone());
        }

        IEnumerator FailWhenGone()
        {
            float left = FailMaxSec;
            while (left > 0f && _cubeTr != null && _cubeTr.position.y > _floorY - FailDrop)
            {
                left -= Time.deltaTime;
                yield return null;
            }

            Fail();
        }

        void OnCrashed()
        {
            bool first = !_gameOver;
            _gameOver = true;

            StopScript();
            if (_cube != null) _cube.Stop();
            if (_shooter != null) _shooter.Stop();
            if (_roll != null) _roll.Stop();
            if (first && _sound != null) _sound.EnterGameOver();
            if (first) StopBossMusic(MusicPlayer.FadeOutSec);

            StartCoroutine(FailAfter(Explosion.Duration));
        }

        IEnumerator FailAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            Fail();
        }

        void Fail()
        {
            if (_failed || _outro) return;
            _failed = true;
            GameMenu.Open(GameMenuKind.Failed, Subtitle, _hud);
        }

        void OnPlayerDamaged()
        {
            if (_sound != null) _sound.ReportPlayerDamaged();
        }

        void OnPlayerScraped()
        {
            if (Time.time - _lastScrapeShake < ScrapeShakeCooldown) return;
            _lastScrapeShake = Time.time;
            Shake(CamShakeMagnitude, CamShakeDuration);
        }

        void Shake(float size, float seconds)
        {
            if (_shakeSize * _camShake > size) return;

            _camShake = 1f;
            _shakeSize = size;
            _shakeSec = seconds;
        }
    }
}
