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
        if (float.IsNaN(baseSpeed)
            || float.IsInfinity(baseSpeed)
            || baseSpeed < 0f)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(baseSpeed),
                baseSpeed,
                "Base character movement speed must be finite and non-negative.");
        }
        if (baseSpeed == 0f)
        {
            return 0f;
        }

        float burdenMultiplier = inventory != null
            ? inventory.GetMoveSpeedMultiplier()
            : 1f;
        if (float.IsNaN(burdenMultiplier)
            || float.IsInfinity(burdenMultiplier)
            || burdenMultiplier < 0f)
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(burdenMultiplier),
                burdenMultiplier,
                "Carry movement multiplier must be finite and non-negative.");
        }

        float result = baseSpeed * burdenMultiplier;
        if (float.IsInfinity(result))
        {
            throw new System.ArgumentOutOfRangeException(
                nameof(result),
                result,
                "Character movement speed overflowed its finite contract.");
        }

        return result;
    }
}
