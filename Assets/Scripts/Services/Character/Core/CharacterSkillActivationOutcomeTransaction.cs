using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public readonly struct CharacterSkillActivationRequest
{
    public CharacterSkillActivationRequest(
        CharacterActor source,
        CharacterSkillInstance skill,
        IReadOnlyList<CharacterActor> recipients,
        CharacterSkillActivationKind activationKind,
        long absoluteHour,
        IReadOnlyList<string> childPublicationKeys = null)
    {
        Source = source;
        Skill = skill;
        Recipients = recipients ?? Array.Empty<CharacterActor>();
        ActivationKind = activationKind;
        AbsoluteHour = absoluteHour;
        ChildPublicationKeys = childPublicationKeys ?? Array.Empty<string>();
    }

    public CharacterActor Source { get; }
    public CharacterSkillInstance Skill { get; }
    public IReadOnlyList<CharacterActor> Recipients { get; }
    public CharacterSkillActivationKind ActivationKind { get; }
    public long AbsoluteHour { get; }
    public IReadOnlyList<string> ChildPublicationKeys { get; }
}

public enum CharacterSkillAutomaticExecutionMode
{
    Standard = 0,
    DefenseUltimate = 1,
    AlreadyApplied = 2
}

public readonly struct CharacterSkillAutomaticActivationRequest
{
    public CharacterSkillAutomaticActivationRequest(
        CharacterSkillExecutionContext context,
        CharacterSkillInstance skill,
        CharacterSkillActivationKind activationKind,
        string triggerEventId,
        CharacterUltimateDomain ultimateDomain = CharacterUltimateDomain.None,
        int triggerSerial = -1,
        CharacterSkillAutomaticExecutionMode executionMode =
            CharacterSkillAutomaticExecutionMode.Standard,
        IReadOnlyList<string> childPublicationKeys = null)
    {
        Context = context;
        Skill = skill;
        ActivationKind = activationKind;
        TriggerEventId = triggerEventId?.Trim() ?? string.Empty;
        UltimateDomain = ultimateDomain;
        TriggerSerial = triggerSerial;
        ExecutionMode = executionMode;
        ChildPublicationKeys = childPublicationKeys ?? Array.Empty<string>();
    }

    public CharacterSkillExecutionContext Context { get; }
    public CharacterSkillInstance Skill { get; }
    public CharacterSkillActivationKind ActivationKind { get; }
    public string TriggerEventId { get; }
    public CharacterUltimateDomain UltimateDomain { get; }
    public int TriggerSerial { get; }
    public CharacterSkillAutomaticExecutionMode ExecutionMode { get; }
    public IReadOnlyList<string> ChildPublicationKeys { get; }
}

public sealed class CharacterSkillAppliedActivationScope
{
    internal CharacterSkillAppliedActivationScope(object state) => State = state;
    internal object State { get; }
    public bool IsValid => State != null;
}

public readonly struct CharacterSkillAppliedActivationRequest
{
    public CharacterSkillAppliedActivationRequest(CharacterSkillExecutionContext context,
        CharacterSkillInstance skill, string triggerEventId, int triggerSerial, string childPublicationKey)
    { Context = context; Skill = skill; TriggerEventId = triggerEventId; TriggerSerial = triggerSerial;
        ChildPublicationKey = childPublicationKey; }
    public CharacterSkillExecutionContext Context { get; }
    public CharacterSkillInstance Skill { get; }
    public string TriggerEventId { get; }
    public int TriggerSerial { get; }
    public string ChildPublicationKey { get; }
    public CharacterSkillActivationKind ActivationKind => CharacterSkillActivationKind.ManualCombatUltimate;
    public CharacterUltimateDomain UltimateDomain => CharacterUltimateDomain.Offense;
}

public readonly struct PreparedCharacterSkillAppliedActivation
{
    internal PreparedCharacterSkillAppliedActivation(
        CharacterSkillAppliedActivationScope scope,
        CharacterSkillActivationFact fact, DomainFactLease lease)
    {
        Scope = scope;
        Fact = fact;
        Lease = lease;
    }

    internal CharacterSkillAppliedActivationScope Scope { get; }
    public CharacterSkillActivationFact Fact { get; }
    public DomainFactLease Lease { get; }
    public string PublicationKey => Lease.PublicationKey;
    public long OwnerRevision => Fact?.OwnerRevision ?? 0L;
    public bool IsValid => Scope?.IsValid == true && Fact != null && Lease.IsValid;
}

public interface ICharacterSkillActivationEffectExecutor
{
    void Execute(
        in CharacterSkillExecutionContext context,
        CharacterSkillInstance skill);
}

public sealed class CharacterSkillActivationEffectExecutor :
    ICharacterSkillActivationEffectExecutor
{
    public void Execute(
        in CharacterSkillExecutionContext context,
        CharacterSkillInstance skill) =>
        CharacterSkillRuntimeEffects.ExecuteSkill(context, skill);
}

public interface ICharacterSkillActivationTransaction
{
    bool TryCommit(
        in CharacterSkillActivationRequest request,
        out string publicationKey,
        out string failureReason);
    bool TryCommitAutomatic(
        in CharacterSkillAutomaticActivationRequest request,
        out string publicationKey,
        out string failureReason);
}

public interface ICharacterSkillAppliedActivationCommand
{
    bool TryBeginAppliedAutomatic(
        in CharacterSkillAppliedActivationRequest request,
        out CharacterSkillAppliedActivationScope scope,
        out string failureReason);
    bool TryPrepareAppliedAutomatic(
        CharacterSkillAppliedActivationScope scope,
        out PreparedCharacterSkillAppliedActivation prepared,
        out string failureReason);
    void RollbackAppliedAutomatic(CharacterSkillAppliedActivationScope scope);
    bool TryFinalizeAppliedAutomatic(
        CharacterActor source, CharacterSkillActivationFact fact, in DomainFactDelivery delivered,
        out string failureReason);
}

