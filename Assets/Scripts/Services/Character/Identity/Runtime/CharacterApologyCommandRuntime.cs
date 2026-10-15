using System;
using DungeonStory.Foundation;
using VContainer;

public interface ICharacterApologyCommand
{
    bool CanApologize(
        CharacterActor offender,
        CharacterActor recipient,
        bool restitutionProvided,
        out string failureReason);

    bool TryApologize(
        CharacterActor offender,
        CharacterActor recipient,
        bool restitutionProvided,
        out string failureReason);

    bool TryApologize(
        CharacterActor offender,
        CharacterActor recipient,
        bool restitutionProvided,
        string offendingPublicationId,
        string relationKind,
        out string failureReason);
}

public sealed class CharacterApologyCommandRuntime : ICharacterApologyCommand
{
    private readonly CharacterRelationshipMemoryService memories;
    private readonly IGameEventBus events;
    private readonly IGameCalendar calendar;
    private readonly ISocialOffenseHistory outcomes;
    private readonly Func<string> operationIdFactory;

    [Inject]
    public CharacterApologyCommandRuntime(
        CharacterRelationshipMemoryService memories,
        IGameEventBus events,
        IGameCalendar calendar,
        ISocialOffenseHistory outcomes)
        : this(memories, events, calendar, outcomes, null)
    {
    }

    public CharacterApologyCommandRuntime(
        CharacterRelationshipMemoryService memories,
        IGameEventBus events,
        IGameCalendar calendar,
        ISocialOffenseHistory outcomes,
        Func<string> operationIdFactory)
    {
        this.memories = memories ?? throw new ArgumentNullException(nameof(memories));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        this.operationIdFactory = operationIdFactory
            ?? (() => $"apology-operation:{Guid.NewGuid():N}");
    }

    public bool CanApologize(
        CharacterActor offender,
        CharacterActor recipient,
        bool restitutionProvided,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (offender == null || recipient == null || offender == recipient)
        {
            failureReason = "서로 다른 두 생존 인물이 필요합니다.";
            return false;
        }
        if (offender.IsDead || recipient.IsDead)
        {
            failureReason = "사망한 인물은 사과를 주고받을 수 없습니다.";
            return false;
        }
        if (!CharacterPersistentIdentity.TryGet(offender, out _)
            || !CharacterPersistentIdentity.TryGet(recipient, out _))
        {
            failureReason = "사과에는 저장 가능한 인물 ID가 필요합니다.";
            return false;
        }
        if (!memories.CanForgive(recipient, offender, restitutionProvided))
        {
            failureReason = restitutionProvided
                ? "보상으로 해소할 수 있는 관계 기억이 없습니다."
                : "보상 없이 받아들일 수 있는 사과 대상이 없습니다.";
            return false;
        }
        return true;
    }

    [GameplayEntryPoint(
        "StaffManagementSurfacePanel apology action; V26 identity focused audit")]
    public bool TryApologize(
        CharacterActor offender,
        CharacterActor recipient,
        bool restitutionProvided,
        out string failureReason)
    {
        if (!CanApologize(
                offender,
                recipient,
                restitutionProvided,
                out failureReason))
            return false;

        if (!TryResolveOffendingOutcome(
                offender,
                recipient,
                out string offendingPublicationId,
                out string relationKind,
                out _,
                out failureReason))
        {
            return false;
        }
        return TryApologize(
            offender,
            recipient,
            restitutionProvided,
            offendingPublicationId,
            relationKind,
            out failureReason);
    }

    public bool TryApologize(
        CharacterActor offender,
        CharacterActor recipient,
        bool restitutionProvided,
        string offendingPublicationId,
        string relationKind,
        out string failureReason)
    {
        if (!CanApologize(
                offender,
                recipient,
                restitutionProvided,
                out failureReason))
        {
            return false;
        }
        if (!TryValidateSelectedCause(
                offender,
                recipient,
                offendingPublicationId,
                relationKind,
                out string rootOperationId,
                out failureReason))
        {
            return false;
        }

        CharacterId offenderId = CharacterPersistentIdentity.Require(offender);
        CharacterId recipientId = CharacterPersistentIdentity.Require(recipient);
        string operationId = operationIdFactory()?.Trim() ?? string.Empty;
        if (operationId.Length == 0)
        {
            failureReason = "사과 작업 ID를 만들 수 없습니다.";
            return false;
        }
        events.Publish(new ApologyEvent(
            offenderId,
            recipientId,
            "betrayal-or-assault",
            restitutionProvided,
            calendar.Day,
            operationId,
            offendingPublicationId,
            rootOperationId,
            "reconciles"));
        return true;
    }

    private bool TryResolveOffendingOutcome(
        CharacterActor offender, CharacterActor recipient,
        out string publicationId, out string relationKind,
        out string rootOperationId, out string failureReason)
    {
        publicationId = relationKind = rootOperationId = string.Empty;
        if (outcomes.TryFind(CharacterPersistentIdentity.Require(offender).Value,
                CharacterPersistentIdentity.Require(recipient).Value, out SocialOffenseReference cause))
        {
            publicationId = cause.PublicationId;
            relationKind = cause.Kind;
            rootOperationId = cause.OperationId;
            failureReason = string.Empty;
            return true;
        }
        failureReason = "사과할 배신·폭행의 실제 결과 기록을 찾을 수 없습니다.";
        return false;
    }

    private bool TryValidateSelectedCause(
        CharacterActor offender, CharacterActor recipient,
        string offendingPublicationId, string relationKind,
        out string rootOperationId, out string failureReason)
    {
        rootOperationId = string.Empty;
        if (!outcomes.TryGet(offendingPublicationId,
                CharacterPersistentIdentity.Require(offender).Value,
                CharacterPersistentIdentity.Require(recipient).Value,
                relationKind?.Trim() ?? string.Empty, out SocialOffenseReference cause))
        {
            failureReason = "선택한 사과 원인이 실제 두 인물의 배신·폭행 기록과 일치하지 않습니다.";
            return false;
        }
        rootOperationId = cause.OperationId;
        failureReason = string.Empty;
        return true;
    }
}
