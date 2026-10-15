#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal interface IProductionCausalActionFixture : IDisposable
{
    string ActionId { get; }
    string DomainId { get; }
    GameplayOutcomeTypeId OutcomeTypeId { get; }
    IReadOnlyList<GameplayOutcomeTypeId> CompanionOutcomeTypeIds { get; }
    IReadOnlyList<string> CompanionOutcomeRelations { get; }
    IReadOnlyDictionary<string, object> CaptureState();
    bool Execute();
    GameplayOutcomeReplayIdentity RequireExactOutcome();
    IReadOnlyList<GameplayOutcomeReplayIdentity> RequireExactCompanionOutcomes();
    JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity);
}

internal interface IProductionCausalOwnerCheckpointFixture
{
    bool HasPersistentOwner { get; }
    string OwnerId { get; }
    string OwnerKind { get; }
    string OwnerRestoreKind { get; }
    object OwnerRuntimeIdentity { get; }
    JObject CaptureOwnerCheckpoint();
}

internal interface IProductionCausalNarrativeEvidenceFixture
{
    IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> CaptureNarrativeEvidence(
        GameplayEntityId subject,
        int maximumCount);
}

internal interface IProductionCausalMemoryHistoryFixture
{
    bool EnsureCompactedMemoryHistory();
}

