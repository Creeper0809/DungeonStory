using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

[Serializable]
public sealed class GameWorldBranchAncestorData
{
    public string worldInstanceId;
    public string runInstanceId;
    public string branchInstanceId;
}

[Serializable]
public sealed class GameWorldLineageSaveData
{
    public const int CurrentVersion = 1;
    public int version;
    public string worldInstanceId;
    public string campaignInstanceId;
    public string runInstanceId;
    public string branchInstanceId;
    public string ledgerRunId;
    public GameWorldBranchAncestorData[] branchAncestors;
}

// Immutable runtime identity. Export/package names never allocate or replace it.
public sealed class GameWorldLineageSnapshot
{
    private readonly GameWorldLineageSaveData data;
    private GameWorldLineageSnapshot(GameWorldLineageSaveData value) { data = Clone(value); }
    public string WorldInstanceId => data.worldInstanceId;
    public string CampaignInstanceId => data.campaignInstanceId;
    public string RunInstanceId => data.runInstanceId;
    public string BranchInstanceId => data.branchInstanceId;
    public string LedgerRunId => data.ledgerRunId;
    public GameWorldLineageSaveData Capture() => Clone(data);

    internal static GameWorldLineageSnapshot Create(string ledgerRunId) => From(new GameWorldLineageSaveData
    {
        version = GameWorldLineageSaveData.CurrentVersion,
        worldInstanceId = NewId("world:"), campaignInstanceId = NewId("campaign:"),
        runInstanceId = NewId("run-instance:"), branchInstanceId = NewId("branch:"),
        ledgerRunId = ledgerRunId, branchAncestors = Array.Empty<GameWorldBranchAncestorData>()
    });

    public static GameWorldLineageSnapshot From(GameWorldLineageSaveData value)
    {
        if (value == null || value.version != GameWorldLineageSaveData.CurrentVersion
            || value.branchAncestors == null || value.branchAncestors.Length > 128)
            throw new InvalidOperationException("world-lineage-shape-invalid");
        RequireId(value.worldInstanceId, "world:");
        RequireId(value.campaignInstanceId, "campaign:");
        RequireId(value.runInstanceId, "run-instance:");
        RequireId(value.branchInstanceId, "branch:");
        if (!new GameplayOutcomeRunId(value.ledgerRunId).IsValid)
            throw new InvalidOperationException("world-lineage-ledger-id-invalid");
        var branches = new HashSet<string>(StringComparer.Ordinal) { value.branchInstanceId };
        foreach (GameWorldBranchAncestorData ancestor in value.branchAncestors)
        {
            if (ancestor == null || ancestor.worldInstanceId != value.worldInstanceId
                || ancestor.runInstanceId != value.runInstanceId || !branches.Add(ancestor.branchInstanceId))
                throw new InvalidOperationException("world-lineage-ancestor-invalid");
            RequireId(ancestor.branchInstanceId, "branch:");
        }
        return new GameWorldLineageSnapshot(value);
    }

    internal GameWorldLineageSnapshot Fork()
    {
        GameWorldLineageSaveData candidate = Capture();
        candidate.branchAncestors = candidate.branchAncestors.Concat(new[]
        {
            new GameWorldBranchAncestorData { worldInstanceId = WorldInstanceId,
                runInstanceId = RunInstanceId, branchInstanceId = BranchInstanceId }
        }).ToArray();
        candidate.branchInstanceId = NewId("branch:");
        return From(candidate);
    }

    private static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N");
    private static void RequireId(string value, string prefix)
    {
        if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal)
            || value.Length != prefix.Length + 32
            || !Guid.TryParseExact(value.Substring(prefix.Length), "N", out Guid guid)
            || guid == Guid.Empty || value != prefix + guid.ToString("N"))
            throw new InvalidOperationException("world-lineage-id-invalid:" + prefix);
    }
    private static GameWorldLineageSaveData Clone(GameWorldLineageSaveData value) => new()
    {
        version = value.version, worldInstanceId = value.worldInstanceId,
        campaignInstanceId = value.campaignInstanceId, runInstanceId = value.runInstanceId,
        branchInstanceId = value.branchInstanceId, ledgerRunId = value.ledgerRunId,
        branchAncestors = value.branchAncestors.Select(item => new GameWorldBranchAncestorData
        { worldInstanceId = item.worldInstanceId, runInstanceId = item.runInstanceId,
            branchInstanceId = item.branchInstanceId }).ToArray()
    };
}

public interface IGameWorldLineageQuery { GameWorldLineageSnapshot Snapshot { get; } }
public interface IGameWorldLineageCommands { void ForkBranch(string expectedBranchInstanceId); }

