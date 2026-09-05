using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum WorldResourceSourceBindingKind
{
    Visual = 0,
    RenewablePatch = 1
}

public sealed class WorldResourceSourceBinding
{
    public WorldResourceSourceBinding(
        string bindingId,
        WorldResourceSourceBindingKind kind,
        WorldResourceVisualKind visualKind,
        WildlifeHabitatType habitatType,
        WorkTypeId workTypeId,
        string recipeId)
    {
        if (!IsCanonical(bindingId)
            || !Enum.IsDefined(typeof(WorldResourceSourceBindingKind), kind)
            || !workTypeId.IsValid
            || !IsCanonical(recipeId))
        {
            throw new ArgumentException(
                "World-resource source binding is invalid.");
        }

        BindingId = bindingId;
        Kind = kind;
        VisualKind = visualKind;
        HabitatType = habitatType;
        WorkTypeId = workTypeId;
        RecipeId = recipeId;
    }

    public string BindingId { get; }
    public WorldResourceSourceBindingKind Kind { get; }
    public WorldResourceVisualKind VisualKind { get; }
    public WildlifeHabitatType HabitatType { get; }
    public WorkTypeId WorkTypeId { get; }
    public string RecipeId { get; }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IWorldResourceSourceBindingCatalog
{
    string CatalogFingerprint { get; }
    IReadOnlyList<WorldResourceSourceBinding> Bindings { get; }
    WorldResourceSourceBinding RequireVisual(WorldResourceVisualKind kind);
    WorldResourceSourceBinding RequireRenewablePatch(
        WildlifeHabitatType habitatType);
}

public interface IWorldResourceSourceBindingContributor
{
    string ContributorId { get; }
    int ContractVersion { get; }
    IReadOnlyList<WorldResourceSourceBinding> CaptureBindings();
}

public sealed class BuiltInWorldResourceSourceBindingContributor :
    IWorldResourceSourceBindingContributor
{
    public const string Id = "world-resource-source-bindings:builtin";
    public const int Version = 1;

    public string ContributorId => Id;
    public int ContractVersion => Version;

    public IReadOnlyList<WorldResourceSourceBinding> CaptureBindings() =>
        Array.AsReadOnly(new[]
        {
            new WorldResourceSourceBinding(
                "world-resource-binding:visual-tree",
                WorldResourceSourceBindingKind.Visual,
                WorldResourceVisualKind.Tree,
                default,
                BuiltInWorkTypeIds.Logging,
                "source:logging"),
            new WorldResourceSourceBinding(
                "world-resource-binding:visual-rock",
                WorldResourceSourceBindingKind.Visual,
                WorldResourceVisualKind.Rock,
                default,
                BuiltInWorkTypeIds.Quarry,
                "source:saltstone"),
            new WorldResourceSourceBinding(
                "world-resource-binding:patch-grass",
                WorldResourceSourceBindingKind.RenewablePatch,
                default,
                WildlifeHabitatType.Grass,
                BuiltInWorkTypeIds.Gather,
                "source:grass"),
            new WorldResourceSourceBinding(
                "world-resource-binding:patch-brush",
                WorldResourceSourceBindingKind.RenewablePatch,
                default,
                WildlifeHabitatType.Brush,
                BuiltInWorkTypeIds.Gather,
                "source:grass")
        });
}

public readonly struct WorldResourceOutputMaximumLineSnapshot
{
    public WorldResourceOutputMaximumLineSnapshot(
        string outputLineId,
        ProductionOutputRole role,
        string itemId,
        float inclusionProbability,
        int maximumQuantity,
        long unitMassGrams,
        long maximumMassGrams,
        string projectionSourceDigest)
    {
        if (!ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || !Enum.IsDefined(typeof(ProductionOutputRole), role)
            || !IsCanonical(itemId)
            || float.IsNaN(inclusionProbability)
            || float.IsInfinity(inclusionProbability)
            || inclusionProbability < 0f
            || inclusionProbability > 1f
            || maximumQuantity < 0
            || unitMassGrams < 0L
            || maximumMassGrams < 0L
            || maximumMassGrams != checked(unitMassGrams * maximumQuantity)
            || (maximumQuantity == 0) != (unitMassGrams == 0L)
            || (maximumQuantity == 0)
                != string.IsNullOrEmpty(projectionSourceDigest)
            || maximumQuantity > 0
                && projectionSourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "World-resource output maximum line is invalid.");
        }

        OutputLineId = outputLineId;
        Role = role;
        ItemId = itemId;
        InclusionProbability = inclusionProbability;
        MaximumQuantity = maximumQuantity;
        UnitMassGrams = unitMassGrams;
        MaximumMassGrams = maximumMassGrams;
        ProjectionSourceDigest = projectionSourceDigest ?? string.Empty;
    }

