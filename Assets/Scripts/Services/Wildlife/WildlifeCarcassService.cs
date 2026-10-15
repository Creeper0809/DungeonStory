using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;

public interface IWildlifeCarcassService
{
    IReadOnlyList<WildlifeCarcassFreshnessSaveData> CaptureFreshness();
    IReadOnlyList<WildlifeCarcassProcessingSaveData> CaptureProcessing();
    void ReplaceFreshnessValidated(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries);
    WildlifeCarcassFreshnessRestoreTransaction ApplyFreshnessRestoreCandidate(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries,
        IReadOnlyList<WildlifeCarcassProcessingSaveData> processing = null);
    void RollbackFreshnessRestore(
        WildlifeCarcassFreshnessRestoreTransaction transaction);
    void CompleteFreshnessRestore(
        WildlifeCarcassFreshnessRestoreTransaction transaction);
    bool TryGetFreshness(
        string stackId,
        out float remainingFreshnessSeconds);
    void SpawnCarcass(WildlifeActor target);
    bool TrySpawnCarcass(WildlifeActor target, out string stackId);
    void TickFreshness(float deltaTime);
    bool TryButcherNextCarcass(
        IBuildingVisitorPort butcher,
        BuildableObject building,
        out int produced,
        out string message);
    bool HasButcherWorkAvailable(BuildableObject building);
    float GetButcherWorkUrgency();
}

public sealed class WildlifeCarcassFreshnessRestoreTransaction
{
    private readonly WildlifeCarcassService owner;
    private Action rollback;
    private Action complete;

    internal WildlifeCarcassFreshnessRestoreTransaction(
        WildlifeCarcassService owner,
        Action rollback,
        Action complete)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    internal void Rollback(WildlifeCarcassService expectedOwner)
    {
        Action action = RequireActive(expectedOwner, rollback);
        action();
        rollback = null;
        complete = null;
    }

    internal void Complete(WildlifeCarcassService expectedOwner)
    {
        Action action = RequireActive(expectedOwner, complete);
        action();
        complete = null;
        rollback = null;
    }

    private Action RequireActive(
        WildlifeCarcassService expectedOwner,
        Action action)
    {
        if (!ReferenceEquals(owner, expectedOwner) || action == null)
        {
            throw new InvalidOperationException(
                "Wildlife carcass freshness restore transaction has the wrong owner or is already finished.");
        }

        return action;
    }
}

public sealed class WildlifeCarcassService : IWildlifeCarcassService
{
    private const float DefaultFreshnessSeconds = 360f;

    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IPhysicalItemTransformService physicalTransforms;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IGameEventBus gameEventBus;
    private IGameCalendar calendar;
    private IDomainFactPublisher facts;
    private Dictionary<string, WildlifeCarcassProcessingSaveData> processingByOperation = new(StringComparer.Ordinal);
    private HashSet<string> processedStacks = new(StringComparer.Ordinal);
    private readonly Queue<string> pendingPublications = new();
    private readonly HashSet<string> queuedPublications = new(StringComparer.Ordinal);
    public string LastProcessingRecoveryFailure { get; private set; } = string.Empty;
    private Dictionary<string, WildlifeCarcassFreshnessSaveData> freshnessByStackId =
        new Dictionary<string, WildlifeCarcassFreshnessSaveData>(StringComparer.Ordinal);

    private WorldItemStackSnapshot cachedBestButcherCarcass;
    private int cachedBestButcherCarcassVersion = -1;

