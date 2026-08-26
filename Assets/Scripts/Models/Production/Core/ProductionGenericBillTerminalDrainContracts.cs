using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum ProductionGenericBillTerminalDrainPhase
{
    PreparedAwaitingInputDestinationReceipt = 0,
    InputDestinationReceiptRecordedAwaitingAcknowledgement = 1,
    InputDestinationAcknowledgedAwaitingBillTerminal = 2,
    BillTerminalCommittedAwaitingOwnerAcknowledgement = 3,
    OwnerAcknowledgedAwaitingCheckpointGc = 4
}

public enum ProductionGenericBillTerminalDrainStatus
{
    Applied = 0,
    Replay = 1,
    Deferred = 2,
    Conflict = 3
}

public sealed class ProductionGenericBillTerminalDrainRequest
{
    public ProductionGenericBillTerminalDrainRequest(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        ProductionBillSaveData sourceBill,
        string inputDestinationDrainStepOperationId,
        string inputDestinationDrainRequestFingerprint,
        string requestFingerprint)
    {
        ParentOperationId = parentOperationId ?? string.Empty;
        StepOperationId = stepOperationId ?? string.Empty;
        OwnerStableId = ownerStableId ?? string.Empty;
        SourceBill = ProductionGenericBillTerminalDrainCanonical.CloneBill(sourceBill);
        InputDestinationDrainStepOperationId =
            inputDestinationDrainStepOperationId ?? string.Empty;
        InputDestinationDrainRequestFingerprint =
            inputDestinationDrainRequestFingerprint ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
    }

    public string ParentOperationId { get; }
    public string StepOperationId { get; }
    public string OwnerStableId { get; }
    public ProductionBillSaveData SourceBill { get; }
    public string InputDestinationDrainStepOperationId { get; }
    public string InputDestinationDrainRequestFingerprint { get; }
    public string RequestFingerprint { get; }
}

public readonly struct ProductionGenericBillTerminalDrainResult
{
    public ProductionGenericBillTerminalDrainResult(
        ProductionGenericBillTerminalDrainStatus status,
        ProductionGenericBillTerminalDrainPhase phase,
        string commitId,
        string receiptFingerprint,
        string failureReason)
    {
        Status = status;
        Phase = phase;
        CommitId = commitId ?? string.Empty;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionGenericBillTerminalDrainStatus Status { get; }
    public ProductionGenericBillTerminalDrainPhase Phase { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }
    public string FailureReason { get; }
}

public interface IProductionGenericBillTerminalDrainQuery
{
    bool TryCaptureLiveBill(
        ProductionBillId billId,
        out ProductionBillSaveData sourceBill,
        out string sourceBillFingerprint,
        out string failureReason);

    bool TryCapture(
        string stepOperationId,
        out ProductionGenericBillTerminalDrainSaveData record);

    IReadOnlyList<ProductionGenericBillTerminalDrainSaveData>
        CaptureCurrentFormat();
}

public interface IProductionGenericBillTerminalDrainCommand
{
    ProductionGenericBillTerminalDrainResult TryPrepare(
        ProductionGenericBillTerminalDrainRequest request);

    ProductionGenericBillTerminalDrainResult TryProgress(
        string stepOperationId);

    ProductionGenericBillTerminalDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);

    ProductionGenericBillTerminalDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint);

    ProductionGenericBillTerminalDrainResult TryRecover(
        string stepOperationId);

    bool TryRestoreCurrentFormat(
        IEnumerable<ProductionGenericBillTerminalDrainSaveData> records,
        out string failureReason);
}

[Serializable]
public sealed class ProductionGenericBillTerminalDrainSaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string parentOperationId = string.Empty;
    public string stepOperationId = string.Empty;
    public string ownerStableId = string.Empty;
    public string billId = string.Empty;
    public string facilityId = string.Empty;
    public string inputDestinationId = string.Empty;
    public ProductionBillSaveData sourceBill = new();
    public string sourceBillFingerprint = string.Empty;
    public string inputDestinationDrainStepOperationId = string.Empty;
    public string inputDestinationDrainRequestFingerprint = string.Empty;
    public string requestFingerprint = string.Empty;
    public ProductionGenericBillTerminalDrainPhase phase;

    public string inputDestinationDrainCommitId = string.Empty;
    public string inputDestinationDrainReceiptFingerprint = string.Empty;
    public int releasedInputQuantity;
    public long releasedInputMassGrams;

    public string wipTerminalCommitId = string.Empty;
    public string billTerminalEffectFingerprint = string.Empty;
    public string commitId = string.Empty;
    public string receiptFingerprint = string.Empty;

    public ProductionGenericBillTerminalDrainSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        parentOperationId = parentOperationId,
        stepOperationId = stepOperationId,
        ownerStableId = ownerStableId,
        billId = billId,
        facilityId = facilityId,
        inputDestinationId = inputDestinationId,
        sourceBill = ProductionGenericBillTerminalDrainCanonical.CloneBill(sourceBill),
        sourceBillFingerprint = sourceBillFingerprint,
        inputDestinationDrainStepOperationId = inputDestinationDrainStepOperationId,
        inputDestinationDrainRequestFingerprint =
            inputDestinationDrainRequestFingerprint,
        requestFingerprint = requestFingerprint,
        phase = phase,
        inputDestinationDrainCommitId = inputDestinationDrainCommitId,
        inputDestinationDrainReceiptFingerprint =
            inputDestinationDrainReceiptFingerprint,
        releasedInputQuantity = releasedInputQuantity,
        releasedInputMassGrams = releasedInputMassGrams,
        wipTerminalCommitId = wipTerminalCommitId,
        billTerminalEffectFingerprint = billTerminalEffectFingerprint,
        commitId = commitId,
        receiptFingerprint = receiptFingerprint
    };
}