    public string OutputLineId { get; }
    public ProductionOutputRole Role { get; }
    public string ItemId { get; }
    public float InclusionProbability { get; }
    public int MaximumQuantity { get; }
    public long UnitMassGrams { get; }
    public long MaximumMassGrams { get; }
    public string ProjectionSourceDigest { get; }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class WorldResourceOutputMaximumEnvelopeSnapshot
{
    public WorldResourceOutputMaximumEnvelopeSnapshot(
        string recipeId,
        IReadOnlyList<string> bindingIds,
        string recipeSourceDigest,
        ProductionOutputFactor maximumOutputFactor,
        IReadOnlyList<WorldResourceOutputMaximumLineSnapshot> lines,
        long maximumOutputMassGrams,
        long massAuthorityRevision,
        string maximumRegistryFingerprint,
        string sourceDigest)
    {
        string[] orderedBindings = (bindingIds
                ?? throw new ArgumentNullException(nameof(bindingIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        WorldResourceOutputMaximumLineSnapshot[] orderedLines = (lines
                ?? throw new ArgumentNullException(nameof(lines)))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (!IsCanonical(recipeId)
            || orderedBindings.Length == 0
            || orderedBindings.Any(value => !IsCanonical(value))
            || orderedBindings.Distinct(StringComparer.Ordinal).Count()
                != orderedBindings.Length
            || orderedLines.Length == 0
            || orderedLines.Select(value => value.OutputLineId)
                .Distinct(StringComparer.Ordinal).Count() != orderedLines.Length
            || string.IsNullOrEmpty(recipeSourceDigest)
            || recipeSourceDigest.Length != 64
            || maximumOutputMassGrams < 0L
            || maximumOutputMassGrams != orderedLines.Sum(
                value => value.MaximumMassGrams)
            || massAuthorityRevision < 0L
            || string.IsNullOrEmpty(maximumRegistryFingerprint)
            || maximumRegistryFingerprint.Length != 64
            || string.IsNullOrEmpty(sourceDigest)
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "World-resource output maximum envelope is invalid.");
        }

        RecipeId = recipeId;
        BindingIds = Array.AsReadOnly(orderedBindings);
        RecipeSourceDigest = recipeSourceDigest;
        MaximumOutputFactor = maximumOutputFactor;
        Lines = Array.AsReadOnly(orderedLines);
        MaximumOutputMassGrams = maximumOutputMassGrams;
        MassAuthorityRevision = massAuthorityRevision;
        MaximumRegistryFingerprint = maximumRegistryFingerprint;
        SourceDigest = sourceDigest;
    }

    public string RecipeId { get; }
    public IReadOnlyList<string> BindingIds { get; }
    public string RecipeSourceDigest { get; }
    public ProductionOutputFactor MaximumOutputFactor { get; }
    public IReadOnlyList<WorldResourceOutputMaximumLineSnapshot> Lines { get; }
    public long MaximumOutputMassGrams { get; }
    public long MassAuthorityRevision { get; }
    public string MaximumRegistryFingerprint { get; }
    public string SourceDigest { get; }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IWorldResourceOutputMaximumEnvelopeAuthority
{
    string AuthorityFingerprint { get; }
    IReadOnlyList<WorldResourceOutputMaximumEnvelopeSnapshot> Envelopes { get; }
    WorldResourceOutputMaximumEnvelopeSnapshot Require(string recipeId);
}

/// <summary>
/// Registered capability bindings for natural-resource topology. Runtime
/// enumeration consumes this catalog and does not branch on recipe IDs.
/// </summary>
public sealed class WorldResourceSourceBindingCatalog :
    IWorldResourceSourceBindingCatalog
{
    private readonly IReadOnlyList<WorldResourceSourceBinding> bindings;
    private readonly IReadOnlyDictionary<WorldResourceVisualKind,
        WorldResourceSourceBinding> visualBindings;
    private readonly IReadOnlyDictionary<WildlifeHabitatType,
        WorldResourceSourceBinding> renewableBindings;

    public WorldResourceSourceBindingCatalog(
        IEnumerable<IWorldResourceSourceBindingContributor> contributors)
    {
        IWorldResourceSourceBindingContributor[] orderedContributors =
            (contributors ?? throw new ArgumentNullException(nameof(contributors)))
            .OrderBy(value => value?.ContributorId, StringComparer.Ordinal)
            .ToArray();
        if (orderedContributors.Length == 0
            || orderedContributors.Any(value => value == null
                || !IsCanonical(value.ContributorId)
                || value.ContractVersion <= 0)
            || orderedContributors.Select(value => value.ContributorId)
                .Distinct(StringComparer.Ordinal).Count()
                != orderedContributors.Length)
        {
            throw new ArgumentException(
                "World-resource binding contributors must be canonical and unique.",
                nameof(contributors));
        }
        WorldResourceSourceBinding[] values = orderedContributors
            .SelectMany(value => value.CaptureBindings()
                ?? throw new InvalidOperationException(
                    "World-resource binding contributor returned null: "
                    + value.ContributorId))
            .ToArray();
        WorldResourceSourceBinding[] ordered = values
            .OrderBy(value => value?.BindingId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null)
            || ordered.Select(value => value.BindingId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException(
                "World-resource bindings must be non-empty and unique.",
                nameof(WorldResourceSourceBindingCatalog));
        }

        Dictionary<WorldResourceVisualKind, WorldResourceSourceBinding>
            visuals = new();
        Dictionary<WildlifeHabitatType, WorldResourceSourceBinding>
            renewables = new();
        foreach (WorldResourceSourceBinding binding in ordered)
        {
            bool added = binding.Kind == WorldResourceSourceBindingKind.Visual
                ? visuals.TryAdd(binding.VisualKind, binding)
                : renewables.TryAdd(binding.HabitatType, binding);
            if (!added)
            {
                throw new ArgumentException(
                    "World-resource topology capability is bound more than once: "
                    + binding.BindingId,
                    nameof(WorldResourceSourceBindingCatalog));
            }
        }

        bindings = Array.AsReadOnly(ordered);
        visualBindings = visuals;
        renewableBindings = renewables;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("world-resource-source-binding-catalog@1");
        digest.Append(orderedContributors.Length);
        foreach (IWorldResourceSourceBindingContributor contributor
                 in orderedContributors)
        {
            digest.Append(contributor.ContributorId);
            digest.Append(contributor.ContractVersion);
            digest.Append(contributor.GetType().FullName ?? string.Empty);
        }
        digest.Append(ordered.Length);
        foreach (WorldResourceSourceBinding binding in ordered)
        {
            digest.Append(binding.BindingId);
            digest.AppendEnum(binding.Kind);
            digest.AppendEnum(binding.VisualKind);
            digest.AppendEnum(binding.HabitatType);
            digest.Append(binding.WorkTypeId.Value);
            digest.Append(binding.RecipeId);
        }
        CatalogFingerprint = digest.ComputeSha256();
    }

    public string CatalogFingerprint { get; }
    public IReadOnlyList<WorldResourceSourceBinding> Bindings => bindings;

    public WorldResourceSourceBinding RequireVisual(
        WorldResourceVisualKind kind) => visualBindings.TryGetValue(
            kind,
            out WorldResourceSourceBinding binding)
        ? binding
        : throw new InvalidOperationException(
            "World-resource visual capability is unbound: " + kind);

    public WorldResourceSourceBinding RequireRenewablePatch(
        WildlifeHabitatType habitatType) => renewableBindings.TryGetValue(
            habitatType,
            out WorldResourceSourceBinding binding)
        ? binding
        : throw new InvalidOperationException(
            "World-resource renewable capability is unbound: "
            + habitatType);

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

[Serializable]
public sealed class WorldResourceResolvedOutputLineSaveData
{
    public int deterministicOrdinal;
    public string outputLineId = string.Empty;
    public ProductionOutputRole role;
    public string itemId = string.Empty;
    public int authoredQuantity;
    public float inclusionProbability;
    public bool included;
    public int resolvedQuantity;
    public long unitMassGrams;
    public long resolvedMassGrams;

    public WorldResourceResolvedOutputLineSaveData Clone() => new()
    {
        deterministicOrdinal = deterministicOrdinal,
        outputLineId = outputLineId ?? string.Empty,
        role = role,
        itemId = itemId ?? string.Empty,
        authoredQuantity = authoredQuantity,
        inclusionProbability = inclusionProbability,
        included = included,
        resolvedQuantity = resolvedQuantity,
        unitMassGrams = unitMassGrams,
        resolvedMassGrams = resolvedMassGrams
    };
}

[Serializable]
public sealed class WorldResourcePendingOutputSaveData
{
    public int rootSeed;
    public int cycleSequence;
    public string operationId = string.Empty;
    public string recipeId = string.Empty;
    public string recipeSourceDigest = string.Empty;
    public long maximumOutputMassGrams;
    public string maximumOutputSourceDigest = string.Empty;
    public long outputFactorNumerator;
    public long outputFactorDenominator;
    public string outcomeFingerprint = string.Empty;
    public long physicalOutputMassGrams;
    public List<WorldResourceResolvedOutputLineSaveData> lines = new();

    public bool IsEmpty => rootSeed == 0
        && cycleSequence == 0
        && string.IsNullOrEmpty(operationId)
        && string.IsNullOrEmpty(recipeId)
        && string.IsNullOrEmpty(recipeSourceDigest)
        && maximumOutputMassGrams == 0L
        && string.IsNullOrEmpty(maximumOutputSourceDigest)
        && outputFactorNumerator == 0L
        && outputFactorDenominator == 0L
        && string.IsNullOrEmpty(outcomeFingerprint)
        && physicalOutputMassGrams == 0L
        && (lines == null || lines.Count == 0);

    public WorldResourcePendingOutputSaveData Clone() => new()
    {
        rootSeed = rootSeed,
        cycleSequence = cycleSequence,
        operationId = operationId ?? string.Empty,
        recipeId = recipeId ?? string.Empty,
        recipeSourceDigest = recipeSourceDigest ?? string.Empty,
        maximumOutputMassGrams = maximumOutputMassGrams,
        maximumOutputSourceDigest = maximumOutputSourceDigest ?? string.Empty,
        outputFactorNumerator = outputFactorNumerator,
        outputFactorDenominator = outputFactorDenominator,
        outcomeFingerprint = outcomeFingerprint ?? string.Empty,
        physicalOutputMassGrams = physicalOutputMassGrams,
        lines = (lines ?? new List<WorldResourceResolvedOutputLineSaveData>())
            .Select(value => value?.Clone())
            .ToList()
    };
}

public interface IWorldResourceOutputPublicationToken
{
}

public sealed class WorldResourceOutputPublicationTransaction
{
    public WorldResourceOutputPublicationTransaction(
        IWorldResourceOutputPublicationToken token)
    {
        Token = token ?? throw new ArgumentNullException(nameof(token));
    }

    public IWorldResourceOutputPublicationToken Token { get; }
}

public enum WorldResourceOutputCommitStatus
{
    Committed = 0,
    RejectedAndRolledBack = 1,
    RetryableRetained = 2,
    Poisoned = 3
}

public interface IWorldResourceOutputPublicationPort
{
    long GetDefinitionUnitMassGrams(string itemId);

    bool TryPrepare(
        WorldResourcePendingOutputSaveData pending,
        Vector2Int position,
        out WorldResourceOutputPublicationTransaction transaction,
        out string failureReason);

    WorldResourceOutputCommitStatus CommitReleased(
        WorldResourceOutputPublicationTransaction transaction,
        Vector2Int position,
        out string failureReason);

    bool TryRollback(
        WorldResourceOutputPublicationTransaction transaction,
        string reasonCode,
        out string failureReason);
}