internal sealed class DelegateProductionCausalActionFixture :
    IProductionCausalActionFixture,
    IProductionCausalOwnerCheckpointFixture,
    IProductionCausalNarrativeEvidenceFixture
{
    private readonly Func<IReadOnlyDictionary<string, object>> capture;
    private readonly Func<bool> execute;
    private readonly Func<GameplayOutcomeReplayIdentity> requireExact;
    private readonly Func<IReadOnlyList<GameplayOutcomeReplayIdentity>>
        requireExactCompanions;
    private readonly Func<GameplayOutcomeReplayIdentity, JObject> captureOutcomeEvidence;
    private readonly Func<JObject> captureOwnerCheckpoint;
    private readonly Func<GameplayEntityId, int,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>>
        captureNarrativeEvidence;
    private readonly object ownerRuntimeIdentity;
    private readonly Action dispose;
    private bool disposed;

    internal DelegateProductionCausalActionFixture(
        string actionId,
        string domainId,
        MigratedProducerOutcomeKind outcomeKind,
        Func<IReadOnlyDictionary<string, object>> capture,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireExact,
        Func<GameplayOutcomeReplayIdentity, JObject> captureOutcomeEvidence,
        Action dispose,
        string ownerId = null,
        string ownerKind = null,
        Func<JObject> captureOwnerCheckpoint = null,
        string ownerRestoreKind = null,
        object ownerRuntimeIdentity = null,
        IEnumerable<MigratedProducerOutcomeKind> companionOutcomeKinds = null,
        Func<IReadOnlyList<GameplayOutcomeReplayIdentity>>
            requireExactCompanions = null,
        IEnumerable<string> companionOutcomeRelations = null,
        Func<GameplayEntityId, int,
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>>
            captureNarrativeEvidence = null)
        : this(
            actionId,
            domainId,
            MigratedProducerOutcomeCatalog.Get(outcomeKind).OutcomeTypeId,
            capture,
            execute,
            requireExact,
            captureOutcomeEvidence,
            dispose,
            ownerId,
            ownerKind,
            captureOwnerCheckpoint,
            ownerRestoreKind,
            ownerRuntimeIdentity,
            (companionOutcomeKinds ?? Array.Empty<MigratedProducerOutcomeKind>())
                .Select(kind => MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId),
            requireExactCompanions,
            companionOutcomeRelations,
            captureNarrativeEvidence)
    {
    }

    internal DelegateProductionCausalActionFixture(
        string actionId,
        string domainId,
        GameplayOutcomeTypeId outcomeTypeId,
        Func<IReadOnlyDictionary<string, object>> capture,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireExact,
        Func<GameplayOutcomeReplayIdentity, JObject> captureOutcomeEvidence,
        Action dispose,
        string ownerId = null,
        string ownerKind = null,
        Func<JObject> captureOwnerCheckpoint = null,
        string ownerRestoreKind = null,
        object ownerRuntimeIdentity = null,
        IEnumerable<GameplayOutcomeTypeId> companionOutcomeTypeIds = null,
        Func<IReadOnlyList<GameplayOutcomeReplayIdentity>>
            requireExactCompanions = null,
        IEnumerable<string> companionOutcomeRelations = null,
        Func<GameplayEntityId, int,
            IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>>
            captureNarrativeEvidence = null)
    {
        ActionId = CausalGameplaySyntax.RequireId(actionId, nameof(actionId));
        DomainId = CausalGameplaySyntax.RequireId(domainId, nameof(domainId));
        if (!outcomeTypeId.IsValid)
            throw new ArgumentException(
                "A valid gameplay outcome type is required.",
                nameof(outcomeTypeId));
        OutcomeTypeId = outcomeTypeId;
        CompanionOutcomeTypeIds = (companionOutcomeTypeIds
                ?? Array.Empty<GameplayOutcomeTypeId>())
            .ToArray();
        // Non-atomic post-commit observers/continuations must explicitly opt in.
        CompanionOutcomeRelations = (companionOutcomeRelations
            ?? Enumerable.Repeat("atomic-sibling", CompanionOutcomeTypeIds.Count)).ToArray();
        if (CompanionOutcomeRelations.Count != CompanionOutcomeTypeIds.Count
            || CompanionOutcomeRelations.Any(value => value is not ("atomic-sibling" or "post-commit-observer" or "post-commit-continuation")))
            throw new ArgumentException("Every companion requires one supported relation.", nameof(companionOutcomeRelations));
        if (CompanionOutcomeTypeIds.Any(value => !value.IsValid)
            || CompanionOutcomeTypeIds.Any(value => value == OutcomeTypeId)
            || CompanionOutcomeTypeIds.Distinct().Count()
                != CompanionOutcomeTypeIds.Count)
        {
            throw new ArgumentException(
                "Companion gameplay outcome types must be valid, distinct and non-primary.",
                nameof(companionOutcomeTypeIds));
        }
        if (CompanionOutcomeTypeIds.Count > 0 && requireExactCompanions == null)
        {
            throw new ArgumentNullException(nameof(requireExactCompanions));
        }
        this.requireExactCompanions = requireExactCompanions
            ?? (() => Array.Empty<GameplayOutcomeReplayIdentity>());
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        this.requireExact = requireExact
            ?? throw new ArgumentNullException(nameof(requireExact));
        this.captureOutcomeEvidence = captureOutcomeEvidence
            ?? throw new ArgumentNullException(nameof(captureOutcomeEvidence));
        this.captureNarrativeEvidence = captureNarrativeEvidence;
        this.dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        bool anyOwnerValue = !string.IsNullOrWhiteSpace(ownerId)
            || !string.IsNullOrWhiteSpace(ownerKind)
            || captureOwnerCheckpoint != null
            || !string.IsNullOrWhiteSpace(ownerRestoreKind)
            || ownerRuntimeIdentity != null;
        bool allOwnerValues = !string.IsNullOrWhiteSpace(ownerId)
            && !string.IsNullOrWhiteSpace(ownerKind)
            && captureOwnerCheckpoint != null;
        if (anyOwnerValue && !allOwnerValues)
        {
            throw new ArgumentException(
                "Persistent causal fixtures require owner ID, kind and checkpoint capture together.");
        }
        OwnerId = allOwnerValues
            ? CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId))
            : string.Empty;
        OwnerKind = allOwnerValues
            ? CausalGameplaySyntax.RequireId(ownerKind, nameof(ownerKind))
            : string.Empty;
        OwnerRestoreKind = allOwnerValues
            ? CausalGameplaySyntax.RequireId(
                string.IsNullOrWhiteSpace(ownerRestoreKind)
                    ? ActionId
                    : ownerRestoreKind,
                nameof(ownerRestoreKind))
            : string.Empty;
        this.ownerRuntimeIdentity = allOwnerValues
            ? ownerRuntimeIdentity ?? this
            : null;
        this.captureOwnerCheckpoint = captureOwnerCheckpoint;
    }

    public string ActionId { get; }
    public string DomainId { get; }
    public GameplayOutcomeTypeId OutcomeTypeId { get; }
    public IReadOnlyList<GameplayOutcomeTypeId> CompanionOutcomeTypeIds { get; }
    public IReadOnlyList<string> CompanionOutcomeRelations { get; }
    public bool HasPersistentOwner => captureOwnerCheckpoint != null;
    public string OwnerId { get; }
    public string OwnerKind { get; }
    public string OwnerRestoreKind { get; }
    public object OwnerRuntimeIdentity => ownerRuntimeIdentity;

    public IReadOnlyDictionary<string, object> CaptureState()
    {
        ThrowIfDisposed();
        IReadOnlyDictionary<string, object> state = capture();
        if (state == null)
            throw new InvalidOperationException("Causal production fixture returned no state.");
        foreach (KeyValuePair<string, object> pair in state)
        {
            CausalGameplaySyntax.RequireStateKey(pair.Key, nameof(state));
            if (pair.Value == null)
                throw new InvalidOperationException(
                    "Causal production fixture state cannot contain null values.");
        }
        return state;
    }

    public bool Execute()
    {
        ThrowIfDisposed();
        return execute();
    }

    public GameplayOutcomeReplayIdentity RequireExactOutcome()
    {
        ThrowIfDisposed();
        return requireExact();
    }

    public IReadOnlyList<GameplayOutcomeReplayIdentity>
        RequireExactCompanionOutcomes()
    {
        ThrowIfDisposed();
        IReadOnlyList<GameplayOutcomeReplayIdentity> identities =
            requireExactCompanions()
            ?? throw new InvalidOperationException(
                "Causal production fixture returned no companion outcome identities.");
        if (identities.Count != CompanionOutcomeTypeIds.Count)
        {
            throw new InvalidOperationException(
                "Causal production fixture companion outcome count changed.");
        }
        return identities;
    }

    public JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity)
    {
        ThrowIfDisposed();
        return captureOutcomeEvidence(identity)
            ?? throw new InvalidOperationException(
                "Causal production fixture returned no exact outcome evidence.");
    }

    public IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot>
        CaptureNarrativeEvidence(GameplayEntityId subject, int maximumCount)
    {
        ThrowIfDisposed();
        if (captureNarrativeEvidence == null)
            throw new InvalidOperationException(
                "This causal fixture does not expose its production narrative evidence query.");
        if (!subject.IsValid || maximumCount <= 0)
            throw new ArgumentException(
                "Narrative evidence capture requires a valid subject and positive count.");
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings =
            captureNarrativeEvidence(subject, maximumCount)
            ?? throw new InvalidOperationException(
                "Causal narrative evidence capture returned null.");
        return bindings.Select(value => value?.Clone()
                ?? throw new InvalidOperationException(
                    "Causal narrative evidence capture returned a null binding."))
            .ToArray();
    }

    public JObject CaptureOwnerCheckpoint()
    {
        ThrowIfDisposed();
        if (!HasPersistentOwner)
        {
            throw new InvalidOperationException(
                "This causal fixture has no persistent owner checkpoint.");
        }
        JObject checkpoint = captureOwnerCheckpoint()
            ?? throw new InvalidOperationException(
                "Persistent causal fixture returned no owner checkpoint.");
        if (!string.Equals(
                (string)checkpoint["ownerId"],
                OwnerId,
                StringComparison.Ordinal)
            || !string.Equals(
                (string)checkpoint["ownerKind"],
                OwnerKind,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Persistent causal fixture checkpoint identity changed.");
        }
        return checkpoint;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        dispose();
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}

internal static class ProductionCausalNarrativeEvidence
{
    internal static IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> Capture(
        GameplayOutcomeLedger ledger,
        GameplayEntityId subject,
        int maximumCount)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        GameplayOutcomeNarrativeEvidenceQuery query = new(ledger);
        IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> sources =
            query.GetForEntity(
                subject,
                maximumCount,
                minimumSalience: 0f,
                includeCompacted: true);
        List<GameplayOutcomeEvidenceBindingSnapshot> bindings =
            GameplayOutcomeEvidenceFormulaProjection.Capture(sources);
        if (bindings.Count == 0)
            throw new InvalidOperationException(
                "The production outcome ledger exposed no model-visible evidence for "
                + subject + ".");
        return bindings;
    }
}

