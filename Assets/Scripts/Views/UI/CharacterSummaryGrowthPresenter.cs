using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects character progression and owns skill-choice confirmation UI state.</summary>
public sealed class CharacterSummaryGrowthPresenter
{
    private readonly IGameEventBus eventBus;
    private readonly CharacterSummaryCombatPresenter combatPresenter;
    private Slider experience;
    private TMP_Text summaryText;
    private Button[] skillButtons = Array.Empty<Button>();
    private int pendingCandidateConfirmation = -1;
    private int pendingCandidateUnlockLevel = -1;

    public CharacterSummaryGrowthPresenter(
        IGameEventBus eventBus,
        CharacterSummaryCombatPresenter combatPresenter)
    {
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this.combatPresenter = combatPresenter
            ?? throw new ArgumentNullException(nameof(combatPresenter));
    }

    public void Bind(Slider generatedExperience, TMP_Text generatedSummary, Button[] generatedSkillButtons)
    {
        experience = generatedExperience;
        summaryText = generatedSummary;
        skillButtons = generatedSkillButtons ?? Array.Empty<Button>();
    }

    public void ResetSelection()
    {
        pendingCandidateConfirmation = -1;
        pendingCandidateUnlockLevel = -1;
    }

    public void ToggleSkill(CharacterActor actor, CharacterProgression progression, int index)
    {
        if (actor == null || progression == null || index < 7 || index >= 10)
        {
            return;
        }

        CharacterSkillDraft draft = GetCurrentActiveDraft(progression);
        int candidateIndex = index - 7;
        if (draft == null || candidateIndex >= draft.candidates.Count)
        {
            return;
        }

        bool confirmed = pendingCandidateUnlockLevel == draft.unlockLevel
            && pendingCandidateConfirmation == candidateIndex;
        if (!confirmed)
        {
            pendingCandidateUnlockLevel = draft.unlockLevel;
            pendingCandidateConfirmation = candidateIndex;
            eventBus.ShowNotice(
                $"{draft.candidates[candidateIndex].displayName}: 다시 누르면 영구 확정",
                NoticeFeedEvent.Grade.WARNING);
        }
        else
        {
            progression.TryChooseActiveSkill(
                draft.unlockLevel,
                candidateIndex,
                confirmed: true,
                out string message);
            eventBus.ShowNotice(message, NoticeFeedEvent.Grade.NONE);
            ResetSelection();
        }

        Refresh(actor, progression);
    }

    public void Refresh(CharacterActor actor, CharacterProgression progression)
    {
        if (progression == null || actor == null)
        {
            SetMeter(experience, 0f, "--");
            if (summaryText != null)
            {
                summaryText.text = "성장 정보가 없습니다.";
            }
            return;
        }

        string experienceText = progression.Level >= CharacterProgression.MaxLevel
            ? "MAX"
            : $"{progression.CurrentExperience}/{progression.ExperienceToNextLevel}";
        SetMeter(experience, progression.ExperienceRatio, experienceText);
        if (summaryText != null)
        {
            string traits = string.Join(", ", progression.ResolveSelectedTraits()
                .Where(trait => trait != null)
                .Select(trait => trait.traitName));
            summaryText.text = BuildProgressionSummary(actor, progression, traits);
        }

        for (int i = 0; i < skillButtons.Length; i++)
        {
            Button button = skillButtons[i];
            if (button == null)
            {
                continue;
            }

            TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            button.gameObject.SetActive(true);
            button.interactable = false;
            bool selected = false;
            string text;
            if (i == 0)
            {
                CharacterCombatAbilityDefinition species =
                    CharacterCombatAbilityCatalog.GetSpeciesAbilities(actor).FirstOrDefault();
                text = species != null
                    ? $"종족기  {species.DisplayName}  ·  {species.Description}"
                    : "종족기  없음";
            }
            else if (i <= 3)
            {
                int slot = i - 1;
                CharacterSkillInstance skill = slot < progression.ActiveSkills.Count
                    ? progression.ActiveSkills[slot]
                    : null;
                int unlockLevel = new[] { 1, 5, 30 }[slot];
                text = skill != null
                    ? $"액티브 {slot + 1}  [{CharacterSkillDisplay.Rarity(skill.rarity)}] {skill.displayName}  ·  {skill.description}"
                    : $"액티브 {slot + 1}  Lv.{unlockLevel} 선택 대기";
            }
            else if (i <= 5)
            {
                int slot = i - 4;
                CharacterSkillInstance skill = slot < progression.PassiveSkills.Count
                    ? progression.PassiveSkills[slot]
                    : null;
                text = skill != null
                    ? $"패시브 {slot + 1}  [{CharacterSkillDisplay.Rarity(skill.rarity)}] {skill.displayName}  ·  {skill.description}"
                    : $"패시브 {slot + 1}  {(slot == 0 ? "정체성 기술 대기" : "Lv.25 서사 조건")}";
            }
            else if (i == 6)
            {
                CharacterSkillInstance skill = progression.Ultimate;
                text = skill != null
                    ? $"궁극기  [{skill.ultimateDomain}] {skill.displayName}  ·  {skill.description}"
                    : "궁극기  Lv.50 서사 완성 후 획득";
            }
            else
            {
                CharacterSkillDraft draft = GetCurrentActiveDraft(progression);
                int candidateIndex = i - 7;
                CharacterSkillInstance candidate = draft != null && candidateIndex < draft.candidates.Count
                    ? draft.candidates[candidateIndex]
                    : null;
                bool visible = candidate != null;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                selected = pendingCandidateUnlockLevel == draft.unlockLevel
                    && pendingCandidateConfirmation == candidateIndex;
                text = $"후보 {candidateIndex + 1}  [{CharacterSkillDisplay.Rarity(candidate.rarity)}] {candidate.displayName}  ·  {candidate.description}";
                button.interactable = true;
            }

            if (label != null)
            {
                label.text = text;
            }
            DungeonUiTheme.StyleButton(button, selected);
        }
    }

