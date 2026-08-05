using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface ICaptivityPolicyActorPort
{
    void ConfineLaborer(string captiveId);
}

internal interface ICaptivityPolicyRuntime
{
    IReadOnlyList<CaptivePolicyData> Policies { get; }
    int Sequence { get; }
    CaptivePolicyData DefaultPolicy { get; }
    CaptivePolicyData Find(string policyId);
    bool TrySetPolicy(string captiveId, string policyId, out string failureReason);
    bool TryCreatePolicy(string displayName, out string policyId, out string failureReason);
    bool TryDuplicatePolicy(string sourcePolicyId, out string policyId, out string failureReason);
    bool TryUpdatePolicy(CaptivePolicyData policy, out string failureReason);
    bool TryDeletePolicy(string policyId, out string failureReason);
    List<CaptivePolicyData> CapturePolicies();
    void Restore(
        int restoredSequence,
        IEnumerable<CaptivePolicyData> restoredPolicies,
        IList<string> warnings);
}

[MovedFrom(
    true,
    sourceAssembly: "Assembly-CSharp",
    sourceClassName: "CaptivityPolicyRuntime")]
internal sealed class CaptivityPolicyStateRuntime : ICaptivityPolicyRuntime
{
    private readonly CaptivityAggregateStateStore stateStore;
    private readonly ICaptivityPolicyActorPort actorPort;

    private List<CaptiveState> captives => stateStore.State.Captives;
    private List<CaptivePolicyData> policies => stateStore.State.Policies;
    private int sequence
    {
        get => stateStore.State.PolicySequence;
        set => stateStore.State.PolicySequence = value;
    }

