using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonRandomStreamStateSaveData
{
    public string streamId = string.Empty;
    public string state = "0";
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonRandomStreamsSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int rootSeed = 1;
    // Intentionally has no default value. JsonUtility materializes a missing
    // List<T> as an empty list, but preserves a missing array as null. The save
    // boundary can therefore distinguish a required stream collection that was
    // omitted from a valid, explicitly captured empty collection.
    public DungeonRandomStreamStateSaveData[] streams;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RandomStreamSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonRandomStreamsSaveData,
        RandomStreamRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
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
    public override int SectionVersion =>
        DungeonRandomStreamsSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Foundation;
    public override IReadOnlyList<string> DependsOn =>
        new[] { DungeonSaveSectionIds.RunVariables };

    protected override DungeonRandomStreamsSaveData CapturePayload()
    {
        return new DungeonRandomStreamsSaveData
        {
            rootSeed = randomStreamProvider.RootSeed,
            streams = randomStreamProvider.CaptureStates()
                .OrderBy(snapshot => snapshot.StreamId, StringComparer.Ordinal)
                .Select(snapshot => new DungeonRandomStreamStateSaveData
                {
                    streamId = snapshot.StreamId,
                    state = snapshot.State.ToString()
                })
                .ToArray()
        };
    }

    protected override RandomStreamRestoreCandidate BuildRestoreCandidate(
        DungeonRandomStreamsSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        if (payload == null || payload.streams == null)
        {
            report.AddError("Random stream payload or stream list is null.");
        }
        else
        {
            if (payload.version != DungeonRandomStreamsSaveData.CurrentVersion)
            {
                report.AddError(
                    $"Random-stream payload version {payload.version} is unsupported.");
            }
            if (payload.rootSeed == 0)
            {
                report.AddError("Random-stream root seed must be non-zero.");
            }
            HashSet<string> streamIds =
                new HashSet<string>(StringComparer.Ordinal);
            string previousStreamId = string.Empty;
            foreach (DungeonRandomStreamStateSaveData saved in payload.streams)
            {
                string streamId = saved?.streamId ?? string.Empty;
                if (saved == null
                    || streamId.Length == 0
                    || !string.Equals(
                        streamId,
                        streamId.Trim(),
                        StringComparison.Ordinal))
                {
                    report.AddError("Random stream payload contains a null or non-canonical stream ID.");
                    continue;
                }

                if (!ulong.TryParse(saved.state, out ulong state)
                    || state == 0UL
                    || !string.Equals(
                        saved.state,
                        state.ToString(),
                        StringComparison.Ordinal))
                {
                    report.AddError(
                        $"Random stream '{streamId}' contains an invalid, zero, or non-canonical state.");
                }

                if (!streamIds.Add(streamId))
                {
                    report.AddError(
                        $"Random stream payload repeats ID '{streamId}'.");
                }
                else if (previousStreamId.Length > 0
                    && string.CompareOrdinal(previousStreamId, streamId) >= 0)
                {
                    report.AddError(
                        "Random stream payload is not in canonical stream-ID order.");
                }
                else
                {
                    previousStreamId = streamId;
                }
            }
        }

        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Random-stream restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        List<RandomStreamStateSnapshot> snapshots = new();
        foreach (DungeonRandomStreamStateSaveData saved in payload.streams)
        {
            snapshots.Add(new RandomStreamStateSnapshot(
                saved.streamId,
                ulong.Parse(saved.state)));
        }
        return randomStreamProvider.BuildRestoreStates(
            payload.rootSeed,
            snapshots);
    }

    protected override void PublishRestoreCandidate(
        RandomStreamRestoreCandidate candidate) =>
        randomStreamProvider.RestoreStates(candidate);
}