public static class ProductionGenericBillTerminalDrainCanonical
{
    public const string CommitPrefix =
        "production-generic-bill-terminal-drain-commit:";

    public static ProductionBillSaveData CloneBill(ProductionBillSaveData source)
    {
        if (source == null)
            return null;
        return JsonUtility.FromJson<ProductionBillSaveData>(
            JsonUtility.ToJson(source));
    }

    public static string CreateSourceBillFingerprint(
        ProductionBillSaveData sourceBill) => Hash(
        "production-generic-bill-terminal-source@1|"
        + (sourceBill == null ? string.Empty : JsonUtility.ToJson(sourceBill)));

    public static string CreateRequestFingerprint(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        ProductionBillSaveData sourceBill,
        string inputDestinationDrainStepOperationId,
        string inputDestinationDrainRequestFingerprint)
    {
        StringBuilder canonical = new StringBuilder(512)
            .Append("production-generic-bill-terminal-request@1|");
        AppendToken(canonical, parentOperationId);
        AppendToken(canonical, stepOperationId);
        AppendToken(canonical, ownerStableId);
        AppendToken(canonical, CreateSourceBillFingerprint(sourceBill));
        AppendToken(canonical, inputDestinationDrainStepOperationId);
        AppendToken(canonical, inputDestinationDrainRequestFingerprint);
        return Hash(canonical.ToString());
    }

    public static string CreateWipTerminalCommitId(
        string billId,
        int cycleSequence) =>
        $"production-wip-terminal:{billId}:{cycleSequence:D8}:facilitydestroyed";

    public static string CreateBillTerminalEffectFingerprint(
        string requestFingerprint,
        string inputDestinationDrainReceiptFingerprint,
        string wipTerminalCommitId)
    {
        StringBuilder canonical = new StringBuilder(256)
            .Append("production-generic-bill-terminal-effect@1|");
        AppendToken(canonical, requestFingerprint);
        AppendToken(canonical, inputDestinationDrainReceiptFingerprint);
        AppendToken(canonical, wipTerminalCommitId);
        return Hash(canonical.ToString());
    }

    public static string CreateCommitId(
        string stepOperationId,
        string requestFingerprint) => CommitPrefix + Hash(
        (stepOperationId ?? string.Empty) + "\n"
        + (requestFingerprint ?? string.Empty));

    public static string CreateReceiptFingerprint(
        string requestFingerprint,
        string inputDestinationDrainReceiptFingerprint,
        string billTerminalEffectFingerprint,
        string commitId)
    {
        StringBuilder canonical = new StringBuilder(320)
            .Append("production-generic-bill-terminal-receipt@1|");
        AppendToken(canonical, requestFingerprint);
        AppendToken(canonical, inputDestinationDrainReceiptFingerprint);
        AppendToken(canonical, billTerminalEffectFingerprint);
        AppendToken(canonical, commitId);
        return Hash(canonical.ToString());
    }

