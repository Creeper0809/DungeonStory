using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public enum CausalGameplayPredicateOperator
{
    Equal = 0,
    NotEqual = 1,
    Less = 2,
    LessOrEqual = 3,
    Greater = 4,
    GreaterOrEqual = 5
}

public enum CausalGameplayNodeStatus
{
    Pending = 0,
    Completed = 1,
    GuardMismatch = 2,
    Diverged = 3,
    Rejected = 4,
    Invalidated = 5
}

public enum CausalGameplayLinkKind
{
    ObservedState = 0,
    CommittedOutcome = 1,
    OrderingOnly = 2
}

public sealed class CausalGameplayCausalLink
{
    public CausalGameplayCausalLink(
        string parentNodeId,
        CausalGameplayLinkKind kind,
        IEnumerable<string> stateKeys)
    {
        ParentNodeId = CausalGameplaySyntax.RequireId(
            parentNodeId,
            nameof(parentNodeId));
        Kind = kind;
        StateKeys = (stateKeys ?? Array.Empty<string>())
            .Select(value => CausalGameplaySyntax.RequireStateKey(
                value,
                nameof(stateKeys)))
            .ToArray();
        if (StateKeys.Count != StateKeys.Distinct(StringComparer.Ordinal).Count())
            throw new ArgumentException("Causal-link state keys must be unique.", nameof(stateKeys));
        if (Kind == CausalGameplayLinkKind.ObservedState && StateKeys.Count == 0)
            throw new ArgumentException(
                "An observed-state causal link requires at least one state key.",
                nameof(stateKeys));
        if (Kind != CausalGameplayLinkKind.ObservedState && StateKeys.Count != 0)
            throw new ArgumentException(
                "Only observed-state causal links may declare state keys.",
                nameof(stateKeys));
    }

    public string ParentNodeId { get; }
    public CausalGameplayLinkKind Kind { get; }
    public IReadOnlyList<string> StateKeys { get; }
}

public sealed class CausalGameplayPredicate
{
    public CausalGameplayPredicate(
        string sourceNodeId,
        string stateKey,
        CausalGameplayPredicateOperator comparison,
        JToken expected)
    {
        SourceNodeId = CausalGameplaySyntax.RequireId(
            sourceNodeId,
            nameof(sourceNodeId));
        StateKey = CausalGameplaySyntax.RequireStateKey(
            stateKey,
            nameof(stateKey));
        Comparison = comparison;
        Expected = expected?.DeepClone()
            ?? throw new ArgumentNullException(nameof(expected));
        if (Expected.Type is not (JTokenType.Boolean
                or JTokenType.Integer
                or JTokenType.Float
                or JTokenType.String))
        {
            throw new ArgumentException(
                "Causal predicates require a scalar JSON value.",
                nameof(expected));
        }
    }

    public string SourceNodeId { get; }
    public string StateKey { get; }
    public CausalGameplayPredicateOperator Comparison { get; }
    public JToken Expected { get; }
}

public sealed class CausalGameplayPlanNode
{
    public CausalGameplayPlanNode(
        string nodeId,
        string actionId,
        JObject arguments,
        IEnumerable<string> dependencyNodeIds,
        IEnumerable<CausalGameplayPredicate> guards,
        IEnumerable<CausalGameplayPredicate> expectedState,
        IEnumerable<CausalGameplayCausalLink> causalLinks = null)
    {
        NodeId = CausalGameplaySyntax.RequireId(nodeId, nameof(nodeId));
        ActionId = CausalGameplaySyntax.RequireId(actionId, nameof(actionId));
        Arguments = arguments != null
            ? (JObject)arguments.DeepClone()
            : new JObject();
        DependencyNodeIds = FreezeIds(
            dependencyNodeIds,
            nameof(dependencyNodeIds));
        Guards = FreezePredicates(guards, nameof(guards));
        ExpectedState = FreezePredicates(
            expectedState,
            nameof(expectedState));
        CausalLinks = (causalLinks ?? Array.Empty<CausalGameplayCausalLink>()).ToArray();
        if (CausalLinks.Any(value => value == null))
            throw new ArgumentException("Null causal link is not allowed.", nameof(causalLinks));
        if (CausalLinks.Count > 0)
        {
            string[] linkParents = CausalLinks.Select(value => value.ParentNodeId).ToArray();
            if (linkParents.Distinct(StringComparer.Ordinal).Count() != linkParents.Length
                || !new HashSet<string>(linkParents, StringComparer.Ordinal)
                    .SetEquals(DependencyNodeIds))
            {
                throw new ArgumentException(
                    "Causal links must describe every dependency exactly once.",
                    nameof(causalLinks));
            }
        }
    }

