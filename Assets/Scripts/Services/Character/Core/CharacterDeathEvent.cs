public struct CharacterDeathEvent
{
    public CharacterActor Actor;
    public string Reason;

    public CharacterDeathEvent(CharacterActor actor, string reason)
    {
        Actor = actor;
        Reason = reason;
    }
}
