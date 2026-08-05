using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IOffenseFieldMedicalRuntime
{
    bool TryApplyPackedStabilization(
        OffenseExpeditionRun expedition,
        CharacterActor character,
        string anatomyNodeId,
        int eventSequence,
        out string reason);
    bool TryApplyStabilization(
        string expeditionId,
        string characterId,
        string anatomyNodeId,
        string consumedKitInstanceId,
        int eventSequence,
        out string reason);
    bool TryInvalidateStabilization(
        string expeditionId,
        string characterId,
        string anatomyNodeId);
    bool TryAssignCarrier(
        string expeditionId,
        string casualtyCharacterId,
        string carrierCharacterId,
        float casualtyBodyWeight,
        float casualtyEquipmentWeight,
        float carrierCapacity,
        float carrierCurrentLoad,
        out string reason);
    bool TrySetStranded(
        string expeditionId,
        OffenseHexCoord position,
        float remainingSupply,
        float estimatedSurvivalHours,
        string reason);
    bool TryDispatchRescue(
        string strandedExpeditionId,
        string rescueExpeditionId,
        IEnumerable<string> rescuerCharacterIds,
        out string reason);
    bool TryMergeRescue(
        string rescueExpeditionId,
        IEnumerable<string> protectedCasualtyIds,
        out string reason);
    bool IsStranded(string expeditionId);
    bool TryGetStrandedState(
        string expeditionId,
        out OffenseStrandedState state);
    bool TryGetRescueConvoy(
        string rescueExpeditionId,
        out RescueConvoyState state);
    float GetMovementTimeMultiplier(string expeditionId);
    IReadOnlyList<FieldStabilizationState> GetStabilizations(string expeditionId);
    IReadOnlyList<OffenseCasualtyCarryState> GetCarries(string expeditionId);
    void ClearOnDungeonArrival(string expeditionId);
    void Capture(OffenseWorldSaveData destination);
}

public sealed class OffenseFieldMedicalRestoreCandidate
{
    internal OffenseFieldMedicalRestoreCandidate(
        List<FieldStabilizationState> stabilizations,
        List<OffenseCasualtyCarryState> carries,
        List<OffenseStrandedState> stranded,
        List<RescueConvoyState> convoys)
    {
        Stabilizations = stabilizations;
        Carries = carries;
        Stranded = stranded;
        Convoys = convoys;
    }

    internal List<FieldStabilizationState> Stabilizations { get; }
    internal List<OffenseCasualtyCarryState> Carries { get; }
    internal List<OffenseStrandedState> Stranded { get; }
    internal List<RescueConvoyState> Convoys { get; }
}

public sealed class OffenseFieldMedicalRuntime : IOffenseFieldMedicalRuntime
{
    private List<FieldStabilizationState> stabilizations = new();
    private List<OffenseCasualtyCarryState> carries = new();
    private List<OffenseStrandedState> stranded = new();
    private List<RescueConvoyState> convoys = new();

    public bool TryApplyPackedStabilization(
        OffenseExpeditionRun expedition,
        CharacterActor character,
        string anatomyNodeId,
        int eventSequence,
        out string reason)
    {
        string expeditionId = expedition?.ExpeditionId ?? string.Empty;
        string characterId = character?.Identity?.PersistentId ?? string.Empty;
        if (!HasId(expeditionId)
            || !HasId(characterId)
            || !HasId(anatomyNodeId)
            || expedition.MemberActors.All(member => member != character))
        {
            reason = "현재 원정에 속한 대원과 손상 부위가 필요합니다.";
            return false;
        }

        if (stabilizations.Any(state => Same(state.expeditionId, expeditionId)
            && Same(state.characterId, characterId)
            && Same(state.anatomyNodeId, anatomyNodeId)
            && state.usedForNode))
        {
            reason = "같은 부위에는 이번 원정에서 가고정을 다시 적용할 수 없습니다.";
            return false;
        }

        OffenseSupplyType kitType = OffenseSupplyCatalog.GetFieldMedicalKit(
            character.SpeciesTag);
        if (!expedition.Supplies.TryConsume(kitType, 1))
        {
            reason = $"{OffenseSupplyCatalog.GetDisplayName(kitType)}이(가) 없습니다.";
            return false;
        }

        string consumedToken = string.Join(
            ":",
            "packed",
            expeditionId,
            kitType,
            Mathf.Max(0, eventSequence));
        if (TryApplyStabilization(
                expeditionId,
                characterId,
                anatomyNodeId,
                consumedToken,
                eventSequence,
                out reason))
        {
            return true;
        }

        expedition.Supplies.Add(kitType, 1);
        return false;
    }