    public string NodeId { get; }
    public string ActionId { get; }
    public JObject Arguments { get; }
    public IReadOnlyList<string> DependencyNodeIds { get; }
    public IReadOnlyList<CausalGameplayPredicate> Guards { get; }
    public IReadOnlyList<CausalGameplayPredicate> ExpectedState { get; }
    public IReadOnlyList<CausalGameplayCausalLink> CausalLinks { get; }

    private static IReadOnlyList<string> FreezeIds(
        IEnumerable<string> values,
        string parameterName)
    {
        string[] frozen = (values ?? Array.Empty<string>())
            .Select(value => CausalGameplaySyntax.RequireId(
                value,
                parameterName))
            .ToArray();
        if (frozen.Length != frozen.Distinct(StringComparer.Ordinal).Count())
        {
            throw new ArgumentException(
                "Causal dependency IDs must be unique.",
                parameterName);
        }
        return frozen;
    }

    private static IReadOnlyList<CausalGameplayPredicate> FreezePredicates(
        IEnumerable<CausalGameplayPredicate> values,
        string parameterName)
    {
        CausalGameplayPredicate[] frozen =
            (values ?? Array.Empty<CausalGameplayPredicate>()).ToArray();
        if (frozen.Any(value => value == null))
            throw new ArgumentException("Null predicate is not allowed.", parameterName);
        return frozen;
    }
}