    internal CaptivityPolicyStateRuntime(
        CaptivityAggregateStateStore stateStore,
        ICaptivityPolicyActorPort actorPort)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.actorPort = actorPort ?? throw new ArgumentNullException(nameof(actorPort));
        if (policies.Count == 0)
        {
            AddBuiltInPolicies();
        }
    }

    public IReadOnlyList<CaptivePolicyData> Policies =>
        policies.Select(policy => policy.Clone()).ToArray();

    public int Sequence => sequence;

    public CaptivePolicyData DefaultPolicy => policies[0];

    public CaptivePolicyData Find(string policyId)
    {
        string id = policyId?.Trim() ?? string.Empty;
        return policies.FirstOrDefault(policy => string.Equals(
            policy.policyId,
            id,
            StringComparison.Ordinal));
    }

    public bool TrySetPolicy(
        string captiveId,
        string policyId,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = FindCaptive(captiveId);
        CaptivePolicyData policy = Find(policyId);
        if (state == null || policy == null)
        {
            failureReason = "포로 또는 수용 정책을 찾을 수 없습니다.";
            return false;
        }

        state.policyId = policy.policyId;
        ApplyLabor(state, policy.allowedLabor);
        return true;
    }

    public bool TryCreatePolicy(
        string displayName,
        out string policyId,
        out string failureReason)
    {
        policyId = string.Empty;
        failureReason = string.Empty;
        string normalizedName = displayName?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0)
        {
            normalizedName = $"수용 정책 {sequence + 1}";
        }

        sequence++;
        policyId = $"captivity:custom:{sequence}";
        policies.Add(new CaptivePolicyData
        {
            policyId = policyId,
            displayName = normalizedName,
            allowedLabor =
                CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
            allowRansom = true,
            allowRecruitment = true,
            allowPerformance = true
        });
        return true;
    }

    public bool TryDuplicatePolicy(
        string sourcePolicyId,
        out string policyId,
        out string failureReason)
    {
        policyId = string.Empty;
        failureReason = string.Empty;
        CaptivePolicyData source = Find(sourcePolicyId);
        if (source == null)
        {
            failureReason = "복제할 수용 정책을 찾을 수 없습니다.";
            return false;
        }

        sequence++;
        CaptivePolicyData duplicate = source.Clone();
        duplicate.policyId = $"captivity:custom:{sequence}";
        duplicate.displayName = $"{source.displayName} 사본";
        policies.Add(duplicate);
        policyId = duplicate.policyId;
        return true;
    }

    public bool TryUpdatePolicy(
        CaptivePolicyData policy,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptivePolicyData target = Find(policy?.policyId);
        if (target == null)
        {
            failureReason = "수정할 수용 정책을 찾을 수 없습니다.";
            return false;
        }

        string displayName = policy.displayName?.Trim() ?? string.Empty;
        if (displayName.Length == 0)
        {
            failureReason = "수용 정책 이름은 비워 둘 수 없습니다.";
            return false;
        }

        target.displayName = displayName;
        target.allowedLabor = policy.allowedLabor & CaptiveLaborPermission.All;
        target.allowRansom = policy.allowRansom;
        target.allowRecruitment = policy.allowRecruitment;
        target.allowCorruption = policy.allowCorruption;
        target.allowPerformance = policy.allowPerformance;
        foreach (CaptiveState state in captives.Where(candidate =>
                     candidate != null
                     && string.Equals(
                         candidate.policyId,
                         target.policyId,
                         StringComparison.Ordinal)))
        {
            ApplyLabor(state, target.allowedLabor);
        }

        return true;
    }

    public bool TryDeletePolicy(
        string policyId,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptivePolicyData policy = Find(policyId);
        if (policy == null)
        {
            failureReason = "삭제할 수용 정책을 찾을 수 없습니다.";
            return false;
        }

        if (string.Equals(
                policy.policyId,
                CaptivityPolicyIds.Standard,
                StringComparison.Ordinal))
        {
            failureReason = "기본 수용 정책은 삭제할 수 없습니다.";
            return false;
        }

        CaptivePolicyData fallback = Find(CaptivityPolicyIds.Standard);
        if (fallback == null)
        {
            failureReason = "포로를 재배정할 기본 수용 정책이 없습니다.";
            return false;
        }

        foreach (CaptiveState state in captives.Where(candidate =>
                     candidate != null
                     && string.Equals(
                         candidate.policyId,
                         policy.policyId,
                         StringComparison.Ordinal)))
        {
            state.policyId = fallback.policyId;
            ApplyLabor(state, fallback.allowedLabor);
        }

        policies.Remove(policy);
        return true;
    }

    public List<CaptivePolicyData> CapturePolicies()
    {
        return policies.Select(policy => policy.Clone()).ToList();
    }

    public void Restore(
        int restoredSequence,
        IEnumerable<CaptivePolicyData> restoredPolicies,
        IList<string> warnings)
    {
        policies.Clear();
        sequence = Mathf.Max(0, restoredSequence);
        foreach (CaptivePolicyData policy in restoredPolicies
                     ?? Enumerable.Empty<CaptivePolicyData>())
        {
            if (policy == null
                || string.IsNullOrWhiteSpace(policy.policyId)
                || policies.Any(existing => string.Equals(
                    existing.policyId,
                    policy.policyId,
                    StringComparison.Ordinal)))
            {
                warnings?.Add("유효하지 않거나 중복된 포로 정책을 건너뛰었습니다.");
                continue;
            }

            policies.Add(policy.Clone());
        }

        if (policies.Count == 0)
        {
            AddBuiltInPolicies();
        }
        else
        {
            EnsureStandardPolicy();
        }
    }

    private CaptiveState FindCaptive(string captiveId)
    {
        string id = captiveId?.Trim() ?? string.Empty;
        return captives.FirstOrDefault(state => string.Equals(
            state?.captiveId,
            id,
            StringComparison.Ordinal));
    }

    private void ApplyLabor(
        CaptiveState state,
        CaptiveLaborPermission permissions)
    {
        if (state == null)
        {
            return;
        }

        state.laborPermissions = permissions & CaptiveLaborPermission.All;
        if (state.status != CaptivityStatus.Labor)
        {
            return;
        }

        if (state.laborPermissions != CaptiveLaborPermission.None)
        {
            return;
        }

        state.status = CaptivityStatus.Confined;
        actorPort.ConfineLaborer(state.captiveId);
    }

    private void AddBuiltInPolicies()
    {
        policies.Add(CreateStandardPolicy());
        policies.Add(new CaptivePolicyData
        {
            policyId = CaptivityPolicyIds.ForcedLabor,
            displayName = "강제 노역",
            allowedLabor = CaptiveLaborPermission.All,
            allowRansom = true,
            allowRecruitment = false,
            allowCorruption = true,
            allowPerformance = false
        });
        policies.Add(new CaptivePolicyData
        {
            policyId = CaptivityPolicyIds.Performer,
            displayName = "공연자 관리",
            allowedLabor =
                CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
            allowRansom = false,
            allowRecruitment = true,
            allowCorruption = false,
            allowPerformance = true
        });
        policies.Add(new CaptivePolicyData
        {
            policyId = CaptivityPolicyIds.Corruption,
            displayName = "타락 의식",
            allowedLabor = CaptiveLaborPermission.None,
            allowRansom = false,
            allowRecruitment = false,
            allowCorruption = true,
            allowPerformance = true
        });
    }

    private void EnsureStandardPolicy()
    {
        if (Find(CaptivityPolicyIds.Standard) == null)
        {
            policies.Insert(0, CreateStandardPolicy());
        }
    }

    private static CaptivePolicyData CreateStandardPolicy()
    {
        return new CaptivePolicyData
        {
            policyId = CaptivityPolicyIds.Standard,
            displayName = "표준 수용",
            allowedLabor =
                CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
            allowRansom = true,
            allowRecruitment = true,
            allowCorruption = false,
            allowPerformance = true
        };
    }
}