    public bool TryApplyStabilization(
        string expeditionId,
        string characterId,
        string anatomyNodeId,
        string consumedKitInstanceId,
        int eventSequence,
        out string reason)
    {
        if (!HasId(expeditionId) || !HasId(characterId) || !HasId(anatomyNodeId))
        {
            reason = "원정대·대원·부위 정보가 필요합니다.";
            return false;
        }

        if (!HasId(consumedKitInstanceId))
        {
            reason = "실제로 소비한 야전 가고정 키트가 필요합니다.";
            return false;
        }

        FieldStabilizationState existing = stabilizations.FirstOrDefault(state =>
            Same(state.expeditionId, expeditionId)
            && Same(state.characterId, characterId)
            && Same(state.anatomyNodeId, anatomyNodeId));
        if (existing != null && existing.usedForNode)
        {
            reason = "같은 부위에는 이번 원정에서 가고정을 다시 적용할 수 없습니다.";
            return false;
        }

        stabilizations.Add(new FieldStabilizationState
        {
            expeditionId = expeditionId,
            characterId = characterId,
            anatomyNodeId = anatomyNodeId,
            consumedKitInstanceId = consumedKitInstanceId,
            active = true,
            usedForNode = true,
            locomotionFloor = 0.5f,
            sustainFloor = 0.5f,
            appliedEventSequence = Mathf.Max(0, eventSequence)
        });
        reason = string.Empty;
        return true;
    }

    public bool TryInvalidateStabilization(
        string expeditionId,
        string characterId,
        string anatomyNodeId)
    {
        FieldStabilizationState state = stabilizations.FirstOrDefault(item =>
            Same(item.expeditionId, expeditionId)
            && Same(item.characterId, characterId)
            && Same(item.anatomyNodeId, anatomyNodeId)
            && item.active);
        if (state == null)
        {
            return false;
        }

        state.active = false;
        return true;
    }

    public bool TryAssignCarrier(
        string expeditionId,
        string casualtyCharacterId,
        string carrierCharacterId,
        float casualtyBodyWeight,
        float casualtyEquipmentWeight,
        float carrierCapacity,
        float carrierCurrentLoad,
        out string reason)
    {
        if (!HasId(expeditionId)
            || !HasId(casualtyCharacterId)
            || !HasId(carrierCharacterId)
            || Same(casualtyCharacterId, carrierCharacterId))
        {
            reason = "서로 다른 운반자와 부상자를 지정해야 합니다.";
            return false;
        }

        float addedWeight = Mathf.Max(0f, casualtyBodyWeight)
            + Mathf.Max(0f, casualtyEquipmentWeight);
        if (carrierCurrentLoad + addedWeight > Mathf.Max(0f, carrierCapacity))
        {
            reason = "운반자의 적재 한도를 초과합니다.";
            return false;
        }

        if (carries.Any(item => item.active
            && Same(item.expeditionId, expeditionId)
            && (Same(item.casualtyCharacterId, casualtyCharacterId)
                || Same(item.carrierCharacterId, carrierCharacterId))))
        {
            reason = "이미 운반 중인 대원 또는 부상자입니다.";
            return false;
        }

        carries.Add(new OffenseCasualtyCarryState
        {
            expeditionId = expeditionId,
            casualtyCharacterId = casualtyCharacterId,
            carrierCharacterId = carrierCharacterId,
            casualtyBodyWeight = Mathf.Max(0f, casualtyBodyWeight),
            casualtyEquipmentWeight = Mathf.Max(0f, casualtyEquipmentWeight),
            active = true
        });
        reason = string.Empty;
        return true;
    }

