using System;
using System.Collections.Generic;

public sealed class GameplayOutcomeBufferLimits
{
    public const int AbsoluteMaximumPageCount = 32768;
    public const int AbsoluteMaximumParticipantsPerOutcome = 2048;
    public const int AbsoluteMaximumMetricsPerOutcome = 1024;
    public const int AbsoluteMaximumSubjectsPerOutcome = 2048;
    public const int AbsoluteMaximumTagsPerOutcome = 256;
    public const int AbsoluteMaximumAnchorsPerOutcome = 1024;
    public const int AbsoluteMaximumProvenancePerOutcome = 2048;
    public const int AbsoluteMaximumFactsPerOutcome = 64;
    public const int MaximumFactValueUtf16Length = 4096;
    public const int MaximumDisplayTextUtf16Length = 256;
    public const int MaximumPronunciationValueUtf16Length = 128;
    public const int MaximumLocaleLength = 32;
    public const int AbsoluteMaximumCompactedMetricKeys = 8192;
    public const int AbsoluteMaximumKnownResultKeys = 1048576;

    public GameplayOutcomeBufferLimits(
        int smallPageCount = 1024,
        int largePageCount = 8,
        int smallParticipants = 32,
        int smallMetrics = 16,
        int smallSubjects = 32,
        int smallTags = 16,
        int smallAnchors = 16,
        int smallProvenance = 16,
        int smallFacts = 8,
        int largeParticipants = AbsoluteMaximumParticipantsPerOutcome,
        int largeMetrics = AbsoluteMaximumMetricsPerOutcome,
        int largeSubjects = AbsoluteMaximumSubjectsPerOutcome,
        int largeTags = AbsoluteMaximumTagsPerOutcome,
        int largeAnchors = AbsoluteMaximumAnchorsPerOutcome,
        int largeProvenance = AbsoluteMaximumProvenancePerOutcome,
        int largeFacts = AbsoluteMaximumFactsPerOutcome,
        int knownResultKeyCapacity = 65536,
        int retainedSmallPageCount = 4096,
        int retainedLargePageCount = 8)
    {
        RequireRange(smallPageCount, 1, AbsoluteMaximumPageCount, nameof(smallPageCount));
        RequireRange(largePageCount, 0, AbsoluteMaximumPageCount, nameof(largePageCount));
        if ((long)smallPageCount + largePageCount > AbsoluteMaximumPageCount)
            throw new ArgumentOutOfRangeException(nameof(largePageCount));
        RequireRange(smallParticipants, 0, AbsoluteMaximumParticipantsPerOutcome, nameof(smallParticipants));
        RequireRange(smallMetrics, 0, AbsoluteMaximumMetricsPerOutcome, nameof(smallMetrics));
        RequireRange(smallSubjects, 0, AbsoluteMaximumSubjectsPerOutcome, nameof(smallSubjects));
        RequireRange(smallTags, 0, AbsoluteMaximumTagsPerOutcome, nameof(smallTags));
        RequireRange(smallAnchors, 0, AbsoluteMaximumAnchorsPerOutcome, nameof(smallAnchors));
        RequireRange(smallProvenance, 0, AbsoluteMaximumProvenancePerOutcome, nameof(smallProvenance));
        RequireRange(smallFacts, 0, AbsoluteMaximumFactsPerOutcome, nameof(smallFacts));
        RequireRange(largeParticipants, smallParticipants, AbsoluteMaximumParticipantsPerOutcome, nameof(largeParticipants));
        RequireRange(largeMetrics, smallMetrics, AbsoluteMaximumMetricsPerOutcome, nameof(largeMetrics));
        RequireRange(largeSubjects, smallSubjects, AbsoluteMaximumSubjectsPerOutcome, nameof(largeSubjects));
        RequireRange(largeTags, smallTags, AbsoluteMaximumTagsPerOutcome, nameof(largeTags));
        RequireRange(largeAnchors, smallAnchors, AbsoluteMaximumAnchorsPerOutcome, nameof(largeAnchors));
        RequireRange(largeProvenance, smallProvenance, AbsoluteMaximumProvenancePerOutcome, nameof(largeProvenance));
        RequireRange(largeFacts, smallFacts, AbsoluteMaximumFactsPerOutcome, nameof(largeFacts));
        RequireRange(
            knownResultKeyCapacity,
            smallPageCount + largePageCount,
            AbsoluteMaximumKnownResultKeys,
            nameof(knownResultKeyCapacity));
        RequireRange(retainedSmallPageCount, 1, AbsoluteMaximumPageCount,
            nameof(retainedSmallPageCount));
        RequireRange(retainedLargePageCount, 0, AbsoluteMaximumPageCount,
            nameof(retainedLargePageCount));
        if ((long)retainedSmallPageCount + retainedLargePageCount
            > AbsoluteMaximumPageCount)
            throw new ArgumentOutOfRangeException(nameof(retainedLargePageCount));

        SmallPageCount = smallPageCount;
        LargePageCount = largePageCount;
        SmallParticipants = smallParticipants;
        SmallMetrics = smallMetrics;
        SmallSubjects = smallSubjects;
        SmallTags = smallTags;
        SmallAnchors = smallAnchors;
        SmallProvenance = smallProvenance;
        SmallFacts = smallFacts;
        LargeParticipants = largeParticipants;
        LargeMetrics = largeMetrics;
        LargeSubjects = largeSubjects;
        LargeTags = largeTags;
        LargeAnchors = largeAnchors;
        LargeProvenance = largeProvenance;
        LargeFacts = largeFacts;
        KnownResultKeyCapacity = knownResultKeyCapacity;
        RetainedSmallPageCount = retainedSmallPageCount;
        RetainedLargePageCount = retainedLargePageCount;
    }

