public interface ICharacterIdRegistry
{
    bool TryGetPersistentId(CharacterActor actor, out string persistentId);
    string GetOrAssignPersistentId(CharacterActor actor);
}
