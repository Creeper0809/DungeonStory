using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static StartPartyPreparationPresentation;
using static StartPartyPreparationViewFactory;

internal enum StartPartyDetailTab
{
    Identity,
    Aptitude,
    Skill
}

internal sealed class StartPartyMemberDetailRenderer
{
    private readonly StartPartyPreparationViewFactory viewFactory;
    private readonly Action refresh;
    private readonly Action<StartPartyMemberPreparation, StartPartyRerollGroup?> reroll;
    private readonly Action<StartPartyMemberPreparation, int> swap;
    private StartPartyDetailTab selectedTab = StartPartyDetailTab.Identity;
    private Transform tooltipRoot;
    private GameObject hoverTooltip;

    public StartPartyMemberDetailRenderer(
        StartPartyPreparationViewFactory viewFactory,
        Action refresh,
        Action<StartPartyMemberPreparation, StartPartyRerollGroup?> reroll,
        Action<StartPartyMemberPreparation, int> swap)
    {
        this.viewFactory = viewFactory
            ?? throw new ArgumentNullException(nameof(viewFactory));
        this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        this.reroll = reroll ?? throw new ArgumentNullException(nameof(reroll));
        this.swap = swap ?? throw new ArgumentNullException(nameof(swap));
    }

    public void ResetTab()
    {
        selectedTab = StartPartyDetailTab.Identity;
    }

