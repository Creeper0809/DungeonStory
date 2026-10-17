using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public readonly struct PreparedMigratedProducerOutcome
{
    internal PreparedMigratedProducerOutcome(
        PreparedOutcomeReservation reservation,
        MigratedProducerOutcomeDefinition definition,
        string ownerKey,
        long previousOwnerRevision,
        long ownerRevision,
        int absoluteDay,
        GameplayOutcomeStatus status,
        int participantCount,
        int metricCount,
        int subjectCount,
        int factCount,
        int provenanceCount)
    {
        Reservation = reservation;
        Definition = definition;
        OwnerKey = ownerKey ?? string.Empty;
        PreviousOwnerRevision = previousOwnerRevision;
        OwnerRevision = ownerRevision;
        AbsoluteDay = absoluteDay;
        Status = status;
        ParticipantCount = participantCount;
        MetricCount = metricCount;
        SubjectCount = subjectCount;
        FactCount = factCount;
        ProvenanceCount = provenanceCount;
    }

    internal PreparedOutcomeReservation Reservation { get; }
    internal MigratedProducerOutcomeDefinition Definition { get; }
    internal string OwnerKey { get; }
    internal long PreviousOwnerRevision { get; }
    internal int ParticipantCount { get; }
    internal int MetricCount { get; }
    internal int SubjectCount { get; }
    internal int FactCount { get; }
    internal int ProvenanceCount { get; }
    public GameplayResultKey ResultKey => Reservation.ResultKey;
    public GameplayOutcomeTypeId OutcomeTypeId =>
        Definition?.OutcomeTypeId ?? default;
    public long OwnerRevision { get; }
    public int AbsoluteDay { get; }
    public GameplayOutcomeStatus Status { get; }
    public float Salience => Definition?.Salience ?? 0f;
    public NarrativeMemoryTier Tier =>
        Definition?.Tier ?? NarrativeMemoryTier.Recent;
    public bool IsValid => Reservation.IsValid
        && Definition != null
        && OwnerRevision > 0L
        && OwnerKey.Length > 0;
}

public readonly struct MigratedProducerOutcomeCommitResult
{
    public MigratedProducerOutcomeCommitResult(
        bool durablyCommitted,
        GameplayResultKey resultKey,
        GameplayOutcomeId outcomeId,
        string canonicalPayloadHash,
        string detailCode)
    {
        DurablyCommitted = durablyCommitted;
        ResultKey = resultKey;
        OutcomeId = outcomeId;
        CanonicalPayloadHash = canonicalPayloadHash ?? string.Empty;
        DetailCode = detailCode ?? string.Empty;
    }

    public bool DurablyCommitted { get; }
    public GameplayResultKey ResultKey { get; }
    public GameplayOutcomeId OutcomeId { get; }
    public string CanonicalPayloadHash { get; }
    public string DetailCode { get; }
}

public interface IMigratedProducerOutcomeTransaction
{
    bool TryReserve(
        MigratedProducerOutcomeKind kind,
        string ownerIdentity,
        int absoluteDay,
        GameplayOutcomeStatus status,
        int participantCount,
        int metricCount,
        int subjectCount,
        int additionalFactCount,
        out PreparedMigratedProducerOutcome prepared,
        out string failureReason,
        int additionalProvenanceCount = 0);
    MigratedProducerOutcomePayloadBuilder CreatePayloadBuilder(
        in PreparedMigratedProducerOutcome prepared);
    MigratedProducerOutcomeCommitResult Commit(
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeReceipt receipt);
    bool CommitBatch(
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeReceipt[] receipts,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason);
    void Cancel(in PreparedMigratedProducerOutcome prepared);
}

public interface IMigratedProducerOutcomeExternalBatchTransaction
{
    bool CommitWithExternalPrepared(
        in PreparedOutcomeToken externalPrepared,
        long expectedExternalOwnerRevision,
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeReceipt[] receipts,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason);

    bool CommitWithExternalPreparedBatch(
        PreparedOwnerOutcome[] externalPrepared,
        long[] expectedExternalOwnerRevisions,
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeReceipt[] receipts,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason);
}

public readonly struct MigratedProducerOutcomeSubject
{
    public MigratedProducerOutcomeSubject(
        GameplayEntityKindId entityKind,
        string stableId,
        string displayName,
        GameplayRoleId role)
    {
        EntityId = new GameplayEntityId(entityKind, stableId);
        DisplayName = MigratedProducerOutcomeSnapshots.Name(stableId, displayName);
        Role = role;
    }

    public GameplayEntityId EntityId { get; }
    public KoreanNameSnapshot DisplayName { get; }
    public GameplayRoleId Role { get; }
}

public readonly struct MigratedProducerOutcomeDetails
{
    public MigratedProducerOutcomeDetails(
        MigratedProducerOutcomeSubject[] subjects,
        GameplayOutcomeMetric[] metrics,
        GameplayOutcomeFact[] facts,
        string summary,
        GameplayOutcomeCausation causation = default,
        bool summaryFirst = false,
        GameplayOutcomeProvenanceReference[] provenance = null)
    {
        Subjects = subjects ?? Array.Empty<MigratedProducerOutcomeSubject>();
        Metrics = metrics ?? Array.Empty<GameplayOutcomeMetric>();
        Facts = facts ?? Array.Empty<GameplayOutcomeFact>();
        Provenance = provenance
            ?? Array.Empty<GameplayOutcomeProvenanceReference>();
        Summary = summary?.Trim() ?? string.Empty;
        Causation = causation;
        SummaryFirst = summaryFirst;
    }

    public MigratedProducerOutcomeSubject[] Subjects { get; }
    public GameplayOutcomeMetric[] Metrics { get; }
    public GameplayOutcomeFact[] Facts { get; }
    public GameplayOutcomeProvenanceReference[] Provenance { get; }
    public string Summary { get; }
    public GameplayOutcomeCausation Causation { get; }
    public bool SummaryFirst { get; }
    public bool IsValid => Subjects != null
        && Subjects.Length > 0
        && Subjects.Length <= 16
        && Metrics != null
        && Metrics.Length <= 16
        && Facts != null
        && Facts.Length <= 14
        && Provenance != null
        && Provenance.Length <= 15
        && Provenance.All(reference => reference.IsValid)
        && !string.IsNullOrWhiteSpace(Summary);
}

