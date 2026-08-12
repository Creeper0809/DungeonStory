#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class PrimitiveStartSurvivalPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/primitive-start-survival-5day-report.txt";
    public const string FocusedReportPath =
        "Artifacts/QA/primitive-survival-focused-report.txt";

    [MenuItem("DungeonStory/Debug/QA/Run Primitive Start Survival 5-Day Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Primitive start survival verification requires PlayMode.");
            return;
        }
        if (UnityEngine.Object.FindFirstObjectByType<PrimitiveStartSurvivalPlayModeRunner>()
            != null)
        {
            Debug.LogWarning("Primitive start survival verification is already running.");
            return;
        }
        CreateRunner(focusedOnly: false);
    }

    [MenuItem("DungeonStory/Debug/QA/Run Primitive Survival Focused Verification")]
    public static void RunFocusedFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Primitive survival focused verification requires PlayMode.");
            return;
        }
        if (UnityEngine.Object.FindFirstObjectByType<PrimitiveStartSurvivalPlayModeRunner>()
            != null)
        {
            Debug.LogWarning("Primitive survival verification is already running.");
            return;
        }
        CreateRunner(focusedOnly: true);
    }

    private static void CreateRunner(bool focusedOnly)
    {
        PrimitiveStartSurvivalPlayModeRunner runner =
            new GameObject(focusedOnly
                    ? "Primitive Survival Focused Verification"
                    : "Primitive Start Survival 5-Day Verification")
                .AddComponent<PrimitiveStartSurvivalPlayModeRunner>();
        runner.FocusedOnly = focusedOnly;
    }
}

