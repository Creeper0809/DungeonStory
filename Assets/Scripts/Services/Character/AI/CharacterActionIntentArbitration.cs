using System;

public enum CharacterActionIntentKind
{
    None = 0,
    RoutineNeed = 100,
    // Emergency self-care is ordered by physical harm risk so a hygiene or
    // bladder action cannot keep ownership while hunger/thirst is already
    // crossing a damage threshold. Existing callers that do not distinguish
    // the need retain the base emergency value.
    EmergencyNeed = 200,
    EmergencySleep = 210,
    EmergencyExcretion = 220,
    EmergencyHygiene = 230,
    EmergencyPhysicalImminent = 240,
    EmergencyPhysicalActive = 250,
    EmergencyPhysicalCritical = 260,
    ProtectedAction = 270,
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
