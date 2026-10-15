using System;
using System.Globalization;

internal static class ResearchEquipmentOutcomeEncoding
{
    public const int FactCount = 4;
    public static readonly GameplayEntityKindId Kind = new("item-stack");
    public static readonly GameplayRoleId Role = new("research-tool");
    public static readonly GameplayMetricUnitId Unit = new("durability-point");
    public static readonly GameplayMetricId Before = new("research.tool-durability-before");
    public static readonly GameplayMetricId After = new("research.tool-durability-after");
    public static readonly GameplayMetricId Spent = new("research.tool-durability-spent");
    public static readonly GameplayMetricId Requested = new("research.tool-durability-requested");
    public static readonly GameplayOutcomeFactId Definition = new("research.tool-definition");
    private static readonly GameplayOutcomeFactId SlotSequence = new("research.tool-slot-sequence");
    private static readonly GameplayOutcomeFactId RevisionBefore = new("research.tool-revision-before");
    private static readonly GameplayOutcomeFactId RevisionAfter = new("research.tool-revision-after");

    public static bool HasEquipment(in GameplayOutcomeReadView outcome) => outcome.ParticipantCount > 0
        && outcome.GetParticipant(outcome.ParticipantCount - 1).RoleId.Equals(Role);

    public static bool IsMetric(GameplayMetricId id, GameplayMetricUnitId unit) => unit.Equals(Unit)
        && (id.Equals(Before) || id.Equals(After) || id.Equals(Spent) || id.Equals(Requested));

    public static bool Write(in ResearchEquipmentUseSnapshot equipment, ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId id = new(Kind, equipment.StackId);
        return builder.AddParticipant(new GameplayOutcomeParticipant(id, Role, GameplayParticipationKind.Direct,
                true, ResearchWorkOutcomeNames.Snapshot(equipment.StackId, equipment.ItemId)))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(id, .55f, NarrativeMemoryTier.Episodic, false, false, 0))
            && builder.AddMetric(new GameplayOutcomeMetric(Before, equipment.Before, Unit, id))
            && builder.AddMetric(new GameplayOutcomeMetric(After, equipment.After, Unit, id))
            && builder.AddMetric(new GameplayOutcomeMetric(Spent, equipment.Spent, Unit, id))
            && builder.AddMetric(new GameplayOutcomeMetric(Requested, equipment.RequestedWear, Unit, id));
    }

    public static bool WriteFacts(in ResearchEquipmentUseSnapshot equipment, ref OutcomeWriteBuilder builder) =>
        builder.AddFact(new GameplayOutcomeFact(Definition, equipment.ItemId))
        && builder.AddFact(new GameplayOutcomeFact(SlotSequence, equipment.AssignmentSequence.ToString(CultureInfo.InvariantCulture)))
        && builder.AddFact(new GameplayOutcomeFact(RevisionBefore, equipment.RevisionBefore.ToString(CultureInfo.InvariantCulture)))
        && builder.AddFact(new GameplayOutcomeFact(RevisionAfter, equipment.RevisionAfter.ToString(CultureInfo.InvariantCulture)));

    public static bool Validate(in GameplayOutcomeReadView outcome, ref int factOffset)
    {
        if (!HasEquipment(outcome)) return outcome.MetricCount == 4;
        if (outcome.MetricCount != 8 || outcome.FactCount < factOffset + FactCount) return false;
        var entity = outcome.GetParticipant(outcome.ParticipantCount - 1).EntityId;
        var before = outcome.GetMetric(4);
        var after = outcome.GetMetric(5);
        var spent = outcome.GetMetric(6);
        var requested = outcome.GetMetric(7);
        if (!Metric(before, Before, entity) || !Metric(after, After, entity)
            || !Metric(spent, Spent, entity) || !Metric(requested, Requested, entity)
            || before.Value <= 0 || after.Value < 0 || after.Value > before.Value || requested.Value <= 0
            || Math.Abs(spent.Value - (before.Value - after.Value)) > .0000001d
            || Math.Abs(after.Value - Math.Max(0, before.Value - requested.Value)) > .0000001d)
            return false;
        if (!outcome.GetFact(factOffset).FactId.Equals(Definition)
            || !GameplayOutcomeStableIdSyntax.IsValid(outcome.GetFact(factOffset).Value)
            || !PositiveLong(outcome.GetFact(factOffset + 1), SlotSequence, out _)
            || !NonnegativeLong(outcome.GetFact(factOffset + 2), RevisionBefore, out long oldRevision)
            || !PositiveLong(outcome.GetFact(factOffset + 3), RevisionAfter, out long newRevision)
            || newRevision <= oldRevision) return false;
        factOffset += FactCount;
        return true;
    }

    private static bool Metric(GameplayOutcomeMetric metric, GameplayMetricId id, GameplayEntityId entity) =>
        metric.MetricId.Equals(id) && metric.UnitId.Equals(Unit) && metric.DefinitionOrInstanceId == entity
        && double.IsFinite(metric.Value);

    private static bool PositiveLong(GameplayOutcomeFact fact, GameplayOutcomeFactId id, out long value) =>
        NonnegativeLong(fact, id, out value) && value > 0;

    private static bool NonnegativeLong(GameplayOutcomeFact fact, GameplayOutcomeFactId id, out long value)
    {
        value = 0;
        return fact.FactId.Equals(id)
            && long.TryParse(fact.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value >= 0 && value.ToString(CultureInfo.InvariantCulture) == fact.Value;
    }
}
