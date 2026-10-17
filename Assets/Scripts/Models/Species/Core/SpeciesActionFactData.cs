using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class GolemUpkeepFactSaveData
{
    public string operationId = string.Empty;
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public string speciesId = string.Empty;
    public string speciesDisplayName = string.Empty;
    public long revision;
    public int absoluteDay;
    public int x;
    public int y;
    public string kind = string.Empty;
    public string physicalEvidence = string.Empty;
    public string workSourceId = string.Empty;
    public string workTypeId = string.Empty;
    public float chargeBefore;
    public float chargeAfter;
    public float integrityBefore;
    public float integrityAfter;
    public float healthBefore;
    public float healthAfter;
    public float completedWork;
    public float requestedDamage;
    public float appliedDamage;
    public List<string> wornNodes = new();

    public GolemUpkeepFactSaveData Clone()
    {
        var result = (GolemUpkeepFactSaveData)MemberwiseClone();
        result.wornNodes = wornNodes == null
            ? null
            : new List<string>(wornNodes);
        return result;
    }

    public void Validate(bool proposed = false)
    {
        SpeciesActionFactDataValidation.Common(
            operationId,
            characterId,
            characterDisplayName,
            speciesId,
            speciesDisplayName,
            revision,
            absoluteDay);
        if (kind is not ("recharge" or "wear" or "discharge")
            || !SpeciesActionFactDataValidation.OptionalText(
                physicalEvidence,
                4096)
            || !SpeciesActionFactDataValidation.OptionalIdentity(workSourceId)
            || !SpeciesActionFactDataValidation.OptionalIdentity(workTypeId)
            || !SpeciesActionFactDataValidation.Percentage(chargeBefore)
            || !SpeciesActionFactDataValidation.Percentage(chargeAfter)
            || !SpeciesActionFactDataValidation.Percentage(integrityBefore)
            || !SpeciesActionFactDataValidation.Percentage(integrityAfter)
            || !SpeciesActionFactDataValidation.NonNegative(healthBefore)
            || !SpeciesActionFactDataValidation.NonNegative(healthAfter)
            || !SpeciesActionFactDataValidation.NonNegative(completedWork)
            || !SpeciesActionFactDataValidation.NonNegative(requestedDamage)
            || !SpeciesActionFactDataValidation.NonNegative(appliedDamage)
            || appliedDamage > requestedDamage + 0.0001f
            || wornNodes == null)
        {
            throw new ArgumentException("golem-upkeep-fact-data-invalid");
        }
        for (int index = 0; index < wornNodes.Count; index++)
        {
            if (!DomainFactValueSyntax.IsIdentity(wornNodes[index]))
                throw new ArgumentException("golem-upkeep-worn-node-invalid");
        }
        bool valid = kind switch
        {
            "recharge" => physicalEvidence.Length > 0
                && workSourceId.Length == 0
                && workTypeId.Length == 0
                && completedWork > 0f
                && wornNodes.Count == 0
                && requestedDamage == 0f
                && appliedDamage == 0f
                && chargeAfter >= chargeBefore
                && integrityAfter == integrityBefore
                && healthAfter == healthBefore,
            "wear" => physicalEvidence.Length == 0
                && workSourceId.Length > 0
                && workTypeId.Length > 0
                && completedWork > 0f
                && wornNodes.Count > 0
                && requestedDamage == 0f
                && appliedDamage == 0f
                && chargeAfter == chargeBefore
                && integrityAfter <= integrityBefore
                && healthAfter <= healthBefore,
            "discharge" => physicalEvidence.Length == 0
                && workSourceId.Length == 0
                && workTypeId.Length == 0
                && completedWork == 0f
                && wornNodes.Count == 0
                && chargeBefore > 0f
                && chargeAfter == 0f
                && integrityAfter == integrityBefore
                && requestedDamage > 0f
                && appliedDamage > 0f
                && healthAfter <= healthBefore,
            _ => false
        };
        if (proposed)
        {
            valid = kind switch
            {
                "recharge" => physicalEvidence.Length == 0
                    && workSourceId.Length == 0
                    && workTypeId.Length == 0
                    && completedWork > 0f
                    && wornNodes.Count == 0
                    && requestedDamage == 0f
                    && appliedDamage == 0f
                    && chargeAfter == chargeBefore
                    && integrityAfter == integrityBefore
                    && healthAfter == healthBefore,
                "wear" => physicalEvidence.Length == 0
                    && workSourceId.Length > 0
                    && workTypeId.Length > 0
                    && completedWork > 0f
                    && requestedDamage == 0f
                    && appliedDamage == 0f
                    && chargeAfter == chargeBefore
                    && integrityAfter == integrityBefore
                    && healthAfter == healthBefore,
                "discharge" => physicalEvidence.Length == 0
                    && workSourceId.Length == 0
                    && workTypeId.Length == 0
                    && completedWork == 0f
                    && wornNodes.Count == 0
                    && chargeBefore > 0f
                    && chargeAfter == 0f
                    && integrityAfter == integrityBefore
                    && requestedDamage > 0f
                    && appliedDamage == 0f
                    && healthAfter == healthBefore,
                _ => false
            };
        }
        if (!valid)
            throw new ArgumentException("golem-upkeep-fact-data-incomplete");
    }
}

