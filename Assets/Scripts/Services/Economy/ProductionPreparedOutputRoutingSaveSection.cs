using System;
using System.Collections.Generic;

public sealed class ProductionPreparedOutputRoutingSaveSection :
    DungeonStrictJsonSaveSection<
        ProductionPreparedOutputRoutingSaveData,
        ProductionPreparedOutputRoutingRestoreJoinPlan>
{
    public const string Id = "economy.production-prepared-output-routing";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ProductionBillsSaveSection.Id
    };

    private readonly IProductionPreparedOutputRoutingPersistence persistence;
    private readonly IProductionPreparedOutputRoutingRestoreJoin restoreJoin;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public ProductionPreparedOutputRoutingSaveSection(
        IProductionPreparedOutputRoutingPersistence persistence,
        IProductionPreparedOutputRoutingRestoreJoin restoreJoin,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.restoreJoin = restoreJoin
            ?? throw new ArgumentNullException(nameof(restoreJoin));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        ProductionPreparedOutputRoutingSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override ProductionPreparedOutputRoutingSaveData CapturePayload() =>
        persistence.Capture();

    protected override ProductionPreparedOutputRoutingRestoreJoinPlan
        BuildRestoreCandidate(ProductionPreparedOutputRoutingSaveData payload) =>
        restoreJoin.Build(persistence.BuildRestoreCandidate(payload));

    protected override void ValidateParsedPayload(
        ProductionPreparedOutputRoutingSaveData payload)
    {
        // Cross-section candidate indexes do not exist during registry
        // preflight. Keep the routing payload's own validation here and defer
        // the Physical + Production join to detached dependency-ordered
        // staging through BuildRestoreCandidate.
        _ = persistence.BuildRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        ProductionPreparedOutputRoutingRestoreJoinPlan candidate)
    {
        if (candidate == null || !candidate.JoinValidated)
            throw new InvalidOperationException(
                "Prepared-output routing restore cannot publish without its physical join.");
        persistence.Restore(candidate.Candidate);
        restoreJoin.Reconcile(candidate);
    }

    protected override void PublishRestoreCandidateProjection(
        ProductionPreparedOutputRoutingSaveData payload,
        ProductionPreparedOutputRoutingRestoreJoinPlan candidate) =>
        lifecycleRestoreCandidates.SetRouting(candidate.Candidate);

    protected override void ValidateRawPayload(string payloadJson)
    {
        RequireTopLevelArrayFields(payloadJson, "batches");
        if (!HasTopLevelProperty(payloadJson, "version")
            || !HasTopLevelProperty(
                payloadJson,
                "lastConfirmedCheckpointSequence")
            || !HasTopLevelProperty(
                payloadJson,
                "lastConfirmedCheckpointDigest"))
        {
            throw new InvalidOperationException(
                "Prepared-output routing current schema is missing a required scalar field.");
        }
    }

    private static bool HasTopLevelProperty(string json, string propertyName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            return false;
        int depth = 0;
        for (int index = 0; index < json.Length; index++)
        {
            char character = json[index];
            if (character == '{' || character == '[')
            {
                depth++;
                continue;
            }
            if (character == '}' || character == ']')
            {
                depth--;
                continue;
            }
            if (character != '"')
                continue;

            int start = ++index;
            bool escaped = false;
            while (index < json.Length)
            {
                char current = json[index];
                if (!escaped && current == '"')
                    break;
                escaped = !escaped && current == '\\';
                if (current != '\\')
                    escaped = false;
                index++;
            }
            if (depth != 1
                || index >= json.Length
                || index - start != propertyName.Length
                || string.CompareOrdinal(
                    json,
                    start,
                    propertyName,
                    0,
                    propertyName.Length) != 0)
            {
                continue;
            }
            int separator = index + 1;
            while (separator < json.Length
                   && char.IsWhiteSpace(json[separator]))
            {
                separator++;
            }
            if (separator < json.Length && json[separator] == ':')
                return true;
        }
        return false;
    }
}
