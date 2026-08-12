using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

internal static class StartPartyPreparationPresentation
{
    public static string BuildTraitTooltipText(CharacterTraitSO trait)
    {
        if (trait == null)
            return "특성 정의 없음";
        StringBuilder builder = new();
        builder.AppendLine($"{trait.traitName} · {TraitRarityLabel(trait.selectionRarity)}");
        if (!string.IsNullOrWhiteSpace(trait.description))
            builder.AppendLine(trait.description.Trim());
        if (!string.IsNullOrWhiteSpace(trait.selectionFamilyId))
            builder.AppendLine($"선택 계열: {trait.selectionFamilyId.Trim()}");

        foreach (GameplayEffectBinding binding in trait.Effects
                     .Where(value => value?.definition != null)
                     .OrderBy(value => value.definition.TargetId,
                         StringComparer.Ordinal)
                     .ThenBy(value => value.bindingId, StringComparer.Ordinal))
        {
            string condition = binding.condition != null
                ? $" ({binding.condition.ConditionId}일 때)"
                : string.Empty;
            builder.AppendLine(
                $"효과: {binding.definition.TargetId} "
                + $"{FormatEffectValue(binding)}{condition}");
        }
        foreach (CharacterIdentityRule rule in (trait.identityRules
                     ?? new List<CharacterIdentityRule>())
                     .Where(value => value != null)
                     .OrderBy(value => value.priority)
                     .ThenBy(value => value.ruleId, StringComparer.Ordinal))
        {
            builder.AppendLine($"성향: {DescribeIdentityRule(rule)}");
        }
        return builder.ToString().TrimEnd();
    }

    private static string FormatEffectValue(GameplayEffectBinding binding) =>
        binding.definition.Operation switch
        {
            GameplayEffectOperation.Multiply => $"×{binding.value:0.##}",
            GameplayEffectOperation.AddPercent => $"{binding.value:+0.##;-0.##;0}%",
            GameplayEffectOperation.AddFlat => $"{binding.value:+0.##;-0.##;0}",
            GameplayEffectOperation.Override => $"={binding.value:0.##}",
            GameplayEffectOperation.ClampMinimum => $"최소 {binding.value:0.##}",
            GameplayEffectOperation.ClampMaximum => $"최대 {binding.value:0.##}",
            _ => binding.value.ToString("0.##")
        };

    private static string DescribeIdentityRule(CharacterIdentityRule rule) => rule switch
    {
        BehaviorUtilityRule value => $"{value.behaviorTag} 선호 {value.utilityDelta:+0.##;-0.##}",
        PersistentNeedRule value => $"{value.needId} 욕구 · {value.deprivationDays}일 미충족 시 {value.deprivedMoodDelta:0.#}",
        EventMoodRule value => $"{value.eventId} 기분 {value.moodDelta:+0.#;-0.#}",
        MoodImmunityRule value => $"{value.eventId} 기분 반응 면역",
        MoodTransformRule value => $"{value.eventId} 기분 반응 ×{value.multiplier:0.##}",
        PostActionConsequenceRule value => $"{value.actionTag} 이후 기분 {value.moodDelta:+0.#;-0.#}, 스트레스 {value.stressDelta:+0.#;-0.#}",
        RelationshipMemoryRule value => $"{value.eventId} 관계 기억 {value.relationshipDelta:+0.#;-0.#}",
        AutonomousWorkRestrictionRule value => $"자율 {value.actionTag} 제한: {value.failureReason}",
        IncidentWeightRule value => $"{value.incidentId} 사건 가중치 ×{value.multiplier:0.##}",
        _ => rule.ruleId
    };

    public static string TraitRarityLabel(CharacterTraitSelectionRarity rarity) =>
        rarity switch
        {
            CharacterTraitSelectionRarity.Common => "일반",
            CharacterTraitSelectionRarity.Uncommon => "고급",
            CharacterTraitSelectionRarity.Rare => "희귀",
            CharacterTraitSelectionRarity.Exceptional => "특별",
            _ => rarity.ToString()
        };

