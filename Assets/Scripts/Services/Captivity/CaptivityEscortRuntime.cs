using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CaptivityEscortRuntime :
    ICaptivityEscortRuntime,
    ICaptivityEscortRestoreLifecycle
{
    private readonly CaptivityActorAccess actors;
    private readonly CaptivityActorRuntimeLookup actorRuntime;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly ICombatEquipmentRuntime combatEquipment;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IGridSystemProvider gridProvider;
    private readonly IDoorAccessCommandService doorAccessCommands;
    private readonly IGameClock gameClock;
    private readonly Dictionary<string, Transform> carriedParents =
        new Dictionary<string, Transform>(StringComparer.Ordinal);

    public CaptivityEscortRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        CaptivityCharacterContext characters,
        CaptivityWorldContext world,
        CaptivitySessionContext session)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime
            ?? throw new ArgumentNullException(nameof(actorRuntime));
        characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        world = world ?? throw new ArgumentNullException(nameof(world));
        session = session ?? throw new ArgumentNullException(nameof(session));
        bodyHealthQuery = characters.BodyHealthQuery;
        bodyHealthCommands = characters.BodyHealthCommands;
        combatEquipment = characters.CombatEquipment;
        itemRuntime = characters.ItemRuntime;
        gridProvider = world.GridProvider;
        doorAccessCommands = world.DoorAccessCommands;
        gameClock = session.GameClock;
    }

    public void ClearTransientState()
    {
        carriedParents.Clear();
    }

    public void RestoreCaptiveParent(string captiveId)
    {
        RestoreParent(captiveId, actorRuntime.Find(captiveId));
    }

    public bool TryGetEscortState(
        string captiveId,
        CharacterActor carrier,
        out CaptiveState captive,
        out CharacterActor subject,
        out string failureReason)
    {
        CaptiveState state = actors.FindState(captiveId);
        subject = actorRuntime.Find(captiveId);
        captive = state;
        failureReason = string.Empty;
        if (state == null
            || subject == null
            || carrier == null
            || !string.Equals(
                state.reservedCarrierId,
                CaptivityActorAccess.RequireCharacterId(
                    carrier?.Identity?.PersistentId),
                StringComparison.Ordinal))
        {
            failureReason = "호송 예약이 유효하지 않습니다.";
            return false;
        }

        return true;
    }

    public IDisposable BeginEscortPass(CharacterActor carrier, string captiveId)
    {
        DoorAccessSubjectRef subject = new DoorAccessSubjectRef(
            CaptivityActorAccess.RequireCharacterId(
                carrier?.Identity?.PersistentId),
            carrier != null && carrier.IsOwner
                ? DoorAccessGroup.Owner
                : DoorAccessGroup.Staff,
            character: carrier);
        return doorAccessCommands.BeginTemporaryOverride(
            subject,
            DoorAccessOverrideKind.EscortPass,
            $"escort:{captiveId?.Trim() ?? string.Empty}");
    }

    public bool TryPickupReservedRestraint(
        CaptiveState captive,
        CharacterActor carrier,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(carrier);
        if (inventory == null)
        {
            failureReason = "운반 인벤토리를 사용할 수 없습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(captive.assignedRestraintItemId)
            || inventory.CountItem(captive.restraintItemId) > 0)
        {
            return true;
        }

        WorldItemReservedStackQuantity reservation =
            new WorldItemReservedStackQuantity(
                captive.restraintStackId,
                captive.restraintItemId,
                Mathf.Max(1, captive.restraintQuantity),
                captive.restraintPickupPosition,
                WorldItemHaulDestinationKind.Warehouse,
                string.Empty);
        return itemRuntime.TryPickupReservedStackQuantity(
            carrier,
            inventory,
            reservation,
            out int pickedUp,
            out failureReason)
            && pickedUp > 0;
    }

    public float AdvanceStabilization(
        string captiveId,
        CharacterActor carrier,
        float workAmount)
    {
        if (!TryGetEscortState(
                captiveId,
                carrier,
                out CaptiveState state,
                out CharacterActor subject,
                out _))
        {
            return 0f;
        }

        state.completedInteractionWork = Mathf.Min(
            state.requiredInteractionWork,
            state.completedInteractionWork + Mathf.Max(0f, workAmount));
        if (state.completedInteractionWork + 0.001f >= state.requiredInteractionWork)
        {
            state.stabilized = bodyHealthCommands.Stabilize(subject)
                || bodyHealthQuery.GetTotalBleeding(subject) <= 0.001f;
            state.status = CaptivityStatus.AwaitingEscort;
            state.lastResult = "현장 안정화 완료";
        }

        return state.completedInteractionWork
            / Mathf.Max(0.01f, state.requiredInteractionWork);
    }

    public bool TryBeginEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason)
    {
        if (!TryGetEscortState(
                captiveId,
                carrier,
                out CaptiveState state,
                out CharacterActor subject,
                out failureReason))
        {
            return false;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(carrier);
        if (!state.stabilized)
        {
            failureReason = "먼저 현장 안정화가 필요합니다.";
            return false;
        }

        if (inventory == null)
        {
            failureReason = "구속구가 없습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(state.assignedRestraintItemId))
        {
            // A save captured during escort restores the already attached physical
            // restraint through CaptiveState rather than creating a duplicate item.
        }
        else if (string.Equals(
                state.restraintItemId,
                CaptivityItemDefinitions.ReinforcedRestraintItemId,
                StringComparison.Ordinal))
        {
            if (!inventory.TryTakeItem(
                    state.restraintItemId,
                    out CharacterCarriedItemSaveData restraint))
            {
                failureReason = "강화 구속구를 찾을 수 없습니다.";
                return false;
            }

            if (!CaptivityDurableToolRuntime.TryAssignRestraint(
                    state,
                    restraint,
                    out failureReason))
            {
                inventory.TryAddPartialStack(
                    restraint.sourceStackId,
                    restraint.itemInstanceId,
                    restraint.itemId,
                    restraint.quantity,
                    itemRuntime.CatalogProvider,
                    itemRuntime.HaulingSettingsProvider,
                    restraint.wasteOrigin,
                    restraint.contamination,
                    restraint.components,
                    out _,
                    out _);
                failureReason = string.IsNullOrWhiteSpace(failureReason)
                    ? "강화 구속구를 장착할 수 없습니다."
                    : failureReason;
                return false;
            }
        }
        else if (!inventory.TryConsumeItem(state.restraintItemId, 1))
        {
            failureReason = "구속구가 없습니다.";
            return false;
        }

        state.restraintStackId = string.Empty;
        state.restraintQuantity = 0;

        ConfiscateEquipment(subject, state.capturePosition);
        state.equipmentConfiscated = true;
        state.restrained = true;
        carriedParents[state.captiveId] = subject.transform.parent;
        subject.transform.SetParent(carrier.transform, worldPositionStays: false);
        subject.transform.localPosition = new Vector3(-0.28f, 0.16f, 0f);
        state.status = CaptivityStatus.Escorting;
        state.lastResult = "감방으로 호송 중";
        return true;
    }

    public bool TryCompleteEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason)
    {
        if (!TryGetEscortState(
                captiveId,
                carrier,
                out CaptiveState state,
                out CharacterActor subject,
                out failureReason)
            || !gridProvider.TryGetGrid(out Grid grid)
            || !grid.IsValidGridPos(state.housingPosition))
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "감방 위치가 유효하지 않습니다."
                : failureReason;
            return false;
        }

        RestoreParent(state.captiveId, subject);
        subject.transform.position = grid.GetWorldPos(state.housingPosition);
        subject.SetAiPaused(true);
        subject.characterType = CharacterType.Intruder;
        subject.SetLifecycleState(CharacterLifecycleState.Downed);
        state.status = CaptivityStatus.Confined;
        state.reservedCarrierId = string.Empty;
        state.restraintStackId = string.Empty;
        state.restraintItemId = string.Empty;
        state.restraintQuantity = 0;
        state.health = EstimateHealth(subject);
        state.nextSecurityCheckAt = gameClock.Time + 5f;
        state.lastResult = "감방 수용 완료";
        actors.Recalculate(state);
        return true;
    }

    public void FailEscort(string captiveId, CharacterActor carrier, string reason)
    {
        CaptiveState state = actors.FindState(captiveId);
        CharacterActor subject = actorRuntime.Find(captiveId);
        if (state == null)
        {
            return;
        }

        if (subject != null)
        {
            RestoreParent(state.captiveId, subject);
            if (carrier != null)
            {
                subject.transform.position = carrier.transform.position;
                state.capturePosition = carrier.GetNowXY();
            }
        }

        if (!string.IsNullOrWhiteSpace(state.restraintStackId))
        {
            itemRuntime.ReleaseReservation(
                state.restraintStackId,
                state.reservedCarrierId);
        }

        CaptivityDurableToolRuntime.TryReturnRestraint(
            itemRuntime,
            state,
            state.capturePosition);

        state.status = CaptivityStatus.AwaitingCapture;
        state.reservedCarrierId = string.Empty;
        state.housingBuildingId = string.Empty;
        state.restraintStackId = string.Empty;
        state.restraintItemId = string.Empty;
        state.restraintQuantity = 0;
        state.lastResult = string.IsNullOrWhiteSpace(reason)
            ? "포획 중단"
            : reason;
    }

    private void ConfiscateEquipment(CharacterActor subject, Vector2Int position)
    {
        foreach (CombatEquipmentInstance instance in
                 combatEquipment.ConfiscateAllFromCharacter(
                     CaptivityActorAccess.RequireCharacterId(
                         subject?.Identity?.PersistentId)))
        {
            combatEquipment.TryDropExistingEquipmentToWorld(
                instance.instanceId,
                position,
                out _,
                out _);
        }
    }

    private void RestoreParent(string captiveId, CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        carriedParents.TryGetValue(captiveId ?? string.Empty, out Transform parent);
        carriedParents.Remove(captiveId ?? string.Empty);
        actor.transform.SetParent(parent, worldPositionStays: true);
    }

    private static float EstimateHealth(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            return 0f;
        }

        return Mathf.Clamp(
            actor.Stats.CurrentHealth / Mathf.Max(1f, actor.Stats.MaxHealth) * 100f,
            0f,
            100f);
    }
}

