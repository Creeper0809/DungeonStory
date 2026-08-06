internal static class CharacterActorRuntimeFacade
{
    internal static CharacterMoodSnapshot GetMood(CharacterStats stats)
    {
        return stats != null
            ? stats.GetMoodSnapshot()
            : new CharacterMoodSnapshot(
                CharacterMoodRules.DefaultBaseMood,
                CharacterMoodRules.DefaultBaseMood,
                System.Array.Empty<CharacterMoodFactorSnapshot>());
    }

    internal static float GetMoveSpeed(
        CharacterStats stats,
        CharacterIdentity identity,
        CharacterCarryInventory inventory)
    {
        float baseSpeed = stats != null
            ? stats.GetMoveSpeed()
            : identity != null && identity.Data != null
                ? identity.Data.moveSpeed
                : 1f;
        return baseSpeed * (inventory != null
            ? inventory.GetMoveSpeedMultiplier()
            : 1f);
    }
}
