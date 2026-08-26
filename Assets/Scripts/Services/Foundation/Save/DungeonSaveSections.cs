using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class DungeonSaveSectionEnvelope
{
    public string sectionId = string.Empty;
    public int sectionVersion = 1;
    public DungeonSaveRestorePhase restorePhase = DungeonSaveRestorePhase.RuntimeState;
    public bool optional;
    public string payloadJson = string.Empty;
}

public enum DungeonSaveRestorePhase
{
    Foundation = 100,
    World = 200,
    Characters = 300,
    Items = 400,
    RuntimeState = 500,
    LateRuntimeState = 600,
    Presentation = 700
}

public interface IDungeonSaveSection
{
    string SectionId { get; }
    int SectionVersion { get; }
    DungeonSaveRestorePhase RestorePhase { get; }
    IReadOnlyList<string> DependsOn { get; }
    string Capture();
    void Restore(string payloadJson, int sectionVersion, DungeonGameRestoreReport report);
}

public interface IOptionalDungeonSaveSection
{
    void RestoreMissing(DungeonGameRestoreReport report);
}

/// <summary>
/// Performs payload-specific validation without touching live runtime state.
/// Save sections backed by typed DTOs must implement this contract.
/// </summary>
public interface IDungeonSaveSectionPreflight
{
    void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report);
}

/// <summary>
/// Immutable, fully prepared restore state. Creating a stage must not mutate
/// live runtime state. Commit is only invoked after every section has staged
/// successfully and aggregate references have been validated.
/// </summary>
public interface IDungeonSaveRestoreStage
{
    string SectionId { get; }
    void Commit(DungeonGameRestoreReport report);
}

public interface IDungeonDiscardableSaveRestoreStage
{
    void Discard();
}

public interface IDungeonDiscardableRestoreCandidate
{
    void Discard();
}

public interface IDungeonRestoreReportContributor
{
    void RecordRestoreResult(DungeonGameRestoreReport report);
}

/// <summary>
/// Opt-in contract used while save sections are migrated from direct restore
/// mutation to detached aggregate state preparation.
/// </summary>
public interface IDungeonStagedSaveSection
{
    IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report);
}

public interface IDungeonStagedOptionalSaveSection
{
    IDungeonSaveRestoreStage StageMissing(
        DungeonGameRestoreReport report);
}

/// <summary>
/// Declares that a staged section's Commit writes only to detached Aggregate
/// or transaction-participant candidates. If every section in a registry has
/// this contract, a failed commit can discard the candidate without replaying
/// a live-world rollback image.
/// </summary>
public interface IDungeonRollbackFreeSaveSection
{
}

/// <summary>
/// Owns non-DTO candidate state that cannot live inside the plain aggregate
/// root, such as inactive Unity world objects. Publish may adopt reversible
/// roots or visibility state; if it or a later participant fails, rollback is
/// invoked in reverse dependency order. Complete is the non-failing final
/// retirement step after every publication and the aggregate-root swap succeed.
/// </summary>
public interface IDungeonRestoreTransactionParticipant
{
    string ParticipantId { get; }
    void BeginRestoreCandidate();
    void PublishRestoreCandidate();
    /// <summary>
    /// Reverses a publication attempt, including an attempt that threw after
    /// applying only part of its candidate. Implementations that publish live
    /// state must override this method. The default preserves source
    /// compatibility for participants whose publish is already a no-op or an
    /// atomic pointer swap and whose discard releases the active candidate.
    /// </summary>
    void RollbackPublishedRestoreCandidate()
    {
        DiscardRestoreCandidate();
    }

    /// <summary>
    /// Releases the previous live image after every participant has published
    /// and the aggregate root pointer has changed. This callback must not fail.
    /// </summary>
    void CompleteRestoreCandidate()
    {
    }

    void DiscardRestoreCandidate();
}

public sealed class DungeonDelegateSaveRestoreStage : IDungeonSaveRestoreStage
{
    private readonly Action<DungeonGameRestoreReport> commit;

    public DungeonDelegateSaveRestoreStage(
        string sectionId,
        Action<DungeonGameRestoreReport> commit)
    {
        SectionId = string.IsNullOrWhiteSpace(sectionId)
            ? throw new ArgumentException("Restore stage requires a section id.", nameof(sectionId))
            : sectionId.Trim();
        this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
    }

    public string SectionId { get; }

