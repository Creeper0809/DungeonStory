using System;
using DungeonStory.Foundation;

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
}

public sealed class CharacterApologyCommandRuntime : ICharacterApologyCommand
{
    private readonly CharacterRelationshipMemoryService memories;
    private readonly IGameEventBus events;
    private readonly IGameCalendar calendar;

    public CharacterApologyCommandRuntime(
        CharacterRelationshipMemoryService memories,
        IGameEventBus events,
        IGameCalendar calendar)
    {
        this.memories = memories ?? throw new ArgumentNullException(nameof(memories));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
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

        CharacterId offenderId = CharacterPersistentIdentity.Require(offender);
        CharacterId recipientId = CharacterPersistentIdentity.Require(recipient);
        events.Publish(new ApologyEvent(
            offenderId,
            recipientId,
            "betrayal-or-assault",
            restitutionProvided,
            calendar.Day));
        return true;
    }
}