internal static class ProductionCausalOwnerCheckpoint
{
    private const string LegacySchema =
        "dungeonstory-causal-owner-checkpoint@1";
    private const string FamilySchema =
        "dungeonstory-causal-owner-checkpoint@2";

    internal static string LedgerRunId(string ownerId, string ownerKind)
    {
        string identity = CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
        string kind = CausalGameplaySyntax.RequireId(ownerKind, nameof(ownerKind));
        string hash = ProductionCausalOutcomeEvidence.Sha256Text(
            kind + "|" + identity);
        return "run:causal-owner:" + hash.Substring("sha256:".Length);
    }

    internal static JObject Capture(
        string ownerId,
        string ownerKind,
        string actionId,
        JObject domainState,
        GameplayOutcomeLedger ledger,
        MigratedProducerOutcomeRuntime outcomes)
    {
        if (domainState == null)
            throw new ArgumentNullException(nameof(domainState));
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        if (outcomes == null)
            throw new ArgumentNullException(nameof(outcomes));
        return new JObject
        {
            ["schemaVersion"] = 1,
            ["schema"] = LegacySchema,
            ["ownerId"] = CausalGameplaySyntax.RequireId(
                ownerId,
                nameof(ownerId)),
            ["ownerKind"] = CausalGameplaySyntax.RequireId(
                ownerKind,
                nameof(ownerKind)),
            ["actionId"] = CausalGameplaySyntax.RequireId(
                actionId,
                nameof(actionId)),
            ["domainState"] = domainState.DeepClone(),
            ["outcomeLedger"] = ParseJson(
                JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()),
                "gameplay outcome ledger"),
            ["outcomeOwners"] = ParseJson(
                JsonUtility.ToJson(outcomes.Capture()),
                "migrated outcome owner revisions")
        };
    }

    internal static JObject CaptureFamily(
        string ownerId,
        string ownerKind,
        string ownerRestoreKind,
        JObject domainState,
        GameplayOutcomeLedger ledger)
    {
        if (domainState == null)
            throw new ArgumentNullException(nameof(domainState));
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        return new JObject
        {
            ["schemaVersion"] = 2,
            ["schema"] = FamilySchema,
            ["ownerId"] = CausalGameplaySyntax.RequireId(
                ownerId,
                nameof(ownerId)),
            ["ownerKind"] = CausalGameplaySyntax.RequireId(
                ownerKind,
                nameof(ownerKind)),
            ["ownerRestoreKind"] = CausalGameplaySyntax.RequireId(
                ownerRestoreKind,
                nameof(ownerRestoreKind)),
            ["domainState"] = domainState.DeepClone(),
            ["outcomeLedger"] = ParseJson(
                JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()),
                "gameplay outcome ledger")
        };
    }

    internal static JObject RestoreCommon(
        JObject checkpoint,
        string ownerId,
        string ownerKind,
        string actionId,
        GameplayOutcomeLedger ledger,
        MigratedProducerOutcomeRuntime outcomes)
    {
        if (checkpoint == null)
            return null;
        RequireFields(
            checkpoint,
            "owner checkpoint",
            "schemaVersion",
            "schema",
            "ownerId",
            "ownerKind",
            "actionId",
            "domainState",
            "outcomeLedger",
            "outcomeOwners");
        if ((int)checkpoint["schemaVersion"] != 1
            || !string.Equals((string)checkpoint["schema"], LegacySchema,
                StringComparison.Ordinal)
            || !string.Equals((string)checkpoint["ownerId"], ownerId,
                StringComparison.Ordinal)
            || !string.Equals((string)checkpoint["ownerKind"], ownerKind,
                StringComparison.Ordinal)
            || !string.Equals((string)checkpoint["actionId"], actionId,
                StringComparison.Ordinal)
            || checkpoint["domainState"] is not JObject domainState
            || checkpoint["outcomeLedger"] is not JObject ledgerJson
            || checkpoint["outcomeOwners"] is not JObject ownersJson)
        {
            throw new InvalidOperationException(
                "Persistent causal owner checkpoint contract or identity is invalid.");
        }

        DungeonMigratedProducerOutcomeSaveData ownerData = JsonUtility.FromJson<
            DungeonMigratedProducerOutcomeSaveData>(
                ownersJson.ToString(Formatting.None));
        GameplayOutcomeLedgerRestoreCandidate ledgerCandidate =
            PrepareLedgerRestore(ledger, ledgerJson);
        MigratedProducerOutcomeRestoreCandidate ownerCandidate =
            outcomes.PrepareRestore(ownerData);
        ledger.BeginRestoreCandidate();
        try
        {
            ledger.PublishGameplayOutcomeRestore(ledgerCandidate);
            ledger.PublishRestoreCandidate();
            outcomes.Restore(ownerCandidate);
            ledger.CompleteRestoreCandidate();
        }
        catch
        {
            ledger.DiscardRestoreCandidate();
            throw;
        }
        return (JObject)domainState.DeepClone();
    }

    internal static JObject RestoreFamilyCommon(
        JObject checkpoint,
        string ownerId,
        string ownerKind,
        string ownerRestoreKind,
        GameplayOutcomeLedger ledger)
    {
        if (checkpoint == null)
            return null;
        RequireFields(
            checkpoint,
            "owner-family checkpoint",
            "schemaVersion",
            "schema",
            "ownerId",
            "ownerKind",
            "ownerRestoreKind",
            "domainState",
            "outcomeLedger");
        if ((int)checkpoint["schemaVersion"] != 2
            || !string.Equals(
                (string)checkpoint["schema"],
                FamilySchema,
                StringComparison.Ordinal)
            || !string.Equals(
                (string)checkpoint["ownerId"],
                ownerId,
                StringComparison.Ordinal)
            || !string.Equals(
                (string)checkpoint["ownerKind"],
                ownerKind,
                StringComparison.Ordinal)
            || !string.Equals(
                (string)checkpoint["ownerRestoreKind"],
                ownerRestoreKind,
                StringComparison.Ordinal)
            || checkpoint["domainState"] is not JObject domainState
            || checkpoint["outcomeLedger"] is not JObject ledgerJson)
        {
            throw new InvalidOperationException(
                "Persistent causal owner-family checkpoint contract or identity is invalid.");
        }

        GameplayOutcomeLedgerRestoreCandidate ledgerCandidate =
            PrepareLedgerRestore(ledger, ledgerJson);
        ledger.BeginRestoreCandidate();
        try
        {
            ledger.PublishGameplayOutcomeRestore(ledgerCandidate);
            ledger.PublishRestoreCandidate();
            ledger.CompleteRestoreCandidate();
        }
        catch
        {
            ledger.DiscardRestoreCandidate();
            throw;
        }
        return (JObject)domainState.DeepClone();
    }

    internal static string RequireRestoreKind(JObject checkpoint)
    {
        if (checkpoint == null)
            throw new ArgumentNullException(nameof(checkpoint));
        int schemaVersion = (int?)checkpoint["schemaVersion"] ?? 0;
        string schema = (string)checkpoint["schema"] ?? string.Empty;
        if (schemaVersion == 1
            && string.Equals(schema, LegacySchema, StringComparison.Ordinal))
        {
            return CausalGameplaySyntax.RequireId(
                (string)checkpoint["actionId"],
                "legacy owner checkpoint actionId");
        }
        if (schemaVersion == 2
            && string.Equals(schema, FamilySchema, StringComparison.Ordinal))
        {
            return CausalGameplaySyntax.RequireId(
                (string)checkpoint["ownerRestoreKind"],
                "owner-family checkpoint restore kind");
        }
        throw new InvalidOperationException(
            "Persistent causal owner checkpoint schema is unsupported.");
    }

    internal static bool IsLegacy(JObject checkpoint) =>
        checkpoint != null
        && (int?)checkpoint["schemaVersion"] == 1
        && string.Equals(
            (string)checkpoint["schema"],
            LegacySchema,
            StringComparison.Ordinal);

    internal static void RequireRestoredEquivalent(
        JObject expected,
        JObject restored)
    {
        JObject expectedClone = (JObject)(expected
            ?? throw new ArgumentNullException(nameof(expected))).DeepClone();
        JObject restoredClone = (JObject)(restored
            ?? throw new ArgumentNullException(nameof(restored))).DeepClone();
        if (expectedClone["outcomeLedger"] is not JObject expectedLedger
            || restoredClone["outcomeLedger"] is not JObject restoredLedger)
        {
            throw new InvalidOperationException(
                "Restored owner checkpoint has no gameplay outcome ledger.");
        }
        long expectedEpoch = (long)expectedLedger["worldEpoch"];
        long restoredEpoch = (long)restoredLedger["worldEpoch"];
        expectedLedger.Remove("worldEpoch");
        restoredLedger.Remove("worldEpoch");
        if (restoredEpoch != checked(expectedEpoch + 1L)
            || !JToken.DeepEquals(expectedClone, restoredClone))
        {
            throw new InvalidOperationException(
                "Persistent causal owner did not survive its save restore exactly: "
                + (restoredEpoch != checked(expectedEpoch + 1L)
                    ? "worldEpoch expected " + (expectedEpoch + 1L)
                        + " but restored " + restoredEpoch
                    : DescribeFirstDifference(
                        expectedClone,
                        restoredClone,
                        "$")));
        }
    }

    private static string DescribeFirstDifference(
        JToken expected,
        JToken restored,
        string path)
    {
        if (expected == null || restored == null)
        {
            return path + " expected=" + CompactToken(expected)
                + " restored=" + CompactToken(restored);
        }
        if (expected.Type != restored.Type)
        {
            return path + " type expected=" + expected.Type
                + " restored=" + restored.Type;
        }
        if (expected is JObject expectedObject
            && restored is JObject restoredObject)
        {
            foreach (JProperty property in expectedObject.Properties())
            {
                if (!restoredObject.TryGetValue(
                        property.Name,
                        StringComparison.Ordinal,
                        out JToken restoredValue))
                {
                    return path + "." + property.Name
                        + " is absent after restore";
                }
                if (!JToken.DeepEquals(property.Value, restoredValue))
                {
                    return DescribeFirstDifference(
                        property.Value,
                        restoredValue,
                        path + "." + property.Name);
                }
            }
            JProperty unexpected = restoredObject.Properties().FirstOrDefault(
                property => expectedObject.Property(
                    property.Name,
                    StringComparison.Ordinal) == null);
            return unexpected == null
                ? path + " differs"
                : path + "." + unexpected.Name
                    + " was added after restore";
        }
        if (expected is JArray expectedArray
            && restored is JArray restoredArray)
        {
            if (expectedArray.Count != restoredArray.Count)
            {
                return path + " count expected=" + expectedArray.Count
                    + " restored=" + restoredArray.Count;
            }
            for (int index = 0; index < expectedArray.Count; index++)
            {
                if (!JToken.DeepEquals(
                        expectedArray[index],
                        restoredArray[index]))
                {
                    return DescribeFirstDifference(
                        expectedArray[index],
                        restoredArray[index],
                        path + "[" + index + "]");
                }
            }
        }
        return path + " expected=" + CompactToken(expected)
            + " restored=" + CompactToken(restored);
    }

    private static string CompactToken(JToken value)
    {
        if (value == null)
            return "<null>";
        string text = value.ToString(Formatting.None);
        return text.Length <= 240 ? text : text.Substring(0, 240) + "...";
    }

    private static JObject ParseJson(string json, string label)
    {
        try
        {
            return JObject.Parse(json ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Could not serialize persistent causal " + label + ".",
                exception);
        }
    }

    private static GameplayOutcomeLedgerRestoreCandidate PrepareLedgerRestore(
        GameplayOutcomeLedger ledger,
        JObject ledgerJson)
    {
        GameplayOutcomeLedgerSaveData ledgerData = JsonUtility.FromJson<
            GameplayOutcomeLedgerSaveData>(
                (ledgerJson ?? throw new ArgumentNullException(nameof(ledgerJson)))
                .ToString(Formatting.None));
        string expectedRunId = (ledger
                ?? throw new ArgumentNullException(nameof(ledger)))
            .CaptureGameplayOutcomes().runId;
        if (ledgerData == null
            || !string.Equals(
                ledgerData.runId,
                expectedRunId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Persistent causal outcome-ledger run identity changed.");
        }
        return ledger.PrepareGameplayOutcomeRestore(ledgerData);
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }
}

