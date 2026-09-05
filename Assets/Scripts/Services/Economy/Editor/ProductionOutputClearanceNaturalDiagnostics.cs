#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;

public static class ProductionOutputClearanceNaturalDiagnostics
{
    public static string CaptureTopologyDigest(
        IProductionAssemblyBridge productionBridge,
        IProductionWorkshopRuntime workshops,
        BuildableObject facility)
    {
        ProductionFacilityHandle handle = RequireHandle(
            productionBridge,
            facility,
            requireProcessProfile: false);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-topology@1");
        digest.Append(handle.InstanceId.Value);
        AppendHandle(digest, handle);
        ProductionSupportLinkSnapshot[] links = RequireWorkshops(workshops)
            .GetLinks(facility)
            .Where(value => value?.Support != null)
            .OrderBy(value => value.Support.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ThenBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(links.Length);
        foreach (ProductionSupportLinkSnapshot link in links)
        {
            string[] featureTags = CanonicalTokens(link.FeatureTags);
            digest.Append(link.Support.PersistentInstanceId.Value);
            digest.Append(link.Support.BuildingData?.id ?? -1);
            digest.Append(link.WorkstationTag ?? string.Empty);
            digest.Append(link.SupportId ?? string.Empty);
            AppendTokens(digest, featureTags);
        }
        return digest.ComputeSha256();
    }

    public static string CaptureTopologySourceDigest(
        IProductionAssemblyBridge productionBridge,
        IProductionWorkshopRuntime workshops,
        BuildableObject facility)
    {
        ProductionFacilityHandle handle = RequireHandle(
            productionBridge,
            facility,
            requireProcessProfile: true);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-topology-source@1");
        AppendHandleSource(digest, handle);
        ProductionSupportLinkSnapshot[] links = RequireWorkshops(workshops)
            .GetLinks(facility)
            .Where(value => value?.Support?.BuildingData != null)
            .OrderBy(value => ProductionFacilityDefinitionIdentity.Resolve(
                    value.Support.BuildingData),
                StringComparer.Ordinal)
            .ThenBy(value => value.SupportId, StringComparer.Ordinal)
            .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
            .ThenBy(value => value.Support.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
        digest.Append(links.Length);
        foreach (ProductionSupportLinkSnapshot link in links)
        {
            BuildingProductionSupportAbility ability =
                link.Support.BuildingData.GetProductionSupportAbility();
            if (ability == null || !ability.IsValid)
            {
                throw new InvalidOperationException(
                    "Output-clearance linked support authoring is incomplete: "
                    + ProductionFacilityDefinitionIdentity.Resolve(
                        link.Support.BuildingData));
            }
            digest.Append(ProductionFacilityDefinitionIdentity.Resolve(
                link.Support.BuildingData));
            digest.Append(link.WorkstationTag ?? string.Empty);
            digest.Append(link.SupportId ?? string.Empty);
            digest.AppendEnum(ability.kind);
            digest.Append(ability.batchCapacity);
            digest.Append(ability.requiresPower);
            digest.AppendFloat(ability.cleanWaterPerCycle);
            digest.AppendFloat(ability.wastewaterPerCycle);
            digest.AppendEnum(ability.wastewaterComposition);
            digest.Append(ability.allowsManualWaterFallback);
            digest.Append(ability.requiresFuel);
            digest.Append(ability.fuelItemId ?? string.Empty);
            digest.Append(ability.fuelPerCycle);
            digest.AppendFloat(ability.workSpeedMultiplier);
            digest.AppendFloat(ability.outputMultiplier);
            digest.AppendFloat(ability.qualityModifier);
            AppendTokens(digest, CanonicalTokens(link.FeatureTags));
            AppendTokens(digest, CanonicalTokens(ability.featureTags));
            AppendTokens(digest,
                CanonicalTokens(ability.compatibleWorkstationTags));
        }
        return digest.ComputeSha256();
    }

    public static string CaptureRandomStateDigest(
        IReadOnlyList<RandomStreamDiagnosticSnapshot> snapshots)
    {
        RandomStreamDiagnosticSnapshot[] ordered = (snapshots
                ?? Array.Empty<RandomStreamDiagnosticSnapshot>())
            .OrderBy(value => NormalizeRandomStreamId(value.StreamId),
                StringComparer.Ordinal)
            .ThenBy(value => value.StreamId, StringComparer.Ordinal)
            .ToArray();
        string[] normalizedIds = ordered
            .Select(value => NormalizeRandomStreamId(value.StreamId))
            .ToArray();
        if (normalizedIds.Distinct(StringComparer.Ordinal).Count()
            != normalizedIds.Length)
        {
            throw new InvalidOperationException(
                "Natural-clearance random stream normalization produced "
                + "duplicate IDs.");
        }
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-random-state@1");
        digest.Append(ordered.Length);
        for (int index = 0; index < ordered.Length; index++)
        {
            RandomStreamDiagnosticSnapshot snapshot = ordered[index];
            if (snapshot.DrawCount < 0L)
                throw new InvalidOperationException(
                    "Natural-clearance random draw count is negative.");
            digest.Append(normalizedIds[index]);
            digest.Append(snapshot.State.ToString(
                "x16",
                CultureInfo.InvariantCulture));
            digest.Append(snapshot.DrawCount);
        }
        return digest.ComputeSha256();
    }

    public static long CaptureRandomDrawDelta(
        IReadOnlyList<RandomStreamDiagnosticSnapshot> before,
        IReadOnlyList<RandomStreamDiagnosticSnapshot> after)
    {
        Dictionary<string, RandomStreamDiagnosticSnapshot> beforeById =
            (before ?? Array.Empty<RandomStreamDiagnosticSnapshot>())
            .ToDictionary(value => value.StreamId, StringComparer.Ordinal);
        HashSet<string> afterIds = new(StringComparer.Ordinal);
        long total = 0L;
        foreach (RandomStreamDiagnosticSnapshot snapshot in
                 after ?? Array.Empty<RandomStreamDiagnosticSnapshot>())
        {
            if (!afterIds.Add(snapshot.StreamId))
                throw new InvalidOperationException(
                    "Natural-clearance random diagnostics contain duplicate "
                    + "stream IDs.");
            long previous = beforeById.TryGetValue(snapshot.StreamId,
                    out RandomStreamDiagnosticSnapshot baseline)
                ? baseline.DrawCount
                : 0L;
            long delta = checked(snapshot.DrawCount - previous);
            if (delta < 0L)
                throw new InvalidOperationException(
                    "Natural-clearance random draw count regressed for stream: "
                    + snapshot.StreamId);
            total = checked(total + delta);
        }
        string[] missing = beforeById.Keys
            .Where(value => !afterIds.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length != 0)
            throw new InvalidOperationException(
                "Natural-clearance random streams disappeared during the seed "
                + "run: " + string.Join(",", missing));
        return total;
    }

    public static string ResolveRosterKey(string characterId)
    {
        if (string.Equals(characterId, "owner", StringComparison.Ordinal))
            return "owner";
        if (characterId?.EndsWith(":01", StringComparison.Ordinal) == true)
            return "staff:01";
        if (characterId?.EndsWith(":02", StringComparison.Ordinal) == true)
            return "staff:02";
        throw new InvalidOperationException(
            "Natural-clearance actor is outside the canonical initial-party "
            + "roster: " + (characterId ?? "<null>"));
    }

    private static ProductionFacilityHandle RequireHandle(
        IProductionAssemblyBridge productionBridge,
        BuildableObject facility,
        bool requireProcessProfile)
    {
        if (productionBridge == null || facility == null)
            throw new InvalidOperationException(
                "Output-clearance topology authority is missing.");
        ProductionFacilityHandle handle = productionBridge.CaptureFacility(
            facility);
        if (handle == null
            || handle.IsDestroyed
            || !handle.InstanceId.IsValid
            || string.IsNullOrWhiteSpace(handle.DefinitionId)
            || string.IsNullOrWhiteSpace(handle.WorkstationTag)
            || requireProcessProfile && handle.ProcessFluidProfile == null)
        {
            throw new InvalidOperationException(
                "Output-clearance facility handle is incomplete.");
        }
        return handle;
    }

    private static IProductionWorkshopRuntime RequireWorkshops(
        IProductionWorkshopRuntime workshops) =>
        workshops ?? throw new InvalidOperationException(
            "Output-clearance workshop topology authority is missing.");

    private static void AppendHandle(
        CanonicalSemanticDigestBuilder digest,
        ProductionFacilityHandle handle)
    {
        digest.Append(handle.DefinitionId);
        digest.Append(handle.WorkstationTag);
        digest.Append(handle.Position.x);
        digest.Append(handle.Position.y);
        AppendHandleCapacity(digest, handle);
    }

    private static void AppendHandleSource(
        CanonicalSemanticDigestBuilder digest,
        ProductionFacilityHandle handle)
    {
        digest.Append(handle.DefinitionId);
        digest.Append(handle.WorkstationTag);
        AppendHandleCapacity(digest, handle);
    }

    private static void AppendHandleCapacity(
        CanonicalSemanticDigestBuilder digest,
        ProductionFacilityHandle handle)
    {
        digest.Append(handle.OutputBufferCycleCapacity);
        digest.Append(handle.StockSensorInstallationItemId);
        digest.Append(handle.AllowsOverflowDump);
        digest.Append(handle.OverflowOffset.x);
        digest.Append(handle.OverflowOffset.y);
        if (handle.ProcessFluidProfile == null)
            throw new InvalidOperationException(
                "Output-clearance process-fluid profile is missing.");
        digest.Append(handle.ProcessFluidProfile.SourceDigest);
    }

    private static string NormalizeRandomStreamId(string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId)
            || !string.Equals(streamId, streamId.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Natural-clearance random stream ID is noncanonical.");
        const string DecisionPrefix = "character-ai:";
        const string MovementPrefix = "character-movement:";
        if (streamId.StartsWith(DecisionPrefix, StringComparison.Ordinal))
            return DecisionPrefix + ResolveRosterKey(
                streamId.Substring(DecisionPrefix.Length));
        if (streamId.StartsWith(MovementPrefix, StringComparison.Ordinal))
            return MovementPrefix + ResolveRosterKey(
                streamId.Substring(MovementPrefix.Length));
        return streamId;
    }

    private static string[] CanonicalTokens(IEnumerable<string> values) =>
        (values ?? Array.Empty<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static void AppendTokens(
        CanonicalSemanticDigestBuilder digest,
        IReadOnlyList<string> values)
    {
        digest.Append(values?.Count ?? 0);
        if (values == null)
            return;
        for (int index = 0; index < values.Count; index++)
            digest.Append(values[index]);
    }
}
#endif