public static class MigratedProducerOutcomeTransactionExtensions
{
    public static bool TryReserveDetailed(
        this IMigratedProducerOutcomeTransaction transaction,
        MigratedProducerOutcomeKind kind,
        string ownerIdentity,
        int absoluteDay,
        GameplayOutcomeStatus status,
        in MigratedProducerOutcomeDetails details,
        out PreparedMigratedProducerOutcome prepared,
        out string failureReason)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (!details.IsValid)
        {
            prepared = default;
            failureReason = "migrated-producer-detailed-shape-invalid";
            return false;
        }
        return transaction.TryReserve(
            kind,
            ownerIdentity,
            absoluteDay,
            status,
            details.Subjects.Length,
            details.Metrics.Length,
            details.Subjects.Length,
            details.Facts.Length,
            out prepared,
            out failureReason,
            details.Provenance.Length);
    }

    public static MigratedProducerOutcomeCommitResult CommitDetailed(
        this IMigratedProducerOutcomeTransaction transaction,
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeDetails details)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (!details.IsValid)
        {
            transaction.Cancel(prepared);
            return new MigratedProducerOutcomeCommitResult(
                false,
                prepared.ResultKey,
                default,
                string.Empty,
                "migrated-producer-detailed-shape-invalid");
        }

        if (!transaction.TryCreateDetailedReceipt(
                prepared,
                details,
                out MigratedProducerOutcomeReceipt receipt))
        {
            transaction.Cancel(prepared);
            return new MigratedProducerOutcomeCommitResult(
                false,
                prepared.ResultKey,
                default,
                string.Empty,
                "migrated-producer-detailed-capacity-invalid");
        }
        return transaction.Commit(prepared, receipt);
    }

    public static bool TryCreateDetailedReceipt(
        this IMigratedProducerOutcomeTransaction transaction,
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeDetails details,
        out MigratedProducerOutcomeReceipt receipt)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        receipt = default;
        if (!details.IsValid)
            return false;
        MigratedProducerOutcomePayloadBuilder builder =
            transaction.CreatePayloadBuilder(prepared);
        if (!TryBuildDetailed(ref builder, prepared, details))
            return false;
        receipt = builder.Build();
        return true;
    }

    public static bool CommitDetailedBatch(
        this IMigratedProducerOutcomeTransaction transaction,
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeDetails[] details,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (prepared == null
            || details == null
            || results == null
            || prepared.Length == 0
            || details.Length != prepared.Length
            || results.Length != prepared.Length)
        {
            failureReason = "migrated-producer-detailed-batch-shape-invalid";
            return false;
        }

        var receipts = new MigratedProducerOutcomeReceipt[prepared.Length];
        for (int index = 0; index < prepared.Length; index++)
        {
            MigratedProducerOutcomePayloadBuilder builder =
                transaction.CreatePayloadBuilder(prepared[index]);
            if (!details[index].IsValid
                || !TryBuildDetailed(
                    ref builder,
                    prepared[index],
                    details[index]))
            {
                for (int cancelIndex = 0;
                     cancelIndex < prepared.Length;
                     cancelIndex++)
                {
                    transaction.Cancel(prepared[cancelIndex]);
                }
                failureReason =
                    "migrated-producer-detailed-batch-capacity-invalid";
                return false;
            }
            receipts[index] = builder.Build();
        }
        return transaction.CommitBatch(
            prepared,
            receipts,
            results,
            out failureReason);
    }

    private static bool TryBuildDetailed(
        ref MigratedProducerOutcomePayloadBuilder builder,
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeDetails details)
        => TryBuildDetailed(ref builder, prepared.Salience, prepared.Tier, details);

    internal static bool TryBuildDetailed(
        ref MigratedProducerOutcomePayloadBuilder builder,
        float salience,
        NarrativeMemoryTier tier,
        in MigratedProducerOutcomeDetails details)
    {
        for (int index = 0; index < details.Subjects.Length; index++)
        {
            MigratedProducerOutcomeSubject subject = details.Subjects[index];
            if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                    subject.EntityId,
                    subject.Role,
                    GameplayParticipationKind.Direct,
                    true,
                    subject.DisplayName))
                || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                    subject.EntityId,
                    salience,
                    tier,
                    tier == NarrativeMemoryTier.Core,
                    false,
                    0)))
            {
                return false;
            }
        }
        for (int index = 0; index < details.Metrics.Length; index++)
        {
            if (!builder.AddMetric(details.Metrics[index]))
                return false;
        }
        for (int index = 0; index < details.Provenance.Length; index++)
        {
            if (!builder.AddProvenance(details.Provenance[index]))
                return false;
        }
        if (details.SummaryFirst && !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.SummaryFact, details.Summary)))
            return false;
        for (int index = 0; index < details.Facts.Length; index++)
        {
            if (!builder.AddFact(details.Facts[index]))
                return false;
        }
        if (!details.SummaryFirst && !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.SummaryFact,
                details.Summary)))
        {
            return false;
        }
        if (details.Causation.HasParent
            || details.Causation.RootOperationId.IsValid
            || !string.IsNullOrEmpty(details.Causation.RelationId))
        {
            builder.SetCausation(details.Causation);
        }
        return true;
    }

    public static MigratedProducerOutcomeCommitResult CommitRequiredSingleSubject(
        this IMigratedProducerOutcomeTransaction transaction,
        MigratedProducerOutcomeKind kind,
        string ownerIdentity,
        int absoluteDay,
        in MigratedProducerOutcomeSubject subject,
        string summary)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (!transaction.TryReserveSingleSubject(
                kind,
                ownerIdentity,
                Math.Max(0, absoluteDay),
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out string reservationFailure))
        {
            throw new InvalidOperationException(
                $"Required migrated outcome reservation failed for '{kind}': "
                + reservationFailure);
        }

        MigratedProducerOutcomeCommitResult committed =
            transaction.CommitSingleSubject(prepared, subject, summary);
        if (!committed.DurablyCommitted)
        {
            throw new InvalidOperationException(
                $"Required migrated outcome commit failed for '{kind}': "
                + committed.DetailCode);
        }
        return committed;
    }

    public static bool TryReserveSingleSubject(
        this IMigratedProducerOutcomeTransaction transaction,
        MigratedProducerOutcomeKind kind,
        string ownerIdentity,
        int absoluteDay,
        GameplayOutcomeStatus status,
        out PreparedMigratedProducerOutcome prepared,
        out string failureReason)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        return transaction.TryReserve(
            kind,
            ownerIdentity,
            absoluteDay,
            status,
            participantCount: 1,
            metricCount: 0,
            subjectCount: 1,
            additionalFactCount: 0,
            out prepared,
            out failureReason);
    }

    public static MigratedProducerOutcomeCommitResult CommitSingleSubject(
        this IMigratedProducerOutcomeTransaction transaction,
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeSubject subject,
        string summary)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        MigratedProducerOutcomePayloadBuilder builder =
            transaction.CreatePayloadBuilder(prepared);
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                subject.EntityId,
                subject.Role,
                GameplayParticipationKind.Direct,
                true,
                subject.DisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                subject.EntityId,
                prepared.Salience,
                prepared.Tier,
                prepared.Tier == NarrativeMemoryTier.Core,
                false,
                0))
            || !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.SummaryFact,
                summary?.Trim() ?? string.Empty)))
        {
            transaction.Cancel(prepared);
            return new MigratedProducerOutcomeCommitResult(
                false,
                prepared.ResultKey,
                default,
                string.Empty,
                "migrated-producer-single-subject-capacity-invalid");
        }
        return transaction.Commit(prepared, builder.Build());
    }

    public static bool CommitSingleSubjectBatch(
        this IMigratedProducerOutcomeTransaction transaction,
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeSubject[] subjects,
        string[] summaries,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction));
        if (prepared == null
            || subjects == null
            || summaries == null
            || results == null
            || prepared.Length == 0
            || subjects.Length != prepared.Length
            || summaries.Length != prepared.Length
            || results.Length != prepared.Length)
        {
            failureReason = "migrated-producer-single-subject-batch-shape-invalid";
            return false;
        }

        MigratedProducerOutcomeReceipt[] receipts =
            new MigratedProducerOutcomeReceipt[prepared.Length];
        for (int index = 0; index < prepared.Length; index++)
        {
            MigratedProducerOutcomePayloadBuilder builder =
                transaction.CreatePayloadBuilder(prepared[index]);
            if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                    subjects[index].EntityId,
                    subjects[index].Role,
                    GameplayParticipationKind.Direct,
                    true,
                    subjects[index].DisplayName))
                || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                    subjects[index].EntityId,
                    prepared[index].Salience,
                    prepared[index].Tier,
                    prepared[index].Tier == NarrativeMemoryTier.Core,
                    false,
                    0))
                || !builder.AddFact(new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.SummaryFact,
                    summaries[index]?.Trim() ?? string.Empty)))
            {
                for (int cancelIndex = 0;
                     cancelIndex < prepared.Length;
                     cancelIndex++)
                {
                    transaction.Cancel(prepared[cancelIndex]);
                }
                failureReason =
                    "migrated-producer-single-subject-batch-capacity-invalid";
                return false;
            }
            receipts[index] = builder.Build();
        }
        return transaction.CommitBatch(
            prepared,
            receipts,
            results,
            out failureReason);
    }
}

