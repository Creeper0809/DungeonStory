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
        string detail,
        IReadOnlyList<OffenseRewardPhysicalItemGrant> physicalItems = null)
    {
        this.category = category;
        this.label = label ?? string.Empty;
        this.requestedAmount = Mathf.Max(0, requestedAmount);
        this.grantedAmount = Mathf.Max(0, grantedAmount);
        this.success = success;
        this.detail = detail ?? string.Empty;
        this.physicalItems = EventPayloadSnapshot.Copy(physicalItems);
    }

    public OffenseRewardCategory category { get; }
    public string label { get; }
    public int requestedAmount { get; }
    public int grantedAmount { get; }
    public bool success { get; }
    public string detail { get; }
    public IReadOnlyList<OffenseRewardPhysicalItemGrant> physicalItems { get; }

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

public sealed class OffenseRewardPhysicalItemGrant
{
    public OffenseRewardPhysicalItemGrant(string itemId, int quantity)
    {
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.quantity = Mathf.Max(0, quantity);
    }

    public string itemId { get; }
    public int quantity { get; }
}

public enum OffenseSettlementValuationState
{
    Valued = 0,
    UnvaluedItem = 1,
    AuthorityUnavailable = 2
}

public sealed class OffenseItemValuationSnapshot
{
    public OffenseItemValuationSnapshot(
        string itemId,
        int quantity,
        OffenseSettlementValuationState state,
        long acquisitionMilliEwuPerUnit = 0L,
        long recoverableMilliEwuPerUnit = 0L,
        string basisId = "",
        string selectedSourceId = "")
    {
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.quantity = Mathf.Max(0, quantity);
        this.state = state;
        this.acquisitionMilliEwuPerUnit = Math.Max(0L, acquisitionMilliEwuPerUnit);
        this.recoverableMilliEwuPerUnit = Math.Max(0L, recoverableMilliEwuPerUnit);
        this.basisId = basisId?.Trim() ?? string.Empty;
        this.selectedSourceId = selectedSourceId?.Trim() ?? string.Empty;
    }

    public string itemId { get; }
    public int quantity { get; }
    public OffenseSettlementValuationState state { get; }
    public long acquisitionMilliEwuPerUnit { get; }
    public long recoverableMilliEwuPerUnit { get; }
    public string basisId { get; }
    public string selectedSourceId { get; }
}

public enum OffenseExpeditionItemReceiptKind
{
    SupplyConsumed = 0,
    AmmunitionConsumed = 1,
    EquipmentWorn = 2,
    EquipmentLost = 3,
    EquipmentRecovered = 4,
    SupplyReturned = 5,
    LootRecovered = 6,
    RewardGranted = 7
}

public sealed class OffenseExpeditionItemReceipt
{
    public OffenseExpeditionItemReceipt(
        OffenseExpeditionItemReceiptKind kind,
        string itemId,
        int quantity,
        string instanceId,
        float durabilityLoss,
        OffenseItemValuationSnapshot valuation)
    {
        this.kind = kind;
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.quantity = Mathf.Max(0, quantity);
        this.instanceId = instanceId?.Trim() ?? string.Empty;
        this.durabilityLoss = Mathf.Clamp01(durabilityLoss);
        this.valuation = valuation;
    }

    public OffenseExpeditionItemReceiptKind kind { get; }
    public string itemId { get; }
    public int quantity { get; }
    public string instanceId { get; }
    public float durabilityLoss { get; }
    public OffenseItemValuationSnapshot valuation { get; }
}

public enum OffenseExpeditionTreatmentKind
{
    Healing = 0,
    Stabilization = 1
}

public sealed class OffenseExpeditionTreatmentReceipt
{
    public OffenseExpeditionTreatmentReceipt(
        OffenseExpeditionTreatmentKind kind,
        string characterId,
        string anatomyNodeId,
        float healedAmount)
    {
        this.kind = kind;
        this.characterId = characterId?.Trim() ?? string.Empty;
        this.anatomyNodeId = anatomyNodeId?.Trim() ?? string.Empty;
        this.healedAmount = Mathf.Max(0f, healedAmount);
    }

