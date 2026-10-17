using System;
using DungeonStory.Narrative.Korean;

internal sealed class TradeInventoryOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId)
    {
        TradeInventoryOutcomeKind kind =
            TradeInventoryOutcomeReader.ResolveKind(outcome);
        string signature =
            "trade-inventory:"
            + TradeInventoryOutcomeDefinitions.Get(kind).Slug
            + ":" + (int)outcome.Status
            + ":" + subjectId.Kind.Value
            + ":" + subjectId.Value;
        if (GameplayOutcomeStableIdSyntax.IsValid(signature))
            return new GameplayMemorySignature(signature);
        return new GameplayMemorySignature(
            "trade-inventory:"
            + TradeInventoryOutcomeDefinitions.Get(kind).Slug
            + ":" + (int)outcome.Status
            + ":" + subjectId.Kind.Value
            + ":h" + StableHash(subjectId.Value).ToString("x16"));
    }

    private static ulong StableHash(string value)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                hash ^= (byte)current;
                hash *= prime;
                hash ^= (byte)(current >> 8);
                hash *= prime;
            }
            return hash;
        }
    }

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        float baseSalience = 0.45f;
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink link = outcome.GetSubject(index);
            if (link.SubjectId == subjectId)
            {
                baseSalience = link.Salience;
                break;
            }
        }
        double magnitude = 0d;
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(TradeInventoryOutcomeIds.KindMetric)
                || metric.MetricId.Equals(TradeInventoryOutcomeIds.ResultRevisionMetric))
                continue;
            magnitude = Math.Max(magnitude, Math.Abs(metric.Value));
        }
        float magnitudeBonus = (float)Math.Min(0.18d, Math.Log10(1d + magnitude) * 0.035d);
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float repetitionPenalty = Math.Min(0.3f, priorMatchingCount * 0.035f);
        float agePenalty = Math.Min(0.35f, age * 0.008f);
        float salience = Math.Clamp(
            baseSalience + magnitudeBonus - repetitionPenalty - agePenalty,
            0f,
            1f);
        NarrativeMemoryTier tier = salience >= 0.88f
            ? NarrativeMemoryTier.Core
            : salience >= 0.4f
                ? NarrativeMemoryTier.Episodic
                : NarrativeMemoryTier.Recent;
        return new OutcomeMemoryEvaluation(
            salience,
            tier,
            Math.Max(evaluationDay + 3, outcome.AbsoluteDay + 3));
    }
}

internal sealed class TradeInventoryOutcomePerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class TradeInventoryOutcomeConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class TradeInventoryOutcomePerspectiveProjector :
    INarrativePerspectiveProjector
{
    private readonly IKoreanJosaFormatter josa;

    public TradeInventoryOutcomePerspectiveProjector(IKoreanJosaFormatter josa)
    {
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));
    }

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        TradeInventoryOutcomeKind kind =
            TradeInventoryOutcomeReader.ResolveKind(outcome);
        TradeInventoryOutcomeDefinition definition =
            TradeInventoryOutcomeDefinitions.Get(kind);
        GameplayOutcomeParticipant viewer = default;
        bool hasViewer = false;
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant candidate = outcome.GetParticipant(index);
            if (candidate.EntityId == perspective.ViewerId)
            {
                viewer = candidate;
                hasViewer = true;
                break;
            }
        }
        double quantity = TradeInventoryOutcomeReader.FirstMetric(
            outcome,
            TradeInventoryOutcomeIds.QuantityMetric);
        if (quantity <= 0d)
            quantity = TradeInventoryOutcomeReader.FirstMetric(
                outcome,
                TradeInventoryOutcomeIds.InputQuantityMetric);
        if (quantity <= 0d)
            quantity = TradeInventoryOutcomeReader.FirstMetric(
                outcome,
                TradeInventoryOutcomeIds.OutputQuantityMetric);
        double gold = TradeInventoryOutcomeReader.FirstMetric(
            outcome,
            TradeInventoryOutcomeIds.RevenueMetric);
        if (gold <= 0d)
            gold = TradeInventoryOutcomeReader.FirstMetric(
                outcome,
                TradeInventoryOutcomeIds.CostMetric);
        if (gold <= 0d)
            gold = TradeInventoryOutcomeReader.FirstMetric(
                outcome,
                TradeInventoryOutcomeIds.GoldMetric);
        if (gold <= 0d)
            gold = TradeInventoryOutcomeReader.FirstMetric(
                outcome,
                TradeInventoryOutcomeIds.LossValueMetric);
        string suffix = quantity > 0d ? $" · 수량 {quantity:0.##}" : string.Empty;
        if (gold > 0d) suffix += $" · 금액 {gold:0.##}";
        if (hasViewer
            && definition.TryGetPerspectiveFrame(
                viewer.RoleId,
                out TradeInventoryPerspectiveFrame viewerFrame))
        {
            return new NarrativeView(
                outcome.OutcomeId,
                perspective.Kind,
                viewerFrame.ViewerText + suffix + ".",
                "trade-inventory-v2+" + josa.FormatterVersion,
                true);
        }

        GameplayOutcomeParticipant primary =
            TradeInventoryOutcomeReader.RequireParticipant(
                outcome,
                definition.NarrativeSubjectRole);
        KoreanJosaFormatResult named = josa.Format(new KoreanJosaRequest(
            primary.DisplayName,
            KoreanJosaKind.Subject));
        string text = named.RequiresNeutralFrame
            ? $"{TradeInventoryOutcomeIds.KoreanLabel(kind)} · {primary.DisplayName.DisplayText} · {definition.GlobalPredicate}{suffix}."
            : $"{named.Text} {definition.GlobalPredicate}{suffix}.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "trade-inventory-v2+" + josa.FormatterVersion,
            named.RequiresNeutralFrame);
    }
}

internal static class TradeInventoryOutcomeReader
{
    internal static TradeInventoryOutcomeKind ResolveKind(
        in GameplayOutcomeReadView outcome)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (!metric.MetricId.Equals(TradeInventoryOutcomeIds.KindMetric))
                continue;
            int raw = checked((int)metric.Value);
            if (Math.Abs(metric.Value - raw) > double.Epsilon)
                throw new InvalidOperationException(
                    "Trade/inventory result.kind must be an exact enum value.");
            TradeInventoryOutcomeKind kind = (TradeInventoryOutcomeKind)raw;
            TradeInventoryOutcomeDefinitions.Get(kind);
            return kind;
        }
        throw new InvalidOperationException(
            "Trade/inventory outcome is missing result.kind.");
    }

    internal static GameplayOutcomeParticipant RequireParticipant(
        in GameplayOutcomeReadView outcome,
        GameplayRoleId role)
    {
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant participant = outcome.GetParticipant(index);
            if (participant.RoleId.Equals(role))
                return participant;
        }
        throw new InvalidOperationException(
            "Trade/inventory narrative subject role is missing: " + role.Value);
    }

    internal static double FirstMetric(
        in GameplayOutcomeReadView outcome,
        GameplayMetricId id)
    {
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(id))
                return metric.Value;
        }
        return 0d;
    }
}

public sealed class TradeInventoryOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private readonly TradeInventoryOutcomePerspectiveProjector projector;
    private readonly TradeInventoryOutcomeMemoryPolicy memory = new();
    private readonly TradeInventoryOutcomePerceptionPolicy perception = new();
    private readonly TradeInventoryOutcomeConsolidator consolidator = new();

    public TradeInventoryOutcomeDescriptor(IKoreanJosaFormatter josa)
    {
        projector = new TradeInventoryOutcomePerspectiveProjector(josa);
    }

    public GameplayOutcomeTypeId OutcomeTypeId => TradeInventoryOutcomeIds.Result;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        TradeInventoryOutcomeSchema.IsKnownRole(roleId);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        TradeInventoryOutcomeSchema.IsKnownMetric(metricId, unitId);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        TradeInventoryOutcomeSchema.ValidateRead(outcome);
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => consolidator;
}