    public int SmallPageCount { get; }
    public int LargePageCount { get; }
    public int SmallParticipants { get; }
    public int SmallMetrics { get; }
    public int SmallSubjects { get; }
    public int SmallTags { get; }
    public int SmallAnchors { get; }
    public int SmallProvenance { get; }
    public int SmallFacts { get; }
    public int LargeParticipants { get; }
    public int LargeMetrics { get; }
    public int LargeSubjects { get; }
    public int LargeTags { get; }
    public int LargeAnchors { get; }
    public int LargeProvenance { get; }
    public int LargeFacts { get; }
    public int KnownResultKeyCapacity { get; }
    public int RetainedSmallPageCount { get; }
    public int RetainedLargePageCount { get; }
    public int TotalPageCount => SmallPageCount + LargePageCount;

    public static GameplayOutcomeBufferLimits Default { get; } = new GameplayOutcomeBufferLimits();

    public bool CanFit(in OutcomeWriteRequirements requirements) =>
        requirements.ParticipantCount <= LargeParticipants
        && requirements.MetricCount <= LargeMetrics
        && requirements.SubjectCount <= LargeSubjects
        && requirements.TagCount <= LargeTags
        && requirements.AnchorCount <= LargeAnchors
        && requirements.ProvenanceCount <= LargeProvenance
        && requirements.FactCount <= LargeFacts;

    internal bool FitsSmall(in OutcomeWriteRequirements requirements) =>
        requirements.ParticipantCount <= SmallParticipants
        && requirements.MetricCount <= SmallMetrics
        && requirements.SubjectCount <= SmallSubjects
        && requirements.TagCount <= SmallTags
        && requirements.AnchorCount <= SmallAnchors
        && requirements.ProvenanceCount <= SmallProvenance
        && requirements.FactCount <= SmallFacts;

    private static void RequireRange(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(name);
    }
}

internal enum GameplayOutcomePageState
{
    Free = 0,
    Reserved = 1,
    Prepared = 2,
    Committed = 3,
    DeliveryFaultPending = 4,
    PublishedAwaitingAcknowledgement = 5,
    PublishedAcknowledged = 6
}

internal readonly struct GameplayOutcomeAnchorRow
{
    public GameplayOutcomeAnchorRow(GameplayEntityId subjectId, NarrativeEvidenceReference reference)
    {
        SubjectId = subjectId;
        Reference = reference;
    }
    public GameplayEntityId SubjectId { get; }
    public NarrativeEvidenceReference Reference { get; }
}

internal sealed class GameplayOutcomeValuePage
{
    public GameplayOutcomeValuePage(
        int participantCapacity,
        int metricCapacity,
        int subjectCapacity,
        int tagCapacity,
        int anchorCapacity,
        int provenanceCapacity,
        int factCapacity,
        bool large,
        bool detached = false)
    {
        Participants = new GameplayOutcomeParticipant[participantCapacity];
        Metrics = new GameplayOutcomeMetric[metricCapacity];
        Subjects = new GameplayOutcomeSubjectLink[subjectCapacity];
        InitialSubjects = new GameplayOutcomeSubjectLink[subjectCapacity];
        Tags = new GameplayOutcomeTagId[tagCapacity];
        Anchors = new GameplayOutcomeAnchorRow[anchorCapacity];
        InitialAnchors = new GameplayOutcomeAnchorRow[anchorCapacity];
        Provenance = new GameplayOutcomeProvenanceReference[provenanceCapacity];
        Facts = new GameplayOutcomeFact[factCapacity];
        IsLarge = large;
        IsDetached = detached;
    }

