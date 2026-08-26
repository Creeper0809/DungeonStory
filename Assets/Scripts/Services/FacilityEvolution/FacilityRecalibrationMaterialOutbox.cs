using System;
using System.Linq;

public static class FacilityRecalibrationMaterialOutbox
{
    public const string ReasonCode="facility-recalibration-catalyst-to-wip";
    public static string FormatOperationId(string orderId)=>$"facility-recalibration-material:{orderId}";
    public static bool TryCommitOrFinalize(FacilityRecalibrationOrder order,IWorldItemStackRuntime items,IPhysicalItemBatchDispositionService service,out string failure)
    {
        if(items==null){failure="recalibration-outbox-invalid";return false;}
        return TryCommitOrFinalize(order,items.GetAllStacks(),service,out failure);
    }

    public static bool TryCommitOrFinalize(FacilityRecalibrationOrder order,System.Collections.Generic.IReadOnlyList<WorldItemStackSnapshot> stacks,IPhysicalItemBatchDispositionService service,out string failure)
    {
        failure=string.Empty;if(order==null||stacks==null||service==null){failure="recalibration-outbox-invalid";return false;}
        if(order.materialsConsumed&&string.IsNullOrEmpty(order.materialTransferOperationId))return true;
        string operation=FormatOperationId(order.orderId);
        if(order.materialTransferOperationId.Length==0)
        {
            WorldItemStackSnapshot source=stacks.Where(x=>x!=null&&x.ItemId==order.catalystItemId&&x.DestinationId==order.destinationId&&x.State==WorldItemStackState.FacilityBuffer&&x.AvailableQuantity>0).OrderBy(x=>x.StackId,StringComparer.Ordinal).FirstOrDefault();
            if(source==null){failure="recalibration-catalyst-missing";return false;}
            if(!service.TryCommitPending(new[]{new PhysicalItemTransformInput(source.StackId,1)},PhysicalItemDispositionKind.Transfer,operation,ReasonCode,out PhysicalItemBatchDispositionReceipt receipt,out failure))return false;
            order.materialTransferOperationId=receipt.OperationId;order.materialTransferCommitId=receipt.CommitId;order.materialTransferSourceStackId=source.StackId;order.materialTransferMassGrams=receipt.InputMassGrams;
        }
        bool pending=service.TryGetPending(operation,out PhysicalItemBatchDispositionReceipt staged);
        if(pending&&(staged.Kind!=PhysicalItemDispositionKind.Transfer
            ||!string.Equals(staged.ReasonCode,ReasonCode,StringComparison.Ordinal)
            ||!string.Equals(staged.CommitId,order.materialTransferCommitId,StringComparison.Ordinal)
            ||staged.Quantity!=1
            ||staged.InputMassGrams!=order.materialTransferMassGrams
            ||staged.SourceStackIds.Count!=1
            ||!string.Equals(staged.SourceStackIds[0],order.materialTransferSourceStackId,StringComparison.Ordinal))){failure="recalibration-receipt-mismatch";return false;}
        if(!order.materialTransferOutcomePublished){if(!pending){failure="recalibration-receipt-missing";return false;}order.materialsConsumed=true;order.state=EvolutionReforgeOrderState.Ready;order.materialTransferOutcomePublished=true;}
        if(pending&&!service.Acknowledge(order.materialTransferCommitId,out failure))return false;
        order.materialTransferOperationId=order.materialTransferCommitId=order.materialTransferSourceStackId=string.Empty;order.materialTransferMassGrams=0;order.materialTransferOutcomePublished=false;return true;
    }
}
