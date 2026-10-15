using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class InvasionActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable facilityDamagedSubscription;
    private IDisposable suppressedSubscription;
    private IDisposable defenseFailedSubscription;
    private IDisposable structureHitSubscription;

    public InvasionActivityObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize()
    {
        facilityDamagedSubscription ??=
            events.Subscribe<InvasionFacilityDamagedEvent>(
                ObserveFacilityDamaged);
        suppressedSubscription ??=
            events.Subscribe<InvasionSuppressedEvent>(ObserveSuppressed);
        defenseFailedSubscription ??=
            events.Subscribe<InvasionDefenseFailedEvent>(ObserveDefenseFailed);
        structureHitSubscription ??=
            events.Subscribe<InvasionStructureHitEvent>(ObserveStructureHit);
    }

    public void Dispose()
    {
        facilityDamagedSubscription?.Dispose();
        suppressedSubscription?.Dispose();
        defenseFailedSubscription?.Dispose();
        structureHitSubscription?.Dispose();
        facilityDamagedSubscription = null;
        suppressedSubscription = null;
        defenseFailedSubscription = null;
        structureHitSubscription = null;
    }

    public static CharacterActivityEvent Project(
        in InvasionFacilityDamagedEvent value) =>
        CharacterActivityEvent.Facility(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Damaged,
            $"{value.facility.name} 손상",
            value.facility,
            actionId: "invasion:damage-facility",
            value: 1f,
            bubbleEligible: true);

    public static CharacterActivityEvent Project(
        in InvasionSuppressedEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Defeated,
            value.HasDefender
                ? $"{value.DefenderName}에게 제압됨"
                : "제압됨",
            actionId: "invasion:suppressed",
            targetId: value.DefenderId,
            targetName: value.DefenderName,
            sentiment: -0.9f,
            bubbleEligible: true);

    public static CharacterActivityEvent Project(
        in InvasionDefenseFailedEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Completed,
            "최종 방어선 돌파",
            actionId: "invasion:owner-defeated",
            targetId: value.OwnerId ?? "owner",
            targetName: value.OwnerName ?? "사장",
            sentiment: 0.8f,
            bubbleEligible: true);

    public static CharacterActivityEvent Project(
        in InvasionStructureHitEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Damaged,
            $"{value.TargetRuntimeName} 돌파 {value.Damage:0.#}",
            actionId: "invasion:breach-structure",
            targetId: value.TargetId,
            targetName: value.TargetDisplayName,
            reasonCode: value.Enraged ? "enraged-breach" : "breach",
            value: value.Damage,
            facilityId: value.TargetDefinitionId,
            sentiment: -0.7f,
            bubbleEligible: true);

    private void ObserveFacilityDamaged(InvasionFacilityDamagedEvent value) =>
        TryPresent(
            () => value.intruderActor.AddActivity(Project(value)),
            "invasion-facility-damage-activity-observer");

    private void ObserveSuppressed(InvasionSuppressedEvent value) =>
        TryPresent(
            () => value.Actor?.AddActivity(Project(value)),
            "invasion-suppressed-activity-observer");

    private void ObserveDefenseFailed(InvasionDefenseFailedEvent value) =>
        TryPresent(
            () => value.Actor?.AddActivity(Project(value)),
            "invasion-owner-defeated-activity-observer");

    private void ObserveStructureHit(InvasionStructureHitEvent value) =>
        TryPresent(
            () => value.Actor.AddActivity(Project(value)),
            "invasion-structure-hit-activity-observer");

    private static void TryPresent(Action callback, string context)
    {
        try
        {
            callback();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(context + ":" + exception.GetType().Name);
        }
    }
}

public static class InvasionActivityObserverRegistration
{
    public static void RegisterInvasionActivityObserver(
        this IContainerBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.RegisterEntryPoint<InvasionActivityObserver>(
            Lifetime.Singleton);
    }
}
