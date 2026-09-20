using System;
using System.Globalization;
using System.Linq;

/// <summary>Exact physical wear observed inside the durable-use rollback boundary.</summary>
public sealed class ResearchEquipmentOutcomeEvidence
{
    public ResearchEquipmentOutcomeEvidence(DurableFacilityEquipmentUseContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        StackId = context.Before.StackId;
        ItemId = context.Before.ItemId.Value;
        FacilityId = context.Slot.OwnerFacilityId.Value;
        AssignmentSequence = context.Slot.AssignmentSequence;
        RevisionBefore = context.Before.ContentRevision;
        RevisionAfter = context.After.ContentRevision;
        Before = Read(context.Before, "current");
        After = Read(context.After, "current");
        RequestedWear = context.WearAmount;
        double maximum = Read(context.Before, "maximum");
        if (!GameplayOutcomeStableIdSyntax.IsValid(StackId)
            || !GameplayOutcomeStableIdSyntax.IsValid(ItemId)
            || !GameplayOutcomeStableIdSyntax.IsValid(FacilityId)
            || !string.Equals(context.Requirement.ItemId.Value, ItemId, StringComparison.Ordinal)
            || context.Before.Quantity != context.After.Quantity
            || AssignmentSequence <= 0 || RevisionBefore < 0 || RevisionAfter <= RevisionBefore
            || !double.IsFinite(maximum) || maximum <= 0 || maximum != Read(context.After, "maximum")
            || !double.IsFinite(Before) || !double.IsFinite(After) || !double.IsFinite(RequestedWear)
            || Before <= 0 || Before > maximum || After < 0 || After > Before || RequestedWear <= 0
            || Math.Abs(After - Math.Max(0, Before - RequestedWear)) > 0.0000001d)
            throw new ArgumentException("research-equipment-wear-context-invalid", nameof(context));
    }

    public string StackId { get; }
    public string ItemId { get; }
    public string FacilityId { get; }
    public long AssignmentSequence { get; }
    public long RevisionBefore { get; }
    public long RevisionAfter { get; }
    public double Before { get; }
    public double After { get; }
    public double RequestedWear { get; }
    public double Spent => Before - After;

    private static double Read(DurableFacilityEquipmentUseSubject subject, string key)
    {
        var component = subject.Components.SingleOrDefault(value =>
            value.ComponentTypeId == ItemInstanceComponentIds.Durability)
            ?? throw new ArgumentException("research-equipment-durability-missing");
        var values = component.Values.Where(value => value.Key == key).ToArray();
        if (values.Length != 1 || values[0].Kind != ItemStateValueKind.Decimal)
            throw new ArgumentException("research-equipment-durability-invalid:" + key);
        return values[0].DecimalValue;
    }
}

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

    public static bool Write(in ResearchEquipmentOutcomeEvidence equipment, ref OutcomeWriteBuilder builder)
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

    public static bool WriteFacts(in ResearchEquipmentOutcomeEvidence equipment, ref OutcomeWriteBuilder builder) =>
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
