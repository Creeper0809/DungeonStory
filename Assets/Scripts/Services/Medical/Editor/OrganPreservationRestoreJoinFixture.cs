#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class OrganPreservationRestoreJoinFixture
{
    public static string Run()
    {
        const string partId="surgical-part:1", source="stack:canister:1"; string op="surgical-organ-preservation:"+partId; const long grams=600; string commit=$"physical-batch-disposition:3:{op}:1:{grams}";
        SurgicalPartInstance part=new(){partInstanceId=partId,kind=SurgicalPartKind.NaturalOrgan,preservationOperationId=op,preservationCommitId=commit,preservationSourceStackId=source,preservationInputMassGrams=grams};
        SurgeryAggregateState state=new(); state.Parts.Add(part);
        PhysicalItemRestoreCandidateDispositionSnapshot receipt=new(PhysicalItemDispositionKind.Sink,op,"organ-preservation-canister-consumed","fixture",new[]{source},1,grams,commit);
        SurgeryRestoreCoordinator.ValidatePreservationPhysicalJoin(state,new Query(receipt));
        Reject(state,new Query()); Reject(new SurgeryAggregateState(),new Query(receipt));
        Reject(state,new Query(new PhysicalItemRestoreCandidateDispositionSnapshot(PhysicalItemDispositionKind.Sink,op,"organ-preservation-canister-consumed","fixture",new[]{source},1,grams+1,commit)));
        VerifyAcknowledgementRecovery();
        return "valid/missing/orphan/mismatch organ-preservation joins are fail-closed";
    }
    static void VerifyAcknowledgementRecovery()
    {
        IDungeonItemCatalogProvider catalog=EditorItemCatalogFactory.Create();
        WorldItemRepository repository=new(new GuidPersistentIdGenerator(),new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService inner=new(repository,new PhysicalItemMassQuery(catalog),EditorNullItemMarkerPresenter.Instance);
        FailOnce dispositions=new(inner){FailNext=true};
        string stack=WorldItemRepositoryEditorAccess.AddStack(repository,"medical:organ-preservation-canister",2,WorldItemStackState.FacilityBuffer,position:new Vector2Int(2,2),destinationId:"building:organ-storage");
        SurgicalPartInstance part=new(){partInstanceId="surgical-part:9",kind=SurgicalPartKind.NaturalOrgan};
        string operation=SurgicalOrganPreservationOutbox.FormatOperationId(part.partInstanceId);
        if(!dispositions.TryCommitPending(new[]{new PhysicalItemTransformInput(stack,1)},PhysicalItemDispositionKind.Sink,operation,SurgicalOrganPreservationOutbox.ReasonCode,out PhysicalItemBatchDispositionReceipt receipt,out _))throw new InvalidOperationException("Could not stage organ canister Sink.");
        SurgicalOrganPreservationOutbox.Record(part,receipt);
        if(SurgicalOrganPreservationOutbox.TryFinalize(part,dispositions,out _)||!part.preservationCanisterApplied)throw new InvalidOperationException("Injected organ acknowledgement failure did not preserve outcome.");
        SurgicalPartInstance restored=SurgeryStateCloner.ClonePart(part);
        if(!SurgicalOrganPreservationOutbox.TryFinalize(restored,dispositions,out _)||repository.GetEditorTestQuantity(stack)!=1||SurgicalOrganPreservationOutbox.HasPending(restored))throw new InvalidOperationException("Organ preservation recovery duplicated Sink or retained pending state.");
    }
    static void Reject(SurgeryAggregateState state,Query query){try{SurgeryRestoreCoordinator.ValidatePreservationPhysicalJoin(state,query);}catch(InvalidOperationException){return;}throw new InvalidOperationException("Organ preservation join accepted invalid provenance.");}
    sealed class Query:IPhysicalItemRestoreCandidateQuery{readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> values;public Query(params PhysicalItemRestoreCandidateDispositionSnapshot[] values)=>this.values=values;public bool IsCandidateAvailable=>true;public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> PendingBatchDispositions=>values;public bool TryGetPendingBatchDisposition(string id,out PhysicalItemRestoreCandidateDispositionSnapshot value){foreach(var item in values)if(item.OperationId==id){value=item;return true;}value=null;return false;}}
    sealed class FailOnce:IPhysicalItemBatchDispositionService{readonly IPhysicalItemBatchDispositionService inner;public bool FailNext;public FailOnce(IPhysicalItemBatchDispositionService inner)=>this.inner=inner;public bool TryCommit(IReadOnlyList<PhysicalItemTransformInput> i,PhysicalItemDispositionKind k,string o,string r,out PhysicalItemBatchDispositionReceipt x,out string f)=>inner.TryCommit(i,k,o,r,out x,out f);public bool TryCommitPending(IReadOnlyList<PhysicalItemTransformInput> i,PhysicalItemDispositionKind k,string o,string r,out PhysicalItemBatchDispositionReceipt x,out string f)=>inner.TryCommitPending(i,k,o,r,out x,out f);public bool TryGetPending(string o,out PhysicalItemBatchDispositionReceipt x)=>inner.TryGetPending(o,out x);public bool Acknowledge(string c,out string f){if(FailNext){FailNext=false;f="injected";return false;}return inner.Acknowledge(c,out f);}}
}
#endif