    public void Render(
        Transform parent,
        Transform root,
        StartPartyMemberPreparation member,
        string readyLabel)
    {
        tooltipRoot = root;
        if (member == null)
        {
            TMP_Text empty = CreateText(parent, "NoMember", "\uC900\uBE44 \uC911\uC778 \uCE90\uB9AD\uD130\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", 22f, TextAlignmentOptions.Center);
            Stretch(empty.rectTransform);
            return;
        }

        Image portraitFrame = CreateImage(parent, "MemberPortraitFrame", DungeonUiTheme.SurfaceRaised);
        SetRect(portraitFrame.rectTransform, new Vector2(0.045f, 0.755f), new Vector2(0.17f, 0.955f));
        Image portrait = CreateImage(parent, "MemberPortrait", Color.clear);
        portrait.transform.SetParent(portraitFrame.transform, false);
        Stretch(portrait.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        portrait.sprite = member.CharacterData != null ? member.CharacterData.characterSprite : null;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;

        TMP_Text name = CreateText(parent, "MemberName", ResolveMemberName(member), 32f, TextAlignmentOptions.MidlineLeft);
        SetRect(name.rectTransform, new Vector2(0.195f, 0.895f), new Vector2(0.62f, 0.965f));
        name.fontStyle = FontStyles.Bold;

        string subtitle = $"{member.RosterLabel}  ·  {member.CharacterData?.SpeciesTag ?? "-"}  ·  Lv.{member.Progression?.Level ?? 1}";
        TMP_Text sub = CreateText(parent, "MemberSubtitle", subtitle, 17f, TextAlignmentOptions.MidlineLeft);
        SetRect(sub.rectTransform, new Vector2(0.197f, 0.845f), new Vector2(0.62f, 0.895f));
        sub.color = DungeonUiTheme.TextSecondary;

        TMP_Text role = CreateText(parent, "MemberRole", readyLabel, 17f, TextAlignmentOptions.MidlineRight);
        SetRect(role.rectTransform, new Vector2(0.62f, 0.895f), new Vector2(0.885f, 0.965f));
        role.color = DungeonUiTheme.TextSecondary;

        if (!member.IsOwner)
        {
            CreateDiceButton(
                parent,
                "PreparationFullRerollDice_" + member.Index,
                () => reroll(member, null),
                new Vector2(0.905f, 0.89f),
                new Vector2(0.955f, 0.955f),
                member.IdentityRerollsRemaining + member.AptitudeRerollsRemaining + member.SkillRerollsRemaining > 0,
                "\uC804\uCCB4 \uB9AC\uB864");
        }

        CreateTab(parent, member, StartPartyDetailTab.Identity, "\uC815\uCCB4\uC131", 0.195f);
        CreateTab(parent, member, StartPartyDetailTab.Aptitude, "\uC7AC\uB2A5", 0.345f);
        CreateTab(parent, member, StartPartyDetailTab.Skill, "\uC2A4\uD0AC", 0.495f);

        switch (selectedTab)
        {
            case StartPartyDetailTab.Identity:
                RenderIdentityDetail(member, parent);
                break;
            case StartPartyDetailTab.Aptitude:
                RenderAptitudeDetail(member, parent);
                break;
            case StartPartyDetailTab.Skill:
                RenderSkillDetail(member, parent);
                break;
        }
    }

    private void CreateTab(
        Transform parent,
        StartPartyMemberPreparation member,
        StartPartyDetailTab tab,
        string label,
        float left)
    {
        Button button = CreateButton(
            parent,
            $"PreparationTab_{member.Index}_{tab}",
            label,
            () =>
            {
                selectedTab = tab;
                refresh();
            },
            new Vector2(left, 0.78f),
            new Vector2(left + 0.13f, 0.85f),
            selectedTab == tab);
        button.image.color = selectedTab == tab
            ? DungeonUiTheme.Accent
            : DungeonUiTheme.SurfaceRaised;
    }

    private void RenderIdentityDetail(StartPartyMemberPreparation member, Transform parent)
    {
        CharacterGrowthState growth = member.Progression?.GrowthState;
        CharacterStartingProfileState profile = growth?.startingProfile;
        Transform basics = CreatePanel(parent, "IdentityBasics", new Vector2(0.045f, 0.38f), new Vector2(0.46f, 0.72f), false);
        basics.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        CreateSectionTitle(basics, "\uAE30\uBCF8 \uC815\uBCF4", member, StartPartyRerollGroup.Identity);
        CreateInfoRow(basics, "\uC774\uB984", ResolveMemberName(member), 0.63f);
        CreateInfoRow(basics, "\uC5ED\uD560", member.RosterLabel, 0.46f);
        CreateInfoRow(basics, "\uC885\uC871", member.CharacterData?.SpeciesTag ?? "-", 0.29f);
        CreateInfoRow(
            basics,
            "\uCD9C\uC2E0\u00B7\uC774\uB825",
            profile?.prepared == true
                ? $"{profile.originDisplayName} \u00B7 {profile.historyDisplayName}"
                : growth?.origin ?? "-",
            0.12f);

        Transform traits = CreatePanel(parent, "IdentityTraits", new Vector2(0.485f, 0.38f), new Vector2(0.955f, 0.72f), false);
        traits.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        TMP_Text traitTitle = CreateText(traits, "TraitTitle", "\uD2B9\uC131", 20f, TextAlignmentOptions.MidlineLeft);
        SetRect(traitTitle.rectTransform, new Vector2(0.05f, 0.78f), new Vector2(0.65f, 0.94f));
        traitTitle.fontStyle = FontStyles.Bold;

        IReadOnlyList<CharacterTraitSO> resolvedTraits = member.Progression?.ResolveSelectedTraits()
            ?? Array.Empty<CharacterTraitSO>();
        if (resolvedTraits.Count == 0)
        {
            TMP_Text none = CreateText(traits, "TraitNone", "-", 17f, TextAlignmentOptions.TopLeft);
            SetRect(none.rectTransform, new Vector2(0.05f, 0.1f), new Vector2(0.94f, 0.73f));
            none.color = DungeonUiTheme.TextSecondary;
            return;
        }

        for (int i = 0; i < resolvedTraits.Count && i < 4; i++)
        {
            RenderTraitChip(traits, resolvedTraits[i], 0.58f - i * 0.17f);
        }
    }

    private void RenderAptitudeDetail(StartPartyMemberPreparation member, Transform parent)
    {
        CharacterGrowthState growth = member.Progression?.GrowthState;
        CharacterStartingProfileState profile = growth?.startingProfile;
        Transform summary = CreatePanel(parent, "AptitudeSummary", new Vector2(0.045f, 0.57f), new Vector2(0.955f, 0.72f), false);
        summary.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        CreateSectionTitle(summary, "\uC7AC\uB2A5", member, StartPartyRerollGroup.Aptitude);
        TMP_Text potential = CreateText(
            summary,
            "PotentialValue",
            $"\uC7A0\uC7AC\uB825  {PotentialLabel(growth?.potentialGrade ?? CharacterPotentialGrade.Ordinary)}",
            17f,
            TextAlignmentOptions.MidlineLeft);
        SetRect(potential.rectTransform, new Vector2(0.04f, 0.22f), new Vector2(0.24f, 0.68f));
        potential.color = DungeonUiTheme.Accent;
        potential.fontStyle = FontStyles.Bold;

        IReadOnlyList<CharacterStartingProficiencyExperience> starts =
            growth?.startingProficiencies;
        starts ??= Array.Empty<CharacterStartingProficiencyExperience>();
        int total = starts.Where(value => value != null).Sum(value => value.experience);
        string ageText = profile?.prepared == true
            ? $"{StartingAgeBandLabel(profile.ageBand)} {profile.biologicalAgeYears:0.#}\uC138"
                + (profile.initialAgeConditionIds.Count > 0
                    ? $" \u00B7 \uAC74\uAC15 \uBB38\uC81C {profile.initialAgeConditionIds.Count}"
                    : string.Empty)
            : "\uB098\uC774 -";
        TMP_Text age = CreateText(
            summary,
            "StartingAge",
            ageText,
            16f,
            TextAlignmentOptions.MidlineLeft);
        SetRect(age.rectTransform, new Vector2(0.24f, 0.22f), new Vector2(0.43f, 0.68f));

        string primary = profile?.prepared == true
            ? ProficiencyLabel(new CharacterProficiencyId(profile.primaryProficiencyId))
            : "-";
        string secondary = profile?.prepared == true
            ? ProficiencyLabel(new CharacterProficiencyId(profile.secondaryProficiencyId))
            : "-";
        TMP_Text specialization = CreateText(
            summary,
            "StartingSpecializations",
            $"\uC8FC {primary} x{CharacterProficiencySpecializationRules.PrimaryLearningMultiplier:0.00}"
                + $"  /  \uBD80 {secondary} x{CharacterProficiencySpecializationRules.SecondaryLearningMultiplier:0.00}",
            16f,
            TextAlignmentOptions.MidlineLeft);
        SetRect(specialization.rectTransform, new Vector2(0.43f, 0.22f), new Vector2(0.78f, 0.68f));

        TMP_Text totalText = CreateText(
            summary,
            "ProficiencyTotal",
            $"XP {total}  /  \uC0C1\uD55C {profile?.proficiencyCap ?? 0}",
            15f,
            TextAlignmentOptions.MidlineRight);
        SetRect(totalText.rectTransform, new Vector2(0.78f, 0.22f), new Vector2(0.95f, 0.68f));
        totalText.color = DungeonUiTheme.TextSecondary;

        Transform stats = CreatePanel(parent, "AptitudeStats", new Vector2(0.045f, 0.08f), new Vector2(0.955f, 0.54f), false);
        stats.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        for (int i = 0; i < BuiltInCharacterProficiencyIds.All.Count; i++)
        {
            CharacterProficiencyId proficiencyId =
                BuiltInCharacterProficiencyIds.All[i];
            int value = starts.FirstOrDefault(item => item != null
                && string.Equals(
                    item.proficiencyId,
                    proficiencyId.Value,
                    StringComparison.Ordinal))?.experience ?? 0;
            int column = i / 3;
            int row = i % 3;
            float left = 0.04f + column * 0.315f;
            float top = 0.82f - row * 0.28f;
            RenderProficiencyBar(stats, proficiencyId, value, left, top);
        }
    }

    private void RenderSkillDetail(StartPartyMemberPreparation member, Transform parent)
    {
        Transform slots = CreatePanel(parent, "SkillSlots", new Vector2(0.045f, 0.38f), new Vector2(0.955f, 0.72f), false);
        slots.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        CreateSectionTitle(slots, "\uC2A4\uD0AC", member, StartPartyRerollGroup.Skill);

        if (member.IsOwner)
        {
            IReadOnlyList<CharacterSkillInstance> ownerSkills = CharacterOwnerFixedSkillUtility.GetSkills(member.CharacterData);
            for (int i = 0; i < ownerSkills.Count && i < CharacterOwnerFixedSkillUtility.FixedSlotCount; i++)
            {
                RenderSkillCard(slots, ownerSkills[i], i, 0.08f + i * 0.225f, 0.12f, 0.205f, true);
            }

            TMP_Text hint = CreateText(
                parent,
                "OwnerSkillHint",
                "\uC0AC\uC7A5\uC740 \uACE0\uC815 \uAD8C\uB2A5\uC73C\uB85C \uB7F0\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4. \uC77C\uBC18 \uC131\uC7A5 \uC2A4\uD0AC\uC740 \uB7F0 \uC911 \uD574\uAE08\uB429\uB2C8\uB2E4.",
                18f,
                TextAlignmentOptions.MidlineLeft);
            SetRect(hint.rectTransform, new Vector2(0.055f, 0.25f), new Vector2(0.94f, 0.34f));
            hint.color = DungeonUiTheme.TextSecondary;
            hint.textWrappingMode = TextWrappingModes.Normal;
            return;
        }

        RenderSlotSummary(slots, "\uC885\uC871 \uC561\uD2F0\uBE0C", member.CharacterData?.SpeciesTag ?? "-", 0.64f);
        RenderSlotSummary(slots, "\uCD08\uAE30 \uC561\uD2F0\uBE0C", member.Progression.ActiveSkills.FirstOrDefault()?.displayName ?? "\uC0DD\uC131 \uD544\uC694", 0.43f);
        RenderSlotSummary(slots, "\uD328\uC2DC\uBE0C", member.Progression.PassiveSkills.FirstOrDefault()?.displayName ?? "\uC0DD\uC131 \uD544\uC694", 0.22f);

        Transform generated = CreatePanel(parent, "GeneratedStartSkills", new Vector2(0.045f, 0.08f), new Vector2(0.955f, 0.35f), false);
        generated.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;
        RenderGeneratedStartSkills(member, generated);
    }

    private void RenderGeneratedStartSkills(StartPartyMemberPreparation member, Transform parent)
    {
        TMP_Text title = CreateText(parent, "GeneratedSkillTitle", "\uCD08\uAE30 \uC2A4\uD0AC", 18f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.035f, 0.76f), new Vector2(0.55f, 0.95f));
        title.fontStyle = FontStyles.Bold;

        CharacterSkillInstance active = member.Progression?.ActiveSkills.FirstOrDefault();
        CharacterSkillInstance passive = member.Progression?.PassiveSkills.FirstOrDefault();
        if (active == null || passive == null)
        {
            TMP_Text waiting = CreateText(parent, "SkillGenerationMissing", "\uCD08\uAE30 \uC2A4\uD0AC\uC744 \uB2E4\uC2DC \uAD6C\uC131\uD558\uACE0 \uC788\uC2B5\uB2C8\uB2E4.", 18f, TextAlignmentOptions.MidlineLeft);
            SetRect(waiting.rectTransform, new Vector2(0.035f, 0.2f), new Vector2(0.8f, 0.68f));
            waiting.color = DungeonUiTheme.Warning;
            return;
        }

        TMP_Text hint = CreateText(parent, "GeneratedSkillHint", "\uCCAB \uC561\uD2F0\uBE0C\uB294 \uC120\uD0DD\uD558\uC9C0 \uC54A\uACE0 \uC815\uCCB4\uC131\uACFC \uC7AC\uB2A5\uC5D0 \uB9DE\uAC8C \uC989\uC2DC \uD655\uC815\uB429\uB2C8\uB2E4.", 14f, TextAlignmentOptions.MidlineRight);
        SetRect(hint.rectTransform, new Vector2(0.48f, 0.76f), new Vector2(0.955f, 0.95f));
        hint.color = DungeonUiTheme.TextSecondary;
        hint.textWrappingMode = TextWrappingModes.Normal;

        RenderSkillCard(parent, active, 0, 0.05f, 0.12f, 0.42f, false);
        RenderSkillCard(parent, passive, 1, 0.53f, 0.12f, 0.42f, false);
    }

