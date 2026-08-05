using System;
using System.Collections.Generic;
using System.Linq;

public static class CharacterWorldSaveValidation
{
    public static void ValidateActor(
        DungeonCharacterSaveData actor,
        string persistentId,
        DungeonGameRestoreReport report)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        string label = $"Character '{persistentId}'";
        ValidateConditions(actor.conditions, label, report);
        ValidateMoodFactors(actor.moodFactors, label, report);
        ValidateWorkPriorities(actor.workPriorities, label, report);
        ValidateStringIds(actor.learnedSkillIds, "learned skill", label, report);
        ValidateStringIds(actor.equippedSkillIds, "equipped skill", label, report);
        ValidateLogs(actor.recentLogEntries, label, report);

        if (actor.visitCount < 0
            || actor.lookAroundCount < 0
            || actor.holdingMoney < 0
            || actor.level < 1
            || actor.currentExperience < 0)
        {
            report.AddError($"{label} contains a negative counter or invalid level.");
        }

        ValidateGrowth(actor.growth, label, report);
        ValidateNarrative(actor.narrative, label, report);
        ValidateSocialMemory(actor.socialMemory, label, report);
        ValidateRecovery(actor.expeditionRecovery, label, report);
        ValidateCarryInventory(actor.carryInventory, label, report);
    }

    public static void ValidatePopulationProfiles(
        IEnumerable<WorldCharacterProfile> profiles,
        DungeonGameRestoreReport report)
    {
        if (profiles == null)
        {
            return;
        }

        foreach (WorldCharacterProfile profile in profiles)
        {
            if (profile == null)
            {
                continue;
            }

            string label = $"Population profile '{profile.persistentId}'";
            if (profile.level < 1
                || profile.currentExperience < 0
                || profile.visitCount < 0)
            {
                report.AddError($"{label} contains a negative counter or invalid level.");
            }

            ValidateGrowth(profile.growth, label, report);
            ValidateNarrative(profile.narrative, label, report);
            ValidateSocialMemory(profile.socialMemory, label, report);
        }
    }

    public static void ValidateReputation(
        GlobalFacilityReputationSnapshot snapshot,
        DungeonGameRestoreReport report)
    {
        if (snapshot == null)
        {
            return;
        }

        ValidateRumors(snapshot.rumors, "Global facility reputation", report);
        ValidateMemoryValues(
            snapshot.reputation,
            "Global facility reputation",
            "reputation",
            report);
    }

    private static void ValidateConditions(
        List<DungeonCharacterConditionSaveData> conditions,
        string label,
        DungeonGameRestoreReport report)
    {
        if (conditions == null)
        {
            report.AddError($"{label} condition collection is missing.");
            return;
        }

        if (conditions.Any(entry => entry == null))
        {
            report.AddError($"{label} contains a null condition entry.");
        }

        foreach (IGrouping<CharacterCondition, DungeonCharacterConditionSaveData> duplicate in
                 conditions.Where(entry => entry != null)
                     .GroupBy(entry => entry.condition)
                     .Where(group => group.Count() > 1))
        {
            report.AddError($"{label} repeats condition '{duplicate.Key}'.");
        }

        foreach (DungeonCharacterConditionSaveData condition in
                 conditions.Where(entry => entry != null))
        {
            if (!Enum.IsDefined(typeof(CharacterCondition), condition.condition)
                || !IsFinite(condition.value))
            {
                report.AddError($"{label} contains an invalid condition value.");
            }
        }
    }

    private static void ValidateMoodFactors(
        List<DungeonCharacterMoodFactorSaveData> factors,
        string label,
        DungeonGameRestoreReport report)
    {
        if (factors == null)
        {
            report.AddError($"{label} mood-factor collection is missing.");
            return;
        }

        if (factors.Any(factor => factor == null))
        {
            report.AddError($"{label} contains a null mood-factor entry.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonCharacterMoodFactorSaveData factor in
                 factors.Where(factor => factor != null))
        {
            string id = factor.id?.Trim() ?? string.Empty;
            if (id.Length == 0 || !ids.Add(id))
            {
                report.AddError(id.Length == 0
                    ? $"{label} contains an empty mood-factor ID."
                    : $"{label} repeats mood-factor ID '{id}'.");
            }

            if (!IsFinite(factor.value)
                || !IsFinite(factor.remainingSeconds)
                || factor.remainingSeconds < 0f)
            {
                report.AddError($"{label} mood factor '{id}' has an invalid value or duration.");
            }
        }
    }

    private static void ValidateWorkPriorities(
        List<DungeonCharacterWorkPrioritySaveData> priorities,
        string label,
        DungeonGameRestoreReport report)
    {
        if (priorities == null)
        {
            report.AddError($"{label} work-priority collection is missing.");
            return;
        }

        if (priorities.Any(entry => entry == null))
        {
            report.AddError($"{label} contains a null work-priority entry.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (DungeonCharacterWorkPrioritySaveData priority in
                 priorities.Where(entry => entry != null))
        {
            string id = priority.workTypeId?.Trim() ?? string.Empty;
            if (id.Length == 0 || !ids.Add(id))
            {
                report.AddError(id.Length == 0
                    ? $"{label} contains an empty work type ID."
                    : $"{label} repeats work type '{id}'.");
            }

            if (id.Length > 0 && !WorkTypeCatalog.TryGet(id, out _))
            {
                report.AddError($"{label} references unknown work type '{id}'.");
            }

            if (!Enum.IsDefined(typeof(WorkPriorityLevel), priority.priority))
            {
                report.AddError($"{label} work type '{id}' has an unknown priority.");
            }
        }
    }

    private static void ValidateStringIds(
        List<string> values,
        string valueLabel,
        string ownerLabel,
        DungeonGameRestoreReport report)
    {
        if (values == null)
        {
            report.AddError($"{ownerLabel} {valueLabel} collection is missing.");
            return;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            string id = value?.Trim() ?? string.Empty;
            if (id.Length == 0 || !ids.Add(id))
            {
                report.AddError(id.Length == 0
                    ? $"{ownerLabel} contains an empty {valueLabel} ID."
                    : $"{ownerLabel} repeats {valueLabel} ID '{id}'.");
            }
        }
    }

    private static void ValidateLogs(
        List<string> entries,
        string label,
        DungeonGameRestoreReport report)
    {
        if (entries == null)
        {
            report.AddError($"{label} visible-log collection is missing.");
            return;
        }

        if (entries.Count > 30 || entries.Any(entry => entry == null))
        {
            report.AddError($"{label} visible-log collection is not canonical.");
        }
    }

    private static void ValidateGrowth(
        CharacterGrowthState growth,
        string label,
        DungeonGameRestoreReport report)
    {
        if (growth == null)
        {
            report.AddError($"{label} growth state is missing.");
            return;
        }

        if (!Enum.IsDefined(typeof(CharacterPotentialGrade), growth.potentialGrade)
            || growth.allocatedGrowthPoints < 0
            || growth.skillGenerationRevision < 0)
        {
            report.AddError($"{label} growth state contains an invalid enum or counter.");
        }

        if (growth.initialBaseStats == null
            || growth.levelGrowthStats == null
            || growth.traitIds == null
            || growth.activeSkills == null
            || growth.passiveSkills == null
            || growth.drafts == null
            || growth.pendingRequestKeys == null
            || growth.allocationRecords == null
            || growth.useLimits == null)
        {
            report.AddError($"{label} growth state contains a missing required component.");
            return;
        }

        if (growth.activeSkills.Any(item => item == null)
            || growth.passiveSkills.Any(item => item == null)
            || growth.drafts.Any(item => item == null)
            || growth.allocationRecords.Any(item => item == null))
        {
            report.AddError($"{label} growth state contains a null list entry.");
        }

        if (growth.traitIds.Count != growth.traitIds.Distinct().Count())
        {
            report.AddError($"{label} growth state repeats a trait ID.");
        }

        ValidateStringIds(
            growth.pendingRequestKeys,
            "pending request",
            label,
            report);
        foreach (CharacterGrowthAllocationRecord allocation in
                 growth.allocationRecords.Where(item => item != null))
        {
            if (allocation.level < 1
                || !Enum.IsDefined(typeof(CharacterStatType), allocation.statType))
            {
                report.AddError($"{label} contains an invalid growth allocation record.");
            }
        }
    }

    private static void ValidateNarrative(
        CharacterNarrativeLedger narrative,
        string label,
        DungeonGameRestoreReport report)
    {
        if (narrative == null || narrative.facts == null)
        {
            report.AddError($"{label} narrative ledger is missing.");
            return;
        }

        if (narrative.facts.Any(fact => fact == null))
        {
            report.AddError($"{label} narrative ledger contains a null fact.");
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterNarrativeFact fact in narrative.facts.Where(fact => fact != null))
        {
            string factId = fact.factId?.Trim() ?? string.Empty;
            string subjectId = fact.subjectId?.Trim() ?? string.Empty;
            string key = $"{(int)fact.domain}:{factId}:{subjectId}";
            if (!Enum.IsDefined(typeof(CharacterNarrativeDomain), fact.domain)
                || factId.Length == 0
                || !keys.Add(key)
                || fact.count < 0
                || fact.lastDay < 0
                || fact.milestoneCount < 0
                || !IsFinite(fact.totalValue))
            {
                report.AddError($"{label} contains an invalid or duplicate narrative fact '{key}'.");
            }
        }
    }

    private static void ValidateSocialMemory(
        CharacterSocialMemorySnapshot memory,
        string label,
        DungeonGameRestoreReport report)
    {
        if (memory == null)
        {
            report.AddError($"{label} social-memory snapshot is missing.");
            return;
        }

        ValidateRumors(memory.recentRumors, label, report);
        ValidateMemoryValues(memory.facilitySentiments, label, "facility sentiment", report);
        ValidateMemoryValues(memory.characterSentiments, label, "character sentiment", report);
        ValidateMemoryValues(memory.sourceTrust, label, "source trust", report);
    }

    private static void ValidateRumors(
        List<SocialRumorSnapshot> rumors,
        string label,
        DungeonGameRestoreReport report)
    {
        if (rumors == null)
        {
            report.AddError($"{label} rumor collection is missing.");
            return;
        }

        if (rumors.Any(rumor => rumor == null))
        {
            report.AddError($"{label} rumor collection contains a null entry.");
        }

        foreach (SocialRumorSnapshot rumor in rumors.Where(rumor => rumor != null))
        {
            if (!Enum.IsDefined(typeof(SocialRumorType), rumor.type)
                || !Enum.IsDefined(typeof(SocialRumorTargetType), rumor.targetType)
                || !IsFinite(rumor.sentiment)
                || !IsFinite(rumor.spreadChance)
                || !IsFinite(rumor.trustImpact)
                || !IsFinite(rumor.remainingSeconds)
                || rumor.spreadChance < 0f
                || rumor.spreadChance > 1f
                || rumor.remainingSeconds < 0f)
            {
                report.AddError($"{label} contains an invalid rumor snapshot.");
            }
        }
    }

    private static void ValidateMemoryValues(
        List<SocialMemoryFloat> values,
        string ownerLabel,
        string valueLabel,
        DungeonGameRestoreReport report)
    {
        if (values == null)
        {
            report.AddError($"{ownerLabel} {valueLabel} collection is missing.");
            return;
        }

        if (values.Any(value => value == null))
        {
            report.AddError($"{ownerLabel} {valueLabel} collection contains a null entry.");
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (SocialMemoryFloat value in values.Where(value => value != null))
        {
            string key = value.key?.Trim() ?? string.Empty;
            if (key.Length == 0 || !keys.Add(key) || !IsFinite(value.value))
            {
                report.AddError($"{ownerLabel} contains an invalid or duplicate {valueLabel} key '{key}'.");
            }
        }
    }

    private static void ValidateRecovery(
        CharacterExpeditionRecoveryState recovery,
        string label,
        DungeonGameRestoreReport report)
    {
        if (recovery == null
            || !IsFinite(recovery.stress)
            || recovery.stress < 0f
            || recovery.stress > 100f)
        {
            report.AddError($"{label} expedition-recovery state is missing or invalid.");
        }
    }

    private static void ValidateCarryInventory(
        CharacterCarryInventorySaveData inventory,
        string label,
        DungeonGameRestoreReport report)
    {
        if (inventory == null || inventory.items == null)
        {
            report.AddError($"{label} carry-inventory snapshot is missing.");
            return;
        }

        if (inventory.items.Any(item => item == null))
        {
            report.AddError($"{label} carry inventory contains a null item.");
        }

        HashSet<string> instanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterCarriedItemSaveData item in
                 inventory.items.Where(item => item != null))
        {
            ItemInstanceId instanceId = (ItemInstanceId)item.itemInstanceId;
            bool hasInstanceId = !string.IsNullOrWhiteSpace(item.itemInstanceId);
            if (string.IsNullOrWhiteSpace(item.itemId)
                || item.quantity <= 0
                || !Enum.IsDefined(typeof(WasteOriginKind), item.wasteOrigin)
                || !IsFinite(item.contamination)
                || item.contamination < 0f
                || item.contamination > 100f
                || item.components == null
                || item.components.Any(component => component == null)
                || (hasInstanceId
                    && (!instanceId.IsValid
                        || item.quantity != 1
                        || !instanceIds.Add(instanceId.Value))))
            {
                report.AddError($"{label} contains an invalid carried item '{item.itemId}'.");
            }
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
