using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Health, deprivation, surgery, diet, and substance projection for the
/// character summary. Captivity is delegated to its own presenter.
/// </summary>
public sealed class CharacterSummaryHealthPresenter
{
    private readonly ICharacterSurgeryWindowService surgeryWindowService;
    private readonly ICharacterDeprivationQuery deprivationRuntime;
    private readonly ICharacterConsumablesQuery consumablesQuery;
    private readonly ICharacterConsumablesCommand consumablesCommands;
    private readonly IResourceEconomyContentCatalog resourceCatalog;
    private readonly CharacterSummaryCaptivityPresenter captivityPresenter;

    private TMP_Text summaryText;
    private Button dietPolicyButton;
    private Button surgeryCommandButton;
    private Button automaticSurgeryButton;
    private Button substanceSelectionButton;
    private Button substancePolicyButton;
    private int selectedSubstanceIndex;

    public CharacterSummaryHealthPresenter(
        ICharacterSurgeryWindowService surgeryWindowService,
        ICharacterDeprivationQuery deprivationRuntime,
        ICharacterConsumablesQuery consumablesQuery,
        ICharacterConsumablesCommand consumablesCommands,
        IResourceEconomyContentCatalog resourceCatalog,
        CharacterSummaryCaptivityPresenter captivityPresenter)
    {
        this.surgeryWindowService = surgeryWindowService
            ?? throw new ArgumentNullException(nameof(surgeryWindowService));
        this.deprivationRuntime = deprivationRuntime
            ?? throw new ArgumentNullException(nameof(deprivationRuntime));
        this.consumablesQuery = consumablesQuery
            ?? throw new ArgumentNullException(nameof(consumablesQuery));
        this.consumablesCommands = consumablesCommands
            ?? throw new ArgumentNullException(nameof(consumablesCommands));
        this.resourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
        this.captivityPresenter = captivityPresenter
            ?? throw new ArgumentNullException(nameof(captivityPresenter));
    }

    public void Bind(
        TMP_Text generatedSummaryText,
        Button generatedCaptivityActionButton,
        Button generatedDietPolicyButton,
        Button generatedSurgeryCommandButton,
        Button generatedAutomaticSurgeryButton,
        Button generatedSubstanceSelectionButton,
        Button generatedSubstancePolicyButton)
    {
        summaryText = generatedSummaryText;
        dietPolicyButton = generatedDietPolicyButton;
        surgeryCommandButton = generatedSurgeryCommandButton;
        automaticSurgeryButton = generatedAutomaticSurgeryButton;
        substanceSelectionButton = generatedSubstanceSelectionButton;
        substancePolicyButton = generatedSubstancePolicyButton;
        captivityPresenter.Bind(generatedCaptivityActionButton);
    }

    public void CycleDietPolicy(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        CharacterDietPolicyKind current = consumablesQuery.GetPolicy(actor);
        CharacterDietPolicyKind next = (CharacterDietPolicyKind)(
            ((int)current + 1) % Enum.GetValues(typeof(CharacterDietPolicyKind)).Length);
        consumablesCommands.SetPolicy(actor, next);
        Refresh(actor);
    }

    public void OpenSurgeryWindow(CharacterActor actor, Transform parent)
    {
        if (actor != null)
        {
            surgeryWindowService.Open(actor, parent);
        }
    }

    public void ToggleAutomaticEmergencySurgery(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        surgeryWindowService.ToggleAutomaticEmergency(actor);
        Refresh(actor);
    }

    public void SelectNextSubstance(CharacterActor actor)
    {
        int count = resourceCatalog.Substances?.Count ?? 0;
        selectedSubstanceIndex = count > 0 ? (selectedSubstanceIndex + 1) % count : 0;
        Refresh(actor);
    }