internal sealed class ProductionCausalWorldCheckpoint
{
    private const string Schema =
        "dungeonstory-causal-campaign-world-checkpoint@1";
    private readonly Dictionary<string, JObject> owners =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, IProductionCausalOwnerCheckpointFixture>
        activeOwners = new(StringComparer.Ordinal);

    internal ProductionCausalWorldCheckpoint(JObject checkpoint)
    {
        JObject source = checkpoint ?? Empty();
        RequireFields(source, "world checkpoint", "schemaVersion", "schema", "owners");
        if ((int)source["schemaVersion"] != 1
            || !string.Equals(
                (string)source["schema"],
                Schema,
                StringComparison.Ordinal)
            || source["owners"] is not JArray rows)
        {
            throw new InvalidOperationException(
                "Causal campaign world checkpoint header is invalid.");
        }
        string previous = string.Empty;
        foreach (JObject row in rows.Cast<JObject>())
        {
            string ownerId = CausalGameplaySyntax.RequireId(
                (string)row["ownerId"],
                "world checkpoint ownerId");
            if (previous.Length > 0
                && string.CompareOrdinal(previous, ownerId) >= 0)
            {
                throw new InvalidOperationException(
                    "World checkpoint owners are duplicated or unordered.");
            }
            previous = ownerId;
            owners.Add(ownerId, (JObject)row.DeepClone());
        }
        InputCheckpoint = CaptureStored();
    }

