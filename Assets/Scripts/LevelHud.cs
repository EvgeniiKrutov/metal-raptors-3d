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

        const string ArenaKeyHint = "W A S D to fly  •  F to fire  •  B to roll  •  ";

        const float BossGap = 0.35f;

        readonly CubeController _plane;
        readonly PlaneShooter _shooter;
        readonly PlaneBomber _bomber;
        readonly PlaneBoost _boost;
        readonly PlaneBarrelRoll _roll;
        readonly PlaneSearchlight _searchlight;

        readonly HealthBar _health;
        readonly HealthBar _airfieldBar;
        readonly Airfield _airfield;
        readonly CooldownSquare _bombSquare;
        readonly CooldownSquare _boostSquare;
        readonly CooldownSquare _rollSquare;
        readonly CooldownSquare _fireSquare;
        readonly CooldownSquare _lightSquare;
        readonly List<CooldownSquare> _column = new List<CooldownSquare>();
        readonly List<PlaneAbility> _needs = new List<PlaneAbility>();
        readonly float _columnX;
        readonly float _columnTop;
        readonly float _bossDrop;
        bool _bossShown;

        readonly Text _hint;
        readonly string _objective;
        PlaneAbility _abilities;

        readonly TouchStick _stick;
        readonly HeadingArrow _arrow;
        readonly bool _arena;
        Camera _cam;

        public Vector2 BossSlot { get; }

        public LevelHud(Transform parent, string objective, CubeController plane,
            PlaneShooter shooter, PlaneBomber bomber, PlaneBoost boost, PlaneBarrelRoll roll,
            PlaneSearchlight searchlight, Airfield airfield, System.Action onPause,
            bool arena = false, PlaneAbility abilities = PlaneAbility.All)
        {
            _plane = plane;
            _arena = arena;
            _abilities = abilities;
            _objective = objective;
            _airfield = airfield;
            _shooter = shooter;
            _bomber = bomber;
            _boost = boost;
            _roll = roll;
            _searchlight = searchlight;

            float x = HudTheme.ColumnLeft;
            float y = HudTheme.ColumnTop;

            _health = new HealthBar(parent, new Vector2(x, -y));
            y += HudTheme.BarHeight + HudTheme.BarToColumn;

            if (arena)
            {
                float above = y;
                y += HudTheme.BarHeight * BossGap;
                BossSlot = new Vector2(x, -y);
                y += HudTheme.BarHeight + HudTheme.BarToColumn;
                _bossDrop = y - above;
            }

            if (_airfield != null)
            {
                _airfieldBar = new HealthBar(parent, new Vector2(x, -y), AirfieldCaption);
                y += HudTheme.BarHeight + HudTheme.BarToColumn;
            }

            _columnX = x;
            _columnTop = y;

            if (!arena)
            {
                _bombSquare = Square(parent, HudTheme.Label("H", "BOMB"), RequestBomb,
                    PlaneAbility.Bomb);
                _boostSquare = Square(parent, HudTheme.Label("R", "BOOST"), RequestBoost,
                    PlaneAbility.Boost);
            }
            if (!arena || _roll != null)
                _rollSquare = Square(parent, HudTheme.Label("B", "ROLL"), RequestRoll,
                    PlaneAbility.Roll);
            if (HudTheme.IsTouch)
                _fireSquare = Square(parent, "FIRE", null, PlaneAbility.None, holdable: true);
            if (_searchlight != null)
                _lightSquare = Square(parent, HudTheme.Label("T", "LIGHT"), ToggleLight,
                    PlaneAbility.None);

            Layout();

            if (HudTheme.IsTouch && _plane != null)
            {
                if (!arena) _plane.EnableHeadingSteering();
                _stick = TouchStick.Create(parent);
                if (!arena) _arrow = new HeadingArrow(parent);
            }

            if (HudTheme.IsTouch && onPause != null)
                new CooldownSquare(parent, new Vector2(-HudTheme.ColumnRight, -HudTheme.ColumnTop),
                    "P", onPause, fromRight: true);

            _hint = UIFactory.CreateText(parent, HintText(), HudTheme.HintSize, Vector2.zero,
                Vector2.zero);
            var rt = _hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-2f * HudTheme.HintSideInset, HudTheme.HintRowHeight);
            rt.anchoredPosition = new Vector2(0f, HudTheme.HintBottom);

            Tick();
        }

        CooldownSquare Square(Transform parent, string label, System.Action onPress,
            PlaneAbility needs, bool holdable = false)
        {
            var square = new CooldownSquare(parent, Vector2.zero, label, onPress, holdable);
            square.Root.SetActive(Has(needs));
            _column.Add(square);
            _needs.Add(needs);
            return square;
        }

        bool Has(PlaneAbility needs) => (_abilities & needs) == needs;

        public void ShowBossSlot()
        {
            if (_bossShown) return;

            _bossShown = true;
            Layout();
        }

        void Layout()
        {
            float y = _columnTop - (_bossShown ? 0f : _bossDrop);
            for (int i = 0; i < _column.Count; i++)
            {
                if (!Has(_needs[i])) continue;

                _column[i].Place(new Vector2(_columnX, -y));
                y += HudTheme.SquarePitch;
            }
        }

        string HintText()
        {
            if (HudTheme.IsTouch) return _objective;
            if (_arena) return ArenaKeyHint + _objective;

            string keys = KeyHint;
            if (Has(PlaneAbility.Bomb)) keys += BombHint;
            if (Has(PlaneAbility.Boost)) keys += BoostHint;
            if (Has(PlaneAbility.Roll)) keys += RollHint;
            return keys + _objective;
        }

        public void Unlock(PlaneAbility ability, HudCurtain curtain)
        {
            PlaneAbility fresh = ability & ~_abilities;
            if (fresh == PlaneAbility.None) return;

            _abilities |= fresh;

            for (int i = 0; i < _column.Count; i++)
            {
                if ((_needs[i] & fresh) == PlaneAbility.None) continue;

                if (curtain != null) curtain.Reveal(_column[i].Root);
                else _column[i].Root.SetActive(true);
            }

            Layout();
            _hint.text = HintText();
        }

        public void Tick()
        {
            if (_bombSquare != null && _bomber != null)
                _bombSquare.Set(_bomber.Charge, _bomber.IsReady);
            if (_boostSquare != null && _boost != null)
                _boostSquare.Set(_boost.Charge, _boost.IsReady || _boost.IsRunning);
            if (_rollSquare != null && _roll != null)
                _rollSquare.Set(_roll.Charge, _roll.IsReady || _roll.IsRunning);
            if (_fireSquare != null && _shooter != null)
            {
                _fireSquare.Set(0f, _shooter.IsReady);
                _shooter.SetHeld(_fireSquare.Held);
            }
            if (_lightSquare != null && _searchlight != null)
                _lightSquare.Set(0f, _searchlight.IsOn);
            if (_health != null && _plane != null)
                _health.Set(_plane.CurrentHealth, _plane.MaxHealth);

            if (_airfieldBar != null && _airfield != null)
                _airfieldBar.Set(_airfield.CurrentHealth, Airfield.MaxHealth);

            TickSteering();
        }

        void TickSteering()
        {
            if (_stick == null || _plane == null) return;

            bool steerable = _plane.Steerable && !GameMenu.IsOpen;
            _stick.SetVisible(steerable);

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

        void ToggleLight()
        {
            if (_searchlight != null) _searchlight.Toggle();
        }
    }
}