    public int Generation;
    public GameplayOutcomePageState State;
    public bool IsLarge { get; }
    public bool IsDetached { get; }
    public GameplayOutcomeId OutcomeId;
    public GameplayResultKey ResultKey;
    public GameplayOutcomeTypeId OutcomeTypeId;
    public GameplayOperationId OperationId;
    public long Sequence;
    public int AbsoluteDay;
    public GameplayOutcomeStatus Status;
    public long WorldEpoch;
    public long OwnerRevision;
    public long CommitGeneration;
    public long PublishedRevision;
    public int PublishedListSlot;
    public GameplayLocationReference Location;
    public GameplayOutcomeCausation Causation;
    public NarrativeMemoryTier StorageTier;
    public int AnchorRevision;
    public long AnchorReservationNonce;
    public bool AnchorReservationActive;
    public long InfluenceReservationNonce;
    public bool InfluenceReservationActive;
    public int RetainedReservationSlot = -1;
    public bool DueIndexDirty;
    public int DeliveryFaultCount;
    public string LastDeliveryFaultCode;
    public string ImmutablePayloadHash;
    public byte[] ImmutablePayloadDigest { get; } = new byte[GameplayOutcomeCanonicalHash.Sha256ByteCount];
    public bool HasImmutablePayloadDigest;
    public GameplayOutcomeParticipant[] Participants { get; }
    public GameplayOutcomeMetric[] Metrics { get; }
    public GameplayOutcomeSubjectLink[] Subjects { get; }
    public GameplayOutcomeSubjectLink[] InitialSubjects { get; }
    public GameplayOutcomeTagId[] Tags { get; }
    public GameplayOutcomeAnchorRow[] Anchors { get; }
    public GameplayOutcomeAnchorRow[] InitialAnchors { get; }
    public GameplayOutcomeProvenanceReference[] Provenance { get; }
    public GameplayOutcomeFact[] Facts { get; }
    public int ParticipantCount;
    public int MetricCount;
    public int SubjectCount;
    public int InitialSubjectCount;
    public int TagCount;
    public int AnchorCount;
    public int InitialAnchorCount;
    public int ProvenanceCount;
    public int FactCount;

    public bool HasCapacity(in OutcomeWriteRequirements requirements) =>
        requirements.ParticipantCount <= Participants.Length
        && requirements.MetricCount <= Metrics.Length
        && requirements.SubjectCount <= Subjects.Length
        && requirements.TagCount <= Tags.Length
        && requirements.AnchorCount <= Anchors.Length
        && requirements.ProvenanceCount <= Provenance.Length
        && requirements.FactCount <= Facts.Length;

    public void Reserve(in OutcomeWriteRequirements requirements)
    {
        Generation++;
        if (Generation <= 0)
            Generation = 1;
        State = GameplayOutcomePageState.Reserved;
        OutcomeId = default;
        ResultKey = requirements.ResultKey;
        OutcomeTypeId = requirements.OutcomeTypeId;
        OperationId = requirements.OperationId;
        Sequence = 0L;
        AbsoluteDay = requirements.AbsoluteDay;
        Status = requirements.Status;
        WorldEpoch = requirements.WorldEpoch;
        OwnerRevision = requirements.OwnerRevision;
        CommitGeneration = -1L;
        PublishedRevision = 0L;
        PublishedListSlot = -1;
        Location = default;
        Causation = default;
        StorageTier = NarrativeMemoryTier.Recent;
        AnchorRevision = 0;
        AnchorReservationActive = false;
        InfluenceReservationActive = false;
        RetainedReservationSlot = -1;
        DueIndexDirty = false;
        DeliveryFaultCount = 0;
        LastDeliveryFaultCode = string.Empty;
        ImmutablePayloadHash = string.Empty;
        Array.Clear(ImmutablePayloadDigest, 0, ImmutablePayloadDigest.Length);
        HasImmutablePayloadDigest = false;
        PublishedListSlot = -1;
        ParticipantCount = 0;
        MetricCount = 0;
        SubjectCount = 0;
        InitialSubjectCount = 0;
        TagCount = 0;
        AnchorCount = 0;
        InitialAnchorCount = 0;
        ProvenanceCount = 0;
        FactCount = 0;
    }

