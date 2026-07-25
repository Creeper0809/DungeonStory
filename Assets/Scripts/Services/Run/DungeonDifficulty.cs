using System;

public enum DungeonDifficulty
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}

public readonly struct DungeonDifficultyMultipliers
{
    public DungeonDifficultyMultipliers(float enemyHealth, float enemyAttack, float enemyInitiative)
    {
        EnemyHealth = enemyHealth;
        EnemyAttack = enemyAttack;
        EnemyInitiative = enemyInitiative;
    }

    public float EnemyHealth { get; }
    public float EnemyAttack { get; }
    public float EnemyInitiative { get; }
}

public static class DungeonDifficultyRules
{
    public static DungeonDifficulty FromLegacy(InvasionThreatDifficulty difficulty)
    {
        return difficulty switch
        {
            InvasionThreatDifficulty.Easy => DungeonDifficulty.Easy,
            InvasionThreatDifficulty.Hard => DungeonDifficulty.Hard,
            _ => DungeonDifficulty.Normal
        };
    }

    public static InvasionThreatDifficulty ToLegacy(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => InvasionThreatDifficulty.Easy,
            DungeonDifficulty.Hard => InvasionThreatDifficulty.Hard,
            _ => InvasionThreatDifficulty.Normal
        };
    }

    public static DungeonDifficultyMultipliers GetOffenseMultipliers(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => new DungeonDifficultyMultipliers(0.8f, 0.8f, 1f),
            DungeonDifficulty.Hard => new DungeonDifficultyMultipliers(1.25f, 1.2f, 1.1f),
            _ => new DungeonDifficultyMultipliers(1f, 1f, 1f)
        };
    }

    public static DungeonDifficulty Normalize(int value)
    {
        return Enum.IsDefined(typeof(DungeonDifficulty), value)
            ? (DungeonDifficulty)value
            : DungeonDifficulty.Normal;
    }
}
