using System;

public readonly struct InvasionSuppressedEvent
{
    public InvasionSuppressedEvent(
        CharacterActor actor,
        CharacterActor defender)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        ActorId = CharacterPersistentIdentity.Require(actor);
        HasDefender = defender != null;
        DefenderId = defender != null
            ? CharacterPersistentIdentity.Require(defender).Value
            : string.Empty;
        DefenderName = defender != null ? defender.name : string.Empty;
    }

    public CharacterActor Actor { get; }
    public CharacterId ActorId { get; }
    public bool HasDefender { get; }
    public string DefenderId { get; }
    public string DefenderName { get; }
}

public readonly struct InvasionDefenseFailedEvent
{
    public InvasionDefenseFailedEvent(
        CharacterActor actor,
        CharacterActor owner)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        ActorId = CharacterPersistentIdentity.Require(actor);
        OwnerId = owner?.Identity?.PersistentId;
        OwnerName = owner?.Identity?.DisplayName;
    }

    public CharacterActor Actor { get; }
    public CharacterId ActorId { get; }
    public string OwnerId { get; }
    public string OwnerName { get; }
}

public readonly struct InvasionStructureHitEvent
{
    public InvasionStructureHitEvent(
        CharacterActor actor,
        BuildableObject target,
        float damage,
        bool enraged)
    {
        if (!float.IsFinite(damage) || damage < 0f)
            throw new ArgumentOutOfRangeException(nameof(damage));
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        ActorId = CharacterPersistentIdentity.Require(actor);
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        TargetId = target.RequirePersistentInstanceId().Value;
        TargetRuntimeName = target.BuildingData?.objectName ?? target.name;
        TargetDisplayName = target.BuildingData != null
            && !string.IsNullOrWhiteSpace(target.BuildingData.objectName)
                ? target.BuildingData.objectName
                : target.name;
        TargetDefinitionId = target.id;
        Damage = damage;
        Enraged = enraged;
    }

    public CharacterActor Actor { get; }
    public CharacterId ActorId { get; }
    public string TargetId { get; }
    public string TargetRuntimeName { get; }
    public string TargetDisplayName { get; }
    public int TargetDefinitionId { get; }
    public float Damage { get; }
    public bool Enraged { get; }
}