    public void CaptureImmutableRows()
    {
        Array.Copy(Subjects, InitialSubjects, SubjectCount);
        Array.Copy(Anchors, InitialAnchors, AnchorCount);
        InitialSubjectCount = SubjectCount;
        InitialAnchorCount = AnchorCount;
    }

    public void ClearAndFree()
    {
        Array.Clear(Participants, 0, ParticipantCount);
        Array.Clear(Metrics, 0, MetricCount);
        Array.Clear(Subjects, 0, SubjectCount);
        Array.Clear(InitialSubjects, 0, InitialSubjectCount);
        Array.Clear(Tags, 0, TagCount);
        Array.Clear(Anchors, 0, AnchorCount);
        Array.Clear(InitialAnchors, 0, InitialAnchorCount);
        Array.Clear(Provenance, 0, ProvenanceCount);
        Array.Clear(Facts, 0, FactCount);
        OutcomeId = default;
        ResultKey = default;
        OutcomeTypeId = default;
        OperationId = default;
        Location = default;
        Causation = default;
        LastDeliveryFaultCode = string.Empty;
        ImmutablePayloadHash = string.Empty;
        Array.Clear(ImmutablePayloadDigest, 0, ImmutablePayloadDigest.Length);
        HasImmutablePayloadDigest = false;
        AnchorReservationActive = false;
        InfluenceReservationActive = false;
        RetainedReservationSlot = -1;
        DueIndexDirty = false;
        ParticipantCount = 0;
        MetricCount = 0;
        SubjectCount = 0;
        InitialSubjectCount = 0;
        TagCount = 0;
        AnchorCount = 0;
        InitialAnchorCount = 0;
        ProvenanceCount = 0;
        FactCount = 0;
        State = GameplayOutcomePageState.Free;
    }

    public string MaterializeImmutablePayloadHash()
    {
        if (!HasImmutablePayloadDigest)
            return string.Empty;
        if (string.IsNullOrEmpty(ImmutablePayloadHash))
            ImmutablePayloadHash = GameplayOutcomeCanonicalHash.ToHex(ImmutablePayloadDigest);
        return ImmutablePayloadHash;
    }

    public bool TryLoadDetachedCopy(
        GameplayOutcomeValuePage source,
        int generation)
    {
        if (source == null || !IsDetached
            || State != GameplayOutcomePageState.Free
            || source.ParticipantCount > Participants.Length
            || source.MetricCount > Metrics.Length
            || source.SubjectCount > Subjects.Length
            || source.InitialSubjectCount > InitialSubjects.Length
            || source.TagCount > Tags.Length
            || source.AnchorCount > Anchors.Length
            || source.InitialAnchorCount > InitialAnchors.Length
            || source.ProvenanceCount > Provenance.Length
            || source.FactCount > Facts.Length)
            return false;
        Generation = generation;
        State = source.State;
        OutcomeId = source.OutcomeId;
        ResultKey = source.ResultKey;
        OutcomeTypeId = source.OutcomeTypeId;
        OperationId = source.OperationId;
        Sequence = source.Sequence;
        AbsoluteDay = source.AbsoluteDay;
        Status = source.Status;
        WorldEpoch = source.WorldEpoch;
        OwnerRevision = source.OwnerRevision;
        CommitGeneration = source.CommitGeneration;
        PublishedRevision = source.PublishedRevision;
        PublishedListSlot = source.PublishedListSlot;
        Location = source.Location;
        Causation = source.Causation;
        StorageTier = source.StorageTier;
        AnchorRevision = source.AnchorRevision;
        AnchorReservationNonce = source.AnchorReservationNonce;
        AnchorReservationActive = false;
        InfluenceReservationNonce = source.InfluenceReservationNonce;
        InfluenceReservationActive = false;
        DueIndexDirty = source.DueIndexDirty;
        DeliveryFaultCount = source.DeliveryFaultCount;
        LastDeliveryFaultCode = source.LastDeliveryFaultCode;
        ImmutablePayloadHash = source.ImmutablePayloadHash;
        HasImmutablePayloadDigest = source.HasImmutablePayloadDigest;
        ParticipantCount = source.ParticipantCount;
        MetricCount = source.MetricCount;
        SubjectCount = source.SubjectCount;
        InitialSubjectCount = source.InitialSubjectCount;
        TagCount = source.TagCount;
        AnchorCount = source.AnchorCount;
        InitialAnchorCount = source.InitialAnchorCount;
        ProvenanceCount = source.ProvenanceCount;
        FactCount = source.FactCount;
        Array.Copy(source.Participants, Participants, ParticipantCount);
        Array.Copy(source.Metrics, Metrics, MetricCount);
        Array.Copy(source.Subjects, Subjects, SubjectCount);
        Array.Copy(source.InitialSubjects, InitialSubjects, InitialSubjectCount);
        Array.Copy(source.Tags, Tags, TagCount);
        Array.Copy(source.Anchors, Anchors, AnchorCount);
        Array.Copy(source.InitialAnchors, InitialAnchors, InitialAnchorCount);
        Array.Copy(source.Provenance, Provenance, ProvenanceCount);
        Array.Copy(source.Facts, Facts, FactCount);
        Array.Copy(source.ImmutablePayloadDigest, ImmutablePayloadDigest,
            source.ImmutablePayloadDigest.Length);
        return true;
    }
}

