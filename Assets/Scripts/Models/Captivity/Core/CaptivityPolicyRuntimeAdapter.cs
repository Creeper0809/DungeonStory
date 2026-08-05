using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityPolicyRuntime : ICaptivityPolicyRuntime
{
    private readonly CaptivityPolicyStateRuntime inner;

    public CaptivityPolicyRuntime(
        CaptivityActorAccess actors,
        ICaptivityPolicyActorPort actorPort)
    {
        if (actors == null)
        {
            throw new System.ArgumentNullException(nameof(actors));
        }
        inner = new CaptivityPolicyStateRuntime(actors.StateStore, actorPort);
    }

    public IReadOnlyList<CaptivePolicyData> Policies => inner.Policies;
    public int Sequence => inner.Sequence;
    public CaptivePolicyData DefaultPolicy => inner.DefaultPolicy;
    public CaptivePolicyData Find(string policyId) => inner.Find(policyId);

    public bool TrySetPolicy(
        string captiveId,
        string policyId,
        out string failureReason) =>
        inner.TrySetPolicy(captiveId, policyId, out failureReason);

    public bool TryCreatePolicy(
        string displayName,
        out string policyId,
        out string failureReason) =>
        inner.TryCreatePolicy(displayName, out policyId, out failureReason);

    public bool TryDuplicatePolicy(
        string sourcePolicyId,
        out string policyId,
        out string failureReason) =>
        inner.TryDuplicatePolicy(sourcePolicyId, out policyId, out failureReason);

    public bool TryUpdatePolicy(
        CaptivePolicyData policy,
        out string failureReason) =>
        inner.TryUpdatePolicy(policy, out failureReason);

    public bool TryDeletePolicy(string policyId, out string failureReason) =>
        inner.TryDeletePolicy(policyId, out failureReason);

    public List<CaptivePolicyData> CapturePolicies() => inner.CapturePolicies();

    public void Restore(
        int restoredSequence,
        IEnumerable<CaptivePolicyData> restoredPolicies,
        IList<string> warnings) =>
        inner.Restore(restoredSequence, restoredPolicies, warnings);

}
