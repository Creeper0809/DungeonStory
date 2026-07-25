using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DefenseResponsePolicyRuntime : IDefenseResponsePolicyRuntime
{
    public const string StandardPolicyId = "defense-policy:standard";
    public const string SurvivalFirstPolicyId = "defense-policy:survival-first";
    public const string HoldTheLinePolicyId = "defense-policy:hold-the-line";

    private readonly List<DefenseResponsePolicyData> policies = new List<DefenseResponsePolicyData>();
    private readonly Dictionary<string, string> assignmentByCharacterId =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private IReadOnlyList<DefenseResponsePolicyData> policiesView;
    private int customSequence;

    public DefenseResponsePolicyRuntime()
    {
        ResetDefaults();
    }

    public IReadOnlyList<DefenseResponsePolicyData> Policies =>
        policiesView ??= ReadOnlyView.List(policies);

    public DefenseResponsePolicyData GetPolicy(CharacterActor actor)
    {
        string policyId = GetAssignedPolicyId(actor);
        return FindPolicy(policyId) ?? FindPolicy(StandardPolicyId);
    }

    public string GetAssignedPolicyId(CharacterActor actor)
    {
        string characterId = GetPersistentId(actor);
        return !string.IsNullOrWhiteSpace(characterId)
            && assignmentByCharacterId.TryGetValue(characterId, out string policyId)
            && FindPolicy(policyId) != null
                ? policyId
                : StandardPolicyId;
    }

    public bool AssignPolicy(CharacterActor actor, string policyId)
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
            policyId = $"defense-policy:custom:{++customSequence}";
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

    public void Restore(DefenseResponsePolicySaveSnapshot snapshot, IList<string> warnings)
    {
        ResetDefaults();
        if (snapshot == null)
        {
            return;
        }

        foreach (DefenseResponsePolicyData source in snapshot.policies
            ?? new List<DefenseResponsePolicyData>())
        {
            if (source == null || source.kind != DefenseResponsePolicyKind.Custom)
            {
                continue;
            }

            DefenseResponsePolicyData restored = source.Clone();
            restored.Normalize();
            if (string.IsNullOrWhiteSpace(restored.id)
                || policies.Any(policy => string.Equals(policy.id, restored.id, StringComparison.Ordinal)))
            {
                warnings?.Add("중복되거나 비어 있는 방어 정책 ID를 건너뛰었습니다.");
                continue;
            }

            policies.Add(restored);
            customSequence++;
        }

        foreach (DefensePolicyAssignmentSaveData assignment in snapshot.assignments
            ?? new List<DefensePolicyAssignmentSaveData>())
        {
            if (assignment == null || string.IsNullOrWhiteSpace(assignment.characterId))
            {
                continue;
            }

            string policyId = FindPolicy(assignment.policyId) != null
                ? assignment.policyId
                : StandardPolicyId;
            if (!string.Equals(policyId, assignment.policyId, StringComparison.Ordinal))
            {
                warnings?.Add($"{assignment.characterId}의 사라진 방어 정책을 표준 정책으로 바꿨습니다.");
            }

            assignmentByCharacterId[assignment.characterId] = policyId;
        }
    }

    private void ResetDefaults()
    {
        policies.Clear();
        assignmentByCharacterId.Clear();
        policiesView = null;
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

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor != null ? actor.Identity?.PersistentId ?? string.Empty : string.Empty;
    }
}