internal sealed class GameplayOutcomeValuePagePool
{
    private readonly GameplayOutcomeValuePage[] pages;
    private readonly int[] freeSmall;
    private readonly int[] freeLarge;
    private readonly List<GameplayOutcomeValuePage> detachedPages =
        new List<GameplayOutcomeValuePage>();
    private readonly List<int> detachedGenerations = new List<int>();
    private readonly Stack<int> freeDetachedSmall;
    private readonly Stack<int> freeDetachedLarge;
    private int freeSmallCount;
    private int freeLargeCount;

    public GameplayOutcomeValuePagePool(GameplayOutcomeBufferLimits limits)
    {
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        pages = new GameplayOutcomeValuePage[limits.TotalPageCount];
        freeSmall = new int[limits.SmallPageCount];
        freeLarge = new int[limits.LargePageCount];
        freeDetachedSmall = new Stack<int>(limits.RetainedSmallPageCount);
        freeDetachedLarge = new Stack<int>(limits.RetainedLargePageCount);
        int index = 0;
        for (; index < limits.SmallPageCount; index++)
        {
            pages[index] = new GameplayOutcomeValuePage(
                limits.SmallParticipants,
                limits.SmallMetrics,
                limits.SmallSubjects,
                limits.SmallTags,
                limits.SmallAnchors,
                limits.SmallProvenance,
                limits.SmallFacts,
                large: false);
            freeSmall[freeSmallCount++] = index;
        }
        for (int large = 0; large < limits.LargePageCount; large++, index++)
        {
            pages[index] = new GameplayOutcomeValuePage(
                limits.LargeParticipants,
                limits.LargeMetrics,
                limits.LargeSubjects,
                limits.LargeTags,
                limits.LargeAnchors,
                limits.LargeProvenance,
                limits.LargeFacts,
                large: true);
            freeLarge[freeLargeCount++] = index;
        }
        int retainedIndex = 0;
        for (; retainedIndex < limits.RetainedSmallPageCount; retainedIndex++)
        {
            detachedPages.Add(new GameplayOutcomeValuePage(
                limits.SmallParticipants,
                limits.SmallMetrics,
                limits.SmallSubjects,
                limits.SmallTags,
                limits.SmallAnchors,
                limits.SmallProvenance,
                limits.SmallFacts,
                large: false,
                detached: true));
            detachedGenerations.Add(0);
            freeDetachedSmall.Push(retainedIndex);
        }
        for (int retainedLarge = 0;
            retainedLarge < limits.RetainedLargePageCount;
            retainedLarge++, retainedIndex++)
        {
            detachedPages.Add(new GameplayOutcomeValuePage(
                limits.LargeParticipants,
                limits.LargeMetrics,
                limits.LargeSubjects,
                limits.LargeTags,
                limits.LargeAnchors,
                limits.LargeProvenance,
                limits.LargeFacts,
                large: true,
                detached: true));
            detachedGenerations.Add(0);
            freeDetachedLarge.Push(retainedIndex);
        }
    }

    public GameplayOutcomeBufferLimits Limits { get; }
    public int FreeSmallCount => freeSmallCount;
    public int FreeLargeCount => freeLargeCount;
    public int TotalCount => pages.Length;
    public int AddressableCount => pages.Length + detachedPages.Count;
    public int PooledInUseCount => pages.Length - freeSmallCount - freeLargeCount;
    public int DetachedInUseCount => detachedPages.Count
        - freeDetachedSmall.Count - freeDetachedLarge.Count;
    public int FreeRetainedSmallCount => freeDetachedSmall.Count;
    public int FreeRetainedLargeCount => freeDetachedLarge.Count;

