using System;
using DungeonStory.Foundation.Facts;
using UnityEngine;

namespace DungeonStory.Environment
{
    public sealed class EnvironmentalFireIgnitedFact
    {
        private readonly EnvironmentalFireCauseSaveRecord record;
        public EnvironmentalFireIgnitedFact(EnvironmentalFireCauseSaveRecord value)
        {
            if (value == null || value.legacyPreOutcome || value.outcomeRevision <= 0 || value.absoluteDay < 0
                || !DomainFactValueSyntax.IsIdentity(value.causeId) || !DomainFactValueSyntax.IsIdentity(value.producerId)
                || !DomainFactValueSyntax.IsIdentity(value.evidenceId) || !DomainFactValueSyntax.IsIdentity(value.targetId)
                || !DomainFactValueSyntax.IsText(value.targetDisplayName, DomainFactValueSyntax.MaximumNameCharacters, false)
                || !DomainFactValueSyntax.IsText(value.reason, 4096, false))
                throw new ArgumentException("fire-ignition-fact-invalid");
            var request = Request(value);
            if (!request.IsValid || request.Target.Kind != EnvironmentalFireTargetKind.Building
                || request.Fingerprint != value.fingerprint || !float.IsFinite(value.appliedIntensity)
                || value.appliedIntensity < 0 || value.appliedIntensity > value.requestedIntensity
                || value.appliedIntensity > 1 || !value.hasLocation && (value.positionX != 0 || value.positionY != 0))
                throw new ArgumentException("fire-ignition-fact-request-invalid");
            var disposition = (EnvironmentalFireIgnitionDisposition)value.disposition;
            bool created = disposition == EnvironmentalFireIgnitionDisposition.Ignited;
            bool hasFire = created || disposition == EnvironmentalFireIgnitionDisposition.AlreadyBurning;
            if (!hasFire && disposition is not EnvironmentalFireIgnitionDisposition.TargetMissing
                    and not EnvironmentalFireIgnitionDisposition.TargetNotCombustible and not EnvironmentalFireIgnitionDisposition.FuelUnavailable
                || hasFire && !DomainFactValueSyntax.IsIdentity(value.fireId)
                || !hasFire && value.fireId != string.Empty
                || created && (!value.hasLocation || value.appliedIntensity <= 0)
                || !created && value.appliedIntensity != 0
                || value.fuelQuantity == 0 && (value.fuelTargetKind != 0 || value.fuelTargetId != string.Empty)
                || value.fuelQuantity < 0)
                throw new ArgumentException("fire-ignition-fact-result-invalid");
            record = value.Clone(); record.publicationKey = string.Empty; record.outcomePending = false;
        }
        public EnvironmentalFireCauseSaveRecord Snapshot() => record.Clone();
        public EnvironmentalFireIgnitionRequest ToRequest() => Request(record);
        private static EnvironmentalFireIgnitionRequest Request(EnvironmentalFireCauseSaveRecord value) => new(
            value.causeId, (EnvironmentalFireIgnitionKind)value.ignitionKind, value.producerId,
            new EnvironmentalFireTargetRef((EnvironmentalFireTargetKind)value.targetKind, value.targetId),
            value.requestedIntensity, value.evidenceId,
            value.fuelQuantity > 0 ? new EnvironmentalFireFuelLossRequest(
                new EnvironmentalFireTargetRef((EnvironmentalFireTargetKind)value.fuelTargetKind, value.fuelTargetId), value.fuelQuantity) : null,
            value.targetDisplayName);
        public static EnvironmentalFireIgnitedFact Create(EnvironmentalFireIgnitionRequest request, in EnvironmentalFireIgnitionResult result,
            Vector2Int position, int day, long revision, float appliedIntensity, bool hasLocation = true) => new(new EnvironmentalFireCauseSaveRecord
        {
            causeId = request.CauseId, fingerprint = request.Fingerprint, ignitionKind = (int)request.Kind,
            producerId = request.ProducerId, evidenceId = request.EvidenceId, targetKind = (int)request.Target.Kind,
            targetId = request.Target.TargetId, requestedIntensity = request.IgnitionIntensity,
            fuelTargetKind = request.FuelLoss == null ? 0 : (int)request.FuelLoss.Target.Kind,
            fuelTargetId = request.FuelLoss?.Target.TargetId ?? string.Empty, fuelQuantity = request.FuelLoss?.Quantity ?? 0,
            disposition = (int)result.Disposition, fireId = result.FireId, reason = result.Reason,
            targetDisplayName = request.TargetDisplayName, positionX = position.x, positionY = position.y,
            hasLocation = hasLocation, absoluteDay = day, outcomeRevision = revision, appliedIntensity = appliedIntensity
        });
    }

