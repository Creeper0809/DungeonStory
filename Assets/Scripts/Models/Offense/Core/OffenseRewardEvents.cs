using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class OffenseExpeditionMemberSnapshot
{
    public OffenseExpeditionMemberSnapshot(
        string name,
        string speciesTag,
        float power,
        bool survived,
        float damageTaken)
    {
        this.name = name ?? string.Empty;
        this.speciesTag = speciesTag ?? string.Empty;
        this.power = Mathf.Max(0f, power);
        this.survived = survived;
        this.damageTaken = Mathf.Max(0f, damageTaken);
    }

    public string name { get; }
    public string speciesTag { get; }
    public float power { get; }
    public bool survived { get; }
    public float damageTaken { get; }

    public string ToSummaryText()
    {
        string state = survived ? "복귀" : "사망";
        string species = string.IsNullOrWhiteSpace(speciesTag) ? "미상" : speciesTag;
        return $"{name} / {species} / 받은 피해 {damageTaken:0.#} / {state}";
    }
}

public sealed class OffenseRewardGrantResult
{
    public OffenseRewardGrantResult(
        OffenseRewardCategory category,
        string label,
        int requestedAmount,
        int grantedAmount,
        bool success,
        string detail)
    {
        this.category = category;
        this.label = label ?? string.Empty;
        this.requestedAmount = Mathf.Max(0, requestedAmount);
        this.grantedAmount = Mathf.Max(0, grantedAmount);
        this.success = success;
        this.detail = detail ?? string.Empty;
    }

    public OffenseRewardCategory category { get; }
    public string label { get; }
    public int requestedAmount { get; }
    public int grantedAmount { get; }
    public bool success { get; }
    public string detail { get; }

    public string ToSummaryText()
    {
        string rewardName = string.IsNullOrWhiteSpace(label) ? category.ToString() : label;
        if (success)
        {
            string amountText = grantedAmount > 0 ? $" x{grantedAmount}" : string.Empty;
            string detailText = string.IsNullOrWhiteSpace(detail) ? string.Empty : $" - {detail}";
            return $"{rewardName}{amountText}{detailText}";
        }

        string reason = string.IsNullOrWhiteSpace(detail) ? "지급 실패" : detail;
        return $"{rewardName} 지급 실패 - {reason}";
    }
}

public sealed class OffenseExpeditionResult
{
    public OffenseExpeditionResult(
        string expeditionId,
        string targetId,
        string targetTitle,
        bool success,
        float totalPower,
        float requiredPower,
        float danger,
        float elapsedSeconds,
        IReadOnlyList<OffenseExpeditionMemberSnapshot> members,
        IReadOnlyList<string> rewardSummaries,
        IReadOnlyList<OffenseRewardGrantResult> grantedRewards = null)
    {
        this.expeditionId = expeditionId ?? string.Empty;
        this.targetId = targetId ?? string.Empty;
        this.targetTitle = targetTitle ?? string.Empty;
        this.success = success;
        this.totalPower = Mathf.Max(0f, totalPower);
        this.requiredPower = Mathf.Max(0f, requiredPower);
        this.danger = Mathf.Max(0f, danger);
        this.elapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        this.members = EventPayloadSnapshot.Copy(members);
        this.rewardSummaries = EventPayloadSnapshot.Copy(rewardSummaries);
        this.grantedRewards = EventPayloadSnapshot.Copy(grantedRewards);
    }

    public string expeditionId { get; }
    public string targetId { get; }
    public string targetTitle { get; }
    public bool success { get; }
    public float totalPower { get; }
    public float requiredPower { get; }
    public float danger { get; }
    public float elapsedSeconds { get; }
    public IReadOnlyList<OffenseExpeditionMemberSnapshot> members { get; }
    public IReadOnlyList<string> rewardSummaries { get; }
    public IReadOnlyList<OffenseRewardGrantResult> grantedRewards { get; }

    public OffenseExpeditionResult WithGrantedRewards(IReadOnlyList<OffenseRewardGrantResult> rewards)
    {
        IReadOnlyList<OffenseRewardGrantResult> safeRewards = EventPayloadSnapshot.Copy(rewards);
        IReadOnlyList<string> summaries = safeRewards
            .Where((reward) => reward != null)
            .Select((reward) => reward.ToSummaryText())
            .ToArray();
        return new OffenseExpeditionResult(
            expeditionId,
            targetId,
            targetTitle,
            success,
            totalPower,
            requiredPower,
            danger,
            elapsedSeconds,
            members,
            summaries.Count > 0 ? summaries : rewardSummaries,
            safeRewards);
    }

    public string ToDetailText()
    {
        List<string> lines = new List<string>
        {
            success ? "원정 성공" : "원정 실패",
            $"대상: {targetTitle}",
            $"위험도: {danger:0.#}",
            "방식: 직접 턴제 전투"
        };

        if (members.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("원정대:");
            foreach (OffenseExpeditionMemberSnapshot member in members)
            {
                if (member != null)
                {
                    lines.Add($"- {member.ToSummaryText()}");
                }
            }
        }

        if (success && grantedRewards.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("지급 결과:");
            foreach (OffenseRewardGrantResult reward in grantedRewards)
            {
                if (reward != null)
                {
                    lines.Add($"- {reward.ToSummaryText()}");
                }
            }
        }
        else if (success && rewardSummaries.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("획득 보상:");
            foreach (string reward in rewardSummaries)
            {
                if (!string.IsNullOrWhiteSpace(reward))
                {
                    lines.Add($"- {reward}");
                }
            }
        }

        return string.Join("\n", lines);
    }
}

public readonly struct OffenseRewardGrantedEvent
{
    public OffenseRewardGrantedEvent(
        OffenseExpeditionResult expeditionResult,
        IReadOnlyList<OffenseRewardGrantResult> grantResults)
    {
        this.expeditionResult = expeditionResult;
        this.grantResults = EventPayloadSnapshot.Copy(grantResults);
    }

    public OffenseExpeditionResult expeditionResult { get; }
    public IReadOnlyList<OffenseRewardGrantResult> grantResults { get; }
}
