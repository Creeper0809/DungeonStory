using System.Collections.Generic;
using System.Linq;
using System;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using VContainer;

public class FacilityEvolutionPanel :
    MonoBehaviour
{
    [SerializeField] private FacilityEvolutionRuntime runtime;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private bool showRejectedHints = true;

    private IFacilityEvolutionRuntimeProvider runtimeProvider;
    private IGameEventBus gameEventBus;
    private IDisposable researchCompletedSubscription;
    private IDisposable infoFeedSubscription;
    private BuildableObject selectedFacility;

    public string LastRenderedText { get; private set; } = string.Empty;
    public BuildableObject SelectedFacility => selectedFacility;

    [Inject]
    public void Construct(
        IFacilityEvolutionRuntimeProvider runtimeProvider,
        IGameEventBus gameEventBus)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new System.ArgumentNullException(nameof(runtimeProvider));
        this.gameEventBus = gameEventBus
            ?? throw new System.ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
        SubscribeToRuntime();
    }

    public void Bind(FacilityEvolutionRuntime nextRuntime)
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

    public void SelectFacility(BuildableObject facility)
    {
        selectedFacility = facility;
        Refresh();
    }

    public bool TryEvolve(string evolutionId, out FacilityEvolutionResult result)
    {
        result = default;
        FacilityEvolutionRuntime activeRuntime = ResolveRuntime();
        if (selectedFacility == null || string.IsNullOrWhiteSpace(evolutionId))
        {
            return false;
        }

        FacilityEvolutionCandidate candidate = activeRuntime
            .GetCandidates(selectedFacility, includeRejected: false)
            .FirstOrDefault((item) => item.Recipe != null && item.Recipe.EffectiveId == evolutionId);
        if (candidate == null || !candidate.Approved)
        {
            return false;
        }

        bool success = activeRuntime.TryEvolve(selectedFacility, candidate.Recipe, out result);
        if (success)
        {
            selectedFacility = result.ResultBuilding;
            Refresh();
        }

        return success;
    }

    public bool TryEvolveFirstApproved(out FacilityEvolutionResult result)
    {
        result = default;
        FacilityEvolutionRuntime activeRuntime = ResolveRuntime();
        if (selectedFacility == null)
        {
            return false;
        }

        FacilityEvolutionCandidate first = activeRuntime
            .GetCandidates(selectedFacility, includeRejected: false)
            .FirstOrDefault();
        return first != null && TryEvolve(first.Recipe.EffectiveId, out result);
    }

    public void Refresh()
    {
        FacilityEvolutionRuntime activeRuntime = ResolveRuntime();
        if (selectedFacility == null || selectedFacility.isDestroy)
        {
            LastRenderedText = "시설 진화\n선택 시설 없음";
            ApplyText();
            return;
        }

        FacilityEvolutionContext context = activeRuntime.BuildContext(selectedFacility);
        RoomProfile profile = context.Profile;
        IReadOnlyList<FacilityEvolutionCandidate> candidates =
            activeRuntime.GetCandidates(selectedFacility, includeRejected: showRejectedHints);
        List<string> lines = new List<string>
        {
            "시설 진화",
            string.Empty,
            $"{FacilityShopService.GetBuildingName(selectedFacility.BuildingData)} / {context.State.StarGrade}성",
            $"계보: {FormatList(context.State.LineageTags)}",
            $"변이: {FormatList(context.State.MutationTags)}",
            $"방: {(profile.IsUsable ? "사용 가능" : "조건 미달")}",
            $"좌석 밀도: {profile.GetMetric(FacilityEvolutionTerms.SeatDensity):0.##}",
            $"좌석당 장식: {profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat):0.##}",
            $"정체성: {FormatPressures(profile.IdentityPressures)}",
            string.Empty,
            "진화 후보:"
        };

        FacilityEvolutionCandidate firstCandidate = candidates?
            .FirstOrDefault((candidate) => candidate != null);
        if (firstCandidate != null && !string.IsNullOrWhiteSpace(firstCandidate.ProposalSource))
        {
            lines.Add($"해석: {firstCandidate.ProposalSource}");
            if (!string.IsNullOrWhiteSpace(firstCandidate.ProposalStatusMessage))
            {
                lines.Add($"상태: {firstCandidate.ProposalStatusMessage}");
            }

            if (!string.IsNullOrWhiteSpace(firstCandidate.FlavorText))
            {
                lines.Add($"서사: {firstCandidate.FlavorText}");
            }
        }

        if (candidates == null || candidates.Count == 0)
        {
            lines.Add("- 없음");
        }
        else
        {
            foreach (FacilityEvolutionCandidate candidate in candidates)
            {
                if (candidate?.Recipe == null)
                {
                    continue;
                }

                string state = candidate.Approved ? "가능" : "영감";
                string resultName = FacilityShopService.GetBuildingName(candidate.Recipe.resultBuilding);
                lines.Add($"- [{state}] {candidate.Recipe.DisplayName} -> {resultName}");
                if (candidate.IdentityScore.UsesIdentityPressure)
                {
                    lines.Add($"  {candidate.IdentityScore.ToMessage()}");
                }

                foreach (string checkLine in FormatChecks(candidate.Validation))
                {
                    lines.Add($"  {checkLine}");
                }

                if (!candidate.Approved && !string.IsNullOrWhiteSpace(candidate.RejectedHintText))
                {
                    lines.Add($"  흐릿한 영감: {candidate.RejectedHintText}");
                }
                else if (!string.IsNullOrWhiteSpace(candidate.Reason))
                {
                    lines.Add($"  {candidate.Reason}");
                }
                else if (!candidate.Approved && candidate.Validation != null)
                {
                    lines.Add($"  {candidate.Validation.ToMessage()}");
                }
            }
        }

        LastRenderedText = string.Join("\n", lines);
        ApplyText();
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        if (eventType.Target is BuildingInfoTarget target)
        {
            SelectFacility(target.Building);
        }
    }

    public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)
    {
        Refresh();
    }

    public void OnTriggerEvent(FacilityEvolutionCompletedEvent eventType)
    {
        if (eventType.result.Success)
        {
            selectedFacility = eventType.result.ResultBuilding;
        }

        Refresh();
    }

    private void ApplyText()
    {
        if (summaryText != null)
        {
            summaryText.text = LastRenderedText;
        }
    }

    private static string FormatList(IEnumerable<string> values)
    {
        string[] filtered = values?
            .Where((value) => !string.IsNullOrWhiteSpace(value))
            .ToArray()
            ?? System.Array.Empty<string>();
        return filtered.Length > 0 ? string.Join(", ", filtered) : "없음";
    }

    private static IEnumerable<string> FormatChecks(FacilityEvolutionValidationResult validation)
    {
        if (validation?.Checks == null || validation.Checks.Count == 0)
        {
            yield break;
        }

        foreach (FacilityEvolutionValidationCheck check in validation.Checks.Take(8))
        {
            string state = check.Passed ? "충족" : "미충족";
            string label = string.IsNullOrWhiteSpace(check.Category)
                ? check.Label
                : $"{check.Category}/{check.Label}";
            string detail = !check.Passed && !string.IsNullOrWhiteSpace(check.Detail)
                ? $" - {check.Detail}"
                : string.Empty;
            yield return $"[{state}] {label}{detail}";
        }
    }

    private static string FormatPressures(IReadOnlyDictionary<string, float> values)
    {
        if (values == null || values.Count == 0)
        {
            return "없음";
        }

        string[] filtered = values
            .Where((entry) => entry.Value >= 0.2f)
            .OrderByDescending((entry) => entry.Value)
            .Take(4)
            .Select((entry) => $"{entry.Key} {entry.Value:0.##}")
            .ToArray();
        return filtered.Length > 0 ? string.Join(", ", filtered) : "낮음";
    }

    private FacilityEvolutionRuntime ResolveRuntime()
    {
        if (runtime != null) return runtime;

        return (runtimeProvider
                ?? throw new System.InvalidOperationException($"{nameof(FacilityEvolutionPanel)} requires {nameof(IFacilityEvolutionRuntimeProvider)} injection or an explicit runtime binding."))
            .Runtime;
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
        infoFeedSubscription?.Dispose();
        infoFeedSubscription = null;
    }

    private void SubscribeToRuntime()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        runtime ??= runtimeProvider?.Runtime;
        if (runtime != null)
        {
            runtime.Completed -= OnEvolutionCompleted;
            runtime.Completed += OnEvolutionCompleted;
        }
    }

    private void UnsubscribeFromRuntime()
    {
        if (runtime != null)
        {
            runtime.Completed -= OnEvolutionCompleted;
        }
    }

    private void OnEvolutionCompleted(FacilityEvolutionResult result)
    {
        OnTriggerEvent(new FacilityEvolutionCompletedEvent(result));
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        researchCompletedSubscription ??=
            gameEventBus.Subscribe<BlueprintResearchCompletedEvent>(OnTriggerEvent);
        infoFeedSubscription ??=
            gameEventBus.Subscribe<InfoFeedEvent>(OnTriggerEvent);
    }
}
