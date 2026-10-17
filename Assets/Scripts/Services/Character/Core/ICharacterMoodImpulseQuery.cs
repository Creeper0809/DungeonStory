public interface ICharacterMoodImpulseQuery
{
    CharacterMoodImpulseType LastAppliedType { get; }
    string LastAppliedActorName { get; }
}
