namespace MetalRaptors
{
    public enum EnemyRole
    {
        Scout,
        Fighter,
        Duelist,
    }

    public static class EnemyRoles
    {
        public static bool IsAce(EnemyRole role) => role == EnemyRole.Duelist;
    }
}
