using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

internal static class V20ContentEffectEvidence
{
    internal static string CaptureEffectOwnerFingerprint(
        V20ContentEffectOwnerSnapshot snapshot)
    {
        snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        var parts = new List<string>
        {
            JsonUtility.ToJson(snapshot.Seasonal),
            JsonUtility.ToJson(snapshot.Society),
            JsonUtility.ToJson(snapshot.Factions),
            JsonUtility.ToJson(snapshot.Milestones),
            JsonUtility.ToJson(snapshot.Items),
            snapshot.MoneyBalance.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            JsonUtility.ToJson(snapshot.Careers),
            JsonUtility.ToJson(snapshot.BodyHealth),
            JsonUtility.ToJson(snapshot.Psychosocial),
            JsonUtility.ToJson(snapshot.PopulationHealth),
            JsonUtility.ToJson(snapshot.Narrative)
        };
        foreach (V20CharacterEffectOwnerSnapshot character in snapshot.Characters
                     .OrderBy(value => value.Actor.Identity?.PersistentId,
                         StringComparer.Ordinal))
        {
            parts.Add(character.Actor.Identity?.PersistentId ?? string.Empty);
            parts.Add(JsonUtility.ToJson(character.Stats));
            parts.Add(JsonUtility.ToJson(character.Progression));
            parts.Add(JsonUtility.ToJson(character.Social));
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(
            string.Join("\n", parts));
    }

    internal static IReadOnlyList<V20ResolvedEventResult>
        AttachApplicationEvidence(
            IReadOnlyList<V20ResolvedEventResult> resolutions,
            V20ContentEffectOwnerSnapshot before,
            V20ContentEffectOwnerSnapshot after,
            V20ContentEffectCommitReferences childCommits,
            V21ContentEffectExecutionRegistry handlers = null)
    {
        handlers ??= V21ContentEffectExecutionRegistry.Default;
        if (before == null) throw new ArgumentNullException(nameof(before));
        if (after == null) throw new ArgumentNullException(nameof(after));
        return (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .Select(resolution => resolution.WithApplications(
                CreateApplicationEvidence(
                    resolution,
                    before,
                    after,
                    childCommits, handlers)))
            .ToArray();
    }

    private static IReadOnlyList<V20EffectApplicationEvidence> CreateApplicationEvidence(
        V20ResolvedEventResult resolution, V20ContentEffectOwnerSnapshot before,
        V20ContentEffectOwnerSnapshot after, V20ContentEffectCommitReferences childCommits,
        V21ContentEffectExecutionRegistry handlers)
    {
        var result = new List<V20EffectApplicationEvidence>();
        string[] participants = resolution.ParticipantCharacterIds
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        for (int index = 0; index < resolution.Effects.Count; index++)
        {
            V20ContentEffect effect = resolution.Effects[index];
            if (effect == null || !effect.IsValid) continue;
            V20ContentEffectHandler handler = handlers.Require(effect.kind);
            string target = string.IsNullOrWhiteSpace(effect.targetId) ? "content-resolution" : effect.targetId;
            if (!handler.ParticipantTargeted && handler.ResolveTarget != null)
                target = handler.ResolveTarget(new(before, resolution, effect, target, participants),
                    new(after, resolution, effect, target, participants));
            foreach (string actualTarget in handler.ParticipantTargeted ? participants : new[] { target })
            {
                var beforeContext = new V20ContentEffectObservation(before, resolution, effect, actualTarget, participants);
                var afterContext = new V20ContentEffectObservation(after, resolution, effect, actualTarget, participants);
                bool beforeExists = !handler.ParticipantTargeted || HasCharacterOwner(before, actualTarget)
                    || handler.Stage == V20ContentEffectStage.Retirement && HasCareer(before, actualTarget);
                bool afterExists = !handler.ParticipantTargeted || HasCharacterOwner(after, actualTarget)
                    || handler.Stage == V20ContentEffectStage.Retirement && HasCareer(after, actualTarget);
                string beforeValue = beforeExists ? handler.Observe(beforeContext) : "unavailable";
                string afterValue = afterExists ? handler.Observe(afterContext) : "unavailable";
                float amount = handler.AppliedAmount(beforeContext, afterContext);
                bool changed = !string.Equals(beforeValue, afterValue, StringComparison.Ordinal);
                V20EffectApplicationStatus status = changed ? V20EffectApplicationStatus.Applied : V20EffectApplicationStatus.NoOp;
                if (handler.ParticipantTargeted)
                    status = !beforeExists || handler.SkipUnchanged && !changed ? V20EffectApplicationStatus.Skipped
                        : !Mathf.Approximately(amount, handler.ExpectedAmount(effect)) ? V20EffectApplicationStatus.Clamped : status;
                result.Add(new V20EffectApplicationEvidence
                {
                    effectIndex = index, targetId = actualTarget, status = status,
                    requestedAmount = effect.amount, appliedAmount = amount,
                    beforeValue = beforeValue, afterValue = afterValue, semantic = handler.Semantic,
                    childCommitId = childCommits?.ChildCommitId(effect.kind) ?? string.Empty
                });
            }
        }
        return result.AsReadOnly();
    }

    internal static string ResolveFactionEffectTarget(V20ResolvedEventResult resolution,
        V20ContentEffect effect, V20ContentEffectOwnerSnapshot before, V20ContentEffectOwnerSnapshot after)
    {
        string changed = (after.Factions?.factions ?? new List<FactionCampaignStateSaveData>())
            .Where(candidate => candidate != null).Select(candidate => candidate.factionId)
            .FirstOrDefault(id => !string.Equals(FactionValue(before, id, effect.kind),
                FactionValue(after, id, effect.kind), StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(changed)) return changed;
        if (!string.IsNullOrWhiteSpace(effect.targetId)) return effect.targetId;
        return string.IsNullOrWhiteSpace(resolution.ContextFactionId) ? "content-resolution" : resolution.ContextFactionId;
    }

    internal static string ResolveWorkDelayTarget(V20ResolvedEventResult resolution,
        V20ContentEffect effect, V20ContentEffectOwnerSnapshot before, V20ContentEffectOwnerSnapshot after)
    {
        string changed = (after.Society?.workDelays ?? new List<V20WorkDelaySaveData>())
            .Where(value => value != null).Select(value => value.scopeId)
            .FirstOrDefault(scope => !string.Equals(WorkDelayValue(before, scope), WorkDelayValue(after, scope), StringComparison.Ordinal));
        return !string.IsNullOrWhiteSpace(changed) ? changed
            : string.IsNullOrWhiteSpace(effect.targetId) ? "content-resolution" : effect.targetId;
    }

    private static bool HasCharacterOwner(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Characters?.Any(value =>
        value?.Actor?.Identity != null
        && string.Equals(
            value.Actor.Identity.PersistentId,
            characterId,
            StringComparison.Ordinal)) == true;

    private static bool HasCareer(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Careers?.characters?.Any(value =>
        value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal)) == true;

    private static V20CharacterEffectOwnerSnapshot CharacterOwner(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Characters?.FirstOrDefault(value =>
        value?.Actor?.Identity != null
        && string.Equals(
            value.Actor.Identity.PersistentId,
            characterId,
            StringComparison.Ordinal));

    internal static float MoodFactorValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId,
        V20ContentEffect effect)
    {
        string factorSuffix = ":" + effect.targetId;
        CharacterMoodMemory factor = CharacterOwner(snapshot, characterId)
            ?.Stats?.Factors?.FirstOrDefault(value => value != null
                && value.Id.StartsWith("content:", StringComparison.Ordinal)
                && value.Id.EndsWith(factorSuffix, StringComparison.Ordinal));
        return factor?.TotalValue ?? 0f;
    }

    internal static float HealthValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.BodyHealth?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.currentHealth ?? 0f;

    internal static float TraumaValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Psychosocial?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.trauma ?? 0f;

    internal static string ProgressionValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId)
    {
        CharacterProgressionSnapshot value = CharacterOwner(snapshot, characterId)
            ?.Progression;
        return value == null
            ? "unavailable"
            : "level=" + value.Level + ",xp=" + value.CurrentExperience;
    }

    internal static string RelationshipValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId,
        IReadOnlyList<string> participants)
    {
        CharacterSocialMemorySnapshot social = CharacterOwner(snapshot, characterId)
            ?.Social;
        if (social == null)
            return "unavailable";
        return string.Join(",", participants
            .Where(value => !string.Equals(
                value,
                characterId,
                StringComparison.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(other => other + "=" + FormatFloat(
                social.characterSentiments?
                    .FirstOrDefault(value => value != null && string.Equals(
                        value.key,
                        other,
                        StringComparison.Ordinal))?.value ?? 0f)));
    }

    internal static float ExposureValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId,
        string diseaseId) => snapshot?.PopulationHealth?.pendingExposures
        ?.FirstOrDefault(value => value != null
            && string.Equals(value.characterId, characterId, StringComparison.Ordinal)
            && string.Equals(value.diseaseId, diseaseId, StringComparison.Ordinal))
        ?.weightedExposureHours ?? 0f;

    internal static string DiseaseExposureValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId,
        string diseaseId)
    {
        bool infected = snapshot?.PopulationHealth?.characters
            ?.FirstOrDefault(value => value != null && string.Equals(
                value.characterId,
                characterId,
                StringComparison.Ordinal))?.activeDiseases?.Any(value =>
                value != null && string.Equals(
                    value.diseaseId,
                    diseaseId,
                    StringComparison.Ordinal)) == true;
        return "exposureHours="
            + FormatFloat(ExposureValue(snapshot, characterId, diseaseId))
            + ",infected=" + BoolValue(infected);
    }

    internal static int AmbitionProgress(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Narrative?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.ambitionProgress ?? 0;

    internal static string AmbitionValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId)
    {
        CharacterNarrativeSaveData value = snapshot?.Narrative?.characters
            ?.FirstOrDefault(candidate => candidate != null && string.Equals(
                candidate.characterId,
                characterId,
                StringComparison.Ordinal));
        return value == null
            ? "unavailable"
            : "id=" + (value.activeAmbitionId ?? string.Empty)
                + ",status=" + value.ambitionStatus
                + ",progress=" + value.ambitionProgress;
    }

    internal static string RetirementValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId)
    {
        CharacterCareerSaveData value = snapshot?.Careers?.characters
            ?.FirstOrDefault(candidate => candidate != null && string.Equals(
                candidate.characterId,
                characterId,
                StringComparison.Ordinal));
        return value == null
            ? "unavailable"
            : "status=" + value.retirementScheduleStatus
                + ",due=" + value.retirementDueAbsoluteDay
                + ",terminal=" + value.retirementTerminalAbsoluteDay
                + ",retired=" + BoolValue(value.retired);
    }

    internal static string FactionValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string factionId,
        V20ContentEffectKind kind)
    {
        FactionCampaignStateSaveData value = snapshot?.Factions?.factions
            ?.FirstOrDefault(candidate => candidate != null && string.Equals(
                candidate.factionId,
                factionId,
                StringComparison.Ordinal));
        if (value == null)
            return "unavailable";
        return (kind switch
        {
            V20ContentEffectKind.FactionRapport => value.rapport,
            V20ContentEffectKind.FactionGrievance => value.grievance,
            V20ContentEffectKind.FactionObligation => value.obligationTokens,
            _ => 0
        }).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static int ItemQuantity(
        V20ContentEffectOwnerSnapshot snapshot,
        string itemId) => snapshot?.Items?.stacks?
        .Where(value => value != null && string.Equals(
            value.itemId,
            itemId,
            StringComparison.Ordinal))
        .Sum(value => Math.Max(0, value.quantity)) ?? 0;

    internal static string WorkDelayValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string scopeId) => (snapshot?.Society?.workDelays
            ?.FirstOrDefault(value => value != null && string.Equals(
                value.scopeId,
                scopeId,
                StringComparison.Ordinal))?.untilAbsoluteDayExclusive ?? 0)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static string FormatFloat(float value) => value.ToString(
        "R",
        System.Globalization.CultureInfo.InvariantCulture);

    internal static string BoolValue(bool value) => value ? "true" : "false";

}