    private void RenderRerollButtons(StartPartyMemberPreparation member, Transform parent)
    {
        CreateButton(parent, "PreparationFullReroll_" + member.Index, "\uC804\uCCB4 \uB9AC\uB864", () => reroll(member, null),
            new Vector2(0.045f, 0.04f), new Vector2(0.18f, 0.1f));
        CreateButton(parent, "PreparationIdentityReroll_" + member.Index, $"\uC815\uCCB4\uC131 {member.IdentityRerollsRemaining}", () => reroll(member, StartPartyRerollGroup.Identity),
            new Vector2(0.195f, 0.04f), new Vector2(0.34f, 0.1f));
        CreateButton(parent, "PreparationAptitudeReroll_" + member.Index, $"\uC7AC\uB2A5 {member.AptitudeRerollsRemaining}", () => reroll(member, StartPartyRerollGroup.Aptitude),
            new Vector2(0.355f, 0.04f), new Vector2(0.5f, 0.1f));
        CreateButton(parent, "PreparationSkillReroll_" + member.Index, $"\uC2A4\uD0AC {member.SkillRerollsRemaining}", () => reroll(member, StartPartyRerollGroup.Skill),
            new Vector2(0.515f, 0.04f), new Vector2(0.66f, 0.1f));
    }

    private void RenderSwapButtons(StartPartyMemberPreparation member, Transform parent)
    {
        CreateButton(parent, "PreparationSwap_" + member.Index + "_1", "\uC9C1\uC6D0 1\uACFC \uAD50\uCCB4", () => swap(member, 1),
            new Vector2(0.69f, 0.04f), new Vector2(0.82f, 0.1f));
        CreateButton(parent, "PreparationSwap_" + member.Index + "_2", "\uC9C1\uC6D0 2\uC640 \uAD50\uCCB4", () => swap(member, 2),
            new Vector2(0.835f, 0.04f), new Vector2(0.965f, 0.1f));
    }