    internal JObject InputCheckpoint { get; }
    internal int RestoredOwnerCount { get; private set; }
    internal int IntroducedOwnerCount { get; private set; }
    internal int OwnerCount => owners.Count;

    internal JObject Find(string actionId, string ownerId)
        => Find(actionId, actionId, ownerId);

    internal JObject Find(
        string actionId,
        string ownerRestoreKind,
        string ownerId)
    {
        if (!owners.TryGetValue(ownerId ?? string.Empty, out JObject checkpoint))
            return null;
        string restoredKind = ProductionCausalOwnerCheckpoint.RequireRestoreKind(
            checkpoint);
        bool valid = ProductionCausalOwnerCheckpoint.IsLegacy(checkpoint)
            ? string.Equals(restoredKind, actionId, StringComparison.Ordinal)
            : string.Equals(
                restoredKind,
                ownerRestoreKind,
                StringComparison.Ordinal);
        if (!valid)
        {
            throw new InvalidOperationException(
                "A persistent campaign owner was rebound to an incompatible owner family: "
                + ownerId);
        }
        return (JObject)checkpoint.DeepClone();
    }

    internal void Bind(IProductionCausalOwnerCheckpointFixture fixture)
    {
        if (fixture == null || !fixture.HasPersistentOwner)
            return;
        if (activeOwners.TryGetValue(
                fixture.OwnerId,
                out IProductionCausalOwnerCheckpointFixture existing))
        {
            if (!string.Equals(
                    existing.OwnerRestoreKind,
                    fixture.OwnerRestoreKind,
                    StringComparison.Ordinal)
                || !ReferenceEquals(
                    existing.OwnerRuntimeIdentity,
                    fixture.OwnerRuntimeIdentity)
                || !JToken.DeepEquals(
                    existing.CaptureOwnerCheckpoint(),
                    fixture.CaptureOwnerCheckpoint()))
            {
                throw new InvalidOperationException(
                    "A persistent campaign owner was bound by different runtime authorities: "
                    + fixture.OwnerId);
            }
            return;
        }
        activeOwners.Add(fixture.OwnerId, fixture);
        JObject current = fixture.CaptureOwnerCheckpoint();
        if (owners.TryGetValue(fixture.OwnerId, out JObject expected))
        {
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(
                expected,
                current);
            owners[fixture.OwnerId] = (JObject)current.DeepClone();
            RestoredOwnerCount++;
            return;
        }
        owners.Add(fixture.OwnerId, (JObject)current.DeepClone());
        IntroducedOwnerCount++;
    }

