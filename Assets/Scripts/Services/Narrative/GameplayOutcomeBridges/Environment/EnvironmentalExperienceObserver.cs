using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Interpret physiological outcomes, never temperature labels or storage salience.</summary>
public sealed class EnvironmentalExperienceObserver : IGameplayExperienceObserver
{
    public string ObserverId => "experience:environment-exposure:v3";
    public IReadOnlyList<string> OutcomeTypes { get; } = new[]
    {
        EnvironmentOutcomeIds.EnvironmentalExposureThreshold.Value
    };

    public GameplayExperienceAssessmentSnapshot Observe(GameplayOutcomeEvidenceBindingSnapshot fact)
    {
        if (!TryRead(fact, out var before, out var after, out var damage, out _, out _))
            return Assess(GameplayExperienceCategory.Unassessed, "exposure-vitals-unavailable-or-inconsistent");
        if (before > 0d && after == 0d)
            return Assess(GameplayExperienceCategory.Transformative, "recorded-vitality-exhausted");
        if (damage == 0d)
            return Assess(GameplayExperienceCategory.Routine, "exposure-without-vitality-loss");
        // This is loss relative to the recorded pre-event vitality, NOT maximum
        // HP, a probability of death, or an inferred permanent disability.
        double loss = damage / before;
        if (loss >= .5d)
            return Assess(GameplayExperienceCategory.Intense, "lost-half-recorded-vitality");
        if (loss >= .1d)
            return Assess(GameplayExperienceCategory.Meaningful, "substantial-recorded-vitality-loss");
        return Assess(GameplayExperienceCategory.Mild, "minor-recorded-vitality-loss");
    }

    public bool CanJoin(GameplayOutcomeEvidenceBindingSnapshot previous,
        GameplayOutcomeEvidenceBindingSnapshot next)
    {
        // Conservative observed continuity. A gap, location/day change, an intervening
        // different event, or a stable endpoint ends the group. No causal edge is invented.
        if (previous.lastDay != next.firstDay || previous.locationId != next.locationId
            || previous.roomId != next.roomId || previous.locationX != next.locationX
            || previous.locationY != next.locationY
            || !TryRead(previous, out _, out var previousAfter, out var previousDamage,
                out var previousBandBefore, out var bandAfter)
            || !TryRead(next, out var nextBefore, out _, out var nextDamage,
                out var bandBefore, out var nextBandAfter))
            return false;
        if (bandAfter <= 0 || Math.Abs(previousAfter - nextBefore) > .0001d) return false;
        if (bandAfter == bandBefore) return true;
        // The owner emits band-transition followed by damage-tick from ONE state
        // update. Both receipts retain the same pre/post exposure interval, rather
        // than the damage receipt pretending it began after the band's transition.
        return previousDamage == 0d && nextDamage > 0d
            && previousBandBefore == bandBefore && bandAfter == nextBandAfter
            && HasEventKind(previous, "band-transition") && HasEventKind(next, "damage-tick")
            && SameExposureInterval(previous, next);
    }

    private static bool HasEventKind(GameplayOutcomeEvidenceBindingSnapshot fact, string kind)
    {
        var matches = fact.facts?.Where(value => value != null
            && value.factId == EnvironmentOutcomeIds.EventKindFact.Value).ToArray();
        return matches?.Length == 1 && matches[0].value == kind;
    }

    private static bool SameExposureInterval(GameplayOutcomeEvidenceBindingSnapshot left,
        GameplayOutcomeEvidenceBindingSnapshot right) => new[]
        {
            EnvironmentOutcomeIds.ColdBeforeMetric.Value, EnvironmentOutcomeIds.ColdAfterMetric.Value,
            EnvironmentOutcomeIds.HeatBeforeMetric.Value, EnvironmentOutcomeIds.HeatAfterMetric.Value,
            EnvironmentOutcomeIds.AirborneBeforeMetric.Value, EnvironmentOutcomeIds.AirborneAfterMetric.Value
        }.All(id => ReadMetric(left, id, out double before)
            && ReadMetric(right, id, out double after) && before.Equals(after));

