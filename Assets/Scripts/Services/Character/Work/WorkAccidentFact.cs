using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class WorkAccidentFactSaveData
{
    public string operationId = string.Empty;
    public string workerId = string.Empty;
    public string workerDisplayName = string.Empty;
    public string facilityId = string.Empty;
    public string facilityDisplayName = string.Empty;
    public string workTypeId = string.Empty;
    public string anatomyNodeId = string.Empty;
    public string observedCause = string.Empty;
    public float appliedDamage;
    public float accidentMultiplier;
    public int absoluteDay;
    public int x;
    public int y;
    public int commandOrigin;
    public bool fireEligible;
    public float fireIntensity;
    public string fireTargetDisplayName = string.Empty;

    public WorkAccidentFactSaveData Clone() =>
        (WorkAccidentFactSaveData)MemberwiseClone();

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(operationId)
            || !DomainFactValueSyntax.IsIdentity(workerId)
            || !new CharacterId(workerId).IsValid
            || !Name(workerDisplayName)
            || !DomainFactValueSyntax.IsIdentity(facilityId)
            || !new BuildingInstanceId(facilityId).IsValid
            || !Name(facilityDisplayName)
            || !DomainFactValueSyntax.IsIdentity(workTypeId)
            || !DomainFactValueSyntax.IsIdentity(anatomyNodeId)
            || !Text(observedCause, 4096)
            || !float.IsFinite(appliedDamage)
            || appliedDamage <= 0f
            || !float.IsFinite(accidentMultiplier)
            || accidentMultiplier < 0f
            || absoluteDay < 0
            || !Enum.IsDefined(
                typeof(CharacterCommandOrigin),
                commandOrigin)
            || fireEligible
                && (!float.IsFinite(fireIntensity)
                    || fireIntensity <= 0f
                    || fireIntensity > 1f
                    || !Name(fireTargetDisplayName))
            || !fireEligible
                && (fireIntensity != 0f
                    || fireTargetDisplayName != string.Empty))
        {
            throw new ArgumentException("work-accident-fact-data-invalid");
        }
    }

    private static bool Name(string value) =>
        DomainFactValueSyntax.IsText(
            value,
            DomainFactValueSyntax.MaximumNameCharacters,
            allowEmpty: false);

    private static bool Text(string value, int maximumLength) =>
        DomainFactValueSyntax.IsText(
            value,
            maximumLength,
            allowEmpty: false);
}

/// <summary>
/// Immutable evidence of an applied work accident without live worker,
/// facility, anatomy, fire, or narrative authorities.
/// </summary>
public sealed class WorkAccidentFact
{
    private readonly WorkAccidentFactSaveData value;

    public WorkAccidentFact(WorkAccidentFactSaveData source)
    {
        value = (source
            ?? throw new ArgumentNullException(nameof(source))).Clone();
        value.Validate();
    }

    public WorkAccidentFactSaveData Snapshot() => value.Clone();

    public void Validate() => value.Validate();
}