public sealed class PrimitiveStartSurvivalPlayModeRunner : MonoBehaviour
{
    private const float DaySeconds = 180f;
    private const int Days = 5;
    private const float VerificationTimeScale = 20f;

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly Dictionary<string, int> primitiveCounts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> primitivePhysicalItemCounts =
        new(StringComparer.Ordinal);
    private float originalTimeScale;
    private IDisposable primitiveSubscription;
    private IDisposable mealSubscription;
    private int physicalMeals;
    private int physicalFieldMeals;
    public bool FocusedOnly { get; set; }
    private string ActiveReportPath => FocusedOnly
        ? PrimitiveStartSurvivalPlayModeVerifier.FocusedReportPath
        : PrimitiveStartSurvivalPlayModeVerifier.ReportPath;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory("Artifacts/QA");
        originalTimeScale = Time.timeScale;
        yield return new WaitForSecondsRealtime(1f);
        yield return RunVerification();
        CompleteVerification();
    }

    private IEnumerator RunVerification()
    {
        DungeonRuntimeLifetimeScope scope = FindRuntimeScope();
        float containerWaitDeadline = Time.realtimeSinceStartup + 10f;
        while (scope == null && Time.realtimeSinceStartup < containerWaitDeadline)
        {
            yield return null;
            scope = FindRuntimeScope();
        }
        Check(scope != null, "RUNTIME_SCOPE", "runtime scope resolved");
        Check(scope?.Container != null, "RUNTIME_CONTAINER", "runtime container built");
        if (scope?.Container == null)
        {
            yield break;
        }

        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
        primitiveSubscription = events.Subscribe<CharacterPrimitiveSurvivalCompletedEvent>(
            completed =>
            {
                primitiveCounts.TryGetValue(completed.ActionId, out int count);
                primitiveCounts[completed.ActionId] = count + 1;
                primitivePhysicalItemCounts.TryGetValue(
                    completed.ActionId,
                    out int physicalCount);
                primitivePhysicalItemCounts[completed.ActionId] =
                    physicalCount + completed.PhysicalItemCount;
            });
        mealSubscription = events.Subscribe<PhysicalMealConsumedEvent>(consumed =>
        {
            if (!consumed.Result.Success)
            {
                return;
            }

            physicalMeals++;
            if (consumed.Facility == null)
            {
                physicalFieldMeals++;
            }
        });

        CharacterActor[] party = ResolveParty();
        if (party.Length == 3)
        {
            Check(true,
                "START_COMMIT",
                "GameplayScene launch flow already committed the starting party.");
        }
        else
        {
            string commit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            Check(commit.Contains("committed=True", StringComparison.OrdinalIgnoreCase),
                "START_COMMIT",
                commit);
            yield return null;
            party = ResolveParty();
        }

        IWorldItemStackRuntime items =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        IGridSystemProvider grids = scope.Container.Resolve<IGridSystemProvider>();
        IFacilityCandidateCache facilities =
            scope.Container.Resolve<IFacilityCandidateCache>();
        ICharacterDeprivationQuery deprivation =
            scope.Container.Resolve<ICharacterDeprivationQuery>();
        ICharacterDeprivationCommand deprivationCommands =
            scope.Container.Resolve<ICharacterDeprivationCommand>();
        IEnvironmentalFieldQuery environment =
            scope.Container.Resolve<IEnvironmentalFieldQuery>();
        ICharacterSpeciesEnvironmentCatalog speciesEnvironment =
            scope.Container.Resolve<ICharacterSpeciesEnvironmentCatalog>();
        IGameClock clock = scope.Container.Resolve<IGameClock>();

        Check(party.Length == 3, "PARTY_SIZE", $"party={party.Length}");
        report.Add("start-environment=" + string.Join(" | ", party.Select(actor =>
            DescribeEnvironment(actor, environment, speciesEnvironment))));
        VerifyStarterSupplies(items);
        int initialRations = CountItem(items, "food:preserved-ration");
        int initialWater = CountItem(items, "resource:clean-water");
        int maximumRations = initialRations;
        int maximumWater = initialWater;

        if (grids.TryGetGrid(out Grid grid))
        {
            int meal = facilities.GetCandidates(grid, FacilityRole.Meal).Count;
            int rest = facilities.GetCandidates(grid, FacilityRole.Rest).Count;
            int toilet = facilities.GetCandidates(grid, FacilityRole.Toilet).Count;
            int hygiene = facilities.GetCandidates(grid, FacilityRole.Hygiene).Count;
            Check(meal + rest + toilet + hygiene == 0,
                "NO_SERVICE_FOUNDATION",
                $"meal/rest/toilet/hygiene={meal}/{rest}/{toilet}/{hygiene}");
        }
        else
        {
            Check(false, "GRID", "grid unavailable");
        }

        if (FocusedOnly)
        {
            yield return RunFocusedVerification(
                party,
                deprivationCommands,
                items,
                clock);
            yield break;
        }

        foreach (CharacterActor actor in party)
        {
            ResetNeeds(actor);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }

        float startedAt = clock.Time;
        float nextDayAt = startedAt + DaySeconds;
        int sampledDay = 0;
        Time.timeScale = VerificationTimeScale;
        while (clock.Time < startedAt + DaySeconds * Days
            && party.All(actor => actor != null && !actor.IsDead))
        {
            maximumRations = Mathf.Max(
                maximumRations,
                CountItem(items, "food:preserved-ration"));
            maximumWater = Mathf.Max(
                maximumWater,
                CountItem(items, "resource:clean-water"));
            if (clock.Time >= nextDayAt)
            {
                sampledDay++;
                report.Add(DescribeDay(sampledDay, party, items, deprivation));
                File.WriteAllLines(
                    ActiveReportPath,
                    report);
                nextDayAt += DaySeconds;
            }
            yield return null;
        }
        Time.timeScale = 0f;

        Check(party.All(actor => actor != null && !actor.IsDead),
            "FIVE_DAY_SURVIVAL",
            string.Join(", ", party.Select(DescribeActor)));
        Check(party.All(actor => actor.CurrentHealth > 0f),
            "POSITIVE_HEALTH",
            string.Join(", ", party.Select(DescribeActor)));
        Check(party.All(actor => !deprivation.HasActiveBreakdown(actor)),
            "NO_ACTIVE_BREAKDOWN",
            string.Join(", ", party.Select(actor =>
                $"{actor.Identity?.PersistentId}:{deprivation.HasActiveBreakdown(actor)}")));
        int naturalFinalRations = CountItem(items, "food:preserved-ration");
        int naturalFinalWater = CountItem(items, "resource:clean-water");
        Check(maximumRations <= initialRations && maximumWater <= initialWater,
            "NATURAL_ITEM_CONSERVATION",
            $"ration initial/max/final={initialRations}/{maximumRations}/{naturalFinalRations}; "
                + $"water initial/max/final={initialWater}/{maximumWater}/{naturalFinalWater}");
        Check(physicalMeals > 0 && naturalFinalRations < initialRations,
            "NATURAL_PHYSICAL_MEALS",
            $"events={physicalMeals}; ration={initialRations}->{naturalFinalRations}");
        Check(naturalFinalWater < initialWater,
            "NATURAL_PHYSICAL_WATER",
            $"water={initialWater}->{naturalFinalWater}");
        if (party.Any(actor => actor == null || actor.IsDead))
        {
            yield break;
        }
    }

    private IEnumerator RunFocusedVerification(
        CharacterActor[] party,
        ICharacterDeprivationCommand deprivationCommands,
        IWorldItemStackRuntime items,
        IGameClock clock)
    {
        CharacterActor focusedActor = party.FirstOrDefault(actor =>
            actor != null
            && !actor.IsDead
            && actor.Role == CharacterRole.Owner)
            ?? party.FirstOrDefault(actor => actor != null && !actor.IsDead);
        Check(focusedActor?.Brain != null && focusedActor.Stats != null,
            "FOCUSED_SETUP",
            focusedActor != null
                ? $"actor={focusedActor.Identity?.PersistentId}; external={focusedActor.Brain?.IsExternallyDrivenActionActive}"
                : "no live focused actor");
        if (focusedActor?.Brain == null || focusedActor.Stats == null)
        {
            yield break;
        }

        foreach (CharacterActor actor in party)
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }
            deprivationCommands.DebugClearBreakdown(actor);
            ResetNeeds(actor);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }

        yield return VerifyFocusedPrimitive<AIPrimitiveFieldMeal>(
            focusedActor,
            CharacterCondition.HUNGER,
            FacilityRole.Meal,
            "survival:field-meal",
            "food:preserved-ration",
            1,
            items,
            clock);
        yield return VerifyFocusedPrimitive<AIPrimitiveFloorRest>(
            focusedActor,
            CharacterCondition.SLEEP,
            FacilityRole.Rest,
            "survival:floor-rest",
            string.Empty,
            0,
            items,
            clock);
        yield return VerifyFocusedPrimitive<AIPrimitiveLatrine>(
            focusedActor,
            CharacterCondition.EXCRETION,
            FacilityRole.Toilet,
            "survival:primitive-latrine",
            string.Empty,
            0,
            items,
            clock);
        yield return VerifyFocusedPrimitive<AIPrimitiveBucketWash>(
            focusedActor,
            CharacterCondition.HYGIENE,
            FacilityRole.Hygiene,
            "survival:bucket-wash",
            "resource:clean-water",
            1,
            items,
            clock);

        Check(physicalFieldMeals == GetCount("survival:field-meal"),
            "FOCUSED_FIELD_MEAL_AUTHORITY",
            $"primitive={GetCount("survival:field-meal")}; physical={physicalFieldMeals}");
        Time.timeScale = 0f;
    }

    private IEnumerator VerifyFocusedPrimitive<TAction>(
        CharacterActor actor,
        CharacterCondition targetNeed,
        FacilityRole authoredFacilityRole,
        string actionId,
        string consumedItemId,
        int expectedItemCost,
        IWorldItemStackRuntime items,
        IGameClock clock)
        where TAction : AIPrimitiveSurvivalAction
    {
        string checkPrefix = "FOCUSED_" + actionId
            .Replace("survival:", string.Empty)
            .Replace('-', '_')
            .ToUpperInvariant();
        if (actor?.Brain == null || actor.Stats == null)
        {
            Check(false, checkPrefix + "_SETUP", "focused actor or brain is missing");
            yield break;
        }

        ResetNeeds(actor);
        CharacterNeedResponseProfile response = actor.Stats.GetNeedResponse(targetNeed);
        SetNeed(actor, targetNeed, Mathf.Max(1f, response.emergencyStart - 1f));
        float needBefore = GetNeed(actor, targetNeed);
        int itemBefore = string.IsNullOrWhiteSpace(consumedItemId)
            ? 0
            : CountItem(items, consumedItemId);
        int countBefore = GetCount(actionId);
        int physicalItemCountBefore = GetPrimitivePhysicalItemCount(actionId);

        AIAction[] originalActions = actor.Brain.availableActions;
        AIAction focusedAction = originalActions?
            .FirstOrDefault(candidate => candidate?.actionset is TAction);
        Check(focusedAction != null,
            checkPrefix + "_REGISTERED",
            focusedAction != null ? focusedAction.actionset.GetType().Name : "missing");
        if (focusedAction == null)
        {
            yield break;
        }

        actor.Brain.availableActions = new[] { focusedAction };
        bool canStart = focusedAction.actionset.CanStart(actor);
        bool authoredFacilityPresent = FacilityCandidateScorer.HasCandidate(
            actor,
            null,
            authoredFacilityRole);
        if (authoredFacilityPresent)
        {
            Check(!canStart,
                checkPrefix + "_SUPPRESSED_BY_FACILITY",
                $"canStart={canStart}; role={authoredFacilityRole}; need={needBefore:0.##}");
            actor.Brain.availableActions = originalActions;
            actor.Brain.RequestImmediateReplan(clearFailures: true);
            yield break;
        }

        bool preferred = actor.Brain.PreferActionOnNextDecision<TAction>(180f);
        Check(canStart && preferred,
            checkPrefix + "_AI_ELIGIBLE",
            $"canStart={canStart}; preferred={preferred}; need={needBefore:0.##}");
        actor.Brain.RequestImmediateReplan(clearFailures: true);

        Time.timeScale = VerificationTimeScale;
        float deadline = clock.Time + 240f;
        float realtimeDeadline = Time.realtimeSinceStartup + 15f;
        while (GetCount(actionId) == countBefore
            && clock.Time < deadline
            && Time.realtimeSinceStartup < realtimeDeadline
            && actor != null
            && !actor.IsDead)
        {
            Time.timeScale = VerificationTimeScale;
            yield return null;
        }

        float externalSettleDeadline = Time.realtimeSinceStartup + 1f;
        while (actor.Brain.IsExternallyDrivenActionActive
            && Time.realtimeSinceStartup < externalSettleDeadline)
        {
            yield return null;
        }

        actor.Brain.availableActions = originalActions;
        actor.Brain.RequestImmediateReplan(clearFailures: true);
        float needAfter = GetNeed(actor, targetNeed);
        int itemAfter = string.IsNullOrWhiteSpace(consumedItemId)
            ? 0
            : CountItem(items, consumedItemId);
        bool completed = GetCount(actionId) == countBefore + 1;
        int eventPhysicalItemCost =
            GetPrimitivePhysicalItemCount(actionId) - physicalItemCountBefore;
        bool itemConserved = eventPhysicalItemCost == expectedItemCost
            && (expectedItemCost == 0
                || itemAfter <= itemBefore - expectedItemCost);
        Check(completed && needAfter > needBefore && itemConserved,
            checkPrefix + "_COMPLETED",
            $"events={countBefore}->{GetCount(actionId)}; "
                + $"need={needBefore:0.##}->{needAfter:0.##}; "
                + $"item={itemBefore}->{itemAfter}; expectedCost={expectedItemCost}; "
                + $"eventPhysicalCost={eventPhysicalItemCost}; "
                + $"action={actor.Brain.CurrentActionDebugLabel}");
    }

    private void CompleteVerification()
    {
        primitiveSubscription?.Dispose();
        mealSubscription?.Dispose();
        Time.timeScale = originalTimeScale;
        report.Insert(0, failures.Count == 0 ? "PASS" : "FAIL");
        report.Add("primitive-counts=" + string.Join(", ", primitiveCounts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value}")));
        report.Add($"physical-field-meals={physicalFieldMeals}");
        report.Add($"physical-meals={physicalMeals}");
        report.AddRange(failures.Select(value => "FAILURE " + value));
        File.WriteAllLines(ActiveReportPath, report);
        if (failures.Count == 0)
        {
            Debug.Log("[PrimitiveStartSurvivalPlayModeVerifier] PASS "
                + string.Join(" | ", report));
        }
        else
        {
            Debug.LogError("[PrimitiveStartSurvivalPlayModeVerifier] FAIL "
                + string.Join(" | ", failures));
        }
        Destroy(gameObject);
    }

    private void VerifyStarterSupplies(IWorldItemStackRuntime items)
    {
        Check(CountItem(items, "food:preserved-ration") >= 24,
            "START_RATIONS",
            $"count={CountItem(items, "food:preserved-ration")}");
        Check(CountItem(items, "resource:clean-water") >= 30,
            "START_WATER",
            $"count={CountItem(items, "resource:clean-water")}");
        Check(CountItem(items, "material:lumber") >= 15,
            "START_LUMBER",
            $"count={CountItem(items, "material:lumber")}");
        Check(CountItem(items, "material:cloth") >= 9,
            "START_CLOTH",
            $"count={CountItem(items, "material:cloth")}");
        Check(CountItem(items, "craft:fermented-vinegar") == 0,
            "NO_VINEGAR_AS_FOOD",
            $"count={CountItem(items, "craft:fermented-vinegar")}");
        Check(CountItem(items, "captivity:restraints") == 0,
            "NO_RESTRAINTS_AS_GENERAL",
            $"count={CountItem(items, "captivity:restraints")}");
    }

    private static DungeonRuntimeLifetimeScope FindRuntimeScope() =>
        Resources.FindObjectsOfTypeAll<DungeonRuntimeLifetimeScope>()
            .Where(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.Container != null)
            .OrderBy(candidate => candidate.gameObject.scene.buildIndex)
            .ThenBy(candidate => candidate.GetInstanceID())
            .FirstOrDefault();

    private static CharacterActor[] ResolveParty()
    {
        return CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                && (actor.Role == CharacterRole.Owner
                    || actor.Identity?.PersistentId?.StartsWith(
                        "character:staff:",
                        StringComparison.Ordinal) == true))
            .OrderBy(actor => actor.Identity?.PersistentId, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
    }

    private static void ResetNeeds(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            return;
        }
        CharacterCondition[] needs =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE,
            CharacterCondition.FUN
        };
        foreach (CharacterCondition need in needs)
        {
            if (actor.Stats.TryGetConditionValue(need, out float current))
            {
                actor.ChangesStat(need, 100f - current);
            }
        }
    }

    private static void SetNeed(
        CharacterActor actor,
        CharacterCondition condition,
        float target)
    {
        if (actor?.Stats != null
            && actor.Stats.TryGetConditionValue(condition, out float current))
        {
            actor.ChangesStat(condition, target - current);
        }
    }

    private static string DescribeDay(
        int day,
        IEnumerable<CharacterActor> party,
        IWorldItemStackRuntime items,
        ICharacterDeprivationQuery deprivation)
    {
        string actors = string.Join(" | ", party.Select(actor =>
            DescribeActor(actor) + ":ai=" + DescribePrimitiveAi(actor, deprivation)));
        string stacks = string.Join(" | ", items.GetAllStacks()
            .Where(stack => stack != null
                && (stack.ItemId == "food:preserved-ration"
                    || stack.ItemId == "resource:clean-water"))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .Select(stack => $"{stack.StackId}:{stack.ItemId}:q={stack.Quantity}:"
                + $"available={stack.AvailableQuantity}:state={stack.State}:"
                + $"destination={stack.DestinationId}:pos={stack.Position}"));
        return $"day={day}; actors=[{actors}]; "
            + $"ration={CountItem(items, "food:preserved-ration")}; "
            + $"water={CountItem(items, "resource:clean-water")}; "
            + $"survival-stacks=[{stacks}]";
    }

    private static string DescribePrimitiveAi(
        CharacterActor actor,
        ICharacterDeprivationQuery deprivation)
    {
        if (actor == null)
        {
            return "missing";
        }

        bool meal = deprivation.NeedsPrimitiveMeal(actor, out string mealReason);
        bool rest = deprivation.NeedsPrimitiveRest(actor, out string restReason);
        bool relief = deprivation.NeedsPrimitiveRelief(actor, out string reliefReason);
        bool wash = deprivation.NeedsPrimitiveWash(actor, out string washReason);
        string registered = string.Join(",", actor.Brain?.availableActions?
            .Where(action => action?.actionset is AIPrimitiveSurvivalAction)
            .Select(action => $"{action.actionset.GetType().Name}:"
                + $"can={action.actionset.CanStart(actor)}:"
                + $"score={action.CalculateScore(actor):0.##}")
            ?? Array.Empty<string>());
        string candidates = string.Join(",", actor.Brain?.LastCandidateScores?
            .Select(candidate => $"{candidate.ActionLabel}:{candidate.Score:0.##}:"
                + candidate.Failure.Kind)
            ?? Array.Empty<string>());
        FacilityRole availableRoles = CharacterAiJobGiver.ResolveAvailableFacilityRoles(actor);
        return $"canRun={actor.CanRunAi}:current={actor.Brain?.CurrentActionDebugLabel}:"
            + $"availableRoles={availableRoles}:registered={registered}:"
            + $"meal={meal}({mealReason}):"
            + $"rest={rest}({restReason}):relief={relief}({reliefReason}):"
            + $"wash={wash}({washReason}):candidates={candidates}";
    }

    private static string DescribeActor(CharacterActor actor)
    {
        if (actor == null)
        {
            return "missing";
        }
        return $"{actor.Identity?.PersistentId}:hp={actor.CurrentHealth:0.##}:"
            + $"h={GetNeed(actor, CharacterCondition.HUNGER):0.##}:"
            + $"t={GetNeed(actor, CharacterCondition.THIRST):0.##}:"
            + $"s={GetNeed(actor, CharacterCondition.SLEEP):0.##}:"
            + $"e={GetNeed(actor, CharacterCondition.EXCRETION):0.##}:"
            + $"y={GetNeed(actor, CharacterCondition.HYGIENE):0.##}";
    }

    private static string DescribeEnvironment(
        CharacterActor actor,
        IEnvironmentalFieldQuery environment,
        ICharacterSpeciesEnvironmentCatalog speciesEnvironment)
    {
        if (actor == null
            || !environment.TryGetCell(
                actor.GetNowXY(),
                out EnvironmentalCellSnapshot cell))
        {
            return $"{actor?.Identity?.PersistentId}:missing";
        }
        SpeciesThermalProfile thermal = speciesEnvironment.GetRequiredThermalProfile(
            new CharacterSpeciesId(actor.SpeciesTag));
        return $"{actor.Identity?.PersistentId}@{actor.GetNowXY()}:"
            + $"species={actor.SpeciesTag}:temp={cell.TemperatureC:0.##}:"
            + $"air={cell.AirQuality:0.##}:light={cell.LightLevel:0.##}:"
            + $"lethal={thermal.LethalMinimum:0.##}..{thermal.LethalMaximum:0.##}";
    }

    private static float GetNeed(CharacterActor actor, CharacterCondition condition) =>
        actor?.Stats != null
        && actor.Stats.TryGetConditionValue(condition, out float value)
            ? value
            : -1f;

    private static int CountItem(IWorldItemStackRuntime items, string itemId) =>
        items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);

    private int GetCount(string actionId) =>
        primitiveCounts.TryGetValue(actionId, out int count) ? count : 0;

    private int GetPrimitivePhysicalItemCount(string actionId) =>
        primitivePhysicalItemCounts.TryGetValue(actionId, out int count) ? count : 0;

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"{(condition ? "PASS" : "FAIL")} {id}: {detail}");
        if (!condition)
        {
            failures.Add(id + ": " + detail);
        }
    }
}
#endif