    public OffenseExpeditionTreatmentKind kind { get; }
    public string characterId { get; }
    public string anatomyNodeId { get; }
    public float healedAmount { get; }
}

public sealed class OffenseExpeditionCurrencyReceipt
{
    public OffenseExpeditionCurrencyReceipt(
        string currencyId,
        int amount,
        string operationId)
    {
        this.currencyId = currencyId?.Trim() ?? string.Empty;
        this.amount = Mathf.Max(0, amount);
        this.operationId = operationId?.Trim() ?? string.Empty;
    }

    public string currencyId { get; }
    public int amount { get; }
    public string operationId { get; }
}

public static class OffenseSettlementCurrencyIds
{
    public const string Gold = "currency:gold";
}

public enum OffenseExpeditionArrivalResolution
{
    Pending = 0,
    Secured = 1,
    Escaped = 2
}

public sealed class OffenseExpeditionArrivalReceipt
{
    public OffenseExpeditionArrivalReceipt(
        string arrivalId,
        string kind,
        int requestedAmount,
        int materializedAmount,
        int securedAmount,
        int escapedAmount,
        OffenseExpeditionArrivalResolution resolution)
    {
        this.arrivalId = arrivalId?.Trim() ?? string.Empty;
        this.kind = kind?.Trim() ?? string.Empty;
        this.requestedAmount = Mathf.Max(0, requestedAmount);
        this.materializedAmount = Mathf.Max(0, materializedAmount);
        this.securedAmount = Mathf.Max(0, securedAmount);
        this.escapedAmount = Mathf.Max(0, escapedAmount);
        this.resolution = resolution;
    }

