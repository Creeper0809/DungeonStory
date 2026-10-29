using System;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

public interface ICharacterVisitorExitOutcomeRuntime
{
    bool BeginExit(CharacterActor actor, string sourceMacro, out string failureReason);
    bool TryStealLooseItemBeforeExit(CharacterActor actor, int searchRadius,
        out WorldItemStackSnapshot stolenItem, out string failureReason);
    bool TryFailExit(CharacterActor actor, VisitorDepartureTerminalKind terminalKind, string failureReason);
    bool TryCompleteDeparture(CharacterActor actor, string handoffKind, out string failureReason);
}

// The exit transaction retains profile write authority; ordinary queries return copies.
public interface ICharacterVisitorExitProfileAuthority
{
    bool TryGetProfileForExit(CharacterActor actor, out WorldCharacterProfile profile);
}

public sealed class CharacterVisitorExitOutcomeRuntime : ICharacterVisitorExitOutcomeRuntime
{
    private readonly ICharacterPopulationService population;
    private readonly ICharacterVisitorExitProfileAuthority profiles;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly WorldItemTheftService theft;
    private readonly VisitorExitPublicationRuntime publications;
    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;

    public CharacterVisitorExitOutcomeRuntime(ICharacterPopulationService population,
        ICharacterVisitorExitProfileAuthority profiles,
        ICombatEquipmentRuntime equipment, WorldItemTheftService theft,
        VisitorExitPublicationRuntime publications, IGameCalendar calendar, IGameEventBus events)
    {
        this.population = population ?? throw new ArgumentNullException(nameof(population));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.theft = theft ?? throw new ArgumentNullException(nameof(theft));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public bool BeginExit(CharacterActor actor, string sourceMacro, out string failureReason)
    {
        if (!TryGetProfile(actor, out WorldCharacterProfile profile, out failureReason)) return false;
        if (!Ready(out failureReason)) return false;
        var existing = profile.visitorExitOperation;
        if (existing != null && existing.IsActive) return true;
        long sequence = profile.nextVisitorExitSequence;
        if (sequence < 1 || sequence == long.MaxValue || calendar.Day < 1)
        { failureReason = "visitor-exit-sequence-or-day-invalid"; return false; }
        string characterId = RequireCharacterId(actor);
        string normalizedMacro = string.IsNullOrWhiteSpace(sourceMacro) ? "exit-dungeon" : sourceMacro.Trim();
        if (!DomainFactValueSyntax.IsText(normalizedMacro, 2048, false))
        { failureReason = "visitor-exit-source-macro-invalid"; return false; }
        string operationId = "visitor-exit:" + characterId + ":" + sequence.ToString(CultureInfo.InvariantCulture);
        if (publications.HasOperation(operationId) || publications.HasOperation(operationId + ":floor-theft:1"))
        { failureReason = "visitor-exit-regressed-sequence"; return false; }
        VisitorDepartureFactData.ValidateIdentity(operationId, sequence, characterId, actor.Identity.DisplayName, calendar.Day);
        profile.visitorExitOperation = new CharacterVisitorExitOperationSaveData
        {
            operationId = operationId, ownerRevision = sequence,
            phase = CharacterVisitorExitPhase.ExitPending,
            sourceMacro = normalizedMacro,
            absoluteDay = calendar.Day, lifecycleBefore = actor.CurrentLifecycleState,
            visitingBefore = profile.isVisiting, visitCountBefore = profile.visitCount,
            equipmentCountBefore = CountOwnedEquipment(characterId)
        };
        profile.nextVisitorExitSequence = sequence + 1;
        return true;
    }

    public bool TryStealLooseItemBeforeExit(CharacterActor actor, int searchRadius,
        out WorldItemStackSnapshot stolenItem, out string failureReason)
    {
        stolenItem = null;
        if (!TryGetActiveOperation(actor, out _, out var operation, out failureReason)) return false;
        if (operation.phase == CharacterVisitorExitPhase.TheftCommitted)
        { failureReason = "items.theft.already_committed_for_exit"; return false; }
        return theft.TryCommitVisitorExitTheft(actor, searchRadius, operation, publications,
            out stolenItem, out failureReason);
    }

    public bool TryFailExit(CharacterActor actor, VisitorDepartureTerminalKind terminalKind, string failureReason)
    {
        if (!Enum.IsDefined(typeof(VisitorDepartureTerminalKind), terminalKind)
            || terminalKind is VisitorDepartureTerminalKind.None or VisitorDepartureTerminalKind.Departed)
            throw new ArgumentOutOfRangeException(nameof(terminalKind));
        if (!TryGetActiveOperation(actor, out var profile, out var operation, out _)) return false;
        var fact = BuildDepartureFact(actor, profile, operation, terminalKind, failureReason,
            actor.CurrentLifecycleState, profile.isVisiting, profile.visitCount,
            CountOwnedEquipment(RequireCharacterId(actor)), false, "none");
        if (!publications.TryPrepare(fact, out var prepared, out _)) return false;
        using (prepared)
        {
            var before = operation.Clone();
            prepared.SetRollback(() => { profile.visitorExitOperation = before.Clone(); return true; });
            SetTerminal(operation, prepared.PublicationKey, terminalKind, failureReason);
            return prepared.PublishApplied().MayBeCommitted;
        }
    }

    public bool TryCompleteDeparture(CharacterActor actor, string handoffKind, out string failureReason)
    {
        if (!TryGetActiveOperation(actor, out var profile, out var operation, out failureReason)) return false;
        string characterId = RequireCharacterId(actor);
        int equipmentBefore = CountOwnedEquipment(characterId);
        bool lifecyclePending = actor.CurrentLifecycleState == operation.lifecycleBefore
            || actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon;
        if (profile.isVisiting != operation.visitingBefore || profile.visitCount != operation.visitCountBefore
            || !lifecyclePending || equipmentBefore != operation.equipmentCountBefore
            || operation.visitCountBefore == int.MaxValue)
        { failureReason = "visitor-exit-authority-replay-conflict"; return false; }
        int expectedVisitCount = operation.visitCountBefore + 1;
        var fact = BuildDepartureFact(actor, profile, operation, VisitorDepartureTerminalKind.Departed,
            string.Empty, CharacterLifecycleState.Despawned, false, expectedVisitCount, 0, equipmentBefore > 0, handoffKind);
        if (!publications.TryPrepare(fact, out var prepared, out failureReason)) return false;
        using (prepared)
        {
            try
            {
                var equipmentSnapshot = equipment.Capture();
                var populationSnapshot = population.CaptureProfiles();
                var lifecycleBefore = actor.CurrentLifecycleState;
                prepared.SetRollback(() =>
                {
                    Rollback(actor, characterId, lifecycleBefore, equipmentSnapshot, populationSnapshot);
                    return true;
                });
                equipment.HandleCharacterDeath(characterId);
                population.ReleaseVisitor(actor);
                actor.SetLifecycleState(CharacterLifecycleState.Despawned);
                if (profile.isVisiting || profile.visitCount != expectedVisitCount || CountOwnedEquipment(characterId) != 0)
                    throw new InvalidOperationException("visitor-exit-authority-postcondition-failed");
                SetTerminal(operation, prepared.PublicationKey, VisitorDepartureTerminalKind.Departed, string.Empty);
                DomainFactDelivery result = prepared.PublishApplied();
                if (!result.MayBeCommitted)
                { failureReason = "visitor-exit-observation-rejected:" + result.Detail; return false; }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            {
                failureReason = "visitor-exit-handoff-failed:" + exception.GetType().Name + ":" + exception.Message;
                return false;
            }
        }
        // The physical departure is committed, even if its observer ACK was lost.
        // This ephemeral projection is never replayed by the durable fact journal.
        try { events.Publish(new VisitorDepartureAppliedEvent(characterId, actor.Identity.DisplayName, operation.absoluteDay)); }
        catch (Exception exception) { UnityEngine.Debug.LogError("visitor-exit-postcommit-projection-failed:" + exception); }
        failureReason = string.Empty;
        return true;
    }

    private static void SetTerminal(CharacterVisitorExitOperationSaveData operation, string publicationKey,
        VisitorDepartureTerminalKind kind, string reason)
    {
        operation.phase = kind == VisitorDepartureTerminalKind.Departed
            ? CharacterVisitorExitPhase.Departed : CharacterVisitorExitPhase.ExitFailed;
        operation.departurePublicationKey = publicationKey;
        operation.terminalKind = kind.ToString();
        operation.failureReason = reason?.Trim() ?? string.Empty;
    }

    private static VisitorDepartureFact BuildDepartureFact(CharacterActor actor, WorldCharacterProfile profile,
        CharacterVisitorExitOperationSaveData operation, VisitorDepartureTerminalKind kind, string failure,
        CharacterLifecycleState lifecycleAfter, bool visitingAfter, int visitCountAfter, int equipmentCountAfter,
        bool equipmentRetired, string handoffKind) => new(new VisitorDepartureFactData
    {
        operationId = operation.operationId, ownerRevision = operation.ownerRevision,
        characterId = RequireCharacterId(actor), characterDisplayName = actor.Identity?.DisplayName ?? profile.displayName,
        absoluteDay = operation.absoluteDay, sourceMacro = operation.sourceMacro, terminalKind = kind,
        failureReason = failure?.Trim() ?? string.Empty, lifecycleBefore = operation.lifecycleBefore,
        lifecycleAfter = lifecycleAfter, visitingBefore = operation.visitingBefore, visitingAfter = visitingAfter,
        visitCountBefore = operation.visitCountBefore, visitCountAfter = visitCountAfter,
        equipmentCountBefore = operation.equipmentCountBefore, equipmentCountAfter = equipmentCountAfter,
        equipmentRetired = equipmentRetired, handoffKind = string.IsNullOrWhiteSpace(handoffKind) ? "none" : handoffKind.Trim(),
        theftPublicationKey = operation.theftPublicationKey
    });

    private bool Ready(out string failure)
    {
        failure = publications.IsBusy || publications.IsRestoring ? "visitor-exit-publication-busy" : string.Empty;
        return failure.Length == 0;
    }

    private void Rollback(
        CharacterActor actor,
        string characterId,
        CharacterLifecycleState lifecycleBefore,
        DungeonCombatEquipmentSaveData equipmentBefore,
        System.Collections.Generic.List<WorldCharacterProfile> populationBefore)
    {
        equipment.PublishRestoreCandidate(
            equipment.BuildRestoreCandidate(equipmentBefore));
        population.RestoreProfiles(populationBefore);
        if (!profiles.TryGetProfileForExit(actor, out WorldCharacterProfile restored)
            || restored == null || restored.persistentId != characterId)
            throw new InvalidOperationException("visitor-exit-profile-rollback-missing");
        population.BindActor(restored, actor);
        actor.SetLifecycleState(lifecycleBefore);
    }

    private int CountOwnedEquipment(string characterId) =>
        equipment.Instances.Count(value => value != null
            && string.Equals(
                value.ownerCharacterId,
                characterId,
                StringComparison.Ordinal));

    private bool TryGetActiveOperation(
        CharacterActor actor,
        out WorldCharacterProfile profile,
        out CharacterVisitorExitOperationSaveData operation,
        out string failureReason)
    {
        operation = null;
        profile = null;
        if (!Ready(out failureReason)) return false;
        if (!TryGetProfile(actor, out profile, out failureReason))
            return false;
        operation = profile.visitorExitOperation;
        if (operation == null || !operation.IsActive)
        {
            failureReason = "visitor-exit-operation-missing";
            return false;
        }
        return true;
    }

    private bool TryGetProfile(
        CharacterActor actor,
        out WorldCharacterProfile profile,
        out string failureReason)
    {
        profile = null;
        if (actor == null || actor.Identity == null)
        {
            failureReason = "visitor-exit-actor-missing";
            return false;
        }
        if (!profiles.TryGetProfileForExit(actor, out profile) || profile == null)
        {
            failureReason = "visitor-exit-profile-missing";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static string RequireCharacterId(CharacterActor actor)
    {
        string characterId = actor?.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(characterId))
            throw new InvalidOperationException("visitor-exit-character-id-missing");
        return characterId;
    }
}