    internal JObject Capture()
    {
        foreach (KeyValuePair<string, IProductionCausalOwnerCheckpointFixture>
                     pair in activeOwners)
        {
            JObject current = pair.Value.CaptureOwnerCheckpoint();
            if (!string.Equals(
                    (string)current["ownerId"],
                    pair.Key,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A persistent campaign owner changed identity during execution.");
            }
            owners[pair.Key] = (JObject)current.DeepClone();
        }
        return CaptureStored();
    }

    internal static JObject Empty() => new()
    {
        ["schemaVersion"] = 1,
        ["schema"] = Schema,
        ["owners"] = new JArray()
    };

    internal static void RequireEveryOwnerRestorable(
        JObject checkpoint,
        string auditRunId)
    {
        var registry = new ProductionCausalWorldCheckpoint(checkpoint);
        int index = 0;
        foreach (JObject owner in registry.owners.Values
                     .OrderBy(value => (string)value["ownerId"],
                         StringComparer.Ordinal))
        {
            string ownerId = (string)owner["ownerId"];
            IReadOnlyList<IProductionCausalActionFixture> fixtures =
                ProductionCausalOwnerRestoreRegistry.Create(
                    auditRunId + ":" + index.ToString("D4"),
                    ownerId,
                    owner);
            try
            {
                IProductionCausalOwnerCheckpointFixture[] restored = fixtures
                    .OfType<IProductionCausalOwnerCheckpointFixture>()
                    .Where(value => value.HasPersistentOwner)
                    .ToArray();
                if (restored.Length == 0
                    || restored.Any(value =>
                        !string.Equals(
                            value.OwnerId,
                            ownerId,
                            StringComparison.Ordinal))
                    || restored.Select(value => value.OwnerRestoreKind)
                        .Distinct(StringComparer.Ordinal).Count() != 1
                    || restored.Any(value => !ReferenceEquals(
                        value.OwnerRuntimeIdentity,
                        restored[0].OwnerRuntimeIdentity)))
                {
                    throw new InvalidOperationException(
                        "World checkpoint owner failed a fresh restore round trip: "
                        + ownerId);
                }
                foreach (IProductionCausalOwnerCheckpointFixture fixture in restored)
                {
                    ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(
                        owner,
                        fixture.CaptureOwnerCheckpoint());
                }
            }
            finally
            {
                foreach (IProductionCausalActionFixture fixture in fixtures)
                    fixture.Dispose();
            }
            index++;
        }
    }

    private JObject CaptureStored() => new()
    {
        ["schemaVersion"] = 1,
        ["schema"] = Schema,
        ["owners"] = new JArray(owners.Values
            .OrderBy(value => (string)value["ownerId"], StringComparer.Ordinal)
            .Select(value => value.DeepClone()))
    };

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }
}

