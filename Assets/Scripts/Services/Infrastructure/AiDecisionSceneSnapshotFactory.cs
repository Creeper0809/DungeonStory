using DungeonStory.AI;

internal static class AiDecisionSceneSnapshotFactory
{
    public static AiCharacterDecisionSnapshot CaptureBase(CharacterActor actor)
    {
        CharacterId characterId = CaptureId(actor);
        bool hasShopping = actor != null
            && actor.TryGetAbility(out AbilityShopping _);
        bool hasWork = CharacterWorkRoleUtility.TryGetWork(
            actor,
            out AbilityWork work);
        return new AiCharacterDecisionSnapshot(
            characterId,
            actor != null,
            hasShopping: hasShopping,
            hasWorkRole: hasWork,
            isOffDuty: hasWork && work.IsOffDuty);
    }

    public static CharacterId CaptureId(CharacterActor actor) =>
        CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            ? id
            : default;
}