    public bool TrySetStranded(
        string expeditionId,
        OffenseHexCoord position,
        float remainingSupply,
        float estimatedSurvivalHours,
        string reason)
    {
        if (!HasId(expeditionId))
        {
            return false;
        }

        OffenseStrandedState state = stranded.FirstOrDefault(item =>
            Same(item.expeditionId, expeditionId));
        if (state == null)
        {
            state = new OffenseStrandedState { expeditionId = expeditionId };
            stranded.Add(state);
        }

        state.q = position.Q;
        state.r = position.R;
        state.remainingSupply = Mathf.Max(0f, remainingSupply);
        state.estimatedSurvivalHours = Mathf.Max(0f, estimatedSurvivalHours);
        state.reason = reason ?? string.Empty;
        state.active = true;
        return true;
    }

    public bool TryDispatchRescue(
        string strandedExpeditionId,
        string rescueExpeditionId,
        IEnumerable<string> rescuerCharacterIds,
        out string reason)
    {
        string[] rescuers = NormalizeIds(rescuerCharacterIds).Take(5).ToArray();
        if (!IsStranded(strandedExpeditionId)
            || !HasId(rescueExpeditionId)
            || rescuers.Length == 0)
        {
            reason = "활성 조난 원정과 1~5명의 구조대가 필요합니다.";
            return false;
        }

        if (convoys.Any(item => Same(item.rescueExpeditionId, rescueExpeditionId)))
        {
            reason = "이미 등록된 구조대입니다.";
            return false;
        }

        convoys.Add(new RescueConvoyState
        {
            rescueExpeditionId = rescueExpeditionId,
            strandedExpeditionId = strandedExpeditionId,
            dispatched = true,
            rescuerCharacterIds = rescuers.ToList()
        });
        reason = string.Empty;
        return true;
    }

    public bool TryMergeRescue(
        string rescueExpeditionId,
        IEnumerable<string> protectedCasualtyIds,
        out string reason)
    {
        RescueConvoyState convoy = convoys.FirstOrDefault(item =>
            Same(item.rescueExpeditionId, rescueExpeditionId));
        if (convoy == null || convoy.merged)
        {
            reason = "합류 가능한 구조대를 찾을 수 없습니다.";
            return false;
        }

        convoy.merged = true;
        convoy.protectedCasualtyIds = NormalizeIds(protectedCasualtyIds).ToList();
        foreach (FieldStabilizationState stabilization in stabilizations.Where(item =>
                     Same(item.expeditionId, convoy.strandedExpeditionId)))
        {
            stabilization.expeditionId = rescueExpeditionId;
        }
        foreach (OffenseCasualtyCarryState carry in carries.Where(item =>
                     Same(item.expeditionId, convoy.strandedExpeditionId)))
        {
            carry.expeditionId = rescueExpeditionId;
        }
        OffenseStrandedState target = stranded.FirstOrDefault(item =>
            Same(item.expeditionId, convoy.strandedExpeditionId));
        if (target != null)
        {
            target.active = false;
        }

        reason = string.Empty;
        return true;
    }

    public bool IsStranded(string expeditionId) => stranded.Any(item =>
        item.active && Same(item.expeditionId, expeditionId));

    public bool TryGetStrandedState(
        string expeditionId,
        out OffenseStrandedState state)
    {
        OffenseStrandedState found = stranded.FirstOrDefault(item =>
            item.active && Same(item.expeditionId, expeditionId));
        state = found != null ? Clone(found) : null;
        return state != null;
    }

