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
            V20ContentEffectCommitReferences childCommits)
    {
        if (before == null) throw new ArgumentNullException(nameof(before));
        if (after == null) throw new ArgumentNullException(nameof(after));
        return (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .Select(resolution => resolution.WithApplications(
                CreateApplicationEvidence(
                    resolution,
                    before,
                    after,
                    childCommits)))
            .ToArray();
    }

    private static IReadOnlyList<V20EffectApplicationEvidence>
        CreateApplicationEvidence(
            V20ResolvedEventResult resolution,
            V20ContentEffectOwnerSnapshot before,
            V20ContentEffectOwnerSnapshot after,
            V20ContentEffectCommitReferences childCommits)
    {
        var result = new List<V20EffectApplicationEvidence>();
        string[] participants = resolution.ParticipantCharacterIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        for (int effectIndex = 0;
             effectIndex < resolution.Effects.Count;
             effectIndex++)
        {
            V20ContentEffect effect = resolution.Effects[effectIndex];
            if (effect == null || !effect.IsValid)
                continue;
            string childCommitId = childCommits?.ChildCommitId(effect.kind)
                ?? string.Empty;
            if (RequiresCharacter(effect.kind)
                || effect.kind == V20ContentEffectKind.RetirementSchedule)
            {
                foreach (string participantId in participants)
                {
                    bool targetExists = HasCharacterOwner(before, participantId)
                        || effect.kind == V20ContentEffectKind.RetirementSchedule
                            && HasCareer(before, participantId);
                    string beforeValue = CharacterEffectValue(
                        before,
                        resolution,
                        effect,
                        participantId,
                        participants);
                    string afterValue = CharacterEffectValue(
                        after,
                        resolution,
                        effect,
                        participantId,
                        participants);
                    float appliedAmount = ResolveCharacterAppliedAmount(
                        effect,
                        before,
                        after,
                        participantId,
                        beforeValue,
                        afterValue);
                    result.Add(new V20EffectApplicationEvidence
                    {
                        effectIndex = effectIndex,
                        targetId = participantId,
                        status = ResolveCharacterApplicationStatus(
                            effect,
                            targetExists,
                            appliedAmount,
                            beforeValue,
                            afterValue),
                        requestedAmount = effect.amount,
                        appliedAmount = appliedAmount,
                        beforeValue = beforeValue,
                        afterValue = afterValue,
                        semantic = EffectSemantic(effect.kind),
                        childCommitId = childCommitId
                    });
                }
                continue;
            }

            string actualTarget = ResolveGlobalEffectTarget(
                resolution,
                effect,
                before,
                after);
            string globalBefore = GlobalEffectValue(
                resolution,
                effect,
                actualTarget,
                before);
            string globalAfter = GlobalEffectValue(
                resolution,
                effect,
                actualTarget,
                after);
            float globalApplied = ResolveGlobalAppliedAmount(
                effect,
                globalBefore,
                globalAfter);
            result.Add(new V20EffectApplicationEvidence
            {
                effectIndex = effectIndex,
                targetId = actualTarget,
                status = string.Equals(
                        globalBefore,
                        globalAfter,
                        StringComparison.Ordinal)
                    ? V20EffectApplicationStatus.NoOp
                    : V20EffectApplicationStatus.Applied,
                requestedAmount = effect.amount,
                appliedAmount = globalApplied,
                beforeValue = globalBefore,
                afterValue = globalAfter,
                semantic = EffectSemantic(effect.kind),
                childCommitId = childCommitId
            });
        }
        return result.AsReadOnly();
    }

    private static V20EffectApplicationStatus ResolveCharacterApplicationStatus(
        V20ContentEffect effect,
        bool targetExists,
        float appliedAmount,
        string beforeValue,
        string afterValue)
    {
        if (!targetExists)
            return V20EffectApplicationStatus.Skipped;
        bool changed = !string.Equals(
            beforeValue,
            afterValue,
            StringComparison.Ordinal);
        if (effect.kind == V20ContentEffectKind.DiseaseExposure && !changed)
            return V20EffectApplicationStatus.Skipped;
        float expected = effect.kind switch
        {
            V20ContentEffectKind.SkillExperience =>
                Math.Max(0, Mathf.RoundToInt(effect.amount)),
            V20ContentEffectKind.Relationship =>
                Mathf.Clamp(effect.amount / 10f, -1f, 1f),
            V20ContentEffectKind.DiseaseExposure =>
                Math.Max(0.1f, Math.Abs(effect.amount)),
            V20ContentEffectKind.AmbitionProgress =>
                Mathf.RoundToInt(effect.amount),
            V20ContentEffectKind.RetirementSchedule => effect.durationDays,
            _ => effect.amount
        };
        if (!Mathf.Approximately(appliedAmount, expected))
            return V20EffectApplicationStatus.Clamped;
        return changed
            ? V20EffectApplicationStatus.Applied
            : V20EffectApplicationStatus.NoOp;
    }

    private static float ResolveCharacterAppliedAmount(
        V20ContentEffect effect,
        V20ContentEffectOwnerSnapshot before,
        V20ContentEffectOwnerSnapshot after,
        string participantId,
        string beforeValue,
        string afterValue)
    {
        switch (effect.kind)
        {
            case V20ContentEffectKind.Mood:
                return MoodFactorValue(after, participantId, effect)
                    - MoodFactorValue(before, participantId, effect);
            case V20ContentEffectKind.Trauma:
                return TraumaValue(after, participantId)
                    - TraumaValue(before, participantId);
            case V20ContentEffectKind.SkillExperience:
                return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
                    ? 0f
                    : Math.Max(0, Mathf.RoundToInt(effect.amount));
            case V20ContentEffectKind.Health:
                return HealthValue(after, participantId)
                    - HealthValue(before, participantId);
            case V20ContentEffectKind.Relationship:
                return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
                    ? 0f
                    : Mathf.Clamp(effect.amount / 10f, -1f, 1f);
            case V20ContentEffectKind.DiseaseExposure:
                return ExposureValue(after, participantId, effect.targetId)
                    - ExposureValue(before, participantId, effect.targetId);
            case V20ContentEffectKind.AmbitionProgress:
                return AmbitionProgress(after, participantId)
                    - AmbitionProgress(before, participantId);
            case V20ContentEffectKind.RetirementSchedule:
                return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
                    ? 0f
                    : effect.durationDays;
            default:
                return 0f;
        }
    }

    private static string CharacterEffectValue(
        V20ContentEffectOwnerSnapshot snapshot,
        V20ResolvedEventResult resolution,
        V20ContentEffect effect,
        string participantId,
        IReadOnlyList<string> participants)
    {
        if (!HasCharacterOwner(snapshot, participantId)
            && effect.kind != V20ContentEffectKind.RetirementSchedule)
            return "unavailable";
        return effect.kind switch
        {
            V20ContentEffectKind.Mood =>
                FormatFloat(MoodFactorValue(snapshot, participantId, effect)),
            V20ContentEffectKind.Trauma =>
                FormatFloat(TraumaValue(snapshot, participantId)),
            V20ContentEffectKind.SkillExperience =>
                ProgressionValue(snapshot, participantId),
            V20ContentEffectKind.Health =>
                FormatFloat(HealthValue(snapshot, participantId)),
            V20ContentEffectKind.Relationship =>
                RelationshipValue(snapshot, participantId, participants),
            V20ContentEffectKind.DiseaseExposure =>
                DiseaseExposureValue(
                    snapshot,
                    participantId,
                    effect.targetId),
            V20ContentEffectKind.AmbitionProgress =>
                AmbitionValue(snapshot, participantId),
            V20ContentEffectKind.RetirementSchedule =>
                RetirementValue(snapshot, participantId),
            _ => "unavailable"
        };
    }

    private static string ResolveGlobalEffectTarget(
        V20ResolvedEventResult resolution,
        V20ContentEffect effect,
        V20ContentEffectOwnerSnapshot before,
        V20ContentEffectOwnerSnapshot after)
    {
        if (effect.kind == V20ContentEffectKind.Money)
            return "treasury";
        if (effect.kind is V20ContentEffectKind.FactionRapport
            or V20ContentEffectKind.FactionGrievance
            or V20ContentEffectKind.FactionObligation)
        {
            string changed = (after.Factions?.factions
                    ?? new List<FactionCampaignStateSaveData>())
                .Where(candidate => candidate != null)
                .Select(candidate => candidate.factionId)
                .FirstOrDefault(factionId => !string.Equals(
                    FactionValue(before, factionId, effect.kind),
                    FactionValue(after, factionId, effect.kind),
                    StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(changed))
                return changed;
            if (!string.IsNullOrWhiteSpace(effect.targetId))
                return effect.targetId;
            if (!string.IsNullOrWhiteSpace(resolution.ContextFactionId))
                return resolution.ContextFactionId;
        }
        if (effect.kind == V20ContentEffectKind.WorkDelayDays)
        {
            string changed = (after.Society?.workDelays
                    ?? new List<V20WorkDelaySaveData>())
                .Where(value => value != null)
                .Select(value => value.scopeId)
                .FirstOrDefault(scope => !string.Equals(
                    WorkDelayValue(before, scope),
                    WorkDelayValue(after, scope),
                    StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(changed))
                return changed;
        }
        return string.IsNullOrWhiteSpace(effect.targetId)
            ? "content-resolution"
            : effect.targetId;
    }

    private static string GlobalEffectValue(
        V20ResolvedEventResult resolution,
        V20ContentEffect effect,
        string targetId,
        V20ContentEffectOwnerSnapshot snapshot) => effect.kind switch
    {
        V20ContentEffectKind.FactionRapport
            or V20ContentEffectKind.FactionGrievance
            or V20ContentEffectKind.FactionObligation =>
            FactionValue(snapshot, targetId, effect.kind),
        V20ContentEffectKind.Money => snapshot.MoneyBalance.ToString(
            System.Globalization.CultureInfo.InvariantCulture),
        V20ContentEffectKind.ItemGrant
            or V20ContentEffectKind.ItemConsume =>
            ItemQuantity(snapshot, targetId).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
        V20ContentEffectKind.WorldFlag =>
            BoolValue(snapshot.Milestones?.worldFlags?.Contains(
                targetId,
                StringComparer.Ordinal) == true),
        V20ContentEffectKind.WorkDelayDays => WorkDelayValue(snapshot, targetId),
        V20ContentEffectKind.Threat
            or V20ContentEffectKind.MilestonePressure =>
            BoolValue(snapshot.Milestones?.activePressureIds?.Contains(
                targetId,
                StringComparer.Ordinal) == true),
        _ => "unavailable"
    };

    private static float ResolveGlobalAppliedAmount(
        V20ContentEffect effect,
        string beforeValue,
        string afterValue)
    {
        if (effect.kind is V20ContentEffectKind.WorldFlag
            or V20ContentEffectKind.Threat
            or V20ContentEffectKind.MilestonePressure)
            return string.Equals(beforeValue, afterValue, StringComparison.Ordinal)
                ? 0f
                : 1f;
        if (effect.kind == V20ContentEffectKind.ItemConsume)
            return Mathf.RoundToInt(effect.amount);
        if (effect.kind is V20ContentEffectKind.ItemGrant
            or V20ContentEffectKind.Money
            or V20ContentEffectKind.FactionRapport
            or V20ContentEffectKind.FactionGrievance
            or V20ContentEffectKind.FactionObligation
            or V20ContentEffectKind.WorkDelayDays)
            return Mathf.RoundToInt(effect.amount);
        return effect.amount;
    }

    private static string EffectSemantic(V20ContentEffectKind kind) => kind switch
    {
        V20ContentEffectKind.Mood => "mood-factor",
        V20ContentEffectKind.Trauma => "trauma",
        V20ContentEffectKind.SkillExperience => "skill-experience",
        V20ContentEffectKind.Health => "health-change-not-pressure",
        V20ContentEffectKind.Relationship => "relationship-sentiment",
        V20ContentEffectKind.FactionRapport => "faction-rapport",
        V20ContentEffectKind.FactionGrievance => "faction-grievance",
        V20ContentEffectKind.FactionObligation => "faction-obligation",
        V20ContentEffectKind.Money => "treasury-balance",
        V20ContentEffectKind.ItemGrant => "item-grant",
        V20ContentEffectKind.ItemConsume => "item-consumption",
        V20ContentEffectKind.WorldFlag => "world-flag",
        V20ContentEffectKind.WorkDelayDays => "work-delay",
        V20ContentEffectKind.Threat => "pressure-activated-not-health-damage",
        V20ContentEffectKind.DiseaseExposure =>
            "exposure-pending-not-infection",
        V20ContentEffectKind.AmbitionProgress => "ambition-progress",
        V20ContentEffectKind.MilestonePressure =>
            "pressure-activated-not-health-damage",
        V20ContentEffectKind.RetirementSchedule =>
            "retirement-scheduled-not-character-exit",
        _ => "unsupported"
    };

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

    private static float MoodFactorValue(
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

    private static float HealthValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.BodyHealth?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.currentHealth ?? 0f;

    private static float TraumaValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Psychosocial?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.trauma ?? 0f;

    private static string ProgressionValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId)
    {
        CharacterProgressionSnapshot value = CharacterOwner(snapshot, characterId)
            ?.Progression;
        return value == null
            ? "unavailable"
            : "level=" + value.Level + ",xp=" + value.CurrentExperience;
    }

    private static string RelationshipValue(
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

    private static float ExposureValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId,
        string diseaseId) => snapshot?.PopulationHealth?.pendingExposures
        ?.FirstOrDefault(value => value != null
            && string.Equals(value.characterId, characterId, StringComparison.Ordinal)
            && string.Equals(value.diseaseId, diseaseId, StringComparison.Ordinal))
        ?.weightedExposureHours ?? 0f;

    private static string DiseaseExposureValue(
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

    private static int AmbitionProgress(
        V20ContentEffectOwnerSnapshot snapshot,
        string characterId) => snapshot?.Narrative?.characters
        ?.FirstOrDefault(value => value != null && string.Equals(
            value.characterId,
            characterId,
            StringComparison.Ordinal))?.ambitionProgress ?? 0;

    private static string AmbitionValue(
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

    private static string RetirementValue(
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

    private static string FactionValue(
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

    private static int ItemQuantity(
        V20ContentEffectOwnerSnapshot snapshot,
        string itemId) => snapshot?.Items?.stacks?
        .Where(value => value != null && string.Equals(
            value.itemId,
            itemId,
            StringComparison.Ordinal))
        .Sum(value => Math.Max(0, value.quantity)) ?? 0;

    private static string WorkDelayValue(
        V20ContentEffectOwnerSnapshot snapshot,
        string scopeId) => (snapshot?.Society?.workDelays
            ?.FirstOrDefault(value => value != null && string.Equals(
                value.scopeId,
                scopeId,
                StringComparison.Ordinal))?.untilAbsoluteDayExclusive ?? 0)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatFloat(float value) => value.ToString(
        "R",
        System.Globalization.CultureInfo.InvariantCulture);

    private static string BoolValue(bool value) => value ? "true" : "false";

    internal static bool RequiresCharacter(V20ContentEffectKind kind) =>
        kind is V20ContentEffectKind.Mood
            or V20ContentEffectKind.Trauma
            or V20ContentEffectKind.SkillExperience
            or V20ContentEffectKind.Health
            or V20ContentEffectKind.Relationship
            or V20ContentEffectKind.DiseaseExposure
            or V20ContentEffectKind.AmbitionProgress;
}
