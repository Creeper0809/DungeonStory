using System;
using DungeonStory.Foundation.Facts;
using DungeonStory.Foundation;
using UnityEngine;

public sealed partial class WildlifeCaptureRuntime
{
    private bool TryReserveCaptureCommand(
        CapturedWildlifeState state,
        string eventKind,
        bool flagBefore,
        bool flagAfter,
        string reasonCode,
        CharacterActor relatedActor,
        out WildlifeGameplayOutcomeSaveData outcome,
        out DomainFactLease reserved,
        out string failureReason)
    {
        EnsureGameplayOutcomeRuntimeCurrent();
        WildlifeActor wildlife = FindActor(state?.wildlifeId);
        long revision = gameplayOutcomes.NextOwnerRevision;
        outcome = CreateCaptureOutcomeBase(
            state,
            wildlife,
            WildlifeGameplayOutcomeProfile.Command,
            eventKind,
            revision);
        outcome.actorId = relatedActor != null
            ? GetCharacterId(relatedActor)
            : string.Empty;
        outcome.actorDisplayName = relatedActor != null
            ? GetCaptureCharacterDisplayName(relatedActor)
            : string.Empty;
        outcome.stateBefore = state != null
            ? (int)state.transportState
            : 0;
        outcome.stateAfter = outcome.stateBefore;
        outcome.flagBefore = flagBefore;
        outcome.flagAfter = flagAfter;
        outcome.reasonCode = reasonCode;
        return gameplayOutcomes.TryReserve(
            outcome,
            out reserved,
            out failureReason);
    }

    private bool TryReserveCaptureAction(
        CapturedWildlifeState state,
        WildlifeActor wildlife,
        CharacterActor actor,
        string eventKind,
        int stateBefore,
        int stateAfter,
        string reasonCode,
        out WildlifeGameplayOutcomeSaveData outcome,
        out DomainFactLease reserved,
        out string failureReason)
    {
        EnsureGameplayOutcomeRuntimeCurrent();
        if (actor == null)
        {
            outcome = null;
            reserved = default;
            failureReason = "captivity-wildlife-outcome-actor-missing";
            return false;
        }

        long revision = gameplayOutcomes.NextOwnerRevision;
        outcome = CreateCaptureOutcomeBase(
            state,
            wildlife,
            WildlifeGameplayOutcomeProfile.Action,
            eventKind,
            revision);
        outcome.actorId = GetCharacterId(actor);
        outcome.actorDisplayName = GetCaptureCharacterDisplayName(actor);
        outcome.stateBefore = Math.Max(0, stateBefore);
        outcome.stateAfter = Math.Max(0, stateAfter);
        int health = wildlife != null ? Mathf.Max(0, wildlife.CurrentHealth) : 0;
        outcome.healthBefore = health;
        outcome.healthAfter = health;
        outcome.reasonCode = reasonCode;
        return gameplayOutcomes.TryReserve(
            outcome,
            out reserved,
            out failureReason);
    }

    private WildlifeGameplayOutcomeSaveData CreateCaptureOutcomeBase(
        CapturedWildlifeState state,
        WildlifeActor wildlife,
        WildlifeGameplayOutcomeProfile profile,
        string eventKind,
        long revision)
    {
        string wildlifeId = state?.wildlifeId?.Trim()
            ?? wildlife?.WildlifeId?.Trim()
            ?? string.Empty;
        string speciesId = state?.speciesId?.Trim()
            ?? wildlife?.SpeciesId?.Trim()
            ?? string.Empty;
        Vector2Int position = wildlife != null
            ? wildlife.GridPosition
            : state?.penPosition ?? Vector2Int.zero;
        var outcome = new WildlifeGameplayOutcomeSaveData
        {
            profile = profile,
            eventKind = eventKind,
            wildlifeId = wildlifeId,
            wildlifeSpeciesId = speciesId,
            wildlifeDisplayName = ResolveCaptureWildlifeDisplayName(
                wildlife,
                speciesId),
            facilityId = state?.penId?.Trim() ?? string.Empty,
            x = position.x,
            y = position.y,
            absoluteDay = gameplayOutcomes.AbsoluteDay,
            ownerRevision = revision
        };
        outcome.operationId = gameplayOutcomes.CreateOperationId(
            profile,
            eventKind,
            wildlifeId,
            revision);
        return outcome;
    }

    private void CommitCaptureOutcome(
        WildlifeGameplayOutcomeSaveData outcome,
        in DomainFactLease reserved)
    {
        gameplayOutcomes.RecordMutation(outcome);
        gameplayOutcomes.CommitReserved(outcome, reserved, out _);
    }

    private string ResolveCaptureWildlifeDisplayName(
        WildlifeActor wildlife,
        string speciesId)
    {
        if (wildlife != null && !string.IsNullOrWhiteSpace(wildlife.DisplayName))
            return wildlife.DisplayName;
        if (speciesCatalog.TryGetSpecies(
                speciesId,
                out WildlifeSpeciesDefinition species)
            && !string.IsNullOrWhiteSpace(species.DisplayName))
        {
            return species.DisplayName;
        }
        return "야생동물";
    }

    private static string GetCaptureCharacterDisplayName(CharacterActor actor)
    {
        string displayName = actor?.Identity?.DisplayName;
        return !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : actor != null ? actor.name : "정착민";
    }
}