internal static class ProductionCausalOwnerRestoreRegistry
{
    private delegate IReadOnlyList<IProductionCausalActionFixture> Factory(
        string runId,
        string ownerId,
        JObject checkpoint);

    private static readonly IReadOnlyDictionary<string, Factory> Factories =
        new Dictionary<string, Factory>(StringComparer.Ordinal)
        {
            ["action:trigger-defense-facility"] =
                (runId, ownerId, checkpoint) => new[]
                {
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .CreateCausalFacilityFixture(
                            runId,
                            MigratedProducerOutcomeFaultMode.None,
                            ownerId,
                            checkpoint)
                },
            ["action:adjust-faction-trust"] =
                (runId, ownerId, checkpoint) => new[]
                {
                    MigratedProducerRunOffenseEntryE2EDebugScenarios
                        .CreateCausalRelationshipFixture(
                            runId,
                            ownerId,
                            checkpoint)
                },
            ["action:purchase-retail-item"] =
                (runId, ownerId, checkpoint) => new[]
                {
                    MigratedProducerEconomyEntryExtendedE2EDebugScenarios
                        .CreateCausalEconomyFixture(
                            runId,
                            ownerId,
                            checkpoint)
                },
            [EnvironmentCropProductionEntryDebugScenarios
                .CausalCropOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    EnvironmentCropProductionEntryDebugScenarios
                        .CreateCausalCropFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [EnvironmentCropProductionEntryDebugScenarios
                .CertifiedSeedCausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    EnvironmentCropProductionEntryDebugScenarios
                        .CreateCertifiedSeedCausalFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [DiseaseRouteExposureCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    DiseaseRouteExposureCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [OffenseTruthRevealCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    OffenseTruthRevealCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [OffenseNodeResolutionCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    OffenseNodeResolutionCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [WorkCompletionIdentityCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    WorkCompletionIdentityCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [RunVariableCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    RunVariableCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [OwnerRunCompletionCausalProductionEntryDebugScenarios.RestoreKind] =
                (runId, ownerId, checkpoint) =>
                    OwnerRunCompletionCausalProductionEntryDebugScenarios.CreateFixtures(
                        runId, ownerId, checkpoint),
            [VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.RestoreKind] =
                (runId, ownerId, checkpoint) =>
                    VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CreateFixtures(
                        runId, ownerId, checkpoint),
            [TabooButcheryCausalProductionEntryDebugScenarios.RestoreKind] =
                (runId, ownerId, checkpoint) =>
                    TabooButcheryCausalProductionEntryDebugScenarios.CreateFixtures(
                        runId, ownerId, checkpoint),
            [OffenseArrivalCausalProductionEntryDebugScenarios.RestoreKind] =
                (runId, ownerId, checkpoint) =>
                    OffenseArrivalCausalProductionEntryDebugScenarios.CreateFixtures(
                        runId, ownerId, checkpoint),
            [FestivalCelebrationCausalProductionEntryDebugScenarios.RestoreKind] =
                (runId, ownerId, checkpoint) =>
                    FestivalCelebrationCausalProductionEntryDebugScenarios.CreateFixtures(
                        runId, ownerId, checkpoint),
            [MetaProgressionCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    MetaProgressionCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [RegularCustomerCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    RegularCustomerCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [CharacterLifeCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    CharacterLifeCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [FactionRouteCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    FactionRouteCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [ConsumablesProgressionCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    ConsumablesProgressionCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [MealShortageCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    MealShortageCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [PrimitiveSurvivalCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    PrimitiveSurvivalCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [ServiceHubDayCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    ServiceHubDayCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .InvasionLifecycleCausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .CreateInvasionLifecycleCausalFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .CombatLifeCycleOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    MigratedProducerCombatDefenseEntryE2EDebugScenarios
                        .RestoreCausalCombatLifeCycleFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [StockSupplyTransformCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    StockSupplyTransformCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [ObservedCareerArcCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    ObservedCareerArcCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [EnvironmentFirePhysicalProductionEntryDebugScenarios
                .CausalFireOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    EnvironmentFirePhysicalProductionEntryDebugScenarios
                        .CreateCausalFireFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [RoomEnvironmentCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    RoomEnvironmentCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                .SpeciesCausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                        .CreateSpeciesCausalFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [SocialLifeCausalProductionEntryDebugScenarios
                .CausalSocialOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    SocialLifeCausalProductionEntryDebugScenarios
                        .CreateCausalSocialFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [StaffRebellionResponseDebugScenarios.CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    StaffRebellionResponseDebugScenarios.CreateCausalFixtures(
                        runId,
                        ownerId,
                        checkpoint),
            [EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [TradeInventoryCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    TradeInventoryCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [CaptivityCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    CaptivityCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [EquipmentApparelCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    EquipmentApparelCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [KnowledgeResearchOutcomeDebugScenarios
                .CausalResearchOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    KnowledgeResearchOutcomeDebugScenarios
                        .CreateCausalResearchFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [ProductionPreparedOutputFullPersistenceDebugScenarios
                .CausalProductionOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    ProductionPreparedOutputFullPersistenceDebugScenarios
                        .CreateCausalProductionFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [FacilityEvolutionDebugScenarios
                .CausalFacilityEvolutionOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    FacilityEvolutionDebugScenarios
                        .CreateCausalEvolutionFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [InfrastructureCommandCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    InfrastructureCommandCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                .CausalOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                        .CreateFixtures(
                            runId,
                            ownerId,
                            checkpoint),
            [FacilitySynthesisDebugScenarios
                .CausalSynthesisOwnerRestoreKind] =
                (runId, ownerId, checkpoint) =>
                    FacilitySynthesisDebugScenarios
                        .CreateCausalSynthesisFixtures(
                            runId,
                            ownerId,
                            checkpoint)
        };

    internal static IReadOnlyList<IProductionCausalActionFixture> Create(
        string runId,
        string ownerId,
        JObject checkpoint)
    {
        string restoreKind = ProductionCausalOwnerCheckpoint
            .RequireRestoreKind(checkpoint);
        if (!Factories.TryGetValue(restoreKind, out Factory factory))
        {
            throw new InvalidOperationException(
                "World checkpoint contains an unsupported owner restore kind: "
                + restoreKind);
        }
        IReadOnlyList<IProductionCausalActionFixture> fixtures = factory(
            CausalGameplaySyntax.RequireId(runId, nameof(runId)),
            CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId)),
            checkpoint);
        if (fixtures == null || fixtures.Count == 0 || fixtures.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "Owner restore factory returned no causal fixtures: "
                + restoreKind);
        }
        return fixtures;
    }

}

internal sealed class SharedProductionCausalFixtureLifetime
{
    private readonly Action dispose;
    private int remaining;

    internal SharedProductionCausalFixtureLifetime(int referenceCount, Action dispose)
    {
        if (referenceCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(referenceCount));
        remaining = referenceCount;
        this.dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
    }

    internal void Release()
    {
        if (remaining <= 0)
            return;
        remaining--;
        if (remaining == 0)
            dispose();
    }
}

internal static class ProductionCausalOutcomeEvidence
{
    internal static JObject Capture(
        GameplayOutcomeLedger ledger,
        GameplayOutcomeReplayIdentity identity)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        if (!ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot snapshot)
            || snapshot == null)
        {
            throw new InvalidOperationException(
                "The exact gameplay outcome disappeared before evidence capture: "
                + identity.OutcomeId);
        }
        if (!string.Equals(
                identity.CanonicalPayloadHash,
                snapshot.immutablePayloadHash,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The exact gameplay outcome payload hash changed during evidence capture.");
        }
        string snapshotJsonText = JsonUtility.ToJson(snapshot);
        JObject snapshotJson = ParseSnapshotJson(snapshotJsonText);
        return new JObject
        {
            ["schemaVersion"] = 2,
            ["schema"] = "dungeonstory-causal-outcome-evidence@2",
            ["outcomeId"] = identity.OutcomeId.ToString(),
            ["resultKey"] = identity.ResultKey.ToString(),
            ["replayState"] = identity.State.ToString(),
            ["canonicalPayloadHash"] = identity.CanonicalPayloadHash,
            ["snapshotJson"] = snapshotJsonText,
            ["snapshotSha256"] = Sha256Text(snapshotJsonText),
            ["snapshot"] = snapshotJson
        };
    }

    internal static JObject ParseSnapshotJson(string json)
    {
        using var text = new StringReader(json ?? string.Empty);
        using var reader = new JsonTextReader(text)
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double
        };
        JObject result = JObject.Load(reader, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load
        });
        if (reader.Read())
        {
            throw new InvalidOperationException(
                "Trailing gameplay outcome snapshot JSON is not allowed.");
        }
        return result;
    }

    internal static string Sha256Text(string value)
    {
        using SHA256 algorithm = SHA256.Create();
        byte[] digest = algorithm.ComputeHash(
            Encoding.UTF8.GetBytes(value ?? string.Empty));
        var result = new StringBuilder("sha256:", 71);
        foreach (byte item in digest)
            result.Append(item.ToString("x2"));
        return result.ToString();
    }
}
#endif
