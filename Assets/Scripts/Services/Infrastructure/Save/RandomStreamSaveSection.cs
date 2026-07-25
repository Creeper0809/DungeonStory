using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

[Serializable]
public sealed class DungeonRandomStreamStateSaveData
{
    public string streamId = string.Empty;
    public string state = "0";
}

[Serializable]
public sealed class DungeonRandomStreamsSaveData
{
    public int rootSeed = 1;
    public List<DungeonRandomStreamStateSaveData> streams =
        new List<DungeonRandomStreamStateSaveData>();
}

public sealed class RandomStreamSaveSection :
    DungeonJsonSaveSection<DungeonRandomStreamsSaveData>
{
    public const string Id = "foundation.random-streams";

    private readonly IRandomStreamProvider randomStreamProvider;

    public RandomStreamSaveSection(
        IRandomStreamProvider randomStreamProvider)
    {
        this.randomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Foundation;
    public override IReadOnlyList<string> DependsOn =>
        new[] { RunVariableSaveSection.Id };

    protected override DungeonRandomStreamsSaveData CapturePayload()
    {
        return new DungeonRandomStreamsSaveData
        {
            rootSeed = randomStreamProvider.RootSeed,
            streams = randomStreamProvider.CaptureStates()
                .Select(snapshot => new DungeonRandomStreamStateSaveData
                {
                    streamId = snapshot.StreamId,
                    state = snapshot.State.ToString()
                })
                .ToList()
        };
    }

    protected override void RestorePayload(
        DungeonRandomStreamsSaveData payload,
        DungeonGameRestoreReport report)
    {
        List<RandomStreamStateSnapshot> snapshots =
            new List<RandomStreamStateSnapshot>();
        HashSet<string> streamIds =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonRandomStreamStateSaveData saved in
                 payload.streams
                 ?? new List<DungeonRandomStreamStateSaveData>())
        {
            if (saved == null
                || string.IsNullOrWhiteSpace(saved.streamId)
                || !ulong.TryParse(saved.state, out ulong state))
            {
                report.AddWarning(
                    "An invalid random stream state was skipped.");
                continue;
            }

            if (!streamIds.Add(saved.streamId))
            {
                throw new InvalidOperationException(
                    $"Duplicate random stream state '{saved.streamId}'.");
            }

            snapshots.Add(new RandomStreamStateSnapshot(
                saved.streamId,
                state));
        }

        randomStreamProvider.RestoreStates(payload.rootSeed, snapshots);
    }
}