    public void Commit(DungeonGameRestoreReport report)
    {
        commit(report ?? throw new ArgumentNullException(nameof(report)));
    }
}

/// <summary>
/// Adds a detached, fallible publication step immediately before an existing
/// restore stage commits. The inner stage remains responsible for discarding
/// its own candidate; transaction participants own rollback of any publication
/// performed by <paramref name="beforeCommit"/>.
/// </summary>
public sealed class DungeonBeforeCommitSaveRestoreStage :
    IDungeonSaveRestoreStage,
    IDungeonDiscardableSaveRestoreStage
{
    private readonly IDungeonSaveRestoreStage inner;
    private readonly Action beforeCommit;
    private bool committed;

    public DungeonBeforeCommitSaveRestoreStage(
        IDungeonSaveRestoreStage inner,
        Action beforeCommit)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.beforeCommit = beforeCommit
            ?? throw new ArgumentNullException(nameof(beforeCommit));
    }

    public string SectionId => inner.SectionId;

    public void Commit(DungeonGameRestoreReport report)
    {
        _ = report ?? throw new ArgumentNullException(nameof(report));
        if (committed)
        {
            throw new InvalidOperationException(
                $"Restore stage '{SectionId}' was already committed.");
        }

        beforeCommit();
        inner.Commit(report);
        committed = true;
    }

    public void Discard()
    {
        if (committed)
        {
            return;
        }

        if (inner is IDungeonDiscardableSaveRestoreStage discardable)
        {
            discardable.Discard();
        }
    }
}

public sealed class DungeonCandidateSaveRestoreStage<TCandidate> :
    IDungeonSaveRestoreStage,
    IDungeonDiscardableSaveRestoreStage
    where TCandidate : class
{
    private readonly Action<TCandidate> publish;
    private TCandidate candidate;
    private bool committed;

    public DungeonCandidateSaveRestoreStage(
        string sectionId,
        TCandidate candidate,
        Action<TCandidate> publish)
    {
        SectionId = string.IsNullOrWhiteSpace(sectionId)
            ? throw new ArgumentException(
                "Restore stage requires a section id.",
                nameof(sectionId))
            : sectionId.Trim();
        this.candidate = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        this.publish = publish ?? throw new ArgumentNullException(nameof(publish));
    }

    public string SectionId { get; }

    public void Commit(DungeonGameRestoreReport report)
    {
        _ = report ?? throw new ArgumentNullException(nameof(report));
        publish(candidate);
        if (candidate is IDungeonRestoreReportContributor contributor)
        {
            contributor.RecordRestoreResult(report);
        }
        committed = true;
        candidate = null;
    }

    public void Discard()
    {
        if (committed || candidate == null)
        {
            return;
        }

        if (candidate is IDungeonDiscardableRestoreCandidate discardable)
        {
            discardable.Discard();
        }
        candidate = null;
    }
}

internal sealed class DungeonRuntimeAggregateRoot
{
    private readonly Dictionary<Type, object> states;

    internal DungeonRuntimeAggregateRoot()
        : this(new Dictionary<Type, object>())
    {
    }

    private DungeonRuntimeAggregateRoot(Dictionary<Type, object> states)
    {
        this.states = states;
    }

    internal T GetOrCreate<T>(Func<T> factory)
        where T : class
    {
        if (states.TryGetValue(typeof(T), out object value))
        {
            return (T)value;
        }

        T created = (factory ?? throw new ArgumentNullException(nameof(factory)))()
            ?? throw new InvalidOperationException(
                $"Aggregate state factory returned null for {typeof(T).Name}.");
        states.Add(typeof(T), created);
        return created;
    }

    internal void Replace<T>(T state)
        where T : class
    {
        states[typeof(T)] = state
            ?? throw new ArgumentNullException(nameof(state));
    }

    internal DungeonRuntimeAggregateRoot ShallowCopy()
    {
        return new DungeonRuntimeAggregateRoot(
            new Dictionary<Type, object>(states));
    }
}

/// <summary>
/// Owns the replaceable root used by migrated runtime aggregates. During save
/// restore, aggregate stores write only to a detached candidate root. The live
/// root reference changes once after every staged section commits successfully.
/// </summary>
public sealed class DungeonRuntimeAggregateRootStore
{
    private DungeonRuntimeAggregateRoot live = new();
    private DungeonRuntimeAggregateRoot candidate;
    private HashSet<Type> candidateOwnedTypes;

