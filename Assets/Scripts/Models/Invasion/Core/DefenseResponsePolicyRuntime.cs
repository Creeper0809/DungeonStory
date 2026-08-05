using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DefenseResponsePolicyRuntime : IDefenseResponsePolicyRuntime
{
    public const string StandardPolicyId = DefenseResponsePolicyIds.Standard;
    public const string SurvivalFirstPolicyId = DefenseResponsePolicyIds.SurvivalFirst;
    public const string HoldTheLinePolicyId = DefenseResponsePolicyIds.HoldTheLine;
    private const string CustomPolicyIdPrefix = DefenseResponsePolicyIds.CustomPrefix;

    private readonly InvasionAggregateStateStore aggregateStateStore;
    private IReadOnlyList<DefenseResponsePolicyData> policiesView;
    private DefensePolicyAggregateState policiesViewState;
    private DefensePolicyAggregateState State => aggregateStateStore.State.Policies;
    private List<DefenseResponsePolicyData> policies => State.Policies;
    private Dictionary<string, string> assignmentByCharacterId =>
        State.AssignmentByCharacterId;
    private int customSequence
    {
        get => State.CustomSequence;
        set => State.CustomSequence = value;
    }

    public DefenseResponsePolicyRuntime(
        InvasionAggregateStateStore aggregateStateStore)
    {
        this.aggregateStateStore = aggregateStateStore
            ?? throw new ArgumentNullException(nameof(aggregateStateStore));
        ResetDefaults();
    }

    public IReadOnlyList<DefenseResponsePolicyData> Policies
    {
        get
        {
            DefensePolicyAggregateState state = State;
            if (!ReferenceEquals(policiesViewState, state))
            {
                policiesViewState = state;
                policiesView = ReadOnlyView.List(state.Policies);
            }
            return policiesView;
        }
    }

    public DefenseResponsePolicyData GetPolicy(IInvasionThreatSubject actor)
    {
        string policyId = GetAssignedPolicyId(actor);
        return FindPolicy(policyId) ?? FindPolicy(StandardPolicyId);
    }

    public string GetAssignedPolicyId(IInvasionThreatSubject actor)
    {
        string characterId = GetPersistentId(actor);
        return !string.IsNullOrWhiteSpace(characterId)
            && assignmentByCharacterId.TryGetValue(characterId, out string policyId)
            && FindPolicy(policyId) != null
                ? policyId
                : StandardPolicyId;
    }

    public bool AssignPolicy(IInvasionThreatSubject actor, string policyId)
    {
        string characterId = GetPersistentId(actor);
        if (string.IsNullOrWhiteSpace(characterId) || FindPolicy(policyId) == null)
        {
            return false;
        }

        assignmentByCharacterId[characterId] = policyId;
        return true;
    }

    public bool TryCreatePolicy(string displayName, out DefenseResponsePolicyData policy)
    {
        policy = null;
        string normalizedName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        string policyId;
        do
        {
            policyId = $"{CustomPolicyIdPrefix}{++customSequence}";
        }
        while (FindPolicy(policyId) != null);

        policy = new DefenseResponsePolicyData
        {
            id = policyId,
            displayName = normalizedName,
            kind = DefenseResponsePolicyKind.Custom,
            autoRespond = true,
            minimumDispatchHealthRatio = 0.4f,
            retreatHealthRatio = 0.2f,
            holdWithoutReplacement = true,
            rejoinHealthRatio = 0.6f
        };
        policies.Add(policy);
        return true;
    }

    public bool TryDuplicatePolicy(
        string sourcePolicyId,
        string displayName,
        out DefenseResponsePolicyData policy)
    {
        policy = null;
        DefenseResponsePolicyData source = FindPolicy(sourcePolicyId);
        if (source == null || !TryCreatePolicy(displayName, out policy))
        {
            return false;
        }

        policy.autoRespond = source.autoRespond;
        policy.minimumDispatchHealthRatio = source.minimumDispatchHealthRatio;
        policy.retreatHealthRatio = source.retreatHealthRatio;
        policy.holdWithoutReplacement = source.holdWithoutReplacement;
        policy.rejoinHealthRatio = source.rejoinHealthRatio;
        return true;
    }

    public bool TryUpdatePolicy(DefenseResponsePolicyData source)
    {
        if (source == null || string.IsNullOrWhiteSpace(source.id))
        {
            return false;
        }

        DefenseResponsePolicyData target = FindPolicy(source.id);
        if (target == null)
        {
            return false;
        }

        string stableId = target.id;
        DefenseResponsePolicyKind stableKind = target.kind;
        source.Normalize();
        target.displayName = string.IsNullOrWhiteSpace(source.displayName)
            ? target.displayName
            : source.displayName;
        target.autoRespond = source.autoRespond;
        target.minimumDispatchHealthRatio = source.minimumDispatchHealthRatio;
        target.retreatHealthRatio = source.retreatHealthRatio;
        target.holdWithoutReplacement = source.holdWithoutReplacement;
        target.rejoinHealthRatio = source.rejoinHealthRatio;
        target.id = stableId;
        target.kind = stableKind;
        return true;
    }

    public bool TryDeletePolicy(string policyId, bool reassignToStandard)
    {
        DefenseResponsePolicyData policy = FindPolicy(policyId);
        if (policy == null || policy.kind != DefenseResponsePolicyKind.Custom)
        {
            return false;
        }

        string[] assignedCharacters = assignmentByCharacterId
            .Where(pair => string.Equals(pair.Value, policyId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();
        if (assignedCharacters.Length > 0 && !reassignToStandard)
        {
            return false;
        }

        foreach (string characterId in assignedCharacters)
        {
            assignmentByCharacterId[characterId] = StandardPolicyId;
        }

        return policies.Remove(policy);
    }

    public DefenseResponsePolicySaveSnapshot Capture()
    {
        return new DefenseResponsePolicySaveSnapshot
        {
            policies = policies.Select(policy => policy.Clone()).ToList(),
            assignments = assignmentByCharacterId
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new DefensePolicyAssignmentSaveData
                {
                    characterId = pair.Key,
                    policyId = pair.Value
                })
                .ToList()
        };
    }

    public void ReplaceFromValidatedSnapshot(
        DefenseResponsePolicySaveSnapshot snapshot)
    {
        if (snapshot?.policies == null || snapshot.assignments == null)
        {
            throw new ArgumentException(
                "Validated defense response policy snapshot is required.",
                nameof(snapshot));
        }

        policies.Clear();
        assignmentByCharacterId.Clear();
        policiesView = null;
        policiesViewState = null;
        customSequence = 0;
        foreach (DefenseResponsePolicyData source in snapshot.policies)
        {
            DefenseResponsePolicyData restored = source?.Clone()
                ?? throw new InvalidOperationException(
                    "Validated defense policy snapshot contains null policy.");
            policies.Add(restored);
            if (restored.kind == DefenseResponsePolicyKind.Custom
                && restored.id.StartsWith(CustomPolicyIdPrefix, StringComparison.Ordinal)
                && int.TryParse(
                    restored.id.Substring(CustomPolicyIdPrefix.Length),
                    out int sequence))
            {
                customSequence = Math.Max(customSequence, sequence);
            }
        }

        foreach (DefensePolicyAssignmentSaveData assignment in snapshot.assignments)
        {
            assignmentByCharacterId.Add(
                assignment.characterId,
                assignment.policyId);
        }
    }

    private void ResetDefaults()
    {
        policies.Clear();
        assignmentByCharacterId.Clear();
        policiesView = null;
        policiesViewState = null;
        customSequence = 0;
        policies.Add(new DefenseResponsePolicyData
        {
            id = StandardPolicyId,
            displayName = "표준",
            kind = DefenseResponsePolicyKind.Standard,
            autoRespond = true,
            minimumDispatchHealthRatio = 0.4f,
            retreatHealthRatio = 0.2f,
            holdWithoutReplacement = true,
            rejoinHealthRatio = 0.6f
        });
        policies.Add(new DefenseResponsePolicyData
        {
            id = SurvivalFirstPolicyId,
            displayName = "생존 우선",
            kind = DefenseResponsePolicyKind.SurvivalFirst,
            autoRespond = true,
            minimumDispatchHealthRatio = 0.55f,
            retreatHealthRatio = 0.35f,
            holdWithoutReplacement = false,
            rejoinHealthRatio = 0.7f
        });
        policies.Add(new DefenseResponsePolicyData
        {
            id = HoldTheLinePolicyId,
            displayName = "끝까지 사수",
            kind = DefenseResponsePolicyKind.HoldTheLine,
            autoRespond = true,
            minimumDispatchHealthRatio = 0.2f,
            retreatHealthRatio = 0f,
            holdWithoutReplacement = true,
            rejoinHealthRatio = 0.2f
        });
    }

    private DefenseResponsePolicyData FindPolicy(string policyId)
    {
        return policies.FirstOrDefault(policy => policy != null
            && string.Equals(policy.id, policyId, StringComparison.Ordinal));
    }

    private static string GetPersistentId(IInvasionThreatSubject actor)
    {
        return actor != null
            ? actor.CaptureInvasionThreatSubject().CharacterId.Value
            : string.Empty;
    }
}
