using System;
using System.Collections.Generic;
using DungeonStory.Content.CoreSession;

/// <summary>
/// Frozen version-one section adapter inside the V18 manifest. Validation is
/// completed before the aggregate candidate is published.
/// </summary>
public sealed class ExperiencePacingSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonExperiencePacingSaveData,
        ExperiencePacingAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "run.experience-pacing";
    private readonly IExperiencePacingRuntime runtime;
    private readonly CoreSessionRulesDefinition rules;

    public ExperiencePacingSaveSection(
        IExperiencePacingRuntime runtime,
        ICoreSessionRulesProvider rulesProvider)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        rules = (rulesProvider
                ?? throw new ArgumentNullException(nameof(rulesProvider)))
            .CoreSessionRules
            ?? throw new InvalidOperationException(
                "Core-session rules are not authored.");
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonExperiencePacingSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[] { RunFlowSaveSection.Id };

    protected override DungeonExperiencePacingSaveData CapturePayload() =>
        runtime.Capture();

    protected override ExperiencePacingAggregateState BuildRestoreCandidate(
        DungeonExperiencePacingSaveData payload)
    {
        DungeonGameRestoreReport report = new();
        ValidatePayload(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Experience-pacing restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        return runtime.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        ExperiencePacingAggregateState candidate) =>
        runtime.PublishRestoreCandidate(candidate);

    private void ValidatePayload(
        DungeonExperiencePacingSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null || payload.introducedConcepts == null)
        {
            report.AddError(
                "Experience-pacing payload or concept list is null.");
            return;
        }
        if (payload.version != DungeonExperiencePacingSaveData.CurrentVersion)
        {
            report.AddError(
                $"Experience-pacing payload version {payload.version} is unsupported.");
        }

        int allowedMask = rules.Rehearsals.Count == 0
            ? 0
            : (1 << rules.Rehearsals.Count) - 1;
        if (payload.currentDay < 1
            || (payload.scheduledRehearsalMask & ~allowedMask) != 0
            || (payload.completedRehearsalMask & ~allowedMask) != 0
            || (payload.completedRehearsalMask
                & ~payload.scheduledRehearsalMask) != 0)
        {
            report.AddError(
                "Experience-pacing day or rehearsal masks are invalid.");
        }

        int activeBit = ResolveSavedRehearsalBit(
            payload.activeRehearsalDay);
        if (payload.activeRehearsalDay != 0
            && (activeBit == 0
                || payload.currentDay < payload.activeRehearsalDay
                || (payload.scheduledRehearsalMask & activeBit) == 0
                || (payload.completedRehearsalMask & activeBit) != 0))
        {
            report.AddError(
                "Experience-pacing active rehearsal is inconsistent.");
        }
        if (!MaskDaysFitCurrentDay(
                payload.scheduledRehearsalMask,
                payload.currentDay))
        {
            report.AddError(
                "Experience-pacing rehearsal history is ahead of the current day.");
        }

        int previous = -1;
        bool hasDefense = false;
        foreach (int raw in payload.introducedConcepts)
        {
            if (!Enum.IsDefined(typeof(ExperienceEventConcept), raw)
                || raw <= previous)
            {
                report.AddError(
                    "Experience-pacing concepts must be defined, unique, and ordered.");
                break;
            }
            previous = raw;
            hasDefense |= raw == (int)ExperienceEventConcept.Defense;
        }
        if (payload.scheduledRehearsalMask != 0 && !hasDefense)
        {
            report.AddError(
                "Experience-pacing rehearsal history requires the Defense concept.");
        }
    }

    private int ResolveSavedRehearsalBit(int day)
    {
        for (int index = 0; index < rules.Rehearsals.Count; index++)
        {
            if (rules.Rehearsals[index]?.Day == day)
            {
                return 1 << index;
            }
        }
        return 0;
    }

    private bool MaskDaysFitCurrentDay(int mask, int currentDay)
    {
        for (int index = 0; index < rules.Rehearsals.Count; index++)
        {
            CoreRehearsalRule rule = rules.Rehearsals[index];
            if ((mask & (1 << index)) != 0
                && (rule == null || currentDay < rule.Day))
            {
                return false;
            }
        }
        return true;
    }
}
