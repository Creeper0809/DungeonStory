using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DungeonStory.FacilityEvolution
{
    public enum FacilityEvolutionWorkKind
    {
        Modification = 0,
        Recalibration = 1,
        Relocation = 2
    }

    public enum FacilityEvolutionWorkPhase
    {
        WaitingForMaterials = 0,
        Ready = 1,
        InProgress = 2,
        Completed = 3,
        Cancelled = 4,
        Blocked = 5,
        Dismantling = 6,
        WaitingForPackage = 7,
        Reinstalling = 8
    }

    public sealed class FacilityEvolutionRecordSnapshot
    {
        public FacilityEvolutionRecordSnapshot(
            IReadOnlyDictionary<string, float> metrics,
            IReadOnlyDictionary<string, int> tokens,
            IEnumerable<string> recentEvents)
        {
            Metrics = Copy(metrics);
            Tokens = new ReadOnlyDictionary<string, int>(
                (tokens ?? new Dictionary<string, int>())
                .ToDictionary(pair => Normalize(pair.Key), pair => pair.Value, StringComparer.Ordinal));
            RecentEvents = Array.AsReadOnly((recentEvents ?? Array.Empty<string>())
                .Select(Normalize)
                .Where(value => value.Length > 0)
                .TakeLast(12)
                .ToArray());
        }

        public IReadOnlyDictionary<string, float> Metrics { get; }
        public IReadOnlyDictionary<string, int> Tokens { get; }
        public IReadOnlyList<string> RecentEvents { get; }

        private static IReadOnlyDictionary<string, float> Copy(
            IReadOnlyDictionary<string, float> source) =>
            new ReadOnlyDictionary<string, float>((source ?? new Dictionary<string, float>())
                .ToDictionary(pair => Normalize(pair.Key), pair => pair.Value, StringComparer.Ordinal));

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }

    public sealed class FacilityUsageEventSnapshot
    {
        public FacilityUsageEventSnapshot(
            string evidenceId,
            string eventId,
            string actorId,
            string targetId,
            float amount,
            long sequence,
            IEnumerable<string> sourceTags)
        {
            EvidenceId = Normalize(evidenceId);
            EventId = Normalize(eventId);
            ActorId = Normalize(actorId);
            TargetId = Normalize(targetId);
            Amount = amount;
            Sequence = sequence;
            SourceTags = NormalizeDistinct(sourceTags);
        }

        public string EvidenceId { get; }
        public string EventId { get; }
        public string ActorId { get; }
        public string TargetId { get; }
        public float Amount { get; }
        public long Sequence { get; }
        public IReadOnlyList<string> SourceTags { get; }
        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
        internal static IReadOnlyList<string> NormalizeDistinct(IEnumerable<string> values) =>
            Array.AsReadOnly((values ?? Array.Empty<string>()).Select(Normalize)
                .Where(value => value.Length > 0).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    public sealed class FacilityHistorySegmentSnapshot
    {
        public FacilityHistorySegmentSnapshot(
            int level,
            int firstGeneration,
            int lastGeneration,
            int eventCount,
            float totalMagnitude,
            string historyHash,
            IReadOnlyDictionary<string, float> metrics,
            IEnumerable<FacilityUsageEventSnapshot> keyEvents,
            IEnumerable<string> participantIds,
            IEnumerable<string> sourceTags)
        {
            Level = level;
            FirstGeneration = firstGeneration;
            LastGeneration = lastGeneration;
            EventCount = eventCount;
            TotalMagnitude = totalMagnitude;
            HistoryHash = historyHash?.Trim() ?? string.Empty;
            Metrics = new ReadOnlyDictionary<string, float>(
                (metrics ?? new Dictionary<string, float>()).ToDictionary(
                    pair => pair.Key?.Trim() ?? string.Empty,
                    pair => pair.Value,
                    StringComparer.Ordinal));
            KeyEvents = Array.AsReadOnly((keyEvents ?? Array.Empty<FacilityUsageEventSnapshot>()).ToArray());
            ParticipantIds = FacilityUsageEventSnapshot.NormalizeDistinct(participantIds);
            SourceTags = FacilityUsageEventSnapshot.NormalizeDistinct(sourceTags);
        }

        public int Level { get; }
        public int FirstGeneration { get; }
        public int LastGeneration { get; }
        public int EventCount { get; }
        public float TotalMagnitude { get; }
        public string HistoryHash { get; }
        public IReadOnlyDictionary<string, float> Metrics { get; }
        public IReadOnlyList<FacilityUsageEventSnapshot> KeyEvents { get; }
        public IReadOnlyList<string> ParticipantIds { get; }
        public IReadOnlyList<string> SourceTags { get; }
    }

    public sealed class FacilityEvolutionNodeSnapshot
    {
        public FacilityEvolutionNodeSnapshot(
            string nodeId,
            string parentNodeId,
            string effectId,
            string burdenEffectId,
            int generation,
            bool active,
            bool historical,
            IEnumerable<string> evidenceIds)
        {
            NodeId = nodeId?.Trim() ?? string.Empty;
            ParentNodeId = parentNodeId?.Trim() ?? string.Empty;
            EffectId = effectId?.Trim() ?? string.Empty;
            BurdenEffectId = burdenEffectId?.Trim() ?? string.Empty;
            Generation = generation;
            Active = active;
            Historical = historical;
            EvidenceIds = FacilityUsageEventSnapshot.NormalizeDistinct(evidenceIds);
        }

        public string NodeId { get; }
        public string ParentNodeId { get; }
        public string EffectId { get; }
        public string BurdenEffectId { get; }
        public int Generation { get; }
        public bool Active { get; }
        public bool Historical { get; }
        public IReadOnlyList<string> EvidenceIds { get; }
    }

    public sealed class FacilityEvolutionCandidateSnapshot
    {
        public FacilityEvolutionCandidateSnapshot(
            string candidateId,
            int targetGeneration,
            string benefitModuleId,
            string burdenModuleId,
            string catalystFamily,
            string historyHash)
        {
            CandidateId = candidateId?.Trim() ?? string.Empty;
            TargetGeneration = targetGeneration;
            BenefitModuleId = benefitModuleId?.Trim() ?? string.Empty;
            BurdenModuleId = burdenModuleId?.Trim() ?? string.Empty;
            CatalystFamily = catalystFamily?.Trim() ?? string.Empty;
            HistoryHash = historyHash?.Trim() ?? string.Empty;
        }

        public string CandidateId { get; }
        public int TargetGeneration { get; }
        public string BenefitModuleId { get; }
        public string BurdenModuleId { get; }
        public string CatalystFamily { get; }
        public string HistoryHash { get; }
    }

    public sealed class FacilityEvolutionWorkSnapshot
    {
        public FacilityEvolutionWorkSnapshot(
            FacilityEvolutionOrderId orderId,
            FacilityEvolutionWorkKind kind,
            FacilityEvolutionWorkPhase phase,
            FacilityEvolutionItemId primaryItemId,
            ItemStackId packageStackId,
            float requiredWork,
            float completedWork,
            FacilityGridAddress source,
            FacilityGridAddress destination,
            bool materialsConsumed)
        {
            OrderId = orderId;
            Kind = kind;
            Phase = phase;
            PrimaryItemId = primaryItemId;
            PackageStackId = packageStackId;
            RequiredWork = requiredWork;
            CompletedWork = completedWork;
            Source = source;
            Destination = destination;
            MaterialsConsumed = materialsConsumed;
        }

        public FacilityEvolutionOrderId OrderId { get; }
        public FacilityEvolutionWorkKind Kind { get; }
        public FacilityEvolutionWorkPhase Phase { get; }
        public FacilityEvolutionItemId PrimaryItemId { get; }
        public ItemStackId PackageStackId { get; }
        public float RequiredWork { get; }
        public float CompletedWork { get; }
        public FacilityGridAddress Source { get; }
        public FacilityGridAddress Destination { get; }
        public bool MaterialsConsumed { get; }
    }

    public sealed class FacilityInstanceEvolutionSnapshot
    {
        public FacilityInstanceEvolutionSnapshot(
            BuildingInstanceId facilityId,
            int generation,
            float mastery,
            long nextUsageSequence,
            IEnumerable<FacilityUsageEventSnapshot> currentEvents,
            IEnumerable<FacilityHistorySegmentSnapshot> historySegments,
            IEnumerable<FacilityEvolutionNodeSnapshot> nodes,
            IEnumerable<FacilityEvolutionCandidateSnapshot> candidates,
            IEnumerable<string> activeNodeIds,
            IEnumerable<string> dormantNodeIds,
            IEnumerable<string> narrativeRequestKeys,
            FacilityEvolutionWorkSnapshot pendingWork)
        {
            FacilityId = facilityId;
            Generation = generation;
            Mastery = mastery;
            NextUsageSequence = nextUsageSequence;
            CurrentEvents = Array.AsReadOnly((currentEvents ?? Array.Empty<FacilityUsageEventSnapshot>()).ToArray());
            HistorySegments = Array.AsReadOnly((historySegments ?? Array.Empty<FacilityHistorySegmentSnapshot>()).ToArray());
            Nodes = Array.AsReadOnly((nodes ?? Array.Empty<FacilityEvolutionNodeSnapshot>()).ToArray());
            Candidates = Array.AsReadOnly((candidates ?? Array.Empty<FacilityEvolutionCandidateSnapshot>()).ToArray());
            ActiveNodeIds = FacilityUsageEventSnapshot.NormalizeDistinct(activeNodeIds);
            DormantNodeIds = FacilityUsageEventSnapshot.NormalizeDistinct(dormantNodeIds);
            NarrativeRequestKeys = FacilityUsageEventSnapshot.NormalizeDistinct(narrativeRequestKeys);
            PendingWork = pendingWork;
        }

        public BuildingInstanceId FacilityId { get; }
        public int Generation { get; }
        public float Mastery { get; }
        public long NextUsageSequence { get; }
        public IReadOnlyList<FacilityUsageEventSnapshot> CurrentEvents { get; }
        public IReadOnlyList<FacilityHistorySegmentSnapshot> HistorySegments { get; }
        public IReadOnlyList<FacilityEvolutionNodeSnapshot> Nodes { get; }
        public IReadOnlyList<FacilityEvolutionCandidateSnapshot> Candidates { get; }
        public IReadOnlyList<string> ActiveNodeIds { get; }
        public IReadOnlyList<string> DormantNodeIds { get; }
        public IReadOnlyList<string> NarrativeRequestKeys { get; }
        public FacilityEvolutionWorkSnapshot PendingWork { get; }
    }

    public sealed class FacilityEvolutionAggregateSnapshot
    {
        public FacilityEvolutionAggregateSnapshot(
            FacilityDefinitionId baseFacilityId,
            FacilityDefinitionId currentFacilityId,
            int starGrade,
            IEnumerable<string> lineageTags,
            IEnumerable<string> mutationTags,
            IReadOnlyDictionary<string, float> identityPressures,
            IEnumerable<string> historyIds,
            FacilityEvolutionRecordSnapshot record,
            FacilityInstanceEvolutionSnapshot instance)
        {
            BaseFacilityId = baseFacilityId;
            CurrentFacilityId = currentFacilityId;
            StarGrade = starGrade;
            LineageTags = FacilityUsageEventSnapshot.NormalizeDistinct(lineageTags);
            MutationTags = FacilityUsageEventSnapshot.NormalizeDistinct(mutationTags);
            IdentityPressures = new ReadOnlyDictionary<string, float>(
                (identityPressures ?? new Dictionary<string, float>()).ToDictionary(
                    pair => pair.Key?.Trim() ?? string.Empty,
                    pair => pair.Value,
                    StringComparer.Ordinal));
            HistoryIds = FacilityUsageEventSnapshot.NormalizeDistinct(historyIds);
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public FacilityDefinitionId BaseFacilityId { get; }
        public FacilityDefinitionId CurrentFacilityId { get; }
        public int StarGrade { get; }
        public IReadOnlyList<string> LineageTags { get; }
        public IReadOnlyList<string> MutationTags { get; }
        public IReadOnlyDictionary<string, float> IdentityPressures { get; }
        public IReadOnlyList<string> HistoryIds { get; }
        public FacilityEvolutionRecordSnapshot Record { get; }
        public FacilityInstanceEvolutionSnapshot Instance { get; }
    }
}