    public sealed class EnvironmentalFireSuppressedFact
    {
        private readonly EnvironmentalFireSuppressionSaveRecord record;
        public EnvironmentalFireSuppressedFact(EnvironmentalFireSuppressionSaveRecord value)
        {
            if (value == null || value.legacyPreOutcome || value.outcomeRevision <= 0 || value.absoluteDay < 0
                || !DomainFactValueSyntax.IsIdentity(value.operationId) || !DomainFactValueSyntax.IsIdentity(value.fireId)
                || !DomainFactValueSyntax.IsIdentity(value.workerId)
                || value.phase != (int)EnvironmentalFireSuppressionPhase.Applied || !DomainFactValueSyntax.IsIdentity(value.targetId)
                || !DomainFactValueSyntax.IsText(value.workerDisplayName, DomainFactValueSyntax.MaximumNameCharacters, false)
                || !float.IsFinite(value.intensityBefore) || value.intensityBefore <= 0 || value.intensityBefore > 1
                || !float.IsFinite(value.intensityAfter) || value.intensityAfter < 0 || value.intensityAfter > value.intensityBefore)
                throw new ArgumentException("fire-suppression-fact-invalid");
            var command = Command(value);
            if (!command.IsValid || command.Fingerprint != value.fingerprint
                || command.WaterLeaseId != value.waterLeaseId
                || value.waterCommittedQuantity != (command.Mode == EnvironmentalFireSuppressionMode.Water ? command.WaterQuantity : 0))
                throw new ArgumentException("fire-suppression-fact-command-invalid");
            record = value.Clone();
            record.publicationKey = record.completionPublicationKey = string.Empty;
            record.outcomePending = record.completionIdentityAcknowledged = record.waterAcknowledged = false;
            record.resultDisposition = (int)(value.intensityAfter <= 0.0001f
                ? EnvironmentalFireSuppressionDisposition.Extinguished : EnvironmentalFireSuppressionDisposition.Applied);
            record.resultReason = value.intensityAfter <= 0.0001f ? "The fire was extinguished." : "Suppression contribution applied.";
        }
        public EnvironmentalFireSuppressionSaveRecord Snapshot() => record.Clone();
        public EnvironmentalFireSuppressionCommand ToCommand() => Command(record);
        public static EnvironmentalFireSuppressionCommand Command(EnvironmentalFireSuppressionSaveRecord value) => new(
            value.operationId, value.fireId, value.workerId, new Vector2Int(value.standX, value.standY),
            (EnvironmentalFireSuppressionMode)value.mode, value.approvedWork, value.waterLeaseId, value.waterQuantity,
            value.workerDisplayName, (CharacterCommandOrigin)value.commandOrigin);
        public static EnvironmentalFireSuppressedFact Create(EnvironmentalFireSuppressionCommand command,
            in EnvironmentalFireSuppressionResult result, string target, Vector2Int position, int day, long revision) => new(new EnvironmentalFireSuppressionSaveRecord
        {
            operationId = command.OperationId, fingerprint = command.Fingerprint, fireId = command.FireId,
            workerId = command.WorkerId, workerDisplayName = command.WorkerDisplayName,
            standX = command.StandPosition.x, standY = command.StandPosition.y, mode = (int)command.Mode,
            approvedWork = command.ApprovedWork, waterLeaseId = command.WaterLeaseId, waterQuantity = command.WaterQuantity,
            phase = (int)EnvironmentalFireSuppressionPhase.Applied, resultDisposition = (int)result.Disposition,
            intensityBefore = result.IntensityBefore, intensityAfter = result.IntensityAfter,
            waterCommitId = result.WaterCommitId, waterCommittedQuantity = result.WaterConsumed,
            resultReason = result.Reason, targetId = target, positionX = position.x, positionY = position.y,
            commandOrigin = (int)command.CommandOrigin, absoluteDay = day, outcomeRevision = revision
        });
    }

    public sealed class EnvironmentalFireSuppressionCompletedFact
    {
        private readonly EnvironmentalFireSuppressionSaveRecord record;
        public EnvironmentalFireSuppressionCompletedFact(EnvironmentalFireSuppressionSaveRecord source)
        {
            _ = new EnvironmentalFireSuppressedFact(source);
            if (source.outcomePending || !RequiresCompletion(source)
                || !DomainFactValueSyntax.IsText(source.publicationKey, 2048, false))
                throw new ArgumentException("fire-completion-fact-child-not-delivered");
            record = source.Clone(); record.completionPublicationKey = string.Empty; record.completionIdentityAcknowledged = false;
        }
        public EnvironmentalFireSuppressionSaveRecord Snapshot() => record.Clone();
        public static bool RequiresCompletion(EnvironmentalFireSuppressionSaveRecord value) => value != null
            && value.phase == (int)EnvironmentalFireSuppressionPhase.Applied
            && (value.mode == (int)EnvironmentalFireSuppressionMode.Water || value.intensityAfter <= 0.0001f);
    }
}
