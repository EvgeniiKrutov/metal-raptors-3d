using System;
using UnityEngine;

namespace MetalRaptors
{
    public class Airfield : MonoBehaviour
    {
        public const float BaseHealth = 100f;
        public const float MaxHealth = BaseHealth * HealthMargin;
        public const float ShellSeconds = 45f;

        const float HealthMargin = 2.6f;
        const float FenceAimHeight = 12f;
        const string DefaultCaption = "AIRFIELD";

        public static Airfield Current { get; private set; }

        public event Action OnLost;

        Rect _field;
        float _groundY;
        float _health = MaxHealth;
        bool _lost;

        public string Caption { get; private set; } = DefaultCaption;

        public bool Outlined { get; private set; }

        public float CurrentHealth => _health;

        public bool Lost => _lost;

        public float FenceX => _field.xMax;

        public Rect Field => _field;

        public float GroundY => _groundY;

        public Vector3 AimPoint(float z) =>
            new Vector3(_field.xMax, _groundY + FenceAimHeight, z);

        public static Airfield Begin(Rect field, float groundY, string caption = DefaultCaption,
            bool outlined = false)
        {
            if (Current != null) Destroy(Current.gameObject);

            var airfield = new GameObject("Airfield").AddComponent<Airfield>();
            airfield._field = field;
            airfield._groundY = groundY;
            airfield.Caption = caption;
            airfield.Outlined = outlined;
            Current = airfield;
            return airfield;
        }

        public void Retire()
        {
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        public void Restore(float health)
        {
            if (_lost) return;
            _health = Mathf.Clamp(health, 1f, MaxHealth);
        }

        public void Shell(float amount)
        {
            if (_lost || amount <= 0f) return;

            _health = Mathf.Max(0f, _health - amount);
            if (_health > 0f) return;

            _lost = true;
            OnLost?.Invoke();
        }

        public Vector3 BlastPoint(System.Random rng) => new Vector3(
            _field.xMin + _field.width * (float)rng.NextDouble(),
            _groundY,
            _field.yMin + _field.height * (float)rng.NextDouble());

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