public sealed class CausalGameplayPlan
{
    public CausalGameplayPlan(
        string planId,
        int revision,
        string planSha256,
        string parentPlanSha256,
        string catalogVersion,
        string catalogSha256,
        IEnumerable<CausalGameplayPlanNode> nodes,
        IEnumerable<string> topologicalOrder,
        int schemaVersion = 1,
        string templateId = "",
        int seed = 0)
    {
        if (schemaVersion is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        SchemaVersion = schemaVersion;
        PlanId = CausalGameplaySyntax.RequireId(planId, nameof(planId));
        if (revision <= 0)
            throw new ArgumentOutOfRangeException(nameof(revision));
        Revision = revision;
        PlanSha256 = CausalGameplaySyntax.RequireSha256(
            planSha256,
            nameof(planSha256));
        ParentPlanSha256 = string.IsNullOrWhiteSpace(parentPlanSha256)
            ? string.Empty
            : CausalGameplaySyntax.RequireSha256(
                parentPlanSha256,
                nameof(parentPlanSha256));
        CatalogVersion = CausalGameplaySyntax.RequireId(
            catalogVersion,
            nameof(catalogVersion));
        CatalogSha256 = CausalGameplaySyntax.RequireSha256(
            catalogSha256,
            nameof(catalogSha256));
        TemplateId = schemaVersion >= 2
            ? CausalGameplaySyntax.RequireId(templateId, nameof(templateId))
            : string.Empty;
        Seed = seed;
        Nodes = (nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray();
        TopologicalOrder = (topologicalOrder
                ?? throw new ArgumentNullException(nameof(topologicalOrder)))
            .Select(value => CausalGameplaySyntax.RequireId(
                value,
                nameof(topologicalOrder)))
            .ToArray();
        ValidateGraph();
    }

    public string PlanId { get; }
    public int SchemaVersion { get; }
    public int Revision { get; }
    public string PlanSha256 { get; }
    public string ParentPlanSha256 { get; }
    public string CatalogVersion { get; }
    public string CatalogSha256 { get; }
    public string TemplateId { get; }
    public int Seed { get; }
    public IReadOnlyList<CausalGameplayPlanNode> Nodes { get; }
    public IReadOnlyList<string> TopologicalOrder { get; }

    private void ValidateGraph()
    {
        if (Nodes.Count == 0)
            throw new InvalidOperationException("A causal plan requires at least one node.");
        Dictionary<string, CausalGameplayPlanNode> byId = Nodes.ToDictionary(
            value => value.NodeId,
            StringComparer.Ordinal);
        if (byId.Count != Nodes.Count)
            throw new InvalidOperationException("Causal node IDs must be unique.");
        if (TopologicalOrder.Count != Nodes.Count
            || TopologicalOrder.Distinct(StringComparer.Ordinal).Count()
                != Nodes.Count
            || TopologicalOrder.Any(value => !byId.ContainsKey(value)))
        {
            throw new InvalidOperationException(
                "Topological order must name every node exactly once.");
        }

        var positions = TopologicalOrder
            .Select((value, index) => (value, index))
            .ToDictionary(pair => pair.value, pair => pair.index, StringComparer.Ordinal);
        foreach (CausalGameplayPlanNode node in Nodes)
        {
            foreach (string dependency in node.DependencyNodeIds)
            {
                if (!positions.TryGetValue(dependency, out int parentIndex)
                    || parentIndex >= positions[node.NodeId])
                {
                    throw new InvalidOperationException(
                        $"Node '{node.NodeId}' has a missing or non-prior dependency '{dependency}'.");
                }
            }
            foreach (CausalGameplayPredicate predicate in node.Guards)
            {
                if (!string.Equals(
                        predicate.SourceNodeId,
                        "initial",
                        StringComparison.Ordinal)
                    && !node.DependencyNodeIds.Contains(
                        predicate.SourceNodeId,
                        StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Predicate source '{predicate.SourceNodeId}' must be initial or a direct dependency of '{node.NodeId}'.");
                }
            }
            foreach (CausalGameplayPredicate predicate in node.ExpectedState)
            {
                if (!string.Equals(
                        predicate.SourceNodeId,
                        node.NodeId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Expected-state source '{predicate.SourceNodeId}' must name its own node '{node.NodeId}'.");
                }
            }
        }
    }
}

public interface ICausalGameplayExecutionWorld
{
    JObject CaptureObservedState();
    bool HasExactOutcome(string outcomeId);
}

public sealed class CausalGameplayActionContext
{
    public CausalGameplayActionContext(
        CausalGameplayPlan plan,
        CausalGameplayPlanNode node,
        ICausalGameplayExecutionWorld world)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        Node = node ?? throw new ArgumentNullException(nameof(node));
        World = world ?? throw new ArgumentNullException(nameof(world));
    }

    public CausalGameplayPlan Plan { get; }
    public CausalGameplayPlanNode Node { get; }
    public ICausalGameplayExecutionWorld World { get; }
}

public readonly struct CausalGameplayActionResult
{
    public CausalGameplayActionResult(
        bool durablyCommitted,
        string outcomeId,
        string outcomeTypeId,
        string failureCode,
        string failureReason,
        JObject outcomeEvidence = null,
        JArray companionOutcomeEvidence = null,
        JArray companionOutcomeRelations = null)
    {
        DurablyCommitted = durablyCommitted;
        OutcomeId = outcomeId?.Trim() ?? string.Empty;
        OutcomeTypeId = outcomeTypeId?.Trim() ?? string.Empty;
        FailureCode = failureCode?.Trim() ?? string.Empty;
        FailureReason = failureReason?.Trim() ?? string.Empty;
        OutcomeEvidence = outcomeEvidence != null
            ? (JObject)outcomeEvidence.DeepClone()
            : null;
        CompanionOutcomeEvidence = companionOutcomeEvidence != null
            ? (JArray)companionOutcomeEvidence.DeepClone()
            : new JArray();
        CompanionOutcomeRelations = companionOutcomeRelations != null
            ? (JArray)companionOutcomeRelations.DeepClone()
            : new JArray();
    }

    public bool DurablyCommitted { get; }
    public string OutcomeId { get; }
    public string OutcomeTypeId { get; }
    public string FailureCode { get; }
    public string FailureReason { get; }
    public JObject OutcomeEvidence { get; }
    public JArray CompanionOutcomeEvidence { get; }
    public JArray CompanionOutcomeRelations { get; }

    public static CausalGameplayActionResult Rejected(
        string code,
        string reason) => new(false, string.Empty, string.Empty, code, reason);
}

public interface ICausalGameplayActionHandler
{
    string ActionId { get; }
    string DomainId { get; }
    IReadOnlyCollection<string> ArgumentNames { get; }
    CausalGameplayActionResult Execute(CausalGameplayActionContext context);
}

public sealed class CausalGameplayActionRegistry
{
    private readonly Dictionary<string, ICausalGameplayActionHandler> handlers;

    public CausalGameplayActionRegistry(
        IEnumerable<ICausalGameplayActionHandler> handlers)
    {
        this.handlers = new Dictionary<string, ICausalGameplayActionHandler>(
            StringComparer.Ordinal);
        foreach (ICausalGameplayActionHandler handler in
                 handlers ?? throw new ArgumentNullException(nameof(handlers)))
        {
            if (handler == null)
                throw new ArgumentException("Null causal action handler is not allowed.", nameof(handlers));
            string actionId = CausalGameplaySyntax.RequireId(
                handler.ActionId,
                nameof(handlers));
            CausalGameplaySyntax.RequireId(handler.DomainId, nameof(handlers));
            if (!this.handlers.TryAdd(actionId, handler))
                throw new InvalidOperationException($"Duplicate causal action handler '{actionId}'.");
        }
        if (this.handlers.Count == 0)
            throw new InvalidOperationException("At least one causal action handler is required.");
    }

    public IReadOnlyCollection<ICausalGameplayActionHandler> Handlers =>
        handlers.Values.OrderBy(value => value.ActionId, StringComparer.Ordinal).ToArray();

    public ICausalGameplayActionHandler Require(string actionId)
    {
        if (!handlers.TryGetValue(actionId ?? string.Empty, out ICausalGameplayActionHandler handler))
            throw new InvalidOperationException($"Unregistered causal action '{actionId}'.");
        return handler;
    }

    public void ValidateArguments(CausalGameplayPlanNode node)
    {
        ICausalGameplayActionHandler handler = Require(node.ActionId);
        HashSet<string> allowed = new(
            handler.ArgumentNames ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        string unknown = node.Arguments.Properties()
            .Select(value => value.Name)
            .FirstOrDefault(value => !allowed.Contains(value));
        if (unknown != null)
        {
            throw new InvalidOperationException(
                $"Action '{node.ActionId}' does not declare argument '{unknown}'.");
        }
    }
}

public sealed class CausalGameplayNodeRecord
{
    internal CausalGameplayNodeRecord(
        CausalGameplayPlanNode node,
        CausalGameplayNodeStatus status,
        JObject before,
        JObject after,
        CausalGameplayActionResult action,
        IEnumerable<string> mismatchReasons)
    {
        NodeId = node.NodeId;
        ActionId = node.ActionId;
        Status = status;
        Before = before != null ? (JObject)before.DeepClone() : null;
        After = after != null ? (JObject)after.DeepClone() : null;
        BeforeSha256 = before != null
            ? CausalGameplayCanonicalJson.Sha256(before)
            : string.Empty;
        AfterSha256 = after != null
            ? CausalGameplayCanonicalJson.Sha256(after)
            : string.Empty;
        OutcomeId = action.OutcomeId;
        OutcomeTypeId = action.OutcomeTypeId;
        FailureCode = action.FailureCode;
        FailureReason = action.FailureReason;
        OutcomeEvidence = action.OutcomeEvidence != null
            ? (JObject)action.OutcomeEvidence.DeepClone()
            : null;
        CompanionOutcomeEvidence = action.CompanionOutcomeEvidence != null
            ? (JArray)action.CompanionOutcomeEvidence.DeepClone()
            : new JArray();
        CompanionOutcomeRelations = action.CompanionOutcomeRelations != null
            ? (JArray)action.CompanionOutcomeRelations.DeepClone()
            : new JArray();
        MismatchReasons = (mismatchReasons ?? Array.Empty<string>()).ToArray();
    }

    public string NodeId { get; }
    public string ActionId { get; }
    public CausalGameplayNodeStatus Status { get; }
    public JObject Before { get; }
    public JObject After { get; }
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }
    public string OutcomeId { get; }
    public string OutcomeTypeId { get; }
    public string FailureCode { get; }
    public string FailureReason { get; }
    public JObject OutcomeEvidence { get; }
    public JArray CompanionOutcomeEvidence { get; }
    public JArray CompanionOutcomeRelations { get; }
    public IReadOnlyList<string> MismatchReasons { get; }
}

public sealed class CausalGameplayExecutionResult
{
    internal CausalGameplayExecutionResult(
        string planId,
        int revision,
        IEnumerable<CausalGameplayNodeRecord> records,
        IEnumerable<string> invalidatedNodeIds,
        JObject finalState,
        int totalNodeCount)
    {
        if (totalNodeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalNodeCount));
        PlanId = planId;
        Revision = revision;
        Records = records.ToArray();
        if (Records.Count > totalNodeCount)
            throw new InvalidOperationException(
                "Causal execution produced more records than plan nodes.");
        InvalidatedNodeIds = invalidatedNodeIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        FinalState = (JObject)finalState.DeepClone();
        FinalStateSha256 = CausalGameplayCanonicalJson.Sha256(finalState);
        NextTopologicalIndex = Records.Count;
        IsComplete = NextTopologicalIndex == totalNodeCount;
        ReplanRequired = Records.Any(value => value.Status is
            CausalGameplayNodeStatus.GuardMismatch
            or CausalGameplayNodeStatus.Diverged
            or CausalGameplayNodeStatus.Rejected)
            || InvalidatedNodeIds.Count > 0;
    }

    public string PlanId { get; }
    public int Revision { get; }
    public IReadOnlyList<CausalGameplayNodeRecord> Records { get; }
    public IReadOnlyList<string> InvalidatedNodeIds { get; }
    public JObject FinalState { get; }
    public string FinalStateSha256 { get; }
    public int NextTopologicalIndex { get; }
    public bool IsComplete { get; }
    public bool ReplanRequired { get; }
}

public sealed class CausalGameplayPlanExecutor
{
    private readonly CausalGameplayActionRegistry registry;
    private readonly ICausalGameplayExecutionWorld world;

    public CausalGameplayPlanExecutor(
        CausalGameplayActionRegistry registry,
        ICausalGameplayExecutionWorld world)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public CausalGameplayExecutionResult Execute(CausalGameplayPlan plan)
    {
        CausalGameplayExecutionResult result = ExecuteSegment(
            plan,
            Array.Empty<CausalGameplayNodeRecord>(),
            int.MaxValue);
        if (!result.IsComplete)
            throw new InvalidOperationException("Full causal execution ended early.");
        return result;
    }

    internal CausalGameplayExecutionResult ExecuteSegment(
        CausalGameplayPlan plan,
        IReadOnlyList<CausalGameplayNodeRecord> committedPrefix,
        int maximumAdditionalNodes)
    {
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (maximumAdditionalNodes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumAdditionalNodes));
        foreach (CausalGameplayPlanNode node in plan.Nodes)
            registry.ValidateArguments(node);

        Dictionary<string, CausalGameplayPlanNode> nodes = plan.Nodes.ToDictionary(
            value => value.NodeId,
            StringComparer.Ordinal);
        CausalGameplayNodeRecord[] prefix = ValidateCommittedPrefix(
            plan,
            nodes,
            committedPrefix ?? Array.Empty<CausalGameplayNodeRecord>());
        var records = new List<CausalGameplayNodeRecord>(nodes.Count);
        records.AddRange(prefix);
        var invalidated = new HashSet<string>(StringComparer.Ordinal);
        int additionalCount = 0;
        for (int orderIndex = prefix.Length;
             orderIndex < plan.TopologicalOrder.Count;
             orderIndex++)
        {
            if (additionalCount >= maximumAdditionalNodes)
                break;
            additionalCount++;
            string nodeId = plan.TopologicalOrder[orderIndex];
            CausalGameplayPlanNode node = nodes[nodeId];
            if (invalidated.Contains(nodeId))
            {
                records.Add(new CausalGameplayNodeRecord(
                    node,
                    CausalGameplayNodeStatus.Invalidated,
                    null,
                    null,
                    default,
                    new[] { "dependency-invalidated" }));
                continue;
            }

            JObject before = world.CaptureObservedState()
                ?? throw new InvalidOperationException("Causal world returned no observed state.");
            string[] guardMismatches = Evaluate(node.Guards, before).ToArray();
            if (guardMismatches.Length > 0)
            {
                records.Add(new CausalGameplayNodeRecord(
                    node,
                    CausalGameplayNodeStatus.GuardMismatch,
                    before,
                    before,
                    CausalGameplayActionResult.Rejected(
                        "guard-mismatch",
                        string.Join("; ", guardMismatches)),
                    guardMismatches));
                InvalidateDescendants(nodeId, nodes, invalidated);
                maximumAdditionalNodes = int.MaxValue;
                continue;
            }

            ICausalGameplayActionHandler handler = registry.Require(node.ActionId);
            CausalGameplayActionResult action = handler.Execute(
                new CausalGameplayActionContext(plan, node, world));
            JObject after = world.CaptureObservedState()
                ?? throw new InvalidOperationException("Causal world returned no post-action state.");
            if (!action.DurablyCommitted)
            {
                records.Add(new CausalGameplayNodeRecord(
                    node,
                    CausalGameplayNodeStatus.Rejected,
                    before,
                    after,
                    action,
                    Array.Empty<string>()));
                InvalidateDescendants(nodeId, nodes, invalidated);
                maximumAdditionalNodes = int.MaxValue;
                continue;
            }
            if (action.OutcomeId.Length == 0
                || action.OutcomeTypeId.Length == 0
                || !world.HasExactOutcome(action.OutcomeId))
            {
                throw new InvalidOperationException(
                    $"Action '{node.ActionId}' claimed a durable result without an exact ledger outcome.");
            }
            foreach (JObject companion in action.CompanionOutcomeEvidence
                         .OfType<JObject>())
            {
                string companionOutcomeId = (string)companion["outcomeId"];
                if (string.IsNullOrWhiteSpace(companionOutcomeId)
                    || !world.HasExactOutcome(companionOutcomeId))
                {
                    throw new InvalidOperationException(
                        $"Action '{node.ActionId}' claimed a companion result without an exact ledger outcome.");
                }
            }

            string[] expectedMismatches = Evaluate(
                node.ExpectedState,
                after).ToArray();
            CausalGameplayNodeStatus status = expectedMismatches.Length == 0
                ? CausalGameplayNodeStatus.Completed
                : CausalGameplayNodeStatus.Diverged;
            records.Add(new CausalGameplayNodeRecord(
                node,
                status,
                before,
                after,
                action,
                expectedMismatches));
            if (status == CausalGameplayNodeStatus.Diverged)
            {
                InvalidateDescendants(nodeId, nodes, invalidated);
                maximumAdditionalNodes = int.MaxValue;
            }
        }

        return new CausalGameplayExecutionResult(
            plan.PlanId,
            plan.Revision,
            records,
            invalidated,
            world.CaptureObservedState(),
            plan.Nodes.Count);
    }

    private CausalGameplayNodeRecord[] ValidateCommittedPrefix(
        CausalGameplayPlan plan,
        IReadOnlyDictionary<string, CausalGameplayPlanNode> nodes,
        IReadOnlyList<CausalGameplayNodeRecord> committedPrefix)
    {
        if (committedPrefix.Count > plan.TopologicalOrder.Count)
            throw new InvalidOperationException(
                "Causal checkpoint prefix exceeds the plan length.");
        var outcomes = new HashSet<string>(StringComparer.Ordinal);
        CausalGameplayNodeRecord previous = null;
        for (int index = 0; index < committedPrefix.Count; index++)
        {
            CausalGameplayNodeRecord record = committedPrefix[index]
                ?? throw new InvalidOperationException(
                    "Causal checkpoint prefix contains a null record.");
            string expectedNodeId = plan.TopologicalOrder[index];
            CausalGameplayPlanNode node = nodes[expectedNodeId];
            if (!string.Equals(record.NodeId, expectedNodeId, StringComparison.Ordinal)
                || !string.Equals(record.ActionId, node.ActionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal checkpoint prefix does not match the plan order.");
            }
            if (record.Status != CausalGameplayNodeStatus.Completed
                || record.Before == null
                || record.After == null
                || string.IsNullOrEmpty(record.OutcomeId)
                || string.IsNullOrEmpty(record.OutcomeTypeId)
                || record.MismatchReasons.Count != 0)
            {
                throw new InvalidOperationException(
                    $"Causal checkpoint node '{record.NodeId}' is not a clean committed prefix record.");
            }
            if (!outcomes.Add(record.OutcomeId)
                || !world.HasExactOutcome(record.OutcomeId))
            {
                throw new InvalidOperationException(
                    $"Causal checkpoint outcome is absent or duplicated: {record.OutcomeId}");
            }
            foreach (JObject companion in record.CompanionOutcomeEvidence
                         .OfType<JObject>())
            {
                string companionOutcomeId = (string)companion["outcomeId"];
                if (string.IsNullOrWhiteSpace(companionOutcomeId)
                    || !outcomes.Add(companionOutcomeId)
                    || !world.HasExactOutcome(companionOutcomeId))
                {
                    throw new InvalidOperationException(
                        "Causal checkpoint companion outcome is absent or duplicated: "
                        + companionOutcomeId);
                }
            }
            if (previous != null
                && !string.Equals(
                    previous.AfterSha256,
                    record.BeforeSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Causal checkpoint state continuity failed before '{record.NodeId}'.");
            }
            previous = record;
        }
        if (previous != null)
        {
            string restoredStateHash = CausalGameplayCanonicalJson.Sha256(
                world.CaptureObservedState());
            if (!string.Equals(
                    restoredStateHash,
                    previous.AfterSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Restored causal world does not match the committed checkpoint prefix.");
            }
        }
        return committedPrefix.ToArray();
    }

    public static IEnumerable<string> Evaluate(
        IEnumerable<CausalGameplayPredicate> predicates,
        JObject state)
    {
        foreach (CausalGameplayPredicate predicate in predicates)
        {
            JToken actual = state[predicate.StateKey];
            if (actual == null)
            {
                yield return predicate.StateKey + ":missing";
                continue;
            }
            if (!Matches(actual, predicate.Expected, predicate.Comparison))
            {
                yield return predicate.StateKey + ":actual="
                    + actual.ToString(Formatting.None)
                    + ",expected="
                    + predicate.Comparison
                    + ":"
                    + predicate.Expected.ToString(Formatting.None);
            }
        }
    }

    private static bool Matches(
        JToken actual,
        JToken expected,
        CausalGameplayPredicateOperator comparison)
    {
        if (comparison == CausalGameplayPredicateOperator.Equal)
            return JToken.DeepEquals(actual, expected);
        if (comparison == CausalGameplayPredicateOperator.NotEqual)
            return !JToken.DeepEquals(actual, expected);
        if (!TryNumber(actual, out double actualNumber)
            || !TryNumber(expected, out double expectedNumber))
        {
            return false;
        }
        return comparison switch
        {
            CausalGameplayPredicateOperator.Less => actualNumber < expectedNumber,
            CausalGameplayPredicateOperator.LessOrEqual => actualNumber <= expectedNumber,
            CausalGameplayPredicateOperator.Greater => actualNumber > expectedNumber,
            CausalGameplayPredicateOperator.GreaterOrEqual => actualNumber >= expectedNumber,
            _ => false
        };
    }

    private static bool TryNumber(JToken token, out double value)
    {
        if (token.Type is JTokenType.Integer or JTokenType.Float)
        {
            value = token.Value<double>();
            return double.IsFinite(value);
        }
        value = 0d;
        return false;
    }

    private static void InvalidateDescendants(
        string failedNodeId,
        IReadOnlyDictionary<string, CausalGameplayPlanNode> nodes,
        ISet<string> invalidated)
    {
        var queue = new Queue<string>();
        queue.Enqueue(failedNodeId);
        while (queue.Count > 0)
        {
            string parent = queue.Dequeue();
            foreach (CausalGameplayPlanNode child in nodes.Values.Where(value =>
                         value.DependencyNodeIds.Contains(parent, StringComparer.Ordinal)))
            {
                if (invalidated.Add(child.NodeId))
                    queue.Enqueue(child.NodeId);
            }
        }
    }
}

public static class CausalGameplayCanonicalJson
{
    public static string Sha256(JToken value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(
            Canonicalize(value).ToString(Formatting.None));
        using SHA256 sha = SHA256.Create();
        return "sha256:" + string.Concat(sha.ComputeHash(bytes)
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }

    public static JToken Canonicalize(JToken value)
    {
        if (value == null)
            return JValue.CreateNull();
        return value.Type switch
        {
            JTokenType.Object => new JObject(((JObject)value).Properties()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => new JProperty(
                    property.Name,
                    Canonicalize(property.Value)))),
            JTokenType.Array => new JArray(((JArray)value)
                .Select(Canonicalize)),
            _ => value.DeepClone()
        };
    }
}

internal static class CausalGameplaySyntax
{
    internal static string RequireId(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > 160)
            throw new ArgumentException("A bounded non-empty ID is required.", parameterName);
        for (int index = 0; index < normalized.Length; index++)
        {
            char current = normalized[index];
            if (!(char.IsLetterOrDigit(current)
                    || current is ':' or '-' or '_' or '.' or '/'))
            {
                throw new ArgumentException(
                    $"ID '{normalized}' contains an unsupported character.",
                    parameterName);
            }
        }
        return normalized;
    }

    internal static string RequireStateKey(string value, string parameterName)
    {
        string normalized = RequireId(value, parameterName);
        if (!normalized.Contains('.'))
            throw new ArgumentException("A namespaced state key is required.", parameterName);
        return normalized;
    }

    internal static string RequireSha256(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length != 71
            || !normalized.StartsWith("sha256:", StringComparison.Ordinal)
            || normalized.Skip(7).Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A sha256:<64 hex> digest is required.", parameterName);
        }
        return normalized.ToLowerInvariant();
    }
}