    public static bool IsValidSave(
        ProductionGenericBillTerminalDrainSaveData value)
    {
        if (value == null
            || value.schemaVersion !=
                ProductionGenericBillTerminalDrainSaveData.CurrentSchemaVersion
            || !Token(value.parentOperationId)
            || !Token(value.stepOperationId)
            || !Token(value.ownerStableId)
            || !Token(value.billId)
            || !Token(value.facilityId)
            || !Token(value.inputDestinationId)
            || !Token(value.inputDestinationDrainStepOperationId)
            || !Digest(value.inputDestinationDrainRequestFingerprint)
            || !Digest(value.sourceBillFingerprint)
            || !Digest(value.requestFingerprint)
            || !Enum.IsDefined(typeof(ProductionGenericBillTerminalDrainPhase),
                value.phase)
            || !IsValidSourceBill(value.sourceBill)
            || !string.Equals(value.billId, value.sourceBill.billId,
                StringComparison.Ordinal)
            || !string.Equals(value.facilityId,
                value.sourceBill.buildingInstanceId, StringComparison.Ordinal)
            || !string.Equals(value.inputDestinationId,
                value.sourceBill.materialDestinationId, StringComparison.Ordinal)
            || !string.Equals(value.sourceBillFingerprint,
                CreateSourceBillFingerprint(value.sourceBill),
                StringComparison.Ordinal)
            || !string.Equals(value.requestFingerprint,
                CreateRequestFingerprint(
                    value.parentOperationId,
                    value.stepOperationId,
                    value.ownerStableId,
                    value.sourceBill,
                    value.inputDestinationDrainStepOperationId,
                    value.inputDestinationDrainRequestFingerprint),
                StringComparison.Ordinal)
            || value.releasedInputQuantity < 0
            || value.releasedInputMassGrams < 0L)
        {
            return false;
        }

        bool childRecorded = value.phase >=
            ProductionGenericBillTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement;
        bool billTerminal = value.phase >=
            ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement;
        if (!childRecorded)
        {
            return Empty(value.inputDestinationDrainCommitId)
                && Empty(value.inputDestinationDrainReceiptFingerprint)
                && value.releasedInputQuantity == 0
                && value.releasedInputMassGrams == 0L
                && Empty(value.wipTerminalCommitId)
                && Empty(value.billTerminalEffectFingerprint)
                && Empty(value.commitId)
                && Empty(value.receiptFingerprint);
        }
        if (!Token(value.inputDestinationDrainCommitId)
            || !Digest(value.inputDestinationDrainReceiptFingerprint))
        {
            return false;
        }
        if (!billTerminal)
        {
            return Empty(value.wipTerminalCommitId)
                && Empty(value.billTerminalEffectFingerprint)
                && Empty(value.commitId)
                && Empty(value.receiptFingerprint);
        }

        string expectedWip = RequiresWipTerminalReceipt(value.sourceBill)
            ? CreateWipTerminalCommitId(
                value.billId,
                value.sourceBill.cycleSequence)
            : string.Empty;
        string expectedEffect = CreateBillTerminalEffectFingerprint(
            value.requestFingerprint,
            value.inputDestinationDrainReceiptFingerprint,
            expectedWip);
        string expectedCommit = CreateCommitId(
            value.stepOperationId,
            value.requestFingerprint);
        return string.Equals(value.wipTerminalCommitId, expectedWip,
                StringComparison.Ordinal)
            && string.Equals(value.billTerminalEffectFingerprint, expectedEffect,
                StringComparison.Ordinal)
            && string.Equals(value.commitId, expectedCommit,
                StringComparison.Ordinal)
            && string.Equals(value.receiptFingerprint,
                CreateReceiptFingerprint(
                    value.requestFingerprint,
                    value.inputDestinationDrainReceiptFingerprint,
                    expectedEffect,
                    expectedCommit),
                StringComparison.Ordinal);
    }

    public static bool IsDigest(string value) => Digest(value);

    public static bool RequiresWipTerminalReceipt(ProductionBillSaveData bill)
    {
        if (bill == null || !bill.materialsConsumed)
            return false;
        try
        {
            return checked(bill.wipInputMassGrams
                + bill.processCleanWaterMassGrams) > 0L;
        }
        catch (OverflowException)
        {
            return true;
        }
    }

    private static bool IsValidSourceBill(ProductionBillSaveData bill) =>
        bill != null
        && ((ProductionBillId)bill.billId).IsValid
        && ((BuildingInstanceId)bill.buildingInstanceId).IsValid
        && Token(bill.recipeId)
        && Token(bill.materialDestinationId)
        && bill.cycleSequence > 0
        && bill.wipInputQuantity >= 0
        && bill.wipInputMassGrams >= 0L
        && bill.processCleanWaterMassGrams >= 0L
        && bill.processWastewaterMassGrams >= 0L
        && bill.resolvedOutputs != null
        && bill.processWastewaterComponents != null
        && bill.processManualWaterTransfers != null
        && bill.allowedMaterialIds != null
        && bill.allowedWorkerIds != null
        && bill.workerContributions != null
        && bill.outputReservations != null
        && bill.routePolicies != null
        && bill.selectedSupplies != null
        && bill.preparedOutput != null;

    private static void AppendToken(StringBuilder target, string value)
    {
        string token = value ?? string.Empty;
        target.Append(token.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':').Append(token).Append('|');
    }

    private static bool Empty(string value) => string.IsNullOrEmpty(value);
    private static bool Token(string value) => !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    private static bool Digest(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static string Hash(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(value ?? string.Empty));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte current in digest)
            result.Append(current.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }
}