    public bool TryRent(in OutcomeWriteRequirements requirements, out int pageIndex, out GameplayOutcomeValuePage page)
    {
        pageIndex = -1;
        page = null;
        bool fitsSmall = Limits.FitsSmall(requirements);
        if (fitsSmall && freeSmallCount > 0)
        {
            pageIndex = freeSmall[--freeSmallCount];
        }
        else if (Limits.CanFit(requirements) && freeLargeCount > 0)
        {
            pageIndex = freeLarge[--freeLargeCount];
        }
        else if (fitsSmall && freeLargeCount > 0)
        {
            pageIndex = freeLarge[--freeLargeCount];
        }
        else
        {
            return false;
        }

        page = pages[pageIndex];
        Stack<int> retained = page.IsLarge
            ? freeDetachedLarge
            : freeDetachedSmall;
        if (retained.Count == 0)
        {
            if (page.IsLarge)
                freeLarge[freeLargeCount++] = pageIndex;
            else
                freeSmall[freeSmallCount++] = pageIndex;
            pageIndex = -1;
            page = null;
            return false;
        }
        int retainedSlot = retained.Pop();
        page.Reserve(requirements);
        page.RetainedReservationSlot = retainedSlot;
        return true;
    }

    public GameplayOutcomeValuePage Get(int pageIndex)
    {
        if (pageIndex < 0)
            return null;
        if (pageIndex < pages.Length)
            return pages[pageIndex];
        int detachedIndex = pageIndex - pages.Length;
        return detachedIndex < detachedPages.Count ? detachedPages[detachedIndex] : null;
    }

    public bool Return(int pageIndex, int expectedGeneration)
    {
        GameplayOutcomeValuePage page = Get(pageIndex);
        if (page == null || page.Generation != expectedGeneration || page.State == GameplayOutcomePageState.Free)
            return false;
        if (page.IsDetached)
        {
            int detachedIndex = pageIndex - pages.Length;
            bool detachedLarge = page.IsLarge;
            page.ClearAndFree();
            if (detachedLarge)
                freeDetachedLarge.Push(detachedIndex);
            else
                freeDetachedSmall.Push(detachedIndex);
            return true;
        }
        bool large = page.IsLarge;
        int retainedSlot = page.RetainedReservationSlot;
        page.ClearAndFree();
        if (retainedSlot >= 0)
        {
            if (large)
                freeDetachedLarge.Push(retainedSlot);
            else
                freeDetachedSmall.Push(retainedSlot);
        }
        if (large)
            freeLarge[freeLargeCount++] = pageIndex;
        else
            freeSmall[freeSmallCount++] = pageIndex;
        return true;
    }

    public bool TryPromotePublishedToDetached(
        int pageIndex,
        int expectedGeneration,
        out int detachedPageIndex,
        out GameplayOutcomeValuePage detached)
    {
        detachedPageIndex = -1;
        detached = null;
        GameplayOutcomeValuePage source = Get(pageIndex);
        if (source == null || source.IsDetached || source.Generation != expectedGeneration
            || source.State != GameplayOutcomePageState.PublishedAcknowledged
            || source.AnchorReservationActive
            || source.InfluenceReservationActive)
            return false;
        int slot;
        int generation;
        if (source.RetainedReservationSlot >= 0)
        {
            slot = source.RetainedReservationSlot;
            generation = detachedGenerations[slot] + 1;
            if (generation <= 0) generation = 1;
            detachedGenerations[slot] = generation;
        }
        else
        {
            return false;
        }
        detached = detachedPages[slot];
        if (!detached.TryLoadDetachedCopy(source, generation))
        {
            // The retained slot was reserved before the owner mutation. Keep
            // that reservation attached to the published source on an
            // unexpected shape/generation failure so a later retry cannot
            // alias the slot with another outcome.
            detached = null;
            return false;
        }
        source.RetainedReservationSlot = -1;
        detachedPageIndex = pages.Length + slot;
        return true;
    }

    public bool TryGrowDetachedAnchorCapacity(
        int pageIndex,
        int expectedGeneration,
        out GameplayOutcomeValuePage grown)
    {
        grown = Get(pageIndex);
        // Retained pages are fixed-capacity slabs. Dynamic array replacement
        // here would put GC on the mechanic/evidence commit path; callers get
        // an explicit CapacityDeferred result instead.
        return false;
    }
}

