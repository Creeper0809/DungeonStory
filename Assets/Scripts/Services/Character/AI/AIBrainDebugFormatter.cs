using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class AIBrainDebugSnapshot
{
    public CharacterActor Actor { get; set; }
    public AIAction BestAction { get; set; }
    public string CurrentActionLabel { get; set; }
    public string CurrentPhase { get; set; }
    public string CurrentPhaseDetail { get; set; }
    public string CurrentDestinationLabel { get; set; }
    public float ActionSwitchRemaining { get; set; }
    public AIActionFailure LastFailure { get; set; }
    public AIActionSet LastFailedAction { get; set; }
    public IReadOnlyList<AIActionDebugCandidate> Candidates { get; set; }
    public int CandidateLimit { get; set; }
    public int DebugVersion { get; set; }
}

internal static class AIBrainDebugFormatter
{
    public static string Format(AIBrainDebugSnapshot snapshot, int candidateCount)
    {
        AIAction bestAction = snapshot.BestAction;
        string actionLabel = bestAction?.actionset != null
            ? GetActionLabel(bestAction.actionset)
            : snapshot.CurrentActionLabel;
        string runningLabel = bestAction?.HasStarted == true
            ? $" ({bestAction.RunningSeconds:0.0}s)"
            : string.Empty;
        string reservationLabel = bestAction?.HasReservation == true
            ? $"\n예약: {GetDestinationLabel(bestAction.ReservedDestination)}"
            : string.Empty;
        string phaseLabel = string.IsNullOrWhiteSpace(snapshot.CurrentPhase)
            ? "없음"
            : snapshot.CurrentPhase;
        if (!string.IsNullOrWhiteSpace(snapshot.CurrentPhaseDetail))
        {
            phaseLabel += $" / {snapshot.CurrentPhaseDetail}";
        }

        string destinationLabel = bestAction?.destination != null
            ? GetDestinationLabel(bestAction.destination)
            : string.IsNullOrWhiteSpace(snapshot.CurrentDestinationLabel)
                ? "없음"
                : snapshot.CurrentDestinationLabel;
        string switchLabel = snapshot.ActionSwitchRemaining > 0f
            ? $"\n전환완충: {snapshot.ActionSwitchRemaining:0.0}s"
            : string.Empty;
        string failureLabel = snapshot.LastFailure.HasFailure
            ? snapshot.LastFailure.ToString()
            : "정상";
        string failedActionLabel = snapshot.LastFailedAction != null
            ? GetActionLabel(snapshot.LastFailedAction)
            : string.Empty;
        string reason = string.IsNullOrWhiteSpace(failedActionLabel)
            ? failureLabel
            : $"{failedActionLabel}: {failureLabel}";

        IEnumerable<AIActionDebugCandidate> candidates =
            (snapshot.Candidates ?? System.Array.Empty<AIActionDebugCandidate>())
            .OrderByDescending(candidate => candidate.Score)
            .Take(Mathf.Clamp(candidateCount, 1, snapshot.CandidateLimit));
        string candidateText = string.Join(
            ", ",
            candidates.Select(candidate => candidate.Failure.HasFailure
                ? $"{candidate.ActionLabel} {candidate.Score:0.00}({candidate.Failure.Kind})"
                : $"{candidate.ActionLabel} {candidate.Score:0.00}"));

        string baseText =
            $"행동: {actionLabel}{runningLabel}"
            + $"\n단계: {phaseLabel}"
            + $"\n목표: {destinationLabel}"
            + $"\n경로: {GetPathLabel(bestAction)}"
            + reservationLabel
            + switchLabel
            + $"\n기분: {GetMoodLabel(snapshot.Actor)}"
            + $"\n이유: {reason}"
            + GetHaulLabel(snapshot.Actor)
            + GetConstructionSafetyLabel(snapshot.Actor, bestAction);
        return string.IsNullOrWhiteSpace(candidateText)
            ? baseText
            : $"{baseText}\n후보: {candidateText}";
    }

    public static int GetHash(AIBrainDebugSnapshot snapshot)
    {
        unchecked
        {
            int hash = snapshot.DebugVersion;
            hash = (hash * 31) + (snapshot.BestAction?.actionset != null
                ? snapshot.BestAction.actionset.GetInstanceID()
                : 0);
            hash = (hash * 31) + (int)snapshot.LastFailure.Kind;
            foreach (AIActionDebugCandidate candidate in
                (snapshot.Candidates ?? System.Array.Empty<AIActionDebugCandidate>())
                .Take(snapshot.CandidateLimit))
            {
                hash = (hash * 31) + candidate.ActionLabel.GetHashCode();
                hash = (hash * 31) + Mathf.RoundToInt(candidate.Score * 1000f);
                hash = (hash * 31) + (int)candidate.Failure.Kind;
            }

            return hash;
        }
    }

    public static string GetDestinationLabel(BuildableObject destination)
    {
        if (destination == null)
        {
            return "없음";
        }

        return destination.BuildingData != null
            && !string.IsNullOrWhiteSpace(destination.BuildingData.objectName)
                ? destination.BuildingData.objectName
                : destination.name;
    }

    public static string GetPathLabel(AIAction action)
    {
        return action == null
            ? "경로 없음"
            : $"{action.planKind} / {action.pathSteps.Count}칸";
    }

    private static string GetHaulLabel(CharacterActor actor)
    {
        AbilityHaul haul = actor != null ? actor.GetComponent<AbilityHaul>() : null;
        if (haul == null)
        {
            return string.Empty;
        }

        CharacterCarryInventory carry = actor.GetComponent<CharacterCarryInventory>();
        IWorldItemStackRuntime itemRuntime = actor.WorldItemStackRuntime;
        IDungeonItemCatalogProvider catalogProvider =
            itemRuntime?.CatalogProvider;
        float currentWeight = carry != null && catalogProvider != null
            ? carry.GetCurrentWeight(catalogProvider)
            : 0f;
        IItemHaulingSettingsProvider haulingSettings =
            itemRuntime?.HaulingSettingsProvider;
        float maxWeight = carry != null && haulingSettings != null
            ? carry.GetMaxAllowedWeight(haulingSettings)
            : 0f;
        return $"\n운반 계획: {haul.CurrentPlanSummary}"
            + $"\n적재: {currentWeight:0.#}/{maxWeight:0.#}kg"
            + $"\n정리 사유: {haul.CurrentUnloadReason}";
    }

    private static string GetConstructionSafetyLabel(
        CharacterActor actor,
        AIAction bestAction)
    {
        ConstructionSite site = bestAction?.destination as ConstructionSite;
        if (site == null)
        {
            return string.Empty;
        }

        ConstructionSafetyResult safety = site.LastSafetyResult.Message.Length > 0
            ? site.LastSafetyResult
            : site.GetConstructionSafetyState(actor?.BuildingVisitor);
        string prefix = safety.IsForcedWarning
            ? "강제 공사"
            : safety.IsSafe ? "공사 안전" : "공사 대기";
        return $"\n{prefix}: {safety.Message}";
    }

    private static string GetMoodLabel(CharacterActor actor)
    {
        return actor?.Stats == null ? "없음" : $"{actor.Stats.Mood:0.#}";
    }

    private static string GetActionLabel(AIActionSet actionSet)
    {
        return actionSet == null ? "행동 없음" : actionSet.GetDisplayLabel();
    }
}