    public bool IsRestoreStaging => candidate != null;
    public int PublishedRestoreRevision { get; private set; }

    public T GetOrCreate<T>(Func<T> factory)
        where T : class
    {
        return Active.GetOrCreate(factory);
    }

    public void Replace<T>(T state)
        where T : class
    {
        Active.Replace(state);
        candidateOwnedTypes?.Add(typeof(T));
    }

    public T GetOrCreateWritable<T>(
        Func<T> factory,
        Func<T, T> clone)
        where T : class
    {
        if (candidate == null)
        {
            return live.GetOrCreate(factory);
        }

        if (candidateOwnedTypes == null)
        {
            throw new InvalidOperationException(
                "Restore candidate ownership tracking is not initialized.");
        }

        T current = candidate.GetOrCreate(factory);
        if (candidateOwnedTypes.Contains(typeof(T)))
        {
            return current;
        }

        T writable = (clone ?? throw new ArgumentNullException(nameof(clone)))(
                current)
            ?? throw new InvalidOperationException(
                $"Aggregate clone returned null for {typeof(T).Name}.");
        candidate.Replace(writable);
        candidateOwnedTypes.Add(typeof(T));
        return writable;
    }

    internal void BeginRestoreStaging()
    {
        if (candidate != null)
        {
            throw new InvalidOperationException(
                "A detached runtime aggregate restore is already active.");
        }

        candidate = live.ShallowCopy();
        candidateOwnedTypes = new HashSet<Type>();
    }

    internal void PublishRestoreStaging()
    {
        live = candidate
            ?? throw new InvalidOperationException(
                "No detached runtime aggregate restore is active.");
        candidate = null;
        candidateOwnedTypes = null;
        unchecked
        {
            PublishedRestoreRevision++;
        }
    }

    internal void DiscardRestoreStaging()
    {
        candidate = null;
        candidateOwnedTypes = null;
    }

    private DungeonRuntimeAggregateRoot Active => candidate ?? live;
}

public interface IDungeonSaveSectionRegistry
{
    IReadOnlyList<IDungeonSaveSection> OrderedSections { get; }
    List<DungeonSaveSectionEnvelope> CaptureAll();
    bool RestoreAll(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report);
    bool TryGetEnvelope(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        out DungeonSaveSectionEnvelope envelope);
}

/// <summary>
/// Validates relations that span multiple raw section envelopes. It runs in
/// the registry immediately before detached section staging, so direct
/// registry restores cannot bypass whole-save joins.
/// </summary>
public interface IDungeonSaveRegistryPreflightValidator
{
    void Validate(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report);
}

public sealed class DungeonSaveSectionRegistry : IDungeonSaveSectionRegistry
{
    private readonly Dictionary<string, IDungeonSaveSection> byId;
    private readonly IReadOnlyList<IDungeonSaveSection> orderedSections;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IReadOnlyList<IDungeonRestoreTransactionParticipant>
        transactionParticipants;
    private readonly IReadOnlyList<IDungeonSaveRegistryPreflightValidator>
        registryPreflightValidators;
    private readonly bool rollbackFree;