    public void CycleSelectedSubstancePolicy(CharacterActor actor)
    {
        if (actor == null || !TryGetSelectedSubstance(out SubstanceDefinitionView substance))
        {
            return;
        }

        CharacterSubstancePolicyState current = consumablesQuery.GetPolicy(
            actor,
            substance.SubstanceId);
        SubstancePolicyMode next = (SubstancePolicyMode)(
            ((int)current.mode + 1) % Enum.GetValues(typeof(SubstancePolicyMode)).Length);
        consumablesCommands.SetPolicy(
            actor,
            substance.SubstanceId,
            next,
            current.moodThreshold,
            current.scheduledHour);
        Refresh(actor);
    }

    public void ExecuteCaptivityAction(CharacterActor actor)
    {
        captivityPresenter.Execute(actor);
        Refresh(actor);
    }

    public void Refresh(CharacterActor actor)
    {
        if (summaryText == null)
        {
            return;
        }

        if (actor == null)
        {
            summaryText.text = CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Empty");
            captivityPresenter.RefreshActionButton(null);
            return;
        }

        StringBuilder builder = new StringBuilder(512);
        if (deprivationRuntime.TryGetSnapshot(actor, out CharacterDeprivationSnapshot snapshot))
        {
            builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Deprivation.Title"));
            foreach (DeprivationKind kind in Enum.GetValues(typeof(DeprivationKind)))
            {
                float burden = snapshot.Burdens != null
                    && snapshot.Burdens.TryGetValue(kind, out float value)
                        ? value
                        : 0f;
                builder.AppendLine(
                    $"{CharacterSummaryTextFormatter.FormatDeprivation(kind),-8} {burden,5:0.#}  {CharacterSummaryTextFormatter.FormatBurdenState(burden)}");
            }

            builder.AppendLine();
            builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Deprivation.InfectionBurden",
                snapshot.InfectionBurden));
            float highest = snapshot.HighestBurden;
            CharacterStats stats = actor.GetComponent<CharacterStats>();
            float mood01 = stats != null ? Mathf.Clamp01(stats.Mood / 100f) : 0.5f;
            float chance = highest >= 70f
                ? CharacterDeprivationRuntime.GetBreakdownChance(actor, highest, mood01) * 100f
                : 0f;
            builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Deprivation.BreakdownChance",
                chance));

            if (snapshot.Breakdown != null && snapshot.Breakdown.active)
            {
                builder.AppendLine(
                    CharacterSummaryHealthStatusTextFormatter.Get(
                        "CharacterSummary.Health.Deprivation.CurrentBreakdown",
                        CharacterSummaryTextFormatter.FormatBreakdown(snapshot.Breakdown.kind)));
                builder.AppendLine(
                    CharacterSummaryHealthStatusTextFormatter.Get(
                        "CharacterSummary.Health.Deprivation.Cause",
                        CharacterSummaryTextFormatter.FormatDeprivation(snapshot.Breakdown.cause)));
                if (!string.IsNullOrWhiteSpace(snapshot.Breakdown.targetId))
                {
                    builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                        "CharacterSummary.Health.Deprivation.Target",
                        snapshot.Breakdown.targetId));
                }
                builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Deprivation.SuppressionResistance",
                    snapshot.Breakdown.suppressionResistance));
            }

            builder.AppendLine();
            builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Taboo.Title"));
            if (snapshot.TabooMemories == null || snapshot.TabooMemories.Count == 0)
            {
                builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Taboo.NoRecords"));
            }
            else
            {
                foreach (string memory in snapshot.TabooMemories.TakeLast(5))
                {
                    builder.AppendLine($"- {memory}");
                }
            }
        }
        else
        {
            builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Health.Empty"));
        }

        captivityPresenter.AppendDetails(builder, actor);
        builder.AppendLine();
        CharacterSurgeryUiText.AppendHealthSummary(
            builder,
            surgeryWindowService.GetHealthSummary(actor));
        AppendConsumableDetails(builder, actor);
        summaryText.text = builder.ToString().TrimEnd();
        captivityPresenter.RefreshActionButton(actor);
        RefreshConsumableButtons(actor);
        RefreshSurgeryButtons(actor);
    }

    private void RefreshSurgeryButtons(CharacterActor actor)
    {
        if (surgeryCommandButton != null)
        {
            surgeryCommandButton.interactable = actor != null;
        }

        if (automaticSurgeryButton == null)
        {
            return;
        }

        bool enabled = actor != null && surgeryWindowService.IsAutomaticEmergencyEnabled(actor);
        TMP_Text label = automaticSurgeryButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = enabled
                ? CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Surgery.AutomaticOn")
                : CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Surgery.AutomaticOff");
        }

        automaticSurgeryButton.interactable = actor != null;
        DungeonUiTheme.StyleButton(automaticSurgeryButton, selected: enabled);
    }

    private void AppendConsumableDetails(StringBuilder builder, CharacterActor actor)
    {
        builder.AppendLine();
        builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Health.Consumables.Title"));
        builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Health.Consumables.DietPolicy",
            CharacterSummaryHealthStatusTextFormatter.DietPolicy(
                consumablesQuery.GetPolicy(actor))));
        foreach (SubstanceDefinitionView substance in resourceCatalog.Substances)
        {
            CharacterSubstancePolicyState policy = consumablesQuery.GetPolicy(actor, substance.SubstanceId);
            CharacterSubstanceState state = consumablesQuery.GetState(actor, substance.SubstanceId);
            bool hasState = state.activeSeconds > 0f
                || state.tolerance > 0.01f
                || state.addiction > 0.01f
                || state.withdrawal > 0.01f
                || state.addicted;
            if (!hasState && policy.mode == SubstancePolicyMode.Forbidden)
            {
                continue;
            }

            builder.AppendLine(
                CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Consumables.SubstanceRow",
                    substance.DisplayName,
                    CharacterSummaryHealthStatusTextFormatter.SubstancePolicy(policy.mode),
                    state.tolerance,
                    state.addiction,
                    state.withdrawal,
                    state.activeSeconds > 0f
                        ? CharacterSummaryHealthStatusTextFormatter.Get(
                            "CharacterSummary.Health.Consumables.ActiveEffectSuffix",
                            state.activeSeconds)
                        : string.Empty));
        }
    }

    private void RefreshConsumableButtons(CharacterActor actor)
    {
        SetButtonLabel(
            dietPolicyButton,
            actor != null
                ? CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Button.DietCurrent",
                    CharacterSummaryHealthStatusTextFormatter.DietPolicy(
                        consumablesQuery.GetPolicy(actor)))
                : CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Button.DietPolicy"));
        if (!TryGetSelectedSubstance(out SubstanceDefinitionView substance))
        {
            SetButtonLabel(
                substanceSelectionButton,
                CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Button.NoSubstance"));
            SetButtonLabel(
                substancePolicyButton,
                CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Health.Button.NoPolicy"));
            if (substancePolicyButton != null)
            {
                substancePolicyButton.interactable = false;
            }
            return;
        }

        SetButtonLabel(substanceSelectionButton, substance.DisplayName);
        CharacterSubstancePolicyState policy = consumablesQuery.GetPolicy(actor, substance.SubstanceId);
        SetButtonLabel(
            substancePolicyButton,
            CharacterSummaryHealthStatusTextFormatter.SubstancePolicy(policy.mode));
        if (substancePolicyButton != null)
        {
            substancePolicyButton.interactable = actor != null;
        }
    }

    private bool TryGetSelectedSubstance(out SubstanceDefinitionView substance)
    {
        substance = null;
        IReadOnlyList<SubstanceDefinitionView> definitions = resourceCatalog.Substances;
        if (definitions == null || definitions.Count == 0)
        {
            return false;
        }

        selectedSubstanceIndex = Mathf.Clamp(selectedSubstanceIndex, 0, definitions.Count - 1);
        substance = definitions[selectedSubstanceIndex];
        return substance != null;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        TMP_Text label = button != null
            ? button.transform.Find("Label")?.GetComponent<TMP_Text>()
            : null;
        if (label != null)
        {
            label.text = text ?? string.Empty;
        }
    }

}
