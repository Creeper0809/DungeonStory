using System;
using System.Collections.Generic;

[Serializable]
public enum WildlifeGameplayOutcomeProfile
{
    Command = 0,
    Action = 1
}

public interface IWildlifeGameplayOutcomeOwner
{
    List<WildlifeGameplayOutcomeSaveData> GameplayOutcomes { get; }
    int NextGameplayOutcomeSequence { get; set; }
}

[Serializable]
public sealed class WildlifeGameplayOutcomeSaveData
{
    public const int CurrentSchemaVersion = 2;

    public int schemaVersion = CurrentSchemaVersion;
    public WildlifeGameplayOutcomeProfile profile;
    public string operationId = string.Empty;
    public string eventKind = string.Empty;
    public string wildlifeId = string.Empty;
    public string wildlifeSpeciesId = string.Empty;
    public string wildlifeDisplayName = string.Empty;
    public string actorId = string.Empty;
    public string actorDisplayName = string.Empty;
    public string facilityId = string.Empty;
    public string facilityDisplayName = string.Empty;
    public int x;
    public int y;
    public int stateBefore;
    public int stateAfter;
    public bool flagBefore;
    public bool flagAfter;
    public bool priorityBefore;
    public bool priorityAfter;
    public int healthBefore;
    public int healthAfter;
    public int damage;
    public bool hit;
    public bool killed;
    public string weaponDefinitionId = string.Empty;
    public string weaponInstanceId = string.Empty;
    public string ammunitionItemId = string.Empty;
    public int ammunitionBefore;
    public int ammunitionConsumed;
    public int ammunitionAfter;
    public string bodyPart = string.Empty;
    public string coverSourceId = string.Empty;
    public int coverDamage;
    public string carcassStackId = string.Empty;
    public string physicalChildResultKey = string.Empty;
    public int outputQuantity;
    public long outputMassGrams;
    public string reasonCode = string.Empty;
    public int absoluteDay;
    public long ownerRevision;
    // Opaque observation identity, not a domain-state or effect authority.
    public string publicationKey = string.Empty;

    public WildlifeGameplayOutcomeSaveData Clone() =>
        (WildlifeGameplayOutcomeSaveData)MemberwiseClone();

    public void Validate(bool proposed = false)
    {
        if (schemaVersion != CurrentSchemaVersion
            || !Enum.IsDefined(typeof(WildlifeGameplayOutcomeProfile), profile)
            || !Text(operationId) || !Text(eventKind) || !Text(wildlifeId)
            || !Text(wildlifeSpeciesId) || !Text(wildlifeDisplayName)
            || !Text(actorId, optional: true) || !Text(actorDisplayName, optional: true)
            || !Text(facilityId, optional: true) || !Text(facilityDisplayName, optional: true)
            || !Text(weaponDefinitionId, optional: true) || !Text(weaponInstanceId, optional: true)
            || !Text(ammunitionItemId, optional: true) || !Text(bodyPart, optional: true)
            || !Text(coverSourceId, optional: true) || !Text(carcassStackId, optional: true)
            || !Text(physicalChildResultKey, optional: true)
            || !Text(publicationKey, optional: true)
            || absoluteDay < 0 || ownerRevision <= 0 || ownerRevision > int.MaxValue
            || operationId != $"{(profile == WildlifeGameplayOutcomeProfile.Command ? "wildlife-command" : "wildlife-action")}:{eventKind}:{wildlifeId}:{ownerRevision}")
            throw new ArgumentException("wildlife-fact-identity-invalid");
        if (!proposed && (stateBefore < 0 || stateAfter < 0
            || healthBefore < 0 || healthAfter < 0 || damage < 0
            || damage != healthBefore - healthAfter
            || ammunitionBefore < 0 || ammunitionAfter < 0 || ammunitionConsumed < 0
            || ammunitionConsumed != ammunitionBefore - ammunitionAfter
            || coverDamage < 0 || outputQuantity < 0 || outputMassGrams < 0
            || !Text(reasonCode)
            || profile == WildlifeGameplayOutcomeProfile.Action && (!Text(actorId) || !Text(actorDisplayName))))
            throw new ArgumentException("wildlife-fact-result-invalid");
    }

    private static bool Text(string value, bool optional = false) => value != null
        && value.Length <= 4096 && (optional || !string.IsNullOrWhiteSpace(value))
        && value == value.Trim();
}
