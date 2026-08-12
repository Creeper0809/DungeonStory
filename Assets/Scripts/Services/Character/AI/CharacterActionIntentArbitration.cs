using System;

public enum CharacterActionIntentKind
{
    None = 0,
    RoutineNeed = 100,
    EmergencyNeed = 200,
    ProtectedAction = 250,
    Breakdown = 300
}

public readonly struct CharacterActionIntentLease
{
    public CharacterActionIntentLease(
        string ownerId,
        CharacterActionIntentKind kind,
        long epoch)
    {
        OwnerId = ownerId ?? string.Empty;
        Kind = kind;
        Epoch = epoch;
    }

    public string OwnerId { get; }
    public CharacterActionIntentKind Kind { get; }
    public long Epoch { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(OwnerId) && Epoch > 0L;
}

