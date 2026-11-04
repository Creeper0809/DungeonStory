#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class FacilityEvolutionDebugScenarios
{
    // Controlled state fixture for the real replacement owner, not gameplay evidence.
    public static void VerifyOperationalStateForBatch()
    {
        VerifyIdleOperationalStateHandoff();
        VerifyActiveUseBlocksHandoff();
    }

    private static void VerifyIdleOperationalStateHandoff()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        BuildableObject source = world.SourceFacility;
        source.RestoreFacilityState(CreateOperationalState(source));
        FacilityRuntimeState original = source.FacilityState;
        Require(original.IsValid(out string stateFailure), "Invalid operational fixture: " + stateFailure);
        string stateBefore = JsonUtility.ToJson(original);
        string sourceId = source.RequirePersistentInstanceId().Value;

        bool replaced = world.CreateReplacer().TryReplace(
            source, world.CombatResultData, out BuildableObject result, out string reason);
        Require(replaced && result != null, "Idle facility replacement failed: " + reason);
        FacilityRuntimeState after = result.FacilityState;
        Require(result.RequirePersistentInstanceId().Value == sourceId
                && ReferenceEquals(world.Grid.GetGridCell(world.SourcePosition)
                    .GetOccupant(GridLayer.Building), result),
            "The replacement did not retain the original world identity and occupancy.");
        Require(after.IsValid(out string afterFailure), "Replacement state invalid: " + afterFailure);
        Require(JsonUtility.ToJson(after) == stateBefore,
            "Replacement changed the facility operational state.");
        Require(!ReferenceEquals(original, after)
                && !ReferenceEquals(original.pendingFuel, after.pendingFuel)
                && !ReferenceEquals(original.pendingFuel.physicalSourceStackIds,
                    after.pendingFuel.physicalSourceStackIds)
                && !ReferenceEquals(original.pendingUses, after.pendingUses)
                && !ReferenceEquals(original.activeUses, after.activeUses)
                && !ReferenceEquals(original.acknowledgedUseRanges, after.acknowledgedUseRanges)
                && !ReferenceEquals(original.acknowledgedUseRanges[0], after.acknowledgedUseRanges[0])
                && !ReferenceEquals(original.acknowledgedUseRanges[1], after.acknowledgedUseRanges[1])
                && !ReferenceEquals(original.lastVandalism, after.lastVandalism)
                && !ReferenceEquals(original.lastVandalism.witnessPublicationKeys,
                    after.lastVandalism.witnessPublicationKeys),
            "Replacement retained mutable operational-state references.");
        long next = result.AllocateFacilityUseOperationSequence();
        Require(next == 7L && result.FacilityState.nextUseOperationSequence == 8L
                && after.nextUseOperationSequence == 7L
                && original.nextUseOperationSequence == 7L
                && source.FacilityState.nextUseOperationSequence == 7L,
            "Replacement rewound or shared the next facility-use sequence.");
        Require(after.IsUseAcknowledged(1L) && after.IsUseAcknowledged(5L)
                && !after.IsUseAcknowledged(6L),
            "Replacement changed acknowledged use identities.");
    }

    private static void VerifyActiveUseBlocksHandoff()
    {
        using EvolutionScenarioWorld world = EvolutionScenarioWorld.CreateCombatDining();
        BuildableObject source = world.SourceFacility;
        FacilityRuntimeState state = CreateOperationalState(source);
        state.activeUses.Add(new FacilityUseExecutionState
        {
            operationSequence = 6L,
            operationId = "facility-use:fixture:6",
            actorId = "character:facility-use-fixture",
            actorDisplayName = "Visitor",
            facilityRole = FacilityRole.Meal.ToString(),
            actionType = "fixture:meal",
            actionBranch = (int)CharacterAiBranch.Rest,
            phase = FacilityUseExecutionPhase.Approach,
            before = new FacilityUseVisitorState
            {
                actorId = "character:facility-use-fixture",
                displayName = "Visitor"
            }
        });
        Require(state.IsValid(out string stateFailure), "Invalid active-use fixture: " + stateFailure);
        source.RestoreFacilityState(state);
        string stateBefore = JsonUtility.ToJson(source.FacilityState);
        string sourceId = source.RequirePersistentInstanceId().Value;
        bool replaced = world.CreateReplacer().TryReplace(
            source, world.CombatResultData, out BuildableObject result, out string reason);
        Require(!replaced && result == null && !string.IsNullOrWhiteSpace(reason),
            "An active facility use must reject replacement.");
        Require(!source.isDestroy && source.RequirePersistentInstanceId().Value == sourceId
                && ReferenceEquals(world.Grid.GetGridCell(world.SourcePosition)
                    .GetOccupant(GridLayer.Building), source)
                && JsonUtility.ToJson(source.FacilityState) == stateBefore,
            "Rejected replacement changed the source state or occupancy.");
    }

    private static FacilityRuntimeState CreateOperationalState(BuildableObject source) => new()
    {
        completedUses = 4,
        completedWorkCycles = 3,
        cleanliness = 67.5f,
        remainingFuelGameSeconds = 125f,
        nextFuelOperationSequence = 3,
        nextUseOperationSequence = 7L,
        nextVandalismOperationSequence = 5L,
        synthesisOutcomeRevision = 9L,
        acknowledgedUseRanges = new List<FacilityUseAcknowledgementRange>
        {
            new() { first = 1L, last = 2L },
            new() { first = 4L, last = 5L }
        },
        lastVandalism = new FacilityVandalismCommitState
        {
            phase = (int)FacilityVandalismCommitPhase.Applied,
            operationSequence = 4L,
            facilityId = source.RequirePersistentInstanceId().Value,
            buildingDefinitionId = source.BuildingData.id.ToString(),
            actorId = "character:vandalism-fixture",
            actorName = "Visitor",
            facilityName = "Dining",
            source = "macro",
            reason = "fixture",
            absoluteDay = 1,
            damagedAfter = true,
            publicationKey = "publication:fixture:vandalism",
            witnessPublicationKeys = new List<string>
            {
                "publication:fixture:witness-a",
                "publication:fixture:witness-b"
            }
        }
    };
}
#endif