public struct OutcomeWriteBuilder
{
    private GameplayOutcomeValuePage page;
    private bool failed;

    internal OutcomeWriteBuilder(GameplayOutcomeValuePage page)
    {
        this.page = page;
        failed = page == null;
    }

    public bool IsValid => page != null && !failed;
    public int ParticipantCount => page?.ParticipantCount ?? 0;
    public int MetricCount => page?.MetricCount ?? 0;
    public int SubjectCount => page?.SubjectCount ?? 0;
    public int TagCount => page?.TagCount ?? 0;
    public int AnchorCount => page?.AnchorCount ?? 0;
    public int ProvenanceCount => page?.ProvenanceCount ?? 0;
    public int FactCount => page?.FactCount ?? 0;

    public bool AddParticipant(in GameplayOutcomeParticipant participant)
    {
        if (!ValidateParticipant(participant) || page.ParticipantCount >= page.Participants.Length)
            return Fail();
        page.Participants[page.ParticipantCount++] = participant;
        return true;
    }

    public bool AddMetric(in GameplayOutcomeMetric metric)
    {
        if (page == null
            || !metric.MetricId.IsValid
            || !metric.UnitId.IsValid
            || double.IsNaN(metric.Value)
            || double.IsInfinity(metric.Value)
            || page.MetricCount >= page.Metrics.Length)
            return Fail();
        page.Metrics[page.MetricCount++] = metric;
        return true;
    }

    public bool AddSubject(in GameplayOutcomeSubjectLink subject)
    {
        if (page == null
            || !subject.SubjectId.IsValid
            || float.IsNaN(subject.Salience)
            || float.IsInfinity(subject.Salience)
            || subject.Salience < 0f
            || subject.Salience > 1f
            || (int)subject.Tier < (int)NarrativeMemoryTier.Recent
            || (int)subject.Tier > (int)NarrativeMemoryTier.Forgotten
            || subject.AnchorRevision != 0
            || subject.NextEvaluationDay != 0
            || page.SubjectCount >= page.Subjects.Length)
            return Fail();
        for (int index = 0; index < page.SubjectCount; index++)
        {
            if (page.Subjects[index].SubjectId == subject.SubjectId)
                return Fail();
        }
        page.Subjects[page.SubjectCount++] = subject;
        return true;
    }

    public bool AddTag(GameplayOutcomeTagId tag)
    {
        if (page == null || !tag.IsValid || page.TagCount >= page.Tags.Length)
            return Fail();
        for (int index = 0; index < page.TagCount; index++)
        {
            if (page.Tags[index].Equals(tag))
                return Fail();
        }
        page.Tags[page.TagCount++] = tag;
        return true;
    }

    public bool AddAnchor(GameplayEntityId subjectId, NarrativeEvidenceReference reference)
    {
        if (page == null
            || !subjectId.IsValid
            || !reference.IsValid
            || page.AnchorCount >= page.Anchors.Length)
            return Fail();
        for (int index = 0; index < page.AnchorCount; index++)
        {
            GameplayOutcomeAnchorRow row = page.Anchors[index];
            if (row.SubjectId == subjectId && row.Reference.Equals(reference))
                return Fail();
        }
        page.Anchors[page.AnchorCount++] = new GameplayOutcomeAnchorRow(subjectId, reference);
        return true;
    }

    public bool AddProvenance(in GameplayOutcomeProvenanceReference reference)
    {
        if (page == null || !reference.IsValid || page.ProvenanceCount >= page.Provenance.Length)
            return Fail();
        for (int index = 0; index < page.ProvenanceCount; index++)
        {
            if (page.Provenance[index].Equals(reference))
                return Fail();
        }
        page.Provenance[page.ProvenanceCount++] = reference;
        return true;
    }

    public bool AddFact(in GameplayOutcomeFact fact)
    {
        if (page == null
            || !fact.FactId.IsValid
            || !GameplayOutcomeLedger.IsValidBoundedUtf16(
                fact.Value,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false)
            || page.FactCount >= page.Facts.Length)
            return Fail();
        for (int index = 0; index < page.FactCount; index++)
        {
            if (page.Facts[index].FactId.Equals(fact.FactId))
                return Fail();
        }
        page.Facts[page.FactCount++] = fact;
        return true;
    }

    public bool SetLocation(in GameplayLocationReference location)
    {
        if (page == null)
            return Fail();
        page.Location = location;
        return true;
    }

