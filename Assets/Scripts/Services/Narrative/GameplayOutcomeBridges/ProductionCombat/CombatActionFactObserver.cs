using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>One observation of an attack owns its entire atomic narrative cohort.</summary>
public sealed class CombatActionFactObserver : IDurableDomainFactObserver<CombatActionFact>,
    IDurableDomainFactRecoveryObserver<CombatActionFact>, IDomainFactObserverBinding
{
    private static readonly MigratedProducerOutcomeKind[] TransitionKinds =
    {
        MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
        MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
        MigratedProducerOutcomeKind.CharacterDeathEvent,
        MigratedProducerOutcomeKind.CharacterKilledEvent
    };
    private readonly GameplayOutcomeLedger ledger;
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly MigratedProducerOutcomeRuntime migrated;
    private readonly GameplayDomainFactCommitGroup nativeCommits;
    private readonly CohortCommitGroup commits;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(CombatActionFact) });
    public CombatActionFactObserver(GameplayOutcomeLedger ledger, IGameplayOutcomeRecorder recorder,
        MigratedProducerOutcomeRuntime migrated, GameplayDomainFactCommitGroup nativeCommits)
    {
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.migrated = migrated ?? throw new ArgumentNullException(nameof(migrated));
        this.nativeCommits = nativeCommits ?? throw new ArgumentNullException(nameof(nativeCommits));
        commits = new CohortCommitGroup(nativeCommits);
    }
    public IDisposable Connect(DomainFactDispatcher dispatcher) => dispatcher.Subscribe("log:combat.action", this);
    public bool TryReserve(in CombatActionFact proposed, out object reservation, out string failure)
    {
        reservation = null;
        if (proposed == null || !proposed.IsProposal)
        { failure = "combat-observer-proposal-required"; return false; }
        var state = new Reservation(proposed);
        var data = proposed.Snapshot();
        try
        {
            if (!ReserveMigrated(MigratedProducerOutcomeKind.CombatAttackResult, proposed.Identity,
                    data.absoluteDay, 2, 14, 1, out state.Attack, out failure)) return Reject();
            if (data.target.kind == CombatFactParticipantKind.Character)
            {
                var requirement = CombatDamageOutcomeAdapter.CreateRequirements(DamageKey(data),
                    data.absoluteDay, ledger.CurrentWorldEpoch, data.attackRevision);
                var result = recorder.TryReserve(requirement, out state.Damage);
                if (!result.Success) { failure = "combat-observer-damage-reserve:" + result.Code; return Reject(); }
                for (int index = 0; index < TransitionKinds.Length; index++)
                    if (!ReserveMigrated(TransitionKinds[index], TransitionIdentity(proposed, index), data.absoluteDay,
                        index == 3 ? 2 : 1, 1, 0, out state.Transitions[index], out failure)) return Reject();
            }
            if (data.hasConflict && !data.conflict.alreadyPublished)
            {
                var receipt = ConflictReceipt(data.conflict);
                var shape = SocialConflictOutcomeAdapter.CreateRequirements(receipt.ResultKey,
                    receipt.AbsoluteDay, false, ledger.CurrentWorldEpoch);
                var result = recorder.TryReserve(shape, out state.Parent);
                if (!result.Success) { failure = "combat-observer-parent-reserve:" + result.Code; return Reject(); }
            }
            reservation = state; failure = string.Empty; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "combat-observer-reserve:" + exception.Message; return Reject(); }
        bool Reject() { Cancel(state); return false; }
    }
    private bool ReserveMigrated(MigratedProducerOutcomeKind kind, string identity, int day,
        int subjects, int facts, int provenance, out PreparedMigratedProducerOutcome prepared, out string failure)
    {
        if (!migrated.TryReserve(kind, identity, day, GameplayOutcomeStatus.Succeeded,
                subjects, 0, subjects, facts, out prepared, out failure, provenance)) return false;
        // Each command/exchange identity already includes its durable revision.
        // Reissuing it must never allocate a second attack and repeat damage.
        if (prepared.PreviousOwnerRevision == 0 && prepared.OwnerRevision == 1) return true;
        migrated.Cancel(prepared); prepared = default;
        failure = "combat-observer-operation-already-applied"; return false;
    }
    public bool TryPrepare(in CombatActionFact actual, object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state || actual == null || actual.IsProposal
            || !actual.MatchesProposal(state.Proposal) || state.Publications.Count != 0)
        { failure = "combat-observer-reservation-mismatch"; return false; }
        return Prepare(actual, state, false, out prepared, out failure);
    }
    public bool TryPrepareReplay(string publicationKey, in CombatActionFact fact,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (fact == null || fact.IsProposal || AttackKey(fact).ToString() != publicationKey)
        { failure = "combat-observer-replay-identity-invalid"; return false; }
        return Prepare(fact, new Reservation(null) { RecoveryKey = publicationKey }, true, out prepared, out failure);
    }
    public DomainFactDelivery Replay(in CombatActionFact fact) => DomainFactDelivery.Reject("combat-observer-saved-reference-required");
    private bool Prepare(CombatActionFact fact, Reservation state, bool recovery,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        var data = fact.Snapshot();
        try
        {
            GameplayOutcomeCausation parent = default;
            if (data.hasConflict)
            {
                var receipt = ConflictReceipt(data.conflict);
                if (!PrepareNative(receipt, receipt.ResultKey, 0, state.Parent,
                        recovery || data.conflict.alreadyPublished, out var item, out failure)) return Reject();
                if (data.conflict.alreadyPublished)
                {
                    if (!item.IsReplay) { item.Cancel(); failure = "combat-observer-parent-not-committed"; return Reject(); }
                }
                else state.Publications.Add(item);
                parent = new GameplayOutcomeCausation(OutcomeId(item), new GameplayOperationId(data.conflict.operationId), "social-conflict-effect");
            }
            GameplayOutcomeCausation derived = default;
            float actualDamage = data.healthBefore - data.healthAfter;
            bool character = data.target.kind == CombatFactParticipantKind.Character;
            if (character && actualDamage > 0)
            {
                var receipt = new CombatDamageOutcomeReceipt(data.operationId, data.attackRevision,
                    Kind(data.attacker), data.attacker.id, Name(data.attacker), new CharacterId(data.target.id), Name(data.target),
                    data.evidence.damageType, data.evidence.bodyPart, actualDamage, data.absoluteDay,
                    new Vector2Int(data.targetX, data.targetY), parent);
                if (!PrepareNative(receipt, receipt.ResultKey, data.attackRevision, state.Damage,
                        recovery, out var damage, out failure)) return Reject();
                state.Publications.Add(damage);
                derived = new GameplayOutcomeCausation(OutcomeId(damage), receipt.OperationId, "direct-effect");
            }
            else if (!recovery) recorder.CancelReservation(state.Damage);
            var attackCause = parent.HasParent ? parent : character && actualDamage > 0
                ? new GameplayOutcomeCausation(default, new GameplayOperationId(data.operationId), "atomic-sibling") : default;
            var capture = AttackCapture(fact, attackCause);
            if (!PrepareMigrated(capture, state.Attack, AttackKey(fact), recovery, out var attack, out failure)) return Reject();
            state.Publications.Add(attack);
            bool[] include = IncludedTransitions(data);
            for (int index = 0; index < TransitionKinds.Length; index++)
            {
                if (!include[index]) { if (!recovery) migrated.Cancel(state.Transitions[index]); continue; }
                capture = TransitionCapture(fact, index, actualDamage > 0 ? derived : default);
                if (!PrepareMigrated(capture, state.Transitions[index], TransitionKey(fact, index),
                    recovery, out var item, out failure)) return Reject();
                state.Publications.Add(item);
            }
            if (state.Publications.Any(value => value.IsReplay) && state.Publications.Any(value => !value.IsReplay))
            { failure = "combat-observer-partially-committed-cohort"; return Reject(); }
            prepared = new CohortPublication(commits, AttackKey(fact).ToString(), state.Publications.ToArray());
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "combat-observer-prepare:" + exception.Message; return Reject(); }
        bool Reject() { Cancel(state); return false; }
    }
    private bool PrepareNative<T>(T receipt, GameplayResultKey key, long revision,
        PreparedOutcomeReservation reserved, bool recovery, out GameplayDomainFactPublication item, out string failure)
    {
        item = null;
        PreparedOutcomeToken token;
        var result = recovery ? recorder.TryPrepare(receipt, out token) : recorder.TryWriteReserved(receipt, reserved, out token);
        if (result.Success) item = new GameplayDomainFactPublication(nativeCommits, recorder, new PreparedOwnerOutcome(token), revision);
        else if (recovery && result.Code is (OutcomePrepareCode.AlreadyCommitted or OutcomePrepareCode.AlreadyPublished or OutcomePrepareCode.AlreadyTerminal))
            item = new GameplayDomainFactPublication(nativeCommits, recorder, key, revision);
        else { failure = "combat-observer-native-prepare:" + result.Code + ":" + result.DetailCode; return false; }
        failure = string.Empty; return true;
    }
    private bool PrepareMigrated(MigratedDomainFactCapture capture, PreparedMigratedProducerOutcome reserved,
        GameplayResultKey key, bool recovery, out GameplayDomainFactPublication item, out string failure) => recovery
        ? migrated.TryPrepareObservedRecovery(capture, key.ToString(), nativeCommits, out item, out failure)
        : migrated.TryPrepareObserved(reserved, capture.ResolveDetails(key), nativeCommits, out item, out failure);
    private GameplayOutcomeId OutcomeId(GameplayDomainFactPublication item)
    {
        if (!item.IsReplay) return item.Prepared.Token.OutcomeId;
        if (ledger.TryGetResultIdentity(item.ReplayKey, out var identity) && identity.OutcomeId.IsValid) return identity.OutcomeId;
        throw new InvalidOperationException("combat-observer-canonical-parent-missing");
    }
    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state) return;
        foreach (var publication in state.Publications) publication.Cancel();
        migrated.Cancel(state.Attack);
        foreach (var transition in state.Transitions) migrated.Cancel(transition);
        recorder.CancelReservation(state.Damage); recorder.CancelReservation(state.Parent);
    }
    internal static GameplayResultKey AttackKey(CombatActionFact fact) => MigratedProducerOutcomeCatalog.ResultKey(
        MigratedProducerOutcomeKind.CombatAttackResult, fact.Identity, 1);
    private static GameplayResultKey DamageKey(CombatActionFactData value) => new(
        ProductionCombatOutcomeIds.CombatProducerId, new GameplayOperationId(value.operationId), value.attackRevision, 0);
    private static string TransitionIdentity(CombatActionFact fact, int index) => fact.Identity + ":" + ((int)TransitionKinds[index]).ToString(CultureInfo.InvariantCulture);
    private static GameplayResultKey TransitionKey(CombatActionFact fact, int index) =>
        MigratedProducerOutcomeCatalog.ResultKey(TransitionKinds[index], TransitionIdentity(fact, index), 1);
    internal static GameplayResultKey[] RelatedKeys(CombatActionFact fact)
    {
        var data = fact.Snapshot(); var keys = new List<GameplayResultKey> { AttackKey(fact) };
        if (data.hasConflict) keys.Add(ConflictReceipt(data.conflict).ResultKey);
        if (data.target.kind == CombatFactParticipantKind.Character && data.healthBefore > data.healthAfter) keys.Add(DamageKey(data));
        var included = IncludedTransitions(data);
        for (int index = 0; index < included.Length; index++) if (included[index]) keys.Add(TransitionKey(fact, index));
        return keys.ToArray();
    }
    private static bool[] IncludedTransitions(CombatActionFactData data)
    {
        if (data.target.kind != CombatFactParticipantKind.Character) return new bool[4];
        float before = data.healthBefore / Math.Max(1, data.maxHealthBefore), after = data.healthAfter / Math.Max(1, data.maxHealthAfter);
        bool damaged = data.healthBefore > data.healthAfter;
        bool died = damaged && data.healthAfter <= 0;
        return new[] { damaged && ((before > .2f && after <= .2f) || (before <= .2f && after > .2f)),
            !data.downedBefore && data.downedAfter, died, died };
    }
    private static SocialConflictOutcomeReceipt ConflictReceipt(CombatConflictFactData data) => new(data.operationId,
        new CharacterId(data.instigator.id), Name(data.instigator), new CharacterId(data.target.id), Name(data.target),
        data.conflictId, data.severity, data.origin, data.committedMoodDelta, data.absoluteDay);
    private static GameplayEntityKindId Kind(CombatFactParticipantData value) => new(value.kind == CombatFactParticipantKind.Character ? "character" : "wildlife");
    private static DungeonStory.Narrative.Korean.KoreanNameSnapshot Name(CombatFactParticipantData value) => ProductionCombatOutcomeNames.Snapshot(value.name, value.id);
    private static MigratedProducerOutcomeSubject Subject(CombatFactParticipantData value, GameplayRoleId role) => new(Kind(value), value.id, value.name, role);
    private static MigratedDomainFactCapture AttackCapture(CombatActionFact fact, GameplayOutcomeCausation cause)
    {
        var data = fact.Snapshot(); var e = data.evidence;
        float damage = data.healthBefore - data.healthAfter;
        string detail = $"executed={e.executed};hit={e.hit};coverBlocked={e.coverBlocked};"
            + (data.target.kind == CombatFactParticipantKind.Character && damage > 0 ? $"bodyPart={e.bodyPart};" : string.Empty)
            + "damage=" + damage.ToString("0.###", CultureInfo.InvariantCulture);
        var facts = new List<GameplayOutcomeFact> { new(MigratedProducerOutcomeIds.DetailFact, detail) };
        facts.AddRange(CombatActionEvidenceMapping.Facts(e));
        string summary = data.target.kind == CombatFactParticipantKind.Character
            ? $"{data.attacker.name}의 공격이 {data.target.name}에게 판정됐다." : $"{data.attacker.name}의 공격 판정이 끝났다.";
        var details = new MigratedProducerOutcomeDetails(new[] { Subject(data.attacker, MigratedProducerOutcomeIds.ActorRole),
                Subject(data.target, MigratedProducerOutcomeIds.TargetRole) }, Array.Empty<GameplayOutcomeMetric>(), facts.ToArray(), summary,
            causation: cause, summaryFirst: true, provenance: new[] { new GameplayOutcomeProvenanceReference("domain-commit", data.operationId) });
        return new MigratedDomainFactCapture(MigratedProducerOutcomeKind.CombatAttackResult, fact.Identity,
            data.absoluteDay, GameplayOutcomeStatus.Succeeded, details);
    }
    private static MigratedDomainFactCapture TransitionCapture(CombatActionFact fact, int index, GameplayOutcomeCausation cause)
    {
        var data = fact.Snapshot(); float damage = data.healthBefore - data.healthAfter;
        string amount = damage.ToString("0.###", CultureInfo.InvariantCulture);
        string summary = index switch { 0 => $"{data.target.name}의 건강이 임계치를 넘었다.",
            1 => $"{data.target.name}이(가) 전투 불능이 됐다.", 2 => $"{data.target.name}이(가) 전투 피해로 사망했다.",
            _ => $"{data.attacker.name}의 공격으로 {data.target.name}이(가) 쓰러졌다." };
        string detail = index switch {
            0 => "beforeRatio=" + (data.healthBefore / Math.Max(1, data.maxHealthBefore)).ToString("0.###", CultureInfo.InvariantCulture)
                + ";afterRatio=" + (data.healthAfter / Math.Max(1, data.maxHealthAfter)).ToString("0.###", CultureInfo.InvariantCulture),
            1 => $"beforeDowned={data.downedBefore};afterDowned={data.downedAfter}",
            2 => $"source={data.damageSource};damage={amount}", _ => $"origin=combat;source={data.damageSource};damage={amount}" };
        var target = Subject(data.target, MigratedProducerOutcomeIds.TargetRole);
        var details = new MigratedProducerOutcomeDetails(index == 3
                ? new[] { Subject(data.attacker, MigratedProducerOutcomeIds.ActorRole), target } : new[] { target },
            Array.Empty<GameplayOutcomeMetric>(), new[] { new GameplayOutcomeFact(MigratedProducerOutcomeIds.DetailFact, detail) },
            summary, causation: cause, summaryFirst: true);
        return new MigratedDomainFactCapture(TransitionKinds[index], TransitionIdentity(fact, index), data.absoluteDay,
            GameplayOutcomeStatus.Succeeded, details);
    }
    private static bool Recoverable(Exception value) => value is not OutOfMemoryException && value is not StackOverflowException && value is not AccessViolationException;
    private sealed class Reservation : IDomainFactReservedIdentity
    {
        public Reservation(CombatActionFact proposal) => Proposal = proposal;
        public CombatActionFact Proposal;
        public string RecoveryKey;
        public PreparedMigratedProducerOutcome Attack;
        public readonly PreparedMigratedProducerOutcome[] Transitions = new PreparedMigratedProducerOutcome[4];
        public PreparedOutcomeReservation Damage, Parent;
        public readonly List<GameplayDomainFactPublication> Publications = new();
        public string PublicationKey => RecoveryKey ?? Attack.ResultKey.ToString();
    }
    private sealed class CohortPublication : IDomainFactPreparedPublication, IDomainFactPreparedIdentity
    {
        public CohortPublication(CohortCommitGroup group, string key, GameplayDomainFactPublication[] publications)
        { CommitGroup = group; Key = key; Publications = publications; }
        public IDomainFactCommitGroup CommitGroup { get; }
        public string Key { get; }
        public string OperationKey => Key;
        public long OwnerRevision => 1L;
        public bool IsReplay => Publications.All(value => value.IsReplay);
        public GameplayDomainFactPublication[] Publications { get; }
        public void Cancel() { foreach (var item in Publications) item.Cancel(); }
    }
    private sealed class CohortCommitGroup : IDomainFactCommitGroup
    {
        private readonly GameplayDomainFactCommitGroup inner;
        public CohortCommitGroup(GameplayDomainFactCommitGroup inner) => this.inner = inner;
        public bool Commit(IReadOnlyList<IDomainFactPreparedPublication> publications, DomainFactDelivery[] results, out string failure)
        {
            if (publications == null || results == null || publications.Count == 0 || results.Length != publications.Count
                || publications.Any(value => value is not CohortPublication || !ReferenceEquals(value.CommitGroup, this)))
                throw new ArgumentException("combat-observer-commit-group-mismatch");
            var cohorts = publications.Cast<CohortPublication>().ToArray();
            var all = cohorts.SelectMany(value => value.Publications).ToArray(); var replies = new DomainFactDelivery[all.Length];
            bool accepted = inner.Commit(all, replies, out failure); int offset = 0;
            for (int index = 0; index < cohorts.Length; index++)
            {
                var cohort = cohorts[index]; var own = replies.Skip(offset).Take(cohort.Publications.Length).ToArray(); offset += own.Length;
                if (!accepted) { results[index] = DomainFactDelivery.Reject(failure); continue; }
                bool complete = own.Select((reply, n) => reply.Completed && reply.OperationKey == cohort.Publications[n].OperationKey
                    && !string.IsNullOrWhiteSpace(reply.PublicationId) && reply.Fingerprint?.Length == 64).All(value => value);
                results[index] = complete ? new DomainFactDelivery(DomainFactDeliveryPhase.Delivered, cohort.Key,
                    "cohort:" + Hash(string.Join("\n", own.Select(value => value.PublicationId))),
                    Hash(string.Join("\n", own.Select(value => value.OperationKey + ":" + value.Fingerprint))), string.Empty)
                    : new DomainFactDelivery(DomainFactDeliveryPhase.InDoubt, cohort.Key, null, null,
                        "combat-observer-cohort-delivery-pending:" + string.Join(";", own.Select(value => value.Detail)));
            }
            return accepted;
        }
        private static string Hash(string text)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
    }
}