    public bool TryGetRescueConvoy(
        string rescueExpeditionId,
        out RescueConvoyState state)
    {
        RescueConvoyState found = convoys.FirstOrDefault(item =>
            item.dispatched
            && !item.merged
            && Same(item.rescueExpeditionId, rescueExpeditionId));
        state = found != null ? Clone(found) : null;
        return state != null;
    }

    public float GetMovementTimeMultiplier(string expeditionId)
    {
        int activeCarries = carries.Count(item =>
            item.active && Same(item.expeditionId, expeditionId));
        return Mathf.Clamp(1f + activeCarries * 0.35f, 1f, 2.5f);
    }

    public IReadOnlyList<FieldStabilizationState> GetStabilizations(
        string expeditionId) => stabilizations
        .Where(item => Same(item.expeditionId, expeditionId))
        .Select(Clone)
        .ToArray();

    public IReadOnlyList<OffenseCasualtyCarryState> GetCarries(
        string expeditionId) => carries
        .Where(item => item.active && Same(item.expeditionId, expeditionId))
        .Select(Clone)
        .ToArray();

    public void ClearOnDungeonArrival(string expeditionId)
    {
        string[] linkedStrandedIds = convoys
            .Where(item => Same(item.rescueExpeditionId, expeditionId))
            .Select(item => item.strandedExpeditionId)
            .Where(HasId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        bool MatchesArrival(string value) => Same(value, expeditionId)
            || linkedStrandedIds.Any(id => Same(value, id));
        stabilizations.RemoveAll(item => MatchesArrival(item.expeditionId));
        carries.RemoveAll(item => MatchesArrival(item.expeditionId));
        stranded.RemoveAll(item => MatchesArrival(item.expeditionId));
        convoys.RemoveAll(item => Same(item.rescueExpeditionId, expeditionId)
            || Same(item.strandedExpeditionId, expeditionId));
    }

    public void Capture(OffenseWorldSaveData destination)
    {
        if (destination == null) return;
        destination.fieldStabilizations = stabilizations.Select(Clone).ToList();
        destination.casualtyCarries = carries.Select(Clone).ToList();
        destination.strandedExpeditions = stranded.Select(Clone).ToList();
        destination.rescueConvoys = convoys.Select(Clone).ToList();
    }

    internal OffenseFieldMedicalRestoreCandidate PrepareRestore(
        OffenseWorldSaveData source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        List<FieldStabilizationState> restoredStabilizations =
            source.fieldStabilizations.Select(value =>
            {
                if (!IsValid(value)
                    || value.locomotionFloor < 0f
                    || value.locomotionFloor > 1f
                    || value.sustainFloor < 0f
                    || value.sustainFloor > 1f
                    || value.appliedEventSequence < 0)
                {
                    throw new InvalidOperationException(
                        $"Invalid field stabilization '{value?.expeditionId ?? "null"}'.");
                }
                return Clone(value);
            }).ToList();
        List<OffenseCasualtyCarryState> restoredCarries =
            source.casualtyCarries.Select(value =>
            {
                if (!IsValid(value)
                    || value.casualtyBodyWeight < 0f
                    || value.casualtyEquipmentWeight < 0f)
                {
                    throw new InvalidOperationException(
                        $"Invalid casualty carry '{value?.expeditionId ?? "null"}'.");
                }
                return Clone(value);
            }).ToList();
        List<OffenseStrandedState> restoredStranded =
            source.strandedExpeditions.Select(value =>
            {
                if (!IsValid(value)
                    || value.remainingSupply < 0f
                    || value.estimatedSurvivalHours < 0f)
                {
                    throw new InvalidOperationException(
                        $"Invalid stranded expedition '{value?.expeditionId ?? "null"}'.");
                }
                return Clone(value);
            }).ToList();
        List<RescueConvoyState> restoredConvoys =
            source.rescueConvoys.Select(value =>
            {
                if (!IsValid(value))
                {
                    throw new InvalidOperationException(
                        $"Invalid rescue convoy '{value?.rescueExpeditionId ?? "null"}'.");
                }
                return Clone(value);
            }).ToList();

        return new OffenseFieldMedicalRestoreCandidate(
            restoredStabilizations,
            restoredCarries,
            restoredStranded,
            restoredConvoys);
    }

    public OffenseFieldMedicalRestoreCandidate BuildRestoreCandidate(
        OffenseWorldSaveData source) =>
        PrepareRestore(source);

    internal void PublishRestore(OffenseFieldMedicalRestoreCandidate candidate)
    {
        candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        stabilizations = candidate.Stabilizations;
        carries = candidate.Carries;
        stranded = candidate.Stranded;
        convoys = candidate.Convoys;
    }

    public void PublishRestoreCandidate(
        OffenseFieldMedicalRestoreCandidate candidate) =>
        PublishRestore(candidate);

    private static bool HasId(string value) => !string.IsNullOrWhiteSpace(value);
    private static bool Same(string left, string right) => string.Equals(
        left?.Trim(), right?.Trim(), StringComparison.Ordinal);
    private static IEnumerable<string> NormalizeIds(IEnumerable<string> values) =>
        (values ?? Array.Empty<string>())
        .Where(HasId)
        .Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal);

