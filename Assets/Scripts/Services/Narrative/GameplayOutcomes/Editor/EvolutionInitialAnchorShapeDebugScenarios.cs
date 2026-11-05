using System;

public static class EvolutionInitialAnchorShapeDebugScenarios
{
    // These synthetic pages test descriptor shape only, not anchor identity or world validity.
    public static void VerifyForBatch()
    {
        VerifyProductionAnchorReads();
        Verify(new ApparelChangeOutcomeDescriptor(), 1, 2, 1, 1, 0);
        Verify(new ApparelPhysicalOutcomeDescriptor(), 1, 2, 2, 1, 3);
        Verify(new ApparelPhysicalOutcomeDescriptor(), 2, 5, 3, 2, 3);
        Verify(new ProductQualityOutcomeDescriptor(), 1, 3, 1, 2, 1);
        Verify(new AcquiredTraitInferenceOutcomeDescriptor(), 1, 2, 1, 1, 3);
        Verify(new MemoryErasureBossAwardOutcomeDescriptor(), 1, 1, 1, 1, 1);
        Verify(new AcquiredTraitReactionOutcomeDescriptor(), 1, 1, 1, 1, 2);
        Verify(new FacilityEvolutionOutcomeDescriptor(), 1, 4, 1, 1, 0);
        Verify(new FacilityEvolutionOutcomeDescriptor(), 1, 4, 1, 1, 2);
        Verify(new MemoryErasureOutcomeDescriptor(), 1, 4, 1, 1, 1);
        Verify(new MemoryErasureOutcomeDescriptor(), 1, 4, 1, 1, 2);
    }

    private static void VerifyProductionAnchorReads()
    {
        var page = new GameplayOutcomeValuePage(8, 8, 8, 8, 8, 0, 0, false);
        var currentOwner = new GameplayEntityId(new GameplayEntityKindId("character"), "character:qa:current");
        var initialOwner = new GameplayEntityId(new GameplayEntityKindId("character"), "character:qa:initial");
        var currentAnchor = new NarrativeEvidenceReference("qa-current", "anchor:qa:current");
        var initialAnchor = new NarrativeEvidenceReference("qa-initial", "anchor:qa:initial");
        page.AnchorCount = 2;
        page.InitialAnchorCount = 1;
        page.Anchors[0] = new GameplayOutcomeAnchorRow(currentOwner, currentAnchor);
        page.InitialAnchors[0] = new GameplayOutcomeAnchorRow(initialOwner, initialAnchor);
        var view = new GameplayOutcomeReadView(page);
        if (view.ProductionAnchorCount != 2 || !view.GetProductionAnchor(0).Equals(currentAnchor)
            || view.GetProductionAnchorSubject(0) != currentOwner)
            throw new InvalidOperationException("Draft producer anchors did not use current authoring input.");
        page.HasImmutablePayloadDigest = true;
        foreach (int dynamicCount in new[] { 0, 2, 4 })
        {
            page.AnchorCount = dynamicCount;
            if (view.ProductionAnchorCount != 1 || !view.GetProductionAnchor(0).Equals(initialAnchor)
                || view.GetProductionAnchorSubject(0) != initialOwner)
                throw new InvalidOperationException("Sealed producer identity changed with mutable evidence retention.");
        }
        bool rejected = false;
        try { view.GetProductionAnchor(1); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("Producer read escaped its original immutable anchor count.");
    }

    private static void Verify(
        IGameplayOutcomeDescriptor descriptor,
        int participants,
        int metrics,
        int subjects,
        int tags,
        int initialAnchors)
    {
        var page = new GameplayOutcomeValuePage(8, 8, 8, 8, 8, 0, 0, false)
        {
            OutcomeTypeId = descriptor.OutcomeTypeId,
            ParticipantCount = participants,
            MetricCount = metrics,
            SubjectCount = subjects,
            TagCount = tags,
            InitialAnchorCount = initialAnchors,
            HasImmutablePayloadDigest = true
        };
        var view = new GameplayOutcomeReadView(page);
        foreach (int currentAnchors in new[] { 0, initialAnchors, initialAnchors + 1 })
        {
            page.AnchorCount = currentAnchors;
            Require(descriptor.Validate(in view).Valid, descriptor, "valid initial shape");
        }

        page.InitialAnchorCount = 4;
        page.AnchorCount = initialAnchors;
        Require(!descriptor.Validate(in view).Valid, descriptor, "invalid initial anchor count");

        page.InitialAnchorCount = initialAnchors;
        page.ParticipantCount = participants + 7;
        Require(!descriptor.Validate(in view).Valid, descriptor, "invalid participant count");

        page.HasImmutablePayloadDigest = false;
        page.ParticipantCount = participants;
        page.AnchorCount = initialAnchors;
        foreach (int draftInitialAnchors in new[] { 0, 4 })
        {
            page.InitialAnchorCount = draftInitialAnchors;
            Require(descriptor.Validate(in view).Valid, descriptor, "valid draft shape");
        }

        page.AnchorCount = 4;
        Require(!descriptor.Validate(in view).Valid, descriptor, "invalid draft anchor count");
        page.AnchorCount = initialAnchors;
        page.ParticipantCount = participants + 7;
        Require(!descriptor.Validate(in view).Valid, descriptor, "invalid draft participant count");
    }

    private static void Require(bool condition, IGameplayOutcomeDescriptor descriptor, string scenario)
    {
        if (!condition)
            throw new InvalidOperationException($"{descriptor.GetType().Name}: {scenario}");
    }
}