    public WildlifeCarcassService(
        IWorldItemStackRuntime itemStackRuntime,
        IPhysicalItemTransformService physicalTransforms,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IGameEventBus gameEventBus)
    {
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.physicalTransforms = physicalTransforms
            ?? throw new ArgumentNullException(nameof(physicalTransforms));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    [Inject]
    public void ConstructFactPublisher(
        IGameCalendar calendar,
        IDomainFactPublisher facts)
    {
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
    }

    public IReadOnlyList<WildlifeCarcassProcessingSaveData> CaptureProcessing() =>
        processingByOperation.Values.OrderBy(row => row.fact.operationId, StringComparer.Ordinal)
            .Select(row => row.Clone()).ToArray();

    public IReadOnlyList<WildlifeCarcassFreshnessSaveData> CaptureFreshness()
    {
        Dictionary<string, WorldItemStackSnapshot> stacksById =
            itemStackRuntime.GetAllStacks()
                .Where(stack => stack != null
                    && !string.IsNullOrWhiteSpace(stack.StackId))
                .GroupBy(stack => stack.StackId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
        return freshnessByStackId.Values
            .Where(entry => entry != null
                && !string.IsNullOrWhiteSpace(entry.stackId)
                && stacksById.TryGetValue(
                    entry.stackId,
                    out WorldItemStackSnapshot stack)
                && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    stack.ItemId,
                    out string speciesId)
                && string.Equals(
                    speciesId,
                    entry.speciesId,
                    StringComparison.Ordinal))
            .Select(Clone)
            .ToArray();
    }

    public void ReplaceFreshnessValidated(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries)
    {
        WildlifeCarcassFreshnessRestoreTransaction transaction =
            ApplyFreshnessRestoreCandidate(entries, CaptureProcessing());
        CompleteFreshnessRestore(transaction);
    }

    public WildlifeCarcassFreshnessRestoreTransaction ApplyFreshnessRestoreCandidate(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries,
        IReadOnlyList<WildlifeCarcassProcessingSaveData> processing = null)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        Dictionary<string, WildlifeCarcassFreshnessSaveData> restored =
            BuildFreshnessState(entries);
        Dictionary<string, WildlifeCarcassFreshnessSaveData> previous =
            freshnessByStackId;
        var restoredProcessing = BuildProcessingState(processing ?? Array.Empty<WildlifeCarcassProcessingSaveData>());
        var previousProcessing = processingByOperation;
        var previousProcessedStacks = processedStacks;
        WorldItemStackSnapshot previousCachedBest = cachedBestButcherCarcass;
        int previousCachedVersion = cachedBestButcherCarcassVersion;
        WildlifeCarcassFreshnessRestoreTransaction transaction =
            new WildlifeCarcassFreshnessRestoreTransaction(
                this,
                rollback: () =>
                {
                    RequireAppliedFreshness(restored);
                    freshnessByStackId = previous;
                    processingByOperation = previousProcessing;
                    processedStacks = previousProcessedStacks;
                    RebuildRecoveryQueue();
                    cachedBestButcherCarcass = previousCachedBest;
                    cachedBestButcherCarcassVersion = previousCachedVersion;
                },
                complete: () => RequireAppliedFreshness(restored));

        freshnessByStackId = restored;
        processingByOperation = restoredProcessing;
        processedStacks = restoredProcessing.Values.Select(row => row.fact.carcassStackId).ToHashSet(StringComparer.Ordinal);
        RebuildRecoveryQueue();
        InvalidateButcherCache();
        return transaction;
    }

    public void RollbackFreshnessRestore(
        WildlifeCarcassFreshnessRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Rollback(this);
    }

    public void CompleteFreshnessRestore(
        WildlifeCarcassFreshnessRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Complete(this);
    }

    private static Dictionary<string, WildlifeCarcassFreshnessSaveData>
        BuildFreshnessState(
            IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries)
    {
        Dictionary<string, WildlifeCarcassFreshnessSaveData> restored =
            new Dictionary<string, WildlifeCarcassFreshnessSaveData>(
                StringComparer.Ordinal);
        foreach (WildlifeCarcassFreshnessSaveData entry in entries)
        {
            if (entry == null
                || !((ItemStackId)entry.stackId).IsValid
                || string.IsNullOrWhiteSpace(entry.speciesId)
                || float.IsNaN(entry.remainingFreshnessSeconds)
                || float.IsInfinity(entry.remainingFreshnessSeconds)
                || entry.remainingFreshnessSeconds < 0f
                || restored.ContainsKey(entry.stackId))
            {
                throw new InvalidOperationException(
                    "Validated wildlife carcass candidate contains invalid state.");
            }

            restored.Add(entry.stackId, Clone(entry));
        }

        return restored;
    }

