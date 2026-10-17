using System;
using DungeonStory.Foundation.Facts;

public enum CharacterLifeTransitionKind
{
    AgeCondition = 1,
    LifeStage = 2
}

[Serializable]
public sealed class CharacterLifeTransitionSaveData
{
    public int kind;
    public int absoluteDay;
    public string characterId = string.Empty;
    public string conditionId = string.Empty;
    public int previous;
    public int current;
    public bool newlyDiagnosed;
    public bool resolved;
}

/// <summary>
/// Frozen evidence of a character-life transition without runtime, journal,
/// anatomy, or narrative authority.
/// </summary>
public sealed class CharacterLifeTransitionFact
{
    private readonly CharacterLifeTransitionSaveData data;
    private readonly string digest;

    public CharacterLifeTransitionFact(CharacterLifeTransitionSaveData source)
    {
        Validate(source);
        data = Copy(source);
        digest = Digest(data);
    }

    public static CharacterLifeTransitionFact FromAgeCondition(
        AgeConditionChange change,
        int absoluteDay) => new(new CharacterLifeTransitionSaveData
        {
            kind = (int)CharacterLifeTransitionKind.AgeCondition,
            absoluteDay = absoluteDay,
            characterId = change.CharacterId.Value,
            conditionId = change.ConditionId,
            previous = (int)change.Previous,
            current = (int)change.Current,
            newlyDiagnosed = change.NewlyDiagnosed,
            resolved = change.Resolved
        });

    public static CharacterLifeTransitionFact FromLifeStage(
        CharacterLifeStageTransition transition,
        int absoluteDay) => new(new CharacterLifeTransitionSaveData
        {
            kind = (int)CharacterLifeTransitionKind.LifeStage,
            absoluteDay = absoluteDay,
            characterId = transition.CharacterId.Value,
            previous = (int)transition.Previous,
            current = (int)transition.Current
        });

    public CharacterLifeTransitionSaveData Snapshot() => Copy(data);

    public CharacterLifeTransitionKind Kind =>
        (CharacterLifeTransitionKind)data.kind;
    public string CharacterId => data.characterId;
    public int AbsoluteDay => data.absoluteDay;
    public string DomainId => "life-transition:" + digest;
    public string AnatomyOperationId =>
        Kind == CharacterLifeTransitionKind.AgeCondition && !data.resolved
            ? "age-condition-anatomy:" + digest
            : throw new InvalidOperationException(
                "An active age-condition transition is required.");

    public AgeConditionChange AgeChange =>
        Kind == CharacterLifeTransitionKind.AgeCondition
            ? new AgeConditionChange(
                new CharacterId(data.characterId),
                data.conditionId,
                (AgeConditionSeverity)data.previous,
                (AgeConditionSeverity)data.current,
                data.newlyDiagnosed,
                data.resolved)
            : throw new InvalidOperationException(
                "An age-condition transition is required.");

    public CharacterLifeStageTransition StageChange =>
        Kind == CharacterLifeTransitionKind.LifeStage
            ? new CharacterLifeStageTransition(
                new CharacterId(data.characterId),
                (CharacterLifeStage)data.previous,
                (CharacterLifeStage)data.current)
            : throw new InvalidOperationException(
                "A life-stage transition is required.");

    private static void Validate(CharacterLifeTransitionSaveData value)
    {
        if (value == null
            || (CharacterLifeTransitionKind)value.kind is not (
                CharacterLifeTransitionKind.AgeCondition
                or CharacterLifeTransitionKind.LifeStage)
            || value.absoluteDay <= 0
            || !DomainFactValueSyntax.IsIdentity(value.characterId)
            || !new CharacterId(value.characterId).IsValid)
        {
            throw Invalid();
        }

        if ((CharacterLifeTransitionKind)value.kind
            == CharacterLifeTransitionKind.AgeCondition)
        {
            ValidateAgeCondition(value);
            return;
        }

        if (value.conditionId != string.Empty
            || value.newlyDiagnosed
            || value.resolved
            || !Enum.IsDefined(
                typeof(CharacterLifeStage),
                (CharacterLifeStage)value.previous)
            || !Enum.IsDefined(
                typeof(CharacterLifeStage),
                (CharacterLifeStage)value.current)
            || value.current <= value.previous)
        {
            throw Invalid();
        }
    }

    private static void ValidateAgeCondition(
        CharacterLifeTransitionSaveData value)
    {
        if (!DomainFactValueSyntax.IsIdentity(value.conditionId)
            || !Enum.IsDefined(
                typeof(AgeConditionSeverity),
                (AgeConditionSeverity)value.previous)
            || !Enum.IsDefined(
                typeof(AgeConditionSeverity),
                (AgeConditionSeverity)value.current)
            || value.newlyDiagnosed && value.resolved)
        {
            throw Invalid();
        }

        var previous = (AgeConditionSeverity)value.previous;
        var current = (AgeConditionSeverity)value.current;
        bool valid = value.newlyDiagnosed
            ? previous == AgeConditionSeverity.Mild
                && current == AgeConditionSeverity.Mild
            : value.resolved
                ? (previous is AgeConditionSeverity.Mild
                    or AgeConditionSeverity.Moderate)
                    && current == AgeConditionSeverity.Mild
                : value.current < value.previous
                    || current == AgeConditionSeverity.Mild
                        && previous == AgeConditionSeverity.Mild
                    || value.current == value.previous + 1;
        if (!valid)
            throw Invalid();
    }

    private static CharacterLifeTransitionSaveData Copy(
        CharacterLifeTransitionSaveData value) => new()
        {
            kind = value.kind,
            absoluteDay = value.absoluteDay,
            characterId = value.characterId,
            conditionId = value.conditionId,
            previous = value.previous,
            current = value.current,
            newlyDiagnosed = value.newlyDiagnosed,
            resolved = value.resolved
        };

    private static string Digest(CharacterLifeTransitionSaveData value)
    {
        var builder = new CanonicalSemanticDigestBuilder();
        builder.Append(value.kind);
        builder.Append(value.absoluteDay);
        builder.Append(value.characterId);
        builder.Append(value.conditionId);
        builder.Append(value.previous);
        builder.Append(value.current);
        builder.Append(value.newlyDiagnosed);
        builder.Append(value.resolved);
        return builder.ComputeSha256();
    }

    private static ArgumentException Invalid() =>
        new("character-life-transition-fact-invalid");
}
