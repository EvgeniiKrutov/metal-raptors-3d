namespace MetalRaptors
{
    /// <summary>
    /// Anything a bullet can hurt. Bullets look for this on whatever they hit and apply their
    /// damage before destroying themselves; future enemies implement it to take machine-gun
    /// fire, and the player plane implements it so enemy fire can wear its health down.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}