public sealed class CharacterSkillActivationTransaction :
    ICharacterSkillActivationTransaction, ICharacterSkillAppliedActivationCommand, VContainer.Unity.ITickable
{
    private readonly ICharacterSkillActivationEffectExecutor effects;
    private readonly IGameCalendar calendar;
    private readonly IDomainFactPublisher facts;
    private readonly CharacterSkillActivationPublicationRuntime publications;
    private ICharacterLifetimeQuery characters;
    private readonly System.Diagnostics.Stopwatch recoveryClock = System.Diagnostics.Stopwatch.StartNew();

    public CharacterSkillActivationTransaction(
        ICharacterSkillActivationEffectExecutor effects,
        IGameCalendar calendar, IDomainFactPublisher facts,
        CharacterSkillActivationPublicationRuntime publications)
    {
        this.effects = effects
            ?? throw new ArgumentNullException(nameof(effects));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
    }

    [VContainer.Inject]
    public void ConstructRecovery(ICharacterLifetimeQuery characters) =>
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));

    public void Tick()
    {
        if (characters == null || publications.IsBusy || publications.IsRestoring
            || recoveryClock.ElapsedMilliseconds < 1000) return;
        recoveryClock.Restart();
        if (!publications.RecoverPending(out _)) return;
        foreach (var row in publications.PendingOwners())
        {
            var source = characters.AllCharacters.SingleOrDefault(actor => actor != null
                && CharacterPersistentIdentity.TryGet(actor, out var id) && id.Value == row.fact.sourceCharacterId);
            if (source != null) TryRecoverStandalone(source, out _);
        }
    }

    public bool TryRecoverStandalone(CharacterActor source, out string failure)
    {
        failure = string.Empty;
        try
        {
            string sourceId = CharacterPersistentIdentity.Require(source).Value;
            var row = publications.PendingForSource(sourceId);
            if (row == null) return true;
            if (publications.IsBusy || publications.IsRestoring)
            { failure = "skill-publication-recovery-busy"; return false; }
            if (row.pending && !publications.RecoverPending(out failure)) return false;
            row = publications.PendingForSource(sourceId);
            if (row == null) return true;
            if (!TryFinalizeAppliedAutomatic(source, new CharacterSkillActivationFact(row.fact),
                CharacterSkillActivationPublicationRuntime.Delivery(row), out failure)) return false;
            publications.CompleteOwner(sourceId, row.fact.ownerRevision);
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "skill-publication-owner-recovery:" + exception.GetType().Name + ":" + exception.Message; return false; }
    }

    public bool TryCommit(
        in CharacterSkillActivationRequest request,
        out string publicationKey,
        out string failureReason)
    {
        publicationKey = string.Empty;
        if (!TryValidate(request, out CharacterActor[] recipients, out failureReason)) return false;
        CharacterActor source = request.Source;
        CharacterSkillInstance skill = request.Skill;
        if (!TryRecoverStandalone(source, out failureReason)) return false;
        string sourceId = CharacterPersistentIdentity.Require(source).Value;
        var limits = RequireLimits(source);
        if (limits.lastSkillActivation?.observationPending == true)
        { failureReason = "skill-activation-joint-publication-pending"; return false; }
        var cooldown = FindCooldown(limits, skill.id);
        int useBefore = cooldown?.useCount ?? 0;
        long readyBefore = cooldown?.readyAbsoluteHour ?? 0L;
        if (readyBefore > request.AbsoluteHour)
        { failureReason = "skill-activation-cooldown-active"; return false; }
        if (limits.activationSequence == long.MaxValue)
        { failureReason = "skill-activation-sequence-exhausted"; return false; }
        long revision = limits.activationSequence + 1;
        if (publications.Find(sourceId, revision) != null)
        { failureReason = "skill-activation-owner-publication-history-mismatch"; return false; }
        long expires, readyAfter;
        try
        {
            expires = checked(request.AbsoluteHour + skill.manualDurationHours);
            readyAfter = checked(request.AbsoluteHour + (long)skill.manualCooldownDays * GameCalendarRules.HoursPerDay);
        }
        catch (OverflowException)
        { failureReason = "skill-activation-time-overflow"; return false; }
        Dictionary<CharacterActor, CharacterSkillUseLimitState> limitBefore;
        Dictionary<CharacterActor, CharacterMoodDeliveryTransactionSnapshot> statsBefore;
        string[] beforeStates;
        string mechanics;
        try
        {
            limitBefore = recipients.Append(source).Distinct().ToDictionary(actor => actor, actor => RequireLimits(actor).Clone());
            statsBefore = recipients.ToDictionary(actor => actor, actor => RequireStats(actor).CaptureMoodDeliveryTransactionState());
            beforeStates = recipients.Select(CanonicalState).ToArray();
            mechanics = ComputeMechanicsSha256(skill);
        }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "skill-activation-snapshot:" + exception.GetType().Name + ":" + exception.Message; return false; }
        if (!publications.TryBegin(sourceId, out failureReason)) return false;
        bool retained = false;
        DomainFactLease lease = default;
        try
        {
            ApplyManualActivation(source, skill, recipients, sourceId, request.AbsoluteHour, expires);
            limits = RequireLimits(source);
            limits.manualSkillCooldowns ??= new List<CharacterManualSkillCooldownState>();
            cooldown = FindCooldown(limits, skill.id) ?? new CharacterManualSkillCooldownState { skillId = skill.id };
            if (!limits.manualSkillCooldowns.Contains(cooldown)) limits.manualSkillCooldowns.Add(cooldown);
            cooldown.useCount = checked(useBefore + 1); cooldown.readyAbsoluteHour = readyAfter;
            var recipientFacts = recipients.Select((actor, index) => new CharacterSkillActivationRecipientFactData
            { characterId = CharacterPersistentIdentity.Require(actor).Value, displayName = actor.Identity?.DisplayName ?? actor.name,
                beforeState = beforeStates[index], afterState = CanonicalState(actor) }).ToList();
            var fact = CreateStandaloneFact(source, skill, revision, request.ActivationKind,
                request.AbsoluteHour, expires, useBefore, readyBefore, readyAfter, mechanics,
                recipientFacts, request.ChildPublicationKeys);
            if (!facts.TryReserve(fact, out lease, out failureReason)) return false;
            string reservedKey = lease.PublicationKey;
            limits.activationSequence = revision;
            limits.lastSkillActivation = CapturePendingMirror(fact, reservedKey);
            var delivered = publications.PublishApplied(fact, lease);
            retained = delivered.MayBeCommitted;
            if (!retained) { failureReason = delivered.Detail; return false; }
            publicationKey = reservedKey;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            retained = publications.Find(sourceId, revision) != null;
            failureReason = "skill-activation-command:" + exception.GetType().Name + ":" + exception.Message;
            if (!retained) return false;
            publicationKey = publications.Find(sourceId, revision).publicationKey;
        }
        finally
        {
            retained |= publications.Find(sourceId, revision) != null;
            try { CancelReservation(lease); }
            finally
            {
                try { if (!retained) Rollback(limitBefore, statsBefore); }
                finally { publications.End(sourceId); }
            }
        }
        // Accepted effects remain accepted during observation recovery. Never retry their mutation.
        if (!TryRecoverStandalone(source, out failureReason))
            failureReason = "skill-activation-observation-pending:" + failureReason;
        return true;
    }

    public bool TryCommitAutomatic(
        in CharacterSkillAutomaticActivationRequest request,
        out string publicationKey,
        out string failureReason)
    {
        publicationKey = string.Empty;
        if (!TryValidateAutomatic(request, out failureReason)) return false;
        var source = request.Context.Actor;
        var skill = request.Skill;
        if (!TryRecoverStandalone(source, out failureReason)) return false;
        string sourceId = CharacterPersistentIdentity.Require(source).Value;
        var limits = RequireLimits(source);
        if (limits.lastSkillActivation?.observationPending == true)
        { failureReason = "skill-activation-joint-publication-pending"; return false; }
        string eventKey = ComputeAutomaticEventKeySha256(sourceId, request);
        var existing = limits.automaticSkillActivations.SingleOrDefault(value => value.eventKeySha256 == eventKey);
        if (existing != null)
        {
            var observed = publications.Find(sourceId, existing.ownerRevision);
            if (observed == null || observed.pending || !observed.ownerFinalized
                || observed.fact.triggerEventKeySha256 != eventKey)
            { failureReason = "skill-automatic-publication-history-mismatch"; return false; }
            publicationKey = observed.publicationKey; return true;
        }
        if (limits.activationSequence == long.MaxValue)
        { failureReason = "skill-activation-sequence-exhausted"; return false; }
        if (request.ActivationKind == CharacterSkillActivationKind.AutomaticUltimate
            && !source.Progression.CanUseUltimate(request.UltimateDomain, request.TriggerSerial))
        { failureReason = "skill-automatic-ultimate-limit-active"; return false; }
        long revision = limits.activationSequence + 1;
        if (publications.Find(sourceId, revision) != null)
        { failureReason = "skill-activation-owner-publication-history-mismatch"; return false; }
        CharacterSkillActivationKind kind = request.ActivationKind;
        int useBefore = limits.automaticSkillActivations.Count(value => value.activationKind == kind && value.skillId == skill.id);
        long hour = Math.Max(0L, calendar.AbsoluteHour);
        AutomaticTargetOwner[] targets;
        string mechanics;
        try
        { targets = BuildAutomaticTargets(request.Context, skill).ToArray(); mechanics = ComputeMechanicsSha256(skill); }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "skill-automatic-snapshot:" + exception.GetType().Name + ":" + exception.Message; return false; }
        if (!publications.TryBegin(sourceId, out failureReason)) return false;
        bool retained = false;
        DomainFactLease lease = default;
        try
        {
            if (request.ExecutionMode == CharacterSkillAutomaticExecutionMode.Standard)
                effects.Execute(request.Context, skill);
            else if (request.ExecutionMode == CharacterSkillAutomaticExecutionMode.DefenseUltimate)
                CharacterSkillRuntimeEffects.ApplyDefenseUltimate(source, skill, request.Context.TargetActor);
            else throw new InvalidOperationException("skill-automatic-execution-mode-invalid");
            var changed = targets.Where(target => target.BeforeState != target.CaptureCanonical()).ToArray();
            bool modifierOnly = kind == CharacterSkillActivationKind.AutomaticUltimate
                && request.UltimateDomain == CharacterUltimateDomain.Management;
            if (changed.Length == 0 && !modifierOnly)
            { failureReason = "skill-automatic-activation-no-effect"; return false; }
            limits = RequireLimits(source);
            limits.automaticSkillActivations.Add(new CharacterAutomaticSkillActivationState
            { eventKeySha256 = eventKey, eventId = request.TriggerEventId, skillId = skill.id, activationKind = kind,
                ownerRevision = revision, useCount = checked(useBefore + 1) });
            if (kind == CharacterSkillActivationKind.AutomaticUltimate)
                limits.MarkUsed(request.UltimateDomain, request.TriggerSerial);
            if (changed.Length == 0) changed = new[] { targets.Single(target => target.CharacterId == sourceId) };
            var recipientFacts = changed.OrderBy(target => target.CharacterId, StringComparer.Ordinal)
                .Select(target => new CharacterSkillActivationRecipientFactData
                { characterId = target.CharacterId, displayName = target.DisplayName,
                    beforeState = target.BeforeState, afterState = target.CaptureCanonical() }).ToList();
            var fact = CreateStandaloneFact(source, skill, revision, kind, hour, hour, useBefore, 0, 0, mechanics,
                recipientFacts, request.ChildPublicationKeys, request.TriggerEventId, eventKey, request.UltimateDomain, request.TriggerSerial);
            if (!facts.TryReserve(fact, out lease, out failureReason)) return false;
            string reservedKey = lease.PublicationKey;
            limits.activationSequence = revision;
            limits.lastSkillActivation = CapturePendingMirror(fact, reservedKey);
            var delivered = publications.PublishApplied(fact, lease);
            retained = delivered.MayBeCommitted;
            if (!retained) { failureReason = delivered.Detail; return false; }
            publicationKey = reservedKey;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            retained = publications.Find(sourceId, revision) != null;
            failureReason = "skill-automatic-command:" + exception.GetType().Name + ":" + exception.Message;
            if (!retained) return false;
            publicationKey = publications.Find(sourceId, revision).publicationKey;
        }
        finally
        {
            retained |= publications.Find(sourceId, revision) != null;
            try { CancelReservation(lease); }
            finally
            {
                try { if (!retained) RollbackAutomatic(targets); }
                finally { publications.End(sourceId); }
            }
        }
        if (!TryRecoverStandalone(source, out failureReason))
            failureReason = "skill-activation-observation-pending:" + failureReason;
        return true;
    }

    private CharacterSkillActivationFact CreateStandaloneFact(CharacterActor source, CharacterSkillInstance skill,
        long revision, CharacterSkillActivationKind kind, long hour, long expires, int useBefore, long readyBefore, long readyAfter,
        string mechanics, List<CharacterSkillActivationRecipientFactData> recipients, IReadOnlyList<string> children,
        string eventId = "", string eventKey = "", CharacterUltimateDomain domain = CharacterUltimateDomain.None, int serial = -1) => new(new()
    {
        sourceCharacterId = CharacterPersistentIdentity.Require(source).Value, sourceDisplayName = source.Identity?.DisplayName ?? source.name,
        ownerRevision = revision, activationKind = kind, skillId = skill.id, skillDisplayName = skill.displayName,
        trigger = skill.trigger, target = skill.target, targetingMode = skill.targetingMode, effectArea = skill.effectArea,
        areaSize = skill.areaSize, durationHours = skill.manualDurationHours, cooldownDays = skill.manualCooldownDays,
        absoluteDay = Math.Max(0, calendar.Day), absoluteHour = hour, expiresAbsoluteHour = expires,
        useCountBefore = useBefore, useCountAfter = checked(useBefore + 1), readyAbsoluteHourBefore = readyBefore, readyAbsoluteHourAfter = readyAfter,
        mechanicsSha256 = mechanics, recipients = recipients, childPublicationKeys = children.ToList(),
        triggerEventId = eventId, triggerEventKeySha256 = eventKey, ultimateDomain = domain, triggerSerial = serial
    });

    private static CharacterSkillActivationCommitState CapturePendingMirror(CharacterSkillActivationFact fact, string key) => new()
    {
        ownerRevision = fact.OwnerRevision, operationId = CharacterSkillActivationIdentity.OperationId(fact.SourceCharacterId, fact.OwnerRevision),
        resultKey = key, observationPending = true, activationKind = fact.ActivationKind, skillId = fact.SkillId,
        mechanicsSha256 = fact.MechanicsSha256, absoluteHour = fact.AbsoluteHour, expiresAbsoluteHour = fact.ExpiresAbsoluteHour,
        useCountBefore = fact.UseCountBefore, useCountAfter = fact.UseCountAfter,
        readyAbsoluteHourBefore = fact.ReadyAbsoluteHourBefore, readyAbsoluteHourAfter = fact.ReadyAbsoluteHourAfter,
        recipientCharacterIds = fact.Recipients.Select(value => value.CharacterId).OrderBy(value => value, StringComparer.Ordinal).ToList(),
        recipientStateSha256 = fact.RecipientStateSha256, triggerEventId = fact.TriggerEventId, triggerEventKeySha256 = fact.TriggerEventKeySha256,
        ultimateDomain = fact.UltimateDomain, triggerSerial = fact.TriggerSerial
    };

    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;

    private void CancelReservation(DomainFactLease lease)
    {
        try { facts.Cancel(lease); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogException(exception); } // Cleanup cannot turn an already applied command into a retryable rejection.
    }

    public bool TryBeginAppliedAutomatic(
        in CharacterSkillAppliedActivationRequest request,
        out CharacterSkillAppliedActivationScope scope,
        out string failureReason)
    {
        scope = null;
        failureReason = string.Empty;
        CharacterActor candidate = request.Context?.Actor;
        if (candidate == null || candidate.IsDead || request.Skill?.IsReady != true
            || request.Skill.kind != CharacterSkillKind.Ultimate
            || request.Skill.ultimateDomain != CharacterUltimateDomain.Offense
            || request.Skill.trigger != CharacterSkillTrigger.ManualCombat
            || request.Context.Trigger != CharacterSkillTrigger.ManualCombat
            || candidate.Progression?.Ultimate?.id != request.Skill.id
            || request.Context.BattleSession == null || request.Context.SourceCombatant == null
            || request.Context.TargetCombatant == null || request.TriggerSerial < 0
            || !DomainFactValueSyntax.IsText(request.TriggerEventId, CharacterSkillActivationFact.MaximumStateCharacters, false)
            || !DomainFactValueSyntax.IsText(request.ChildPublicationKey, 1024, false))
        {
            failureReason = "skill-manual-combat-ultimate-contract-invalid";
            return false;
        }

        CharacterActor source = request.Context.Actor;
        string sourceId;
        try
        {
            sourceId = CharacterPersistentIdentity.Require(source).Value;
        }
        catch (Exception exception)
        {
            failureReason = "skill-activation-source-identity-invalid:"
                + exception.Message;
            return false;
        }

        CharacterSkillUseLimitState limits = RequireLimits(source);
        if (limits.lastSkillActivation?.observationPending == true)
        { failureReason = "skill-activation-joint-publication-pending"; return false; }
        if (request.Context.SourceCombatant.PersistentId != sourceId
            || !ReferenceEquals(request.Context.BattleSession.FindCombatant(sourceId), request.Context.SourceCombatant)
            || !ReferenceEquals(request.Context.BattleSession.FindCombatant(request.Context.TargetCombatant.PersistentId), request.Context.TargetCombatant))
        { failureReason = "skill-applied-battle-owner-mismatch"; return false; }
        limits.automaticSkillActivations ??=
            new List<CharacterAutomaticSkillActivationState>();
        string eventKey = ComputeAutomaticEventKeySha256(sourceId, request.ActivationKind,
            request.Skill.id, request.Context.Trigger, request.TriggerEventId, request.UltimateDomain, request.TriggerSerial);
        if (limits.automaticSkillActivations.Any(value => value != null
                && string.Equals(
                    value.eventKeySha256,
                    eventKey,
                    StringComparison.Ordinal)))
        {
            failureReason = "skill-applied-activation-already-committed";
            return false;
        }
        if (limits.activationSequence == long.MaxValue)
        {
            failureReason = "skill-activation-sequence-exhausted";
            return false;
        }
        if (!source.Progression.CanUseUltimate(
                request.UltimateDomain,
                request.TriggerSerial))
        {
            failureReason = "skill-automatic-ultimate-limit-active";
            return false;
        }

        AutomaticTargetOwner[] targets;
        try
        {
            targets = BuildAutomaticTargets(
                    request.Context,
                    request.Skill)
                .ToArray();
        }
        catch (Exception exception)
        {
            failureReason = "skill-applied-target-snapshot-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }

        CharacterSkillActivationKind activationKind = request.ActivationKind;
        string skillId = request.Skill.id;
        int useCountBefore = limits.automaticSkillActivations.Count(value =>
            value != null
            && value.activationKind == activationKind
            && string.Equals(
                value.skillId,
                skillId,
                StringComparison.Ordinal));
        scope = new CharacterSkillAppliedActivationScope(
            new AppliedActivationState(
                request,
                sourceId,
                eventKey,
                limits.activationSequence + 1L,
                useCountBefore,
                Math.Max(0L, calendar.AbsoluteHour),
                targets));
        return true;
    }

    public bool TryPrepareAppliedAutomatic(
        CharacterSkillAppliedActivationScope scope,
        out PreparedCharacterSkillAppliedActivation prepared,
        out string failureReason)
    {
        prepared = default;
        failureReason = string.Empty;
        if (scope?.State is not AppliedActivationState state || state.PreparedFact != null)
        {
            failureReason = "skill-applied-activation-scope-invalid";
            return false;
        }

        CharacterActor source = state.Request.Context.Actor;
        CharacterSkillUseLimitState limits = RequireLimits(source);
        if (limits.activationSequence != state.Revision - 1L
            || limits.automaticSkillActivations.Any(value => value != null
                && string.Equals(
                    value.eventKeySha256,
                    state.EventKey,
                    StringComparison.Ordinal)))
        {
            RollbackAutomatic(state.Targets);
            failureReason = "skill-applied-activation-owner-changed";
            return false;
        }

        AutomaticTargetOwner[] recipients = state.Targets
            .Where(value => !string.Equals(
                value.BeforeState,
                value.CaptureCanonical(),
                StringComparison.Ordinal))
            .ToArray();
        if (recipients.Length == 0)
        {
            RollbackAutomatic(state.Targets);
            failureReason = "skill-automatic-activation-no-effect";
            return false;
        }

        CharacterSkillInstance skill = state.Request.Skill;
        CharacterSkillActivationRecipientFactData[] recipientFacts =
            recipients
                .OrderBy(value => value.CharacterId, StringComparer.Ordinal)
                .Select(value =>
                    new CharacterSkillActivationRecipientFactData
                    { characterId = value.CharacterId, displayName = value.DisplayName,
                        beforeState = value.BeforeState, afterState = value.CaptureCanonical() })
                .ToArray();
        string mechanicsSha256 = ComputeMechanicsSha256(skill);
        var fact = new CharacterSkillActivationFact(new CharacterSkillActivationFactData
        {
            sourceCharacterId = state.SourceId, sourceDisplayName = source.Identity?.DisplayName ?? source.name,
            ownerRevision = state.Revision, activationKind = state.Request.ActivationKind,
            skillId = skill.id, skillDisplayName = skill.displayName, trigger = skill.trigger, target = skill.target,
            targetingMode = skill.targetingMode, effectArea = skill.effectArea, areaSize = skill.areaSize,
            durationHours = skill.manualDurationHours, cooldownDays = skill.manualCooldownDays,
            absoluteDay = Math.Max(0, calendar.Day), absoluteHour = state.AbsoluteHour, expiresAbsoluteHour = state.AbsoluteHour,
            useCountBefore = state.UseCountBefore, useCountAfter = checked(state.UseCountBefore + 1),
            mechanicsSha256 = mechanicsSha256, recipients = recipientFacts.ToList(),
            childPublicationKeys = new List<string> { state.Request.ChildPublicationKey },
            triggerEventId = state.Request.TriggerEventId, triggerEventKeySha256 = state.EventKey,
            ultimateDomain = state.Request.UltimateDomain, triggerSerial = state.Request.TriggerSerial
        });
        if (!facts.TryReserve(fact, out DomainFactLease lease, out failureReason))
        {
            RollbackAutomatic(state.Targets);
            return false;
        }

        try
        {
            limits = RequireLimits(source);
            limits.automaticSkillActivations.Add(
                new CharacterAutomaticSkillActivationState
                {
                    eventKeySha256 = state.EventKey,
                    eventId = state.Request.TriggerEventId,
                    skillId = skill.id,
                    activationKind = state.Request.ActivationKind,
                    ownerRevision = state.Revision,
                    useCount = state.UseCountBefore + 1
                });
            limits.MarkUsed(
                state.Request.UltimateDomain,
                state.Request.TriggerSerial);
            limits.activationSequence = state.Revision;
            limits.lastSkillActivation =
                new CharacterSkillActivationCommitState
                {
                    ownerRevision = state.Revision,
                    operationId = CharacterSkillActivationIdentity.OperationId(state.SourceId, state.Revision),
                    resultKey = lease.PublicationKey,
                    observationPending = true,
                    activationKind = state.Request.ActivationKind,
                    skillId = skill.id,
                    mechanicsSha256 = mechanicsSha256,
                    absoluteHour = state.AbsoluteHour,
                    expiresAbsoluteHour = state.AbsoluteHour,
                    useCountBefore = state.UseCountBefore,
                    useCountAfter = state.UseCountBefore + 1,
                    readyAbsoluteHourBefore = 0L,
                    readyAbsoluteHourAfter = 0L,
                    recipientCharacterIds = recipientFacts
                        .Select(value => value.characterId)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToList(),
                    recipientStateSha256 = fact.RecipientStateSha256,
                    triggerEventId = state.Request.TriggerEventId,
                    triggerEventKeySha256 = state.EventKey,
                    ultimateDomain = state.Request.UltimateDomain,
                    triggerSerial = state.Request.TriggerSerial
                };
            state.PreparedFact = fact;
            state.Lease = lease;
            prepared = new PreparedCharacterSkillAppliedActivation(scope, fact, lease);
            return true;
        }
        catch (Exception exception)
        {
            facts.Cancel(lease);
            RollbackAutomatic(state.Targets);
            failureReason = "skill-applied-owner-prepare-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    public void RollbackAppliedAutomatic(
        CharacterSkillAppliedActivationScope scope)
    {
        if (scope?.State is AppliedActivationState state)
        {
            facts.Cancel(state.Lease);
            RollbackAutomatic(state.Targets);
        }
    }

    public bool TryFinalizeAppliedAutomatic(
        CharacterActor source, CharacterSkillActivationFact fact, in DomainFactDelivery delivered,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (source?.Progression == null || fact == null
            || CharacterPersistentIdentity.Require(source).Value != fact.SourceCharacterId
            || !delivered.Completed || string.IsNullOrWhiteSpace(delivered.PublicationId)
            || !IsSha256(delivered.Fingerprint))
        {
            failureReason = "skill-applied-activation-delivery-invalid";
            return false;
        }
        CharacterSkillUseLimitState limits = RequireLimits(source);
        if (limits.activationSequence != fact.OwnerRevision
            || !fact.MatchesUseLimit(limits.lastSkillActivation, delivered.OperationKey)
            || !limits.lastSkillActivation.observationPending
                && limits.lastSkillActivation.canonicalPayloadHash != delivered.Fingerprint)
        {
            failureReason = "skill-applied-activation-owner-missing";
            return false;
        }
        bool wasPending = limits.lastSkillActivation.observationPending;
        limits.lastSkillActivation.canonicalPayloadHash = delivered.Fingerprint;
        limits.lastSkillActivation.observationPending = false;
        if (wasPending) source.Progression.PublishSkillActivationChanged();
        return true;
    }

    private void ApplyManualActivation(
        CharacterActor source,
        CharacterSkillInstance skill,
        IReadOnlyList<CharacterActor> recipients,
        string sourceId,
        long absoluteHour,
        long expiresAbsoluteHour)
    {
        foreach (CharacterActor recipient in recipients)
        {
            CharacterSkillUseLimitState targetLimits = RequireLimits(recipient);
            targetLimits.manualSkillBuffs ??=
                new List<CharacterManualSkillBuffState>();
            targetLimits.manualSkillBuffs.RemoveAll(value => value == null
                || value.expiresAbsoluteHour <= absoluteHour
                || (string.Equals(
                        value.sourceCharacterId,
                        sourceId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.skillId,
                        skill.id,
                        StringComparison.Ordinal)));
            targetLimits.manualSkillBuffs.Add(new CharacterManualSkillBuffState
            {
                sourceCharacterId = sourceId,
                skillId = skill.id,
                expiresAbsoluteHour = expiresAbsoluteHour,
                frozenSkill = skill.Clone()
            });
            effects.Execute(
                new CharacterSkillExecutionContext(
                    source,
                    CharacterSkillTrigger.ManualWork,
                    eventId: "manual-work:" + sourceId + ":" + skill.id
                        + ":" + absoluteHour,
                    targetActor: recipient),
                skill);
        }
    }

    private static bool TryValidateAutomatic(
        in CharacterSkillAutomaticActivationRequest request,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterSkillExecutionContext context = request.Context;
        CharacterActor source = context?.Actor;
        CharacterSkillInstance skill = request.Skill;
        if (source == null || source.IsDead)
            failureReason = "skill-activation-source-unavailable";
        else if (source.Progression?.GrowthState == null)
            failureReason = "skill-activation-source-progression-unavailable";
        else if (skill == null || !skill.IsReady)
            failureReason = "skill-activation-skill-unavailable";
        else if (!DomainFactValueSyntax.IsText(request.TriggerEventId,
                     CharacterSkillActivationFact.MaximumStateCharacters, allowEmpty: false))
            failureReason = "skill-automatic-trigger-event-invalid";
        else if (skill.trigger != context.Trigger)
            failureReason = "skill-automatic-trigger-mismatch";
        else if (request.ActivationKind
                 == CharacterSkillActivationKind.AutomaticPassive)
        {
            bool learned = source.Progression != null
                && source.Progression.PassiveSkills
                    .Concat(source.Progression.OwnerFixedSkills)
                    .Count(value => value != null
                        && string.Equals(
                            value.id,
                            skill.id,
                            StringComparison.Ordinal)) == 1;
            if ((skill.kind != CharacterSkillKind.Passive
                    && skill.kind != CharacterSkillKind.OwnerFixed)
                || request.UltimateDomain != CharacterUltimateDomain.None
                || request.TriggerSerial != -1
                || request.ExecutionMode
                    != CharacterSkillAutomaticExecutionMode.Standard
                || !learned)
            {
                failureReason = "skill-automatic-passive-contract-invalid";
            }
        }
        else if (request.ActivationKind
                 == CharacterSkillActivationKind.AutomaticUltimate)
        {
            bool learned = source.Progression?.Ultimate != null
                && string.Equals(
                    source.Progression.Ultimate.id,
                    skill.id,
                    StringComparison.Ordinal);
            bool modeMatches = request.UltimateDomain
                == CharacterUltimateDomain.Defense
                    ? request.ExecutionMode
                        == CharacterSkillAutomaticExecutionMode.DefenseUltimate
                    : request.ExecutionMode
                        == CharacterSkillAutomaticExecutionMode.Standard;
            if (skill.kind != CharacterSkillKind.Ultimate
                || request.UltimateDomain == CharacterUltimateDomain.None
                || request.UltimateDomain != skill.ultimateDomain
                || request.TriggerSerial < 0
                || !modeMatches
                || !learned)
            {
                failureReason = "skill-automatic-ultimate-contract-invalid";
            }
        }
        else
            failureReason = "skill-automatic-activation-kind-invalid";

        if (failureReason.Length == 0
            && (request.ChildPublicationKeys.Any(value => !DomainFactValueSyntax.IsText(value, 1024, false))
                || request.ChildPublicationKeys.Distinct(StringComparer.Ordinal).Count()
                    != request.ChildPublicationKeys.Count))
        {
            failureReason = "skill-automatic-child-result-invalid";
        }
        return failureReason.Length == 0;
    }

    private static string ComputeAutomaticEventKeySha256(
        string sourceCharacterId,
        in CharacterSkillAutomaticActivationRequest request) =>
        ComputeAutomaticEventKeySha256(sourceCharacterId, request.ActivationKind, request.Skill.id,
            request.Context.Trigger, request.TriggerEventId, request.UltimateDomain, request.TriggerSerial);

    private static string ComputeAutomaticEventKeySha256(string sourceCharacterId,
        CharacterSkillActivationKind kind, string skillId, CharacterSkillTrigger trigger, string eventId,
        CharacterUltimateDomain domain, int serial)
    {
        StringBuilder canonical = new("character-skill-auto-event-v1;");
        AppendToken(canonical, sourceCharacterId);
        AppendToken(canonical, kind.ToString());
        AppendToken(canonical, skillId);
        AppendToken(canonical, trigger.ToString());
        AppendToken(canonical, eventId);
        AppendToken(canonical, domain.ToString());
        AppendToken(canonical, serial.ToString(
            CultureInfo.InvariantCulture));
        return CharacterSkillActivationFact.HashCanonical(canonical.ToString());
    }

    private static IEnumerable<AutomaticTargetOwner> BuildAutomaticTargets(
        CharacterSkillExecutionContext context,
        CharacterSkillInstance skill)
    {
        var values = new Dictionary<string, AutomaticTargetOwner>(
            StringComparer.Ordinal);

        void AddActor(CharacterActor actor)
        {
            if (actor == null)
                return;
            string id = CharacterPersistentIdentity.Require(actor).Value;
            if (!values.TryGetValue(id, out AutomaticTargetOwner owner))
            {
                owner = new AutomaticTargetOwner(
                    id,
                    actor.Identity?.DisplayName ?? actor.name);
                values.Add(id, owner);
            }
            owner.Attach(actor);
        }

        void AddCombatant(OffenseBattleCombatant combatant)
        {
            if (combatant == null)
                return;
            string id = RequireDomainIdentity(
                combatant.PersistentId,
                nameof(combatant.PersistentId));
            if (!values.TryGetValue(id, out AutomaticTargetOwner owner))
            {
                owner = new AutomaticTargetOwner(id, combatant.DisplayName);
                values.Add(id, owner);
            }
            owner.Attach(context.BattleSession, combatant);
        }

        AddActor(context.Actor);
        AddActor(context.TargetActor);
        if (context.BattleSession != null)
        {
            AddCombatant(CharacterSkillRuntimeEffects
                .ResolveBattleSourceForActivation(context));
            foreach (OffenseBattleCombatant target in
                     CharacterSkillRuntimeEffects
                         .ResolveBattleTargetsForActivation(context, skill))
            {
                AddCombatant(target);
            }
        }
        foreach (AutomaticTargetOwner value in values.Values)
            value.Seal();
        return values.Values.OrderBy(
            value => value.CharacterId,
            StringComparer.Ordinal);
    }

    private static void RollbackAutomatic(
        IEnumerable<AutomaticTargetOwner> targets)
    {
        foreach (AutomaticTargetOwner target in
                 targets ?? Array.Empty<AutomaticTargetOwner>())
        {
            target.Rollback();
        }
    }

    private sealed class AutomaticTargetOwner
    {
        private CharacterActor actor;
        private OffenseBattleSession battleSession;
        private OffenseBattleCombatant combatant;
        private CharacterSkillUseLimitState limitsBefore;
        private CharacterMoodDeliveryTransactionSnapshot statsBefore;
        private bool hasActorSnapshot;
        private OffenseBattleCombatantPersistenceState battleBefore;

        internal AutomaticTargetOwner(string characterId, string displayName)
        {
            CharacterId = RequireDomainIdentity(
                characterId,
                nameof(characterId));
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? CharacterId
                : displayName.Trim();
        }

        internal string CharacterId { get; }
        internal string DisplayName { get; }
        internal string BeforeState { get; private set; }

        internal void Attach(CharacterActor value) => actor = value;

        internal void Attach(
            OffenseBattleSession session,
            OffenseBattleCombatant value)
        {
            battleSession = session;
            combatant = value;
        }

        internal void Seal()
        {
            if (actor != null)
            {
                limitsBefore = RequireLimits(actor).Clone();
                statsBefore = RequireStats(actor)
                    .CaptureMoodDeliveryTransactionState();
                hasActorSnapshot = true;
            }
            if (combatant != null)
                battleBefore = CaptureBattleState();
            BeforeState = CaptureCanonical();
        }

        internal string CaptureCanonical()
        {
            StringBuilder value = new("automatic-skill-target-v1;");
            AppendToken(value, CharacterId);
            AppendToken(value, actor != null
                ? CanonicalState(actor)
                : "none");
            AppendToken(value, actor != null
                ? CanonicalAutomaticLimitState(RequireLimits(actor))
                : "none");
            AppendToken(value, combatant != null
                ? JsonUtility.ToJson(CaptureBattleState())
                : "none");
            return value.ToString();
        }

        internal void Rollback()
        {
            if (hasActorSnapshot && actor?.Progression?.GrowthState != null)
            {
                actor.Progression.GrowthState.useLimits =
                    limitsBefore.Clone();
                actor.Stats?.RestoreMoodDeliveryTransactionState(
                    statsBefore,
                    publish: true);
            }
            if (combatant != null && battleBefore != null)
                RestoreBattleState(combatant, battleBefore);
        }

        private OffenseBattleCombatantPersistenceState CaptureBattleState() =>
            battleSession?.CapturePersistentState()?.combatants
                ?.SingleOrDefault(value => value != null
                    && string.Equals(
                        value.persistentId,
                        combatant.PersistentId,
                        StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                "skill-automatic-battle-target-state-missing");
    }

    private sealed class AppliedActivationState
    {
        internal AppliedActivationState(
            in CharacterSkillAppliedActivationRequest request,
            string sourceId,
            string eventKey,
            long revision,
            int useCountBefore,
            long absoluteHour,
            AutomaticTargetOwner[] targets)
        {
            Request = request;
            SourceId = sourceId;
            EventKey = eventKey;
            Revision = revision;
            UseCountBefore = useCountBefore;
            AbsoluteHour = absoluteHour;
            Targets = targets;
        }

        internal CharacterSkillAppliedActivationRequest Request { get; }
        internal CharacterSkillActivationFact PreparedFact { get; set; }
        internal DomainFactLease Lease { get; set; }
        internal string SourceId { get; }
        internal string EventKey { get; }
        internal long Revision { get; }
        internal int UseCountBefore { get; }
        internal long AbsoluteHour { get; }
        internal AutomaticTargetOwner[] Targets { get; }
    }

    private static string CanonicalAutomaticLimitState(
        CharacterSkillUseLimitState limits)
    {
        StringBuilder history = new("automatic-skill-history-v1;");
        CharacterAutomaticSkillActivationState[] entries =
            (limits.automaticSkillActivations
                ?? new List<CharacterAutomaticSkillActivationState>())
            .Where(value => value != null)
            .OrderBy(value => value.ownerRevision)
            .ToArray();
        foreach (CharacterAutomaticSkillActivationState entry in entries)
        {
            AppendToken(history, entry.eventKeySha256 ?? string.Empty);
            AppendToken(history, entry.skillId ?? string.Empty);
            AppendToken(history, entry.activationKind.ToString());
            AppendToken(history, entry.ownerRevision.ToString(
                CultureInfo.InvariantCulture));
            AppendToken(history, entry.useCount.ToString(
                CultureInfo.InvariantCulture));
        }
        return string.Join("|",
            limits.offenseBattleSerial.ToString(CultureInfo.InvariantCulture),
            limits.defenseInvasionSerial.ToString(CultureInfo.InvariantCulture),
            limits.managementOperatingDay.ToString(CultureInfo.InvariantCulture),
            entries.Length.ToString(CultureInfo.InvariantCulture),
            CharacterSkillActivationFact.HashCanonical(history.ToString()));
    }

    private static void RestoreBattleState(
        OffenseBattleCombatant combatant,
        OffenseBattleCombatantPersistenceState saved)
    {
        if (saved.maxHealth > 0f)
        {
            combatant.RestoreStats(new OffenseBattleStats(
                saved.maxHealth,
                saved.attack,
                saved.strength,
                saved.toughness,
                saved.dexterity,
                saved.moveSpeed,
                saved.shooting,
                saved.evasion));
        }
        combatant.RestoreHealth(saved.currentHealth, saved.totalDamageTaken);
        combatant.RestoreInitiativePenalty(saved.initiativePenalty);
        combatant.SetCover(saved.coverBlockChance);
        combatant.RestoreTurnsStarted(saved.turnsStarted);
        combatant.RestoreFormation(saved.formation);
        combatant.RestoreCombatState(
            saved.suppression,
            saved.bloodLoss,
            saved.lastHitBodyPart,
            saved.fireMode);
        combatant.SetArcanePowerMultiplier(
            saved.hasArcanePowerMultiplier
                ? saved.arcanePowerMultiplier
                : combatant.ArcanePowerMultiplier);
        if (saved.bodyParts != null && saved.bodyParts.Count > 0)
        {
            combatant.ApplyBodyHealth(new CharacterBodyHealthSnapshot(
                saved.bodyParts,
                saved.bloodLoss,
                saved.suppression,
                1f,
                1f,
                1f,
                false));
        }
        combatant.RestoreCooldowns((saved.cooldowns
                ?? new List<OffenseBattleCooldownPersistenceState>())
            .Where(value => value != null)
            .Select(value => new KeyValuePair<string, int>(
                value.abilityId,
                value.remainingTurns)));
        combatant.RestoreStatuses((saved.statuses
                ?? new List<OffenseBattleStatusPersistenceState>())
            .Where(value => value != null)
            .Select(value => new OffenseBattleStatus(
                value.id,
                value.type,
                value.value,
                value.remainingTurns,
                value.sourceId)));
    }

    private static bool TryValidate(
        in CharacterSkillActivationRequest request,
        out CharacterActor[] recipients,
        out string failureReason)
    {
        recipients = Array.Empty<CharacterActor>();
        failureReason = string.Empty;
        if (request.Source == null || request.Source.IsDead)
            failureReason = "skill-activation-source-unavailable";
        else if (request.Source.Progression?.GrowthState == null)
            failureReason = "skill-activation-source-progression-unavailable";
        else if (request.Skill == null || !request.Skill.IsReady)
            failureReason = "skill-activation-skill-unavailable";
        else if (request.ActivationKind is not
                 (CharacterSkillActivationKind.ManualDungeon
                  or CharacterSkillActivationKind.ManualExpedition))
            failureReason = "skill-activation-kind-not-supported";
        else if (request.Skill.kind != CharacterSkillKind.Active
            || request.Skill.trigger != CharacterSkillTrigger.ManualWork)
            failureReason = "skill-activation-manual-contract-invalid";
        else if (request.Skill.manualDurationHours < 1
            || request.Skill.manualCooldownDays < 1
            || request.AbsoluteHour < 0L)
            failureReason = "skill-activation-timing-invalid";
        if (failureReason.Length > 0)
            return false;

        try
        {
            recipients = (request.Recipients ?? Array.Empty<CharacterActor>())
                .Where(value => value != null && !value.IsDead)
                .OrderBy(
                    value => CharacterPersistentIdentity.Require(value).Value,
                    StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception)
        {
            failureReason = "skill-activation-recipient-identity-invalid:"
                + exception.Message;
            return false;
        }
        if (recipients.Length == 0)
        {
            failureReason = "skill-activation-recipient-empty";
            return false;
        }
        if (recipients.Distinct().Count() != recipients.Length)
        {
            failureReason = "skill-activation-recipient-duplicate";
            return false;
        }
        if (request.ChildPublicationKeys == null
            || request.ChildPublicationKeys.Any(value => !DomainFactValueSyntax.IsText(value, 1024, false))
            || request.ChildPublicationKeys.Distinct(StringComparer.Ordinal).Count() != request.ChildPublicationKeys.Count)
        { failureReason = "skill-manual-child-publication-invalid"; return false; }
        return true;
    }

    internal static string ComputeMechanicsSha256(
        CharacterSkillInstance skill)
    {
        if (skill == null)
            throw new ArgumentNullException(nameof(skill));
        StringBuilder canonical = new("character-skill-mechanics-v1;");
        AppendToken(canonical, skill.id);
        AppendToken(canonical, skill.ruleId ?? string.Empty);
        AppendToken(canonical, skill.combinationId ?? string.Empty);
        AppendToken(canonical, skill.kind.ToString());
        AppendToken(canonical, skill.trigger.ToString());
        AppendToken(canonical, skill.target.ToString());
        AppendToken(canonical, skill.targetingMode.ToString());
        AppendToken(canonical, skill.effectArea.ToString());
        AppendToken(canonical, skill.areaSize.ToString(
            CultureInfo.InvariantCulture));
        AppendToken(canonical, skill.manualDurationHours.ToString(
            CultureInfo.InvariantCulture));
        AppendToken(canonical, skill.manualCooldownDays.ToString(
            CultureInfo.InvariantCulture));
        AppendToken(canonical, skill.formulaVersion.ToString(
            CultureInfo.InvariantCulture));
        AppendToken(canonical, skill.formulaCatalogSha256 ?? string.Empty);
        AppendToken(canonical, skill.prominencePolicyVersion.ToString(
            CultureInfo.InvariantCulture));
        AppendToken(canonical, skill.primaryCapabilityId ?? string.Empty);
        AppendToken(canonical, skill.calculatedCost.ToString(
            CultureInfo.InvariantCulture));
        AppendToken(canonical, skill.positiveCost.ToString(
            CultureInfo.InvariantCulture));
        AppendToken(canonical, skill.drawbackCredit.ToString(
            CultureInfo.InvariantCulture));

        string[] modules = (skill.modules
                ?? new List<CharacterSkillModuleSelection>())
            .Where(value => value != null)
            .Select(value => (value.moduleId ?? string.Empty) + "|"
                + (value.variantId ?? string.Empty))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        canonical.Append(modules.Length.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        foreach (string module in modules)
            AppendToken(canonical, module);

        string[] capabilities = (skill.formulaCapabilities
                ?? new List<CharacterSkillFormulaCapabilityEnvelope>())
            .Where(value => value != null)
            .Select(value => CanonicalCapability(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        canonical.Append(capabilities.Length.ToString(
                CultureInfo.InvariantCulture))
            .Append(';');
        foreach (string capability in capabilities)
            AppendToken(canonical, capability);
        return CharacterSkillActivationFact.HashCanonical(canonical.ToString());
    }

    private static string CanonicalCapability(
        CharacterSkillFormulaCapabilityEnvelope capability)
    {
        StringBuilder value = new();
        AppendToken(value, capability.capabilityId ?? string.Empty);
        AppendToken(value, capability.formatterId ?? string.Empty);
        AppendToken(value, capability.applicatorId ?? string.Empty);
        string[] parameters = (capability.parameters
                ?? new List<CharacterSkillFormulaParameter>())
            .Where(parameter => parameter != null)
            .Select(parameter => (parameter.parameterId ?? string.Empty) + "|"
                + parameter.units.ToString(CultureInfo.InvariantCulture))
            .OrderBy(parameter => parameter, StringComparer.Ordinal)
            .ToArray();
        value.Append(parameters.Length.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        foreach (string parameter in parameters)
            AppendToken(value, parameter);
        return value.ToString();
    }

    private static string CanonicalState(CharacterActor actor)
    {
        CharacterStats stats = RequireStats(actor);
        CharacterSkillUseLimitState limits = RequireLimits(actor);
        StringBuilder state = new("character-skill-state-v1;");
        AppendToken(
            state,
            CharacterPersistentIdentity.Require(actor).Value);
        AppendToken(state, stats.MaxHealth.ToString("R",
            CultureInfo.InvariantCulture));
        AppendToken(state, stats.CurrentHealth.ToString("R",
            CultureInfo.InvariantCulture));
        AppendToken(state, stats.InjurySeverity.ToString("R",
            CultureInfo.InvariantCulture));
        KeyValuePair<CharacterCondition, float>[] conditions =
            stats.StatSnapshot
                .OrderBy(value => (int)value.Key)
                .ToArray();
        state.Append(conditions.Length.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        foreach (KeyValuePair<CharacterCondition, float> condition in conditions)
        {
            AppendToken(state, ((int)condition.Key).ToString(
                CultureInfo.InvariantCulture));
            AppendToken(state, condition.Value.ToString("R",
                CultureInfo.InvariantCulture));
        }
        string[] buffs = (limits.manualSkillBuffs
                ?? new List<CharacterManualSkillBuffState>())
            .Where(value => value != null)
            .Select(value => string.Join("|",
                value.sourceCharacterId ?? string.Empty,
                value.skillId ?? string.Empty,
                value.expiresAbsoluteHour.ToString(
                    CultureInfo.InvariantCulture),
                value.frozenSkill == null
                    ? "none"
                    : ComputeMechanicsSha256(value.frozenSkill)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        state.Append(buffs.Length.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        foreach (string buff in buffs)
            AppendToken(state, buff);
        string canonical = state.ToString();
        if (!DomainFactValueSyntax.IsText(
                canonical,
                CharacterSkillActivationFact.MaximumStateCharacters,
                allowEmpty: false))
        {
            throw new InvalidOperationException(
                "skill-activation-recipient-state-too-large");
        }
        return canonical;
    }

    private static CharacterSkillUseLimitState RequireLimits(
        CharacterActor actor)
    {
        if (actor?.Progression?.GrowthState == null)
            throw new InvalidOperationException(
                "Character progression state is unavailable.");
        actor.Progression.GrowthState.useLimits ??=
            new CharacterSkillUseLimitState();
        return actor.Progression.GrowthState.useLimits;
    }

    private static CharacterStats RequireStats(CharacterActor actor) =>
        actor?.Stats ?? throw new InvalidOperationException(
            "Character stats are unavailable.");

    private static CharacterManualSkillCooldownState FindCooldown(
        CharacterSkillUseLimitState limits,
        string skillId) => (limits.manualSkillCooldowns
            ??= new List<CharacterManualSkillCooldownState>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.skillId,
                    skillId,
                    StringComparison.Ordinal));

    private static void Rollback(
        IReadOnlyDictionary<CharacterActor, CharacterSkillUseLimitState>
            limitBefore,
        IReadOnlyDictionary<CharacterActor,
            CharacterMoodDeliveryTransactionSnapshot> statsBefore)
    {
        foreach (KeyValuePair<CharacterActor, CharacterSkillUseLimitState> pair
                 in limitBefore)
        {
            if (pair.Key?.Progression?.GrowthState != null)
                pair.Key.Progression.GrowthState.useLimits = pair.Value.Clone();
        }
        foreach (KeyValuePair<CharacterActor,
                     CharacterMoodDeliveryTransactionSnapshot> pair in
                 statsBefore)
        {
            pair.Key?.Stats?.RestoreMoodDeliveryTransactionState(
                pair.Value,
                publish: true);
        }
    }

    private static void AppendToken(StringBuilder target, string value)
    {
        string normalized = value ?? string.Empty;
        target.Append(normalized.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(normalized).Append(';');
    }

    private static string RequireDomainIdentity(string value, string parameter)
    {
        if (!DomainFactValueSyntax.IsIdentity(value)) throw new ArgumentException("skill-activation-identity-invalid", parameter);
        return value;
    }
    private static bool IsSha256(string value) => value?.Length == 64
        && value.All(current => current is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}
