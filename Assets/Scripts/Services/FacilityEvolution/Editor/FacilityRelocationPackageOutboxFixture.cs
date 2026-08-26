using System;
using System.Collections.Generic;
using UnityEngine;

public static class FacilityRelocationPackageOutboxFixture
{
    public static bool Run()
    {
        IDungeonItemCatalogProvider catalog=EditorItemCatalogFactory.Create();
        WorldItemRepository repository=new(new GuidPersistentIdGenerator(),new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService inner=new(repository,new PhysicalItemMassQuery(catalog),EditorNullItemMarkerPresenter.Instance);
        FailOnce service=new(inner){FailNext=true};
        string stack=WorldItemRepositoryEditorAccess.AddStack(repository,EvolutionCatalystItemDefinitions.FacilityPackageItemId,1,WorldItemStackState.FacilityBuffer,position:new Vector2Int(3,3),destinationId:"facility-input:relocation:qa");
        FacilityRelocationOrder order=new(){orderId="facility-relocation:qa",packageItemId=EvolutionCatalystItemDefinitions.FacilityPackageItemId,packageStackId=stack,destinationId="facility-input:relocation:qa",phase=FacilityRelocationPhase.WaitingForPackage};
        if(FacilityRelocationPackageOutbox.TryCommitOrFinalize(order,service,out _)||!order.packageConsumed||!service.TryGetPending(order.packageTransferOperationId,out PhysicalItemBatchDispositionReceipt receipt))return false;
        FacilityRelocationOrder restored=order.Clone();
        if(!FacilityRelocationPackageOutbox.TryCommitOrFinalize(restored,service,out _)||repository.GetEditorTestQuantity(stack)!=0||restored.packageTransferOperationId.Length!=0)return false;
        PhysicalItemRestoreCandidateDispositionSnapshot candidate=new(receipt.Kind,receipt.OperationId,receipt.ReasonCode,"fixture",receipt.SourceStackIds,receipt.Quantity,receipt.InputMassGrams,receipt.CommitId);
        FacilityEvolutionPendingMaterialRestoreGuard.ValidateRelocationPackageOwnerSet(new[]{order},new Query(candidate));
        if(!Reject(new[]{order},new Query())||!Reject(Array.Empty<FacilityRelocationOrder>(),new Query(candidate)))return false;
        PhysicalItemRestoreCandidateDispositionSnapshot mismatch=new(receipt.Kind,receipt.OperationId,receipt.ReasonCode,"fixture",receipt.SourceStackIds,receipt.Quantity,receipt.InputMassGrams+1,receipt.CommitId);
        return Reject(new[]{order},new Query(mismatch));
    }
    static bool Reject(IReadOnlyList<FacilityRelocationOrder> orders,Query query){try{FacilityEvolutionPendingMaterialRestoreGuard.ValidateRelocationPackageOwnerSet(orders,query);return false;}catch(InvalidOperationException){return true;}}
    sealed class Query:IPhysicalItemRestoreCandidateQuery{readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> values;public Query(params PhysicalItemRestoreCandidateDispositionSnapshot[] values)=>this.values=values;public bool IsCandidateAvailable=>true;public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> PendingBatchDispositions=>values;public bool TryGetPendingBatchDisposition(string id,out PhysicalItemRestoreCandidateDispositionSnapshot value){foreach(var item in values)if(item.OperationId==id){value=item;return true;}value=null;return false;}}
    sealed class FailOnce:IPhysicalItemBatchDispositionService{readonly IPhysicalItemBatchDispositionService inner;public bool FailNext;public FailOnce(IPhysicalItemBatchDispositionService inner)=>this.inner=inner;public bool TryCommit(IReadOnlyList<PhysicalItemTransformInput> i,PhysicalItemDispositionKind k,string o,string r,out PhysicalItemBatchDispositionReceipt x,out string f)=>inner.TryCommit(i,k,o,r,out x,out f);public bool TryCommitPending(IReadOnlyList<PhysicalItemTransformInput> i,PhysicalItemDispositionKind k,string o,string r,out PhysicalItemBatchDispositionReceipt x,out string f)=>inner.TryCommitPending(i,k,o,r,out x,out f);public bool TryGetPending(string o,out PhysicalItemBatchDispositionReceipt x)=>inner.TryGetPending(o,out x);public bool Acknowledge(string c,out string f){if(FailNext){FailNext=false;f="injected";return false;}return inner.Acknowledge(c,out f);}}
}