    private string BuildProgressionSummary(
        CharacterActor actor,
        CharacterProgression progression,
        string traits)
    {
        StringBuilder builder = new StringBuilder(768);
        builder.AppendLine(
            $"Lv.{progression.Level}  ·  잠재력 {CharacterSkillDisplay.Potential(progression.PotentialGrade)}  ·  성장 +{progression.GrowthState.allocatedGrowthPoints}");
        builder.AppendLine($"특성  {(string.IsNullOrWhiteSpace(traits) ? "없음" : traits)}");
        builder.AppendLine("능력치  기본 | 종족·특성 | 레벨 | 장비 | 조건부 | 최종");

        CombatEquipmentUiStatBlock equipment = combatPresenter.GetCurrentEquipmentBonuses(actor);
        foreach (CharacterStatDefinition definition in CharacterStatCatalog.All
                     .Where(item => item.LegacyType.HasValue))
        {
            CharacterStatType statType = definition.LegacyType.Value;
            CharacterStatBreakdown breakdown = progression.GetStatBreakdown(statType);
            int equipmentBonus = GetEquipmentBonus(equipment, statType);
            int finalValue = Mathf.Max(0, breakdown.FinalValue + equipmentBonus);
            builder.AppendLine(
                $"{definition.DisplayName}  {breakdown.BaseValue} | {CharacterSummaryTextFormatter.FormatSigned(breakdown.SpeciesTraitValue)}"
                + $" | {CharacterSummaryTextFormatter.FormatSigned(breakdown.LevelGrowthValue)}"
                + $" | {CharacterSummaryTextFormatter.FormatSigned(equipmentBonus)}"
                + $" | {CharacterSummaryTextFormatter.FormatSigned(breakdown.ConditionalPassiveValue)} | {finalValue}");
        }

        builder.AppendLine(equipment != null && equipment.maxHealth != 0
            ? $"장비 체력 {CharacterSummaryTextFormatter.FormatSigned(equipment.maxHealth)} · 장비 보정은 출정 전투에만 적용"
            : "장비 보정은 출정 전투에만 적용");

        IReadOnlyList<CharacterGrowthAllocationRecord> records = progression.GrowthState.allocationRecords;
        if (records == null || records.Count == 0)
        {
            builder.Append("최근 성장  아직 레벨 성장 기록 없음");
        }
        else
        {
            builder.Append("최근 성장  ");
            builder.Append(string.Join(" / ", records
                .Where(record => record != null)
                .OrderByDescending(record => record.level)
                .Take(4)
                .Select(FormatGrowthAllocationRecord)));
        }
        return builder.ToString();
    }

    private static CharacterSkillDraft GetCurrentActiveDraft(CharacterProgression progression)
    {
        return progression?.Drafts
            .Where(draft => draft != null
                && draft.kind == CharacterSkillKind.Active
                && draft.isReady
                && !draft.permanentlyChosen)
            .OrderBy(draft => draft.unlockLevel)
            .FirstOrDefault();
    }

    private static string FormatGrowthAllocationRecord(CharacterGrowthAllocationRecord record)
    {
        string statName = CharacterStatCatalog.TryGet(record.statType, out CharacterStatDefinition definition)
            ? definition.DisplayName
            : record.statType.ToString();
        string reason = string.IsNullOrWhiteSpace(record.reason) ? "성장 기록" : record.reason;
        return $"Lv.{record.level} {statName}+1({reason})";
    }

    private static int GetEquipmentBonus(
        CombatEquipmentUiStatBlock equipment,
        CharacterStatType statType)
    {
        if (equipment == null)
        {
            return 0;
        }

        return statType switch
        {
            CharacterStatType.Attack => equipment.attack,
            CharacterStatType.Strength => equipment.strength,
            CharacterStatType.Toughness => equipment.toughness,
            CharacterStatType.Dexterity => equipment.dexterity,
            CharacterStatType.MoveSpeed => equipment.moveSpeed,
            _ => 0
        };
    }

    private static void SetMeter(Slider slider, float normalizedValue, string valueText)
    {
        if (slider == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalizedValue);
        slider.value = clamped;
        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null)
        {
            fill.color = DungeonUiTheme.GetMeterColor(clamped);
        }

        Transform row = slider.transform.parent;
        TMP_Text value = row != null ? row.Find("Value")?.GetComponent<TMP_Text>() : null;
        if (value != null)
        {
            value.text = valueText;
            value.color = clamped < 0.25f
                ? DungeonUiTheme.Danger
                : clamped < 0.5f
                    ? DungeonUiTheme.Warning
                    : DungeonUiTheme.TextSecondary;
        }
    }
}
