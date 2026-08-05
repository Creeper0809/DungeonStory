using System;
using System.Collections.Generic;
using System.Linq;

public interface IOffenseFieldMobilityService
{
    bool TryUpdate(OffenseExpeditionRun expedition, out string message);
}

/// <summary>
/// Evaluates whether a travelling expedition can carry its casualties or must
/// become stranded. The expedition aggregate owns neither anatomy queries nor
/// medical transport bookkeeping.
/// </summary>
public sealed class OffenseFieldMobilityService : IOffenseFieldMobilityService
{
    private readonly IOffenseFieldMedicalRuntime fieldMedical;
    private readonly IAnatomyEffectRuntime anatomyEffects;
    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseTravelRuntime travel;

    public OffenseFieldMobilityService(
        IOffenseFieldMedicalRuntime fieldMedical,
        IAnatomyEffectRuntime anatomyEffects,
        IOffenseWorldSimulation world,
        IOffenseTravelRuntime travel)
    {
        this.fieldMedical = fieldMedical
            ?? throw new ArgumentNullException(nameof(fieldMedical));
        this.anatomyEffects = anatomyEffects
            ?? throw new ArgumentNullException(nameof(anatomyEffects));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
    }

    public bool TryUpdate(OffenseExpeditionRun expedition, out string message)
    {
        message = string.Empty;
        if (expedition == null
            || !expedition.UsesWorldTravel
            || fieldMedical.IsStranded(expedition.ExpeditionId))
        {
            return false;
        }

        IReadOnlyList<FieldStabilizationState> stabilizations =
            fieldMedical.GetStabilizations(expedition.ExpeditionId);
        IReadOnlyList<OffenseCasualtyCarryState> existingCarries =
            fieldMedical.GetCarries(expedition.ExpeditionId);
        List<OffenseFieldMobilityMemberSnapshot> immobileSnapshots = new();
        List<OffenseFieldMobilityMemberSnapshot> mobileSnapshots = new();
        foreach (CharacterActor actor in expedition.MemberActors.Where(value =>
                     value != null && !value.IsDead))
        {
            string characterId = actor.Identity?.PersistentId ?? string.Empty;
            AnatomyActionAxisSnapshot axes = anatomyEffects.GetActionAxes(actor);
            FieldStabilizationState stabilization = stabilizations
                .FirstOrDefault(state => state.active
                    && string.Equals(
                        state.characterId,
                        characterId,
                        StringComparison.Ordinal));
            CharacterCarryInventory inventory = actor.CarryInventory;
            OffenseFieldMobilityMemberSnapshot snapshot =
                new OffenseFieldMobilityMemberSnapshot(
                    characterId,
                    actor.Lifecycle?.CurrentState
                        == CharacterLifecycleState.Downed,
                    axes.Locomotion,
                    axes.Sustain,
                    stabilization != null,
                    stabilization?.locomotionFloor ?? 0f,
                    stabilization?.sustainFloor ?? 0f,
                    inventory?.GetMaxAllowedWeight() ?? 0f,
                    inventory?.GetBaseCarryLimit() ?? 20f,
                    inventory?.GetCurrentWeight() ?? 0f,
                    actor.MaxHealth);
            if (OffenseFieldMobilityRules.IsImmobile(snapshot))
            {
                immobileSnapshots.Add(snapshot);
            }
            else
            {
                mobileSnapshots.Add(snapshot);
            }
        }

        if (immobileSnapshots.Count == 0)
        {
            return false;
        }

        HashSet<string> assignedCasualties = existingCarries
            .Select(state => state.casualtyCharacterId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> assignedCarriers = existingCarries
            .Select(state => state.carrierCharacterId)
            .ToHashSet(StringComparer.Ordinal);
        for (int casualtyIndex = 0;
             casualtyIndex < immobileSnapshots.Count;
             casualtyIndex++)
        {
            OffenseFieldMobilityMemberSnapshot casualtySnapshot =
                immobileSnapshots[casualtyIndex];
            if (assignedCasualties.Contains(casualtySnapshot.CharacterId))
            {
                continue;
            }

            int carrierIndex = OffenseFieldMobilityRules.FindBestCarrierIndex(
                mobileSnapshots,
                assignedCarriers);
            if (carrierIndex < 0)
            {
                break;
            }

            OffenseFieldCarryPlan plan =
                OffenseFieldMobilityRules.CreateCarryPlan(
                    casualtySnapshot,
                    mobileSnapshots[carrierIndex]);
            if (fieldMedical.TryAssignCarrier(
                    expedition.ExpeditionId,
                    plan.CasualtyCharacterId,
                    plan.CarrierCharacterId,
                    plan.BodyWeight,
                    plan.CasualtyCarryWeight,
                    plan.CarrierCapacity,
                    plan.CarrierCurrentLoad,
                    out _))
            {
                assignedCarriers.Add(plan.CarrierCharacterId);
                assignedCasualties.Add(plan.CasualtyCharacterId);
            }
        }

        if (OffenseFieldMobilityRules.AreAllCasualtiesAssigned(
                immobileSnapshots,
                assignedCasualties))
        {
            message = $"field-carry-assigned:{immobileSnapshots.Count}";
            return false;
        }

        OffenseHexCoord position = world.DungeonCoord;
        if (travel.TryGetState(
                expedition.ExpeditionId,
                out OffenseTravelStateData travelState))
        {
            position = travelState.CurrentCoord;
        }

        float remainingSupply = expedition.Supplies.TotalCount;
        float estimatedSurvivalHours =
            OffenseFieldMobilityRules.CalculateEstimatedSurvivalHours(
                remainingSupply);
        fieldMedical.TrySetStranded(
            expedition.ExpeditionId,
            position,
            remainingSupply,
            estimatedSurvivalHours,
            "insufficient-mobile-carriers");
        message = $"field-expedition-stranded:{position.Q}:{position.R}:"
            + $"{estimatedSurvivalHours:0.#}";
        return true;
    }
}
