#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class AccordSignalRestoreJoinFixture
{
    public static void Run()
    {
        const int day=30; string op=$"accord-signal-support:{day:D8}"; const string source="stack:signal:1"; const long grams=900;
        string commit=$"physical-batch-disposition:3:{op}:1:{grams}";
        RunMilestoneWorldSaveData owner=new(){lastAccordSignalSupportAbsoluteDay=day,pendingAccordSignalOperationId=op,pendingAccordSignalCommitId=commit,pendingAccordSignalSourceStackId=source,pendingAccordSignalMassGrams=grams};
        PhysicalItemRestoreCandidateDispositionSnapshot receipt=new(PhysicalItemDispositionKind.Sink,op,"alliance-signal-kit-consumed","fixture",new[]{source},1,grams,commit);
        RunMilestonesSaveSection.ValidateAccordSignalPhysicalJoin(owner,new Query(receipt));
        Reject(owner,new Query()); Reject(new RunMilestoneWorldSaveData(),new Query(receipt));
        Reject(owner,new Query(new PhysicalItemRestoreCandidateDispositionSnapshot(PhysicalItemDispositionKind.Sink,op,"alliance-signal-kit-consumed","fixture",new[]{source},1,grams+1,commit)));
        VerifyAcknowledgementRecovery();
    }
    private static void VerifyAcknowledgementRecovery()
    {
        IDungeonItemCatalogProvider itemCatalog=EditorItemCatalogFactory.Create();
        WorldItemRepository repository=new(new GuidPersistentIdGenerator(),new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService inner=new(repository,new PhysicalItemMassQuery(itemCatalog),EditorNullItemMarkerPresenter.Instance);
        FailOnce disposition=new(inner){FailNext=true};
        string stack=WorldItemRepositoryEditorAccess.AddStack(repository,"supply:alliance-signal-kit",2,WorldItemStackState.FacilityBuffer,position:new Vector2Int(1,1),destinationId:"building:signal");
        V20StoryContentCatalog catalog=new(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        V20CampaignRuntime first=Create(catalog,disposition,null);
        int day=GameCalendarRules.DaysPerSeason;
        if(first.TryActivateAccordSignalSupport(day,stack)) throw new InvalidOperationException("Injected acknowledgement failure unexpectedly succeeded.");
        RunMilestoneWorldSaveData saved=JsonUtility.FromJson<RunMilestoneWorldSaveData>(JsonUtility.ToJson(first.CaptureMilestones()));
        V20CampaignRuntime restored=Create(catalog,disposition,saved);
        if(!restored.TryActivateAccordSignalSupport(day,stack)||repository.GetEditorTestQuantity(stack)!=1||restored.CaptureMilestones().pendingAccordSignalOperationId.Length!=0)
            throw new InvalidOperationException("Accord signal recovery duplicated Sink or retained pending provenance.");
    }
    private static V20CampaignRuntime Create(V20StoryContentCatalog catalog,IPhysicalItemBatchDispositionService disposition,RunMilestoneWorldSaveData data)
    { V20CampaignRuntime runtime=new(new DungeonRuntimeAggregateRootStore(),catalog,disposition); data??=new RunMilestoneWorldSaveData(); if(data.grantedRewardIds.Count==0)data.grantedRewardIds.Add("reward:ending:monster-accord"); runtime.PublishMilestones(runtime.PrepareMilestones(data)); return runtime; }
    private static void Reject(RunMilestoneWorldSaveData data,Query query){try{RunMilestonesSaveSection.ValidateAccordSignalPhysicalJoin(data,query);}catch(InvalidOperationException){return;}throw new InvalidOperationException("Accord signal restore join accepted invalid provenance.");}
    private sealed class Query:IPhysicalItemRestoreCandidateQuery { readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> values; public Query(params PhysicalItemRestoreCandidateDispositionSnapshot[] values)=>this.values=values; public bool IsCandidateAvailable=>true; public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> PendingBatchDispositions=>values; public bool TryGetPendingBatchDisposition(string id,out PhysicalItemRestoreCandidateDispositionSnapshot result){foreach(var value in values)if(value.OperationId==id){result=value;return true;}result=null;return false;} }
    private sealed class FailOnce:IPhysicalItemBatchDispositionService { readonly IPhysicalItemBatchDispositionService inner; public bool FailNext; public FailOnce(IPhysicalItemBatchDispositionService inner)=>this.inner=inner; public bool TryCommit(IReadOnlyList<PhysicalItemTransformInput> i,PhysicalItemDispositionKind k,string o,string r,out PhysicalItemBatchDispositionReceipt x,out string f)=>inner.TryCommit(i,k,o,r,out x,out f); public bool TryCommitPending(IReadOnlyList<PhysicalItemTransformInput> i,PhysicalItemDispositionKind k,string o,string r,out PhysicalItemBatchDispositionReceipt x,out string f)=>inner.TryCommitPending(i,k,o,r,out x,out f); public bool TryGetPending(string o,out PhysicalItemBatchDispositionReceipt x)=>inner.TryGetPending(o,out x); public bool Acknowledge(string c,out string f){if(FailNext){FailNext=false;f="injected";return false;}return inner.Acknowledge(c,out f);} }
}
#endif
