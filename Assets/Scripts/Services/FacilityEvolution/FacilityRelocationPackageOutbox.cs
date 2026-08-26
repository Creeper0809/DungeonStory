using System;

public static class FacilityRelocationPackageOutbox
{
    public const string ReasonCode="facility-relocation-package-to-wip";
    public static string FormatOperationId(string orderId)=>$"facility-relocation-package:{orderId}";
    public static bool TryCommitOrFinalize(FacilityRelocationOrder order,IPhysicalItemBatchDispositionService service,out string failure)
    {
        failure=string.Empty;
        if(order==null||service==null||string.IsNullOrWhiteSpace(order.packageStackId)){failure="relocation-package-outbox-invalid";return false;}
        if(order.packageConsumed&&string.IsNullOrEmpty(order.packageTransferOperationId))return true;
        string operation=FormatOperationId(order.orderId);
        if(order.packageTransferOperationId.Length==0)
        {
            if(!service.TryCommitPending(new[]{new PhysicalItemTransformInput(order.packageStackId,1)},PhysicalItemDispositionKind.Transfer,operation,ReasonCode,out PhysicalItemBatchDispositionReceipt receipt,out failure))return false;
            order.packageTransferOperationId=receipt.OperationId;order.packageTransferCommitId=receipt.CommitId;order.packageTransferMassGrams=receipt.InputMassGrams;
        }
        bool pending=service.TryGetPending(operation,out PhysicalItemBatchDispositionReceipt staged);
        if(pending&&!string.Equals(staged.CommitId,order.packageTransferCommitId,StringComparison.Ordinal)){failure="relocation-package-receipt-mismatch";return false;}
        if(!order.packageTransferOutcomePublished){if(!pending){failure="relocation-package-receipt-missing";return false;}order.packageConsumed=true;order.packageTransferOutcomePublished=true;}
        if(pending&&!service.Acknowledge(order.packageTransferCommitId,out failure))return false;
        order.packageTransferOperationId=order.packageTransferCommitId=string.Empty;order.packageTransferMassGrams=0;order.packageTransferOutcomePublished=false;
        return true;
    }
}