    public bool SetCausation(in GameplayOutcomeCausation causation)
    {
        if (page == null
            || causation.HasParent && !causation.ParentOutcomeId.IsValid
            || !string.IsNullOrEmpty(causation.RelationId)
                && !GameplayOutcomeStableIdSyntax.IsValid(causation.RelationId))
            return Fail();
        page.Causation = causation;
        return true;
    }

    internal bool CountsMatch(in OutcomeWriteRequirements requirements) =>
        IsValid
        && page.ParticipantCount == requirements.ParticipantCount
        && page.MetricCount == requirements.MetricCount
        && page.SubjectCount == requirements.SubjectCount
        && page.TagCount == requirements.TagCount
        && page.AnchorCount == requirements.AnchorCount
        && page.ProvenanceCount == requirements.ProvenanceCount
        && page.FactCount == requirements.FactCount;

    private bool ValidateParticipant(in GameplayOutcomeParticipant participant) =>
        page != null
        && participant.EntityId.IsValid
        && participant.RoleId.IsValid
        && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(participant.DisplayName)
        && (int)participant.ParticipationKind >= (int)GameplayParticipationKind.Direct
        && (int)participant.ParticipationKind <= (int)GameplayParticipationKind.GroupObservation
        && (participant.ParticipationKind != GameplayParticipationKind.OptionalWitness
            || participant.HasPerceptionEvidence);

    private bool Fail()
    {
        failed = true;
        return false;
    }
}

public readonly struct GameplayOutcomeReadView
{
    private readonly GameplayOutcomeValuePage page;

    internal GameplayOutcomeReadView(GameplayOutcomeValuePage page) => this.page = page;

    public GameplayOutcomeId OutcomeId => page.OutcomeId;
    public GameplayResultKey ResultKey => page.ResultKey;
    public GameplayOutcomeTypeId OutcomeTypeId => page.OutcomeTypeId;
    public GameplayOperationId OperationId => page.OperationId;
    public long Sequence => page.Sequence;
    public long OwnerRevision => page.OwnerRevision;
    public int AbsoluteDay => page.AbsoluteDay;
    public GameplayOutcomeStatus Status => page.Status;
    public GameplayLocationReference Location => page.Location;
    public GameplayOutcomeCausation Causation => page.Causation;
    public NarrativeMemoryTier StorageTier => page.StorageTier;
    public int ParticipantCount => page.ParticipantCount;
    public int MetricCount => page.MetricCount;
    public int SubjectCount => page.SubjectCount;
    public int TagCount => page.TagCount;
    public int AnchorCount => page.AnchorCount;
    public int InitialSubjectCount => page.InitialSubjectCount;
    public int InitialAnchorCount => page.InitialAnchorCount;
    public int ProvenanceCount => page.ProvenanceCount;
    public int FactCount => page.FactCount;
    public string ImmutablePayloadHash => page.MaterializeImmutablePayloadHash();
    public GameplayOutcomeParticipant GetParticipant(int index) => page.Participants[RequireIndex(index, page.ParticipantCount)];
    public GameplayOutcomeMetric GetMetric(int index) => page.Metrics[RequireIndex(index, page.MetricCount)];
    public GameplayOutcomeSubjectLink GetSubject(int index) => page.Subjects[RequireIndex(index, page.SubjectCount)];
    public GameplayOutcomeTagId GetTag(int index) => page.Tags[RequireIndex(index, page.TagCount)];
    public GameplayEntityId GetAnchorSubject(int index) => page.Anchors[RequireIndex(index, page.AnchorCount)].SubjectId;
    public NarrativeEvidenceReference GetAnchor(int index) => page.Anchors[RequireIndex(index, page.AnchorCount)].Reference;
    public GameplayOutcomeSubjectLink GetInitialSubject(int index) =>
        page.InitialSubjects[RequireIndex(index, page.InitialSubjectCount)];
    public GameplayEntityId GetInitialAnchorSubject(int index) =>
        page.InitialAnchors[RequireIndex(index, page.InitialAnchorCount)].SubjectId;
    public NarrativeEvidenceReference GetInitialAnchor(int index) =>
        page.InitialAnchors[RequireIndex(index, page.InitialAnchorCount)].Reference;
    public GameplayOutcomeProvenanceReference GetProvenance(int index) =>
        page.Provenance[RequireIndex(index, page.ProvenanceCount)];
    public GameplayOutcomeFact GetFact(int index) =>
        page.Facts[RequireIndex(index, page.FactCount)];

    private static int RequireIndex(int index, int count)
    {
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return index;
    }
}