[Serializable]
public sealed class MigratedProducerOwnerRevisionSaveData
{
    public string ownerKey = string.Empty;
    public long revision;

    public MigratedProducerOwnerRevisionSaveData Clone() => new()
    {
        ownerKey = ownerKey,
        revision = revision
    };
}

[Serializable]
public sealed class DungeonMigratedProducerOutcomeSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<MigratedProducerOwnerRevisionSaveData> owners = new();
}

public interface IMigratedProducerOutcomePersistence
{
    DungeonMigratedProducerOutcomeSaveData Capture();
    MigratedProducerOutcomeRestoreCandidate PrepareRestore(
        DungeonMigratedProducerOutcomeSaveData snapshot);
    void Restore(MigratedProducerOutcomeRestoreCandidate candidate);
}

public sealed class MigratedProducerOutcomeRestoreCandidate
{
    internal MigratedProducerOutcomeRestoreCandidate(
        IReadOnlyList<MigratedProducerOwnerRevisionSaveData> owners) =>
        Owners = owners ?? throw new ArgumentNullException(nameof(owners));

    internal IReadOnlyList<MigratedProducerOwnerRevisionSaveData> Owners { get; }
}

internal sealed class MigratedProducerOutcomeAggregateState
{
    public Dictionary<string, long> OwnerRevisions { get; } =
        new(StringComparer.Ordinal);

