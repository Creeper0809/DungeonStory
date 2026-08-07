using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public readonly struct V20ContentEffectsResolvedEvent
{
    public V20ContentEffectsResolvedEvent(
        string definitionId,
        string resolutionId,
        IReadOnlyList<V20ContentEffect> effects,
        bool physicalEffectsApplied)
    {
        DefinitionId = definitionId ?? string.Empty;
        ResolutionId = resolutionId ?? string.Empty;
        Effects = effects ?? Array.Empty<V20ContentEffect>();
        PhysicalEffectsApplied = physicalEffectsApplied;
    }

    public string DefinitionId { get; }
    public string ResolutionId { get; }
    public IReadOnlyList<V20ContentEffect> Effects { get; }
    public bool PhysicalEffectsApplied { get; }
}

public sealed class V20CampaignApplicationAdapter : IStartable, IDisposable
{
    private readonly ISocietyEventCommand society;
    private readonly IRunMilestoneCommand milestones;
    private readonly IRunMilestoneQuery milestoneQuery;
    private readonly IEndlessCrisisCommand endless;
    private readonly IGameCalendar calendar;
    private readonly IRunSeedProvider runSeed;
    private readonly IGameEventBus events;
    private readonly V20MilestoneWorldSnapshotProjector milestoneProjector;
    private readonly IStockQuery stock;
    private readonly IGameMoneyAccount money;
    private readonly IWorldItemStackRuntime items;
    private readonly IItemTransferService transfers;
    private readonly IWorldDropZoneQuery dropZones;
    private IDisposable dayStartedSubscription;

    public V20CampaignApplicationAdapter(
        ISocietyEventCommand society,
        IRunMilestoneCommand milestones,
        IRunMilestoneQuery milestoneQuery,
        IEndlessCrisisCommand endless,
        IGameCalendar calendar,
        IRunSeedProvider runSeed,
        IGameEventBus events,
        V20MilestoneWorldSnapshotProjector milestoneProjector,
        IStockQuery stock,
        IGameMoneyAccount money,
        IWorldItemStackRuntime items,
        IItemTransferService transfers,
        IWorldDropZoneQuery dropZones)
    {
        this.society = society ?? throw new ArgumentNullException(nameof(society));
        this.milestones = milestones ?? throw new ArgumentNullException(nameof(milestones));
        this.milestoneQuery = milestoneQuery ?? throw new ArgumentNullException(nameof(milestoneQuery));
        this.endless = endless ?? throw new ArgumentNullException(nameof(endless));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.runSeed = runSeed ?? throw new ArgumentNullException(nameof(runSeed));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.milestoneProjector = milestoneProjector
            ?? throw new ArgumentNullException(nameof(milestoneProjector));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.dropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
    }

    public void Start()
    {
        dayStartedSubscription ??= events.Subscribe<OperatingDayStartedEvent>(
            OnDayStarted);
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
    }

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        RunMilestoneEvaluationSnapshot snapshot =
            milestoneProjector.Build(Math.Max(1, started.day));
        V20DailyEventContext context = new()
        {
            AbsoluteDay = Math.Max(1, started.day),
            RunSeed = runSeed.RunSeed,
            Season = GameCalendarRules.Project(Math.Max(1, started.day), 0).Season,
            Generation = Mathf.Max(
                0,
                Mathf.FloorToInt(snapshot.WorldMetrics.TryGetValue(
                    V20WorldMetricKind.CompletedGenerations,
                    out float generations)
                        ? generations
                        : 0f))
        };
        CopySnapshot(snapshot, context.Requirements);
        foreach (CharacterActor actor in milestoneProjector.LivingCharacters)
            context.ParticipantCharacterIds.Add(actor.Identity.PersistentId);

