using System;
using DungeonStory.Environment;
using DungeonStory.Rooms;

namespace DungeonStory.Exterior
{
    public enum ExteriorZoneKind
    {
        Entrance = 0,
        DropZone = 1,
        ReceptionPoint = 2,
        GuardPost = 3,
        PatrolPoint = 4,
        OutdoorRestSpot = 5,
        ExpeditionStaging = 6,
        IncidentPoint = 7
    }

    public enum ExteriorIncidentKind
    {
        None = 0,
        MerchantCart = 1,
        Informant = 2,
        Thief = 3,
        InjuredReturnee = 4,
        PredatorApproach = 5,
        CargoDamage = 6
    }

    public enum ExteriorIncidentStage
    {
        Preparing = 0,
        Active = 1,
        Interacting = 2,
        Resolved = 3,
        Failed = 4,
        TimedOut = 5
    }

    public enum ExteriorIncidentOutcome
    {
        None = 0,
        TradeAvailable = 1,
        IntelligenceAcquired = 2,
        TheftPrevented = 3,
        ItemStolen = 4,
        RescueOrdered = 5,
        VisitorLost = 6,
        TradePurchased = 7,
        PredatorApproached = 8,
        CargoSecured = 9,
        CargoDamaged = 10
    }

    public readonly struct ExteriorZoneId : IEquatable<ExteriorZoneId>
    {
        public ExteriorZoneId(string value)
        {
            Value = value?.Trim() ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => Value.StartsWith("exterior:", StringComparison.Ordinal);

        public static ExteriorZoneId Create(ExteriorZoneKind kind, int x, int y) =>
            new ExteriorZoneId($"exterior:{kind}:{x}:{y}");

        public bool Equals(ExteriorZoneId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ExteriorZoneId other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);

        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct ExteriorZoneAddress
    {
        public ExteriorZoneAddress(int x, int y, RoomId adjacentRoomId = default)
        {
            X = x;
            Y = y;
            AdjacentRoomId = adjacentRoomId;
        }

        public int X { get; }
        public int Y { get; }
        public RoomId AdjacentRoomId { get; }
    }

    public readonly struct ExteriorZoneSnapshot
    {
        public ExteriorZoneSnapshot(
            ExteriorZoneId zoneId,
            BuildingInstanceId buildingId,
            ExteriorZoneKind kind,
            ExteriorZoneAddress address,
            float cleanliness,
            float damage,
            float patrolReadiness,
            float receptionReadiness,
            int waitingVisitors,
            float firstImpressionBonus,
            int completedWorks)
        {
            ZoneId = zoneId;
            BuildingId = buildingId;
            Kind = kind;
            Address = address;
            Cleanliness = cleanliness;
            Damage = damage;
            PatrolReadiness = patrolReadiness;
            ReceptionReadiness = receptionReadiness;
            WaitingVisitors = waitingVisitors;
            FirstImpressionBonus = firstImpressionBonus;
            CompletedWorks = completedWorks;
        }

        public ExteriorZoneId ZoneId { get; }
        public BuildingInstanceId BuildingId { get; }
        public ExteriorZoneKind Kind { get; }
        public ExteriorZoneAddress Address { get; }
        public float Cleanliness { get; }
        public float Damage { get; }
        public float PatrolReadiness { get; }
        public float ReceptionReadiness { get; }
        public int WaitingVisitors { get; }
        public float FirstImpressionBonus { get; }
        public int CompletedWorks { get; }
    }

    public readonly struct ExteriorHazardSnapshot
    {
        public ExteriorHazardSnapshot(
            DungeonStory.Environment.EnvironmentalCellSnapshot environment,
            float exteriorNightDanger,
            float weatherPressure01)
        {
            Environment = environment;
            ExteriorNightDanger = ExteriorMath.Clamp(exteriorNightDanger, 0f, 100f);
            WeatherPressure01 = ExteriorMath.Clamp(weatherPressure01, 0f, 1f);
        }

        public DungeonStory.Environment.EnvironmentalCellSnapshot Environment { get; }
        public float ExteriorNightDanger { get; }
        public float WeatherPressure01 { get; }
    }

    public readonly struct ExteriorActivityOverviewSnapshot
    {
        public ExteriorActivityOverviewSnapshot(
            int zoneCount,
            int dropZoneCount,
            int incidentCount,
            float averageCleanliness,
            float averageDamage,
            float averagePatrolReadiness,
            float averageReceptionReadiness)
        {
            ZoneCount = zoneCount;
            DropZoneCount = dropZoneCount;
            IncidentCount = incidentCount;
            AverageCleanliness = averageCleanliness;
            AverageDamage = averageDamage;
            AveragePatrolReadiness = averagePatrolReadiness;
            AverageReceptionReadiness = averageReceptionReadiness;
        }

        public int ZoneCount { get; }
        public int DropZoneCount { get; }
        public int IncidentCount { get; }
        public float AverageCleanliness { get; }
        public float AverageDamage { get; }
        public float AveragePatrolReadiness { get; }
        public float AverageReceptionReadiness { get; }
    }

    public static class ExteriorActivityRules
    {
        public static float CalculateIncidentChance(
            ExteriorHazardSnapshot hazard,
            float patrolReadiness) =>
            ExteriorMath.Clamp(
                0.18f
                + hazard.ExteriorNightDanger * 0.004f
                + hazard.WeatherPressure01 * 0.12f
                - ExteriorMath.Clamp(patrolReadiness, 0f, 100f) * 0.0025f,
                0.08f,
                0.72f);

        public static float GetIncidentSelectionWeight(
            ExteriorIncidentKind kind,
            ExteriorHazardSnapshot hazard,
            float patrolReadiness)
        {
            float danger = hazard.ExteriorNightDanger / 100f;
            float patrol = ExteriorMath.Clamp(patrolReadiness / 100f, 0f, 1f);
            return kind switch
            {
                ExteriorIncidentKind.Thief =>
                    Math.Max(0.1f, 0.35f + danger * 2.8f - patrol * 1.35f),
                ExteriorIncidentKind.PredatorApproach =>
                    Math.Max(0.05f, 0.15f + danger * 2.5f - patrol * 1.1f),
                ExteriorIncidentKind.CargoDamage =>
                    Math.Max(0.05f,
                        0.1f + danger * 1.35f
                        + hazard.WeatherPressure01 * 1.8f
                        - patrol * 0.55f),
                ExteriorIncidentKind.MerchantCart => 0.85f,
                ExteriorIncidentKind.Informant => 0.7f,
                ExteriorIncidentKind.InjuredReturnee => 0.55f,
                _ => 0f
            };
        }

        public static float GetReceptionUrgency(ExteriorZoneSnapshot zone, bool activeIncident) =>
            ExteriorMath.Clamp(
                15f + (100f - zone.ReceptionReadiness) * 0.55f
                + zone.WaitingVisitors * 12f
                + (activeIncident ? 25f : 0f),
                0f,
                95f);

        public static float GetPatrolUrgency(ExteriorZoneSnapshot zone, bool activeIncident) =>
            ExteriorMath.Clamp(
                20f + (100f - zone.PatrolReadiness) * 0.55f
                + zone.Damage * 0.25f
                + (activeIncident ? 35f : 0f),
                0f,
                95f);
    }

    internal static class ExteriorMath
    {
        internal static float Clamp(float value, float minimum, float maximum) =>
            Math.Min(maximum, Math.Max(minimum, value));
    }
}