    public MigratedProducerOutcomeAggregateState DeepClone()
    {
        var clone = new MigratedProducerOutcomeAggregateState();
        foreach (KeyValuePair<string, long> pair in OwnerRevisions)
            clone.OwnerRevisions.Add(pair.Key, pair.Value);
        return clone;
    }
}

public sealed class MigratedProducerOutcomeRuntime :
    IMigratedProducerOutcomeTransaction,
    IMigratedProducerOutcomeExternalBatchTransaction,
    IMigratedProducerOutcomePersistence
{
    private const int MaximumOwnerIdentityLength = 512;
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly GameplayOutcomeLedger ledger;
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public MigratedProducerOutcomeRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder,
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
        _ = Current;
    }

    private MigratedProducerOutcomeAggregateState Current =>
        rootStore.GetOrCreate(() => new MigratedProducerOutcomeAggregateState());

    private MigratedProducerOutcomeAggregateState Writable =>
        rootStore.GetOrCreateWritable(
            () => new MigratedProducerOutcomeAggregateState(),
            state => state.DeepClone());

    public bool TryReserve(
        MigratedProducerOutcomeKind kind,
        string ownerIdentity,
        int absoluteDay,
        GameplayOutcomeStatus status,
        int participantCount,
        int metricCount,
        int subjectCount,
        int additionalFactCount,
        out PreparedMigratedProducerOutcome prepared,
        out string failureReason,
        int additionalProvenanceCount = 0)
    {
        prepared = default;
        string identity = ownerIdentity?.Trim() ?? string.Empty;
        if (identity.Length == 0
            || identity.Length > MaximumOwnerIdentityLength
            || absoluteDay < 0
            || !Enum.IsDefined(typeof(GameplayOutcomeStatus), status)
            || participantCount <= 0 || participantCount > 16
            || metricCount < 0 || metricCount > 16
            || subjectCount <= 0 || subjectCount > participantCount
            || additionalFactCount < 0 || additionalFactCount > 14
            || additionalProvenanceCount < 0 || additionalProvenanceCount > 15)
        {
            failureReason = "migrated-producer-reservation-shape-invalid";
            return false;
        }

        MigratedProducerOutcomeDefinition definition;
        try
        {
            definition = MigratedProducerOutcomeCatalog.Get(kind);
        }
        catch (ArgumentOutOfRangeException)
        {
            failureReason = "migrated-producer-kind-invalid";
            return false;
        }

        string ownerKey = definition.ProducerId + "|" + identity;
        long previousRevision = Current.OwnerRevisions.TryGetValue(
            ownerKey,
            out long observed)
                ? observed
                : 0L;
        if (previousRevision == long.MaxValue)
        {
            failureReason = "migrated-producer-owner-revision-exhausted";
            return false;
        }
        long revision = previousRevision + 1L;
        GameplayResultKey resultKey;
        try
        {
            resultKey = MigratedProducerOutcomeCatalog.ResultKey(kind, identity, revision);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "migrated-producer-result-key-invalid:"
                + exception.GetType().Name;
            return false;
        }

        var requirements = new OutcomeWriteRequirements(
            resultKey,
            definition.OutcomeTypeId,
            absoluteDay,
            status,
            ledger.CurrentWorldEpoch,
            ownerRevision: revision,
            participantCount,
            metricCount,
            subjectCount,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: additionalProvenanceCount + 1,
            factCount: additionalFactCount + 2);
        OutcomePrepareResult reserve = recorder.TryReserve(
            requirements,
            out PreparedOutcomeReservation reservation);
        if (!reserve.Success)
        {
            failureReason = "migrated-producer-reserve-" + reserve.Code
                + ":" + reserve.DetailCode;
            return false;
        }
        prepared = new PreparedMigratedProducerOutcome(
            reservation,
            definition,
            ownerKey,
            previousRevision,
            revision,
            absoluteDay,
            status,
            participantCount,
            metricCount,
            subjectCount,
            additionalFactCount + 2,
            additionalProvenanceCount + 1);
        failureReason = string.Empty;
        return true;
    }

    public MigratedProducerOutcomePayloadBuilder CreatePayloadBuilder(
        in PreparedMigratedProducerOutcome prepared)
    {
        if (!prepared.IsValid)
            throw new ArgumentException("A valid migrated producer reservation is required.", nameof(prepared));
        var builder = new MigratedProducerOutcomePayloadBuilder(
            prepared.ResultKey,
            prepared.OutcomeTypeId,
            prepared.AbsoluteDay,
            prepared.Status,
            prepared.OwnerRevision);
        if (!builder.AddTag(prepared.Definition.DomainTag)
            || !builder.AddProvenance(
                MigratedProducerOutcomeIds.ProducerMigrationProvenance)
            || !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.SourceTypeFact,
                prepared.Definition.SourceTypeName)))
        {
            throw new InvalidOperationException(
                "The migrated producer payload defaults exceeded their fixed capacity.");
        }
        return builder;
    }

    internal bool TryReserveObserved(in MigratedDomainFactCapture fact,
        out PreparedMigratedProducerOutcome prepared, out string failure)
    {
        prepared = default;
        string identity = fact.OwnerIdentity?.Trim() ?? string.Empty;
        if (identity.Length == 0 || identity.Length > MaximumOwnerIdentityLength)
        { failure = "fact-observer-owner-identity-invalid"; return false; }
        var definition = MigratedProducerOutcomeCatalog.Get(fact.Kind);
        string ownerKey = definition.ProducerId + "|" + identity;
        long before = Current.OwnerRevisions.TryGetValue(ownerKey, out long observed) ? observed : 0L;
        if (before == long.MaxValue)
        { failure = "fact-observer-owner-revision-exhausted"; return false; }
        var key = MigratedProducerOutcomeCatalog.ResultKey(fact.Kind, identity, before + 1L);
        // Observer-only binding: generated operation subjects never escape
        // into a gameplay fact or become a second domain identity authority.
        var details = fact.ResolveDetails(key);
        return this.TryReserveDetailed(fact.Kind, identity, fact.AbsoluteDay, fact.Status,
            details, out prepared, out failure);
    }

    internal bool TryPrepareObserved(
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeDetails details,
        GameplayDomainFactCommitGroup group,
        out GameplayDomainFactPublication publication,
        out string failure)
    {
        publication = null;
        if (!this.TryCreateDetailedReceipt(prepared, details, out var receipt)
            || !ReceiptMatches(prepared, receipt))
        { failure = "fact-observer-migrated-receipt-mismatch"; return false; }
        var revision = new ObservedRevision(this, prepared);
        if (!revision.Validate())
        { failure = "fact-observer-migrated-owner-revision-stale"; return false; }
        var written = recorder.TryWriteReserved(receipt, prepared.Reservation, out var token);
        if (!written.Success)
        { failure = "fact-observer-migrated-write:" + written.Code + ":" + written.DetailCode; return false; }
        publication = new GameplayDomainFactPublication(group, recorder,
            new PreparedOwnerOutcome(token), prepared.OwnerRevision) { Revision = revision };
        failure = string.Empty;
        return true;
    }

    private sealed class ObservedRevision : IGameplayDomainFactRevision
    {
        private readonly MigratedProducerOutcomeRuntime owner;
        private readonly PreparedMigratedProducerOutcome prepared;
        public ObservedRevision(MigratedProducerOutcomeRuntime owner, PreparedMigratedProducerOutcome prepared)
        { this.owner = owner; this.prepared = prepared; }
        public string ConflictKey => prepared.OwnerKey;
        public bool Validate()
        {
            long current = owner.Current.OwnerRevisions.TryGetValue(prepared.OwnerKey, out long value) ? value : 0L;
            return current == prepared.PreviousOwnerRevision && prepared.OwnerRevision == current + 1L;
        }
        public void Apply() => owner.Writable.OwnerRevisions[prepared.OwnerKey] = prepared.OwnerRevision;
        public void Rollback() => owner.RestoreOwnerRevision(prepared);
    }

    internal bool TryPrepareObservedRecovery(
        in MigratedDomainFactCapture fact,
        string publicationKey,
        GameplayDomainFactCommitGroup group,
        out GameplayDomainFactPublication publication,
        out string failure)
    {
        publication = null;
        failure = "fact-observer-recovery-identity-invalid";
        if (fact.ReservationOnly)
        { failure = "fact-observer-proposal-cannot-be-replayed"; return false; }
        string[] parts = publicationKey?.Split('|');
        if (parts == null || parts.Length != 4
            || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long revision)
            || revision < 1L || parts[3] != "0")
            return false;
        var key = MigratedProducerOutcomeCatalog.ResultKey(fact.Kind, fact.OwnerIdentity, revision);
        if (key.ToString() != publicationKey) return false;
        var details = fact.ResolveDetails(key);
        if (!details.IsValid)
        { failure = "fact-observer-recovery-payload-invalid"; return false; }
        var definition = MigratedProducerOutcomeCatalog.Get(fact.Kind);
        string ownerKey = definition.ProducerId + "|" + fact.OwnerIdentity.Trim();
        long current = Current.OwnerRevisions.TryGetValue(ownerKey, out long value) ? value : 0L;
        var builder = new MigratedProducerOutcomePayloadBuilder(key, definition.OutcomeTypeId,
            fact.AbsoluteDay, fact.Status, revision);
        if (!builder.AddTag(definition.DomainTag)
            || !builder.AddProvenance(MigratedProducerOutcomeIds.ProducerMigrationProvenance)
            || !builder.AddFact(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.SourceTypeFact, definition.SourceTypeName))
            || !MigratedProducerOutcomeTransactionExtensions.TryBuildDetailed(
                ref builder, definition.Salience, definition.Tier, details))
        { failure = "fact-observer-recovery-payload-invalid"; return false; }

        // The complete payload is compared even if this exact key already exists.
        var prepared = recorder.TryPrepare(builder.Build(), out var token);
        if (prepared.Code is OutcomePrepareCode.AlreadyCommitted or OutcomePrepareCode.AlreadyPublished
            or OutcomePrepareCode.AlreadyTerminal)
        {
            if (current < revision)
            { failure = "fact-observer-recovery-canonical-owner-revision-missing"; return false; }
            publication = new GameplayDomainFactPublication(group, recorder, key, revision);
        }
        else if (prepared.Success)
        {
            // Commit may have advanced the observer counter before its durable
            // boundary threw. Recover the saved key, never allocate the next key.
            if (current != revision - 1L && current != revision)
            {
                recorder.CancelPrepared(token);
                failure = "fact-observer-recovery-owner-revision-conflict";
                return false;
            }
            publication = new GameplayDomainFactPublication(group, recorder,
                new PreparedOwnerOutcome(token), revision)
            { Revision = new RecoveryRevision(this, ownerKey, current, revision) };
        }
        else
        { failure = "fact-observer-recovery-prepare:" + prepared.Code + ":" + prepared.DetailCode; return false; }
        failure = string.Empty;
        return true;
    }

    private sealed class RecoveryRevision : IGameplayDomainFactRevision
    {
        private readonly MigratedProducerOutcomeRuntime owner;
        private readonly long before;
        private readonly long after;
        public RecoveryRevision(MigratedProducerOutcomeRuntime owner, string ownerKey, long before, long after)
        { this.owner = owner; ConflictKey = ownerKey; this.before = before; this.after = after; }
        public string ConflictKey { get; }
        public bool Validate() => (owner.Current.OwnerRevisions.TryGetValue(ConflictKey, out long value)
            ? value : 0L) == before;
        public void Apply() => owner.Writable.OwnerRevisions[ConflictKey] = after;
        public void Rollback()
        {
            if (before == 0L) owner.Writable.OwnerRevisions.Remove(ConflictKey);
            else owner.Writable.OwnerRevisions[ConflictKey] = before;
        }
    }

    public MigratedProducerOutcomeCommitResult Commit(
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeReceipt receipt)
    {
        if (!ReceiptMatches(prepared, receipt))
        {
            Cancel(prepared);
            return Rejected(prepared.ResultKey, "migrated-producer-receipt-mismatch");
        }
        long currentRevision = Current.OwnerRevisions.TryGetValue(
            prepared.OwnerKey,
            out long observed)
                ? observed
                : 0L;
        if (currentRevision != prepared.PreviousOwnerRevision
            || prepared.OwnerRevision != currentRevision + 1L)
        {
            Cancel(prepared);
            return Rejected(prepared.ResultKey, "migrated-producer-owner-revision-stale");
        }

        OutcomePrepareResult written = recorder.TryWriteReserved(
            receipt,
            prepared.Reservation,
            out PreparedOutcomeToken token);
        if (!written.Success)
        {
            recorder.CancelReservation(prepared.Reservation);
            return Rejected(
                prepared.ResultKey,
                "migrated-producer-write-" + written.Code + ":" + written.DetailCode);
        }

        Writable.OwnerRevisions[prepared.OwnerKey] = prepared.OwnerRevision;
        OwnerOutcomeCommitResult committed;
        try
        {
            committed = transactions.Commit(
                new PreparedOwnerOutcome(token),
                prepared.OwnerRevision);
        }
        catch (Exception exception)
        {
            RestoreOwnerRevision(prepared);
            recorder.CancelPrepared(token);
            return Rejected(
                prepared.ResultKey,
                "migrated-producer-commit-exception:" + exception.GetType().Name);
        }
        if (!committed.DurablyCommitted)
        {
            RestoreOwnerRevision(prepared);
            return Rejected(prepared.ResultKey, committed.DetailCode);
        }
        return new MigratedProducerOutcomeCommitResult(
            true,
            committed.ResultKey,
            committed.OutcomeId,
            committed.CanonicalPayloadHash,
            committed.DetailCode);
    }

    public bool CommitBatch(
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeReceipt[] receipts,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason)
    {
        if (prepared == null
            || receipts == null
            || results == null
            || prepared.Length == 0
            || prepared.Length
                > GameplayOutcomeTransactionLimits.MaximumAtomicCommitBatchCount
            || receipts.Length != prepared.Length
            || results.Length != prepared.Length)
        {
            CancelBatch(prepared);
            failureReason = "migrated-producer-batch-shape-invalid";
            return false;
        }

        var ownerKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < prepared.Length; index++)
        {
            PreparedMigratedProducerOutcome item = prepared[index];
            if (!ReceiptMatches(item, receipts[index])
                || !ownerKeys.Add(item.OwnerKey))
            {
                CancelBatch(prepared);
                failureReason = "migrated-producer-batch-receipt-mismatch:"
                    + index;
                return false;
            }
            long currentRevision = Current.OwnerRevisions.TryGetValue(
                item.OwnerKey,
                out long observed)
                    ? observed
                    : 0L;
            if (currentRevision != item.PreviousOwnerRevision
                || item.OwnerRevision != currentRevision + 1L)
            {
                CancelBatch(prepared);
                failureReason = "migrated-producer-batch-owner-revision-stale:"
                    + index;
                return false;
            }
        }

        var tokens = new PreparedOwnerOutcome[prepared.Length];
        for (int index = 0; index < prepared.Length; index++)
        {
            OutcomePrepareResult written = recorder.TryWriteReserved(
                receipts[index],
                prepared[index].Reservation,
                out PreparedOutcomeToken token);
            if (!written.Success)
            {
                for (int preparedIndex = 0;
                     preparedIndex < index;
                     preparedIndex++)
                {
                    transactions.Cancel(tokens[preparedIndex]);
                }
                for (int reservationIndex = index;
                     reservationIndex < prepared.Length;
                     reservationIndex++)
                {
                    recorder.CancelReservation(
                        prepared[reservationIndex].Reservation);
                }
                failureReason = "migrated-producer-batch-write-"
                    + written.Code + ":" + written.DetailCode + ":" + index;
                return false;
            }
            tokens[index] = new PreparedOwnerOutcome(token);
        }

        MigratedProducerOutcomeAggregateState writable = Writable;
        var expectedRevisions = new long[prepared.Length];
        for (int index = 0; index < prepared.Length; index++)
        {
            writable.OwnerRevisions[prepared[index].OwnerKey] =
                prepared[index].OwnerRevision;
            expectedRevisions[index] = prepared[index].OwnerRevision;
        }

        var ownerResults = new OwnerOutcomeCommitResult[prepared.Length];
        bool committed = transactions.CommitBatch(
            tokens,
            expectedRevisions,
            ownerResults,
            out failureReason);
        if (!committed)
        {
            for (int index = 0; index < prepared.Length; index++)
                RestoreOwnerRevision(prepared[index]);
            return false;
        }

        for (int index = 0; index < ownerResults.Length; index++)
        {
            OwnerOutcomeCommitResult ownerResult = ownerResults[index];
            results[index] = new MigratedProducerOutcomeCommitResult(
                ownerResult.DurablyCommitted,
                ownerResult.ResultKey,
                ownerResult.OutcomeId,
                ownerResult.CanonicalPayloadHash,
                ownerResult.DetailCode);
        }
        failureReason = string.Empty;
        return true;
    }

    public bool CommitWithExternalPrepared(
        in PreparedOutcomeToken externalPrepared,
        long expectedExternalOwnerRevision,
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeReceipt[] receipts,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason)
    {
        return CommitWithExternalPreparedBatch(
            new[] { new PreparedOwnerOutcome(externalPrepared) },
            new[] { expectedExternalOwnerRevision },
            prepared,
            receipts,
            results,
            out failureReason);
    }

    public bool CommitWithExternalPreparedBatch(
        PreparedOwnerOutcome[] externalPrepared,
        long[] expectedExternalOwnerRevisions,
        PreparedMigratedProducerOutcome[] prepared,
        MigratedProducerOutcomeReceipt[] receipts,
        MigratedProducerOutcomeCommitResult[] results,
        out string failureReason)
    {
        if (externalPrepared == null
            || expectedExternalOwnerRevisions == null
            || externalPrepared.Length == 0
            || expectedExternalOwnerRevisions.Length
                != externalPrepared.Length
            || prepared == null
            || receipts == null
            || results == null
            || prepared.Length == 0
            || prepared.Length + externalPrepared.Length
                > GameplayOutcomeTransactionLimits.MaximumAtomicCommitBatchCount
            || receipts.Length != prepared.Length
            || results.Length != prepared.Length)
        {
            CancelExternalBatch(externalPrepared);
            CancelBatch(prepared);
            failureReason = "migrated-producer-external-batch-shape-invalid";
            return false;
        }

        var ownerKeys = new HashSet<string>(StringComparer.Ordinal);
        var externalKeys = new HashSet<GameplayResultKey>();
        for (int index = 0; index < externalPrepared.Length; index++)
        {
            if (!externalPrepared[index].IsValid
                || expectedExternalOwnerRevisions[index] < 0L
                || !externalKeys.Add(externalPrepared[index].ResultKey))
            {
                CancelExternalBatch(externalPrepared);
                CancelBatch(prepared);
                failureReason =
                    "migrated-producer-external-batch-token-invalid:" + index;
                return false;
            }
        }
        for (int index = 0; index < prepared.Length; index++)
        {
            PreparedMigratedProducerOutcome item = prepared[index];
            if (!ReceiptMatches(item, receipts[index])
                || !ownerKeys.Add(item.OwnerKey)
                || externalKeys.Contains(item.ResultKey))
            {
                CancelExternalBatch(externalPrepared);
                CancelBatch(prepared);
                failureReason =
                    "migrated-producer-external-batch-receipt-mismatch:" + index;
                return false;
            }
            long currentRevision = Current.OwnerRevisions.TryGetValue(
                item.OwnerKey,
                out long observed)
                    ? observed
                    : 0L;
            if (currentRevision != item.PreviousOwnerRevision
                || item.OwnerRevision != currentRevision + 1L)
            {
                CancelExternalBatch(externalPrepared);
                CancelBatch(prepared);
                failureReason =
                    "migrated-producer-external-batch-owner-revision-stale:"
                    + index;
                return false;
            }
        }

        int externalCount = externalPrepared.Length;
        var tokens = new PreparedOwnerOutcome[prepared.Length + externalCount];
        var expectedRevisions = new long[prepared.Length + externalCount];
        var ownerResults = new OwnerOutcomeCommitResult[prepared.Length + externalCount];
        for (int index = 0; index < externalCount; index++)
        {
            tokens[index] = externalPrepared[index];
            expectedRevisions[index] = expectedExternalOwnerRevisions[index];
        }
        for (int index = 0; index < prepared.Length; index++)
        {
            OutcomePrepareResult written = recorder.TryWriteReserved(
                receipts[index],
                prepared[index].Reservation,
                out PreparedOutcomeToken token);
            if (!written.Success)
            {
                for (int preparedIndex = 0;
                     preparedIndex < index;
                     preparedIndex++)
                {
                    transactions.Cancel(tokens[preparedIndex + externalCount]);
                }
                for (int reservationIndex = index;
                     reservationIndex < prepared.Length;
                     reservationIndex++)
                {
                    recorder.CancelReservation(
                        prepared[reservationIndex].Reservation);
                }
                CancelExternalBatch(externalPrepared);
                failureReason = "migrated-producer-external-batch-write-"
                    + written.Code + ":" + written.DetailCode + ":" + index;
                return false;
            }
            tokens[index + externalCount] = new PreparedOwnerOutcome(token);
            expectedRevisions[index + externalCount] = prepared[index].OwnerRevision;
        }

        MigratedProducerOutcomeAggregateState writable = Writable;
        for (int index = 0; index < prepared.Length; index++)
        {
            writable.OwnerRevisions[prepared[index].OwnerKey] =
                prepared[index].OwnerRevision;
        }

        bool committed = transactions.CommitBatch(
            tokens,
            expectedRevisions,
            ownerResults,
            out failureReason);
        if (!committed)
        {
            for (int index = 0; index < prepared.Length; index++)
                RestoreOwnerRevision(prepared[index]);
            return false;
        }

        for (int index = 0; index < prepared.Length; index++)
        {
            OwnerOutcomeCommitResult ownerResult =
                ownerResults[index + externalCount];
            results[index] = new MigratedProducerOutcomeCommitResult(
                ownerResult.DurablyCommitted,
                ownerResult.ResultKey,
                ownerResult.OutcomeId,
                ownerResult.CanonicalPayloadHash,
                ownerResult.DetailCode);
        }
        failureReason = string.Empty;
        return true;
    }

    private void CancelExternalBatch(PreparedOwnerOutcome[] prepared)
    {
        if (prepared == null)
            return;
        for (int index = 0; index < prepared.Length; index++)
            transactions.Cancel(prepared[index]);
    }

    public void Cancel(in PreparedMigratedProducerOutcome prepared)
    {
        if (prepared.Reservation.IsValid)
            recorder.CancelReservation(prepared.Reservation);
    }

    private void CancelBatch(PreparedMigratedProducerOutcome[] prepared)
    {
        if (prepared == null) return;
        for (int index = 0; index < prepared.Length; index++)
            Cancel(prepared[index]);
    }

    private static bool ReceiptMatches(
        in PreparedMigratedProducerOutcome prepared,
        in MigratedProducerOutcomeReceipt receipt) =>
        prepared.IsValid
        && receipt.Payload.ResultKey == prepared.ResultKey
        && receipt.Payload.OutcomeTypeId == prepared.OutcomeTypeId
        && receipt.Payload.OwnerRevision == prepared.OwnerRevision
        && receipt.Payload.Participants.Count == prepared.ParticipantCount
        && receipt.Payload.Metrics.Count == prepared.MetricCount
        && receipt.Payload.Subjects.Count == prepared.SubjectCount
        && receipt.Payload.Facts.Count == prepared.FactCount
        && receipt.Payload.Provenance.Count == prepared.ProvenanceCount;

    public DungeonMigratedProducerOutcomeSaveData Capture() => new()
    {
        owners = Current.OwnerRevisions
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new MigratedProducerOwnerRevisionSaveData
            {
                ownerKey = pair.Key,
                revision = pair.Value
            })
            .ToList()
    };

    public MigratedProducerOutcomeRestoreCandidate PrepareRestore(
        DungeonMigratedProducerOutcomeSaveData snapshot)
    {
        MigratedProducerOutcomeSaveValidation.RequireValid(snapshot);
        return new MigratedProducerOutcomeRestoreCandidate(
            snapshot.owners.Select(value => value.Clone()).ToArray());
    }

    public void Restore(MigratedProducerOutcomeRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        var replacement = new MigratedProducerOutcomeAggregateState();
        for (int index = 0; index < candidate.Owners.Count; index++)
        {
            MigratedProducerOwnerRevisionSaveData row = candidate.Owners[index];
            replacement.OwnerRevisions.Add(row.ownerKey, row.revision);
        }
        rootStore.Replace(replacement);
    }

    private void RestoreOwnerRevision(in PreparedMigratedProducerOutcome prepared)
    {
        if (prepared.PreviousOwnerRevision == 0L)
            Writable.OwnerRevisions.Remove(prepared.OwnerKey);
        else
            Writable.OwnerRevisions[prepared.OwnerKey] = prepared.PreviousOwnerRevision;
    }

    private static MigratedProducerOutcomeCommitResult Rejected(
        GameplayResultKey resultKey,
        string detailCode) => new(
        false,
        resultKey,
        default,
        string.Empty,
        detailCode);
}

