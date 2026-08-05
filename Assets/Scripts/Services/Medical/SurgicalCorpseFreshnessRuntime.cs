using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SurgicalCorpseFreshnessRuntime :
    ISurgicalCorpseFreshnessRuntime,
    ITickable
{
    private const float SecondsPerDay = 180f;
    private const float HumanoidCorpseFreshnessSeconds = SecondsPerDay * 2f;

    private readonly IWorldItemStackRuntime items;
    private readonly IWildlifeCarcassService wildlifeCarcasses;
    private readonly IGameClock clock;
    private readonly SurgeryAggregateStateStore stateStore;
    private int observedItemVersion = -1;

    private Dictionary<string, SurgicalCorpseFreshnessState> humanoidCorpses =>
        stateStore.State.CorpseFreshness;

    public SurgicalCorpseFreshnessRuntime(
        IWorldItemStackRuntime items,
        IWildlifeCarcassService wildlifeCarcasses,
        IGameClock clock,
        SurgeryAggregateStateStore stateStore)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.wildlifeCarcasses = wildlifeCarcasses
            ?? throw new ArgumentNullException(nameof(wildlifeCarcasses));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public void Tick()
    {
        RefreshHumanoidCorpseIndex();
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        foreach (SurgicalCorpseFreshnessState state in humanoidCorpses.Values)
        {
            state.remainingFreshnessSeconds = Mathf.Max(
                0f,
                state.remainingFreshnessSeconds - clock.DeltaTime);
        }
    }

    public bool TryGetFreshness(
        string stackId,
        out float remainingFreshnessSeconds,
        out bool isFresh)
    {
        remainingFreshnessSeconds = 0f;
        isFresh = false;
        if (string.IsNullOrWhiteSpace(stackId))
        {
            return false;
        }

        string normalized = stackId.Trim();
        RefreshHumanoidCorpseIndex();
        if (humanoidCorpses.TryGetValue(
                normalized,
                out SurgicalCorpseFreshnessState humanoid))
        {
            remainingFreshnessSeconds = Mathf.Max(
                0f,
                humanoid.remainingFreshnessSeconds);
            isFresh = remainingFreshnessSeconds > 0f;
            return true;
        }

        if (!wildlifeCarcasses.TryGetFreshness(
                normalized,
                out remainingFreshnessSeconds))
        {
            return false;
        }

        isFresh = remainingFreshnessSeconds > 0f;
        return true;
    }

    public IReadOnlyList<SurgicalCorpseFreshnessState> Capture()
    {
        RefreshHumanoidCorpseIndex();
        return humanoidCorpses.Values
            .OrderBy(state => state.stackId, StringComparer.Ordinal)
            .Select(state => state.Clone())
            .ToArray();
    }

    private void RefreshHumanoidCorpseIndex()
    {
        if (observedItemVersion == items.ItemStackVersion)
        {
            return;
        }

        observedItemVersion = items.ItemStackVersion;
        IReadOnlyList<WorldItemStackSnapshot> current = items.GetAllStacks();
        HashSet<string> liveIds = new(
            current
                .Where(IsHumanoidCorpse)
                .Select(stack => stack.StackId),
            StringComparer.Ordinal);

        foreach (string staleId in humanoidCorpses.Keys
                     .Where(id => !liveIds.Contains(id))
                     .ToArray())
        {
            humanoidCorpses.Remove(staleId);
        }

        foreach (WorldItemStackSnapshot stack in current.Where(IsHumanoidCorpse))
        {
            if (!humanoidCorpses.ContainsKey(stack.StackId))
            {
                humanoidCorpses.Add(
                    stack.StackId,
                    new SurgicalCorpseFreshnessState
                    {
                        stackId = stack.StackId,
                        remainingFreshnessSeconds =
                            HumanoidCorpseFreshnessSeconds
                    });
            }
        }
    }

    private static bool IsHumanoidCorpse(WorldItemStackSnapshot stack)
    {
        return stack != null
            && !string.IsNullOrWhiteSpace(stack.StackId)
            && string.Equals(
                stack.ItemId,
                DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                StringComparison.Ordinal);
    }
}
