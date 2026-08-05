using System;
using System.Linq;
using UnityEngine;

internal static class SurvivalHealthStateRules
{
    public static bool TryGetStatus(
        DungeonSurvivalSaveData state,
        CharacterActor actor,
        float outdoorTemperature,
        out SurvivalCharacterStatus status)
    {
        SurvivalFoodStatePersistence.EnsureLists(state);
        status = default;
        if (actor == null)
        {
            return false;
        }

        string persistentId = CharacterPersistentIdentity.Require(actor).Value;
        SurvivalHealthSaveData[] activeEntries = state.health
            .Where(entry => entry != null
                && entry.remainingSeconds > 0f
                && entry.state != SurvivalHealthState.Healthy
                && string.Equals(entry.persistentId, persistentId, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.state == SurvivalHealthState.Infected ? 3 : 0)
            .ThenByDescending(entry => entry.state == SurvivalHealthState.Sick ? 2 : 0)
            .ThenByDescending(entry => entry.state == SurvivalHealthState.Exposed ? 1 : 0)
            .ThenByDescending(entry => entry.severity)
            .ToArray();
        SurvivalHealthSaveData primary = activeEntries.FirstOrDefault();
        float distanceFromComfort = Mathf.Abs(outdoorTemperature - 20f);
        float temperatureComfort = Mathf.Clamp01(1f - (distanceFromComfort / 22f));
        status = new SurvivalCharacterStatus(
            hasStatus: primary != null
                || state.consecutiveWaterShortageDays > 0
                || state.consecutiveFoodShortageDays > 0,
            primaryState: primary?.state ?? SurvivalHealthState.Healthy,
            severity01: primary?.severity ?? 0f,
            remainingSeconds: primary?.remainingSeconds ?? 0f,
            source: primary?.source ?? string.Empty,
            activeIssueCount: activeEntries.Length,
            temperatureComfort01: temperatureComfort,
            waterSummary: state.consecutiveWaterShortageDays > 0
                ? $"물 부족 {state.consecutiveWaterShortageDays}일"
                : "물 정상",
            foodSummary: state.consecutiveFoodShortageDays > 0
                ? $"식량 부족 {state.consecutiveFoodShortageDays}일"
                : "식량 정상");
        return true;
    }

    public static bool HasTreatable(DungeonSurvivalSaveData state)
    {
        SurvivalFoodStatePersistence.EnsureLists(state);
        return state.health.Any(entry => entry != null
            && entry.remainingSeconds > 0f
            && entry.state is SurvivalHealthState.Sick
                or SurvivalHealthState.Infected
                or SurvivalHealthState.Exposed
                or SurvivalHealthState.Recovering);
    }

    public static SurvivalHealthSaveData FindTreatmentEntry(
        DungeonSurvivalSaveData state,
        bool useFirstActive)
    {
        SurvivalFoodStatePersistence.EnsureLists(state);
        return useFirstActive
            ? state.health.FirstOrDefault(IsActiveIssue)
            : state.health
                .Where(IsActiveIssue)
                .OrderByDescending(entry => entry.state == SurvivalHealthState.Infected ? 1 : 0)
                .ThenByDescending(entry => entry.severity)
                .FirstOrDefault();
    }

    public static void RegisterOrRefresh(
        DungeonSurvivalSaveData state,
        CharacterActor actor,
        SurvivalHealthState healthState,
        float severity,
        float durationSeconds,
        string source)
    {
        if (actor == null)
        {
            return;
        }

        SurvivalFoodStatePersistence.EnsureLists(state);
        string persistentId = CharacterPersistentIdentity.Require(actor).Value;
        SurvivalHealthSaveData entry = state.health.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.persistentId, persistentId, StringComparison.Ordinal)
            && candidate.state == healthState);
        if (entry == null)
        {
            state.health.Add(new SurvivalHealthSaveData
            {
                persistentId = persistentId,
                state = healthState,
                severity = Mathf.Clamp01(severity),
                remainingSeconds = Mathf.Max(1f, durationSeconds),
                source = source ?? string.Empty
            });
            return;
        }

        entry.severity = Mathf.Clamp01(Mathf.Max(entry.severity, severity));
        entry.remainingSeconds = Mathf.Max(entry.remainingSeconds, durationSeconds);
        entry.source = source ?? entry.source;
    }

    public static bool HasActive(
        DungeonSurvivalSaveData state,
        CharacterActor actor,
        SurvivalHealthState healthState)
    {
        string persistentId = actor?.Identity?.PersistentId;
        return !string.IsNullOrWhiteSpace(persistentId)
            && state.health.Any(entry => entry != null
                && entry.state == healthState
                && entry.remainingSeconds > 0f
                && string.Equals(entry.persistentId, persistentId, StringComparison.Ordinal));
    }

    private static bool IsActiveIssue(SurvivalHealthSaveData entry)
    {
        return entry != null
            && entry.remainingSeconds > 0f
            && entry.state != SurvivalHealthState.Healthy;
    }
}