internal static class MigratedProducerOutcomeSaveValidation
{
    public static void RequireValid(DungeonMigratedProducerOutcomeSaveData data)
    {
        if (data == null
            || data.version != DungeonMigratedProducerOutcomeSaveData.CurrentVersion
            || data.owners == null)
        {
            throw new InvalidOperationException(
                "Migrated producer outcome save header is invalid.");
        }
        string previous = null;
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < data.owners.Count; index++)
        {
            MigratedProducerOwnerRevisionSaveData row = data.owners[index];
            string key = row?.ownerKey ?? string.Empty;
            if (row == null
                || key.Length == 0
                || key.Length > 768
                || row.revision <= 0L
                || !unique.Add(key)
                || previous != null
                && string.CompareOrdinal(previous, key) >= 0)
            {
                throw new InvalidOperationException(
                    "Migrated producer outcome owner revisions are invalid or non-canonical.");
            }
            previous = key;
        }
    }
}

public sealed class MigratedProducerOutcomeSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "narrative.migrated-producer-outcomes";
    private static readonly string[] Dependencies =
    {
        GameplayOutcomeLedgerSaveSection.Id
    };
    private readonly IMigratedProducerOutcomePersistence persistence;

    public MigratedProducerOutcomeSaveSection(
        IMigratedProducerOutcomePersistence persistence) =>
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));

    public string SectionId => Id;
    public int SectionVersion => DungeonMigratedProducerOutcomeSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;
    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                "Unsupported migrated producer outcome section version "
                + sectionVersion + "; expected " + SectionVersion + ".");
            return;
        }
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            report.AddError("Required migrated producer outcome payload is missing.");
            return;
        }
        try
        {
            MigratedProducerOutcomeSaveValidation.RequireValid(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    payloadJson));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            report.AddError(
                "Migrated producer outcome payload is invalid: "
                + exception.Message);
        }
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion
            || string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                "Migrated producer outcome restore requires the exact current version.");
        }
        MigratedProducerOutcomeRestoreCandidate candidate =
            persistence.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }
}
