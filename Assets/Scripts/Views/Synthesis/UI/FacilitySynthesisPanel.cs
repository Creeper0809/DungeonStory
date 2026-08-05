using System.Collections.Generic;
using System.Linq;
using System;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using VContainer;

public class FacilitySynthesisPanel : MonoBehaviour
{
    [SerializeField] private FacilitySynthesisRuntime runtime;
    [SerializeField] private TMP_Text summaryText;
    private FacilityFeatureSceneRuntimeReferences runtimeReferences;
    private IGameEventBus gameEventBus;
    private IDisposable researchCompletedSubscription;

    public string LastRenderedText { get; private set; } = string.Empty;

    [Inject]
    public void Construct(
        FacilityFeatureSceneRuntimeReferences runtimeReferences,
        IGameEventBus gameEventBus)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new System.ArgumentNullException(nameof(runtimeReferences));
        this.gameEventBus = gameEventBus
            ?? throw new System.ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

    public void Bind(FacilitySynthesisRuntime nextRuntime)
    {
        UnsubscribeFromRuntime();
        runtime = nextRuntime;
        SubscribeToRuntime();
        Refresh();
    }

    internal void BindGeneratedView(TMP_Text summaryText)
    {
        this.summaryText = summaryText
            ?? throw new System.ArgumentNullException(nameof(summaryText));
        ApplyText();
    }

    public void Refresh()
    {
        FacilitySynthesisRuntime activeRuntime = ResolveRuntime();
        List<string> lines = new List<string>
        {
            "시설 합성",
            string.Empty,
            "선택 재료:"
        };

        IReadOnlyList<BuildableObject> selected = activeRuntime.SelectedMaterials;
        if (selected == null || selected.Count == 0)
        {
            lines.Add("- 없음");
        }
        else
        {
            lines.AddRange(selected
                .Where((building) => building != null)
                .Select((building) => $"- {FacilityShopService.GetBuildingName(building.BuildingData)} Lv.{building.FacilityLevel}"));
        }

        lines.Add(string.Empty);
        lines.Add("조합식:");
        IReadOnlyList<FacilitySynthesisRecipeSO> recipes = activeRuntime.VisibleRecipes;
        if (recipes == null || recipes.Count == 0)
        {
            lines.Add("- 없음");
        }
        else
        {
            lines.AddRange(recipes.Select((recipe) =>
            {
                FacilitySynthesisRecipeSnapshot snapshot = activeRuntime.ToSnapshot(recipe);
                return snapshot != null ? $"- {snapshot.ToSummaryText()}" : "- 조합식 오류";
            }));
        }

        LastRenderedText = string.Join("\n", lines);
        ApplyText();
    }

    public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)
    {
        Refresh();
    }

    public void OnTriggerEvent(FacilitySynthesisCompletedEvent eventType)
    {
        Refresh();
    }

    private void ApplyText()
    {
        if (summaryText != null)
        {
            summaryText.text = LastRenderedText;
        }
    }

    private FacilitySynthesisRuntime ResolveRuntime()
    {
        if (runtime != null) return runtime;

        return (runtimeReferences
                ?? throw new System.InvalidOperationException($"{nameof(FacilitySynthesisPanel)} requires {nameof(FacilityFeatureSceneRuntimeReferences)} injection or an explicit runtime binding."))
            .Synthesis
            ?? throw new System.InvalidOperationException(
                $"{nameof(FacilitySynthesisPanel)} requires a loaded {nameof(FacilitySynthesisRuntime)}.");
    }

    private void OnEnable()
    {
        SubscribeToRuntime();
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromRuntime();
        researchCompletedSubscription?.Dispose();
        researchCompletedSubscription = null;
    }

    private void SubscribeToRuntime()
    {
        if (runtime != null)
        {
            runtime.SelectionChanged -= Refresh;
            runtime.SelectionChanged += Refresh;
            runtime.Completed -= OnSynthesisCompleted;
            runtime.Completed += OnSynthesisCompleted;
        }
    }

    private void UnsubscribeFromRuntime()
    {
        if (runtime != null)
        {
            runtime.SelectionChanged -= Refresh;
            runtime.Completed -= OnSynthesisCompleted;
        }
    }

    private void OnSynthesisCompleted(FacilitySynthesisResult result)
    {
        OnTriggerEvent(new FacilitySynthesisCompletedEvent(result));
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || researchCompletedSubscription != null || gameEventBus == null)
        {
            return;
        }

        researchCompletedSubscription =
            gameEventBus.Subscribe<BlueprintResearchCompletedEvent>(OnTriggerEvent);
    }
}
