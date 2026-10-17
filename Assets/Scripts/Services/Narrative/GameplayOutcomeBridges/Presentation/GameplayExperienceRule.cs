using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public readonly struct GameplayExperienceJudgment
{
    public GameplayExperienceJudgment(GameplayExperienceCategory category, string reason, string explanation)
    { Category = category; Reason = reason; Explanation = explanation; }
    public GameplayExperienceCategory Category { get; }
    public string Reason { get; }
    public string Explanation { get; }
}

/// <summary>A reusable action component: domain decisions live in concrete policies, not this dispatcher.</summary>
public abstract class GameplayActionExperiencePolicy : IGameplayExperienceObserver
{
    protected GameplayActionExperiencePolicy(string outcomeType, string description)
    {
        OutcomeType = GameplayOutcomeStableIdSyntax.Require(outcomeType, nameof(outcomeType));
        Description = string.IsNullOrWhiteSpace(description)
            ? throw new ArgumentException("An action policy needs a description.", nameof(description)) : description;
        ObserverId = "experience:" + OutcomeType + ":v3";
        OutcomeTypes = new[] { OutcomeType };
    }
    public string OutcomeType { get; }
    public string Description { get; }
    public string ObserverId { get; }
    public IReadOnlyList<string> OutcomeTypes { get; }
    protected abstract GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts);
    public virtual GameplayExperienceAssessmentSnapshot Observe(GameplayOutcomeEvidenceBindingSnapshot fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        if (fact.outcomeTypeId != OutcomeType)
            throw new ArgumentException("The receipt belongs to another action policy.", nameof(fact));
        var context = new GameplayExperienceFacts(fact);
        GameplayExperienceJudgment judgment;
        try
        {
            context.ValidateShape();
            judgment = Evaluate(context);
        }
        catch (GameplayExperienceEvidenceException failure)
        {
            judgment = new(GameplayExperienceCategory.Unassessed, "required-evidence-invalid",
                "판정에 필요한 구조화 근거가 없거나 잘못됨: " + failure.Message);
        }
        var result = GameplayExperienceImportancePolicy.Assess(ObserverId, judgment.Category,
            judgment.Reason, context.MetricIds.OrderBy(value => value, StringComparer.Ordinal).ToArray());
        result.explanationKo = judgment.Explanation;
        result.evidenceFactIds = context.FactIds.OrderBy(value => value, StringComparer.Ordinal).ToList();
        result.evidenceRoleIds = context.RoleIds.OrderBy(value => value, StringComparer.Ordinal).ToList();
        return result;
    }
    public virtual bool CanJoin(GameplayOutcomeEvidenceBindingSnapshot previous,
        GameplayOutcomeEvidenceBindingSnapshot next) => false;
}

/// <summary>Strict typed evidence reader, recording the fields used for a judgment.</summary>
public sealed class GameplayExperienceFacts
{
    public GameplayExperienceFacts(GameplayOutcomeEvidenceBindingSnapshot value) =>
        Binding = value ?? throw new ArgumentNullException(nameof(value));
    public GameplayOutcomeEvidenceBindingSnapshot Binding { get; }
    internal HashSet<string> MetricIds { get; } = new(StringComparer.Ordinal);
    internal HashSet<string> FactIds { get; } = new(StringComparer.Ordinal);
    internal HashSet<string> RoleIds { get; } = new(StringComparer.Ordinal);
    public bool Success => Binding.status == (int)GameplayOutcomeStatus.Succeeded;
    public bool Character => Binding.subjectKindId == "character";
    public bool Partial => Binding.status == (int)GameplayOutcomeStatus.PartiallySucceeded;

    public void ValidateShape()
    {
        if (!Enum.IsDefined(typeof(GameplayOutcomeStatus), Binding.status)) Fail("status");
        if (Binding.sourceKind != "exact") Fail("exact receipt required; compacted history lacks per-event context");
        if (string.IsNullOrWhiteSpace(Binding.subjectKindId) || string.IsNullOrWhiteSpace(Binding.subjectId))
            Fail("subject identity");
        if (Binding.participants == null || Binding.metrics == null || Binding.facts == null)
            Fail("typed collections");
    }

