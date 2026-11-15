namespace MetalRaptors
{
    public enum EnemyRole
    {
        Scout,
        Fighter,
        Roller,
    }

    public static class EnemyRoles
    {
        public static bool IsAce(EnemyRole role) => role == EnemyRole.Roller;
    }
}
