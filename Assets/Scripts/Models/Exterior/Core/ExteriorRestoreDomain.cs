using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Exterior
{
    public readonly struct ExteriorIncidentSnapshot
    {
        public ExteriorIncidentSnapshot(
            string incidentId,
            ExteriorIncidentKind kind,
            ExteriorZoneId zoneId,
            ExteriorIncidentStage stage,
            ExteriorIncidentOutcome outcome,
            float durationSeconds,
            float remainingSeconds)
        {
            IncidentId = incidentId?.Trim() ?? string.Empty;
            Kind = kind;
            ZoneId = zoneId;
            Stage = stage;
            Outcome = outcome;
            DurationSeconds = durationSeconds;
            RemainingSeconds = remainingSeconds;
        }

        public string IncidentId { get; }
        public ExteriorIncidentKind Kind { get; }
        public ExteriorZoneId ZoneId { get; }
        public ExteriorIncidentStage Stage { get; }
        public ExteriorIncidentOutcome Outcome { get; }
        public float DurationSeconds { get; }
        public float RemainingSeconds { get; }
        public bool IsTerminal => Stage is ExteriorIncidentStage.Resolved
            or ExteriorIncidentStage.Failed
            or ExteriorIncidentStage.TimedOut;
    }

    public sealed class ExteriorActivityRestoreCandidate
    {
        internal ExteriorActivityRestoreCandidate(
            int nextIncidentSequence,
            IReadOnlyList<ExteriorZoneSnapshot> zones,
            IReadOnlyList<ExteriorIncidentSnapshot> incidents)
        {
            NextIncidentSequence = nextIncidentSequence;
            Zones = zones;
            Incidents = incidents;
        }

        public int NextIncidentSequence { get; }
        public IReadOnlyList<ExteriorZoneSnapshot> Zones { get; }
        public IReadOnlyList<ExteriorIncidentSnapshot> Incidents { get; }
    }

    public sealed class ExteriorActivityState
    {
        public ExteriorActivityState(
            int nextIncidentSequence,
            IReadOnlyList<ExteriorZoneSnapshot> zones,
            IReadOnlyList<ExteriorIncidentSnapshot> incidents)
        {
            NextIncidentSequence = nextIncidentSequence;
            Zones = zones ?? Array.Empty<ExteriorZoneSnapshot>();
            Incidents = incidents ?? Array.Empty<ExteriorIncidentSnapshot>();
        }

        public int NextIncidentSequence { get; }
        public IReadOnlyList<ExteriorZoneSnapshot> Zones { get; }
        public IReadOnlyList<ExteriorIncidentSnapshot> Incidents { get; }
    }

    public sealed class ExteriorActivityStateStore
    {
        public ExteriorActivityStateStore(ExteriorActivityState initial)
        {
            Current = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        public ExteriorActivityState Current { get; private set; }

        public void Commit(ExteriorActivityRestoreCandidate candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            Current = new ExteriorActivityState(
                candidate.NextIncidentSequence,
                candidate.Zones.ToArray(),
                candidate.Incidents.ToArray());
        }
    }

    public static class ExteriorActivityRestoreRules
    {
        public const int MaximumZones = 64;
        public const int MaximumIncidentHistory = 32;

        public static ExteriorActivityRestoreCandidate Prepare(
            int nextIncidentSequence,
            IReadOnlyList<ExteriorZoneSnapshot> zones,
            IReadOnlyList<ExteriorIncidentSnapshot> incidents)
        {
            if (nextIncidentSequence < 1)
                throw new InvalidOperationException("Exterior incident sequence must be positive.");
            if (zones == null || zones.Count == 0 || zones.Count > MaximumZones)
                throw new InvalidOperationException("Exterior zones are missing or exceed the limit.");
            if (incidents == null || incidents.Count > MaximumIncidentHistory)
                throw new InvalidOperationException("Exterior incident history exceeds the limit.");

            HashSet<ExteriorZoneId> zoneIds = new();
            HashSet<BuildingInstanceId> buildingIds = new();
            HashSet<string> placements = new(StringComparer.Ordinal);
            foreach (ExteriorZoneSnapshot zone in zones)
            {
                ExteriorZoneId expected = ExteriorZoneId.Create(
                    zone.Kind,
                    zone.Address.X,
                    zone.Address.Y);
                if (!zone.ZoneId.IsValid
                    || !zone.ZoneId.Equals(expected)
                    || !zone.BuildingId.IsValid
                    || !zoneIds.Add(zone.ZoneId)
                    || !buildingIds.Add(zone.BuildingId)
                    || !placements.Add($"{zone.Kind}:{zone.Address.X}:{zone.Address.Y}")
                    || !InRange(zone.Cleanliness, 0f, 100f)
                    || !InRange(zone.Damage, 0f, 100f)
                    || !InRange(zone.PatrolReadiness, 0f, 100f)
                    || !InRange(zone.ReceptionReadiness, 0f, 100f)
                    || !InRange(zone.FirstImpressionBonus, 0f, 25f))
                {
                    throw new InvalidOperationException("Exterior zones contain invalid or duplicate state.");
                }
            }

            HashSet<string> incidentIds = new(StringComparer.Ordinal);
            HashSet<ExteriorZoneId> activeZones = new();
            int highestSequence = 0;
            foreach (ExteriorIncidentSnapshot incident in incidents)
            {
                if (incident.Kind == ExteriorIncidentKind.None
                    || !incident.ZoneId.IsValid
                    || !zoneIds.Contains(incident.ZoneId)
                    || !TryParseIncidentId(incident.IncidentId, incident.Kind, out int sequence)
                    || !incidentIds.Add(incident.IncidentId)
                    || !IsFiniteAtLeast(incident.DurationSeconds, 0.01f)
                    || !InRange(incident.RemainingSeconds, 0f, incident.DurationSeconds)
                    || !incident.IsTerminal && !activeZones.Add(incident.ZoneId))
                {
                    throw new InvalidOperationException("Exterior incidents contain invalid or duplicate state.");
                }
                highestSequence = Math.Max(highestSequence, sequence);
            }

            if (nextIncidentSequence <= highestSequence)
                throw new InvalidOperationException("Exterior incident sequence does not exceed restored history.");

            return new ExteriorActivityRestoreCandidate(
                nextIncidentSequence,
                zones.ToArray(),
                incidents.ToArray());
        }

        private static bool TryParseIncidentId(
            string incidentId,
            ExteriorIncidentKind kind,
            out int sequence)
        {
            string prefix = $"incident:{kind}:";
            sequence = 0;
            return incidentId != null
                && incidentId.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(incidentId.Substring(prefix.Length), out sequence)
                && sequence > 0;
        }

        private static bool IsFiniteAtLeast(float value, float minimum) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum;

        private static bool InRange(float value, float minimum, float maximum) =>
            IsFiniteAtLeast(value, minimum) && value <= maximum;
    }
}
