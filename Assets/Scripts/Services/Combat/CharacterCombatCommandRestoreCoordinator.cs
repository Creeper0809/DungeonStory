using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class CharacterCombatCommandRestoreCoordinator
{
    private const string RestoreParticipantId = "400.world.combat-command-stances";

    private readonly CharacterCombatCommandWorldServices world;
    private readonly CharacterCombatCommandCombatServices combat;
    private readonly CharacterCombatCommandCollaborators collaborators;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly Action published;
    private bool restoreTransactionActive;
    private bool restoreCandidateReady;
    private bool publicationPendingCompletion;

    internal CharacterCombatCommandRestoreCoordinator(
        CharacterCombatCommandWorldServices world,
        CharacterCombatCommandCombatServices combat,
        CharacterCombatCommandCollaborators collaborators,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        Action published)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
        this.collaborators = collaborators
            ?? throw new ArgumentNullException(nameof(collaborators));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.published = published ?? throw new ArgumentNullException(nameof(published));
    }

    internal string ParticipantId => RestoreParticipantId;

    internal CharacterCombatCommandRestoreCandidate PrepareRestore(
        CharacterCombatCommandSaveData payload)
    {
        DungeonGameRestoreReport report = new();
        CharacterCombatCommandSaveValidation.Validate(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Combat-command restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        if (!world.WorldRegistry.TryGetGrid(out Grid grid) || grid == null)
        {
            throw new InvalidOperationException(
                "Combat-command restore requires a facility Grid candidate.");
        }

        ValidateWorldReferences(payload, grid, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Combat-command world references are invalid: "
                + string.Join(" | ", report.Errors));
        }

        return new CharacterCombatCommandRestoreCandidate(
            CharacterCombatCommandSaveValidation.CreateState(payload));
    }

    internal void PublishRestore(CharacterCombatCommandRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (!restoreTransactionActive || !aggregateRootStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Combat-command restore requires the V18 save registry transaction boundary.");
        }
        if (restoreCandidateReady)
        {
            throw new InvalidOperationException(
                "A combat-command restore candidate was staged more than once.");
        }

        aggregateRootStore.Replace(candidate.State);
        restoreCandidateReady = true;
    }

    internal void BeginRestoreCandidate()
    {
        if (restoreTransactionActive)
        {
            throw new InvalidOperationException(
                "A combat-command restore candidate is already active.");
        }

        restoreTransactionActive = true;
        restoreCandidateReady = false;
    }

    internal void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive || !restoreCandidateReady)
        {
            throw new InvalidOperationException(
                "No combat-command restore candidate is ready to publish.");
        }

        // Character-world publication already exposes the detached restored
        // actors to the registry. Do not mutate their AI or presentation until
            // every later participant has published and the aggregate root pointer
            // has changed. Unpausing AI triggers an immediate replan, so a
            // publish/rollback projection cannot restore reservations exactly.
        publicationPendingCompletion = true;
        restoreCandidateReady = false;
        restoreTransactionActive = false;
    }

    internal void RollbackPublishedRestoreCandidate()
    {
        publicationPendingCompletion = false;
        restoreCandidateReady = false;
        restoreTransactionActive = false;
    }

    internal void CompleteRestoreCandidate()
    {
        if (!publicationPendingCompletion)
        {
            return;
        }

        CharacterCombatCommandAggregateState state =
            aggregateRootStore.GetOrCreate(
                () => new CharacterCombatCommandAggregateState());
        foreach (CharacterActor actor in world.WorldRegistry.Characters)
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            string actorId = CharacterPersistentIdentity.Require(actor).Value;
            bool inCombatStance = state.CombatStance.Contains(actorId)
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active;
            actor.SetAiPaused(inCombatStance);
            actor.GetComponent<DefenseCombatPresentation>()?.SetStatus(
                inCombatStance ? "전투 태세" : string.Empty,
                inCombatStance);
        }

        publicationPendingCompletion = false;
        published();
    }

    internal void DiscardRestoreCandidate()
    {
        if (publicationPendingCompletion)
        {
            RollbackPublishedRestoreCandidate();
            return;
        }

        restoreCandidateReady = false;
        restoreTransactionActive = false;
    }

    private void ValidateWorldReferences(
        CharacterCombatCommandSaveData payload,
        Grid grid,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, CharacterActor> characters = new(StringComparer.Ordinal);
        foreach (CharacterActor actor in world.WorldRegistry.Characters)
        {
            if (actor == null)
            {
                continue;
            }

            string id = CharacterPersistentIdentity.Require(actor).Value;
            if (!characters.TryAdd(id, actor))
            {
                report.AddError($"Detached character world duplicates combat actor '{id}'.");
            }
        }

        foreach (string actorId in payload.stanceCharacterIds)
        {
            if (!characters.TryGetValue(actorId, out CharacterActor actor)
                || actor.IsDead
                || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
            {
                report.AddError($"Combat stance references unavailable actor '{actorId}'.");
            }
        }

        foreach (CharacterCombatCommand command in payload.commands)
        {
            if (!characters.TryGetValue(command.actorId, out _))
            {
                report.AddError($"Combat command '{command.commandId}' references a missing actor.");
            }
            if (CharacterCombatCommandSaveValidation.RequiresTargetCell(command.type)
                && (!grid.IsValidGridPos(command.TargetCell)
                    || command.type != CombatCommandType.ForceFire
                        && !grid.IsWalkable(command.TargetCell)))
            {
                report.AddError($"Combat command '{command.commandId}' has an invalid target cell.");
            }
            bool missingTarget = command.type == CombatCommandType.Rescue
                ? collaborators.Participants.FindCharacter(command.targetId) == null
                : CharacterCombatCommandSaveValidation.RequiresTargetId(command.type)
                    && !collaborators.Participants.Find(command.targetId).IsValid;
            if (missingTarget)
            {
                report.AddError($"Combat command '{command.commandId}' references missing target '{command.targetId}'.");
            }
            if (command.weaponInstanceId.Length > 0
                && !combat.Equipment.TryGetInstance(command.weaponInstanceId, out _))
            {
                report.AddError($"Combat command '{command.commandId}' references missing weapon '{command.weaponInstanceId}'.");
            }
        }
    }
}
