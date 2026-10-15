using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class CharacterNarrativeRuntime :
    ICharacterNarrativeQuery,
    ICharacterNarrativeCommand,
    ICharacterNarrativePersistence,
    ICharacterProficiencyQuery,
    ICharacterProficiencyCommand,
    IDungeonRestoreTransactionParticipant
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly ICharacterNarrativeCatalog catalog;
    private readonly CharacterIdentityStateStore identityStates;
    private readonly WorkCompletionIdentityDeliveryLedger completionDeliveries;
    private readonly IGameContentDefinitionSource content;
    private readonly IGameEventBus gameEvents;
    private readonly IDomainFactPublisher domainFacts;
    private CharacterIdentityRuntimeStateSaveData[] stagedIdentityStates;
    private WorkCompletionIdentityDeliveryCursorSaveData[]
        stagedCompletionDeliveries;
    private CharacterIdentityRuntimeStateSaveData[] previousIdentityStates;
    private WorkCompletionIdentityDeliveryCursorSaveData[]
        previousCompletionDeliveries;
    private int previousVersion;
    private bool restoreTransactionActive;
    private bool restoreCandidatePublished;
    private int version = 1;

    public CharacterNarrativeRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        ICharacterNarrativeCatalog catalog,
        CharacterIdentityStateStore identityStates = null,
        IGameContentDefinitionSource content = null,
        WorkCompletionIdentityDeliveryLedger completionDeliveries = null,
        IDomainFactPublisher domainFacts = null)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.identityStates = identityStates ?? new CharacterIdentityStateStore();
        this.content = content;
        this.completionDeliveries = completionDeliveries
            ?? new WorkCompletionIdentityDeliveryLedger();
        this.domainFacts = domainFacts;
    }

    [Inject]
    public CharacterNarrativeRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        ICharacterNarrativeCatalog catalog,
        CharacterIdentityStateStore identityStates,
        IGameContentDefinitionSource content,
        WorkCompletionIdentityDeliveryLedger completionDeliveries,
        IGameEventBus gameEvents,
        IDomainFactPublisher domainFacts)
        : this(
            rootStore,
            catalog,
            identityStates,
            content,
            completionDeliveries,
            domainFacts)
    {
        this.gameEvents = gameEvents
            ?? throw new ArgumentNullException(nameof(gameEvents));
    }

    public int Version => version;
    public string ParticipantId =>
        "145.characters.narrative-external-state";
    public IReadOnlyCollection<CharacterNarrativeSnapshot> All => Current.Characters.Values.Select(value => value.Snapshot()).ToArray();
    public bool TryGet(CharacterId characterId, out CharacterNarrativeSnapshot snapshot)
    {
        if (Current.Characters.TryGetValue(characterId, out CharacterNarrativeRecord value))
        {
            snapshot = value.Snapshot();
            return true;
        }
        snapshot = null;
        return false;
    }

    public bool TryGetProficiency(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        long absoluteHour,
        out CharacterProficiencySnapshot snapshot)
    {
        catalog.Require(proficiencyId);
        if (!Current.Characters.ContainsKey(characterId))
        {
            snapshot = default;
            return false;
        }
        CharacterNarrativeRecord record = RequireWritable(characterId);
        bool found = record.TryGetProficiency(
            proficiencyId,
            absoluteHour,
            out snapshot);
        if (found) version = unchecked(version + 1);
        return found;
    }

    public IReadOnlyList<CharacterProficiencySnapshot> GetAllProficiencies(
        CharacterId characterId,
        long absoluteHour)
    {
        IReadOnlyList<CharacterProficiencySnapshot> snapshots =
            RequireWritable(characterId).GetAllProficiencies(
                catalog.Proficiencies,
                absoluteHour);
        version = unchecked(version + 1);
        return snapshots;
    }

    public long AddApprovedWork(
        CharacterId characterId,
        ProficiencyWorkProfile profile,
        float approvedWork,
        float difficultyMultiplier,
        ProficiencyWorkOutcome outcome,
        float learningMultiplier,
        float repetitionMultiplier,
        long absoluteHour) => AddApprovedWork(
        characterId,
        profile,
        approvedWork,
        difficultyMultiplier,
        outcome,
        learningMultiplier,
        repetitionMultiplier,
        absoluteHour,
        sourceOperationId: null);

    public long AddApprovedWork(
        CharacterId characterId,
        ProficiencyWorkProfile profile,
        float approvedWork,
        float difficultyMultiplier,
        ProficiencyWorkOutcome outcome,
        float learningMultiplier,
        float repetitionMultiplier,
        long absoluteHour,
        string sourceOperationId)
    {
        catalog.Require(profile.Primary);
        if (profile.Secondary.IsValid) catalog.Require(profile.Secondary);
        CharacterNarrativeAggregateState beforeState = CloneState(Current);
        CharacterNarrativeAggregateState projection = CloneState(beforeState);
        List<CharacterProficiencyAwardCommitReceipt> projectedReceipts = new();
        long projectedAward = ApplyApprovedWork(
            projection,
            characterId,
            profile,
            approvedWork,
            difficultyMultiplier,
            outcome,
            learningMultiplier,
            repetitionMultiplier,
            absoluteHour,
            sourceOperationId,
            projectedReceipts);
        return CommitProficiencyAwardMutation(
            beforeState,
            projection,
            projectedAward,
            projectedReceipts,
            (candidate, receipts) => ApplyApprovedWork(
                candidate,
                characterId,
                profile,
                approvedWork,
                difficultyMultiplier,
                outcome,
                learningMultiplier,
                repetitionMultiplier,
                absoluteHour,
                sourceOperationId,
                receipts));
    }

    public long AddDirectExperience(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        float experience,
        long absoluteHour,
        bool applyLearningMultiplier = true)
    {
        catalog.Require(proficiencyId);
        CharacterNarrativeAggregateState beforeState = CloneState(Current);
        CharacterNarrativeAggregateState projection = CloneState(beforeState);
        List<CharacterProficiencyAwardCommitReceipt> projectedReceipts = new();
        long projectedAward = ApplyDirectExperience(
            projection,
            characterId,
            proficiencyId,
            experience,
            absoluteHour,
            applyLearningMultiplier,
            projectedReceipts);
        return CommitProficiencyAwardMutation(
            beforeState,
            projection,
            projectedAward,
            projectedReceipts,
            (candidate, receipts) => ApplyDirectExperience(
                candidate,
                characterId,
                proficiencyId,
                experience,
                absoluteHour,
                applyLearningMultiplier,
                receipts));
    }

    private long CommitProficiencyAwardMutation(
        CharacterNarrativeAggregateState beforeState,
        CharacterNarrativeAggregateState projection,
        long projectedAward,
        IReadOnlyList<CharacterProficiencyAwardCommitReceipt> projectedReceipts,
        Func<CharacterNarrativeAggregateState,
            List<CharacterProficiencyAwardCommitReceipt>, long> applyMutation)
    {
        if (domainFacts == null || projectedReceipts.Count == 0)
        {
            rootStore.Replace(projection);
            if (projectedAward > 0L) version = unchecked(version + 1);
            PublishPromotionReceipts(projectedReceipts);
            return projectedAward;
        }

        DomainFactLease[] reserved =
            ReserveProficiencyOutcomes(projectedReceipts).ToArray();
        int previousRuntimeVersion = version;
        bool candidatePublished = false;
        long committedAward;
        List<CharacterProficiencyAwardCommitReceipt> committedReceipts;
        try
        {
            // Reservation is complete before the authoritative COW candidate is
            // mutated. The detached projection above exists only to freeze the
            // exact receipt-owned operation identity.
            CharacterNarrativeAggregateState candidate = CloneState(beforeState);
            committedReceipts = new List<CharacterProficiencyAwardCommitReceipt>();
            committedAward = applyMutation(candidate, committedReceipts);
            RequireMatchingProjection(
                projectedAward,
                projectedReceipts,
                committedAward,
                committedReceipts);

            rootStore.Replace(candidate);
            candidatePublished = true;
            if (committedAward > 0L) version = unchecked(version + 1);

            var emissions = new DomainFactEmission[reserved.Length];
            var committed = new DomainFactDelivery[reserved.Length];
            for (int index = 0; index < reserved.Length; index++)
            {
                CharacterProficiencyAwardCommitReceipt receipt =
                    committedReceipts[index];
                emissions[index] = DomainFactEmission.Create(reserved[index], receipt);
            }
            if (!domainFacts.PublishBatch(
                    emissions,
                    committed,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Proficiency award outcome batch commit was rejected: "
                    + failureReason);
            }
        }
        catch
        {
            for (int index = 0; index < reserved.Length; index++)
                domainFacts.Cancel(reserved[index]);
            if (candidatePublished)
            {
                rootStore.Replace(beforeState);
                version = previousRuntimeVersion;
            }
            throw;
        }

        // Receipt publication is an observer step after every outcome is durable.
        // Observer failures must not roll back the committed aggregate.
        PublishPromotionReceipts(committedReceipts);
        return committedAward;
    }

    private List<DomainFactLease> ReserveProficiencyOutcomes(
        IReadOnlyList<CharacterProficiencyAwardCommitReceipt> receipts)
    {
        List<DomainFactLease> reserved = new(receipts.Count);
        try
        {
            for (int index = 0; index < receipts.Count; index++)
            {
                CharacterProficiencyAwardCommitReceipt receipt = receipts[index];
                if (!domainFacts.TryReserve(receipt,
                        out DomainFactLease prepared,
                        out string failureReason))
                {
                    throw new InvalidOperationException(
                        "Proficiency award outcome reservation failed: "
                        + failureReason);
                }
                reserved.Add(prepared);
            }
            return reserved;
        }
        catch
        {
            for (int index = 0; index < reserved.Count; index++)
                domainFacts.Cancel(reserved[index]);
            throw;
        }
    }

    private long ApplyApprovedWork(
        CharacterNarrativeAggregateState state,
        CharacterId characterId,
        ProficiencyWorkProfile profile,
        float approvedWork,
        float difficultyMultiplier,
        ProficiencyWorkOutcome outcome,
        float learningMultiplier,
        float repetitionMultiplier,
        long absoluteHour,
        string sourceOperationId,
        List<CharacterProficiencyAwardCommitReceipt> receipts)
    {
        CharacterNarrativeRecord record = RequireRecord(state, characterId);
        CharacterProficiencySnapshot beforePrimary = RequireProficiencySnapshot(
            record,
            profile.Primary,
            absoluteHour);
        CharacterProficiencySnapshot beforeSecondary = default;
        bool observesSecondary = profile.Secondary.IsValid
            && profile.SecondaryWeight > 0f;
        if (observesSecondary)
        {
            beforeSecondary = RequireProficiencySnapshot(
                record,
                profile.Secondary,
                absoluteHour);
        }

        long awarded = record.AddApprovedWork(
            profile,
            approvedWork,
            difficultyMultiplier,
            outcome,
            learningMultiplier,
            repetitionMultiplier,
            absoluteHour);
        AddPromotionReceiptIfCommitted(
            receipts,
            CharacterProficiencyAwardKind.ApprovedWork,
            characterId,
            beforePrimary,
            RequireProficiencySnapshot(record, profile.Primary, absoluteHour),
            absoluteHour,
            sourceOperationId);
        if (observesSecondary)
        {
            AddPromotionReceiptIfCommitted(
                receipts,
                CharacterProficiencyAwardKind.ApprovedWork,
                characterId,
                beforeSecondary,
                RequireProficiencySnapshot(
                    record,
                    profile.Secondary,
                    absoluteHour),
                absoluteHour,
                sourceOperationId);
        }
        return awarded;
    }

    private long ApplyDirectExperience(
        CharacterNarrativeAggregateState state,
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        float experience,
        long absoluteHour,
        bool applyLearningMultiplier,
        List<CharacterProficiencyAwardCommitReceipt> receipts)
    {
        CharacterNarrativeRecord record = RequireRecord(state, characterId);
        CharacterProficiencySnapshot before = RequireProficiencySnapshot(
            record,
            proficiencyId,
            absoluteHour);
        long awarded = record.AddDirectExperience(
            proficiencyId,
            experience,
            absoluteHour,
            applyLearningMultiplier);
        AddPromotionReceiptIfCommitted(
            receipts,
            CharacterProficiencyAwardKind.DirectExperience,
            characterId,
            before,
            RequireProficiencySnapshot(record, proficiencyId, absoluteHour),
            absoluteHour);
        return awarded;
    }

    private static CharacterProficiencySnapshot RequireProficiencySnapshot(
        CharacterNarrativeRecord record,
        CharacterProficiencyId proficiencyId,
        long absoluteHour)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        if (!record.TryGetProficiency(
                proficiencyId,
                absoluteHour,
                out CharacterProficiencySnapshot snapshot))
        {
            throw new InvalidOperationException(
                $"Character proficiency '{proficiencyId.Value}' is unavailable after catalog validation.");
        }
        return snapshot;
    }

    private static void AddPromotionReceiptIfCommitted(
        ICollection<CharacterProficiencyAwardCommitReceipt> receipts,
        CharacterProficiencyAwardKind awardKind,
        CharacterId characterId,
        CharacterProficiencySnapshot before,
        CharacterProficiencySnapshot after,
        long absoluteHour,
        string causalSourceOperationId = null)
    {
        if (after.CurrentMilliExperience <= before.CurrentMilliExperience
            || (int)CareerRules.ResolveRank(after.CurrentExperience)
                <= (int)CareerRules.ResolveRank(before.CurrentExperience))
        {
            return;
        }

        receipts.Add(new CharacterProficiencyAwardCommitReceipt(
            awardKind,
            characterId,
            after.ProficiencyId,
            before.CurrentMilliExperience,
            after.CurrentMilliExperience,
            before.LifetimeMilliExperience,
            after.LifetimeMilliExperience,
            absoluteHour,
            causalSourceOperationId));
    }

    private void PublishPromotionReceipts(
        IReadOnlyList<CharacterProficiencyAwardCommitReceipt> receipts)
    {
        if (gameEvents == null) return;
        for (int index = 0; index < receipts.Count; index++)
        {
            try
            {
                gameEvents.Publish(receipts[index]);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                && exception is not StackOverflowException
                && exception is not AccessViolationException)
            {
                Debug.LogError(
                    "character-proficiency-award-post-commit-observer:"
                    + exception.GetType().Name);
            }
        }
    }

    private CharacterNarrativeAggregateState CloneState(
        CharacterNarrativeAggregateState source) =>
        CharacterNarrativeAggregateState.Restore(source.Capture(), catalog);

    private static CharacterNarrativeRecord RequireRecord(
        CharacterNarrativeAggregateState state,
        CharacterId characterId) =>
        state.Characters.TryGetValue(characterId, out CharacterNarrativeRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Unknown narrative character '{characterId.Value}'.");

    private static void RequireMatchingProjection(
        long projectedAward,
        IReadOnlyList<CharacterProficiencyAwardCommitReceipt> projectedReceipts,
        long committedAward,
        IReadOnlyList<CharacterProficiencyAwardCommitReceipt> committedReceipts)
    {
        if (projectedAward != committedAward
            || projectedReceipts.Count != committedReceipts.Count)
        {
            throw new InvalidOperationException(
                "Proficiency award mutation diverged from its reserved projection.");
        }
        for (int index = 0; index < projectedReceipts.Count; index++)
        {
            if (!string.Equals(
                    projectedReceipts[index].SourceOperationId,
                    committedReceipts[index].SourceOperationId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Proficiency award receipt diverged from its reserved projection.");
            }
        }
    }

    public long AddCombatExperience(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        float experience,
        bool training,
        string stableAwardKey,
        long absoluteHour)
    {
        catalog.Require(proficiencyId);
        CharacterNarrativeAggregateState beforeState = CloneState(Current);
        CharacterNarrativeAggregateState projection = CloneState(beforeState);
        List<CharacterProficiencyAwardCommitReceipt> projectedReceipts = new();
        long projectedAward = ApplyCombatExperience(
            projection,
            characterId,
            proficiencyId,
            experience,
            training,
            stableAwardKey,
            absoluteHour,
            projectedReceipts);
        return CommitProficiencyAwardMutation(
            beforeState,
            projection,
            projectedAward,
            projectedReceipts,
            (candidate, receipts) => ApplyCombatExperience(
                candidate,
                characterId,
                proficiencyId,
                experience,
                training,
                stableAwardKey,
                absoluteHour,
                receipts));
    }

    private long ApplyCombatExperience(
        CharacterNarrativeAggregateState state,
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        float experience,
        bool training,
        string stableAwardKey,
        long absoluteHour,
        List<CharacterProficiencyAwardCommitReceipt> receipts)
    {
        CharacterNarrativeRecord record = RequireRecord(state, characterId);
        CharacterProficiencySnapshot before = RequireProficiencySnapshot(
            record,
            proficiencyId,
            absoluteHour);
        long awarded = record.AddCombatExperience(
            proficiencyId,
            experience,
            training,
            stableAwardKey,
            absoluteHour);
        AddPromotionReceiptIfCommitted(
            receipts,
            CharacterProficiencyAwardKind.CombatExperience,
            characterId,
            before,
            RequireProficiencySnapshot(record, proficiencyId, absoluteHour),
            absoluteHour,
            stableAwardKey);
        return awarded;
    }

    public void RecordPractice(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        long absoluteHour)
    {
        catalog.Require(proficiencyId);
        RequireWritable(characterId).RecordPractice(
            proficiencyId,
            absoluteHour);
        version = unchecked(version + 1);
    }

    public bool CanPerformPractice(
        CharacterId characterId,
        string practiceId,
        int absoluteDay,
        out int nextAllowedAbsoluteDay)
    {
        nextAllowedAbsoluteDay = int.MaxValue;
        CulturalPracticeDefinitionSO practice = catalog.Practices.FirstOrDefault(
            value => string.Equals(
                value.StableId,
                practiceId?.Trim(),
                StringComparison.Ordinal));
        return practice != null
            && Current.Characters.TryGetValue(
                characterId,
                out CharacterNarrativeRecord record)
            && record.CanPerformPractice(
                practice.StableId,
                Math.Max(0, absoluteDay),
                out nextAllowedAbsoluteDay);
    }

    public bool TryPreviewAmbitionProgress(
        CharacterId characterId,
        int amount,
        out AmbitionProgressPreview preview)
    {
        preview = default;
        if (amount <= 0
            || Current.AmbitionTransitionObservations.Any(value => value.fact.characterId == characterId.Value)
            || !TryGet(characterId, out CharacterNarrativeSnapshot narrative)
            || narrative.AmbitionStatus != CharacterAmbitionStatus.Active
            || !narrative.ActiveAmbitionId.IsValid)
        {
            return false;
        }
        CharacterAmbitionDefinitionSO definition = catalog.Require(
            narrative.ActiveAmbitionId);
        preview = new AmbitionProgressPreview(
            narrative.ActiveAmbitionId,
            narrative.AmbitionProgress,
            definition.targetProgress,
            narrative.AmbitionProgress + amount >= definition.targetProgress,
            definition.completionRewards);
        return true;
    }

    public CharacterNarrativeSnapshot Register(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressed,
        IReadOnlyList<string> latent,
        IReadOnlyList<CharacterStartingProficiencyExperience>
            startingProficiencies = null)
    {
        if (Writable.Characters.ContainsKey(characterId)) throw new InvalidOperationException($"Narrative '{characterId.Value}' is already registered.");
        int index = (int)(PersistentEntityId.GetStableHash32(characterId) % (uint)catalog.Backgrounds.Count);
        CharacterBackgroundId background = new(catalog.Backgrounds[index].StableId);
        SpeciesCultureId culture = new(catalog.RequireDefaultCulture(phenotypeSpeciesId.Value).StableId);
        ResolveHeritableTraits(
            characterId,
            phenotypeSpeciesId,
            expressed,
            latent,
            out IReadOnlyList<string> resolvedExpressed,
            out IReadOnlyList<string> resolvedLatent);
        CharacterNarrativeRecord record = CharacterNarrativeRecord.Create(
            characterId,
            phenotypeSpeciesId,
            background,
            culture,
            resolvedExpressed,
            resolvedLatent,
            startingProficiencies: startingProficiencies);
        Writable.Characters.Add(characterId, record);
        version = unchecked(version + 1);
        return record.Snapshot();
    }

    public CharacterNarrativeSnapshot RegisterEnemyOrigin(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressed,
        IReadOnlyList<string> latent,
        CharacterBackgroundId backgroundId,
        SpeciesCultureId cultureId,
        string enemyArchetypeId,
        string originFactionId,
        string militaryTrainingId,
        float loyalty,
        IReadOnlyList<CharacterStartingProficiencyExperience>
            startingProficiencies = null)
    {
        if (Writable.Characters.ContainsKey(characterId))
            throw new InvalidOperationException($"Narrative '{characterId.Value}' is already registered.");
        catalog.Require(backgroundId);
        catalog.Require(cultureId);
        CharacterNarrativeRecord record = CharacterNarrativeRecord.Create(
            characterId,
            phenotypeSpeciesId,
            backgroundId,
            cultureId,
            expressed,
            latent,
            enemyArchetypeId,
            originFactionId,
            militaryTrainingId,
            loyalty,
            startingProficiencies);
        Writable.Characters.Add(characterId, record);
        version = unchecked(version + 1);
        return record.Snapshot();
    }

    public bool TryInitializeBackground(
        CharacterId characterId,
        int absoluteDay,
        out BackgroundInitializationOutcome outcome)
    {
        CharacterNarrativeRecord record = RequireWritable(characterId);
        bool initialized = record.TryInitializeBackground(
            catalog.Require(record.Snapshot().BackgroundId),
            absoluteDay,
            out outcome);
        if (initialized)
        {
            version = unchecked(version + 1);
        }
        return initialized;
    }

    public void StartAmbition(CharacterId characterId, CharacterAmbitionId ambitionId, int absoluteDay)
    {
        RequireAmbitionObservationSettled(characterId);
        CharacterAmbitionDefinitionSO definition = catalog.Require(ambitionId);
        RequireWritable(characterId).StartAmbition(
            ambitionId,
            absoluteDay,
            checked(absoluteDay + definition.deadlineDays));
        version = unchecked(version + 1);
    }

    public void AddAmbitionProgress(CharacterId characterId, int amount, int absoluteDay)
    {
        RequireAmbitionObservationSettled(characterId);
        CharacterNarrativeRecord record = RequireWritable(characterId);
        CharacterAmbitionDefinitionSO definition = catalog.Require(record.Snapshot().ActiveAmbitionId);
        record.AddAmbitionProgress(amount, definition.targetProgress, absoluteDay);
        version = unchecked(version + 1);
    }

    public void FailAmbition(CharacterId characterId, int absoluteDay) { RequireAmbitionObservationSettled(characterId); RequireWritable(characterId).CloseAmbition(CharacterAmbitionStatus.Failed, absoluteDay); version = unchecked(version + 1); }
    public void AbandonAmbition(CharacterId characterId, int absoluteDay) { RequireAmbitionObservationSettled(characterId); RequireWritable(characterId).CloseAmbition(CharacterAmbitionStatus.Abandoned, absoluteDay); version = unchecked(version + 1); }
    private void RequireAmbitionObservationSettled(CharacterId characterId)
    {
        if (Current.AmbitionTransitionObservations.Any(value => value.fact.characterId == characterId.Value))
            throw new InvalidOperationException("Ambition transition observation is pending; recover it before another ambition mutation.");
    }
    public void BeginAssimilation(CharacterId characterId, SpeciesCultureId targetCultureId) { catalog.Require(targetCultureId); RequireWritable(characterId).BeginAssimilation(targetCultureId); version = unchecked(version + 1); }
    public void AdvanceAssimilationDay(CharacterId characterId) { RequireWritable(characterId).AdvanceAssimilationDay(); version = unchecked(version + 1); }
    public void RecordPracticeParticipation(
        CharacterId characterId,
        string practiceId,
        SpeciesCultureId practiceCultureId,
        int assimilationDays,
        int absoluteDay)
    {
        CulturalPracticeDefinitionSO practice = catalog.Practices.Single(value =>
            string.Equals(
                value.StableId,
                practiceId?.Trim(),
                StringComparison.Ordinal));
        if (!string.Equals(
                practice.cultureId,
                practiceCultureId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Practice culture does not match its authored definition.");
        }
        catalog.Require(practiceCultureId);
        RequireWritable(characterId).RecordPracticeParticipation(
            practice.StableId,
            practiceCultureId,
            assimilationDays,
            absoluteDay);
        version = unchecked(version + 1);
    }
    public void RecordPracticeNeglect(
        CharacterId characterId,
        string practiceId,
        int absoluteDay)
    {
        CulturalPracticeDefinitionSO practice = catalog.Practices.Single(value =>
            string.Equals(
                value.StableId,
                practiceId?.Trim(),
                StringComparison.Ordinal));
        RequireWritable(characterId).RecordPracticeNeglect(
            practice.StableId,
            absoluteDay);
        version = unchecked(version + 1);
    }
    public void RecordResolvedEvent(CharacterId characterId, NarrativeEventId eventId, string choiceId, int absoluteDay)
    {
        RequireWritable(characterId).RecordEvent(
            catalog.Require(eventId),
            choiceId,
            absoluteDay,
            archivedId => catalog.Require(new NarrativeEventId(archivedId)).category);
        version = unchecked(version + 1);
    }
    public void MarkHeritableTraitsAnalyzed(CharacterId characterId)
    {
        RequireWritable(characterId).MarkHeritableTraitsAnalyzed();
        version = unchecked(version + 1);
    }

    public CharacterNarrativeWorldSaveData Capture()
    {
        CharacterNarrativeWorldSaveData data = Current.Capture();
        data.identityStates = identityStates.Capture()
            .Select(value => value.Clone()).ToList();
        data.workCompletionDeliveries = completionDeliveries.Capture()
            .Select(value => value.Clone()).ToList();
        return data;
    }
    public CharacterNarrativeAggregateState PrepareRestore(CharacterNarrativeWorldSaveData data) => CharacterNarrativeAggregateState.Restore(data, catalog);
    public void PublishRestore(CharacterNarrativeAggregateState candidate)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));

        CharacterIdentityRuntimeStateSaveData[] identityCandidate =
            ValidateIdentityCandidate(candidate.IdentityStates);
        WorkCompletionIdentityDeliveryCursorSaveData[] deliveryCandidate =
            WorkCompletionIdentityDeliveryLedger.ValidateAndClone(
                candidate.WorkCompletionDeliveries);

        if (restoreTransactionActive)
        {
            if (stagedIdentityStates != null
                || stagedCompletionDeliveries != null)
                throw new InvalidOperationException(
                    "Character narrative restore candidate is already staged.");
            stagedIdentityStates = identityCandidate;
            stagedCompletionDeliveries = deliveryCandidate;
            rootStore.Replace(candidate);
            return;
        }

        CharacterIdentityRuntimeStateSaveData[] identityBefore =
            identityStates.Capture()
                .Select(value => value.Clone())
                .ToArray();
        WorkCompletionIdentityDeliveryCursorSaveData[] deliveryBefore =
            completionDeliveries.Capture()
                .Select(value => value.Clone())
                .ToArray();
        try
        {
            ApplyExternalRestoreCandidate(
                identityCandidate,
                deliveryCandidate);
            rootStore.Replace(candidate);
            version = unchecked(version + 1);
        }
        catch
        {
            identityStates.RestoreTrustedTransactionSnapshot(identityBefore);
            completionDeliveries.Restore(deliveryBefore);
            throw;
        }
    }

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive)
            throw new InvalidOperationException(
                "Character narrative restore transaction is already active.");
        previousIdentityStates = identityStates.Capture()
            .Select(value => value.Clone())
            .ToArray();
        previousCompletionDeliveries = completionDeliveries.Capture()
            .Select(value => value.Clone())
            .ToArray();
        previousVersion = version;
        stagedIdentityStates = null;
        stagedCompletionDeliveries = null;
        restoreCandidatePublished = false;
        restoreTransactionActive = true;
    }

    public void PublishRestoreCandidate()
    {
        RequireActiveRestoreCandidate();
        restoreCandidatePublished = true;
        ApplyExternalRestoreCandidate(
            stagedIdentityStates,
            stagedCompletionDeliveries);
        version = unchecked(previousVersion + 1);
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (!restoreTransactionActive)
            return;
        if (restoreCandidatePublished)
        {
            identityStates.RestoreTrustedTransactionSnapshot(
                previousIdentityStates);
            completionDeliveries.Restore(previousCompletionDeliveries);
            version = previousVersion;
        }
        ClearRestoreTransaction();
    }

    public void CompleteRestoreCandidate() => ClearRestoreTransaction();

    public void DiscardRestoreCandidate()
    {
        if (restoreCandidatePublished)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }
        ClearRestoreTransaction();
    }

    private CharacterIdentityRuntimeStateSaveData[] ValidateIdentityCandidate(
        IEnumerable<CharacterIdentityRuntimeStateSaveData> source)
    {
        CharacterIdentityRuntimeStateSaveData[] candidate =
            (source ?? throw new ArgumentNullException(nameof(source)))
            .Select(value => value?.Clone()
                ?? throw new InvalidOperationException(
                    "Character identity restore candidate contains null."))
            .ToArray();
        if (candidate.Length > 0 && content == null)
            throw new InvalidOperationException(
                "Identity rule state restore requires the content definition source.");

        CharacterIdentityStateStore validator = new();
        validator.Restore(
            candidate,
            content?.GetAll<CharacterTraitSO>()
                ?? Array.Empty<CharacterTraitSO>());
        return validator.Capture()
            .Select(value => value.Clone())
            .ToArray();
    }

    private void ApplyExternalRestoreCandidate(
        IEnumerable<CharacterIdentityRuntimeStateSaveData> identityCandidate,
        IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData>
            deliveryCandidate)
    {
        identityStates.RestoreTrustedTransactionSnapshot(
            identityCandidate
                ?? throw new InvalidOperationException(
                    "Character identity restore candidate was not staged."));
        completionDeliveries.Restore(
            deliveryCandidate
                ?? throw new InvalidOperationException(
                    "Work-completion restore candidate was not staged."));
    }

    private void RequireActiveRestoreCandidate()
    {
        if (!restoreTransactionActive
            || stagedIdentityStates == null
            || stagedCompletionDeliveries == null)
            throw new InvalidOperationException(
                "Character narrative restore candidate was not staged.");
    }

    private void ClearRestoreTransaction()
    {
        stagedIdentityStates = null;
        stagedCompletionDeliveries = null;
        previousIdentityStates = null;
        previousCompletionDeliveries = null;
        restoreCandidatePublished = false;
        restoreTransactionActive = false;
    }

    private void ResolveHeritableTraits(
        CharacterId characterId,
        CharacterSpeciesId speciesId,
        IReadOnlyList<string> expressed,
        IReadOnlyList<string> latent,
        out IReadOnlyList<string> resolvedExpressed,
        out IReadOnlyList<string> resolvedLatent)
    {
        string[] authored = (expressed ?? Array.Empty<string>())
            .Concat(latent ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (authored.Length > 0)
        {
            foreach (string id in authored) catalog.RequireHeritable(id);
            string[] expressedSelection = (expressed ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .Take(4)
                .ToArray();
            resolvedExpressed = expressedSelection;
            resolvedLatent = (latent ?? Array.Empty<string>())
                .Where(value => !expressedSelection.Contains(value, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToArray();
            return;
        }

        HeritableTraitDefinitionSO[] candidates = catalog.HeritableTraits
            .Where(value => value.compatibleSpeciesTags == null
                || value.compatibleSpeciesTags.Count == 0
                || value.compatibleSpeciesTags.Contains(
                    speciesId.Value,
                    StringComparer.Ordinal))
            .OrderBy(value => PersistentEntityId.GetStableHash32(
                characterId.Value + ":" + value.traitId))
            .ThenBy(value => value.traitId, StringComparer.Ordinal)
            .ToArray();
        List<HeritableTraitDefinitionSO> selected = new();
        foreach (HeritableTraitDefinitionSO candidate in candidates)
        {
            if (selected.Any(value => !string.IsNullOrWhiteSpace(value.incompatibilityGroup)
                && string.Equals(
                    value.incompatibilityGroup,
                    candidate.incompatibilityGroup,
                    StringComparison.Ordinal)))
                continue;
            selected.Add(candidate);
            if (selected.Count == 3) break;
        }
        resolvedExpressed = selected.Take(2).Select(value => value.traitId).ToArray();
        resolvedLatent = selected.Skip(2).Take(1).Select(value => value.traitId).ToArray();
    }

    private CharacterNarrativeAggregateState Current => rootStore.GetOrCreate(CreateEmpty);
    private CharacterNarrativeAggregateState Writable => rootStore.GetOrCreateWritable(CreateEmpty, value => CharacterNarrativeAggregateState.Restore(value.Capture(), catalog));
    private static CharacterNarrativeAggregateState CreateEmpty() => new();
    private CharacterNarrativeRecord RequireWritable(CharacterId id) => Writable.Characters.TryGetValue(id, out CharacterNarrativeRecord value)
        ? value
        : throw new KeyNotFoundException($"Unknown narrative character '{id.Value}'.");
}

/// <summary>
/// Converts one physical trait-analysis kit at an operational trait analyzer
/// into a persistent player-facing reveal of the subject's latent hereditary
/// traits. Internal inheritance rules retain the latent traits before analysis;
/// only their visible projection changes.
/// </summary>
public interface ITraitAnalysisRecovery
{
    bool TryRecoverPending(out DomainFailure failure);
}

public sealed class TraitAnalysisCommandRuntime :
    ITraitAnalysisCommand,
    ITraitAnalysisRecovery
{
    private const string TraitAnalyzerBuildingId = "building:8879";
    private const string TraitAnalysisKitId = "medical:trait-analysis-kit";
    public const string DispositionReasonCode =
        "heritable-trait-analysis-kit-consumed";

    private readonly ICharacterNarrativeQuery query;
    private readonly ICharacterNarrativePersistence persistence;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IStockQuery stock;
    private readonly IPhysicalItemBatchDispositionService physicalItems;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher factRecovery;
    private readonly IGameCalendar calendar;
    private readonly ICharacterWorldQuery characters;

    public TraitAnalysisCommandRuntime(
        ICharacterNarrativeQuery query,
        ICharacterNarrativePersistence persistence,
        IFacilityCapabilityQuery facilities,
        IStockQuery stock,
        IPhysicalItemBatchDispositionService physicalItems,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher factRecovery,
        IGameCalendar calendar,
        ICharacterWorldQuery characters)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.factRecovery = factRecovery ?? throw new ArgumentNullException(nameof(factRecovery));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
    }

    public bool TryAnalyze(
        CharacterId characterId,
        out IReadOnlyList<string> revealedLatentTraitIds,
        out DomainFailure failure)
    {
        revealedLatentTraitIds = Array.Empty<string>();
        failure = DomainFailure.None;
        CharacterNarrativeWorldSaveData beforeRecovery = persistence.Capture();
        TraitAnalysisCommitSaveData prior = beforeRecovery.pendingTraitAnalysis
            ?? new TraitAnalysisCommitSaveData();
        bool samePendingRequest =
            (TraitAnalysisCommitPhase)prior.phase
                != TraitAnalysisCommitPhase.None
            && string.Equals(
                prior.characterId,
                characterId.Value,
                StringComparison.Ordinal);
        if (!TryRecoverPendingCore(out bool recoveredOperation, out failure))
            return false;
        if (samePendingRequest && recoveredOperation)
        {
            if (query.TryGet(characterId, out CharacterNarrativeSnapshot recovered))
            {
                revealedLatentTraitIds =
                    recovered.VisibleLatentHeritableTraitIds;
            }
            return true;
        }
        if (!characterId.IsValid
            || !query.TryGet(characterId, out CharacterNarrativeSnapshot current)
            || current.HeritableTraitsAnalyzed
            || current.LatentHeritableTraitIds.Count == 0)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        BuildableObject facility = facilities.FindOperational(
                FacilityCapabilityKind.Medical,
                TraitAnalyzerBuildingId)
            .OrderBy(value => value?.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (facility == null || !facility.PersistentInstanceId.IsValid)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }

        CharacterActor actor = characters.Characters.FirstOrDefault(value =>
            value != null
            && CharacterPersistentIdentity.TryGet(value, out CharacterId id)
            && id.Equals(characterId));
        string characterDisplayName = actor?.Identity?.DisplayName?.Trim()
            ?? string.Empty;
        if (characterDisplayName.Length == 0)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                characterId.Value,
                "trait-analysis-character-display-missing");
            return false;
        }

        WorldItemStackSnapshot kit = stock.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && !value.Forbidden
                && value.AvailableQuantity > 0
                && string.Equals(
                    value.ItemId,
                    TraitAnalysisKitId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (kit == null)
        {
            failure = new DomainFailure(FailureCode.ProductionMaterialsMissing);
            return false;
        }

        CharacterNarrativeWorldSaveData intent = persistence.Capture();
        int operationSequence = intent.nextTraitAnalysisOperationSequence;
        string operationId = FormatOperationId(
            characterId,
            operationSequence);
        string facilityDisplayName = facility.BuildingData?.objectName?.Trim();
        if (string.IsNullOrWhiteSpace(facilityDisplayName))
            facilityDisplayName = facility.name?.Trim();
        if (string.IsNullOrWhiteSpace(facilityDisplayName))
            facilityDisplayName = TraitAnalyzerBuildingId;
        intent.pendingTraitAnalysis = new TraitAnalysisCommitSaveData
        {
            phase = (int)TraitAnalysisCommitPhase.IntentRecorded,
            operationSequence = operationSequence,
            operationId = operationId,
            characterId = characterId.Value,
            characterDisplayName = characterDisplayName,
            facilityInstanceId = facility.PersistentInstanceId.Value,
            facilityDefinitionId = TraitAnalyzerBuildingId,
            facilityDisplayName = facilityDisplayName,
            itemId = TraitAnalysisKitId,
            sourceStackId = kit.StackId,
            absoluteDay = Mathf.Max(1, calendar.Day),
            latentTraitIds = current.LatentHeritableTraitIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList()
        };
        try
        {
            persistence.PublishRestore(persistence.PrepareRestore(intent));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                characterId.Value,
                "trait-analysis-intent-rejected",
                exception.GetType().Name);
            return false;
        }

        if (!physicalItems.TryCommitPending(
                new[] { new PhysicalItemTransformInput(kit.StackId, 1) },
                PhysicalItemDispositionKind.Sink,
                operationId,
                DispositionReasonCode,
                out _,
                out string consumeFailure))
        {
            TryClearUncommittedIntent(operationId);
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                kit.StackId,
                consumeFailure ?? string.Empty);
            return false;
        }

        if (!TryRecoverPendingCore(out bool completed, out failure)
            || !completed)
            return false;
        if (query.TryGet(characterId, out CharacterNarrativeSnapshot result))
        {
            revealedLatentTraitIds = result.VisibleLatentHeritableTraitIds;
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryRecoverPending(out DomainFailure failure) =>
        TryRecoverPendingCore(out _, out failure);

    public static string FormatOperationId(
        CharacterId characterId,
        int sequence) =>
        $"trait-analysis:{characterId.Value}:{sequence:D8}";

    private bool TryRecoverPendingCore(
        out bool completedOperation,
        out DomainFailure failure)
    {
        completedOperation = false;
        CharacterNarrativeWorldSaveData captured = persistence.Capture();
        TraitAnalysisCommitSaveData pending = captured.pendingTraitAnalysis
            ?? new TraitAnalysisCommitSaveData();
        TraitAnalysisCommitPhase phase =
            (TraitAnalysisCommitPhase)pending.phase;
        if (phase == TraitAnalysisCommitPhase.None)
        {
            failure = DomainFailure.None;
            return true;
        }

        bool hasPhysicalReceipt = physicalItems.TryGetPending(
            pending.operationId,
            out PhysicalItemBatchDispositionReceipt physicalReceipt);
        if (hasPhysicalReceipt && !ReceiptMatches(pending, physicalReceipt))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                pending.operationId,
                "trait-analysis-physical-receipt-mismatch");
            return false;
        }

        if (phase == TraitAnalysisCommitPhase.IntentRecorded)
        {
            if (!hasPhysicalReceipt)
            {
                ClearPending(captured, advanceSequence: false);
                try
                {
                    persistence.PublishRestore(
                        persistence.PrepareRestore(captured));
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    failure = new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable,
                        pending.operationId,
                        "trait-analysis-intent-clear-failed",
                        exception.GetType().Name);
                    return false;
                }
                failure = DomainFailure.None;
                return true;
            }

            captured.pendingTraitAnalysis.physicalCommitId =
                physicalReceipt.CommitId;
            captured.pendingTraitAnalysis.physicalSourceStackIds =
                physicalReceipt.SourceStackIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();
            captured.pendingTraitAnalysis.physicalInputMassGrams =
                physicalReceipt.InputMassGrams;
            try
            {
                CharacterNarrativeAggregateState candidate =
                    persistence.PrepareRestore(captured);
                if (!candidate.Characters.TryGetValue(
                        new CharacterId(pending.characterId),
                        out CharacterNarrativeRecord candidateRecord))
                    throw new InvalidOperationException(
                        "Trait-analysis character disappeared.");
                candidateRecord.MarkHeritableTraitsAnalyzed();
                candidate.PendingTraitAnalysis.phase =
                    (int)TraitAnalysisCommitPhase.RevealPublished;
                persistence.PublishRestore(candidate);
                phase = TraitAnalysisCommitPhase.RevealPublished;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    pending.characterId,
                    "trait-analysis-reveal-publication-failed",
                    exception.GetType().Name);
                return false;
            }
        }

        if (phase == TraitAnalysisCommitPhase.RevealPublished)
        {
            CharacterNarrativeWorldSaveData revealed = persistence.Capture();
            TraitAnalysisCommitSaveData evidence =
                revealed.pendingTraitAnalysis;
            if (!physicalItems.TryGetPending(
                    evidence.operationId,
                    out physicalReceipt)
                || !ReceiptMatches(evidence, physicalReceipt))
            {
                failure = new DomainFailure(
                    FailureCode.ItemTransferConsumptionFailed,
                    evidence.operationId,
                    "trait-analysis-physical-receipt-missing");
                return false;
            }
            DomainFactLease lease = default;
            try
            {
                HeritableTraitsAnalyzedFact fact = evidence.CreateCompletedFact();
                if (evidence.publicationKey.Length == 0)
                {
                    if (!facts.TryReserve(fact, out lease, out string reserveFailure))
                    {
                        failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable,
                            evidence.operationId, "trait-analysis-observation-reserve-failed", reserveFailure);
                        return false;
                    }
                    evidence.publicationKey = lease.PublicationKey;
                    // Persist the opaque identity before an observer can commit.
                    persistence.PublishRestore(persistence.PrepareRestore(revealed));
                }

                DomainFactDelivery delivery;
                if (lease.IsValid) delivery = facts.Publish(lease, fact);
                else
                {
                    var deliveries = new DomainFactDelivery[1];
                    factRecovery.ReplayBatch(new[] { DomainFactReplay.Create(evidence.publicationKey, fact) },
                        deliveries, out _);
                    delivery = deliveries[0];
                }
                if (delivery.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
                {
                    if (delivery.OperationKey != evidence.publicationKey
                        || string.IsNullOrEmpty(delivery.PublicationId)
                        || string.IsNullOrEmpty(delivery.Fingerprint)
                        || evidence.publicationId.Length > 0 && (evidence.publicationId != delivery.PublicationId
                            || evidence.publicationFingerprint != delivery.Fingerprint))
                        throw new InvalidOperationException("Trait-analysis observation proof conflicts with its pending owner.");
                    revealed = persistence.Capture();
                    var accepted = revealed.pendingTraitAnalysis;
                    if (accepted.operationId != evidence.operationId || accepted.publicationKey != evidence.publicationKey)
                        throw new InvalidOperationException("Trait-analysis pending owner changed during observation.");
                    accepted.publicationId = delivery.PublicationId;
                    accepted.publicationFingerprint = delivery.Fingerprint;
                    if (delivery.Completed)
                        accepted.phase = (int)TraitAnalysisCommitPhase.OutcomePublished;
                    persistence.PublishRestore(persistence.PrepareRestore(revealed));
                }
                if (!delivery.Completed)
                {
                    failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable, evidence.operationId,
                        "trait-analysis-observation-incomplete", delivery.Phase + ":" + delivery.Detail);
                    return false;
                }
                phase = TraitAnalysisCommitPhase.OutcomePublished;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    evidence.operationId,
                    "trait-analysis-observation-failed",
                    exception.GetType().Name + ":" + exception.Message);
                return false;
            }
            finally
            {
                if (lease.IsValid) facts.Cancel(lease);
            }
        }

        CharacterNarrativeWorldSaveData terminal = persistence.Capture();
        TraitAnalysisCommitSaveData terminalPending =
            terminal.pendingTraitAnalysis;
        if (physicalItems.TryGetPending(
                terminalPending.operationId,
                out physicalReceipt)
            && !physicalItems.Acknowledge(
                physicalReceipt.CommitId,
                out string acknowledgeFailure))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferConsumptionFailed,
                terminalPending.operationId,
                "trait-analysis-physical-acknowledge-failed",
                acknowledgeFailure ?? string.Empty);
            return false;
        }
        ClearPending(terminal, advanceSequence: true);
        try
        {
            persistence.PublishRestore(persistence.PrepareRestore(terminal));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                terminalPending.operationId,
                "trait-analysis-terminal-clear-failed",
                exception.GetType().Name);
            return false;
        }
        completedOperation = true;
        failure = DomainFailure.None;
        return true;
    }

    private void TryClearUncommittedIntent(string operationId)
    {
        if (physicalItems.TryGetPending(operationId, out _))
            return;
        CharacterNarrativeWorldSaveData captured = persistence.Capture();
        if ((TraitAnalysisCommitPhase)captured.pendingTraitAnalysis.phase
                == TraitAnalysisCommitPhase.IntentRecorded
            && string.Equals(
                captured.pendingTraitAnalysis.operationId,
                operationId,
                StringComparison.Ordinal))
        {
            ClearPending(captured, advanceSequence: false);
            persistence.PublishRestore(persistence.PrepareRestore(captured));
        }
    }

    private static bool ReceiptMatches(
        TraitAnalysisCommitSaveData pending,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            pending.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            DispositionReasonCode,
            StringComparison.Ordinal)
        && receipt.Quantity == 1
        && receipt.SourceStackIds.Count == 1
        && string.Equals(
            receipt.SourceStackIds[0],
            pending.sourceStackId,
            StringComparison.Ordinal)
        && ((TraitAnalysisCommitPhase)pending.phase == TraitAnalysisCommitPhase.IntentRecorded
            || pending.physicalCommitId == receipt.CommitId
                && pending.physicalInputMassGrams == receipt.InputMassGrams
                && pending.physicalSourceStackIds != null
                && pending.physicalSourceStackIds.SequenceEqual(receipt.SourceStackIds, StringComparer.Ordinal));

    private static void ClearPending(
        CharacterNarrativeWorldSaveData data,
        bool advanceSequence)
    {
        if (advanceSequence)
        {
            data.nextTraitAnalysisOperationSequence = checked(
                data.nextTraitAnalysisOperationSequence + 1);
        }
        data.pendingTraitAnalysis = new TraitAnalysisCommitSaveData();
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException;
}