    public DungeonSaveSectionRegistry(
        IEnumerable<IDungeonSaveSection> sections,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
        : this(
            sections,
            aggregateRootStore,
            Array.Empty<IDungeonRestoreTransactionParticipant>(),
            Array.Empty<IDungeonSaveRegistryPreflightValidator>())
    {
    }

    public DungeonSaveSectionRegistry(
        IEnumerable<IDungeonSaveSection> sections,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IEnumerable<IDungeonRestoreTransactionParticipant> transactionParticipants)
        : this(
            sections,
            aggregateRootStore,
            transactionParticipants,
            Array.Empty<IDungeonSaveRegistryPreflightValidator>())
    {
    }

    public DungeonSaveSectionRegistry(
        IEnumerable<IDungeonSaveSection> sections,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IEnumerable<IDungeonRestoreTransactionParticipant> transactionParticipants,
        IEnumerable<IDungeonSaveRegistryPreflightValidator>
            registryPreflightValidators)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        IDungeonSaveSection[] source = sections?
            .Where(section => section != null)
            .ToArray() ?? Array.Empty<IDungeonSaveSection>();

        byId = new Dictionary<string, IDungeonSaveSection>(StringComparer.Ordinal);
        foreach (IDungeonSaveSection section in source)
        {
            string sectionId = NormalizeId(section.SectionId);
            if (sectionId.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Save section {section.GetType().Name} has an empty id.");
            }

            if (section.SectionVersion <= 0)
            {
                throw new InvalidOperationException(
                    $"Save section '{sectionId}' has invalid version {section.SectionVersion}.");
            }

            if (!byId.TryAdd(sectionId, section))
            {
                throw new InvalidOperationException($"Duplicate save section id '{sectionId}'.");
            }

            if (!(section is IDungeonStagedSaveSection))
            {
                throw new InvalidOperationException(
                    $"Save section '{sectionId}' does not implement detached restore staging.");
            }

            if (section is IOptionalDungeonSaveSection
                && !(section is IDungeonStagedOptionalSaveSection))
            {
                throw new InvalidOperationException(
                    $"Optional save section '{sectionId}' does not stage missing-data restore.");
            }
        }

        orderedSections = TopologicalSort(source);
        this.transactionParticipants = ValidateTransactionParticipants(
            transactionParticipants);
        this.registryPreflightValidators = (registryPreflightValidators
                ?? Array.Empty<IDungeonSaveRegistryPreflightValidator>())
            .Where(value => value != null)
            .OrderBy(value => value.GetType().FullName, StringComparer.Ordinal)
            .ToArray();
        rollbackFree = source.All(section =>
            section is IDungeonRollbackFreeSaveSection);
    }

    public IReadOnlyList<IDungeonSaveSection> OrderedSections => orderedSections;

    public List<DungeonSaveSectionEnvelope> CaptureAll()
    {
        return orderedSections.Select(section => new DungeonSaveSectionEnvelope
        {
            sectionId = section.SectionId,
            sectionVersion = section.SectionVersion,
            restorePhase = section.RestorePhase,
            optional = section is IOptionalDungeonSaveSection,
            payloadJson = section.Capture() ?? string.Empty
        }).ToList();
    }

    public bool RestoreAll(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (!TryPreflight(envelopes, report, out Dictionary<string, DungeonSaveSectionEnvelope> savedById))
        {
            return false;
        }

        if (!TryStageAll(savedById, report, out List<IDungeonSaveRestoreStage> stages))
        {
            DiscardStages(stages, report);
            return false;
        }

        // Rollback remains only as a transitional safety net while a registry contains
        // legacy sections. An all-marker V19 registry has already completed every
        // fallible parse/build step against detached candidates, so taking another full
        // live-world snapshot here would add cost without protecting any live mutation.
        List<DungeonSaveSectionEnvelope> rollbackImage = rollbackFree
            ? null
            : CaptureAll();
        if (!TryBeginTransactionParticipants(report))
        {
            DiscardStages(stages, report);
            return false;
        }

        aggregateRootStore.BeginRestoreStaging();
        bool committed = CommitStages(stages, report);
        if (committed)
        {
            if (!TryPublishTransactionParticipants(
                    report,
                    out int publishAttemptCount))
            {
                RollbackTransactionParticipants(
                    publishAttemptCount,
                    report);
                aggregateRootStore.DiscardRestoreStaging();
                DiscardStages(stages, report);
                return false;
            }

            aggregateRootStore.PublishRestoreStaging();
            return TryCompleteTransactionParticipants(report);
        }
        aggregateRootStore.DiscardRestoreStaging();
        DiscardStages(stages, report);
        DiscardTransactionParticipants(report);
        if (rollbackFree)
        {
            return false;
        }

        DungeonGameRestoreReport rollbackReport = new DungeonGameRestoreReport();
        List<IDungeonSaveRestoreStage> rollbackStages = null;
        bool rollbackPrepared =
            TryPreflight(rollbackImage, rollbackReport, out Dictionary<string, DungeonSaveSectionEnvelope> rollbackById)
            && TryStageAll(rollbackById, rollbackReport, out rollbackStages);
        bool rollbackCommitted = false;
        if (rollbackPrepared)
        {
            rollbackPrepared = TryBeginTransactionParticipants(rollbackReport);
        }

        if (rollbackPrepared)
        {
            aggregateRootStore.BeginRestoreStaging();
            rollbackCommitted = CommitStages(rollbackStages, rollbackReport);
            if (rollbackCommitted)
            {
                rollbackCommitted = TryPublishTransactionParticipants(
                    rollbackReport,
                    out int rollbackPublishAttemptCount);
                if (rollbackCommitted)
                {
                    aggregateRootStore.PublishRestoreStaging();
                    rollbackCommitted = TryCompleteTransactionParticipants(
                        rollbackReport);
                }
                else
                {
                    RollbackTransactionParticipants(
                        rollbackPublishAttemptCount,
                        rollbackReport);
                    aggregateRootStore.DiscardRestoreStaging();
                }
            }
            else
            {
                aggregateRootStore.DiscardRestoreStaging();
                DiscardTransactionParticipants(rollbackReport);
            }
        }

        if (!rollbackPrepared || !rollbackCommitted)
        {
            report.AddError("Restore failed and the live-world rollback image could not be reapplied.");
            foreach (string rollbackError in rollbackReport.Errors)
            {
                report.AddError($"Rollback: {rollbackError}");
            }
        }

        return false;
    }