    public static void AppendFlags(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{label}  {value}");
        }
    }

    public static string FormatFacilityRoles(FacilityRole roles)
    {
        if (roles == FacilityRole.None)
        {
            return string.Empty;
        }

        return string.Join(", ", Enum.GetValues(typeof(FacilityRole))
            .Cast<FacilityRole>()
            .Where(role => role != FacilityRole.None && (roles & role) != 0)
            .Select(FacilityRoleLabel));
    }

    public static string FormatWorkTypes(IEnumerable<WorkTypeId> workTypeIds)
    {
        return CodexDomainTextFormatter.FormatWorkTypes(workTypeIds);
    }

    public static string FacilityRoleLabel(FacilityRole role)
    {
        return role switch
        {
            FacilityRole.Meal => "식사",
            FacilityRole.Purchase => "구매",
            FacilityRole.Rest => "휴식",
            FacilityRole.Training => "훈련",
            FacilityRole.Research => "연구",
            FacilityRole.Mana => "마나",
            FacilityRole.Logistics => "물류",
            FacilityRole.Toilet => "화장실",
            FacilityRole.Hygiene => "위생",
            FacilityRole.Administration => "운영",
            FacilityRole.Security => "방어",
            _ => role.ToString()
        };
    }


    public static string BuildRosterLabel(StartPartyMemberPreparation member)
    {
        string state = member.IsReadyToStart ? "\uC900\uBE44 \uC644\uB8CC" : "\uC900\uBE44 \uC911";
        return $"{member.RosterLabel}\n{ResolveMemberName(member)} - {state}";
    }

    public static string ResolveMemberName(StartPartyMemberPreparation member)
    {
        string preparedName = member.Progression?.GrowthState?.displayName;
        if (!string.IsNullOrWhiteSpace(preparedName))
        {
            return preparedName;
        }

        return member.CharacterData != null ? member.CharacterData.characterName : "-";
    }

    public static string FormatTraitIds(IEnumerable<int> traitIds)
    {
        int[] ids = traitIds?.Distinct().ToArray() ?? Array.Empty<int>();
        return ids.Length == 0 ? "-" : string.Join(", ", ids.Select(id => $"Trait {id}"));
    }

    public static string PotentialLabel(CharacterPotentialGrade grade)
    {
        return grade switch
        {
            CharacterPotentialGrade.Promising => "\uC720\uB9DD",
            CharacterPotentialGrade.Excellent => "\uC6B0\uC218",
            CharacterPotentialGrade.Exceptional => "\uD0C1\uC6D4",
            CharacterPotentialGrade.Genius => "\uCC9C\uC7AC",
            _ => "\uD3C9\uBC94"
        };
    }

    public static string ProficiencyLabel(CharacterProficiencyId id)
    {
        if (id == BuiltInCharacterProficiencyIds.Fieldwork) return "현장 작업";
        if (id == BuiltInCharacterProficiencyIds.ConstructionEngineering) return "건설·공학";
        if (id == BuiltInCharacterProficiencyIds.Crafting) return "제작";
        if (id == BuiltInCharacterProficiencyIds.FoodProduction) return "식량 생산";
        if (id == BuiltInCharacterProficiencyIds.Scholarship) return "학술";
        if (id == BuiltInCharacterProficiencyIds.Medicine) return "의료";
        if (id == BuiltInCharacterProficiencyIds.Social) return "사교";
        if (id == BuiltInCharacterProficiencyIds.MeleeCombat) return "근접 전투";
        if (id == BuiltInCharacterProficiencyIds.RangedCombat) return "원거리 전투";
        throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown proficiency id.");
    }

    public static string ProficiencyBandLabel(long milliExperience)
    {
        CharacterProficiencyBandSnapshot band =
            ProficiencyProgressionRules.ResolveBand(milliExperience);
        string rank = band.Rank switch
        {
            CharacterProficiencyRank.Apprentice => "\uACAC\uC2B5\uC0DD",
            CharacterProficiencyRank.Skilled => "\uC219\uB828\uC790",
            CharacterProficiencyRank.Technician => "\uAE30\uC220\uC790",
            CharacterProficiencyRank.Expert => "\uC804\uBB38\uAC00",
            CharacterProficiencyRank.Master => "\uB300\uAC00",
            _ => throw new ArgumentOutOfRangeException()
        };
        string grade = band.Subgrade switch
        {
            CharacterProficiencySubgrade.Fourth => "IV",
            CharacterProficiencySubgrade.Third => "III",
            CharacterProficiencySubgrade.Second => "II",
            CharacterProficiencySubgrade.First => "I",
            _ => throw new ArgumentOutOfRangeException()
        };
        return $"{rank} {grade}";
    }

    public static string StartingAgeBandLabel(CharacterStartingAgeBand band) =>
        band switch
        {
            CharacterStartingAgeBand.YoungAdult => "\uC80A\uC740 \uC131\uC778",
            CharacterStartingAgeBand.EstablishedAdult => "\uACBD\uB825 \uC131\uC778",
            CharacterStartingAgeBand.VeteranAdult => "\uBCA0\uD14C\uB791",
            CharacterStartingAgeBand.Elder => "\uB178\uB144",
            _ => throw new ArgumentOutOfRangeException(nameof(band), band, null)
        };

}