internal static class CaptivityDurableToolRuntime
{
    private const float RestraintUseWear = 5f;

    public static bool TryAssignRestraint(
        CaptiveState state,
        CharacterCarriedItemSaveData item,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (state == null
            || item == null
            || !string.Equals(
                item.itemId,
                CaptivityItemDefinitions.ReinforcedRestraintItemId,
                StringComparison.Ordinal)
            || !((ItemInstanceId)item.itemInstanceId).IsValid)
        {
            failureReason = "강화 구속구의 물리 인스턴스가 유효하지 않습니다.";
            return false;
        }

        float current = DurableToolItemRules.ReadCurrentDurability(
            item.itemId,
            item.components);
        if (!DurableToolItemRules.TryGetMaximumDurability(
                item.itemId,
                out float maximum)
            || current <= RestraintUseWear)
        {
            failureReason = "강화 구속구가 파손되어 사용할 수 없습니다.";
            return false;
        }

        state.assignedRestraintItemId = item.itemId;
        state.assignedRestraintInstanceId = item.itemInstanceId;
        state.assignedRestraintMaximumDurability = maximum;
        state.assignedRestraintDurability = Mathf.Max(
            0f,
            current - RestraintUseWear);
        return true;
    }

    public static bool TryAssignLaborTool(
        CaptiveState state,
        WorldItemStackSnapshot item)
    {
        if (state == null
            || item == null
            || !string.Equals(
                item.ItemId,
                CaptivityItemDefinitions.PrisonerWorkKitItemId,
                StringComparison.Ordinal)
            || !((ItemInstanceId)item.ItemInstanceId).IsValid
            || !DurableToolItemRules.TryGetMaximumDurability(
                item.ItemId,
                out float maximum))
        {
            return false;
        }

        float current = DurableToolItemRules.ReadCurrentDurability(
            item.ItemId,
            item.Components);
        if (current <= 0f)
        {
            return false;
        }

        state.assignedLaborToolItemId = item.ItemId;
        state.assignedLaborToolInstanceId = item.ItemInstanceId;
        state.assignedLaborToolDurability = current;
        state.assignedLaborToolMaximumDurability = maximum;
        return true;
    }

