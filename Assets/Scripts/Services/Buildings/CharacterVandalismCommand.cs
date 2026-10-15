using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

public interface ICharacterVandalismCommand
{
    bool TryCommitDamage(CharacterActor actor, BuildableObject facility, FacilityVandalismSourceKind source,
        string reason, IReadOnlyList<string> witnessPublicationKeys, out string publicationKey, out string failureReason);
}

public sealed class CharacterVandalismCommand : ICharacterVandalismCommand
{
    private readonly IDomainFactStagingPublisher facts;
    private readonly FacilityVandalismPublicationOutbox publications;
    private readonly IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private readonly IGameCalendar calendar;
    public CharacterVandalismCommand(IDomainFactStagingPublisher facts, FacilityVandalismPublicationOutbox publications,
        IBuildingStructuralIntegrityRuntime structuralIntegrity, IGameCalendar calendar)
    {
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
        this.structuralIntegrity = structuralIntegrity ?? throw new ArgumentNullException(nameof(structuralIntegrity));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public bool TryCommitDamage(CharacterActor actor, BuildableObject facility, FacilityVandalismSourceKind source,
        string reason, IReadOnlyList<string> witnessPublicationKeys, out string publicationKey, out string failureReason)
    {
        publicationKey = string.Empty;
        if (!TryValidateTarget(actor, facility, reason, out failureReason)) return false;
        if (!publications.CanAccept) { failureReason = "facility-vandalism-delivery-capacity-unavailable"; return false; }
        FacilityVandalismCommitState applied;
        IDomainFactStage stage;
        bool hasStructural = structuralIntegrity.TryGet(facility, out var structuralBefore);
        try
        {
            applied = new FacilityVandalismCommitState
            {
                operationSequence = facility.NextVandalismOperationSequence,
                facilityId = facility.RequirePersistentInstanceId().Value,
                buildingDefinitionId = BuildingDefinitionIdentity.Resolve(facility.BuildingData),
                actorId = CharacterPersistentIdentity.Require(actor).Value,
                actorName = (actor.Identity?.DisplayName ?? actor.name)?.Trim(),
                facilityName = FacilityDisplayName(facility)?.Trim(),
                source = source switch
                {
                    FacilityVandalismSourceKind.Macro => "macro",
                    FacilityVandalismSourceKind.Deprivation => "deprivation",
                    _ => throw new ArgumentOutOfRangeException(nameof(source))
                },
                reason = reason?.Trim(), absoluteDay = Math.Max(1, calendar.Day),
                positionX = facility.centerPos.x, positionY = facility.centerPos.y,
                damagedBefore = false, damagedAfter = true, hasStructuralIntegrity = hasStructural,
                structuralHitPointsBefore = hasStructural ? structuralBefore.CurrentHitPoints : 0,
                structuralHitPointsAfter = hasStructural ? structuralBefore.CurrentHitPoints : 0,
                phase = (int)FacilityVandalismCommitPhase.Applied,
                witnessPublicationKeys = (witnessPublicationKeys ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToList()
            };
            if (!facts.TryStage(new FacilityVandalismFact(applied), out stage, out failureReason)) return false;
            if (stage.IsReplay || stage.OwnerRevision != applied.operationSequence
                || string.IsNullOrWhiteSpace(stage.OperationKey))
            {
                stage.Cancel();
                failureReason = "facility-vandalism-owner-publication-conflict";
                return false;
            }
            applied.publicationKey = stage.OperationKey;
        }
        catch (Exception exception)
        {
            failureReason = "facility-vandalism-fact-prepare-failed:" + exception.Message;
            return false;
        }
        var ownerBefore = facility.FacilityState.Clone();
        bool damageBefore = facility.IsDamaged, pendingAdded = false, commitStarted = false;
        try
        {
            facility.SetDamaged(true);
            if (!facility.IsDamaged || facility.IsGridDestroyed)
                throw new InvalidOperationException("facility-vandalism-damage-not-applied");
            bool hasAfter = structuralIntegrity.TryGet(facility, out var after);
            if (hasAfter != hasStructural || hasStructural && !NearlyEqual(structuralBefore.CurrentHitPoints, after.CurrentHitPoints))
                throw new InvalidOperationException("facility-vandalism-structural-owner-changed");
            facility.CommitVandalismState(applied);
            publications.Add(applied);
            pendingAdded = true;
            commitStarted = true;
            DomainFactDelivery result;
            try { result = stage.Commit(); }
            catch (Exception exception) { result = DomainFactDelivery.Uncertain("facility-vandalism-reply-lost:" + exception.Message); }
            if (!result.MayBeCommitted)
            {
                commitStarted = false;
                Rollback(facility, damageBefore, ownerBefore);
                publications.Remove(applied.publicationKey);
                pendingAdded = false;
                failureReason = "facility-vandalism-publication-rejected:" + result.Detail;
                return false;
            }
            // Unknown replies retain damage and the frozen source, including when
            // the building is repaired or removed before the next delivery attempt.
            publications.Acknowledge(applied.publicationKey, result);
            publicationKey = applied.publicationKey;
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            if (!commitStarted)
            {
                Rollback(facility, damageBefore, ownerBefore);
                if (pendingAdded) publications.Remove(applied.publicationKey);
                failureReason = "facility-vandalism-domain-commit-failed:" + exception.Message;
                return false;
            }
            UnityEngine.Debug.LogWarning("facility-vandalism-observation-pending:" + exception.Message);
            publicationKey = applied.publicationKey;
            failureReason = string.Empty;
            return true;
        }
        finally { if (stage.IsActive) stage.Cancel(); }
    }

    private static bool TryValidateTarget(
        CharacterActor actor,
        BuildableObject facility,
        string reason,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || actor.IsDead)
            failureReason = "facility-vandalism-actor-unavailable";
        else if (facility == null)
            failureReason = "facility-vandalism-target-missing";
        else if (facility.IsGridDestroyed || facility.isDestroy)
            failureReason = "facility-vandalism-target-destroyed";
        else if (!FacilityVandalismTargetRules
                     .IsPersistentAggregateTarget(facility))
            failureReason = "facility-vandalism-target-not-persistent-facility";
        else if (facility.IsDamaged)
            failureReason = "facility-vandalism-target-already-damaged";
        else if (!facility.CanApplyDamageRules)
            failureReason = "facility-vandalism-damage-authority-missing";
        else if (facility.Facility == null)
            failureReason = "facility-vandalism-target-not-facility";
        else if (string.IsNullOrWhiteSpace(reason))
            failureReason = "facility-vandalism-reason-missing";
        return failureReason.Length == 0;
    }

    private static string FacilityDisplayName(BuildableObject facility) =>
        !string.IsNullOrWhiteSpace(facility.BuildingData?.objectName)
            ? facility.BuildingData.objectName
            : facility.name;

    private static void Rollback(
        BuildableObject facility,
        bool damagedBefore,
        FacilityRuntimeState ownerBefore)
    {
        facility.SetDamaged(damagedBefore);
        facility.RestoreFacilityState(ownerBefore);
        if (facility.IsDamaged != damagedBefore)
        {
            throw new InvalidOperationException(
                "facility-vandalism-damage-rollback-failed");
        }
    }

    private static bool NearlyEqual(float left, float right) =>
        Math.Abs(left - right) <= 0.0001f;
}
