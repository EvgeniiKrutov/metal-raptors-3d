using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class LevelHud
    {
        const string KeyHint = "A / D to steer  •  F to fire  •  H to bomb  •  R to boost  •  ";

        readonly CubeController _plane;
        readonly PlaneShooter _shooter;
        readonly PlaneBomber _bomber;
        readonly PlaneBoost _boost;
        readonly PlaneSearchlight _searchlight;

        readonly HealthBar _health;
        readonly CooldownSquare _bombSquare;
        readonly CooldownSquare _boostSquare;
        readonly CooldownSquare _fireSquare;
        readonly CooldownSquare _lightSquare;

        public Vector2 TaskCorner { get; }

        public LevelHud(Transform parent, string objective, CubeController plane,
            PlaneShooter shooter, PlaneBomber bomber, PlaneBoost boost, PlaneSearchlight searchlight,
            System.Action onPause)
        {
            _plane = plane;
            _shooter = shooter;
            _bomber = bomber;
            _boost = boost;
            _searchlight = searchlight;

            float x = HudTheme.ColumnLeft;
            float y = HudTheme.ColumnTop;

            _health = new HealthBar(parent, new Vector2(x, -y));
            y += HudTheme.BarHeight + HudTheme.BarToColumn;

            _bombSquare = Square(parent, x, ref y, HudTheme.Label("H", "BOMB"), RequestBomb);
            _boostSquare = Square(parent, x, ref y, HudTheme.Label("R", "BOOST"), RequestBoost);
            if (HudTheme.IsTouch)
                _fireSquare = Square(parent, x, ref y, "FIRE", null, holdable: true);
            if (_searchlight != null)
                _lightSquare = Square(parent, x, ref y, HudTheme.Label("T", "LIGHT"), ToggleLight);

            TaskCorner = new Vector2(x, -y);

            if (HudTheme.IsTouch && onPause != null)
                new CooldownSquare(parent, new Vector2(-HudTheme.ColumnRight, -HudTheme.ColumnTop),
                    "P", onPause, fromRight: true);

            Text hint = UIFactory.CreateText(parent,
                HudTheme.IsTouch ? objective : KeyHint + objective,
                HudTheme.HintSize, Vector2.zero, Vector2.zero);
            var rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-2f * HudTheme.HintSideInset, HudTheme.HintRowHeight);
            rt.anchoredPosition = new Vector2(0f, HudTheme.HintBottom);

            Tick();
        }

        CooldownSquare Square(Transform parent, float x, ref float y, string label,
            System.Action onPress, bool holdable = false)
        {
            var square = new CooldownSquare(parent, new Vector2(x, -y), label, onPress, holdable);
            y += HudTheme.SquarePitch;
            return square;
        }

        public void Tick()
        {
            if (_bombSquare != null && _bomber != null)
                _bombSquare.Set(_bomber.Charge, _bomber.IsReady);
            if (_boostSquare != null && _boost != null)
                _boostSquare.Set(_boost.Charge, _boost.IsReady || _boost.IsRunning);
            if (_fireSquare != null && _shooter != null)
            {
                _fireSquare.Set(0f, _shooter.IsReady);
                _shooter.SetHeld(_fireSquare.Held);
            }
            if (_lightSquare != null && _searchlight != null)
                _lightSquare.Set(0f, _searchlight.IsOn);
            if (_health != null && _plane != null)
                _health.Set(_plane.CurrentHealth, _plane.MaxHealth);
        }

        void RequestBomb()
        {
            if (_bomber != null) _bomber.Request();
        }

        void RequestBoost()
        {
            if (_boost != null) _boost.Request();
        }

        void ToggleLight()
        {
            if (_searchlight != null) _searchlight.Toggle();
        }
    }
}
