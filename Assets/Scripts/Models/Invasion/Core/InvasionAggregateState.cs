using System;
using System.Collections.Generic;
using System.Linq;

public sealed class InvasionAggregateState
{
    public InvasionThreatAggregateState Threat = new();
    public DefensePolicyAggregateState Policies = new();
    public InvasionCampaignAggregateState Campaign = new();

    public InvasionAggregateState Clone()
    {
        return new InvasionAggregateState
        {
            Threat = Threat.Clone(),
            Policies = Policies.Clone(),
            Campaign = Campaign.Clone()
        };
    }
}

public sealed class InvasionThreatAggregateState
{
    public float CurrentThreat;
    public float SecondsSinceLastInvasion;
    public float SafetyRemaining;
    public float CandidateDelayRemaining = -1f;
    public float WarningCooldownRemaining;
    public bool WarningRaisedThisCycle;
    public bool CandidateRaisedThisCycle;
    public float ResidualRisk;
    public InvasionThreatFactors LastFactors;

    public InvasionThreatAggregateState Clone()
    {
        return (InvasionThreatAggregateState)MemberwiseClone();
    }
}

public sealed class DefensePolicyAggregateState
{
    public readonly List<DefenseResponsePolicyData> Policies = new();
    public readonly Dictionary<string, string> AssignmentByCharacterId =
        new(StringComparer.Ordinal);
    public int CustomSequence;

    public DefensePolicyAggregateState Clone()
    {
        DefensePolicyAggregateState clone = new()
        {
            CustomSequence = CustomSequence
        };
        clone.Policies.AddRange(Policies.Select(policy => policy.Clone()));
        foreach (KeyValuePair<string, string> assignment in
                 AssignmentByCharacterId)
        {
            clone.AssignmentByCharacterId.Add(
                assignment.Key,
                assignment.Value);
        }
        return clone;
    }
}

public sealed class InvasionCampaignAggregateState
{
    public readonly Dictionary<string, HumanInvasionBranchState> Branches =
        new(StringComparer.Ordinal);
    public readonly List<HumanSupportSiteState> SupportSites = new();
    public readonly List<ScheduledInvasionOperationState> Operations = new();
    public int CurrentDay = 1;
    public int OperationSequence;

    public InvasionCampaignAggregateState Clone()
    {
        InvasionCampaignAggregateState clone = new()
        {
            CurrentDay = CurrentDay,
            OperationSequence = OperationSequence
        };
        foreach (KeyValuePair<string, HumanInvasionBranchState> branch in Branches)
        {
            clone.Branches.Add(branch.Key, Clone(branch.Value));
        }
        clone.SupportSites.AddRange(SupportSites.Select(Clone));
        clone.Operations.AddRange(Operations.Select(Clone));
        return clone;
    }

    private static HumanInvasionBranchState Clone(HumanInvasionBranchState source)
    {
        return new HumanInvasionBranchState
        {
            branchId = source.branchId,
            displayName = source.displayName,
            strength = source.strength,
            operational = source.operational,
            lastRecoveryAmount = source.lastRecoveryAmount,
            recoveryReason = source.recoveryReason
        };
    }

    private static HumanSupportSiteState Clone(HumanSupportSiteState source)
    {
        return new HumanSupportSiteState
        {
            siteId = source.siteId,
            branchId = source.branchId,
            displayName = source.displayName,
            q = source.q,
            r = source.r,
            alive = source.alive,
            connected = source.connected,
            destroyedDay = source.destroyedDay
        };
    }

    private static ScheduledInvasionOperationState Clone(
        ScheduledInvasionOperationState source)
    {
        return new ScheduledInvasionOperationState
        {
            operationId = source.operationId,
            kind = source.kind,
            primaryBranchId = source.primaryBranchId,
            participatingBranchIds = source.participatingBranchIds.ToList(),
            objectiveId = source.objectiveId,
            scheduledDay = source.scheduledDay,
            intelligenceConfidence = source.intelligenceConfidence
        };
    }
}