[Serializable]
public sealed class SpeciesIncidentFactSaveData
{
    public string operationId = string.Empty;
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public string speciesId = string.Empty;
    public string speciesDisplayName = string.Empty;
    public long revision;
    public int absoluteDay;
    public int x;
    public int y;
    public string incidentId = string.Empty;
    public string summary = string.Empty;

    public SpeciesIncidentFactSaveData Clone() =>
        (SpeciesIncidentFactSaveData)MemberwiseClone();

    public void Validate(bool proposed = false)
    {
        SpeciesActionFactDataValidation.Common(
            operationId,
            characterId,
            characterDisplayName,
            speciesId,
            speciesDisplayName,
            revision,
            absoluteDay);
        if (!DomainFactValueSyntax.IsIdentity(incidentId)
            || !SpeciesActionFactDataValidation.Text(
                summary,
                4096,
                allowEmpty: proposed))
        {
            throw new ArgumentException("species-incident-fact-data-invalid");
        }
    }
}

[Serializable]
public sealed class HarpyGaleFactSaveData
{
    public string operationId = string.Empty;
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public string speciesId = string.Empty;
    public string speciesDisplayName = string.Empty;
    public long revision;
    public int absoluteDay;
    public int x;
    public int y;
    public string sourceStackId = string.Empty;
    public string destinationStackId = string.Empty;
    public string itemId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string itemDisplayName = string.Empty;
    public string reasonCode = string.Empty;
    public int quantity;
    public int sourceX;
    public int sourceY;
    public int destinationX;
    public int destinationY;
    public long massGrams;
    public long physicalRevision;

    public HarpyGaleFactSaveData Clone() =>
        (HarpyGaleFactSaveData)MemberwiseClone();

    public void Validate()
    {
        SpeciesActionFactDataValidation.Common(
            operationId,
            characterId,
            characterDisplayName,
            speciesId,
            speciesDisplayName,
            revision,
            absoluteDay);
        if (!DomainFactValueSyntax.IsIdentity(sourceStackId)
            || !DomainFactValueSyntax.IsIdentity(destinationStackId)
            || !DomainFactValueSyntax.IsIdentity(itemId)
            || !SpeciesActionFactDataValidation.OptionalIdentity(itemInstanceId)
            || !SpeciesActionFactDataValidation.Name(itemDisplayName)
            || !DomainFactValueSyntax.IsIdentity(reasonCode)
            || quantity <= 0
            || massGrams <= 0L
            || physicalRevision <= 0L)
        {
            throw new ArgumentException("harpy-gale-fact-data-invalid");
        }
    }
}

internal static class SpeciesActionFactDataValidation
{
    internal static void Common(
        string operationId,
        string characterId,
        string characterDisplayName,
        string speciesId,
        string speciesDisplayName,
        long revision,
        int absoluteDay)
    {
        if (!DomainFactValueSyntax.IsIdentity(operationId)
            || !DomainFactValueSyntax.IsIdentity(characterId)
            || !new CharacterId(characterId).IsValid
            || !Name(characterDisplayName)
            || !DomainFactValueSyntax.IsIdentity(speciesId)
            || !new CharacterSpeciesId(speciesId).IsValid
            || !Name(speciesDisplayName)
            || revision <= 0L
            || absoluteDay < 0)
        {
            throw new ArgumentException("species-action-common-invalid");
        }
    }

    internal static bool Name(string value) => Text(
        value,
        DomainFactValueSyntax.MaximumNameCharacters,
        allowEmpty: false);

    internal static bool OptionalIdentity(string value) =>
        value != null
        && (value.Length == 0 || DomainFactValueSyntax.IsIdentity(value));

    internal static bool OptionalText(string value, int maximumLength) =>
        Text(value, maximumLength, allowEmpty: true);

    internal static bool Text(
        string value,
        int maximumLength,
        bool allowEmpty) =>
        DomainFactValueSyntax.IsText(value, maximumLength, allowEmpty)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    internal static bool Percentage(float value) =>
        float.IsFinite(value) && value >= 0f && value <= 100f;

    internal static bool NonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;
}
