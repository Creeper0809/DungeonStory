using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface ICombatAmmoResupplyRuntime
{
    bool TryRequestAmmoResupply(CharacterActor actor, out string message);
    bool IsResupplying(CharacterActor actor);
}

public interface ICombatEquipmentPickupRuntime
{
    bool TryRequestEquipmentPickup(
        CharacterActor actor,
        string equipmentDefinitionId,
        out string message);
    bool TryUnequipToWorld(
        CharacterActor actor,
        CombatEquipmentLoadoutSlot slot,
        out string message);
}

public sealed class CombatLoadoutPreparationRuntime :
    IInitializable,
    ITickable,
    IDisposable,
    ICombatAmmoResupplyRuntime,
    ICombatEquipmentPickupRuntime
{
    private sealed class PreparationRequest
    {
        public string DefinitionId = string.Empty;
        public string ItemId = string.Empty;
        public int Quantity = 1;
        public bool IsEquipment;
    }

    private sealed class ActorPreparationState
    {
        public CharacterActor Actor;
        public CharacterCarryInventory Inventory;
        public Queue<PreparationRequest> Pending = new Queue<PreparationRequest>();
        public PreparationRequest Current;
        public WorldItemReservedStackQuantity Reservation;
        public Vector2Int PickupStand;
        public bool Moving;
        public bool Finished;
        public bool CombatResupply;
        public bool WasAiPaused;
        public float NextReservationAttemptAt;
    }

    private readonly ICharacterWorldQuery characterWorld;
    private readonly IGridSystemProvider gridProvider;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly ICombatEquipmentRuntime equipmentRuntime;
    private readonly ICombatEquipmentCatalog equipmentCatalog;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly Dictionary<string, ActorPreparationState> states =
        new Dictionary<string, ActorPreparationState>(StringComparer.Ordinal);
    private readonly List<ActorPreparationState> tickStates =
        new List<ActorPreparationState>();
    private readonly List<string> completedActorIds = new List<string>();
    private IDisposable threatWarningSubscription;
    private IDisposable invasionResolvedSubscription;

    public CombatLoadoutPreparationRuntime(
        ICharacterWorldQuery characterWorld,
        IGridSystemProvider gridProvider,
        IWorldItemStackRuntime itemRuntime,
        ICombatEquipmentRuntime equipmentRuntime,
        ICombatEquipmentCatalog equipmentCatalog,
        IGridPathSearchBroker pathSearchBroker,
        IGameEventBus gameEventBus,
        IGameClock gameClock)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.equipmentRuntime = equipmentRuntime
            ?? throw new ArgumentNullException(nameof(equipmentRuntime));
        this.equipmentCatalog = equipmentCatalog
            ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public void Initialize()
    {
        threatWarningSubscription = gameEventBus.Subscribe<InvasionThreatWarningEvent>(
            OnThreatWarning);
        invasionResolvedSubscription = gameEventBus.Subscribe<InvasionResolvedEvent>(OnTriggerEvent);
    }

    public void Dispose()
    {
        threatWarningSubscription?.Dispose();
        threatWarningSubscription = null;
        invasionResolvedSubscription?.Dispose();
        invasionResolvedSubscription = null;
        CancelAll();
    }

    private void OnThreatWarning(InvasionThreatWarningEvent eventType)
    {
        BeginPreparation();
    }

    public void OnTriggerEvent(InvasionResolvedEvent eventType)
    {
        CancelAll();
    }

    public void Tick()
    {
        if (states.Count == 0)
        {
            return;
        }

        tickStates.Clear();
        foreach (ActorPreparationState state in states.Values)
        {
            tickStates.Add(state);
        }

        for (int index = 0; index < tickStates.Count; index++)
        {
            TickActor(tickStates[index]);
        }

        completedActorIds.Clear();
        foreach (KeyValuePair<string, ActorPreparationState> pair in states)
        {
            if (pair.Value == null || pair.Value.Finished)
            {
                completedActorIds.Add(pair.Key);
            }
        }

        for (int index = 0; index < completedActorIds.Count; index++)
        {
            string completedId = completedActorIds[index];
            if (states.TryGetValue(completedId, out ActorPreparationState state))
            {
                FinishActor(state);
            }

            states.Remove(completedId);
        }
    }

    public bool TryRequestAmmoResupply(CharacterActor actor, out string message)
    {
        message = string.Empty;
        if (actor == null || actor.IsDead)
        {
            message = "재보급할 캐릭터가 유효하지 않습니다.";
            return false;
        }

        string actorId = GetId(actor);
        if (states.TryGetValue(actorId, out ActorPreparationState existing))
        {
            message = existing.CombatResupply ? "탄약 재보급 중" : "전투 장비 수령 중";
            return true;
        }

        CharacterCombatLoadoutProfile profile =
            equipmentRuntime.GetActiveProfileSnapshot(actorId);
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        CombatWeaponSO ammunitionWeapon = profile == null
            ? null
            : ResolveDesiredAmmunitionWeapon(profile);
        ItemDefinitionId ammoItemId = ammunitionWeapon == null
            ? default
            : CombatAmmunitionPolicy.GetPreferred(
                ammunitionWeapon.CompatibleAmmunitionItemIds);
        int carriedAmmo = ammunitionWeapon == null || inventory == null
            ? 0
            : CombatAmmunitionPolicy.CountAvailable(
                ammunitionWeapon,
                inventory);
        if (profile == null
            || !ammoItemId.IsValid
            || profile.desiredAmmo <= carriedAmmo)
        {
            message = "추가로 필요한 탄약이 없습니다.";
            return false;
        }

        PreparationRequest request = new PreparationRequest
        {
            ItemId = ammoItemId.Value,
            Quantity = Mathf.Max(1, profile.desiredAmmo - carriedAmmo),
            IsEquipment = false
        };
        bool hasReservation = itemRuntime.TryReserveStoredItemForDirectPickup(
            actor,
            request.ItemId,
            request.Quantity,
            out WorldItemReservedStackQuantity reservation,
            out Vector2Int pickupStand,
            out string failureReason);

        actor.Brain?.StopCurrentActionForReplan("탄약 재보급");
        actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
        bool wasAiPaused = actor.IsAiPaused();
        actor.SetAiPaused(true);
        states[actorId] = new ActorPreparationState
        {
            Actor = actor,
            Inventory = inventory,
            Current = request,
            Reservation = reservation,
            PickupStand = pickupStand,
            CombatResupply = true,
            WasAiPaused = wasAiPaused,
            NextReservationAttemptAt = hasReservation ? 0f : gameClock.Time + 0.5f
        };
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(
            hasReservation ? "창고 탄약 재보급" : "탄약 입고 대기",
            combatActive: true);
        message = hasReservation
            ? "창고 탄약을 예약하고 재보급을 시작합니다."
            : string.IsNullOrWhiteSpace(failureReason)
                ? "탄약이 입고될 때까지 재보급 요청을 유지합니다."
                : $"{failureReason} 재보급 요청은 유지됩니다.";
        return true;
    }

    public bool IsResupplying(CharacterActor actor)
    {
        return actor != null
            && states.TryGetValue(GetId(actor), out ActorPreparationState state)
            && state != null
            && state.CombatResupply;
    }

    public bool TryRequestEquipmentPickup(
        CharacterActor actor,
        string equipmentDefinitionId,
        out string message)
    {
        message = string.Empty;
        string definitionId = equipmentDefinitionId?.Trim() ?? string.Empty;
        if (actor == null
            || actor.IsDead
            || string.IsNullOrWhiteSpace(definitionId)
            || !equipmentCatalog.TryGet(
                definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            message = "수령할 장비 또는 캐릭터가 유효하지 않습니다.";
            return false;
        }

        string actorId = GetId(actor);
        if (states.ContainsKey(actorId))
        {
            message = "이미 장비나 탄약을 수령하고 있습니다.";
            return false;
        }

        PreparationRequest request = new PreparationRequest
        {
            DefinitionId = definitionId,
            ItemId = definition.ItemId,
            Quantity = 1,
            IsEquipment = true
        };
        bool hasReservation = itemRuntime.TryReserveStoredItemForDirectPickup(
            actor,
            request.ItemId,
            1,
            out WorldItemReservedStackQuantity reservation,
            out Vector2Int pickupStand,
            out string failureReason);
        if (!hasReservation)
        {
            message = string.IsNullOrWhiteSpace(failureReason)
                ? "창고에 사용할 수 있는 대체 장비가 없습니다."
                : failureReason;
            return false;
        }

        actor.Brain?.StopCurrentActionForReplan("대체 장비 수령");
        actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
        bool wasAiPaused = actor.IsAiPaused();
        actor.SetAiPaused(true);
        states[actorId] = new ActorPreparationState
        {
            Actor = actor,
            Inventory = CharacterCarryInventory.Ensure(actor),
            Current = request,
            Reservation = reservation,
            PickupStand = pickupStand,
            WasAiPaused = wasAiPaused
        };
        DefenseCombatPresentation.Ensure(actor)?.SetStatus(
            "대체 장비 수령",
            combatActive: false);
        message = $"{definition.DisplayName} 수령을 시작합니다.";
        return true;
    }

    public bool TryUnequipToWorld(
        CharacterActor actor,
        CombatEquipmentLoadoutSlot slot,
        out string message)
    {
        message = string.Empty;
        if (actor == null || actor.IsDead)
        {
            message = "장비를 해제할 캐릭터가 없습니다.";
            return false;
        }

        string actorId = GetId(actor);
        CharacterCombatLoadoutProfile profile =
            equipmentRuntime.GetActiveProfileSnapshot(actorId);
        List<string> instanceIds = slot == CombatEquipmentLoadoutSlot.Weapon
            ? profile?.weaponInstanceIds?.ToList() ?? new List<string>()
            : (profile?.armorInstanceIds ?? new List<string>())
                .Concat(string.IsNullOrWhiteSpace(profile?.shieldInstanceId)
                    ? Array.Empty<string>()
                    : new[] { profile.shieldInstanceId })
                .ToList();
        if (instanceIds.Count == 0)
        {
            message = "해제할 장비가 없습니다.";
            return false;
        }

        List<(string InstanceId, string StackId)> spawned =
            new List<(string InstanceId, string StackId)>();
        Vector2Int dropPosition = actor.GetNowXY();
        foreach (string instanceId in instanceIds)
        {
            if (!equipmentRuntime.TryGetInstance(instanceId, out CombatEquipmentInstance instance)
                || !equipmentCatalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
                || !itemRuntime.SpawnExistingUniqueItemAt(
                    definition.ItemId,
                    (ItemInstanceId)instance.instanceId,
                    dropPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out string stackId))
            {
                foreach ((string _, string spawnedStackId) in spawned)
                {
                    itemRuntime.DeleteStack(spawnedStackId);
                }

                message = "장비를 내려놓을 공간을 만들지 못했습니다.";
                return false;
            }

            spawned.Add((instanceId, stackId));
        }

        if (!equipmentRuntime.TryUnassignSlot(actorId, slot, out message))
        {
            foreach ((string _, string stackId) in spawned)
            {
                itemRuntime.DeleteStack(stackId);
            }

            return false;
        }

        foreach ((string instanceId, string stackId) in spawned)
        {
            if (!equipmentRuntime.TryLinkToWorldStack(
                instanceId,
                stackId,
                CombatEquipmentWorldState.Loose))
            {
                message = "장비와 물리 스택 연결에 실패했습니다.";
                return false;
            }
        }

        message = $"장비 {spawned.Count}개를 바닥에 내려놓았습니다.";
        return true;
    }

    private void BeginPreparation()
    {
        CancelAll();
        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (!IsEligibleGuard(actor))
            {
                continue;
            }

            string characterId = GetId(actor);
            CharacterCombatLoadoutState loadout = equipmentRuntime.GetOrCreateLoadout(characterId);
            if (string.Equals(
                loadout.activeProfileId,
                CombatLoadoutPresetIds.Peace,
                StringComparison.Ordinal))
            {
                equipmentRuntime.TrySetActiveProfile(characterId, CombatLoadoutPresetIds.Combat);
            }

            CharacterCombatLoadoutProfile profile =
                equipmentRuntime.GetActiveProfileSnapshot(characterId);
            CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
            Queue<PreparationRequest> pending = BuildRequests(profile, inventory);
            if (pending.Count == 0)
            {
                continue;
            }

            bool wasAiPaused = actor.IsAiPaused();
            actor.SetAiPaused(true);
            DefenseCombatPresentation.Ensure(actor)?.SetStatus("전투 장비 수령", combatActive: false);
            states[characterId] = new ActorPreparationState
            {
                Actor = actor,
                Inventory = inventory,
                Pending = pending,
                WasAiPaused = wasAiPaused
            };
        }
    }

    private Queue<PreparationRequest> BuildRequests(
        CharacterCombatLoadoutProfile profile,
        CharacterCarryInventory inventory)
    {
        Queue<PreparationRequest> result = new Queue<PreparationRequest>();
        if (profile == null)
        {
            return result;
        }

        HashSet<string> equippedDefinitions = profile.weaponInstanceIds
            .Concat(profile.armorInstanceIds)
            .Append(profile.shieldInstanceId)
            .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId)
                && equipmentRuntime.TryGetInstance(instanceId, out _))
            .Select(instanceId =>
            {
                equipmentRuntime.TryGetInstance(instanceId, out CombatEquipmentInstance instance);
                return instance?.definitionId ?? string.Empty;
            })
            .ToHashSet(StringComparer.Ordinal);

        IEnumerable<string> desiredEquipment = profile.desiredWeaponDefinitionIds
            .Concat(profile.desiredArmorDefinitionIds)
            .Append(profile.desiredShieldDefinitionId)
            .Where(id => !string.IsNullOrWhiteSpace(id));
        foreach (string definitionId in desiredEquipment)
        {
            if (equippedDefinitions.Contains(definitionId)
                || !equipmentCatalog.TryGet(definitionId, out CombatEquipmentDefinitionSO definition))
            {
                continue;
            }

            result.Enqueue(new PreparationRequest
            {
                DefinitionId = definitionId,
                ItemId = definition.ItemId,
                Quantity = 1,
                IsEquipment = true
            });
        }

        CombatWeaponSO ammunitionWeapon = ResolveDesiredAmmunitionWeapon(profile);
        ItemDefinitionId ammoItemId = ammunitionWeapon == null
            ? default
            : CombatAmmunitionPolicy.GetPreferred(
                ammunitionWeapon.CompatibleAmmunitionItemIds);
        int carriedAmmo = ammunitionWeapon == null || inventory == null
            ? 0
            : CombatAmmunitionPolicy.CountAvailable(
                ammunitionWeapon,
                inventory);
        int missingAmmo = Mathf.Max(0, profile.desiredAmmo - carriedAmmo);
        if (ammoItemId.IsValid && missingAmmo > 0)
        {
            result.Enqueue(new PreparationRequest
            {
                ItemId = ammoItemId.Value,
                Quantity = missingAmmo,
                IsEquipment = false
            });
        }

        return result;
    }

    private CombatWeaponSO ResolveDesiredAmmunitionWeapon(
        CharacterCombatLoadoutProfile profile)
    {
        foreach (string definitionId in profile.desiredWeaponDefinitionIds)
        {
            if (equipmentCatalog.TryGet(definitionId, out CombatEquipmentDefinitionSO definition)
                && definition is CombatWeaponSO weapon
                && weapon.CompatibleAmmunitionItemIds.Count > 0)
            {
                return weapon;
            }
        }

        return null;
    }

    private void TickActor(ActorPreparationState state)
    {
        CharacterActor actor = state?.Actor;
        if (actor == null || actor.IsDead || state.Inventory == null)
        {
            if (state != null)
            {
                state.Finished = true;
            }

            return;
        }

        if (state.Moving)
        {
            return;
        }

        if (state.Current == null)
        {
            if (state.Pending.Count == 0)
            {
                state.Finished = true;
                return;
            }

            state.Current = state.Pending.Dequeue();
            if (!itemRuntime.TryReserveStoredItemForDirectPickup(
                actor,
                state.Current.ItemId,
                state.Current.Quantity,
                out state.Reservation,
                out state.PickupStand,
                out _))
            {
                state.NextReservationAttemptAt = gameClock.Time + 0.5f;
                return;
            }
        }
        else if (!state.Reservation.IsValid)
        {
            if (gameClock.Time < state.NextReservationAttemptAt)
            {
                return;
            }

            if (!itemRuntime.TryReserveStoredItemForDirectPickup(
                    actor,
                    state.Current.ItemId,
                    state.Current.Quantity,
                    out state.Reservation,
                    out state.PickupStand,
                    out _))
            {
                state.NextReservationAttemptAt = gameClock.Time + 0.5f;
                return;
            }
        }

        if (actor.GetNowXY() != state.PickupStand)
        {
            if (!TryStartMove(state))
            {
                ReleaseReservationForRetry(state);
            }

            return;
        }

        if (!itemRuntime.TryPickupReservedStackQuantity(
            actor,
            state.Inventory,
            state.Reservation,
            out int pickedUp,
            out _)
            || pickedUp <= 0)
        {
            ReleaseReservationForRetry(state);
            return;
        }

        if (state.Current.IsEquipment)
        {
            EquipPickedItem(state);
        }

        state.Current.Quantity = Mathf.Max(0, state.Current.Quantity - pickedUp);
        if (state.Current.Quantity <= 0)
        {
            state.Current = null;
        }

        state.Reservation = default;
    }

    private bool TryStartMove(ActorPreparationState state)
    {
        Grid grid = gridProvider.Grid;
        AbilityMove movement = state.Actor != null
            ? state.Actor.GetComponent<AbilityMove>()
            : null;
        if (grid == null || movement == null)
        {
            return false;
        }

        Queue<GridMoveStep> path = pathSearchBroker.GetMovePathTo(
            grid,
            state.Actor.GetNowXY(),
            state.PickupStand,
            traversalContext: GridTraversalContext.ForCharacter(state.Actor));
        if (path == null || path.Count == 0)
        {
            return false;
        }

        state.Moving = true;
        state.Actor.StartCoroutine(MoveToPickup(state, movement, path));
        return true;
    }

    private static IEnumerator MoveToPickup(
        ActorPreparationState state,
        AbilityMove movement,
        Queue<GridMoveStep> path)
    {
        yield return movement.MoveByPath(path);
        if (state != null)
        {
            state.Moving = false;
        }
    }

    private void EquipPickedItem(ActorPreparationState state)
    {
        if (!equipmentRuntime.TryGetInstanceBySourceStack(
                state.Reservation.StackId,
                out CombatEquipmentInstance instance))
        {
            throw new InvalidOperationException(
                $"Picked equipment stack '{state.Reservation.StackId}' has no item-instance state.");
        }

        if (equipmentRuntime.TryAssignToCharacter(
            GetId(state.Actor),
            instance.instanceId,
            out _))
        {
            state.Inventory.TryConsumeSourceStack(
                state.Reservation.StackId,
                state.Current.ItemId);
        }
    }

    private void ReleaseCurrent(ActorPreparationState state)
    {
        if (state.Reservation.IsValid)
        {
            itemRuntime.ReleaseReservation(
                state.Reservation.StackId,
                GetId(state.Actor));
        }

        state.Current = null;
        state.Reservation = default;
        state.Moving = false;
    }

    private void ReleaseReservationForRetry(ActorPreparationState state)
    {
        if (state == null)
        {
            return;
        }

        if (state.Reservation.IsValid)
        {
            itemRuntime.ReleaseReservation(
                state.Reservation.StackId,
                GetId(state.Actor));
        }

        state.Reservation = default;
        state.Moving = false;
        state.NextReservationAttemptAt = gameClock.Time + 0.5f;
    }

    private void FinishActor(ActorPreparationState state)
    {
        if (state?.Actor == null)
        {
            return;
        }

        state.Actor.SetAiPaused(state.WasAiPaused);
        DefenseCombatPresentation.Ensure(state.Actor)?.SetStatus(
            state.CombatResupply ? "탄약 재보급 완료" : "전투 준비 완료",
            combatActive: state.CombatResupply);
    }

    private void CancelAll()
    {
        foreach (ActorPreparationState state in states.Values)
        {
            ReleaseCurrent(state);
            if (state?.Actor != null)
            {
                state.Actor.SetAiPaused(state.WasAiPaused);
                DefenseCombatPresentation.Ensure(state.Actor)?.SetStatus(
                    string.Empty,
                    combatActive: false);
            }
        }

        states.Clear();
        tickStates.Clear();
        completedActorIds.Clear();
    }

    private static bool IsEligibleGuard(CharacterActor actor)
    {
        if (actor == null
            || actor.IsDead
            || actor.IsOwner
            || actor.characterType != CharacterType.NPC)
        {
            return false;
        }

        AbilityWork work = actor.GetComponent<AbilityWork>();
        return work != null
            && work.CurrentDutyState == AbilityWork.DutyState.OnDuty
            && work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard);
    }

    private static string GetId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : string.Empty;
    }
}