    private static bool IsValid(FieldStabilizationState value) => value != null
        && HasId(value.expeditionId) && HasId(value.characterId)
        && HasId(value.anatomyNodeId) && HasId(value.consumedKitInstanceId);
    private static bool IsValid(OffenseCasualtyCarryState value) => value != null
        && HasId(value.expeditionId) && HasId(value.casualtyCharacterId)
        && HasId(value.carrierCharacterId)
        && !Same(value.casualtyCharacterId, value.carrierCharacterId);
    private static bool IsValid(OffenseStrandedState value) => value != null
        && HasId(value.expeditionId);
    private static bool IsValid(RescueConvoyState value) => value != null
        && HasId(value.rescueExpeditionId) && HasId(value.strandedExpeditionId);

    private static FieldStabilizationState Clone(FieldStabilizationState source) =>
        new()
        {
            expeditionId = source.expeditionId,
            characterId = source.characterId,
            anatomyNodeId = source.anatomyNodeId,
            consumedKitInstanceId = source.consumedKitInstanceId,
            active = source.active,
            usedForNode = source.usedForNode,
            locomotionFloor = source.locomotionFloor,
            sustainFloor = source.sustainFloor,
            appliedEventSequence = source.appliedEventSequence
        };

    private static OffenseCasualtyCarryState Clone(OffenseCasualtyCarryState source) =>
        new()
        {
            expeditionId = source.expeditionId,
            casualtyCharacterId = source.casualtyCharacterId,
            carrierCharacterId = source.carrierCharacterId,
            casualtyBodyWeight = source.casualtyBodyWeight,
            casualtyEquipmentWeight = source.casualtyEquipmentWeight,
            active = source.active
        };

    private static OffenseStrandedState Clone(OffenseStrandedState source) => new()
    {
        expeditionId = source.expeditionId,
        q = source.q,
        r = source.r,
        remainingSupply = source.remainingSupply,
        estimatedSurvivalHours = source.estimatedSurvivalHours,
        reason = source.reason,
        active = source.active
    };

    private static RescueConvoyState Clone(RescueConvoyState source) => new()
    {
        rescueExpeditionId = source.rescueExpeditionId,
        strandedExpeditionId = source.strandedExpeditionId,
        dispatched = source.dispatched,
        merged = source.merged,
        rescuerCharacterIds = source.rescuerCharacterIds.ToList(),
        protectedCasualtyIds = source.protectedCasualtyIds.ToList()
    };
}