public sealed class GameWorldLineageRuntime : IGameWorldLineageQuery,
    IGameWorldLineageCommands, IDungeonRestoreTransactionParticipant
{
    private GameWorldLineageSnapshot current;
    private GameWorldLineageSnapshot previous;
    private GameWorldLineageSnapshot candidate;
    public GameWorldLineageRuntime(IGameplayOutcomePersistence outcomes)
    {
        current = GameWorldLineageSnapshot.Create((outcomes ?? throw new ArgumentNullException(nameof(outcomes)))
            .CaptureGameplayOutcomes().runId);
    }
    public GameWorldLineageSnapshot Snapshot => current;
    public string ParticipantId => GameWorldLineageSaveSection.Id;
    public void ForkBranch(string expectedBranchInstanceId)
    {
        if (previous != null || expectedBranchInstanceId != current.BranchInstanceId)
            throw new InvalidOperationException("world-lineage-fork-conflict");
        current = current.Fork();
    }
    internal void Stage(GameWorldLineageSnapshot value)
    {
        if (previous == null || candidate != null || value == null)
            throw new InvalidOperationException("world-lineage-stage-invalid");
        candidate = value;
    }
    public void BeginRestoreCandidate()
    {
        if (previous != null || candidate != null) throw new InvalidOperationException("world-lineage-restore-active");
        previous = current;
    }
    public void PublishRestoreCandidate()
    {
        if (previous == null || candidate == null) throw new InvalidOperationException("world-lineage-candidate-missing");
        current = candidate;
    }
    public void RollbackPublishedRestoreCandidate()
    {
        if (previous != null) current = previous;
        DiscardRestoreCandidate();
    }
    public void CompleteRestoreCandidate() { previous = null; candidate = null; }
    public void DiscardRestoreCandidate() { previous = null; candidate = null; }
}

public sealed class GameWorldLineageSaveSection :
    DungeonStrictJsonSaveSection<GameWorldLineageSaveData, GameWorldLineageSnapshot>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "foundation.world-lineage";
    private readonly GameWorldLineageRuntime owner;
    public GameWorldLineageSaveSection(GameWorldLineageRuntime owner) { this.owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
    public override string SectionId => Id;
    public override int SectionVersion => GameWorldLineageSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Foundation;
    protected override GameWorldLineageSaveData CapturePayload() => owner.Snapshot.Capture();
    protected override GameWorldLineageSnapshot BuildRestoreCandidate(GameWorldLineageSaveData payload) => GameWorldLineageSnapshot.From(payload);
    protected override void PublishRestoreCandidate(GameWorldLineageSnapshot value) => owner.Stage(value);
    protected override void ValidateRawPayload(string json) => ReadSnapshot(json);

    public static GameWorldLineageSnapshot ReadSnapshot(string json)
    {
        JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        string[] fields = { "version", "worldInstanceId", "campaignInstanceId", "runInstanceId",
            "branchInstanceId", "ledgerRunId", "branchAncestors" };
        if (!new HashSet<string>(root.Properties().Select(p => p.Name)).SetEquals(fields)
            || root["version"].Type != JTokenType.Integer || root["branchAncestors"] is not JArray ancestors
            || fields.Skip(1).Take(5).Any(key => root[key].Type != JTokenType.String))
            throw new InvalidOperationException("world-lineage-required-fields-invalid");
        foreach (JToken token in ancestors)
            if (token is not JObject row || !new HashSet<string>(row.Properties().Select(p => p.Name))
                .SetEquals(new[] { "worldInstanceId", "runInstanceId", "branchInstanceId" })
                || row.Properties().Any(p => p.Value.Type != JTokenType.String))
                throw new InvalidOperationException("world-lineage-ancestor-fields-invalid");
        return GameWorldLineageSnapshot.From(root.ToObject<GameWorldLineageSaveData>());
    }
}

public sealed class GameWorldLineageSaveValidation : IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator, IDungeonSaveRegistryPreflightValidator
{
    public void Validate(DungeonGameSaveData data, DungeonGameRestoreReport report) =>
        Validate(data.sections.ToDictionary(s => s.sectionId, StringComparer.Ordinal), report);
    public void Validate(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections, DungeonGameRestoreReport report)
    {
        if (!sections.TryGetValue(GameWorldLineageSaveSection.Id, out var lineage)
            || !sections.TryGetValue(GameplayOutcomeLedgerSaveSection.Id, out var ledger))
        { report.AddError("world-lineage-or-ledger-section-missing"); return; }
        GameWorldLineageSnapshot snapshot = GameWorldLineageSaveSection.ReadSnapshot(lineage.payloadJson);
        JObject ledgerData = JObject.Parse(ledger.payloadJson,
            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (snapshot.LedgerRunId != (string)ledgerData["runId"])
            report.AddError("world-lineage-ledger-run-mismatch");
    }
}