    public bool HasRole(params string[] roles)
    {
        foreach (var participant in Binding.participants ?? new())
            if (participant != null && participant.entityKindId == Binding.subjectKindId
                && participant.entityId == Binding.subjectId && roles.Contains(participant.roleId))
            { RoleIds.Add(participant.roleId); return true; }
        return false;
    }

    public string Fact(string id)
    {
        FactIds.Add(id);
        var values = Binding.facts?.Where(value => value != null && value.factId == id).ToArray();
        if (values == null || values.Length != 1 || string.IsNullOrWhiteSpace(values[0].value)) Fail(id);
        return values[0].value;
    }

    public T EnumFact<T>(string id) where T : struct, Enum
    {
        string text = Fact(id);
        if (!Enum.TryParse(text, out T value) || !Enum.IsDefined(typeof(T), value)) Fail(id + " enum");
        return value;
    }

    public double Number(string id, string unit, string role = null, bool anyReference = false)
    {
        MetricIds.Add(id);
        string kind = Binding.subjectKindId, entity = Binding.subjectId;
        if (role != null)
        {
            RoleIds.Add(role);
            var participants = Binding.participants?.Where(value => value != null && value.roleId == role)
                .Select(value => (value.entityKindId, value.entityId)).Distinct().ToArray();
            if (participants == null || participants.Length != 1) Fail("reference-role:" + role);
            (kind, entity) = participants[0];
        }
        var values = Binding.metrics?.Where(value => value != null && value.metricId == id
            && (anyReference || value.referenceKindId == kind && value.referenceId == entity)).ToArray();
        if (values == null || values.Length != 1 || values[0].unitId != unit
            || values[0].sampleCount != 1 || double.IsNaN(values[0].value)
            || double.IsInfinity(values[0].value)) Fail(id + " value/unit/reference");
        return values[0].value;
    }

    public double FactNumber(string id)
    {
        if (!double.TryParse(Fact(id), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || double.IsNaN(value) || double.IsInfinity(value)) Fail(id + " numeric fact");
        return value;
    }

    public bool HasMetric(string id) => Binding.metrics?.Any(value => value != null && value.metricId == id) == true;
    public static void Fail(string field) => throw new GameplayExperienceEvidenceException(field);

    public GameplayExperienceJudgment Judge(GameplayExperienceCategory level, string reason, string explanation) =>
        new(level, reason, explanation);
    public GameplayExperienceJudgment Technical(string reason = "no-applied-personal-consequence") =>
        Judge(GameplayExperienceCategory.NotExperience, reason,
            "명령·예약·처리 결과만 확인됨. 실제 개인 피해나 성취로 평가하지 않음.");
    public GameplayExperienceJudgment Action(GameplayExperienceCategory success, string description,
        GameplayExperienceCategory failure = GameplayExperienceCategory.NotExperience) =>
        Judge(Success ? success : failure, Success ? "confirmed-action" : "non-successful-action",
            Success ? description : "완료되지 않은 처리. 별도 실제 결과가 없으므로 강렬한 경험으로 가산하지 않음.");

    public GameplayExperienceJudgment Damage(double before, double after, double damage, string description)
    {
        if (before < 0 || after < 0 || damage < 0 || after > before
            || Math.Abs(before - after - damage) > .001d) Fail("health delta");
        var category = damage == 0 ? GameplayExperienceCategory.Routine
            : after == 0 && before > 0 ? GameplayExperienceCategory.Transformative
            : damage / before >= .5d ? GameplayExperienceCategory.Intense
            : damage / before >= .1d ? GameplayExperienceCategory.Meaningful
            : GameplayExperienceCategory.Mild;
        return Judge(category, "confirmed-health-loss", description
            + " 직전 체력 대비 실제 손실로 평가하며 사망·영구 장애를 추가 추정하지 않음.");
    }

    public GameplayExperienceJudgment AppliedDamage(double damage, string description)
    {
        if (damage < 0) Fail("negative damage");
        return Judge(damage > 0 ? GameplayExperienceCategory.Mild : GameplayExperienceCategory.Routine,
            "damage-without-severity-context", description
            + " 실제 피해만 확인되며 생명 위기·영구 손상의 근거가 없어 고득점으로 승격하지 않음.");
    }
}

public sealed class GameplayExperienceEvidenceException : Exception
{
    public GameplayExperienceEvidenceException(string field) : base(field) { }
}
