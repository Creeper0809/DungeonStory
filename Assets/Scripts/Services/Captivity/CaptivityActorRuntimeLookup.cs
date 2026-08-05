using System;

internal sealed class CaptivityActorRuntimeLookup
{
    private readonly Func<string, CharacterActor> findActor;

    public CaptivityActorRuntimeLookup(Func<string, CharacterActor> findActor)
    {
        this.findActor = findActor ?? throw new ArgumentNullException(nameof(findActor));
    }

    public CharacterActor Find(string captiveId) =>
        findActor(captiveId?.Trim() ?? string.Empty);
}
