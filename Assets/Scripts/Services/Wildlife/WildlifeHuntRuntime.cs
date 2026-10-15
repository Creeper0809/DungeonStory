using System;
using DungeonStory.Foundation.Facts;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class WildlifeHuntRuntime
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IWildlifeEcosystemRuntime ecosystemRuntime;
    private readonly ICombatResolutionService combatResolution;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterPerformanceQuery performance;
    private readonly ICombatLineOfSightService lineOfSightService;
    private readonly ICombatCoverQuery coverQuery;
    private readonly ICombatCoverDurabilityRegistry coverDurability;
    private readonly ICombatAmmoResupplyRuntime ammoResupplyRuntime;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWildlifeCarcassService carcassService;
    private readonly IGameClock gameClock;
    private readonly IGameEventBus events;
    private readonly IWorldUiHierarchy worldUiHierarchy;
    private readonly List<WildlifeActor> wildlife;
    private readonly Action<string, string> cancelFoodRaid;
    private readonly Action<WildlifeActor> destroyActor;
    private readonly WildlifeGameplayOutcomeRuntime gameplayOutcomes;
    private readonly CharacterCombatTransactionFacade combatTransactions;

    public WildlifeHuntRuntime(
        WildlifeWorldServices world,
        WildlifeCombatServices combat,
        WildlifeExecutionServices execution,
        List<WildlifeActor> wildlife,
        Action<string, string> cancelFoodRaid,
        Action<WildlifeActor> destroyActor,
        WildlifeGameplayOutcomeRuntime gameplayOutcomes)
    {
        WildlifeWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        WildlifeCombatServices requiredCombat = combat
            ?? throw new ArgumentNullException(nameof(combat));
        gridSystemProvider = requiredWorld.Grid;
        gameDataProvider = requiredWorld.Session;
        events = requiredWorld.Events;
        ecosystemRuntime = requiredWorld.Ecosystem;
        itemStackRuntime = requiredWorld.Items;
        combatResolution = requiredCombat.Resolution;
        combatEquipmentRuntime = requiredCombat.Equipment;
        bodyHealthQuery = requiredCombat.BodyHealthQuery;
        performance = requiredCombat.Performance;
        lineOfSightService = requiredCombat.LineOfSight;
        coverQuery = requiredCombat.Cover;
        coverDurability = requiredCombat.CoverDurability;
        ammoResupplyRuntime = requiredCombat.AmmoResupply;
        carcassService = requiredCombat.Carcasses;
        gameClock = (execution ?? throw new ArgumentNullException(nameof(execution))).Clock;
        worldUiHierarchy = execution.WorldUiHierarchy;
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.cancelFoodRaid = cancelFoodRaid
            ?? throw new ArgumentNullException(nameof(cancelFoodRaid));
        this.destroyActor = destroyActor
            ?? throw new ArgumentNullException(nameof(destroyActor));
        this.gameplayOutcomes = gameplayOutcomes
            ?? throw new ArgumentNullException(nameof(gameplayOutcomes));
        combatTransactions = requiredCombat.CombatTransactions
            ?? throw new ArgumentNullException(
                nameof(requiredCombat.CombatTransactions));
    }

    public bool HasAvailableHuntJob(CharacterActor actor)
    {
        return TryFindBestHuntTarget(actor, out _);
    }

    public bool TryReserveBestHuntJob(
        CharacterActor actor,
        out WildlifeHuntJob job,
        out string reason)
    {
        job = default;
        reason = string.Empty;
        if (actor == null)
        {
            reason = "사냥할 직원이 없습니다.";
            return false;
        }

        if (!TryFindBestHuntTarget(actor, out WildlifeActor target))
        {
            reason = "지정된 사냥감이 없습니다.";
            return false;
        }

        if (!target.TryReserve(actor))
        {
            reason = "이미 다른 사냥꾼이 추적 중입니다.";
            return false;
        }

        job = new WildlifeHuntJob(target);
        return true;
    }

    public void ReleaseHuntReservation(string wildlifeId, CharacterActor actor)
    {
        if (TryGetWildlife(wildlifeId, out WildlifeActor target))
        {
            target.ReleaseReservation(actor);
        }
    }

    public bool DesignateHunt(string wildlifeId, bool designated, bool priority = false)
    {
        if (!TryGetWildlife(wildlifeId, out WildlifeActor target))
        {
            return false;
        }

        bool expectedPriority = designated && priority;
        WildlifeState expectedState = designated && target.State != WildlifeState.Dead
            ? WildlifeState.Hunted
            : !designated && target.State == WildlifeState.Hunted
                ? WildlifeState.Idle
                : target.State;
        if (target.HuntDesignated == designated
            && target.PriorityHunt == expectedPriority
            && target.State == expectedState)
        {
            return true;
        }

        long revision = gameplayOutcomes.NextOwnerRevision;
        var outcome = new WildlifeGameplayOutcomeSaveData
        {
            profile = WildlifeGameplayOutcomeProfile.Command,
            eventKind = "hunt-designation",
            wildlifeId = target.WildlifeId,
            wildlifeSpeciesId = target.SpeciesId,
            wildlifeDisplayName = target.DisplayName,
            x = target.GridPosition.x,
            y = target.GridPosition.y,
            stateBefore = (int)target.State,
            stateAfter = (int)expectedState,
            flagBefore = target.HuntDesignated,
            flagAfter = designated,
            priorityBefore = target.PriorityHunt,
            priorityAfter = expectedPriority,
            reasonCode = designated
                ? expectedPriority ? "priority-hunt-designated" : "hunt-designated"
                : "hunt-designation-cleared",
            absoluteDay = gameplayOutcomes.AbsoluteDay,
            ownerRevision = revision
        };
        outcome.operationId = gameplayOutcomes.CreateOperationId(
            outcome.profile,
            outcome.eventKind,
            outcome.wildlifeId,
            revision);
        if (!gameplayOutcomes.TryReserve(
                outcome,
                out DomainFactLease reserved,
                out _))
        {
            return false;
        }

        target.SetHuntDesignation(designated, priority);
        outcome.stateAfter = (int)target.State;
        outcome.flagAfter = target.HuntDesignated;
        outcome.priorityAfter = target.PriorityHunt;
        gameplayOutcomes.RecordMutation(outcome);
        gameplayOutcomes.CommitReserved(outcome, reserved, out _);
        return true;
    }

    public bool ApplyHuntHit(CharacterActor hunter, string wildlifeId, out string message)
    {
        return ApplyHuntHitWithCombatCore(hunter, wildlifeId, out message);
    }

    public bool CanAttackHuntTargetFrom(
        CharacterActor hunter,
        WildlifeActor target,
        Grid grid,
        Vector2Int attackerCell)
    {
        if (hunter == null || target == null || !target.IsAlive || grid == null)
        {
            return false;
        }

        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        CombatWeaponSnapshot weapon = CombatWeaponSnapshot.CreateUnarmed();
        if (equipment != null)
        {
            equipment.TryGetActiveWeapon(GetCharacterId(hunter), out weapon);
        }
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();

        int distance = Manhattan(attackerCell, target.GridPosition);
        if (!weapon.IsRanged)
        {
            return attackerCell.y == target.GridPosition.y
                && Mathf.Abs(attackerCell.x - target.GridPosition.x) == 1;
        }

        if (distance <= 0 || distance > weapon.MaximumRange)
        {
            return false;
        }

        CombatRangeBand band = CombatRangeRules.GetBand(distance);
        if (weapon.GetAccuracyMultiplier(band) <= 0f
            || weapon.GetDamageMultiplier(band) <= 0f)
        {
            return false;
        }

        CombatLineOfSightResult sight = lineOfSightService.Evaluate(
            grid,
            attackerCell,
            target.GridPosition,
            GetCharacterId(hunter),
            "wildlife:" + target.WildlifeId);
        return sight.HasLineOfSight && !sight.FriendlyFireRisk;
    }

    public bool NeedsHuntReload(CharacterActor hunter)
    {
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        return hunter != null
            && equipment != null
            && equipment.TryGetActiveWeapon(GetCharacterId(hunter), out CombatWeaponSnapshot weapon)
            && weapon != null
            && weapon.RequiresAmmo
            && weapon.LoadedAmmo <= 0;
    }

    public float GetHuntReloadDuration(CharacterActor hunter)
    {
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        if (hunter == null
            || equipment == null
            || !equipment.TryGetActiveWeapon(GetCharacterId(hunter), out CombatWeaponSnapshot weapon)
            || weapon == null)
        {
            return 0f;
        }

        CharacterBodyHealthSnapshot body =
            bodyHealthQuery?.GetSnapshot(hunter)
            ?? CreateHealthyBodySnapshot();
        return combatResolution.CalculateReloadTime(
            CreateHunterCombatStats(hunter, body),
            weapon);
    }

    public bool TryReloadHuntWeapon(CharacterActor hunter, out string message)
    {
        message = string.Empty;
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        if (hunter == null
            || equipment == null
            || !equipment.TryGetActiveWeapon(GetCharacterId(hunter), out CombatWeaponSnapshot weapon)
            || weapon == null
            || !weapon.RequiresAmmo)
        {
            return true;
        }

        if (weapon.LoadedAmmo > 0)
        {
            return true;
        }

        if (!equipment.TryReloadFromCharacterInventory(
                GetCharacterId(hunter),
                weapon.InstanceId,
                out int consumed)
            || consumed <= 0)
        {
            if (ammoResupplyRuntime?.TryRequestAmmoResupply(hunter, out string resupplyMessage)
                == true)
            {
                message = string.IsNullOrWhiteSpace(resupplyMessage)
                    ? "창고 탄약 재보급을 시작합니다."
                    : resupplyMessage;
                return false;
            }

            message = "사용 가능한 호환 탄약이 없습니다.";
            return false;
        }

        message = $"{consumed}발 장전";
        return true;
    }

    public float GetHuntAttackInterval(CharacterActor hunter)
    {
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        CombatWeaponSnapshot weapon = CombatWeaponSnapshot.CreateUnarmed();
        CharacterCombatLoadoutProfile profile = null;
        if (hunter != null && equipment != null)
        {
            string hunterId = GetCharacterId(hunter);
            equipment.TryGetActiveWeapon(hunterId, out weapon);
            profile = equipment.GetActiveProfileSnapshot(hunterId);
        }
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();

        CharacterBodyHealthSnapshot body =
            bodyHealthQuery?.GetSnapshot(hunter)
            ?? CreateHealthyBodySnapshot();
        return combatResolution.CalculateAttackInterval(
            CreateHunterCombatStats(hunter, body),
            weapon,
            ResolveSupportedFireMode(weapon, profile?.fireMode ?? CombatFireMode.Aimed));
    }

    private bool ApplyHuntHitWithCombatCore(
        CharacterActor hunter,
        string wildlifeId,
        out string message)
    {
        message = string.Empty;
        if (hunter == null
            || !TryGetWildlife(wildlifeId, out WildlifeActor target)
            || !target.IsAlive)
        {
            message = "사냥 대상이 사라졌습니다.";
            return false;
        }

        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            message = "전투 격자를 찾지 못했습니다.";
            return false;
        }

        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        ICharacterBodyHealthQuery healthQuery = bodyHealthQuery;
        string hunterId = GetCharacterId(hunter);
        CombatWeaponSnapshot weapon = CombatWeaponSnapshot.CreateUnarmed();
        if (equipment != null)
        {
            equipment.TryGetActiveWeapon(hunterId, out weapon);
        }
        weapon ??= CombatWeaponSnapshot.CreateUnarmed();

        CharacterCombatLoadoutProfile profile = equipment?.GetActiveProfileSnapshot(hunterId);
        if (weapon.IsRanged && profile?.holdFire == true)
        {
            message = "사격 중지 상태입니다.";
            return false;
        }

        int distance = Manhattan(hunter.GetNowXY(), target.GridPosition);
        if (!weapon.IsRanged
            && (hunter.GetNowXY().y != target.GridPosition.y
                || Mathf.Abs(hunter.GetNowXY().x - target.GridPosition.x) != 1))
        {
            message = "근접 공격은 같은 층의 바로 옆 칸에서만 가능합니다.";
            return false;
        }

        CombatLineOfSightResult sight = weapon.IsRanged
            ? lineOfSightService.Evaluate(
                grid,
                hunter.GetNowXY(),
                target.GridPosition,
                hunterId,
                "wildlife:" + target.WildlifeId)
            : new CombatLineOfSightResult(
                true,
                false,
                default,
                Array.Empty<Vector2Int>(),
                string.Empty);
        CombatFireMode fireMode = ResolveSupportedFireMode(
            weapon,
            profile?.fireMode ?? CombatFireMode.Aimed);
        CharacterBodyHealthSnapshot hunterBody = healthQuery?.GetSnapshot(hunter)
            ?? CreateHealthyBodySnapshot();
        CombatAttackResult result = combatResolution.Resolve(new CombatAttackRequest(
            $"hunt:{hunterId}:{target.WildlifeId}:{gameClock.FrameCount}",
            hunterId,
            "wildlife:" + target.WildlifeId,
            CreateHunterCombatStats(hunter, hunterBody),
            CreateWildlifeCombatStats(target),
            weapon,
            distance,
            fireMode,
            weapon.IsRanged
                ? coverQuery.GetCover(grid, hunter.GetNowXY(), target.GridPosition)
                : default,
            hasLineOfSight: sight.HasLineOfSight,
            friendlyFireRisk: sight.FriendlyFireRisk,
            defenderMeleeLocked: distance <= 1,
            attackerSuppression: hunterBody.Suppression,
            attackPowerMultiplier: hunter.GetCombatPowerMultiplier()));
        if (!result.Executed)
        {
            message = ResolveHuntFailureMessage(weapon, distance, sight);
            return false;
        }

        bool predictedKill = target.WouldCombatDamageKill(result);
        if (predictedKill
            && target.Species != null
            && !itemStackRuntime.CatalogProvider.TryGetDefinition(
                target.Species.CarcassItemId,
                out _))
        {
            message = "사체 아이템 정의가 없어 사냥 결과를 확정할 수 없습니다.";
            return false;
        }

        int healthBefore = target.CurrentHealth;
        WildlifeState stateBefore = target.State;
        int ammunitionBefore = weapon.RequiresAmmo ? weapon.LoadedAmmo : 0;
        long revision = gameplayOutcomes.NextOwnerRevision;
        var outcome = new WildlifeGameplayOutcomeSaveData
        {
            profile = WildlifeGameplayOutcomeProfile.Action,
            eventKind = "hunt-attack",
            wildlifeId = target.WildlifeId,
            wildlifeSpeciesId = target.SpeciesId,
            wildlifeDisplayName = target.DisplayName,
            actorId = hunterId,
            actorDisplayName = GetCharacterDisplayName(hunter),
            x = target.GridPosition.x,
            y = target.GridPosition.y,
            stateBefore = (int)stateBefore,
            stateAfter = (int)stateBefore,
            healthBefore = healthBefore,
            healthAfter = healthBefore,
            weaponDefinitionId = OptionalStable(weapon.DefinitionId),
            weaponInstanceId = OptionalStable(weapon.InstanceId),
            ammunitionItemId = OptionalStable(
                string.IsNullOrWhiteSpace(result.AmmunitionItemId)
                    ? weapon.AmmunitionItemId
                    : result.AmmunitionItemId),
            ammunitionBefore = ammunitionBefore,
            ammunitionAfter = ammunitionBefore,
            bodyPart = result.BodyPart.ToString(),
            coverSourceId = OptionalStable(result.CoverSourceId),
            coverDamage = Mathf.Max(0, Mathf.RoundToInt(result.CoverDamage)),
            absoluteDay = gameplayOutcomes.AbsoluteDay,
            ownerRevision = revision,
            reasonCode = ResolveOutcomeReason(result, killed: false)
        };
        outcome.operationId = gameplayOutcomes.CreateOperationId(
            outcome.profile,
            outcome.eventKind,
            outcome.wildlifeId,
            revision);
        if (!gameplayOutcomes.TryReserve(
                outcome,
                out DomainFactLease reserved,
                out string reserveFailure))
        {
            message = "사냥 결과 관찰을 예약하지 못했습니다: " + reserveFailure;
            return false;
        }

        string preparedCarcassStackId = string.Empty;
        long preparedCarcassMassGrams = 0L;
        if (predictedKill)
        {
            if (carcassService?.TrySpawnCarcass(
                    target,
                    out preparedCarcassStackId) != true)
            {
                gameplayOutcomes.Cancel(reserved);
                message = "사체를 물리 아이템으로 확보하지 못해 치명타를 확정하지 않았습니다.";
                return false;
            }
            preparedCarcassMassGrams = itemStackRuntime.MassQuery
                .GetDefinitionUnitMass(
                    (ItemDefinitionId)target.Species.CarcassItemId)
                .Value;
        }

        PresentHuntAttack(hunter, target, weapon);
        ConsumeHuntWeapon(equipment, weapon, result, target.GridPosition);
        if (result.CoverBlocked)
        {
            coverDurability.TryApplyDamage(result.CoverSourceId, result.CoverDamage);
        }

        target.RegisterThreat(hunter.GetNowXY(), result.Hit ? 0.75f : 0.35f);
        target.SetHuntDesignation(true, target.PriorityHunt);
        int applied = result.Hit ? target.ApplyCombatDamage(result, hunter) : 0;
        bool killed = !target.IsAlive;
        if (killed != predictedKill)
        {
            throw new InvalidOperationException(
                $"Wildlife lethal preflight diverged for '{target.WildlifeId}': "
                + $"predicted={predictedKill};actual={killed}.");
        }
        bool destroyKilledTarget = false;
        events.Publish(new WildlifeHuntAttackResolvedEvent(
            CharacterPersistentIdentity.Require(hunter), GetCharacterDisplayName(hunter),
            target.WildlifeId, target.DisplayName, result.BodyPart, applied, result.Hit, result.CoverBlocked, killed));

        if (target.RetaliationDamage > 0
            && !killed
            && target.Aggression > 0.45f
            && distance <= 1)
        {
            ApplyWildlifeRetaliation(
                outcome.operationId,
                revision,
                target,
                hunter,
                equipment,
                healthQuery);
        }

        if (killed)
        {
            cancelFoodRaid(
                target.WildlifeId,
                "습격 늑대가 처치되어 도난이 취소되었습니다.");
            ecosystemRuntime?.NotifyWildlifeKilled(target, byHunt: true);
            hunter.Progression?.AddExperience(target.IsDangerous ? 20 : 10);
            PublishHuntCompleted(hunter, target);
            outcome.carcassStackId = preparedCarcassStackId;
            outcome.outputQuantity = 1;
            outcome.outputMassGrams = preparedCarcassMassGrams;
            wildlife.Remove(target);
            destroyKilledTarget = target != null;
        }

        outcome.stateAfter = (int)target.State;
        outcome.healthAfter = target.CurrentHealth;
        outcome.damage = healthBefore - target.CurrentHealth;
        outcome.hit = result.Hit;
        outcome.killed = killed;
        outcome.reasonCode = ResolveOutcomeReason(result, killed);
        outcome.ammunitionAfter = ResolveAmmunitionAfter(
            equipment,
            hunterId,
            weapon,
            ammunitionBefore);
        outcome.ammunitionConsumed = ammunitionBefore - outcome.ammunitionAfter;
        gameplayOutcomes.RecordMutation(outcome);
        DomainFactDelivery commit = gameplayOutcomes.CommitReserved(
            outcome,
            reserved,
            out string commitFailure);
        if (destroyKilledTarget)
            destroyActor(target);

        message = killed
            ? "사냥감 처치"
            : result.Hit
                ? $"{GetBodyPartName(result.BodyPart)} 명중"
                : result.CoverBlocked
                    ? "엄폐물에 막힘"
                    : result.Evaded
                        ? "사냥감이 회피"
                        : "빗나감";
        if (!commit.Completed && !string.IsNullOrWhiteSpace(commitFailure))
            message += " · 관찰 전달 대기(" + commitFailure + ")";
        return true;
    }

    private static int ResolveAmmunitionAfter(
        ICombatEquipmentRuntime equipment,
        string hunterId,
        CombatWeaponSnapshot before,
        int ammunitionBefore)
    {
        if (before?.RequiresAmmo != true)
            return 0;
        if (equipment != null
            && equipment.TryGetActiveWeapon(hunterId, out CombatWeaponSnapshot after)
            && after != null
            && string.Equals(after.InstanceId, before.InstanceId, StringComparison.Ordinal))
        {
            return Mathf.Clamp(after.LoadedAmmo, 0, ammunitionBefore);
        }
        return ammunitionBefore;
    }

    private static string ResolveOutcomeReason(
        CombatAttackResult result,
        bool killed)
    {
        if (killed)
            return "hunt-killed";
        if (result.Hit)
            return "hunt-hit";
        if (result.CoverBlocked)
            return "hunt-cover-blocked";
        if (result.Evaded)
            return "hunt-evaded";
        return "hunt-missed";
    }

    private static string OptionalStable(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private void ApplyWildlifeRetaliation(
        string huntOperationId,
        long huntRevision,
        WildlifeActor wildlifeActor,
        CharacterActor hunter,
        ICombatEquipmentRuntime equipment,
        ICharacterBodyHealthQuery healthQuery)
    {
        if (wildlifeActor == null || hunter == null || hunter.IsDead)
        {
            return;
        }

        string hunterId = GetCharacterId(hunter);
        CharacterBodyHealthSnapshot hunterBody = healthQuery?.GetSnapshot(hunter)
            ?? CreateHealthyBodySnapshot();
        CombatWeaponSnapshot naturalWeapon = CreateWildlifeNaturalWeapon(wildlifeActor);
        var request = new CombatAttackRequest(
            huntOperationId,
            wildlifeActor.WildlifeId,
            hunterId,
            CreateWildlifeCombatStats(wildlifeActor),
            CreateHunterCombatStats(hunter, hunterBody),
            naturalWeapon,
            1,
            CombatFireMode.Aimed,
            default,
            defenderDowned: hunterBody.Downed,
            defenderMeleeLocked: true,
            defenderSuppression: hunterBody.Suppression,
            defenderArmor: equipment?.GetArmor(hunterId),
            defenderShield: equipment?.GetShield(hunterId) ?? default);
        CharacterCombatTransactionResult transaction =
            combatTransactions.ResolveAndApply(
                request,
                new CombatParticipantRef(wildlifeActor),
                hunter,
                naturalWeapon.Verb?.damageType ?? CombatDamageType.Pierce,
                $"{wildlifeActor.DisplayName}의 반격",
                CharacterCommandOrigin.Autonomous,
                huntRevision);
        if (!transaction.Succeeded)
        {
            Debug.LogWarning(
                "Wildlife retaliation transaction failed: "
                + transaction.FailureReason);
            return;
        }
        CombatAttackResult retaliation = transaction.AttackResult;

        DefenseCombatPresentation.Ensure(hunter)?.PlayHit(
            retaliation.AppliedDamage,
            CombatDamageType.Slash,
            worldUiHierarchy);
        if (retaliation.Hit)
        {
            hunter.ApplyMoodFactor(
                "survival:hunt:retaliation",
                $"{wildlifeActor.DisplayName}에게 반격당함",
                -4f,
                180f,
                1);
        }
    }

    private static CombatWeaponSnapshot CreateWildlifeNaturalWeapon(WildlifeActor actor)
    {
        float baseDamage = Mathf.Max(2f, actor?.RetaliationDamage ?? 2);
        return new CombatWeaponSnapshot(
            "combat:wildlife-natural",
            string.Empty,
            CombatEquipmentKind.MeleeWeapon,
            new MeleeStrikeVerb
            {
                attackTime = 1.05f,
                baseDamage = baseDamage,
                penetration = Mathf.Max(0f, baseDamage * 0.2f),
                damageType = CombatDamageType.Pierce,
                tracking = 0.08f
            },
            new[]
            {
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Contact,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                }
            },
            1,
            CombatEquipmentQuality.Normal,
            string.Empty,
            0,
            0,
            0f,
            false,
            false,
            false);
    }

    private CombatStatSnapshot CreateHunterCombatStats(
        CharacterActor hunter,
        CharacterBodyHealthSnapshot body)
    {
        return CombatRuntimeStatFactory.Create(hunter, body, performance);
    }

    private static CombatStatSnapshot CreateWildlifeCombatStats(WildlifeActor actor)
    {
        if (actor == null)
        {
            return default;
        }

        float speed = Mathf.Max(0.5f, actor.Species?.MoveSpeed ?? 1f);
        float mobility = actor.CombatMobility;
        float health = Mathf.Clamp01(actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        return new CombatStatSnapshot(
            melee: Mathf.Clamp(3f + actor.RetaliationDamage * 0.45f, 2f, 14f),
            shooting: 0f,
            evasion: Mathf.Clamp(2f + speed * 3f, 2f, 14f) * mobility,
            moveSpeed: Mathf.Clamp(3f + speed * 3f, 3f, 14f) * mobility,
            strength: Mathf.Clamp(2f + actor.RetaliationDamage * 0.5f, 2f, 15f),
            toughness: Mathf.Clamp(actor.MaxHealth * 0.12f, 1f, 16f),
            dexterity: Mathf.Clamp(2f + speed * 2.5f, 2f, 14f) * mobility,
            healthMultiplier: health);
    }

    private static CharacterBodyHealthSnapshot CreateHealthyBodySnapshot()
    {
        return new CharacterBodyHealthSnapshot(
            Array.Empty<CharacterBodyPartHealthState>(),
            0f,
            0f,
            1f,
            1f,
            1f,
            false);
    }

    private static CombatFireMode ResolveSupportedFireMode(
        CombatWeaponSnapshot weapon,
        CombatFireMode requested)
    {
        if (weapon == null)
        {
            return CombatFireMode.Aimed;
        }

        return requested switch
        {
            CombatFireMode.Rapid when weapon.SupportsRapid => CombatFireMode.Rapid,
            CombatFireMode.Suppressive when weapon.SupportsSuppressive => CombatFireMode.Suppressive,
            _ => CombatFireMode.Aimed
        };
    }

    private static string ResolveHuntFailureMessage(
        CombatWeaponSnapshot weapon,
        int distance,
        CombatLineOfSightResult sight)
    {
        if (weapon == null)
        {
            return "사용할 무기가 없습니다.";
        }

        if (distance > weapon.MaximumRange || (!weapon.IsRanged && distance > 1))
        {
            return "무기 사거리 밖입니다.";
        }

        if (weapon.IsRanged && !sight.HasLineOfSight)
        {
            return "사선이 막혔습니다.";
        }

        if (weapon.IsRanged && sight.FriendlyFireRisk)
        {
            return "아군이 사선에 있어 사격을 보류합니다.";
        }

        if (weapon.RequiresAmmo && weapon.LoadedAmmo <= 0)
        {
            return "장전된 탄약이 없습니다.";
        }

        return "공격할 수 없습니다.";
    }

    private void PresentHuntAttack(
        CharacterActor hunter,
        WildlifeActor target,
        CombatWeaponSnapshot weapon)
    {
        if (hunter == null || target == null)
        {
            return;
        }

        DefenseCombatPresentation.Ensure(hunter)?.PlayAttack(target.transform.position);
        if (!weapon.IsRanged)
        {
            return;
        }

        float projectileSpeed = weapon.Verb switch
        {
            ProjectileVerb projectile => projectile.projectileSpeed,
            RecoverableThrowVerb recoverable => recoverable.projectileSpeed,
            _ => 12f
        };
        CombatProjectilePresentation.Launch(
            hunter.transform.position,
            target.transform.position,
            projectileSpeed,
            weapon.Verb?.damageType ?? CombatDamageType.Pierce,
            arcing: false,
            gameClock: gameClock,
            worldUiHierarchy: worldUiHierarchy);
    }

    private void ConsumeHuntWeapon(
        ICombatEquipmentRuntime equipment,
        CombatWeaponSnapshot weapon,
        CombatAttackResult result,
        Vector2Int impactPosition)
    {
        if (equipment == null || weapon == null)
        {
            return;
        }

        if (weapon.RequiresAmmo && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            equipment.TryConsumeLoadedAmmo(
                weapon.InstanceId,
                Mathf.Max(1, result.AmmunitionConsumed));
            return;
        }

        if (weapon.Verb?.DropsWeaponOnUse != true
            || string.IsNullOrWhiteSpace(weapon.InstanceId)
            || !equipment.TryDropExistingEquipmentToWorld(
                weapon.InstanceId,
                impactPosition,
                out _,
                out _))
        {
            return;
        }
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : string.Empty;
    }

    private static string GetCharacterDisplayName(CharacterActor actor)
    {
        string displayName = actor?.Identity?.DisplayName;
        return !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : actor != null ? actor.name : "사냥꾼";
    }

    private static string GetBodyPartName(CombatBodyPart bodyPart)
    {
        return bodyPart switch
        {
            CombatBodyPart.Head => "머리",
            CombatBodyPart.Torso => "몸통",
            CombatBodyPart.LeftArm => "왼앞다리",
            CombatBodyPart.RightArm => "오른앞다리",
            CombatBodyPart.LeftLeg => "왼뒷다리",
            CombatBodyPart.RightLeg => "오른뒷다리",
            _ => "몸"
        };
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
    private bool TryFindBestHuntTarget(CharacterActor hunter, out WildlifeActor target)
    {
        target = null;
        if (hunter == null || !gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int start = hunter.GetNowXY();

        string hunterId = CharacterPersistentIdentity.Require(hunter).Value;
        int bestPriority = -1;
        int bestDistance = int.MaxValue;
        bool bestDangerous = false;
        foreach (WildlifeActor candidate in wildlife)
        {
            Vector2Int candidatePosition = candidate != null
                ? grid.GetXY(candidate.transform.position)
                : default;
            if (candidate == null
                || !candidate.IsAlive
                || !candidate.HuntDesignated
                || (!string.IsNullOrWhiteSpace(candidate.ReservedByPersistentId)
                    && candidate.ReservedByPersistentId != hunterId))
            {
                continue;
            }

            int priority = candidate.PriorityHunt ? 1 : 0;
            int distance = Manhattan(start, candidatePosition);
            bool dangerous = candidate.IsDangerous;
            if (target == null
                || priority > bestPriority
                || (priority == bestPriority && distance < bestDistance)
                || (priority == bestPriority && distance == bestDistance && dangerous && !bestDangerous))
            {
                target = candidate;
                bestPriority = priority;
                bestDistance = distance;
                bestDangerous = dangerous;
            }
        }

        return target != null;
    }

    private bool TryGetWildlife(string wildlifeId, out WildlifeActor target)
    {
        string normalized = wildlifeId?.Trim() ?? string.Empty;
        target = wildlife.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.WildlifeId, normalized, StringComparison.Ordinal));
        return target != null;
    }
    private void PublishHuntCompleted(CharacterActor hunter, WildlifeActor target)
    {
        int day = 0;
        if (gameDataProvider != null && gameDataProvider.TryGetSessionState(out GameSessionState data))
        {
            day = data.day != null ? data.day.Value : 0;
        }

        events.Publish(new WildlifeHuntCompletedEvent(CharacterPersistentIdentity.Require(hunter),
            target.SpeciesId, target.IsDangerous, target.MaxHealth, day));
    }
}
