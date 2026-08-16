using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Work
{
    public sealed class ResearchWorkerHandle
    {
        public ResearchWorkerHandle(object runtimeObject, CharacterId characterId)
        {
            RuntimeObject = runtimeObject
                ?? throw new ArgumentNullException(nameof(runtimeObject));
            CharacterId = characterId;
        }

        public object RuntimeObject { get; }
        public CharacterId CharacterId { get; }
    }

    public sealed class ResearchFacilityHandle
    {
        public ResearchFacilityHandle(object runtimeObject, BuildingInstanceId buildingId)
        {
            RuntimeObject = runtimeObject
                ?? throw new ArgumentNullException(nameof(runtimeObject));
            BuildingId = buildingId;
        }

        public object RuntimeObject { get; }
        public BuildingInstanceId BuildingId { get; }
    }

    public readonly struct ResearchWorkPlan
    {
        public ResearchWorkPlan(float requiredWork, string label)
        {
            RequiredWork = Math.Max(0.1f, requiredWork);
            Label = label ?? string.Empty;
        }

        public float RequiredWork { get; }
        public string Label { get; }
    }

    public readonly struct ResearchWorkProgressResult
    {
        public ResearchWorkProgressResult(
            bool succeeded,
            bool completed,
            float progressRatio,
            string label,
            string failureCode)
        {
            Succeeded = succeeded;
            Completed = completed;
            ProgressRatio = Math.Max(0f, Math.Min(1f, progressRatio));
            Label = label ?? string.Empty;
            FailureCode = failureCode ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Completed { get; }
        public float ProgressRatio { get; }
        public string Label { get; }
        public string FailureCode { get; }
    }

    public interface IResearchWorkRuntimePort
    {
        ResearchWorkerHandle CaptureWorker(object runtimeWorker);
        ResearchFacilityHandle CaptureFacility(object runtimeFacility);
        bool HasResearchWork(ResearchFacilityHandle facility);
        ResearchWorkPlan CreatePlan(ResearchFacilityHandle facility);
        ResearchWorkProgressResult ApplyApprovedWork(
            ResearchWorkerHandle worker,
            ResearchFacilityHandle facility,
            float approvedWorkUnits);
    }

    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class ResearchWorkExecutionHandler
    {
        private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Research };
        private readonly IResearchWorkRuntimePort runtime;

        public ResearchWorkExecutionHandler(IResearchWorkRuntimePort runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

        public bool IsAvailable(
            WorkTypeId workTypeId,
            ResearchFacilityHandle facility,
            out string reason)
        {
            reason = string.Empty;
            return facility != null
                && workTypeId == BuiltInWorkTypeIds.Research
                && runtime.HasResearchWork(facility);
        }

        public ResearchWorkPlan CreatePlan(ResearchFacilityHandle facility) =>
            runtime.CreatePlan(facility);

        public ResearchWorkProgressResult ApplyApprovedWork(
            ResearchWorkerHandle worker,
            ResearchFacilityHandle facility,
            float approvedWorkUnits) =>
            runtime.ApplyApprovedWork(worker, facility, approvedWorkUnits);
    }
}
