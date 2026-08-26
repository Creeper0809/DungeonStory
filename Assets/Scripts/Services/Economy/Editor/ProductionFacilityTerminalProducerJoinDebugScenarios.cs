using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityTerminalProducerJoinDebugScenarios
{
    private const string Operation = "drain-operation:qa-terminal-join";
    private const string Facility = "facility:qa-terminal-join";
    private const string Request =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Receipt =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Commit = "terminal-drain-commit:qa-terminal-join";

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Terminal Producer Upper Joins")]
    public static void RunAll()
    {
        VerifyGenericPhaseAndReceiptJoin();
        VerifyCombatPhaseAndReceiptJoin();
        VerifyApparelPhaseAndReceiptJoin();
        Debug.Log("V27_TERMINAL_PRODUCER_UPPER_JOIN=PASS");
    }

    private static void VerifyGenericPhaseAndReceiptJoin()
    {
        ProductionFacilityDestructiveDrainOwnerSaveData owner = Owner(
            "bill:qa-terminal-join",
            "step:generic-terminal-join");
        ProductionFacilityDestructiveDrainEntrySaveData entry = Entry(
            ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills,
            owner);
        ProductionGenericBillTerminalDrainSaveData producer = new()
        {
            parentOperationId = Operation,
            stepOperationId = owner.stepOperationId,
            ownerStableId = owner.ownerStableId,
            facilityId = Facility,
            requestFingerprint = Request,
            phase = ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement,
            commitId = Commit,
            receiptFingerprint = Receipt
        };
        HashSet<string> joined = new(StringComparer.Ordinal);
        ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
            .ValidateGenericTerminalProducerJoin(
                entry,
                new[] { producer },
                joined);
        Require(joined.SetEquals(new[] { owner.stepOperationId }),
            "Generic effect-ahead producer did not join its planned owner.");

        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        owner.commitId = Commit;
        owner.receiptFingerprint = Receipt;
        joined.Clear();
        ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
            .ValidateGenericTerminalProducerJoin(
                entry,
                new[] { producer },
                joined);
        owner.receiptFingerprint = Request;
        RequireThrows(
            () => ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
                .ValidateGenericTerminalProducerJoin(
                    entry,
                    new[] { producer },
                    new HashSet<string>(StringComparer.Ordinal)),
            "generic-producer-receipt-mismatch");
    }

    private static void VerifyCombatPhaseAndReceiptJoin()
    {
        ProductionFacilityDestructiveDrainOwnerSaveData owner = Owner(
            "craft-order:qa-terminal-join",
            "step:combat-terminal-join");
        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        owner.commitId = Commit;
        owner.receiptFingerprint = Receipt;
        ProductionFacilityDestructiveDrainEntrySaveData entry = Entry(
            ProductionFacilityDestructiveDrainParticipantIds
                .CombatEquipmentCrafting,
            owner);
        CombatEquipmentTerminalDrainSaveData producer = new()
        {
            parentOperationId = Operation,
            stepOperationId = owner.stepOperationId,
            source = new CombatEquipmentTerminalFrozenSourceSaveData
            {
                ownerStableId = owner.ownerStableId,
                facilityId = Facility
            },
            requestFingerprint = Request,
            phase = CombatEquipmentTerminalDrainPhase
                .TerminalEffectsCommittedAwaitingOwnerAcknowledgement,
            commitId = Commit,
            receiptFingerprint = Receipt
        };
        HashSet<string> joined = new(StringComparer.Ordinal);
        ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
            .ValidateCombatTerminalProducerJoin(
                entry,
                new[] { producer },
                joined);
        Require(joined.Contains(owner.stepOperationId),
            "Combat terminal producer did not join its upper owner.");

        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .OwnerAcknowledged;
        producer.phase = CombatEquipmentTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        joined.Clear();
        ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
            .ValidateCombatTerminalProducerJoin(
                entry,
                new[] { producer },
                joined);
        producer.phase = CombatEquipmentTerminalDrainPhase
            .InputDestinationAcknowledgedAwaitingTerminalEffects;
        RequireThrows(
            () => ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
                .ValidateCombatTerminalProducerJoin(
                    entry,
                    new[] { producer },
                    new HashSet<string>(StringComparer.Ordinal)),
            "combat-producer-phase-mismatch");
    }

    private static void VerifyApparelPhaseAndReceiptJoin()
    {
        ProductionFacilityDestructiveDrainOwnerSaveData owner = Owner(
            "apparel-order:qa-terminal-join",
            "step:apparel-terminal-join");
        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        owner.commitId = Commit;
        owner.receiptFingerprint = Receipt;
        ProductionFacilityDestructiveDrainEntrySaveData entry = Entry(
            ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
            owner);
        ProductionApparelOrderTerminalDrainSaveData producer = new()
        {
            parentOperationId = Operation,
            stepOperationId = owner.stepOperationId,
            ownerStableId = owner.ownerStableId,
            facilityId = Facility,
            requestFingerprint = Request,
            phase = ProductionApparelOrderTerminalDrainPhase
                .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement,
            commitId = Commit,
            receiptFingerprint = Receipt
        };
        HashSet<string> joined = new(StringComparer.Ordinal);
        ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
            .ValidateApparelTerminalProducerJoin(
                entry,
                new[] { producer },
                joined);
        Require(joined.Contains(owner.stepOperationId),
            "Apparel terminal producer did not join its upper owner.");

        owner.phase = ProductionFacilityDestructiveDrainStepPhase.Planned;
        owner.commitId = string.Empty;
        owner.receiptFingerprint = string.Empty;
        producer.phase = ProductionApparelOrderTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        RequireThrows(
            () => ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
                .ValidateApparelTerminalProducerJoin(
                    entry,
                    new[] { producer },
                    new HashSet<string>(StringComparer.Ordinal)),
            "apparel-producer-phase-mismatch");

        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        owner.commitId = Commit;
        owner.receiptFingerprint = Receipt;
        RequireThrows(
            () => ProductionFacilityDestructiveDrainCrossAggregateSaveValidation
                .ValidateApparelTerminalProducerJoin(
                    entry,
                    Array.Empty<ProductionApparelOrderTerminalDrainSaveData>(),
                    new HashSet<string>(StringComparer.Ordinal)),
            "apparel-producer-missing");
    }

    private static ProductionFacilityDestructiveDrainEntrySaveData Entry(
        string participantId,
        ProductionFacilityDestructiveDrainOwnerSaveData owner) => new()
    {
        operationId = Operation,
        facilityId = Facility,
        participants = new List<
            ProductionFacilityDestructiveDrainParticipantSaveData>
        {
            new()
            {
                participantId = participantId,
                owners = new List<
                    ProductionFacilityDestructiveDrainOwnerSaveData>
                {
                    owner
                }
            }
        }
    };

    private static ProductionFacilityDestructiveDrainOwnerSaveData Owner(
        string ownerId,
        string stepId) => new()
    {
        ownerStableId = ownerId,
        stepOperationId = stepId,
        requestFingerprint = Request,
        phase = ProductionFacilityDestructiveDrainStepPhase.Planned
    };

    private static void RequireThrows(Action action, string token)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(token, StringComparison.Ordinal),
                "Unexpected join failure: " + exception.Message);
            return;
        }
        throw new InvalidOperationException(
            "Expected terminal producer join failure was not observed: "
            + token);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
