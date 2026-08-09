using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IOffenseExpeditionReturnPort
{
    void Begin(string expeditionId);
    void ReleaseResources(OffenseExpeditionRun expedition, bool hasSurvivor);
    bool TryBeginMemberReturn(
        string expeditionId,
        CharacterActor actor,
        Action completed);
    void EndMemberImmediately(CharacterActor actor, bool survived);
    void HandleMemberDeath(CharacterActor actor);
    void Seal(string expeditionId);
}

public interface IOffenseExpeditionReturnCoordinator
{
    void Complete(
        OffenseExpeditionRun expedition,
        bool success,
        string message,
        List<OffenseExpeditionResult> resultHistory,
        Action stateChanged);
}

public sealed class OffenseExpeditionReturnPort : IOffenseExpeditionReturnPort
{
    private readonly IOffensePreparationService preparation;
    private readonly IExpeditionReturnService returnService;
    private readonly IOffenseReturnArrivalRuntime arrivals;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IGameMoneyAccount money;
    private readonly IGameEventBus events;
    private readonly IWorldDropZoneQuery dropZones;

    public OffenseExpeditionReturnPort(
        IOffensePreparationService preparation,
        IExpeditionReturnService returnService,
        IOffenseReturnArrivalRuntime arrivals,
        ICombatEquipmentRuntime equipment,
        IGameMoneyAccount money,
        IGameEventBus events,
        IWorldDropZoneQuery dropZones)
    {
        this.preparation = preparation
            ?? throw new ArgumentNullException(nameof(preparation));
        this.returnService = returnService
            ?? throw new ArgumentNullException(nameof(returnService));
        this.arrivals = arrivals
            ?? throw new ArgumentNullException(nameof(arrivals));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
    }

    public void Begin(string expeditionId)
    {
        arrivals.BeginExpeditionReturn(expeditionId);
    }

    public void ReleaseResources(
        OffenseExpeditionRun expedition,
        bool hasSurvivor)
    {
        if (!hasSurvivor)
        {
            preparation.AbandonPackedSupplies(expedition.ExpeditionId);
            return;
        }

        preparation.ReturnSupplies(
            expedition.Supplies,
            expedition.ExpeditionId);
        preparation.DepositLoot(expedition.CarriedStock);
        MaterializeRecoveredEquipment(expedition);
        int returningFunds = expedition.TakeReturningFieldFunds();
        if (returningFunds > 0)
        {
            money.Add(
                returningFunds,
                new EconomyTransactionContext(
                    EconomyTransactionKind.ExpeditionFieldFundReturn,
                    expedition.ExpeditionId,
                    description: "expedition-field-fund-return"));
        }

        events.RaiseAlert(
            "expedition-cargo-arrived",
            "expedition-cargo-unloaded",
            EventAlertImportance.Low,
            "offense");
    }

    private void MaterializeRecoveredEquipment(OffenseExpeditionRun expedition)
    {
        if (expedition.RecoveredEquipmentInstanceIds.Count == 0)
        {
            return;
        }
        if (!dropZones.TryGetExpeditionLootDropoff(out Vector2Int dropoff))
        {
            events.RaiseAlert(
                "원정 장비 하역 실패",
                "전리품 하역 지점을 찾지 못해 회수 장비가 대기 중입니다.",
                EventAlertImportance.High,
                "offense");
            return;
        }

        List<string> failures = new List<string>();
        foreach (string instanceId in expedition.RecoveredEquipmentInstanceIds
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!equipment.TryMaterializeRecoveredEquipment(
                    instanceId,
                    dropoff,
                    out string failure))
            {
                failures.Add($"{instanceId}: {failure}");
            }
        }
        if (failures.Count > 0)
        {
            events.RaiseAlert(
                "원정 장비 하역 일부 실패",
                string.Join(" | ", failures),
                EventAlertImportance.High,
                "offense");
        }
    }

    public bool TryBeginMemberReturn(
        string expeditionId,
        CharacterActor actor,
        Action completed)
    {
        arrivals.RegisterReturningMember(expeditionId);
        bool started = returnService.TryBeginReturn(
            actor,
            true,
            () =>
            {
                arrivals.CompleteReturningMember(expeditionId);
                completed?.Invoke();
            },
            out _);
        if (!started)
        {
            arrivals.CompleteReturningMember(expeditionId);
        }

        return started;
    }

    public void EndMemberImmediately(CharacterActor actor, bool survived)
    {
        actor?.EndExpedition(survived);
    }

    public void HandleMemberDeath(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        equipment.HandleCharacterDeath(actor?.Identity?.PersistentId);
    }

    public void Seal(string expeditionId)
    {
        arrivals.SealExpeditionReturn(expeditionId);
    }
}

/// <summary>
/// Explicit capability for isolated aggregate tests that do not compose the
/// exterior return presentation. It never stores state and is safe to share.
/// </summary>
public sealed class NoOpOffenseExpeditionReturnPort :
    IOffenseExpeditionReturnPort
{
    public static readonly NoOpOffenseExpeditionReturnPort Instance = new();

    private NoOpOffenseExpeditionReturnPort()
    {
    }

    public void Begin(string expeditionId)
    {
    }

    public void ReleaseResources(OffenseExpeditionRun expedition, bool hasSurvivor)
    {
    }

    public bool TryBeginMemberReturn(
        string expeditionId,
        CharacterActor actor,
        Action completed)
    {
        return false;
    }

    public void EndMemberImmediately(CharacterActor actor, bool survived)
    {
        actor?.EndExpedition(survived);
    }

    public void HandleMemberDeath(CharacterActor actor)
    {
    }

    public void Seal(string expeditionId)
    {
    }
}

