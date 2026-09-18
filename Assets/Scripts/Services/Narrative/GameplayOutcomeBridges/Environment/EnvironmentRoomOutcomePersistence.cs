using System;
using System.Collections.Generic;

[Serializable]
public sealed class RoomEnvironmentOutcomeSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public long nextOutcomeSequence = 1L;
    public List<RoomEnvironmentObservationSaveData> observations = new();
}

[Serializable]
public sealed class RoomEnvironmentObservationSaveData
{
    public string observerRoomKey = string.Empty;
    public float cleanliness;
}

public sealed class RoomEnvironmentOutcomeRestoreCandidate
{
    public RoomEnvironmentOutcomeRestoreCandidate(
        long nextOutcomeSequence,
        IReadOnlyDictionary<string, float> observations)
    {
        NextOutcomeSequence = nextOutcomeSequence;
        Observations = observations;
    }
    public long NextOutcomeSequence { get; }
    public IReadOnlyDictionary<string, float> Observations { get; }
}

public interface IRoomEnvironmentOutcomePersistence
{
    RoomEnvironmentOutcomeSaveData CaptureOutcomeState();
    RoomEnvironmentOutcomeRestoreCandidate PrepareOutcomeRestore(
        RoomEnvironmentOutcomeSaveData data);
    void PublishOutcomeRestore(RoomEnvironmentOutcomeRestoreCandidate candidate);
}

public sealed class RoomEnvironmentOutcomeSaveSection :
    DungeonStrictJsonSaveSection<
        RoomEnvironmentOutcomeSaveData,
        RoomEnvironmentOutcomeRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "world.room-environment-outcomes";
    private static readonly string[] Dependencies =
    {
        FoundationSessionSaveSection.Id,
        GameplayOutcomeLedgerSaveSection.Id
    };
    private readonly IRoomEnvironmentOutcomePersistence persistence;

    public RoomEnvironmentOutcomeSaveSection(
        IRoomEnvironmentOutcomePersistence persistence) =>
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));

    public override string SectionId => Id;
    public override int SectionVersion => RoomEnvironmentOutcomeSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.World;
    public override IReadOnlyList<string> DependsOn => Dependencies;
    protected override RoomEnvironmentOutcomeSaveData CapturePayload() =>
        persistence.CaptureOutcomeState();
    protected override RoomEnvironmentOutcomeRestoreCandidate BuildRestoreCandidate(
        RoomEnvironmentOutcomeSaveData payload) =>
        persistence.PrepareOutcomeRestore(payload);
    protected override void PublishRestoreCandidate(
        RoomEnvironmentOutcomeRestoreCandidate candidate) =>
        persistence.PublishOutcomeRestore(candidate);
}