    private void RequireAppliedFreshness(
        Dictionary<string, WildlifeCarcassFreshnessSaveData> expected)
    {
        if (!ReferenceEquals(freshnessByStackId, expected))
        {
            throw new InvalidOperationException(
                "Wildlife carcass freshness restore transaction is no longer the active state.");
        }
    }

    public bool TryGetFreshness(
        string stackId,
        out float remainingFreshnessSeconds)
    {
        remainingFreshnessSeconds = 0f;
        if (string.IsNullOrWhiteSpace(stackId)
            || !freshnessByStackId.TryGetValue(
                stackId.Trim(),
                out WildlifeCarcassFreshnessSaveData freshness))
        {
            return false;
        }

        remainingFreshnessSeconds = Mathf.Max(
            0f,
            freshness.remainingFreshnessSeconds);
        return true;
    }

    public void SpawnCarcass(WildlifeActor target)
    {
        _ = TrySpawnCarcass(target, out _);
    }

    public bool TrySpawnCarcass(WildlifeActor target, out string stackId)
    {
        stackId = string.Empty;
        if (target == null || target.Species == null)
        {
            return false;
        }

        Vector2Int position = target.GridPosition;
        string itemId = target.Species.CarcassItemId;
        // Wildlife species and the physical carcass definition are an exact
        // mass-authority join. Silently despawning the body when content is
        // missing destroys both matter and the downstream butcher obligation.
        if (!itemStackRuntime.CatalogProvider.TryGetDefinition(itemId, out _))
        {
            throw new InvalidOperationException(
                $"Wildlife species '{target.SpeciesId}' has no physical carcass definition '{itemId}'.");
        }

        if (!itemStackRuntime.SpawnItemAt(
                itemId,
                1,
                position,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned <= 0)
        {
            return false;
        }

        WorldItemStackSnapshot stack = itemStackRuntime
            .GetStacksAt(position, includeStored: true)
            .LastOrDefault(candidate => candidate != null
                && string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal)
                && !freshnessByStackId.ContainsKey(candidate.StackId));
        if (stack == null)
        {
            return false;
        }

        freshnessByStackId[stack.StackId] = new WildlifeCarcassFreshnessSaveData
        {
            stackId = stack.StackId,
            speciesId = target.SpeciesId,
            remainingFreshnessSeconds = DefaultFreshnessSeconds
        };
        InvalidateButcherCache();
        stackId = stack.StackId;
        return true;
    }

    public void TickFreshness(float deltaTime)
    {
        if (deltaTime > 0f) RecoverPendingProcessing();
        if (freshnessByStackId.Count == 0 || deltaTime <= 0f)
        {
            return;
        }

        List<string> expired = null;
        foreach (WildlifeCarcassFreshnessSaveData entry in freshnessByStackId.Values)
        {
            entry.remainingFreshnessSeconds -= deltaTime;
            if (entry.remainingFreshnessSeconds <= 0f)
            {
                expired ??= new List<string>();
                expired.Add(entry.stackId);
            }
        }

        if (expired == null)
        {
            return;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = itemStackRuntime.GetAllStacks();
        foreach (string stackId in expired)
        {
            WorldItemStackSnapshot stack = stacks.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.StackId, stackId, StringComparison.Ordinal));
            if (stack == null)
            {
                freshnessByStackId.Remove(stackId);
                continue;
            }

            if (physicalTransforms.TryTransformWholeStack(
                    stackId,
                    new[]
                    {
                        new PhysicalItemTransformOutput(
                            WildlifeItemDefinitions.RotItemId,
                            1,
                            stack.Position)
                    },
                    $"wildlife-carcass-decay:{stackId}",
                    "wildlife-carcass-decay-loss",
                    out _,
                    out _,
                    out _))
            {
                freshnessByStackId.Remove(stackId);
            }
        }

