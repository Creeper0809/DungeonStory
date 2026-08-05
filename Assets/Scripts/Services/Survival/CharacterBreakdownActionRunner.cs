using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CharacterBreakdownActionPolicyDependencies
{
    internal CharacterBreakdownActionPolicyDependencies(
        IRandomStream breakdownRandom,
        ICharacterNeedBalanceRuntime needBalanceRuntime,
        IItemDefinitionCatalog itemCatalog,
        ICharacterBodyHealthCommand bodyHealthCommands)
    {
        BreakdownRandom = breakdownRandom
            ?? throw new ArgumentNullException(nameof(breakdownRandom));
        NeedBalanceRuntime = needBalanceRuntime
            ?? throw new ArgumentNullException(nameof(needBalanceRuntime));
        ItemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        BodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
    }

    internal IRandomStream BreakdownRandom { get; }
    internal ICharacterNeedBalanceRuntime NeedBalanceRuntime { get; }
    internal IItemDefinitionCatalog ItemCatalog { get; }
    internal ICharacterBodyHealthCommand BodyHealthCommands { get; }
}

internal sealed class CharacterBreakdownActionExecutionDependencies
{
    internal CharacterBreakdownActionExecutionDependencies(
        CharacterDeprivationStateStore stateStore,
        CharacterSafeDrinkPlanner safeDrinkPlanner,
        CharacterEmergencyMovement emergencyMovement,
        CharacterDeprivationDiagnostics diagnostics,
        CharacterDeprivationConsequences consequences)
    {
        StateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        SafeDrinkPlanner = safeDrinkPlanner
            ?? throw new ArgumentNullException(nameof(safeDrinkPlanner));
        EmergencyMovement = emergencyMovement
            ?? throw new ArgumentNullException(nameof(emergencyMovement));
        Diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        Consequences = consequences
            ?? throw new ArgumentNullException(nameof(consequences));
    }

    internal CharacterDeprivationStateStore StateStore { get; }
    internal CharacterSafeDrinkPlanner SafeDrinkPlanner { get; }
    internal CharacterEmergencyMovement EmergencyMovement { get; }
    internal CharacterDeprivationDiagnostics Diagnostics { get; }
    internal CharacterDeprivationConsequences Consequences { get; }
}

internal sealed class CharacterBreakdownActionRunner
{
    private const int AccidentSearchRadius = 32;
    private static readonly WaitForSeconds CannibalAttackDelay =
        new WaitForSeconds(0.75f);
    private static readonly WaitForSeconds CorpseSpawnDelay =
        new WaitForSeconds(0.1f);
    private static readonly WaitForSeconds CollapseDelay =
        new WaitForSeconds(5f);
    private static readonly WaitForSeconds ViolentActionDelay =
        new WaitForSeconds(0.8f);
    private static readonly WaitForSeconds BreakdownIdleDelay =
        new WaitForSeconds(1.5f);

    private readonly CharacterBreakdownWorld world;
    private readonly IRandomStream breakdownRandom;
    private readonly ICharacterNeedBalanceRuntime needBalanceRuntime;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly CharacterDeprivationStateStore stateStore;
    private readonly CharacterSafeDrinkPlanner safeDrinkPlanner;
    private readonly CharacterEmergencyMovement emergencyMovement;
    private readonly CharacterDeprivationDiagnostics diagnostics;
    private readonly CharacterDeprivationConsequences consequences;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly HashSet<CharacterId> runningActorIds =
        new HashSet<CharacterId>();
    private readonly Dictionary<CharacterBreakdownKind, Func<CharacterActor, IEnumerator>>
        actionRoutines =
            new Dictionary<CharacterBreakdownKind, Func<CharacterActor, IEnumerator>>();

