using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

internal static class StartPartyPreparationPresentation
{
    public static string BuildTraitTooltipText(CharacterTraitSO trait)
    {
        StringBuilder builder = new StringBuilder();
        List<string> statLines = new List<string>();
        foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType)).Cast<CharacterStatType>())
        {
            int value = trait.statBonus?.Get(type) ?? 0;
            if (value != 0)
            {
                statLines.Add($"{StatLabel(type)} {value:+#;-#;0}");
            }
        }

        builder.AppendLine(statLines.Count > 0
            ? "스탯 변화  " + string.Join(" · ", statLines)
            : "스탯 변화  없음");

        CharacterModelModifiers modifiers = trait.modifiers;
        List<string> modifierLines = new List<string>();
        AddMultiplierLine(modifierLines, "욕구 소모", modifiers?.consumptionMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "소비 성향", modifiers?.spendingMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "대기 인내", modifiers?.waitPatienceMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "혼잡 민감도", modifiers?.crowdSensitivityMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "사고 확률", modifiers?.accidentChanceMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "작업 속도", modifiers?.workSpeedMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "연구 속도", modifiers?.researchSpeedMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "전투력", modifiers?.combatPowerMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "이동 속도", modifiers?.moveSpeedMultiplier ?? 1f);
        AddMultiplierLine(modifierLines, "체류 시간", modifiers?.stayDurationMultiplier ?? 1f);
        if (modifierLines.Count > 0)
        {
            builder.AppendLine("세부 보정  " + string.Join(" · ", modifierLines));
        }

        AppendFlags(builder, "선호 시설", FormatFacilityRoles(modifiers?.preferredFacilityRoles ?? FacilityRole.None));
        AppendFlags(builder, "기피 시설", FormatFacilityRoles(modifiers?.dislikedFacilityRoles ?? FacilityRole.None));
        AppendFlags(
            builder,
            "선호 업무",
            FormatWorkTypes(modifiers != null ? modifiers.PreferredWorkTypeIds : Array.Empty<WorkTypeId>()));
        AppendFlags(
            builder,
            "기피 업무",
            FormatWorkTypes(modifiers != null ? modifiers.DislikedWorkTypeIds : Array.Empty<WorkTypeId>()));
        return builder.ToString().TrimEnd();
    }

    public static void AddMultiplierLine(List<string> lines, string label, float value)
    {
        if (Mathf.Approximately(value, 1f))
        {
            return;
        }

        float delta = (value - 1f) * 100f;
        lines.Add($"{label} x{value:0.##} ({delta:+0.#;-0.#;0}%)");
    }

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

    public static string FormatStats(CharacterStatBlock stats)
    {
        if (stats == null)
        {
            return "-";
        }

        return string.Join(" - ", Enum.GetValues(typeof(CharacterStatType))
            .Cast<CharacterStatType>()
            .Select(type => $"{type} {stats.Get(type)}"));
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

    public static string StatLabel(CharacterStatType type)
    {
        return type switch
        {
            CharacterStatType.Attack => "\uACF5\uACA9",
            CharacterStatType.Sales => "\uD310\uB9E4",
            CharacterStatType.Research => "\uC5F0\uAD6C",
            CharacterStatType.MoveSpeed => "\uC774\uB3D9",
            CharacterStatType.Strength => "\uADFC\uB825",
            CharacterStatType.Toughness => "\uB9F7\uC9D1",
            CharacterStatType.Dexterity => "\uBBFC\uCCA9",
            CharacterStatType.Cleaning => "\uCCAD\uC18C",
            CharacterStatType.Endurance => "\uC9C0\uAD6C",
            _ => type.ToString()
        };
    }

}
