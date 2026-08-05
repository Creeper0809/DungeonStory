using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class CharacterMedicalAggregateState
{
    internal List<CharacterMedicalOrder> Orders { get; } = new();
    internal int OrderSequence { get; set; }

    internal CharacterMedicalAggregateState Clone()
    {
        CharacterMedicalAggregateState clone = new()
        {
            OrderSequence = OrderSequence
        };
        foreach (CharacterMedicalOrder order in Orders)
        {
            clone.Orders.Add(CharacterMedicalOrderPersistence.Clone(order));
        }

        return clone;
    }
}

internal sealed class CharacterMedicalDownedRegistration
{
    internal CharacterMedicalDownedRegistration(
        Grid grid,
        UnityEngine.Vector2Int position,
        DownedCharacterGridOccupant occupant)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Position = position;
        Occupant = occupant
            ?? throw new ArgumentNullException(nameof(occupant));
    }

    internal Grid Grid { get; }
    internal UnityEngine.Vector2Int Position { get; }
    internal DownedCharacterGridOccupant Occupant { get; }
}

public sealed class CharacterMedicalRestoreCandidate
{
    internal CharacterMedicalRestoreCandidate(CharacterMedicalAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CharacterMedicalAggregateState State { get; }

    internal Dictionary<string, CharacterMedicalDownedRegistration>
        DownedRegistrations { get; } = new(StringComparer.Ordinal);

    internal HashSet<CharacterActor> DownedPatients { get; } = new();
}

internal sealed class CharacterMedicalOrderViewSnapshot
{
    internal CharacterMedicalOrderViewSnapshot(
        IReadOnlyList<CharacterMedicalOrder> view,
        List<CharacterMedicalOrder> source)
    {
        View = view;
        Source = source;
    }

    internal IReadOnlyList<CharacterMedicalOrder> View { get; }
    internal List<CharacterMedicalOrder> Source { get; }
}

internal sealed class CharacterMedicalPatientPhaseSnapshot
{
    internal CharacterMedicalPatientPhaseSnapshot(CharacterActor actor)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        LifecycleState = actor.CurrentLifecycleState;
        AiPaused = actor.IsAiPaused();
        DecisionState = actor.State;

        AIBrain brain = actor.Brain;
        if (brain == null)
        {
            return;
        }

        HasBrain = true;
        BestAction = brain.bestAction;
        IsExecuted = brain.isExecuted;
        IsBestActionEnd = brain.isBestActionEnd;
        ActionPhase = brain.CurrentActionPhase;
        ActionPhaseDetail = brain.CurrentActionPhaseDetail;
    }

    internal CharacterActor Actor { get; }
    internal CharacterLifecycleState LifecycleState { get; }
    internal bool AiPaused { get; }
    internal CharacterDecisionState DecisionState { get; }
    internal bool HasBrain { get; }
    internal AIAction BestAction { get; }
    internal bool IsExecuted { get; }
    internal bool IsBestActionEnd { get; }
    internal string ActionPhase { get; }
    internal string ActionPhaseDetail { get; }

    internal void Restore()
    {
        if (Actor == null)
        {
            return;
        }

        Actor.SetLifecycleState(LifecycleState);
        Actor.SetAiPaused(AiPaused);
        Actor.state = DecisionState;
        AIBrain brain = Actor.Brain;
        if (!HasBrain || brain == null)
        {
            return;
        }

        brain.bestAction = BestAction;
        brain.isExecuted = IsExecuted;
        brain.isBestActionEnd = IsBestActionEnd;
        brain.SetActionPhase(
            ActionPhase,
            destination: null,
            detail: ActionPhaseDetail);
    }
}

internal sealed class CharacterMedicalPublication
{
    internal CharacterMedicalPublication(
        CharacterMedicalRestoreCandidate candidate,
        IReadOnlyDictionary<string, CharacterMedicalDownedRegistration>
            previousDownedRegistrations,
        IDictionary<string, Transform> previousCarriedPatientParents,
        IDictionary<string, string> previousTreatmentFacilityReservations,
        CharacterMedicalOrderViewSnapshot previousOrderView)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        PreviousDownedRegistrations = new Dictionary<
            string,
            CharacterMedicalDownedRegistration>(
                previousDownedRegistrations
                ?? throw new ArgumentNullException(
                    nameof(previousDownedRegistrations)),
                StringComparer.Ordinal);
        PreviousCarriedPatientParents = new Dictionary<string, Transform>(
            previousCarriedPatientParents
            ?? throw new ArgumentNullException(
                nameof(previousCarriedPatientParents)),
            StringComparer.Ordinal);
        PreviousTreatmentFacilityReservations =
            new Dictionary<string, string>(
                previousTreatmentFacilityReservations
                ?? throw new ArgumentNullException(
                    nameof(previousTreatmentFacilityReservations)),
                StringComparer.Ordinal);
        PreviousOrderView = previousOrderView
            ?? throw new ArgumentNullException(nameof(previousOrderView));
        foreach (CharacterActor patient in candidate.DownedPatients)
        {
            if (patient != null)
            {
                PreviousPatientPhases.Add(
                    new CharacterMedicalPatientPhaseSnapshot(patient));
            }
        }
    }

    internal CharacterMedicalRestoreCandidate Candidate { get; }
    internal IReadOnlyDictionary<string, CharacterMedicalDownedRegistration>
        PreviousDownedRegistrations { get; }
    internal IReadOnlyDictionary<string, Transform>
        PreviousCarriedPatientParents { get; }
    internal IReadOnlyDictionary<string, string>
        PreviousTreatmentFacilityReservations { get; }
    internal CharacterMedicalOrderViewSnapshot PreviousOrderView { get; }
    internal List<CharacterMedicalPatientPhaseSnapshot> PreviousPatientPhases { get; }
        = new();
}