        foreach (V20ResolvedEventResult result in society.EvaluateDaily(context))
            PublishApplied(result);
        foreach (string milestoneId in milestones.Evaluate(snapshot))
        {
            events.Publish(new V20ContentEffectsResolvedEvent(
                milestoneId,
                "completed",
                Array.Empty<V20ContentEffect>(),
                physicalEffectsApplied: true));
        }
        if (milestoneQuery.Phase == RunProgressionPhase.EndlessAge
            && started.day % 10 == 0)
        {
            endless.ComposeNextEndlessCrisis(started.day, runSeed.RunSeed);
        }
    }

    private void PublishApplied(V20ResolvedEventResult result)
    {
        bool applied = TryApplyPhysicalEffects(result);
        events.Publish(new V20ContentEffectsResolvedEvent(
            result.DefinitionId,
            result.ResolutionId,
            result.Effects,
            applied));
    }

    private bool TryApplyPhysicalEffects(V20ResolvedEventResult result)
    {
        int moneyDelta = result.Effects
            .Where(value => value?.kind == V20ContentEffectKind.Money)
            .Sum(value => Mathf.RoundToInt(value.amount));
        Dictionary<string, int> consumes = result.Effects
            .Where(value => value?.kind == V20ContentEffectKind.ItemConsume)
            .GroupBy(value => value.targetId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => Math.Max(0, Mathf.RoundToInt(value.amount))),
                StringComparer.Ordinal);
        V20ContentEffect[] grants = result.Effects
            .Where(value => value?.kind == V20ContentEffectKind.ItemGrant)
            .ToArray();
        if (moneyDelta < 0 && !money.CanSpend(-moneyDelta)) return false;
        if (consumes.Any(pair => stock.GetGlobalQuantity(pair.Key) < pair.Value))
            return false;
        if (consumes.Keys.Concat(grants.Select(value => value.targetId))
            .Any(itemId => !items.CatalogProvider.TryGetDefinition(itemId, out _)))
            return false;
        Vector2Int dropoff = default;
        if (grants.Length > 0
            && !dropZones.TryGetDeliveryDropoff(out dropoff))
            return false;

        EconomyTransactionContext transaction = new(
            moneyDelta >= 0
                ? EconomyTransactionKind.ContractIncome
                : EconomyTransactionKind.LegacyExpense,
            $"v20:{result.DefinitionId}",
            description: result.ResolutionId);
        if (moneyDelta > 0) money.Add(moneyDelta, transaction);
        else if (moneyDelta < 0 && !money.TrySpend(-moneyDelta, transaction, out _))
            return false;

        foreach (KeyValuePair<string, int> consume in consumes)
        {
            int remaining = consume.Value;
            foreach (WorldItemStackSnapshot stack in stock.GetAllStacks()
                         .Where(value => string.Equals(
                             value.ItemId,
                             consume.Key,
                             StringComparison.Ordinal))
                         .OrderBy(value => value.StackId, StringComparer.Ordinal))
            {
                int quantity = Math.Min(remaining, stack.Quantity);
                if (quantity <= 0) continue;
                if (!transfers.TryConsumeStackQuantity(
                        (ItemStackId)stack.StackId,
                        quantity,
                        out _,
                        out _))
                    return false;
                remaining -= quantity;
                if (remaining == 0) break;
            }
            if (remaining != 0) return false;
        }

        if (grants.Length == 0) return true;
        foreach (V20ContentEffect grant in grants)
        {
            int amount = Math.Max(0, Mathf.RoundToInt(grant.amount));
            if (amount > 0 && (!items.SpawnItemAt(
                    grant.targetId,
                    amount,
                    dropoff,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                || spawned != amount))
                return false;
        }
        return true;
    }

    private static void CopySnapshot(
        RunMilestoneEvaluationSnapshot source,
        RunMilestoneEvaluationSnapshot destination)
    {
        foreach (int id in source.CompletedResearchIds)
            destination.CompletedResearchIds.Add(id);
        foreach (string flag in source.WorldFlags)
            destination.WorldFlags.Add(flag);
        foreach (KeyValuePair<V20WorldMetricKind, float> pair in source.WorldMetrics)
            destination.WorldMetrics[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, int> pair in source.ItemQuantities)
            destination.ItemQuantities[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, int> pair in source.FacilityCounts)
            destination.FacilityCounts[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, FactionCampaignStateSaveData> pair in source.Factions)
            destination.Factions[pair.Key] = pair.Value;
        destination.EligibleCharacterCount = source.EligibleCharacterCount;
    }
}
