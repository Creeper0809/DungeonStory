using System;

/// <summary>A captured domain transition, independent of its durable log subscriber.</summary>
public readonly struct CharacterEnvironmentExposureFact
{
    private readonly EnvironmentalExposureOutcomeSaveData snapshot;
    public CharacterEnvironmentExposureFact(EnvironmentalExposureOutcomeSaveData actual) =>
        snapshot = Copy(actual ?? throw new ArgumentNullException(nameof(actual)));
    public EnvironmentalExposureOutcomeSaveData Snapshot() => Copy(snapshot
        ?? throw new InvalidOperationException("An exposure fact has no snapshot."));
    private static EnvironmentalExposureOutcomeSaveData Copy(EnvironmentalExposureOutcomeSaveData source) => new()
    {
        schemaVersion = source.schemaVersion,
        operationId = source.operationId,
        eventKind = source.eventKind,
        characterId = source.characterId,
        characterDisplayName = source.characterDisplayName,
        x = source.x,
        y = source.y,
        absoluteDay = source.absoluteDay,
        ownerRevision = source.ownerRevision,
        temperatureC = source.temperatureC,
        airQuality = source.airQuality,
        lightLevel = source.lightLevel,
        coldBefore = source.coldBefore,
        coldAfter = source.coldAfter,
        heatBefore = source.heatBefore,
        heatAfter = source.heatAfter,
        airborneBefore = source.airborneBefore,
        airborneAfter = source.airborneAfter,
        bandBefore = source.bandBefore,
        bandAfter = source.bandAfter,
        damage = source.damage,
        healthBefore = source.healthBefore,
        healthAfter = source.healthAfter,
        reasonCode = source.reasonCode,
    };
}