        InvalidateButcherCache();
    }

    public bool TryButcherNextCarcass(
        IBuildingVisitorPort butcher,
        BuildableObject building,
        out int produced,
        out string message)
    {
        produced = 0;
        WorldItemStackSnapshot carcass = FindBestButcherCarcass();
        if (carcass == null)
        {
            message = "손질할 사체가 없습니다.";
            return false;
        }

        if (string.Equals(
                carcass.ItemId,
                DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                StringComparison.Ordinal))
        {
            return TryButcherHumanoidCorpse(
                butcher,
                building,
                carcass,
                out produced,
                out message);
        }

        if (!WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                carcass.ItemId,
                out string speciesId)
            || !speciesCatalog.TryGetSpecies(speciesId, out WildlifeSpeciesDefinition species))
        {
            message = "알 수 없는 사체입니다.";
            return false;
        }

        Vector2Int outputPosition = building != null ? building.centerPos : carcass.Position;
        PhysicalItemTransformOutput[] outputs = (species.ButcherYields
                ?? Array.Empty<WildlifeButcherYield>())
            .Where(yieldItem => yieldItem != null && yieldItem.amount > 0)
            .Select(yieldItem => new PhysicalItemTransformOutput(
                yieldItem.itemId,
                yieldItem.amount,
                outputPosition))
            .ToArray();
        if (outputs.Length == 0)
        {
            message = "도축 산출물 권위가 없습니다.";
            return false;
        }
        RequireFactDependencies();
        if (physicalTransforms is not IPhysicalItemTransformBatchService
            batchTransforms)
        {
            throw new InvalidOperationException(
                "Wildlife butchery requires atomic physical/outcome batch support.");
        }
        int outcomeDay = Math.Max(1, calendar.Day);
        string parentOperation =
            $"wildlife-carcass-butcher-parent:{carcass.StackId}";
        CharacterBuildingVisitorAdapter.TryGetActor(butcher, out CharacterActor butcherActor);
        var parentFact = new WildlifeCarcassProcessedFact(new WildlifeCarcassProcessingData
        {
            operationId = parentOperation,
            transformOperationId = $"wildlife-carcass-butcher:{carcass.StackId}",
            absoluteDay = outcomeDay,
            actorId = butcherActor != null ? CharacterPersistentIdentity.Require(butcherActor).Value : string.Empty,
            actorDisplayName = butcherActor != null ? butcherActor.Identity.DisplayName : string.Empty,
            speciesId = species.SpeciesId, speciesDisplayName = species.DisplayName,
            carcassStackId = carcass.StackId,
            facilityId = building != null ? building.RequirePersistentInstanceId().Value : string.Empty,
            inputQuantity = carcass.Quantity, outputQuantity = outputs.Sum(output => output.Quantity)
        });
        if (!TryReserveProcessing(parentFact, out var parentLease, out var parentJournal,
                out string reservationFailure))
        {
            message = "사체 처리 사실을 예약하지 못했습니다: " + reservationFailure;
            return false;
        }

        PhysicalItemTransformReceipt receipt = default;
        Exception physicalObserverFailure = null;
        bool transformed;
        string transformFailure;
        try
        {
            transformed = batchTransforms.TryTransformWholeStackWithCompanion(
                carcass.StackId,
                outputs,
                $"wildlife-carcass-butcher:{carcass.StackId}",
                "wildlife-carcass-butcher-loss",
                DomainFactEmission.Create(parentLease, parentFact), outcomeDay,
                out receipt,
                out _,
                out transformFailure);
        }
        catch (Exception exception)
        {
            if (!receipt.IsCommitted)
            {
                CancelProcessing(parentLease, parentJournal);
                throw;
            }
            physicalObserverFailure = exception;
            transformed = true;
            transformFailure = string.Empty;
        }
        if (!transformed)
        {
            CancelProcessing(parentLease, parentJournal);
            message = transformFailure;
            return false;
        }
        ConfirmProcessing(parentJournal, receipt);
        produced = receipt.OutputQuantity;
        freshnessByStackId.Remove(carcass.StackId);
        InvalidateButcherCache();

        if (!receipt.WasReplayed)
            gameEventBus.Publish(new WildlifeCarcassProcessedEvent(butcher, building, butcherActor, parentFact));
        message = produced > 0 ? "손질 완료" : "손질 산출물 없음";
        if (physicalObserverFailure != null)
        {
            throw new AggregateException(
                "Wildlife butchery committed; observer publication failed.",
                physicalObserverFailure);
        }
        return produced > 0;
    }

    public bool HasButcherWorkAvailable(BuildableObject building)
    {
        return building != null && FindBestButcherCarcass() != null;
    }

    public float GetButcherWorkUrgency()
    {
        int carcasses = itemStackRuntime.GetAllStacks().Count(stack =>
            stack != null
            && (WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(stack.ItemId, out _)
                || (string.Equals(
                        stack.ItemId,
                        DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                        StringComparison.Ordinal)
                    && stack.EmergencyButcheryAllowed)));
        return Mathf.Clamp(carcasses * 22f, 0f, 90f);
    }

    private bool TryButcherHumanoidCorpse(
        IBuildingVisitorPort butcher,
        BuildableObject building,
        WorldItemStackSnapshot carcass,
        out int produced,
        out string message)
    {
        produced = 0;
        if (carcass == null || !carcass.EmergencyButcheryAllowed)
        {
            message = "비상 도축이 허용되지 않은 사체입니다.";
            return false;
        }

        CharacterActor butcherActor = null;
        bool hasButcherActor = CharacterBuildingVisitorAdapter.TryGetActor(
            butcher,
            out butcherActor);
        DomainFactLease tabooLease = default;
        WildlifeCarcassProcessedFact tabooFact = null;
        WildlifeCarcassProcessingSaveData tabooJournal = null;
        Dictionary<string, WildlifeCarcassFreshnessSaveData> freshnessBefore = null;
        CharacterMoodDeliveryTransactionSnapshot statsBefore = null;
        float resolvedMood = 0f;
        float hygieneBefore = 0f;
        float hygieneAfter = 0f;
        bool moodApplied = false;
        int outcomeDay = 0;
        bool sameSpecies = hasButcherActor && string.Equals(
            butcherActor.SpeciesTag, carcass.SourceSpeciesTag, StringComparison.OrdinalIgnoreCase);
        IPhysicalItemTransformBatchService batchTransforms = physicalTransforms as IPhysicalItemTransformBatchService;
        if (hasButcherActor)
        {
            RequireFactDependencies();
            outcomeDay = Math.Max(1, calendar.Day);
            if (batchTransforms == null)
                throw new InvalidOperationException("Humanoid butchery requires atomic physical/outcome batch support.");
            _ = CharacterPersistentIdentity.Require(butcherActor);
            tabooFact = new WildlifeCarcassProcessedFact(new WildlifeCarcassProcessingData
            {
                operationId = "taboo-butchery:" + carcass.StackId,
                transformOperationId = $"humanoid-corpse-butcher:{carcass.StackId}",
                absoluteDay = outcomeDay, taboo = true,
                actorId = CharacterPersistentIdentity.Require(butcherActor).Value,
                actorDisplayName = butcherActor.Identity.DisplayName,
                carcassStackId = carcass.StackId,
                facilityId = building != null ? building.RequirePersistentInstanceId().Value : string.Empty,
                inputQuantity = carcass.Quantity, outputQuantity = 6,
                sourceCharacterId = carcass.SourceCharacterId ?? string.Empty,
                sourceDisplayName = string.IsNullOrWhiteSpace(carcass.SourceDisplayName)
                    ? "이름 모를 자" : carcass.SourceDisplayName,
                sameSpecies = sameSpecies
            });
            if (!TryReserveProcessing(tabooFact, out tabooLease, out tabooJournal, out string reservationFailure))
                throw new InvalidOperationException("Required taboo-butchery fact reservation failed: " + reservationFailure);
        }

        Vector2Int outputPosition = building != null ? building.centerPos : carcass.Position;
        var outputs = new[]
        {
            new PhysicalItemTransformOutput(DarkSurvivalItemDefinitions.HumanoidMeatItemId, 4, outputPosition),
            new PhysicalItemTransformOutput(DarkSurvivalItemDefinitions.BoneItemId, 2, outputPosition)
        };
        PhysicalItemTransformReceipt receipt = default;
        Exception physicalObserverFailure = null;
        bool transformed;
        string transformFailure;
        try
        {
            if (hasButcherActor)
            {
                freshnessBefore = freshnessByStackId.ToDictionary(
                    pair => pair.Key, pair => Clone(pair.Value), StringComparer.Ordinal);
                statsBefore = butcherActor.Stats.CaptureMoodDeliveryTransactionState();
                resolvedMood = butcherActor.ApplyMoodFactorBeforeOutcomeCommit(
                    "mood:butchery-guilt",
                    sameSpecies ? "동족의 사체를 손질함" : "인간형 사체를 손질함",
                    sameSpecies ? -16f : -9f, 900f, 1, out moodApplied);
                hygieneBefore = butcherActor.Stats.ChangeConditionBeforeOutcomeCommit(
                    CharacterCondition.HYGIENE, -18f);
                hygieneAfter = butcherActor.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0f);
                freshnessByStackId.Remove(carcass.StackId);
                InvalidateButcherCache();
                transformed = batchTransforms.TryTransformWholeStackWithCompanion(
                    carcass.StackId, outputs, $"humanoid-corpse-butcher:{carcass.StackId}",
                    "humanoid-corpse-butcher-loss", DomainFactEmission.Create(tabooLease, tabooFact), outcomeDay,
                    out receipt, out _, out transformFailure);
            }
            else
                transformed = physicalTransforms.TryTransformWholeStack(
                    carcass.StackId, outputs, $"humanoid-corpse-butcher:{carcass.StackId}",
                    "humanoid-corpse-butcher-loss", out receipt, out _, out transformFailure);
        }
        catch (Exception exception)
        {
            if (!receipt.IsCommitted)
            {
                Rollback(exception);
                throw;
            }
            // The transform exposes its committed receipt before post-commit marker callbacks.
            physicalObserverFailure = exception;
            transformed = true;
            transformFailure = string.Empty;
        }
        if (!transformed)
        {
            Rollback(new InvalidOperationException(transformFailure));
            message = transformFailure;
            return false;
        }
        // Applied effects survive pending delivery; recovery never repeats body or inventory mutations.
        if (hasButcherActor) ConfirmProcessing(tabooJournal, receipt);
        produced = receipt.OutputQuantity;
        freshnessByStackId.Remove(carcass.StackId);
        InvalidateButcherCache();
        var failures = new List<Exception>();
        if (physicalObserverFailure != null) failures.Add(physicalObserverFailure);
        if (hasButcherActor && !receipt.WasReplayed)
        {
            void Publish(Action action)
            {
                try { action(); }
                catch (Exception exception) when (exception is not OutOfMemoryException
                    && exception is not StackOverflowException && exception is not AccessViolationException)
                { failures.Add(exception); }
            }
            if (moodApplied)
                Publish(() => butcherActor.Stats.PublishCommittedMoodFactor(
                    "mood:butchery-guilt", resolvedMood, outcomeDay));
            Publish(() => butcherActor.Stats.PublishCommittedConditionChange(
                CharacterCondition.HYGIENE, hygieneBefore, hygieneAfter, outcomeDay));
            Publish(() => gameEventBus.Publish(
                new WildlifeCarcassProcessedEvent(butcher, building, butcherActor, tabooFact)));
            Publish(() => PublishHumanoidButcheryObserver(butcherActor, carcass, outputPosition));
        }
        if (failures.Count > 0)
            throw new AggregateException("Humanoid butchery committed; observer publication failed.", failures);
        message = produced > 0 ? "비상 도축 완료" : "도축 산출물 없음";
        return produced > 0;

        void Rollback(Exception original)
        {
            if (!hasButcherActor)
                return;
            var failures = new List<Exception>();
            void Restore(Action action)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            Restore(() => CancelProcessing(tabooLease, tabooJournal));
            if (freshnessBefore != null) freshnessByStackId = freshnessBefore;
            InvalidateButcherCache();
            if (statsBefore != null)
                Restore(() => butcherActor.Stats.RestoreMoodDeliveryTransactionState(statsBefore, publish: false));
            if (failures.Count > 0)
            {
                failures.Insert(0, original);
                throw new AggregateException("Humanoid butchery failed and rollback also failed.", failures);
            }
        }
    }

    private void PublishHumanoidButcheryObserver(
        CharacterActor butcher,
        WorldItemStackSnapshot carcass,
        Vector2Int outputPosition)
    {
        bool sameSpecies = string.Equals(
            butcher.SpeciesTag,
            carcass.SourceSpeciesTag,
            StringComparison.OrdinalIgnoreCase);
        string sourceName = string.IsNullOrWhiteSpace(carcass.SourceDisplayName)
            ? "이름 모를 자"
            : carcass.SourceDisplayName;
        gameEventBus.Publish(new CharacterTabooIncidentEvent<CharacterActor>(
            butcher,
            outputPosition,
            $"{sourceName}의 사체를 비상 도축했다",
            "인간형 사체의 비상 도축을 목격함",
            sameSpecies ? -10f : -7f));
    }

    private void RequireFactDependencies()
    {
        if (calendar == null || facts == null || physicalTransforms is not IPhysicalItemTransformRecoveryService)
        {
            throw new InvalidOperationException(
                "Wildlife carcass processing requires calendar, fact publication and physical recovery.");
        }
    }

    private bool TryReserveProcessing(WildlifeCarcassProcessedFact fact,
        out DomainFactLease lease, out WildlifeCarcassProcessingSaveData journal, out string failure)
    {
        lease = default;
        journal = null;
        var data = fact.Snapshot();
        if (processingByOperation.Count >= WildlifeSaveValidation.MaxCarcassProcessing
            || processingByOperation.ContainsKey(data.operationId) || processedStacks.Contains(data.carcassStackId))
        { failure = "Carcass processing identity is already present or capacity is exhausted."; return false; }
        if (!facts.TryReserve(fact, out lease, out failure)) return false;
        journal = new WildlifeCarcassProcessingSaveData { fact = data, publicationKey = lease.PublicationKey };
        try
        {
            journal.Validate();
            processingByOperation.Add(data.operationId, journal);
            processedStacks.Add(data.carcassStackId);
            InvalidateButcherCache();
        }
        catch { facts.Cancel(lease); throw; }
        return true;
    }

    private void CancelProcessing(DomainFactLease lease, WildlifeCarcassProcessingSaveData journal)
    {
        facts.Cancel(lease);
        if (journal == null) return;
        // Never erase a blocked child rollback or an ambiguously committed cohort.
        if (physicalTransforms is IPhysicalItemTransformRecoveryService recovery
            && recovery.HasTransformation(journal.fact.transformOperationId))
        { QueueProcessing(journal.fact.operationId); return; }
        processingByOperation.Remove(journal.fact.operationId);
        processedStacks.Remove(journal.fact.carcassStackId);
        InvalidateButcherCache();
    }

    private void ConfirmProcessing(WildlifeCarcassProcessingSaveData journal, PhysicalItemTransformReceipt receipt)
    {
        journal.publicationConfirmed = !receipt.PublicationPending;
        if (!journal.publicationConfirmed) QueueProcessing(journal.fact.operationId);
    }

    private void QueueProcessing(string operation)
    { if (queuedPublications.Add(operation)) pendingPublications.Enqueue(operation); }

    private void RebuildRecoveryQueue()
    {
        pendingPublications.Clear();
        queuedPublications.Clear();
        LastProcessingRecoveryFailure = string.Empty;
        foreach (var row in processingByOperation.Values)
            if (!row.publicationConfirmed) QueueProcessing(row.fact.operationId);
    }

    public void RecoverPendingProcessing()
    {
        if (pendingPublications.Count == 0) return;
        RequireFactDependencies();
        var recovery = (IPhysicalItemTransformRecoveryService)physicalTransforms;
        int count = Math.Min(8, pendingPublications.Count);
        string failure = string.Empty;
        for (int index = 0; index < count; index++)
        {
            string operation = pendingPublications.Dequeue();
            queuedPublications.Remove(operation);
            if (!processingByOperation.TryGetValue(operation, out var row) || row.publicationConfirmed) continue;
            bool applied = recovery.TryRecoverTransformation(row.fact.transformOperationId,
                DomainFactReplay.Create(row.publicationKey, new WildlifeCarcassProcessedFact(row.fact)),
                out var receipt, out string detail);
            if (applied && !receipt.PublicationPending) row.publicationConfirmed = true;
            else { QueueProcessing(operation); failure = detail; }
        }
        if (!string.IsNullOrEmpty(failure) && failure != LastProcessingRecoveryFailure)
            Debug.LogWarning("Carcass processing fact recovery pending: " + failure);
        LastProcessingRecoveryFailure = failure;
    }

    private static Dictionary<string, WildlifeCarcassProcessingSaveData> BuildProcessingState(
        IReadOnlyList<WildlifeCarcassProcessingSaveData> rows)
    {
        if (rows.Count > WildlifeSaveValidation.MaxCarcassProcessing)
            throw new InvalidOperationException("Carcass processing journal exceeds capacity.");
        var restored = new Dictionary<string, WildlifeCarcassProcessingSaveData>(StringComparer.Ordinal);
        var stacks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row == null) throw new InvalidOperationException("Missing carcass processing journal.");
            row.Validate();
            if (!restored.TryAdd(row.fact.operationId, row.Clone()) || !stacks.Add(row.fact.carcassStackId))
                throw new InvalidOperationException("Duplicate carcass processing journal.");
        }
        return restored;
    }

    private WorldItemStackSnapshot FindBestButcherCarcass()
    {
        if (cachedBestButcherCarcassVersion == itemStackRuntime.ItemStackVersion)
        {
            return cachedBestButcherCarcass;
        }

        cachedBestButcherCarcassVersion = itemStackRuntime.ItemStackVersion;
        cachedBestButcherCarcass = itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null
                && !processedStacks.Contains(stack.StackId)
                && !stack.Forbidden
                && stack.AvailableQuantity > 0
                && (WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(stack.ItemId, out _)
                    || (string.Equals(
                            stack.ItemId,
                            DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                            StringComparison.Ordinal)
                        && stack.EmergencyButcheryAllowed))
                && (stack.State == WorldItemStackState.Stored
                    || stack.State == WorldItemStackState.Loose
                    || stack.State == WorldItemStackState.FacilityBuffer))
            .OrderBy(stack => stack.State == WorldItemStackState.Stored ? 0 : 1)
            .ThenBy(stack => freshnessByStackId.TryGetValue(
                    stack.StackId,
                    out WildlifeCarcassFreshnessSaveData freshness)
                ? freshness.remainingFreshnessSeconds
                : DefaultFreshnessSeconds)
            .FirstOrDefault();
        return cachedBestButcherCarcass;
    }

    private void InvalidateButcherCache()
    {
        cachedBestButcherCarcass = null;
        cachedBestButcherCarcassVersion = -1;
    }

    private static WildlifeCarcassFreshnessSaveData Clone(
        WildlifeCarcassFreshnessSaveData source)
    {
        return new WildlifeCarcassFreshnessSaveData
        {
            stackId = source.stackId,
            speciesId = source.speciesId,
            remainingFreshnessSeconds = source.remainingFreshnessSeconds
        };
    }
}
