using System.Collections.Generic;

public interface IDefenseResponsePolicyRuntime
{
    IReadOnlyList<DefenseResponsePolicyData> Policies { get; }
    DefenseResponsePolicyData GetPolicy(IInvasionThreatSubject actor);
    string GetAssignedPolicyId(IInvasionThreatSubject actor);
    bool AssignPolicy(IInvasionThreatSubject actor, string policyId);
    bool TryCreatePolicy(string displayName, out DefenseResponsePolicyData policy);
    bool TryDuplicatePolicy(
        string sourcePolicyId,
        string displayName,
        out DefenseResponsePolicyData policy);
    bool TryUpdatePolicy(DefenseResponsePolicyData source);
    bool TryDeletePolicy(string policyId, bool reassignToStandard);
    DefenseResponsePolicySaveSnapshot Capture();
    void ReplaceFromValidatedSnapshot(
        DefenseResponsePolicySaveSnapshot snapshot);
}
