using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public sealed class WorldResourceSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonWorldResourceSaveData,
        WorldResourceRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.world-resources";

    private static readonly string[] Dependencies =
    {
        RandomStreamSaveSection.Id,
        WildlifeSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly IWorldResourcePersistence persistence;

    public WorldResourceSaveSection(IWorldResourcePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonWorldResourceSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonWorldResourceSaveData CapturePayload() =>
        persistence.Capture();

    protected override void ValidateRawPayload(string payloadJson)
    {
        var root = JObject.Parse(payloadJson, new JsonLoadSettings
        { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (root["nodes"] is not JArray nodes)
            throw new InvalidOperationException("World-resource nodes array is missing.");
        foreach (JToken node in nodes)
        {
            if (node is not JObject || node["sources"] is not JArray sources)
                throw new InvalidOperationException("World-resource sources array is missing.");
            foreach (JToken source in sources)
                if (source is not JObject || source["pendingOutput"] is not JObject pending
                    || pending["parentPublicationKey"]?.Type != JTokenType.String
                    || pending["parentResultKey"]?.Type != JTokenType.String)
                    throw new InvalidOperationException("World-resource pending publication identity is missing.");
        }
    }

    protected override WorldResourceRestoreCandidate BuildRestoreCandidate(
        DungeonWorldResourceSaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        WorldResourceRestoreCandidate candidate) =>
        persistence.Restore(candidate);
}