    private void CreateSectionTitle(
        Transform parent,
        string label,
        StartPartyMemberPreparation member,
        StartPartyRerollGroup? rerollGroup)
    {
        TMP_Text title = CreateText(parent, "SectionTitle", label, 20f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.045f, 0.78f), new Vector2(0.7f, 0.95f));
        title.fontStyle = FontStyles.Bold;
        if (member == null || member.IsOwner || !rerollGroup.HasValue)
        {
            return;
        }

        int remaining = rerollGroup.Value switch
        {
            StartPartyRerollGroup.Identity => member.IdentityRerollsRemaining,
            StartPartyRerollGroup.Aptitude => member.AptitudeRerollsRemaining,
            StartPartyRerollGroup.Skill => member.SkillRerollsRemaining,
            _ => 0
        };
        CreateDiceButton(
            parent,
            $"Preparation{rerollGroup.Value}RerollDice_{member.Index}",
            () => reroll(member, rerollGroup.Value),
            new Vector2(0.875f, 0.76f),
            new Vector2(0.955f, 0.94f),
            remaining > 0,
            $"{label} \uB9AC\uB864 {remaining}");
    }

    private void CreateInfoRow(Transform parent, string label, string value, float bottom)
    {
        TMP_Text labelText = CreateText(parent, "InfoLabel_" + label, label, 15f, TextAlignmentOptions.MidlineLeft);
        SetRect(labelText.rectTransform, new Vector2(0.05f, bottom), new Vector2(0.27f, bottom + 0.13f));
        labelText.color = DungeonUiTheme.TextSecondary;
        TMP_Text valueText = CreateText(parent, "InfoValue_" + label, value, 17f, TextAlignmentOptions.MidlineLeft);
        SetRect(valueText.rectTransform, new Vector2(0.28f, bottom), new Vector2(0.94f, bottom + 0.13f));
        valueText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void RenderTraitChip(Transform parent, CharacterTraitSO trait, float bottom)
    {
        Transform chip = CreatePanel(parent, "TraitChip_" + (trait != null ? trait.id : 0), new Vector2(0.05f, bottom), new Vector2(0.94f, bottom + 0.145f), false);
        chip.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.03f, 0.85f);
        string titleText = trait != null
            ? $"{trait.traitName}  [{StartPartyPreparationPresentation.TraitRarityLabel(trait.selectionRarity)}]"
            : "-";
        TMP_Text title = CreateText(chip, "TraitName", titleText, 15f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.04f, 0.57f), new Vector2(0.95f, 0.96f));
        title.color = DungeonUiTheme.Accent;
        title.fontStyle = FontStyles.Bold;
        TMP_Text description = CreateText(chip, "TraitDescription", trait != null ? trait.description : string.Empty, 12f, TextAlignmentOptions.TopLeft);
        SetRect(description.rectTransform, new Vector2(0.04f, 0.06f), new Vector2(0.95f, 0.56f));
        description.color = DungeonUiTheme.TextSecondary;
        description.textWrappingMode = TextWrappingModes.Normal;

        EventTrigger trigger = chip.gameObject.AddComponent<EventTrigger>();
        AddEventTrigger(trigger, EventTriggerType.PointerEnter, () => ShowTraitTooltip(trait));
        AddEventTrigger(trigger, EventTriggerType.PointerExit, HideTooltip);
    }

    private void ShowTraitTooltip(CharacterTraitSO trait)
    {
        if (trait == null || tooltipRoot == null)
        {
            return;
        }

        HideTooltip();
        hoverTooltip = new GameObject("TraitStatTooltip", typeof(RectTransform), typeof(Image));
        hoverTooltip.transform.SetParent(tooltipRoot, false);
        RectTransform rect = hoverTooltip.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.57f, 0.11f), new Vector2(0.955f, 0.34f));
        Image panel = hoverTooltip.GetComponent<Image>();
        panel.color = new Color(0.02f, 0.025f, 0.03f, 0.97f);
        panel.raycastTarget = false;

        TMP_Text title = CreateText(hoverTooltip.transform, "TooltipTitle", trait.traitName, 19f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.045f, 0.74f), new Vector2(0.95f, 0.94f));
        title.color = DungeonUiTheme.Accent;
        title.fontStyle = FontStyles.Bold;

        TMP_Text body = CreateText(hoverTooltip.transform, "TooltipBody", BuildTraitTooltipText(trait), 14f, TextAlignmentOptions.TopLeft);
        SetRect(body.rectTransform, new Vector2(0.045f, 0.08f), new Vector2(0.95f, 0.72f));
        body.color = DungeonUiTheme.TextPrimary;
        body.textWrappingMode = TextWrappingModes.Normal;
        hoverTooltip.transform.SetAsLastSibling();
    }

    public void HideTooltip()
    {
        if (hoverTooltip == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(hoverTooltip);
        hoverTooltip = null;
    }

    private static void AddEventTrigger(EventTrigger trigger, EventTriggerType eventId, Action action)
    {
        if (trigger == null)
        {
            return;
        }

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventId };
        entry.callback.AddListener(_ => action?.Invoke());
        trigger.triggers.Add(entry);
    }

    private void RenderProficiencyBar(
        Transform parent,
        CharacterProficiencyId proficiencyId,
        int experience,
        float left,
        float top)
    {
        string suffix = proficiencyId.Value.Replace(':', '_');
        TMP_Text label = CreateText(
            parent,
            "ProficiencyLabel_" + suffix,
            ProficiencyLabel(proficiencyId),
            15f,
            TextAlignmentOptions.MidlineLeft);
        SetRect(label.rectTransform, new Vector2(left, top - 0.12f), new Vector2(left + 0.13f, top));
        label.color = DungeonUiTheme.TextSecondary;

        TMP_Text number = CreateText(
            parent,
            "ProficiencyValue_" + suffix,
            $"{ProficiencyBandLabel(experience * ProficiencyProgressionRules.MilliPerExperience)}  {experience}",
            13f,
            TextAlignmentOptions.MidlineRight);
        SetRect(number.rectTransform, new Vector2(left + 0.215f, top - 0.12f), new Vector2(left + 0.30f, top));

        Image back = CreateImage(parent, "ProficiencyBack_" + suffix, new Color(0.02f, 0.025f, 0.03f, 0.9f));
        SetRect(back.rectTransform, new Vector2(left + 0.14f, top - 0.085f), new Vector2(left + 0.21f, top - 0.035f));
        Image fill = CreateImage(back.transform, "Fill", DungeonUiTheme.Accent);
        long milliExperience = experience
            * ProficiencyProgressionRules.MilliPerExperience;
        CharacterProficiencyBandSnapshot band =
            ProficiencyProgressionRules.ResolveBand(milliExperience);
        float normalized = Mathf.Clamp01(
            (milliExperience - band.MinimumMilliExperience)
            / (float)Math.Max(
                1L,
                band.NextMilliExperience - band.MinimumMilliExperience));
        SetRect(fill.rectTransform, Vector2.zero, new Vector2(normalized, 1f));
    }

    private void RenderSlotSummary(Transform parent, string label, string value, float bottom)
    {
        Transform row = CreatePanel(parent, "SkillSlot_" + label, new Vector2(0.05f, bottom), new Vector2(0.95f, bottom + 0.16f), false);
        row.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.03f, 0.85f);
        TMP_Text labelText = CreateText(row, "Label", label, 15f, TextAlignmentOptions.MidlineLeft);
        SetRect(labelText.rectTransform, new Vector2(0.035f, 0.12f), new Vector2(0.3f, 0.88f));
        labelText.color = DungeonUiTheme.TextSecondary;
        TMP_Text valueText = CreateText(row, "Value", value, 18f, TextAlignmentOptions.MidlineLeft);
        SetRect(valueText.rectTransform, new Vector2(0.32f, 0.12f), new Vector2(0.95f, 0.88f));
    }

    private void RenderSkillCard(
        Transform parent,
        CharacterSkillInstance skill,
        int index,
        float left,
        float bottom,
        float width,
        bool fixedSkill)
    {
        Transform card = CreatePanel(parent, "OwnerSkillCard_" + index, new Vector2(left, bottom), new Vector2(left + width, bottom + 0.52f), false);
        card.GetComponent<Image>().color = fixedSkill
            ? new Color(DungeonUiTheme.Accent.r, DungeonUiTheme.Accent.g, DungeonUiTheme.Accent.b, 0.25f)
            : new Color(0.02f, 0.025f, 0.03f, 0.85f);
        TMP_Text name = CreateText(card, "Name", skill != null ? skill.displayName : "-", 16f, TextAlignmentOptions.TopLeft);
        SetRect(name.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.92f));
        name.fontStyle = FontStyles.Bold;
        name.textWrappingMode = TextWrappingModes.Normal;
        TMP_Text desc = CreateText(card, "Description", skill != null ? skill.description : string.Empty, 13f, TextAlignmentOptions.TopLeft);
        SetRect(desc.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.55f));
        desc.color = DungeonUiTheme.TextSecondary;
        desc.textWrappingMode = TextWrappingModes.Normal;
    }

    private Button CreateDiceButton(
        Transform parent,
        string name,
        Action clicked,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool interactable,
        string accessibleLabel) =>
        viewFactory.CreateDiceButton(
            parent,
            name,
            clicked,
            anchorMin,
            anchorMax,
            interactable,
            accessibleLabel);

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float size,
        TextAlignmentOptions alignment) =>
        viewFactory.CreateText(parent, name, text, size, alignment);

    private Image CreateImage(Transform parent, string name, Color color) =>
        viewFactory.CreateImage(parent, name, color);

    private Transform CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool raised) =>
        viewFactory.CreatePanel(parent, name, anchorMin, anchorMax, raised);

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Action clicked,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool selected = false) =>
        viewFactory.CreateButton(
            parent,
            name,
            label,
            clicked,
            anchorMin,
            anchorMax,
            selected);
}
