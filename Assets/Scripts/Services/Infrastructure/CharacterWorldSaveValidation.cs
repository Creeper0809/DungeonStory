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
        ValidateHaulDeliveryIntent(actor, label, report);
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
            || !Enum.IsDefined(
                typeof(CharacterTraitSelectionAuthorityOrigin),
                growth.traitSelectionAuthorityOrigin)
            || growth.traitSelectionAuthorityOrigin
                == CharacterTraitSelectionAuthorityOrigin.None
            || growth.traitSelectionAuthorityVersion
                != CharacterGrowthState.CurrentTraitSelectionAuthorityVersion
            || growth.skillGenerationRevision < 0)
        {
            report.AddError($"{label} growth state contains an invalid enum or counter.");
        }

        if (growth.startingProfile == null
            || growth.startingProficiencies == null
            || growth.traitIds == null
            || growth.activeSkills == null
            || growth.passiveSkills == null
            || growth.drafts == null
            || growth.pendingRequestKeys == null
            || growth.useLimits == null)
        {
            report.AddError($"{label} growth state contains a missing required component.");
            return;
        }

        try
        {
            CharacterStartingProficiencyRules.Validate(
                growth.startingProficiencies);
        }
        catch (InvalidOperationException exception)
        {
            report.AddError($"{label} {exception.Message}");
        }

        ValidateStartingProfile(growth, label, report);

        if (growth.activeSkills.Any(item => item == null)
            || growth.passiveSkills.Any(item => item == null)
            || growth.drafts.Any(item => item == null))
        {
            report.AddError($"{label} growth state contains a null list entry.");
        }

        if (growth.traitIds.Count != growth.traitIds.Distinct().Count())
        {
            report.AddError($"{label} growth state repeats a trait ID.");
        }
        if (growth.traitIds.Count > 4)
        {
            report.AddError($"{label} growth state exceeds the four-trait limit.");
        }

        ValidateStringIds(
            growth.pendingRequestKeys,
            "pending request",
            label,
            report);
    }

    private static void ValidateStartingProfile(
        CharacterGrowthState growth,
        string label,
        DungeonGameRestoreReport report)
    {
        CharacterStartingProfileState profile = growth.startingProfile;
        profile.EnsureCollections();
        if (!profile.prepared)
            return;

        CharacterProficiencyId primary = new(profile.primaryProficiencyId);
        CharacterProficiencyId secondary = new(profile.secondaryProficiencyId);
        bool knownCap = profile.proficiencyCap
            is CharacterStartingProfileRules.YoungAdultCap
            or CharacterStartingProfileRules.EstablishedAdultCap
            or CharacterStartingProfileRules.VeteranAdultCap
            or CharacterStartingProfileRules.ElderCap;
        if (string.IsNullOrWhiteSpace(profile.originId)
            || string.IsNullOrWhiteSpace(profile.originDisplayName)
            || string.IsNullOrWhiteSpace(profile.historyId)
            || string.IsNullOrWhiteSpace(profile.historyDisplayName)
            || !primary.IsValid
            || !secondary.IsValid
            || primary == secondary
            || !BuiltInCharacterProficiencyIds.All.Contains(primary)
            || !BuiltInCharacterProficiencyIds.All.Contains(secondary)
            || !Enum.IsDefined(typeof(CharacterStartingAgeBand), profile.ageBand)
            || double.IsNaN(profile.biologicalAgeYears)
            || double.IsInfinity(profile.biologicalAgeYears)
            || profile.biologicalAgeYears < 0d
            || !knownCap
            || profile.proficiencyCap != CharacterStartingProfileRules
                .ResolveAgeCap(profile.ageBand)
            || profile.initialAgeConditionIds.Any(string.IsNullOrWhiteSpace)
            || profile.initialAgeConditionIds.Count
                != profile.initialAgeConditionIds.Distinct(StringComparer.Ordinal).Count()
            || growth.startingProficiencies.Any(value =>
                value != null
                && (value.experience > profile.proficiencyCap
                    || Math.Abs(
                        value.learningMultiplier
                        - CharacterProficiencySpecializationRules.Resolve(
                            profile,
                            new CharacterProficiencyId(value.proficiencyId)))
                    > 0.0001f)))
        {
            report.AddError($"{label} contains an invalid prepared starting profile.");
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

    private static void ValidateHaulDeliveryIntent(
        DungeonCharacterSaveData actor,
        string label,
        DungeonGameRestoreReport report)
    {
        HaulDeliveryIntentSaveData intent = actor.haulDeliveryIntent;
        CharacterCarriedItemSaveData[] haulCarried = actor.carryInventory?.items?
            .Where(item => item != null
                && (item.ownerOperationId?.StartsWith(
                    "haul:",
                    StringComparison.Ordinal) ?? false))
            .ToArray() ?? Array.Empty<CharacterCarriedItemSaveData>();
        if (intent == null)
        {
            if (haulCarried.Length > 0)
                report.AddError($"{label} has carried haul items without a delivery intent.");
            return;
        }

        bool isDefaultEmptyIntent = string.IsNullOrWhiteSpace(intent.operationId)
            && string.IsNullOrWhiteSpace(intent.ownerCharacterId)
            && string.IsNullOrWhiteSpace(intent.destinationId)
            && (intent.commitments == null || intent.commitments.Count == 0);
        if (isDefaultEmptyIntent)
        {
            if (haulCarried.Length > 0)
                report.AddError($"{label} has carried haul items without a delivery intent.");
            return;
        }

        string operation = intent.operationId?.Trim() ?? string.Empty;
        if (operation.Length == 0
            || !HaulDeliveryOperationIdentity.TryParse(
                operation,
                intent.ownerCharacterId,
                out _)
            || !string.Equals(
                intent.ownerCharacterId?.Trim(),
                actor.persistentId?.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(intent.destinationId)
            || !Enum.IsDefined(typeof(WorldItemHaulDestinationKind), intent.destinationKind)
            || intent.commitments == null
            || !intent.HasCommittedPickup)
        {
            report.AddError($"{label} has an invalid haul delivery intent '{operation}'.");
            return;
        }

        Dictionary<string, CharacterCarriedItemSaveData> carriedByStack =
            new(StringComparer.Ordinal);
        if (haulCarried.Any(item => !string.Equals(
                item.ownerOperationId?.Trim(),
                operation,
                StringComparison.Ordinal)))
        {
            report.AddError(
                $"{label} has carried haul items owned by more than one delivery intent.");
        }
        foreach (CharacterCarriedItemSaveData carried in haulCarried.Where(item =>
                     string.Equals(
                         item.ownerOperationId?.Trim(),
                         operation,
                         StringComparison.Ordinal)))
        {
            string stackId = carried.carriedStackId?.Trim() ?? string.Empty;
            if (stackId.Length == 0 || !carriedByStack.TryAdd(stackId, carried))
            {
                report.AddError(
                    $"{label} haul delivery '{operation}' has an invalid or duplicate carried stack.");
            }
        }
        HashSet<string> commitmentIds = new(StringComparer.Ordinal);
        foreach (HaulDeliveryItemCommitmentSaveData commitment in
                 intent.commitments.Where(value => value != null))
        {
            if (string.IsNullOrWhiteSpace(commitment.carriedStackId)
                || !commitmentIds.Add(commitment.carriedStackId.Trim())
                || commitment.quantity <= 0
                || string.IsNullOrWhiteSpace(commitment.itemId)
                || string.IsNullOrWhiteSpace(commitment.expectedStackSignature)
                || !carriedByStack.TryGetValue(
                    commitment.carriedStackId.Trim(),
                    out CharacterCarriedItemSaveData carried)
                || carried.quantity != commitment.quantity
                || !string.Equals(carried.itemId, commitment.itemId, StringComparison.Ordinal)
                || !string.Equals(
                    carried.sourceStackId?.Trim(),
                    commitment.sourceStackId?.Trim(),
                    StringComparison.Ordinal)
                || !string.Equals(
                    ItemReservationSignature.Create(
                        carried.itemId,
                        carried.components),
                    commitment.expectedStackSignature,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"{label} haul delivery '{operation}' has a mismatched physical commitment.");
            }
        }
        if (intent.commitments.Count != carriedByStack.Count)
        {
            report.AddError(
                $"{label} haul delivery '{operation}' does not exactly own its carried stacks.");
        }
        if (!ExactWarehouseHaulAdmissionJoin.TryValidateSavedIntent(
                intent,
                haulCarried,
                out string admissionFailure))
        {
            report.AddError(
                $"{label} haul delivery '{operation}' has an invalid warehouse admission join: "
                + admissionFailure);
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