public sealed class OffenseExpeditionReturnCoordinator :
    IOffenseExpeditionReturnCoordinator
{
    private readonly IOffenseExpeditionReturnPort returnPort;
    private readonly IOffenseExpeditionResultFinalizer resultFinalizer;
    private readonly IGameEventBus events;

    public OffenseExpeditionReturnCoordinator(
        IOffenseExpeditionReturnPort returnPort,
        IOffenseExpeditionResultFinalizer resultFinalizer,
        IGameEventBus events)
    {
        this.returnPort = returnPort
            ?? throw new ArgumentNullException(nameof(returnPort));
        this.resultFinalizer = resultFinalizer
            ?? throw new ArgumentNullException(nameof(resultFinalizer));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Complete(
        OffenseExpeditionRun expedition,
        bool success,
        string message,
        List<OffenseExpeditionResult> resultHistory,
        Action stateChanged)
    {
        if (expedition == null)
        {
            return;
        }

        returnPort.Begin(expedition.ExpeditionId);
        bool hasSurvivor = expedition.MemberStates.Any(member =>
                member?.Actor != null && !member.Actor.IsDead)
            || expedition.ProtectedRescueActors.Any(actor =>
                actor != null && !actor.IsDead);
        List<OffenseExpeditionMemberSnapshot> members = new();
        int returningAnimations = 0;
        bool registrationSealed = false;
        bool resolved = false;
        OffenseExpeditionResult pendingResult = null;

        void ResolveIfReady()
        {
            if (resolved
                || !registrationSealed
                || returningAnimations > 0
                || pendingResult == null)
            {
                return;
            }

            resolved = true;
            returnPort.ReleaseResources(expedition, hasSurvivor);
            pendingResult = resultFinalizer.Finalize(
                expedition,
                pendingResult,
                resultHistory);
            returnPort.Seal(expedition.ExpeditionId);
            stateChanged?.Invoke();
            if (!string.IsNullOrWhiteSpace(message))
            {
                events.RaiseAlert(
                    success ? "expedition-returned" : "expedition-ended",
                    message,
                    success
                        ? EventAlertImportance.Medium
                        : EventAlertImportance.High,
                    "offense");
            }
        }

        void RegisterActor(
            CharacterActor actor,
            float stress,
            float damageTaken,
            bool awardSuccessExperience)
        {
            if (actor == null)
            {
                return;
            }

            bool survived = !actor.IsDead;
            actor.Lifecycle?.RecordExpeditionReturn(stress, survived);
            bool animated = false;
            if (survived)
            {
                returningAnimations++;
                animated = returnPort.TryBeginMemberReturn(
                    expedition.ExpeditionId,
                    actor,
                    () =>
                    {
                        returningAnimations = Mathf.Max(
                            0,
                            returningAnimations - 1);
                        ResolveIfReady();
                        stateChanged?.Invoke();
                    });
                if (!animated)
                {
                    returningAnimations = Mathf.Max(
                        0,
                        returningAnimations - 1);
                }
            }

            if (!animated)
            {
                returnPort.EndMemberImmediately(actor, survived);
            }

            if (success && survived && awardSuccessExperience)
            {
                actor.Progression?.AddExperience(
                    OffenseExpeditionRuntime.CalculateSuccessfulReturnExperience(
                        expedition));
            }
            else if (!survived)
            {
                returnPort.HandleMemberDeath(actor);
            }

            actor.EnsureRuntimeState();
            members.Add(new OffenseExpeditionMemberSnapshot(
                actor.Identity?.DisplayName ?? actor.name,
                actor.Identity?.SpeciesTag ?? string.Empty,
                OffenseExpeditionService.CalculateMemberPower(actor),
                survived,
                damageTaken));
        }

        foreach (OffenseExpeditionMemberState member in expedition.MemberStates)
        {
            RegisterActor(
                member.Actor,
                member.Stress,
                member.TotalDamageTaken,
                awardSuccessExperience: true);
        }

        foreach (CharacterActor protectedActor in expedition.ProtectedRescueActors)
        {
            RegisterActor(
                protectedActor,
                stress: 0f,
                damageTaken: 0f,
                awardSuccessExperience: false);
        }

        pendingResult = new OffenseExpeditionResult(
            expedition.ExpeditionId,
            expedition.Target.id,
            expedition.Target.title,
            success,
            expedition.TotalPower,
            expedition.Target.requiredPower,
            expedition.Target.danger,
            expedition.TotalDurationSeconds - expedition.RemainingSeconds,
            members,
            success
                ? expedition.Target.rewards?
                    .Where(reward => reward != null)
                    .Select(reward => reward.ToSummaryText())
                    .ToArray() ?? Array.Empty<string>()
                : Array.Empty<string>());
        registrationSealed = true;
        ResolveIfReady();
    }
}