    private static IReadOnlyList<IDungeonRestoreTransactionParticipant>
        ValidateTransactionParticipants(
            IEnumerable<IDungeonRestoreTransactionParticipant> participants)
    {
        IDungeonRestoreTransactionParticipant[] ordered = participants?
            .Where(participant => participant != null)
            .OrderBy(
                participant => participant.ParticipantId,
                StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<IDungeonRestoreTransactionParticipant>();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (IDungeonRestoreTransactionParticipant participant in ordered)
        {
            string id = NormalizeId(participant.ParticipantId);
            if (id.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Restore transaction participant {participant.GetType().Name} has an empty id.");
            }

            if (!ids.Add(id))
            {
                throw new InvalidOperationException(
                    $"Duplicate restore transaction participant id '{id}'.");
            }
        }

        return ordered;
    }

    private bool TryBeginTransactionParticipants(
        DungeonGameRestoreReport report)
    {
        int begunCount = 0;
        try
        {
            for (; begunCount < transactionParticipants.Count; begunCount++)
            {
                transactionParticipants[begunCount]
                    .BeginRestoreCandidate();
            }

            return true;
        }
        catch (Exception exception)
        {
            string id = begunCount < transactionParticipants.Count
                ? transactionParticipants[begunCount].ParticipantId
                : "unknown";
            report.AddError(
                $"Failed to begin restore transaction participant '{id}': {exception.Message}");
            for (int index = begunCount - 1; index >= 0; index--)
            {
                TryDiscardTransactionParticipant(
                    transactionParticipants[index],
                    report);
            }

            return false;
        }
    }

    private bool TryPublishTransactionParticipants(
        DungeonGameRestoreReport report,
        out int publishAttemptCount)
    {
        publishAttemptCount = 0;
        for (int index = 0; index < transactionParticipants.Count; index++)
        {
            IDungeonRestoreTransactionParticipant participant =
                transactionParticipants[index];
            publishAttemptCount = index + 1;
            try
            {
                participant.PublishRestoreCandidate();
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Restore transaction participant '{participant.ParticipantId}' violated its non-failing publish contract: {exception.Message}");
                return false;
            }
        }

        return true;
    }

    private bool TryCompleteTransactionParticipants(
        DungeonGameRestoreReport report)
    {
        bool completed = true;
        // Completion retires the previous live image. Execute in reverse
        // dependency order so downstream world projections release their old
        // objects before the facility/grid root that owns them is destroyed.
        for (int index = transactionParticipants.Count - 1;
             index >= 0;
             index--)
        {
            IDungeonRestoreTransactionParticipant participant =
                transactionParticipants[index];
            try
            {
                participant.CompleteRestoreCandidate();
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Restore transaction participant '{participant.ParticipantId}' violated its non-failing completion contract: {exception.Message}");
                completed = false;
            }
        }

        return completed;
    }

    private void RollbackTransactionParticipants(
        int publishAttemptCount,
        DungeonGameRestoreReport report)
    {
        for (int index = transactionParticipants.Count - 1;
             index >= 0;
             index--)
        {
            IDungeonRestoreTransactionParticipant participant =
                transactionParticipants[index];
            if (index >= publishAttemptCount)
            {
                TryDiscardTransactionParticipant(participant, report);
                continue;
            }

            try
            {
                participant.RollbackPublishedRestoreCandidate();
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Failed to roll back published restore transaction participant '{participant.ParticipantId}': {exception.Message}");
            }
        }
    }

    private void DiscardTransactionParticipants(
        DungeonGameRestoreReport report)
    {
        for (int index = transactionParticipants.Count - 1;
             index >= 0;
             index--)
        {
            TryDiscardTransactionParticipant(
                transactionParticipants[index],
                report);
        }
    }

    private static void TryDiscardTransactionParticipant(
        IDungeonRestoreTransactionParticipant participant,
        DungeonGameRestoreReport report)
    {
        try
        {
            participant.DiscardRestoreCandidate();
        }
        catch (Exception exception)
        {
            report.AddError(
                $"Failed to discard restore transaction participant '{participant.ParticipantId}': {exception.Message}");
        }
    }

    private bool TryPreflight(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report,
        out Dictionary<string, DungeonSaveSectionEnvelope> savedById)
    {
        savedById = new Dictionary<string, DungeonSaveSectionEnvelope>(StringComparer.Ordinal);
        foreach (DungeonSaveSectionEnvelope envelope in envelopes
                     ?? Array.Empty<DungeonSaveSectionEnvelope>())
        {
            if (envelope == null)
            {
                report.AddError("V19 save contains a null section envelope.");
                continue;
            }

            string sectionId = NormalizeId(envelope.sectionId);
            if (sectionId.Length == 0)
            {
                report.AddError("V19 save contains a section with an empty id.");
                continue;
            }

            if (!savedById.TryAdd(sectionId, envelope))
            {
                report.AddError($"V19 save contains duplicate section '{sectionId}'.");
            }
        }

        foreach (IDungeonSaveSection section in orderedSections)
        {
            if (!savedById.TryGetValue(section.SectionId, out DungeonSaveSectionEnvelope envelope))
            {
                if (!(section is IOptionalDungeonSaveSection))
                {
                    report.AddError($"V19 save is missing required section '{section.SectionId}'.");
                }

                continue;
            }

            if (envelope.sectionVersion != section.SectionVersion)
            {
                report.AddError(
                    $"Section '{section.SectionId}' has version {envelope.sectionVersion}; expected {section.SectionVersion}.");
                continue;
            }

            if (envelope.restorePhase != section.RestorePhase)
            {
                report.AddError(
                    $"Section '{section.SectionId}' has restore phase {envelope.restorePhase}; expected {section.RestorePhase}.");
            }

            if (string.IsNullOrWhiteSpace(envelope.payloadJson))
            {
                report.AddError($"Section '{section.SectionId}' has an empty payload.");
                continue;
            }

            if (section is IDungeonSaveSectionPreflight preflight)
            {
                try
                {
                    preflight.ValidatePayload(envelope.payloadJson, envelope.sectionVersion, report);
                }
                catch (Exception exception)
                {
                    report.AddError(
                        $"Section '{section.SectionId}' failed preflight: {exception.Message}");
                }
            }
        }

        foreach (KeyValuePair<string, DungeonSaveSectionEnvelope> pair in savedById
                     .Where(pair => !byId.ContainsKey(pair.Key)))
        {
            if (pair.Value.optional)
            {
                report.AddWarning($"Unknown optional V19 save section '{pair.Key}' was ignored.");
            }
            else
            {
                report.AddError($"Unknown required V19 save section '{pair.Key}'.");
            }
        }

        foreach (IDungeonSaveRegistryPreflightValidator validator in
                 registryPreflightValidators)
        {
            try
            {
                validator.Validate(savedById, report);
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Registry aggregate preflight '{validator.GetType().Name}' failed: {exception.Message}");
            }
        }

        return report.Success;
    }

    private bool TryStageAll(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> savedById,
        DungeonGameRestoreReport report,
        out List<IDungeonSaveRestoreStage> stages)
    {
        stages = new List<IDungeonSaveRestoreStage>(orderedSections.Count);
        foreach (IDungeonSaveSection section in orderedSections)
        {
            if (!savedById.TryGetValue(section.SectionId, out DungeonSaveSectionEnvelope envelope))
            {
                if (section is IDungeonStagedOptionalSaveSection stagedOptional)
                {
                    IDungeonSaveRestoreStage missingStage =
                        stagedOptional.StageMissing(report);
                    if (missingStage == null)
                    {
                        report.AddError(
                            $"Optional section '{section.SectionId}' produced no missing-data stage.");
                    }
                    else
                    {
                        stages.Add(missingStage);
                    }
                }
                else if (section is IOptionalDungeonSaveSection optional)
                {
                    stages.Add(new DungeonDelegateSaveRestoreStage(
                        section.SectionId,
                        optional.RestoreMissing));
                }

                continue;
            }

            try
            {
                IDungeonSaveRestoreStage stage =
                    ((IDungeonStagedSaveSection)section).StageRestore(
                        envelope.payloadJson,
                        envelope.sectionVersion,
                        report);
                if (stage == null)
                {
                    report.AddError(
                        $"Section '{section.SectionId}' produced no restore stage.");
                }
                else if (!string.Equals(
                             stage.SectionId,
                             section.SectionId,
                             StringComparison.Ordinal))
                {
                    report.AddError(
                        $"Section '{section.SectionId}' produced stage '{stage.SectionId}'.");
                }
                else
                {
                    stages.Add(stage);
                }
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Failed to stage section '{section.SectionId}': {exception.Message}");
                break;
            }

            if (!report.Success)
            {
                break;
            }
        }

        return report.Success;
    }

    private static bool CommitStages(
        IReadOnlyList<IDungeonSaveRestoreStage> stages,
        DungeonGameRestoreReport report)
    {
        foreach (IDungeonSaveRestoreStage stage in stages
                     ?? Array.Empty<IDungeonSaveRestoreStage>())
        {
            try
            {
                stage.Commit(report);
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Failed to commit staged section '{stage.SectionId}': {exception.Message}");
                break;
            }

            if (!report.Success)
            {
                break;
            }
        }

        return report.Success;
    }

    private static void DiscardStages(
        IReadOnlyList<IDungeonSaveRestoreStage> stages,
        DungeonGameRestoreReport report)
    {
        for (int index = (stages?.Count ?? 0) - 1; index >= 0; index--)
        {
            if (stages[index] is not IDungeonDiscardableSaveRestoreStage discardable)
            {
                continue;
            }

            try
            {
                discardable.Discard();
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Failed to discard staged section '{stages[index].SectionId}': {exception.Message}");
            }
        }
    }

    public bool TryGetEnvelope(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        out DungeonSaveSectionEnvelope envelope)
    {
        string normalizedId = NormalizeId(sectionId);
        envelope = envelopes?.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                NormalizeId(candidate.sectionId),
                normalizedId,
                StringComparison.Ordinal));
        return envelope != null;
    }

    private IReadOnlyList<IDungeonSaveSection> TopologicalSort(
        IReadOnlyList<IDungeonSaveSection> sections)
    {
        List<IDungeonSaveSection> result = new List<IDungeonSaveSection>(sections.Count);
        Dictionary<string, VisitState> states =
            new Dictionary<string, VisitState>(StringComparer.Ordinal);

        foreach (IDungeonSaveSection section in sections
                     .OrderBy(item => item.RestorePhase)
                     .ThenBy(item => item.SectionId, StringComparer.Ordinal))
        {
            Visit(section, states, result);
        }

        return result;
    }

    private void Visit(
        IDungeonSaveSection section,
        IDictionary<string, VisitState> states,
        ICollection<IDungeonSaveSection> result)
    {
        string sectionId = NormalizeId(section.SectionId);
        if (states.TryGetValue(sectionId, out VisitState state))
        {
            if (state == VisitState.Visiting)
            {
                throw new InvalidOperationException(
                    $"Save section dependency cycle includes '{sectionId}'.");
            }

            return;
        }

        states[sectionId] = VisitState.Visiting;
        foreach (string dependencyId in section.DependsOn ?? Array.Empty<string>())
        {
            string normalizedDependency = NormalizeId(dependencyId);
            if (!byId.TryGetValue(normalizedDependency, out IDungeonSaveSection dependency))
            {
                throw new InvalidOperationException(
                    $"Save section '{sectionId}' depends on missing section '{normalizedDependency}'.");
            }

            if (dependency.RestorePhase > section.RestorePhase)
            {
                throw new InvalidOperationException(
                    $"Save section '{sectionId}' depends on later phase section '{normalizedDependency}'.");
            }

            Visit(dependency, states, result);
        }

        states[sectionId] = VisitState.Visited;
        result.Add(section);
    }

    private static string NormalizeId(string sectionId)
    {
        return sectionId?.Trim() ?? string.Empty;
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }
}
