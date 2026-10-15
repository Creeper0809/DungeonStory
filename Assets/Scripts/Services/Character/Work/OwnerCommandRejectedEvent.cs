using System;
using DungeonStory.Foundation.Facts;

public enum OwnerCommandRejectedKind
{
    PriorityWork = 1,
    Suppress = 2
}

/// <summary>
/// A rejected direct owner command with frozen domain target evidence.
/// Activity formatting and narrative policy belong to the observer.
/// </summary>
public readonly struct OwnerCommandRejectedEvent : ICharacterIdentityEvent
{
    public OwnerCommandRejectedEvent(
        OwnerCommandRejectedKind kind,
        CharacterId character,
        string targetId,
        string targetName,
        int targetDefinitionId,
        string reasonCode,
        string detail)
    {
        detail ??= string.Empty;
        if (!Enum.IsDefined(typeof(OwnerCommandRejectedKind), kind)
            || !character.IsValid
            || !DomainFactValueSyntax.IsText(targetId, 1024, false)
            || !DomainFactValueSyntax.IsText(targetName, 16384, true)
            || !DomainFactValueSyntax.IsText(reasonCode, 1024, false)
            || !DomainFactValueSyntax.IsText(detail, 16384, true)
            || kind == OwnerCommandRejectedKind.PriorityWork
                && !new BuildingInstanceId(targetId).IsValid
            || kind == OwnerCommandRejectedKind.Suppress
                && (!new CharacterId(targetId).IsValid
                    || targetDefinitionId != -1))
        {
            throw new ArgumentException("owner-command-rejected-event-invalid");
        }

        Kind = kind;
        Character = character;
        TargetId = targetId;
        TargetName = targetName;
        TargetDefinitionId = targetDefinitionId;
        ReasonCode = reasonCode;
        Detail = detail;
    }

    public string EventId => "event:owner-command-rejected";
    public int AbsoluteDay => 0;
    public CharacterCommandOrigin Origin =>
        CharacterCommandOrigin.DirectPlayerOrder;
    public OwnerCommandRejectedKind Kind { get; }
    public CharacterId Character { get; }
    public string TargetId { get; }
    public string TargetName { get; }
    public int TargetDefinitionId { get; }
    public string ReasonCode { get; }
    public string Detail { get; }
}