    public CharacterBreakdownActionRunner(
        CharacterBreakdownWorld world,
        CharacterBreakdownActionPolicyDependencies policy,
        CharacterBreakdownActionExecutionDependencies execution)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        _ = policy ?? throw new ArgumentNullException(nameof(policy));
        _ = execution ?? throw new ArgumentNullException(nameof(execution));
        breakdownRandom = policy.BreakdownRandom;
        needBalanceRuntime = policy.NeedBalanceRuntime;
        itemCatalog = policy.ItemCatalog;
        bodyHealthCommands = policy.BodyHealthCommands;
        stateStore = execution.StateStore;
        safeDrinkPlanner = execution.SafeDrinkPlanner;
        emergencyMovement = execution.EmergencyMovement;
        diagnostics = execution.Diagnostics;
        consequences = execution.Consequences;
        CreateActionRoutines();
    }

    public bool TryRunActive(CharacterActor actor, out string status)
    {
        status = string.Empty;
        if (!stateStore.TryGetWritable(actor, out CharacterDeprivationState state)
            || state.breakdown == null
            || !state.breakdown.active)
        {
            return false;
        }

        if (runningActorIds.Contains((CharacterId)state.characterId))
        {
            status = GetBreakdownLabel(state.breakdown.kind) + " 진행 중";
            return true;
        }

        if (!actionRoutines.ContainsKey(state.breakdown.kind))
        {
            state.breakdown.kind = ResolveBreakdownKind(state.breakdown.cause);
            status = "붕괴 행동을 다시 고르는 중";
            return true;
        }

        status = GetBreakdownLabel(state.breakdown.kind);
        actor.Brain?.BeginExternallyDrivenAction(
            "결핍 붕괴",
            status,
            "붕괴 행동이 끝날 때까지 유지");
        Begin(actor, state.breakdown.kind);
        return true;
    }

    public void Begin(CharacterActor actor, CharacterBreakdownKind kind)
    {
        if (!stateStore.TryGet(actor, out CharacterDeprivationState state)
            || state.breakdown == null
            || !state.breakdown.active
            || state.breakdown.kind != kind)
        {
            return;
        }

        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        if (runningActorIds.Add(actorId))
        {
            actor.Brain?.StopCurrentActionForReplan("결핍 붕괴");
            actor.StartCoroutine(RunBreakdownAction(
                actor,
                actorId,
                kind,
                state.breakdownGeneration));
        }
    }

    public void ReleaseActor(CharacterId actorId)
    {
        if (actorId.IsValid)
        {
            runningActorIds.Remove(actorId);
        }
    }

    public void Reset()
    {
        runningActorIds.Clear();
    }

    private IEnumerator RunBreakdownAction(
        CharacterActor actor,
        CharacterId actorId,
        CharacterBreakdownKind kind,
        int generation)
    {
        try
        {
            if (actionRoutines.TryGetValue(kind, out Func<CharacterActor, IEnumerator> routine))
            {
                yield return routine(actor);
            }
        }
        finally
        {
            runningActorIds.Remove(actorId);
            actor?.Brain?.EndExternallyDrivenAction(clearFailures: true);
            if (actor != null
                && !actor.IsDead
                && stateStore.TryGet(actor, out CharacterDeprivationState current)
                && current.breakdown != null
                && current.breakdown.active
                && current.breakdownGeneration != generation)
            {
                Begin(actor, current.breakdown.kind);
            }
        }
    }


    private IEnumerator RunDesperateRelief(CharacterActor actor)
    {
        if (!TryChooseAccidentPosition(actor, out Vector2Int target))
        {
            yield break;
        }

        yield return emergencyMovement.MoveNear(actor, target, 0);
        if (actor == null || actor.IsDead)
        {
            yield break;
        }

        Vector2Int position = actor.GetNowXY();
        string id = GetPersistentId(actor);
        world.AddFilth(WorldFilthType.Waste, position, 22f, id, 0.8f);
        world.AddFilth(WorldFilthType.Stain, position, 8f, id, 0.55f, wallStain: true);
        RecoverNeed(
            actor,
            CharacterCondition.EXCRETION,
            90f,
            CharacterNeedRecoverySource.Emergency);
        actor.ChangesStat(CharacterCondition.HYGIENE, -25f);
        actor.ApplyMoodFactor("survival:public-accident", "아무 데서나 사고를 냄", -10f, 360f, 1);
        consequences.ApplyWitnessMood(actor, position, "끔찍한 사고를 목격함", -4f);
        consequences.RecordTaboo(actor, "통제력을 잃고 던전을 오염시켰다");
    }


    private IEnumerator RunDesperateDrink(CharacterActor actor, bool allowWaste, bool safeOnly = false)
    {
        diagnostics.DesperateDrinkAttempts++;
        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        if (world.TryGetGrid(out Grid grid)
            && safeDrinkPlanner.TryFindReservableWaterStack(
                grid,
                actor,
                actorId.Value,
                out WorldItemStockCandidate waterStack,
                out Vector2Int approach,
                out Queue<GridMoveStep> path,
                out _,
                countSafeReliefPlan: false))
        {
            try
            {
                yield return emergencyMovement.MoveNear(actor, approach, 0, path);
                if (actor != null
                    && !actor.IsDead
                    && actor.GetNowXY() == approach
                    && Manhattan(actor.GetNowXY(), waterStack.Position) <= 1)
                {
                    diagnostics.DesperateDrinkStackArrivals++;
                    if (world.TryConsumeStack(
                            waterStack.StackId,
                            1,
                            out _))
                    {
                        diagnostics.DesperateDrinkStackConsumptions++;
                        RecoverNeed(
                            actor,
                            CharacterCondition.THIRST,
                            65f,
                            CharacterNeedRecoverySource.Emergency);
                        actor.ApplyMoodFactor(
                            "survival:clean-water",
                            "물을 마심",
                            2f,
                            90f,
                            1);
                        consequences.EndActiveBreakdownIfRelieved(actor);
                        yield break;
                    }
                }
                else
                {
                    diagnostics.DesperateDrinkStackMoveFailures++;
                }
            }
            finally
            {
                safeDrinkPlanner.Release(actorId.Value, approach);
            }
        }

        Vector2Int facilityApproach = default;
        if (world.TryGetGrid(out grid)
            && safeDrinkPlanner.TryFindReservableWaterFacility(
                grid,
                actor,
                actorId.Value,
                out BuildableObject waterFacility,
                out facilityApproach,
                out Queue<GridMoveStep> facilityPath,
                out _))
        {
            yield return emergencyMovement.MoveNear(
                actor,
                facilityApproach,
                0,
                facilityPath);
            if (actor != null
                && !actor.IsDead
                && waterFacility != null
                && !waterFacility.IsGridDestroyed
                && actor.GetNowXY() == facilityApproach
                && Manhattan(actor.GetNowXY(), waterFacility.centerPos) <= 1)
            {
                RecoverNeed(
                    actor,
                    CharacterCondition.THIRST,
                    65f,
                    CharacterNeedRecoverySource.Emergency);
                consequences.EndActiveBreakdownIfRelieved(actor);
                safeDrinkPlanner.Release(actorId.Value, facilityApproach);
                actor.ApplyMoodFactor("survival:well-water", "수원에서 물을 마심", 1f, 90f, 1);
                yield break;
            }
        }

        if (safeDrinkPlanner.HasReservation(actorId.Value))
        {
            safeDrinkPlanner.Release(actorId.Value, facilityApproach);
        }

        if (world.TryFindDrinkSource(actor.GetNowXY(), allowFoul: !safeOnly, out WorldWaterSourceSnapshot source)
            && (!safeOnly || source.Quality == WorldWaterQuality.Clean))
        {
            int standDistance = source.TerrainType == GridCellTerrainType.DeepWater ? 1 : 0;
            yield return emergencyMovement.MoveNear(actor, source.Position, standDistance);
            if (actor != null
                && !actor.IsDead
                && Manhattan(actor.GetNowXY(), source.Position) <= standDistance
                && world.TryDrink(
                    source.SourceId,
                    ApplyPersonalWaterConsumption(1f),
                    out WorldWaterQuality quality,
                    out float consumed)
                && consumed > 0f)
            {
                RecoverNeed(
                    actor,
                    CharacterCondition.THIRST,
                    GetWaterRecovery(quality),
                    CharacterNeedRecoverySource.Emergency);
                consequences.EndActiveBreakdownIfRelieved(actor);
                if (quality != WorldWaterQuality.Clean)
                {
                    bodyHealthCommands.ApplyLegacyDamage(
                        actor,
                        quality == WorldWaterQuality.Foul ? 5f : 2f,
                        "오염된 물",
                        allowDeath: true);
                    actor.ChangesStat(CharacterCondition.HYGIENE, -12f);
                    consequences.AddInfection(actor, quality == WorldWaterQuality.Foul ? 22f : 10f);
                    actor.ApplyMoodFactor("survival:foul-water", "썩은 물을 삼킴", -7f, 240f, 1);
                }
                yield break;
            }
        }

        if (!allowWaste || GetNeed(actor, CharacterCondition.EXCRETION) > 25f)
        {
            yield break;
        }

        Vector2Int position = actor.GetNowXY();
        string id = GetPersistentId(actor);
        world.AddFilth(WorldFilthType.Waste, position, 12f, id, 0.95f);
        RecoverNeed(
            actor,
            CharacterCondition.EXCRETION,
            70f,
            CharacterNeedRecoverySource.Emergency);
        RecoverNeed(
            actor,
            CharacterCondition.THIRST,
            25f,
            CharacterNeedRecoverySource.Emergency);
        consequences.EndActiveBreakdownIfRelieved(actor);
        actor.ChangesStat(CharacterCondition.HYGIENE, -35f);
        bodyHealthCommands.ApplyLegacyDamage(
            actor,
            7f,
            "체액 오염 섭취",
            allowDeath: true);
        consequences.AddInfection(actor, 35f);
        actor.ApplyMoodFactor("survival:taboo-drink", "마셔서는 안 될 것을 마심", -14f, 600f, 1);
        consequences.RecordTaboo(actor, "갈증 끝에 자신의 오염물을 마셨다");
    }


    private IEnumerator RunDesperateEat(CharacterActor actor)
    {
        if (TryFindEmergencyFood(actor, out WorldItemStackSnapshot food))
        {
            yield return emergencyMovement.MoveNear(actor, food.Position, 0);
            if (actor != null
                && !actor.IsDead
                && Manhattan(actor.GetNowXY(), food.Position) == 0
                && world.TryConsumeStack(food.StackId, 1, out WorldItemStackSnapshot consumed))
            {
                bool humanoid = consumed.ItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId
                    || consumed.ItemId == DarkSurvivalItemDefinitions.HumanoidMeatItemId;
                actor.Stats?.RecoverNeed(
                    CharacterCondition.HUNGER,
                    humanoid ? 75f : 55f,
                    CharacterNeedRecoverySource.Emergency);
                if (humanoid)
                {
                    ApplyCannibalismConsequences(actor, consumed);
                }
                else if (IsUnsafeFood(consumed))
                {
                    bodyHealthCommands.ApplyLegacyDamage(
                        actor,
                        3f,
                        "오염 음식",
                        allowDeath: true);
                    consequences.AddInfection(actor, 12f);
                }
                yield break;
            }
        }

        CharacterActor victim = FindLivingVictim(actor);
        if (victim == null)
        {
            yield break;
        }

        if (stateStore.TryGetWritable(actor, out CharacterDeprivationState state))
        {
            state.breakdown.targetId = GetPersistentId(victim);
            state.breakdown.targetGridX = victim.GetNowXY().x;
            state.breakdown.targetGridY = victim.GetNowXY().y;
        }

        while (actor != null && victim != null && !actor.IsDead && !victim.IsDead)
        {
            yield return emergencyMovement.MoveNear(actor, victim.GetNowXY(), 1);
            if (actor == null || victim == null || actor.IsDead || victim.IsDead)
            {
                break;
            }

            if (Manhattan(actor.GetNowXY(), victim.GetNowXY()) > 1)
            {
                break;
            }

            float damage = Mathf.Max(4f, actor.GetCharacterStat(CharacterStatType.Strength) * 1.2f);
            bodyHealthCommands.ApplyLegacyDamage(
                victim,
                damage,
                $"굶주린 {actor.Identity?.DisplayName ?? actor.name}의 습격",
                allowDeath: true);
            if (!victim.IsDead)
            {
                bodyHealthCommands.ApplyLegacyDamage(
                    actor,
                    Mathf.Max(
                        1f,
                        victim.GetCharacterStat(CharacterStatType.Strength) * 0.35f),
                    "필사적인 반격",
                    allowDeath: true);
            }
            yield return CannibalAttackDelay;
        }

        if (victim != null && victim.IsDead)
        {
            yield return CorpseSpawnDelay;
            WorldItemStackSnapshot corpse = FindHumanoidCorpse(victim);
            if (corpse != null
                && world.TryConsumeStack(corpse.StackId, 1, out WorldItemStackSnapshot consumed))
            {
                RecoverNeed(
                    actor,
                    CharacterCondition.HUNGER,
                    85f,
                    CharacterNeedRecoverySource.Emergency);
                ApplyCannibalismConsequences(actor, consumed);
            }
        }
    }


    private static IEnumerator RunCollapse(CharacterActor actor)
    {
        if (actor == null)
        {
            yield break;
        }

        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Started,
            "바닥에 쓰러져 잠듦",
            actionId: "survival/collapse",
            sentiment: -0.65f,
            bubbleEligible: true));
        yield return CollapseDelay;
        if (actor != null && !actor.IsDead)
        {
            RecoverNeed(
                actor,
                CharacterCondition.SLEEP,
                35f,
                CharacterNeedRecoverySource.Emergency);
            actor.ApplyMoodFactor("survival:floor-collapse", "차가운 바닥에서 깨어남", -5f, 180f, 1);
        }
    }


    private IEnumerator RunViolentImpulse(CharacterActor actor)
    {
        if (actor == null)
        {
            yield break;
        }

        actor.ApplyMoodFactor("survival:violent-impulse", "분노에 휩쓸림", -6f, 180f, 1);
        CharacterAiPersonality personality = GetPersonality(actor);
        GetViolentImpulseThresholds(personality, out float vandalThreshold, out float assaultThreshold);
        float choice = breakdownRandom.NextFloat();
        if (choice < vandalThreshold && TryFindVandalismTarget(actor, out BuildableObject building))
        {
            yield return emergencyMovement.MoveNear(actor, building.centerPos, 1);
            if (actor != null
                && !actor.IsDead
                && building != null
                && !building.IsGridDestroyed
                && !building.IsDamaged
                && Manhattan(actor.GetNowXY(), building.centerPos) <= 1)
            {
                building.SetDamaged(true);
                actor.AddActivity(CharacterActivityEvent.Facility(
                    CharacterActivityKinds.Combat,
                    CharacterActivityOutcomes.Damaged,
                    $"{GetBuildingLabel(building)}을 파손함",
                    building,
                    actionId: "survival:violent-vandalism",
                    reasonCode: "mental-instability",
                    value: 1f,
                    bubbleEligible: true));
                consequences.ApplyWitnessMood(actor, actor.GetNowXY(), "붕괴자의 난동을 목격함", -5f);
                yield return ViolentActionDelay;
                yield break;
            }
        }

        if (choice < assaultThreshold)
        {
            CharacterActor victim = FindLivingVictim(actor);
            if (victim != null)
            {
                yield return emergencyMovement.MoveNear(actor, victim.GetNowXY(), 1);
                if (actor != null
                    && victim != null
                    && !actor.IsDead
                    && !victim.IsDead
                    && Manhattan(actor.GetNowXY(), victim.GetNowXY()) <= 1)
                {
                    float damage = Mathf.Clamp(
                        2f + actor.GetCharacterStat(CharacterStatType.Strength) * 0.45f,
                        3f,
                        10f);
                    bodyHealthCommands.ApplyLegacyDamage(
                        victim,
                        damage,
                        $"붕괴한 {actor.Identity?.DisplayName ?? actor.name}의 폭행",
                        allowDeath: true);
                    actor.AddActivity(CharacterActivityEvent.Create(
                        CharacterActivityKinds.Combat,
                        CharacterActivityOutcomes.Damaged,
                        $"{victim.Identity?.DisplayName ?? victim.name}에게 달려들었다",
                        actionId: "survival:violent-assault",
                        targetId: GetPersistentId(victim),
                        reasonCode: "mental-instability",
                        value: damage,
                        sentiment: -1f,
                        bubbleEligible: true));
                    consequences.ApplyWitnessMood(actor, victim.GetNowXY(), "이성을 잃은 폭행을 목격함", -7f);
                    yield return ViolentActionDelay;
                    yield break;
                }
            }
        }

        if (IdleBehaviorRunner.TryRunDefault(actor, 2.2f, true, out string behavior, out _))
        {
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Health,
                CharacterActivityOutcomes.Started,
                $"불안정하게 {behavior}",
                actionId: "survival/mental-breakdown",
                sentiment: -0.75f,
                bubbleEligible: true));
        }
        yield return BreakdownIdleDelay;
        actor.ChangesStat(CharacterCondition.FUN, 8f);
    }


    private bool TryFindVandalismTarget(CharacterActor actor, out BuildableObject target)
    {
        target = null;
        if (actor == null)
        {
            return false;
        }

        Vector2Int origin = actor.GetNowXY();
        int bestDistance = int.MaxValue;
        IReadOnlyList<BuildableObject> buildings = world.Buildings;
        for (int index = 0; index < buildings.Count; index++)
        {
            BuildableObject candidate = buildings[index];
            if (candidate == null
                || candidate.IsGridDestroyed
                || candidate.IsDamaged
                || candidate.IsGridMovement)
            {
                continue;
            }

            int distance = Manhattan(origin, candidate.centerPos);
            if (distance >= bestDistance)
            {
                continue;
            }

            target = candidate;
            bestDistance = distance;
        }

        return target != null;
    }


    private static string GetBuildingLabel(BuildableObject building)
    {
        return building?.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building != null ? building.name : "시설";
    }


    private bool TryChooseAccidentPosition(CharacterActor actor, out Vector2Int position)
    {
        position = actor != null ? actor.GetNowXY() : default;
        if (actor == null || !world.TryGetGrid(out Grid grid))
        {
            return false;
        }

        Vector2Int origin = actor.GetNowXY();
        GridCell best = null;
        int bestPriority = int.MaxValue;
        int bestDistance = int.MaxValue;
        int minX = Mathf.Max(0, origin.x - AccidentSearchRadius);
        int maxX = Mathf.Min(grid.width - 1, origin.x + AccidentSearchRadius);
        for (int x = minX; x <= maxX; x++)
        {
            Vector2Int candidate = new Vector2Int(x, origin.y);
            GridCell cell = grid.GetGridCell(candidate);
            if (cell == null || !grid.IsWalkable(candidate))
            {
                continue;
            }

            int priority = world.GetAccidentLocationPriority(grid, cell);
            int candidateDistance = Mathf.Abs(x - origin.x);
            if (best != null
                && (priority > bestPriority
                    || (priority == bestPriority
                        && candidateDistance >= bestDistance)))
            {
                continue;
            }

            best = cell;
            bestPriority = priority;
            bestDistance = candidateDistance;
        }
        if (best == null)
        {
            return false;
        }

        position = best.Position;
        return true;
    }


    private bool TryFindEmergencyFood(CharacterActor actor, out WorldItemStackSnapshot food)
    {
        food = null;
        return actor != null
            && world.TryFindBestAvailableStack(
                actor.GetNowXY(),
                GetEmergencyFoodRank,
                out food);
    }


    private int GetEmergencyFoodRank(string itemId)
    {
        if (!itemCatalog.TryGet((ItemDefinitionId)itemId, out ItemDefinitionSO definition))
        {
            return int.MaxValue;
        }

        ResourceIngredientTag tags =
            definition.GetFeatureOrDefault<ProductionItemFeature>()?.ingredientTags
            ?? ResourceIngredientTag.None;
        if ((tags & (ResourceIngredientTag.Spoiled | ResourceIngredientTag.Forbidden)) != 0)
        {
            return 0;
        }
        if (WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(itemId, out _)) return 1;
        if (itemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId) return 2;
        if (itemId == DarkSurvivalItemDefinitions.HumanoidMeatItemId) return 3;
        return definition.StockCategory == StockCategory.Food ? 4 : int.MaxValue;
    }

    private bool IsUnsafeFood(WorldItemStackSnapshot stack)
    {
        if (stack == null || stack.Contamination > 0.01f)
        {
            return stack != null;
        }

        if (!itemCatalog.TryGet(
                (ItemDefinitionId)stack.ItemId,
                out ItemDefinitionSO definition))
        {
            return false;
        }

        ResourceIngredientTag tags =
            definition.GetFeatureOrDefault<ProductionItemFeature>()?.ingredientTags
            ?? ResourceIngredientTag.None;
        return (tags & (ResourceIngredientTag.Spoiled | ResourceIngredientTag.Forbidden)) != 0;
    }


    private CharacterActor FindLivingVictim(CharacterActor attacker)
    {
        if (attacker == null)
        {
            return null;
        }

        CharacterActor best = null;
        float bestHealthRatio = float.PositiveInfinity;
        int bestNearbyCount = int.MaxValue;
        float bestSentiment = float.PositiveInfinity;
        int bestDistance = int.MaxValue;
        Vector2Int origin = attacker.GetNowXY();
        IReadOnlyList<CharacterActor> characters = world.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor candidate = characters[index];
            if (!IsEligibleHumanoid(candidate)
                || candidate == attacker
                || candidate.IsDead)
            {
                continue;
            }

            float healthRatio =
                candidate.CurrentHealth / Mathf.Max(1f, candidate.MaxHealth);
            int nearbyCount = CountNearbyHumanoids(candidate, 3);
            float sentiment =
                attacker.SocialMemory?.GetRelationshipSentiment(candidate) ?? 0f;
            int distance = Manhattan(origin, candidate.GetNowXY());
            if (best != null
                && (healthRatio > bestHealthRatio
                    || Mathf.Approximately(healthRatio, bestHealthRatio)
                    && (nearbyCount > bestNearbyCount
                        || nearbyCount == bestNearbyCount
                        && (sentiment > bestSentiment
                            || Mathf.Approximately(sentiment, bestSentiment)
                            && distance >= bestDistance))))
            {
                continue;
            }

            best = candidate;
            bestHealthRatio = healthRatio;
            bestNearbyCount = nearbyCount;
            bestSentiment = sentiment;
            bestDistance = distance;
        }

        return best;
    }


    private int CountNearbyHumanoids(CharacterActor center, int radius)
    {
        if (center == null)
        {
            return 0;
        }

        int count = 0;
        Vector2Int origin = center.GetNowXY();
        IReadOnlyList<CharacterActor> characters = world.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor candidate = characters[index];
            if (IsEligibleHumanoid(candidate)
                && candidate != center
                && !candidate.IsDead
                && Manhattan(origin, candidate.GetNowXY()) <= radius)
            {
                count++;
            }
        }

        return count;
    }


    private WorldItemStackSnapshot FindHumanoidCorpse(CharacterActor victim)
    {
        if (victim == null)
        {
            return null;
        }

        string victimId = GetPersistentId(victim);
        IReadOnlyList<WorldItemStackSnapshot> stacks =
            world.GetStacksAt(victim.GetNowXY(), includeStored: true);
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (stack != null
                && stack.ItemId ==
                    DarkSurvivalItemDefinitions.HumanoidCorpseItemId
                && string.Equals(
                    stack.SourceCharacterId,
                    victimId,
                    StringComparison.Ordinal))
            {
                return stack;
            }
        }

        return null;
    }


    private void ApplyCannibalismConsequences(CharacterActor actor, WorldItemStackSnapshot consumed)
    {
        bool sameSpecies = !string.IsNullOrWhiteSpace(consumed.SourceSpeciesTag)
            && string.Equals(actor.Identity?.SpeciesTag, consumed.SourceSpeciesTag, StringComparison.OrdinalIgnoreCase);
        CharacterAiPersonality personality = GetPersonality(actor);
        float conscience01 = personality != null
            ? (Mathf.InverseLerp(0.25f, 2f, personality.selfCare)
                + Mathf.InverseLerp(0.25f, 2f, personality.orderliness)
                + Mathf.InverseLerp(0.25f, 2f, personality.routineAdherence)) / 3f
            : 0.5f;
        float appetite01 = personality != null
            ? (Mathf.InverseLerp(0.25f, 2f, personality.riskTaking)
                + Mathf.InverseLerp(0.25f, 2f, personality.noveltySeeking)) * 0.5f
            : 0.5f;
        float mood = (sameSpecies ? -18f : -11f) * Mathf.Lerp(0.45f, 1.25f, conscience01);
        string reaction = appetite01 > 0.72f && conscience01 < 0.45f
            ? "금기의 맛을 다시 떠올림"
            : conscience01 < 0.35f
                ? "금기에 무감각해짐"
                : sameSpecies ? "동족을 먹었다" : "인간형 사체를 먹었다";
        actor.ApplyMoodFactor(
            sameSpecies ? "survival:same-species-cannibalism" : "survival:cannibalism",
            reaction,
            mood,
            900f,
            1);
        actor.ChangesStat(CharacterCondition.HYGIENE, -20f);
        consequences.AddInfection(actor, sameSpecies ? 20f : 12f);
        string victim = string.IsNullOrWhiteSpace(consumed.SourceDisplayName) ? "이름 모를 사체" : consumed.SourceDisplayName;
        consequences.RecordTaboo(actor, $"극한의 굶주림 속에서 {victim}을 먹었다");
        consequences.ApplyWitnessMood(
            actor,
            actor.GetNowXY(),
            "금기의 포식을 목격함",
            sameSpecies ? -12f : -8f,
            permanentMemory: true);
    }


    private static void GetViolentImpulseThresholds(
        CharacterAiPersonality personality,
        out float vandalThreshold,
        out float assaultThreshold)
    {
        float risk01 = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.riskTaking)
            : 0.5f;
        float order01 = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.orderliness)
            : 0.5f;
        float social01 = personality != null
            ? Mathf.InverseLerp(0.25f, 2f, personality.sociability)
            : 0.5f;
        float vandalWeight = 0.25f + (1f - order01) * 0.35f;
        float assaultWeight = 0.2f + risk01 * 0.4f + (1f - social01) * 0.1f;
        float restlessWeight = 0.2f + (1f - risk01) * 0.25f;
        float total = vandalWeight + assaultWeight + restlessWeight;
        vandalThreshold = vandalWeight / total;
        assaultThreshold = vandalThreshold + assaultWeight / total;
    }


    internal static CharacterAiPersonality GetPersonality(CharacterActor actor)
    {
        return actor != null && actor.Identity != null && actor.Identity.Data != null
            ? actor.Identity.Data.aiPersonality
            : null;
    }

    private float ApplyPersonalWaterConsumption(float amount)
    {
        return needBalanceRuntime.ApplyPersonalContinuousWaterMultiplier(amount);
    }

    private static float GetWaterRecovery(WorldWaterQuality quality)
    {
        return quality switch
        {
            WorldWaterQuality.Clean => 65f,
            WorldWaterQuality.Unsafe => 55f,
            _ => 45f
        };
    }

    private static void RecoverNeed(
        CharacterActor actor,
        CharacterCondition condition,
        float amount,
        CharacterNeedRecoverySource source)
    {
        actor?.Stats?.RecoverNeed(condition, amount, source);
    }

    private static float GetNeed(
        CharacterActor actor,
        CharacterCondition condition)
    {
        return actor != null
            && actor.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? Mathf.Clamp(value, 0f, 100f)
                : 100f;
    }

    private static bool IsEligibleHumanoid(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && actor.CurrentLifecycleState != CharacterLifecycleState.OnExpedition;
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : string.Empty;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static CharacterBreakdownKind ResolveBreakdownKind(
        DeprivationKind kind)
    {
        return kind switch
        {
            DeprivationKind.Bladder => CharacterBreakdownKind.DesperateRelief,
            DeprivationKind.Thirst => CharacterBreakdownKind.DesperateDrink,
            DeprivationKind.Hunger => CharacterBreakdownKind.DesperateEat,
            DeprivationKind.Exhaustion => CharacterBreakdownKind.Collapse,
            _ => CharacterBreakdownKind.ViolentImpulse
        };
    }

    private static string GetBreakdownLabel(CharacterBreakdownKind kind)
    {
        return kind switch
        {
            CharacterBreakdownKind.DesperateRelief => "배변 붕괴",
            CharacterBreakdownKind.DesperateDrink => "갈증 붕괴",
            CharacterBreakdownKind.DesperateEat => "굶주림 붕괴",
            CharacterBreakdownKind.Collapse => "탈진 실신",
            CharacterBreakdownKind.ViolentImpulse => "정신 붕괴",
            _ => "붕괴"
        };
    }

    private void CreateActionRoutines()
    {
        actionRoutines[CharacterBreakdownKind.DesperateRelief] =
            RunDesperateRelief;
        actionRoutines[CharacterBreakdownKind.DesperateDrink] =
            actor => RunDesperateDrink(actor, allowWaste: true);
        actionRoutines[CharacterBreakdownKind.DesperateEat] =
            RunDesperateEat;
        actionRoutines[CharacterBreakdownKind.Collapse] =
            RunCollapse;
        actionRoutines[CharacterBreakdownKind.ViolentImpulse] =
            RunViolentImpulse;
    }
}