public sealed class TraitAnalysisRecoveryAdapter : IStartable
{
    private readonly ITraitAnalysisRecovery recovery;

    public TraitAnalysisRecoveryAdapter(ITraitAnalysisRecovery recovery)
    {
        this.recovery = recovery
            ?? throw new ArgumentNullException(nameof(recovery));
    }

    public void Start()
    {
        if (!recovery.TryRecoverPending(out DomainFailure failure))
        {
            throw new InvalidOperationException(
                "Trait-analysis recovery failed: " + failure);
        }
    }
}

public sealed class CharacterNarrativeApplicationAdapter : IStartable, IDisposable, IDungeonSaveRestoreCompletedHook
{
    private readonly ICharacterNarrativeQuery query;
    private readonly ICharacterNarrativeCommand commands;
    private readonly ICharacterNarrativeCatalog catalog;
    private readonly ICharacterLifeQuery life;
    private readonly IGameEventBus events;
    private readonly IContentRequirementEvaluator requirements;
    private readonly IV20MilestoneWorldSnapshotQuery world;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterNarrativePersistence persistence;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher factRecovery;
    private IDisposable dayEndedSubscription;

    public CharacterNarrativeApplicationAdapter(
        ICharacterNarrativeQuery query,
        ICharacterNarrativeCommand commands,
        ICharacterNarrativeCatalog catalog,
        ICharacterLifeQuery life,
        IGameEventBus events,
        IContentRequirementEvaluator requirements,
        IV20MilestoneWorldSnapshotQuery world,
        ICharacterWorldQuery characters,
        ICharacterNarrativePersistence persistence,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher factRecovery)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.requirements = requirements
            ?? throw new ArgumentNullException(nameof(requirements));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.factRecovery = factRecovery ?? throw new ArgumentNullException(nameof(factRecovery));
    }

    public void Start()
    {
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
        RecoverPendingAmbitionTransition();
    }
    public void OnRestoreCompleted() => RecoverPendingAmbitionTransition();
    public void Dispose() { dayEndedSubscription?.Dispose(); dayEndedSubscription = null; }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        RecoverPendingAmbitionTransition();
        int nextDay = ended.day + 1;
        RunMilestoneEvaluationSnapshot snapshot = world.Build(nextDay);
        foreach (CharacterLifeRecord record in life.Records.OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal))
        {
            if (!query.TryGet(record.CharacterId, out CharacterNarrativeSnapshot narrative))
            {
                narrative = commands.Register(record.CharacterId, record.PhenotypeSpeciesId, Array.Empty<string>(), Array.Empty<string>());
            }
            if (!narrative.BackgroundInitialized
                && commands.TryInitializeBackground(
                    record.CharacterId,
                    nextDay,
                    out BackgroundInitializationOutcome background))
            {
                ApplyBackgroundInitialization(record.CharacterId, background);
                query.TryGet(record.CharacterId, out narrative);
                ApplyInitialCultureAttitudes(record.CharacterId, narrative);
            }
            if (narrative.AmbitionStatus == CharacterAmbitionStatus.Active)
            {
                CharacterAmbitionDefinitionSO active = catalog.Require(
                    narrative.ActiveAmbitionId);
                bool deadlineReached =
                    narrative.AmbitionDueAbsoluteDay > 0
                    && nextDay >= narrative.AmbitionDueAbsoluteDay;
                bool failureConditionsMet =
                    HasRequirements(active.failureConditions)
                    && requirements.TryEvaluate(
                        active.failureConditions,
                        snapshot,
                        new[] { record.CharacterId.Value },
                        out _);
                if (deadlineReached || failureConditionsMet)
                {
                    TransitionAmbition(
                        record.CharacterId,
                        narrative,
                        active,
                        nextDay,
                        fail: true,
                        failureReason: deadlineReached
                            ? CharacterAmbitionTransitionFactData
                                .DeadlineReason
                            : CharacterAmbitionTransitionFactData
                                .FailureReason);
                }
                continue;
            }
            if (record.LifeStage >= CharacterLifeStage.Adult
                && nextDay >= narrative.NextAmbitionAllowedAbsoluteDay)
            {
                uint seed = PersistentEntityId.GetStableHash32(record.CharacterId.Value + ":" + nextDay);
                CharacterAmbitionDefinitionSO[] eligible = catalog.Ambitions
                    .Where(value => requirements.TryEvaluate(
                        value.activationRequirements,
                        snapshot,
                        new[] { record.CharacterId.Value },
                        out _))
                    .OrderBy(value => value.StableId, StringComparer.Ordinal)
                    .ToArray();
                if (eligible.Length == 0)
                {
                    continue;
                }
                CharacterAmbitionDefinitionSO selected = eligible[
                    (int)(seed % (uint)eligible.Length)];
                TransitionAmbition(
                    record.CharacterId,
                    narrative,
                    selected,
                    nextDay,
                    fail: false,
                    failureReason: string.Empty);
            }
        }
    }

    private void TransitionAmbition(
        CharacterId characterId,
        CharacterNarrativeSnapshot before,
        CharacterAmbitionDefinitionSO definition,
        int transitionDay,
        bool fail,
        string failureReason)
    {
        if (before == null)
            throw new ArgumentNullException(nameof(before));
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        CharacterAmbitionStatus afterStatus = fail
            ? CharacterAmbitionStatus.Failed
            : CharacterAmbitionStatus.Active;
        string ambitionId = fail
            ? before.ActiveAmbitionId.Value
            : definition.StableId;
        var data = new CharacterAmbitionTransitionFactData
        {
            characterId = characterId.Value, characterDisplayName = RequireCharacterDisplayName(characterId),
            ambitionId = ambitionId, ambitionDisplayName = definition.DisplayName?.Trim(),
            definitionRevision = definition.AuthoringRevision, beforeStatus = before.AmbitionStatus,
            afterStatus = afterStatus, transitionDay = transitionDay,
            startDayBefore = before.AmbitionStartedAbsoluteDay, startDayAfter = fail ? 0 : transitionDay,
            dueDayBefore = before.AmbitionDueAbsoluteDay,
            dueDayAfter = fail ? 0 : checked(transitionDay + definition.deadlineDays),
            progressBefore = before.AmbitionProgress, progressAfter = 0,
            nextAllowedDayBefore = before.NextAmbitionAllowedAbsoluteDay,
            nextAllowedDayAfter = fail ? checked(transitionDay + 10) : before.NextAmbitionAllowedAbsoluteDay,
            reasonCode = fail ? failureReason : CharacterAmbitionTransitionFactData.ActivationReason
        };
        var fact = new CharacterAmbitionTransitionFact(data);
        CharacterNarrativeWorldSaveData rollback = persistence.Capture();
        if (rollback.ambitionTransitionObservations.Count != 0)
            throw new InvalidOperationException("Ambition transition observation is already pending.");
        if (!facts.TryReserve(fact, out var lease, out string prepareFailure))
            throw new InvalidOperationException("Ambition transition observation reserve failed: " + prepareFailure);

        bool mutationAttempted = false;
        bool mayBeCommitted = false;
        try
        {
            mutationAttempted = true;
            if (fail)
            {
                commands.FailAmbition(characterId, transitionDay);
            }
            else
            {
                commands.StartAmbition(
                    characterId,
                    new CharacterAmbitionId(ambitionId),
                    transitionDay);
            }
            query.TryGet(characterId, out var actual);
            data.RequireAfterState(actual);
            var applied = persistence.Capture();
            var pending = new CharacterAmbitionTransitionObservationSaveData
            { fact = data.Clone(), publicationKey = lease.PublicationKey };
            applied.ambitionTransitionObservations.Add(pending);
            // The applied state and exact replay intent are durable before crossing the observation boundary.
            persistence.PublishRestore(persistence.PrepareRestore(applied));
            var delivery = facts.Publish(lease, fact);
            mayBeCommitted = delivery.MayBeCommitted;
            AcceptAmbitionDelivery(pending, delivery);
        }
        catch (Exception transitionFailure)
        {
            if (!mutationAttempted || mayBeCommitted)
                throw;
            try
            {
                CharacterNarrativeAggregateState candidate =
                    persistence.PrepareRestore(rollback);
                persistence.PublishRestore(candidate);
            }
            catch (Exception rollbackFailure)
            {
                throw new AggregateException(
                    "Ambition transition and rollback both failed.",
                    transitionFailure,
                    rollbackFailure);
            }
            throw;
        }
        finally { if (lease.IsValid) facts.Cancel(lease); }
    }

    public void RecoverPendingAmbitionTransition()
    {
        var captured = persistence.Capture();
        if (captured.ambitionTransitionObservations.Count == 0) return;
        var pending = captured.ambitionTransitionObservations.Single();
        pending.Validate();
        query.TryGet(new CharacterId(pending.fact.characterId), out var actual);
        pending.fact.RequireAfterState(actual);
        var fact = new CharacterAmbitionTransitionFact(pending.fact);
        var deliveries = new DomainFactDelivery[1];
        if (!factRecovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, fact) },
                deliveries, out string failure))
            throw new InvalidOperationException("Ambition transition observation incomplete: " + failure);
        // A failed replay cannot prove that an earlier attempt never committed. Never undo saved effects here.
        AcceptAmbitionDelivery(pending, deliveries[0]);
    }

    private void AcceptAmbitionDelivery(CharacterAmbitionTransitionObservationSaveData pending,
        DomainFactDelivery delivery)
    {
        if (delivery.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
        {
            if (delivery.OperationKey != pending.publicationKey || string.IsNullOrEmpty(delivery.PublicationId)
                || string.IsNullOrEmpty(delivery.Fingerprint)
                || pending.publicationId.Length > 0 && (delivery.PublicationId != pending.publicationId
                    || delivery.Fingerprint != pending.publicationFingerprint))
                throw new InvalidOperationException("Ambition transition observation proof conflict.");
            var accepted = persistence.Capture();
            var current = accepted.ambitionTransitionObservations.Single();
            if (current.publicationKey != pending.publicationKey)
                throw new InvalidOperationException("Ambition transition observation owner changed during delivery.");
            current.publicationId = delivery.PublicationId;
            current.publicationFingerprint = delivery.Fingerprint;
            if (delivery.Completed) accepted.ambitionTransitionObservations.Clear();
            persistence.PublishRestore(persistence.PrepareRestore(accepted));
        }
        if (!delivery.Completed)
            throw new InvalidOperationException("Ambition transition observation incomplete: " + delivery.Phase + ":" + delivery.Detail);
    }

    private string RequireCharacterDisplayName(CharacterId characterId)
    {
        CharacterActor actor = characters.Characters.FirstOrDefault(value =>
            value != null
            && value.Identity != null
            && string.Equals(
                value.Identity.PersistentId,
                characterId.Value,
                StringComparison.Ordinal));
        string displayName = actor?.Identity?.DisplayName?.Trim()
            ?? string.Empty;
        if (displayName.Length == 0)
        {
            throw new InvalidOperationException(
                $"Ambition transition character '{characterId.Value}' "
                + "has no authoritative display name.");
        }
        return displayName;
    }

    private static bool HasRequirements(V20ContentRequirementSet value) =>
        value != null
        && ((value.items?.Count ?? 0) > 0
            || (value.facilities?.Count ?? 0) > 0
            || (value.research?.Count ?? 0) > 0
            || (value.characters?.Count ?? 0) > 0
            || (value.factions?.Count ?? 0) > 0
            || (value.worldMetrics?.Count ?? 0) > 0
            || (value.requiredFlags?.Count ?? 0) > 0
            || (value.excludedFlags?.Count ?? 0) > 0);

    private void ApplyBackgroundInitialization(
        CharacterId characterId,
        BackgroundInitializationOutcome outcome)
    {
        CharacterActor actor = characters.Characters.FirstOrDefault(value =>
            value != null
            && value.Identity != null
            && string.Equals(
                value.Identity.PersistentId,
                characterId.Value,
                StringComparison.Ordinal));
        if (actor == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(outcome.InitialMemoryCode))
        {
            actor.AiMemory?.RecordDecision(
                CharacterAiBranch.ContinueCurrent,
                CharacterAiIntentionType.None,
                outcome.InitialMemoryCode,
                0.1f);
        }
        foreach (V20ContentEffect effect in outcome.StartingEffects)
        {
            switch (effect.kind)
            {
                case V20ContentEffectKind.Mood:
                    actor.ApplyMoodFactor(
                        $"background:{outcome.BackgroundId.Value}:{effect.targetId}",
                        effect.targetId,
                        effect.amount,
                        Math.Max(1, effect.durationDays) * 180f);
                    break;
                case V20ContentEffectKind.SkillExperience:
                    throw new InvalidOperationException(
                        $"Background '{outcome.BackgroundId.Value}' must author starting proficiency XP through startingSkills, not a generic SkillExperience effect.");
                default:
                    throw new InvalidOperationException(
                        $"Background '{outcome.BackgroundId.Value}' has unsupported starting effect '{effect.kind}'.");
            }
        }
    }

    private void ApplyInitialCultureAttitudes(
        CharacterId newcomerId,
        CharacterNarrativeSnapshot newcomerNarrative)
    {
        if (newcomerNarrative == null || !newcomerNarrative.CultureId.IsValid)
        {
            return;
        }
        CharacterActor newcomer = characters.Characters.FirstOrDefault(value =>
            value != null
            && value.Identity != null
            && string.Equals(
                value.Identity.PersistentId,
                newcomerId.Value,
                StringComparison.Ordinal));
        if (newcomer == null)
        {
            return;
        }

        SpeciesCultureDefinitionSO newcomerCulture = catalog.Require(
            newcomerNarrative.CultureId);
        foreach (CharacterActor resident in characters.Characters
                     .Where(value => value != null
                         && value.Identity != null
                         && !string.Equals(
                             value.Identity.PersistentId,
                             newcomerId.Value,
                             StringComparison.Ordinal))
                     .OrderBy(
                         value => value.Identity.PersistentId,
                         StringComparer.Ordinal))
        {
            CharacterId residentId = CharacterPersistentIdentity.Require(resident);
            if (!query.TryGet(residentId, out CharacterNarrativeSnapshot residentNarrative)
                || !residentNarrative.BackgroundInitialized
                || !residentNarrative.CultureId.IsValid
                || residentNarrative.CultureId.Equals(newcomerNarrative.CultureId))
            {
                continue;
            }

            SpeciesCultureDefinitionSO residentCulture = catalog.Require(
                residentNarrative.CultureId);
            float newcomerSentiment = ResolveCultureAttitude(
                newcomerCulture,
                residentCulture.StableId);
            float residentSentiment = ResolveCultureAttitude(
                residentCulture,
                newcomerCulture.StableId);
            newcomer.SocialMemory.RememberCharacterExperience(
                resident,
                newcomerSentiment,
                "culture:initial-attitude");
            resident.SocialMemory.RememberCharacterExperience(
                newcomer,
                residentSentiment,
                "culture:initial-attitude");
        }
    }

    private static float ResolveCultureAttitude(
        SpeciesCultureDefinitionSO source,
        string targetCultureId)
    {
        V20WeightedId authored = (source?.otherCultureAttitudes
                ?? new List<V20WeightedId>())
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.id,
                    targetCultureId,
                    StringComparison.Ordinal));
        return Mathf.Clamp((authored?.weight ?? 1f) - 1f, -1f, 1f);
    }
}
