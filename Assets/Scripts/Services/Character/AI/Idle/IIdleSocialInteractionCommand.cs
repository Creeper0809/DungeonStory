public interface IIdleSocialInteractionCommand
{
    bool TryExecute(CharacterActor actor, CharacterActor target, string behaviorId, out string failureReason);
}