    private GameplayExperienceAssessmentSnapshot Assess(GameplayExperienceCategory category, string reason)
    {
        var assessment = GameplayExperienceImportancePolicy.Assess(ObserverId, category, reason,
            EnvironmentOutcomeIds.HealthBeforeMetric.Value,
            EnvironmentOutcomeIds.HealthAfterMetric.Value,
            EnvironmentOutcomeIds.DamageMetric.Value);
        assessment.explanationKo = category switch
        {
            GameplayExperienceCategory.Routine => "환경 노출 수치만 변했고 실제 체력 손실은 없음.",
            GameplayExperienceCategory.Mild => "직전 체력의 10% 미만 실제 손실. 단순 온도 이동과 구분.",
            GameplayExperienceCategory.Meaningful => "직전 체력의 10% 이상 50% 미만 실제 손실.",
            GameplayExperienceCategory.Intense => "직전 체력의 50% 이상 실제 손실. 영구 장애를 추가 추정하지 않음.",
            GameplayExperienceCategory.Transformative => "직전 체력이 양수에서 0으로 소진됨. 사망 여부는 별도 결과가 필요.",
            _ => "노출 전후 체력·피해·단계의 구조화 근거가 없거나 서로 모순됨."
        };
        return assessment;
    }

    private static bool TryRead(GameplayOutcomeEvidenceBindingSnapshot fact,
        out double before, out double after, out double damage, out int bandBefore, out int bandAfter)
    {
        before = after = damage = 0d;
        bandBefore = bandAfter = 0;
        if (!Enum.IsDefined(typeof(GameplayOutcomeStatus), fact.status)
            || fact.sourceKind != "exact" || fact.subjectKindId != EnvironmentOutcomeIds.CharacterKind.Value
            || !ReadMetric(fact, EnvironmentOutcomeIds.HealthBeforeMetric.Value, out before)
            || !ReadMetric(fact, EnvironmentOutcomeIds.HealthAfterMetric.Value, out after)
            || !ReadMetric(fact, EnvironmentOutcomeIds.DamageMetric.Value, out damage)
            || !ReadMetric(fact, EnvironmentOutcomeIds.BandBeforeMetric.Value, out double previousBand)
            || !ReadMetric(fact, EnvironmentOutcomeIds.BandAfterMetric.Value, out double currentBand)
            || before < 0 || after < 0 || damage < 0 || after > before
            || Math.Abs(before - after - damage) > .001d
            || previousBand != Math.Floor(previousBand) || currentBand != Math.Floor(currentBand)
            || previousBand < 0 || previousBand > 4 || currentBand < 0 || currentBand > 4)
            return false;
        bandBefore = (int)previousBand;
        bandAfter = (int)currentBand;
        return damage == 0 || before > 0;
    }

    private static bool ReadMetric(GameplayOutcomeEvidenceBindingSnapshot fact, string id, out double value)
    {
        value = 0d;
        var matches = fact.metrics?.Where(metric => metric != null && metric.metricId == id
            && metric.referenceKindId == fact.subjectKindId && metric.referenceId == fact.subjectId).ToArray();
        if (matches == null || matches.Length != 1 || matches[0].sampleCount != 1
            || double.IsNaN(matches[0].value) || double.IsInfinity(matches[0].value)) return false;
        string expectedUnit = id == EnvironmentOutcomeIds.BandBeforeMetric.Value
            || id == EnvironmentOutcomeIds.BandAfterMetric.Value
            ? EnvironmentOutcomeIds.EnumUnit.Value : EnvironmentOutcomeIds.PointUnit.Value;
        if (matches[0].unitId != expectedUnit) return false;
        value = matches[0].value;
        return true;
    }
}
