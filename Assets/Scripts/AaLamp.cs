using UnityEngine;

namespace MetalRaptors
{
    public class AaLamp : MonoBehaviour, IDamageable
    {
        public AaGun Gun { get; set; }

        public void TakeDamage(float amount)
        {
            if (Gun != null) Gun.HitLamp(amount);
        }
    }
}