    public string arrivalId { get; }
    public string kind { get; }
    public int requestedAmount { get; }
    public int materializedAmount { get; }
    public int securedAmount { get; }
    public int escapedAmount { get; }
    public OffenseExpeditionArrivalResolution resolution { get; }
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
        IReadOnlyList<OffenseRewardGrantResult> grantedRewards = null,
        IReadOnlyList<OffenseExpeditionItemReceipt> itemReceipts = null,
        IReadOnlyList<OffenseExpeditionTreatmentReceipt> treatmentReceipts = null,
        IReadOnlyList<OffenseExpeditionArrivalReceipt> arrivalReceipts = null,
        IReadOnlyList<OffenseExpeditionCurrencyReceipt> currencyReceipts = null)
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
        this.itemReceipts = EventPayloadSnapshot.Copy(itemReceipts);
        this.treatmentReceipts = EventPayloadSnapshot.Copy(treatmentReceipts);
        this.arrivalReceipts = EventPayloadSnapshot.Copy(arrivalReceipts);
        this.currencyReceipts = EventPayloadSnapshot.Copy(currencyReceipts);
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
    public IReadOnlyList<OffenseExpeditionItemReceipt> itemReceipts { get; }
    public IReadOnlyList<OffenseExpeditionTreatmentReceipt> treatmentReceipts { get; }
    public IReadOnlyList<OffenseExpeditionArrivalReceipt> arrivalReceipts { get; }
    public IReadOnlyList<OffenseExpeditionCurrencyReceipt> currencyReceipts { get; }

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
            safeRewards,
            itemReceipts,
            treatmentReceipts,
            arrivalReceipts,
            currencyReceipts);
    }

    public OffenseExpeditionResult WithSettlement(
        IReadOnlyList<OffenseExpeditionItemReceipt> items,
        IReadOnlyList<OffenseExpeditionTreatmentReceipt> treatments)
    {
        return new OffenseExpeditionResult(
            expeditionId, targetId, targetTitle, success, totalPower,
            requiredPower, danger, elapsedSeconds, members, rewardSummaries,
            grantedRewards, items, treatments, arrivalReceipts,
            currencyReceipts);
    }

    public OffenseExpeditionResult WithArrivalReceipts(
        IReadOnlyList<OffenseExpeditionArrivalReceipt> arrivals)
    {
        return new OffenseExpeditionResult(
            expeditionId, targetId, targetTitle, success, totalPower,
            requiredPower, danger, elapsedSeconds, members, rewardSummaries,
            grantedRewards, itemReceipts, treatmentReceipts, arrivals,
            currencyReceipts);
    }

    public OffenseExpeditionResult WithAdditionalItemReceipts(
        IReadOnlyList<OffenseExpeditionItemReceipt> additional)
    {
        return WithSettlement(
            itemReceipts.Concat(additional ?? Array.Empty<OffenseExpeditionItemReceipt>())
                .Where(value => value != null)
                .ToArray(),
            treatmentReceipts);
    }

    public OffenseExpeditionResult WithAdditionalCurrencyReceipts(
        IReadOnlyList<OffenseExpeditionCurrencyReceipt> additional)
    {
        return new OffenseExpeditionResult(
            expeditionId, targetId, targetTitle, success, totalPower,
            requiredPower, danger, elapsedSeconds, members, rewardSummaries,
            grantedRewards, itemReceipts, treatmentReceipts, arrivalReceipts,
            currencyReceipts.Concat(
                    additional ?? Array.Empty<OffenseExpeditionCurrencyReceipt>())
                .Where(value => value != null)
                .ToArray());
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

        if (itemReceipts.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("실제 물자 결산:");
            foreach (OffenseExpeditionItemReceipt receipt in itemReceipts)
            {
                if (receipt == null) continue;
                string value = receipt.valuation?.state switch
                {
                    OffenseSettlementValuationState.Valued =>
                        $" / 취득 {receipt.valuation.acquisitionMilliEwuPerUnit}"
                        + $"·회수 {receipt.valuation.recoverableMilliEwuPerUnit} mEWU/개"
                        + $" ({receipt.valuation.basisId})",
                    OffenseSettlementValuationState.UnvaluedItem =>
                        " / EWU 미평가(품목 누락)",
                    _ => " / EWU 권위 불가"
                };
                string wear = receipt.durabilityLoss > 0f
                    ? $" / 내구도 -{receipt.durabilityLoss * 100f:0.#}%"
                    : string.Empty;
                lines.Add($"- {receipt.kind}: {receipt.itemId} x{receipt.quantity}{wear}{value}");
            }
        }

        if (treatmentReceipts.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("실제 치료:");
            foreach (OffenseExpeditionTreatmentReceipt receipt in treatmentReceipts)
            {
                if (receipt == null) continue;
                string amount = receipt.kind == OffenseExpeditionTreatmentKind.Healing
                    ? $" +{receipt.healedAmount:0.#}"
                    : string.Empty;
                lines.Add($"- {receipt.characterId}: {receipt.kind}{amount}");
            }
        }

        if (currencyReceipts.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("실제 통화 반환:");
            foreach (OffenseExpeditionCurrencyReceipt receipt in currencyReceipts)
            {
                if (receipt != null)
                    lines.Add($"- {receipt.currencyId}: {receipt.amount}");
            }
        }

        if (arrivalReceipts.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("귀환 대상:");
            foreach (OffenseExpeditionArrivalReceipt receipt in arrivalReceipts)
            {
                if (receipt == null) continue;
                string state = receipt.resolution switch
                {
                    OffenseExpeditionArrivalResolution.Pending => "정산 중",
                    OffenseExpeditionArrivalResolution.Secured => "확보 완료",
                    OffenseExpeditionArrivalResolution.Escaped => "탈출 종결",
                    _ => receipt.resolution.ToString()
                };
                lines.Add($"- {receipt.kind}: 요청 {receipt.requestedAmount}, "
                    + $"도착 {receipt.materializedAmount}, 확보 {receipt.securedAmount}, "
                    + $"탈출 {receipt.escapedAmount} / {state}");
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

public readonly struct OffenseExpeditionArrivalResolvedEvent
{
    public OffenseExpeditionArrivalResolvedEvent(
        string expeditionId,
        IReadOnlyList<OffenseExpeditionArrivalReceipt> receipts)
    {
        this.expeditionId = expeditionId?.Trim() ?? string.Empty;
        this.receipts = EventPayloadSnapshot.Copy(receipts);
    }

    public string expeditionId { get; }
    public IReadOnlyList<OffenseExpeditionArrivalReceipt> receipts { get; }
}
