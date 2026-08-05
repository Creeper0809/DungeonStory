using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.FacilityEvolution
{
    public sealed class FacilityEvolutionRestoreCandidate
    {
        internal FacilityEvolutionRestoreCandidate(FacilityEvolutionAggregateSnapshot snapshot) =>
            Snapshot = snapshot;
        public FacilityEvolutionAggregateSnapshot Snapshot { get; }
    }

    public sealed class FacilityEvolutionAggregateStore
    {
        public FacilityEvolutionAggregateStore(FacilityEvolutionAggregateSnapshot initial) =>
            Current = initial ?? throw new ArgumentNullException(nameof(initial));
        public FacilityEvolutionAggregateSnapshot Current { get; private set; }
        public void Commit(FacilityEvolutionRestoreCandidate candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            Current = candidate.Snapshot;
        }
    }

    public static class FacilityEvolutionRestoreRules
    {
        public const int MaximumRawEvents = 128;
        public const int MaximumHistorySegments = 64;
        public const int MaximumNodes = 256;
        public const int MaximumCandidates = 16;

        public static FacilityEvolutionRestoreCandidate Prepare(
            FacilityEvolutionAggregateSnapshot snapshot)
        {
            if (snapshot == null) throw new InvalidOperationException("Facility evolution state is missing.");
            if (!snapshot.BaseFacilityId.IsValid || !snapshot.CurrentFacilityId.IsValid)
                throw new InvalidOperationException("Facility definition IDs are invalid.");
            if (snapshot.StarGrade < 1 || snapshot.StarGrade > 64)
                throw new InvalidOperationException("Facility star grade is outside the supported range.");
            ValidateDistinct(snapshot.LineageTags, "lineage tag");
            ValidateDistinct(snapshot.MutationTags, "mutation tag");
            ValidateFiniteMap(snapshot.IdentityPressures, "identity pressure", allowNegative: true);
            ValidateDistinct(snapshot.HistoryIds, "history ID");
            ValidateRecord(snapshot.Record);
            ValidateInstance(snapshot.Instance);
            return new FacilityEvolutionRestoreCandidate(snapshot);
        }

        private static void ValidateRecord(FacilityEvolutionRecordSnapshot record)
        {
            ValidateFiniteMap(record.Metrics, "record metric", allowNegative: true);
            foreach (KeyValuePair<string, int> pair in record.Tokens)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0)
                    throw new InvalidOperationException("Facility evolution record tokens are invalid.");
            }
            if (record.RecentEvents.Count > 12)
                throw new InvalidOperationException("Facility evolution recent-event history exceeds its limit.");
        }

        private static void ValidateInstance(FacilityInstanceEvolutionSnapshot instance)
        {
            if (!instance.FacilityId.IsValid)
                throw new InvalidOperationException("Facility evolution building instance ID is invalid.");
            if (instance.Generation < 0 || !IsFiniteAtLeast(instance.Mastery, 0f))
                throw new InvalidOperationException("Facility evolution progression is invalid.");
            if (instance.NextUsageSequence < 1)
                throw new InvalidOperationException("Facility evolution usage sequence is invalid.");
            if (instance.CurrentEvents.Count > MaximumRawEvents
                || instance.HistorySegments.Count > MaximumHistorySegments
                || instance.Nodes.Count > MaximumNodes
                || instance.Candidates.Count > MaximumCandidates)
                throw new InvalidOperationException("Facility evolution collection limits were exceeded.");

            long previousSequence = 0;
            HashSet<string> evidenceIds = new(StringComparer.Ordinal);
            foreach (FacilityUsageEventSnapshot entry in instance.CurrentEvents)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.EvidenceId)
                    || string.IsNullOrWhiteSpace(entry.EventId)
                    || !IsFinite(entry.Amount) || entry.Sequence <= previousSequence
                    || !evidenceIds.Add(entry.EvidenceId))
                    throw new InvalidOperationException("Facility evolution usage events are invalid or duplicated.");
                previousSequence = entry.Sequence;
            }
            if (instance.NextUsageSequence <= previousSequence)
                throw new InvalidOperationException("Facility evolution usage sequence does not exceed restored events.");

            HashSet<string> historyHashes = new(StringComparer.Ordinal);
            foreach (FacilityHistorySegmentSnapshot segment in instance.HistorySegments)
            {
                if (segment == null || segment.Level < 0 || segment.FirstGeneration < 0
                    || segment.LastGeneration < segment.FirstGeneration || segment.EventCount < 0
                    || !IsFiniteAtLeast(segment.TotalMagnitude, 0f)
                    || string.IsNullOrWhiteSpace(segment.HistoryHash)
                    || !historyHashes.Add(segment.HistoryHash))
                    throw new InvalidOperationException("Facility evolution history segments are invalid or duplicated.");
                ValidateFiniteMap(segment.Metrics, "history metric", allowNegative: true);
            }

            HashSet<string> nodeIds = new(StringComparer.Ordinal);
            foreach (FacilityEvolutionNodeSnapshot node in instance.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId)
                    || string.IsNullOrWhiteSpace(node.EffectId) || node.Generation < 0
                    || !nodeIds.Add(node.NodeId))
                    throw new InvalidOperationException("Facility evolution nodes are invalid or duplicated.");
            }
            foreach (FacilityEvolutionNodeSnapshot node in instance.Nodes)
            {
                if (node.ParentNodeId.Length > 0 && !nodeIds.Contains(node.ParentNodeId))
                    throw new InvalidOperationException("Facility evolution node parent reference is missing.");
            }

            HashSet<string> candidateIds = new(StringComparer.Ordinal);
            foreach (FacilityEvolutionCandidateSnapshot candidate in instance.Candidates)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.CandidateId)
                    || candidate.TargetGeneration <= instance.Generation
                    || string.IsNullOrWhiteSpace(candidate.BenefitModuleId)
                    || !candidateIds.Add(candidate.CandidateId))
                    throw new InvalidOperationException("Facility evolution candidates are invalid or duplicated.");
            }
            ValidateDistinct(instance.ActiveNodeIds, "active node ID");
            ValidateDistinct(instance.DormantNodeIds, "dormant node ID");
            if (instance.ActiveNodeIds.Intersect(instance.DormantNodeIds, StringComparer.Ordinal).Any()
                || instance.ActiveNodeIds.Any(id => !nodeIds.Contains(id))
                || instance.DormantNodeIds.Any(id => !nodeIds.Contains(id)))
                throw new InvalidOperationException("Facility evolution active/dormant node projections are inconsistent.");
            ValidateDistinct(instance.NarrativeRequestKeys, "narrative request key");
            ValidateWork(instance.PendingWork);
        }

        private static void ValidateWork(FacilityEvolutionWorkSnapshot work)
        {
            if (work == null) return;
            if (!work.OrderId.IsValid || !IsFiniteAtLeast(work.RequiredWork, 0.01f)
                || !IsFiniteAtLeast(work.CompletedWork, 0f)
                || work.CompletedWork > work.RequiredWork + 0.001f)
                throw new InvalidOperationException("Facility evolution work order is invalid.");
            if (work.Kind == FacilityEvolutionWorkKind.Relocation
                && work.Phase != FacilityEvolutionWorkPhase.Dismantling
                && !work.PackageStackId.IsValid)
                throw new InvalidOperationException("Packed facility relocation requires a typed item stack ID.");
            if (work.Kind != FacilityEvolutionWorkKind.Relocation
                && work.Phase is FacilityEvolutionWorkPhase.Dismantling
                    or FacilityEvolutionWorkPhase.WaitingForPackage
                    or FacilityEvolutionWorkPhase.Reinstalling)
                throw new InvalidOperationException("Only relocation orders may use relocation phases.");
        }

        private static void ValidateDistinct(IEnumerable<string> values, string label)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                    throw new InvalidOperationException($"Facility evolution {label} values are invalid or duplicated.");
            }
        }

        private static void ValidateFiniteMap(
            IReadOnlyDictionary<string, float> values,
            string label,
            bool allowNegative)
        {
            foreach (KeyValuePair<string, float> pair in values ?? new Dictionary<string, float>())
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || !IsFinite(pair.Value)
                    || !allowNegative && pair.Value < 0f)
                    throw new InvalidOperationException($"Facility evolution {label} values are invalid.");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFiniteAtLeast(float value, float minimum) => IsFinite(value) && value >= minimum;
    }
}