    public static bool TryReturnRestraint(
        IWorldItemStackRuntime items,
        CaptiveState state,
        Vector2Int position) =>
        TryReturn(
            items,
            state?.assignedRestraintItemId,
            state?.assignedRestraintInstanceId,
            state?.assignedRestraintDurability ?? 0f,
            position,
            () =>
            {
                state.assignedRestraintItemId = string.Empty;
                state.assignedRestraintInstanceId = string.Empty;
                state.assignedRestraintDurability = 0f;
                state.assignedRestraintMaximumDurability = 0f;
            });

    public static bool TryReturnLaborTool(
        IWorldItemStackRuntime items,
        CaptiveState state,
        Vector2Int position) =>
        TryReturn(
            items,
            state?.assignedLaborToolItemId,
            state?.assignedLaborToolInstanceId,
            state?.assignedLaborToolDurability ?? 0f,
            position,
            () =>
            {
                state.assignedLaborToolItemId = string.Empty;
                state.assignedLaborToolInstanceId = string.Empty;
                state.assignedLaborToolDurability = 0f;
                state.assignedLaborToolMaximumDurability = 0f;
                state.nextLaborToolWearAt = 0f;
            });

    private static bool TryReturn(
        IWorldItemStackRuntime items,
        string itemId,
        string instanceId,
        float durability,
        Vector2Int position,
        Action clear)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return true;
        }

        if (items == null
            || !((ItemInstanceId)instanceId).IsValid
            || !items.SpawnExistingUniqueItemAt(
                itemId,
                (ItemInstanceId)instanceId,
                position,
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId))
        {
            return false;
        }

        items.TrySetInstanceComponent(
            stackId,
            DurableToolItemRules.CreateDurability(itemId, durability));
        clear?.Invoke();
        return true;
    }
}
