using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class LevelHud
    {
        const string AirfieldCaption = "AIRFIELD";

        const string KeyHint = "A / D to steer  •  F to fire  •  ";
        const string BombHint = "H to bomb  •  ";
        const string BoostHint = "R to boost  •  ";
        const string RollHint = "B to roll  •  ";
        const string LightHint = "T for light  •  ";

        const string ArenaKeyHint = "W A S D to fly  •  F to fire  •  B to roll  •  ";

        const string GunnerKeyHint = "W / S to aim  •  F to fire  •  ";
        const string DiveHint = "R to dive  •  ";
        const string JamHint = "GUN JAMMED  •  TAP F ×";
        const string JamTouchHint = "GUN JAMMED  •  TAP FIRE ×";
        const int JamSizeBoost = 6;
        const float JamAnchorY = 0.3f;
        static readonly Color JamColor = new Color(1f, 0.42f, 0.28f, 1f);
        const string RepairHint = "PLANE DAMAGED  •  TAP H ×";
        const string RepairTouchHint = "PLANE DAMAGED  •  TAP FIX ×";
        static readonly Color RepairColor = new Color(0.42f, 0.95f, 0.5f, 1f);

        readonly CubeController _plane;
        readonly PlaneShooter _shooter;
        readonly PlaneBomber _bomber;
        readonly PlaneBoost _boost;
        readonly PlaneBarrelRoll _roll;
        readonly PlaneSearchlight _searchlight;
        readonly RearGun _gun;
        readonly GunnerAutopilot _pilot;

        readonly HealthDial _health;
        readonly SpeedDial _speed;
        readonly HealthBar _airfieldBar;
        readonly Airfield _airfield;
        readonly SkillDial _bombDial;
        readonly SkillDial _boostDial;
        readonly SkillDial _rollDial;
        readonly SkillDial _diveDial;
        readonly SkillDial _fireDial;
        readonly SkillDial _fixDial;
        readonly SkillDial _lightDial;
        readonly List<SkillDial> _cluster = new List<SkillDial>();
        readonly List<PlaneAbility> _needs = new List<PlaneAbility>();

        readonly Text _hint;
        readonly Text _jam;
        int _jamShown = -1;
        readonly Text _repair;
        int _repairShown = -1;
        SupplyDrop _supply;
        readonly string _objective;
        PlaneAbility _abilities;

        readonly TouchStick _stick;
        readonly HeadingArrow _arrow;
        readonly CrateArrow _crateArrow;
        readonly bool _arena;
        bool _disarmed;
        Camera _cam;

        public LevelHud(Transform parent, string objective, CubeController plane,
            PlaneShooter shooter, PlaneBomber bomber, PlaneBoost boost, PlaneBarrelRoll roll,
            PlaneSearchlight searchlight, Airfield airfield, System.Action onPause,
            bool arena = false, PlaneAbility abilities = PlaneAbility.All, RearGun gun = null,
            GunnerAutopilot pilot = null, bool speedDial = false)
        {
            _plane = plane;
            _gun = gun;
            _pilot = pilot;
            _arena = arena;
            _abilities = abilities;
            _objective = objective;
            _airfield = airfield;
            _shooter = shooter;
            _bomber = bomber;
            _boost = boost;
            _roll = roll;
            _searchlight = searchlight;

            _health = new HealthDial(parent, HudTheme.DialCentre);

            if (speedDial && !HudTheme.IsTouch && plane != null)
                _speed = new SpeedDial(parent, HudTheme.SpeedDialCentre, plane.MinSpeedFraction,
                    PlaneEnergy.StallFraction, plane.DiveSpeedFraction, plane.TopSpeedFraction);

            if (_airfield != null)
                _airfieldBar = new HealthBar(parent, AirfieldCaption, HudTheme.Fill);

            if (!arena)
            {
                _bombDial = Skill(parent, HudIcon.Bomb, RequestBomb, PlaneAbility.Bomb);
                _boostDial = Skill(parent, HudIcon.Boost, RequestBoost, PlaneAbility.Boost);
            }
            if (pilot != null)
                _diveDial = Skill(parent, HudIcon.Dive, RequestDive, PlaneAbility.Dive);
            if (!arena || _roll != null)
                _rollDial = Skill(parent, HudIcon.Roll, RequestRoll, PlaneAbility.Roll);
            if (HudTheme.IsTouch && gun != null)
                _fixDial = Skill(parent, HudIcon.Fix, RequestFix, PlaneAbility.None);
            if (_searchlight != null)
                _lightDial = Skill(parent, HudIcon.Light, ToggleLight, PlaneAbility.None);

            if (HudTheme.IsTouch)
            {
                _fireDial = new SkillDial(parent, HudTheme.SkillAnchor, HudIcon.Fire,
                    HudTheme.FireSize, null, holdable: true);
                _fireDial.Place(HudTheme.FireCentre);
            }

            Layout();

            if (HudTheme.IsTouch && _plane != null)
            {
                bool flying = !arena && gun == null;
                if (flying) _plane.EnableHeadingSteering();
                _stick = TouchStick.Create(parent);
                if (flying) _arrow = new HeadingArrow(parent);
            }

            if (HudTheme.IsTouch && onPause != null)
            {
                float half = HudTheme.SkillSize * 0.5f;
                var pause = new SkillDial(parent, Vector2.one, HudIcon.Pause, HudTheme.SkillSize,
                    onPause);
                pause.Place(new Vector2(-(HudTheme.ColumnRight + half),
                    -(HudTheme.ColumnTop + half)));
            }

            _hint = UIFactory.CreateText(parent, HintText(), HudTheme.HintSize, Vector2.zero,
                Vector2.zero);
            var rt = _hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-2f * HudTheme.HintSideInset, HudTheme.HintRowHeight);
            rt.anchoredPosition = new Vector2(0f, HudTheme.HintBottom);

            if (gun != null)
            {
                _jam = AlertLabel(parent, JamColor, 0f);
                _repair = AlertLabel(parent, RepairColor, HudTheme.HintRowHeight);
            }

            if (_plane != null) _crateArrow = new CrateArrow(parent);

            Tick();
        }

        public void Track(SupplyDrop supply) => _supply = supply;

        static Text AlertLabel(Transform parent, Color color, float lift)
        {
            int size = HudTheme.HintSize + JamSizeBoost;
            Text label = UIFactory.CreateText(parent, string.Empty, size, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            label.color = color;

            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0f, JamAnchorY);
            rt.anchorMax = new Vector2(1f, JamAnchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(-2f * HudTheme.HintSideInset, HudTheme.HintRowHeight);
            rt.anchoredPosition = new Vector2(0f, lift);

            label.gameObject.SetActive(false);
            return label;
        }

        SkillDial Skill(Transform parent, HudIcon icon, System.Action onPress, PlaneAbility needs)
        {
            var dial = new SkillDial(parent, HudTheme.SkillAnchor, icon, HudTheme.SkillSize,
                onPress);
            dial.Root.SetActive(Has(needs));
            _cluster.Add(dial);
            _needs.Add(needs);
            return dial;
        }

        bool Has(PlaneAbility needs) => (_abilities & needs) == needs;

        void Layout()
        {
            int shown = 0;
            for (int i = 0; i < _cluster.Count; i++)
                if (Has(_needs[i])) shown++;

            Vector2 hub = HudTheme.SkillHub;
            float orbit = HudTheme.SkillOrbit;
            float step = HudTheme.SkillStep;
            float turn = HudTheme.SkillTurn;

            int slot = 0;
            for (int i = 0; i < _cluster.Count; i++)
            {
                if (!Has(_needs[i])) continue;

                float angle = (HudTheme.SkillMid + (slot - (shown - 1) * 0.5f) * step)
                              * Mathf.Deg2Rad;
                _cluster[i].Place(hub
                    + new Vector2(Mathf.Cos(angle), turn * Mathf.Sin(angle)) * orbit);
                slot++;
            }
        }

        string HintText()
        {
            if (HudTheme.IsTouch) return _objective;
            if (_gun != null)
            {
                string dive = Has(PlaneAbility.Dive) ? DiveHint : string.Empty;
                return GunnerKeyHint + dive + _objective;
            }
            if (_arena) return ArenaKeyHint + _objective;

            string keys = KeyHint;
            if (Has(PlaneAbility.Bomb)) keys += BombHint;
            if (Has(PlaneAbility.Boost)) keys += BoostHint;
            if (Has(PlaneAbility.Roll)) keys += RollHint;
            if (_searchlight != null) keys += LightHint;
            return keys + _objective;
        }

        public void Unlock(PlaneAbility ability, HudCurtain curtain)
        {
            PlaneAbility fresh = ability & ~_abilities;
            if (fresh == PlaneAbility.None) return;

            _abilities |= fresh;

            for (int i = 0; i < _cluster.Count; i++)
            {
                if ((_needs[i] & fresh) == PlaneAbility.None) continue;

                if (curtain != null) curtain.Reveal(_cluster[i].Root);
                else _cluster[i].Root.SetActive(true);
            }

            Layout();
            _hint.text = HintText();
        }

        public void Disarm(HudCurtain curtain)
        {
            _disarmed = true;

            foreach (SkillDial dial in new[] { _diveDial, _fireDial })
            {
                if (dial == null) continue;

                if (curtain != null) curtain.Withdraw(dial.Root);
                else dial.Root.SetActive(false);
            }
        }

        public void Tick()
        {
            if (_bombDial != null && _bomber != null)
                _bombDial.Set(_bomber.Charge, _bomber.IsReady);
            if (_boostDial != null && _boost != null)
                _boostDial.Set(_boost.Charge, _boost.IsReady || _boost.IsRunning);
            if (_rollDial != null && _roll != null)
                _rollDial.Set(_roll.Charge, _roll.IsReady || _roll.IsRunning);
            if (_diveDial != null && _pilot != null)
                _diveDial.Set(_pilot.Charge, _pilot.IsReady || _pilot.IsRunning);
            if (_fireDial != null && _shooter != null)
            {
                _fireDial.Set(0f, _shooter.IsReady);
                _shooter.SetHeld(_fireDial.Held);
            }
            if (_lightDial != null && _searchlight != null)
                _lightDial.Set(0f, _searchlight.IsOn);
            if (_fixDial != null) _fixDial.Set(0f, true);
            if (_health != null && _plane != null)
                _health.Set(_plane.CurrentHealth, _plane.MaxHealth);
            if (_speed != null && _plane != null)
                _speed.Set(_plane.SpeedFraction, _plane.Boosting);

            if (_airfieldBar != null && _airfield != null)
                _airfieldBar.Set(_airfield.CurrentHealth, Airfield.MaxHealth);

            TickJam();
            TickRepair();
            TickSteering();
            TickCrate();
        }

        void TickCrate()
        {
            if (_crateArrow == null) return;

            Vector3 crate = Vector3.zero;
            bool visible = _supply != null && _plane != null && _plane.CurrentHealth > 0f
                           && !CinematicBars.AnyShowing && !GameMenu.IsOpen && _supply.Aim(out crate);
            if (_cam == null) _cam = Camera.main;
            _crateArrow.Tick(_cam, _plane != null ? _plane.transform.position : Vector3.zero, crate, visible);
        }

        void TickRepair()
        {
            if (_repair == null) return;

            int taps = _supply != null && _supply.Repairing && _gun.Ready ? _supply.RepairLeft : 0;
            if (_fixDial != null && _fixDial.Root.activeSelf != taps > 0)
                _fixDial.Root.SetActive(taps > 0);
            if (taps == _repairShown) return;

            _repairShown = taps;
            _repair.gameObject.SetActive(taps > 0);
            if (taps > 0) _repair.text = (HudTheme.IsTouch ? RepairTouchHint : RepairHint) + taps;
        }

        void TickJam()
        {
            if (_jam == null || _shooter == null) return;

            int taps = _shooter.Jammed && _gun.Ready ? _shooter.TapsLeft : 0;
            if (taps == _jamShown) return;

            _jamShown = taps;
            _jam.gameObject.SetActive(taps > 0);
            if (taps > 0) _jam.text = (HudTheme.IsTouch ? JamTouchHint : JamHint) + taps;
        }

        void TickSteering()
        {
            if (_stick == null || _plane == null) return;

            bool steerable = _plane.Steerable && !GameMenu.IsOpen && !DevFreeze.Active && !_disarmed;
            _stick.SetVisible(steerable);

            if (_gun != null)
            {
                _gun.SetAimInput(steerable ? _stick.Pull.y : 0f);
                return;
            }

            if (_arena)
            {
                _plane.SetHoverInput(steerable && _stick.Steering
                    ? new Vector2(Mathf.Cos(_stick.Angle), Mathf.Sin(_stick.Angle))
                    : Vector2.zero);
                return;
            }

            if (!steerable) _plane.SetTargetHeading(_plane.Heading);
            else if (_stick.Steering) _plane.SetTargetHeading(_stick.Angle);

            if (_cam == null) _cam = Camera.main;
            _arrow.Tick(_cam, _plane.transform.position, _plane.TargetHeading, steerable);
        }

        void RequestBomb()
        {
            if (_bomber != null) _bomber.Request();
        }

        void RequestBoost()
        {
            if (_boost != null) _boost.Request();
        }

        void RequestRoll()
        {
            if (_roll != null) _roll.Request();
        }

        void RequestDive()
        {
            if (_pilot != null) _pilot.Request();
        }

        void RequestFix()
        {
            if (_supply != null) _supply.Tap();
        }

        void ToggleLight()
        {
            if (_searchlight != null) _searchlight.Toggle();
        }
    }
}
