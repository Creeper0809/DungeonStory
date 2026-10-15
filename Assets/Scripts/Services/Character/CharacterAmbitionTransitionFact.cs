using System;
using DungeonStory.Foundation.Facts;

// Facts describe a domain transition, not a log record or a scoring decision.
[Serializable]
public sealed class CharacterAmbitionTransitionFactData
{
    public const string ActivationReason = "activation-requirements-met";
    public const string FailureReason = "failure-conditions-met";
    public const string DeadlineReason = "deadline-reached";
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public string ambitionId = string.Empty;
    public string ambitionDisplayName = string.Empty;
    public string reasonCode = string.Empty;
    public int definitionRevision;
    public CharacterAmbitionStatus beforeStatus, afterStatus;
    public int transitionDay, startDayBefore, startDayAfter, dueDayBefore, dueDayAfter;
    public int progressBefore, progressAfter, nextAllowedDayBefore, nextAllowedDayAfter;

    public CharacterAmbitionTransitionFactData Clone() =>
        (CharacterAmbitionTransitionFactData)MemberwiseClone();

    public void Validate()
    {
        bool Name(string value) => DomainFactValueSyntax.IsText(value, 256, false)
            && value == value.Trim();
        if (!DomainFactValueSyntax.IsIdentity(characterId)
            || !DomainFactValueSyntax.IsIdentity(ambitionId) || !new CharacterAmbitionId(ambitionId).IsValid
            || !Name(characterDisplayName) || !Name(ambitionDisplayName)
            || !Enum.IsDefined(typeof(CharacterAmbitionStatus), beforeStatus)
            || !Enum.IsDefined(typeof(CharacterAmbitionStatus), afterStatus)
            || definitionRevision <= 0 || transitionDay < 1 || startDayBefore < 0 || startDayAfter < 0
            || dueDayBefore < 0 || dueDayAfter < 0 || progressBefore < 0 || progressAfter < 0
            || nextAllowedDayBefore < 0 || nextAllowedDayAfter < 0)
            throw new InvalidOperationException("Ambition transition fact values are invalid.");
        bool starting = afterStatus == CharacterAmbitionStatus.Active
            && beforeStatus != CharacterAmbitionStatus.Active && reasonCode == ActivationReason
            && startDayAfter == transitionDay && dueDayAfter > transitionDay
            && progressAfter == 0 && nextAllowedDayAfter == nextAllowedDayBefore;
        bool failing = beforeStatus == CharacterAmbitionStatus.Active && afterStatus == CharacterAmbitionStatus.Failed
            && (reasonCode == FailureReason || reasonCode == DeadlineReason)
            && startDayBefore <= transitionDay && startDayAfter == 0
            && (reasonCode != DeadlineReason || dueDayBefore > 0 && dueDayBefore <= transitionDay)
            && dueDayAfter == 0 && progressAfter == 0 && nextAllowedDayAfter == checked(transitionDay + 10);
        if (!starting && !failing)
            throw new InvalidOperationException("Ambition transition fact is inconsistent.");
    }

    public void RequireAfterState(CharacterNarrativeSnapshot actual)
    {
        if (actual == null || actual.CharacterId.Value != characterId
            || actual.AmbitionStatus != afterStatus || actual.AmbitionStartedAbsoluteDay != startDayAfter
            || actual.AmbitionDueAbsoluteDay != dueDayAfter || actual.AmbitionProgress != progressAfter
            || actual.NextAmbitionAllowedAbsoluteDay != nextAllowedDayAfter
            || (afterStatus == CharacterAmbitionStatus.Active
                ? actual.ActiveAmbitionId.Value != ambitionId : actual.ActiveAmbitionId.IsValid))
            throw new InvalidOperationException("Ambition transition state does not match its frozen fact.");
    }
}

public readonly struct CharacterAmbitionTransitionFact
{
    private readonly CharacterAmbitionTransitionFactData data;
    public CharacterAmbitionTransitionFact(CharacterAmbitionTransitionFactData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.Validate();
        this.data = data.Clone();
    }
    public CharacterAmbitionTransitionFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("Ambition transition fact is uninitialized.");
}

[Serializable]
public sealed class CharacterAmbitionTransitionObservationSaveData
{
    public CharacterAmbitionTransitionFactData fact;
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string publicationFingerprint = string.Empty;

    public CharacterAmbitionTransitionObservationSaveData Clone() => new()
    {
        fact = fact?.Clone(), publicationKey = publicationKey,
        publicationId = publicationId, publicationFingerprint = publicationFingerprint
    };

    public void Validate()
    {
        if (fact == null) throw new InvalidOperationException("Ambition transition pending fact is missing.");
        fact.Validate();
        bool Text(string value, bool empty) => DomainFactValueSyntax.IsText(value, 4096, empty)
            && value == value.Trim();
        if (!Text(publicationKey, false) || !Text(publicationId, true) || !Text(publicationFingerprint, true)
            || (publicationId.Length == 0) != (publicationFingerprint.Length == 0))
            throw new InvalidOperationException("Ambition transition observation metadata is invalid.");
    }
}
