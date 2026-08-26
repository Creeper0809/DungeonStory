using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

namespace DungeonStory.Balance
{
    [Flags]
    public enum SpatialCellRole
    {
        None = 0,
        ExclusiveFootprint = 1 << 0,
        TraversableFootprint = 1 << 1,
        OperationalAccess = 1 << 2,
        QueueAccess = 1 << 3,
        SharedCorridor = 1 << 4,
        UtilityOverlay = 1 << 5,
        StorageBuffer = 1 << 6,
        OverflowContainment = 1 << 7,
        AuthorizedLooseSource = 1 << 8,
        EmergencyEgress = 1 << 9,
        FixedWorldFeature = 1 << 10
    }

    [BalanceImmutableRecord]
    public sealed class FacilityPlacementCandidate
    {
        public FacilityPlacementCandidate(
            string stableId,
            IEnumerable<Vector2Int> exclusiveFootprint,
            IEnumerable<Vector2Int> operationalAccess,
            IEnumerable<Vector2Int> queueAccess,
            int expectedVisitsPerDay,
            int averageOccupancyMilliSeconds,
            int faultVisitMultiplierPermille = 1000)
        {
            StableId = RequireId(stableId, nameof(stableId));
            ExclusiveFootprint = FreezeCells(exclusiveFootprint);
            OperationalAccess = FreezeCells(operationalAccess);
            QueueAccess = FreezeCells(queueAccess);
            if (ExclusiveFootprint.Count == 0)
                throw new ArgumentException("A facility placement needs a footprint.", nameof(exclusiveFootprint));
            if (OperationalAccess.Count == 0)
                throw new ArgumentException("A facility placement needs operational access.", nameof(operationalAccess));
            if (expectedVisitsPerDay < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedVisitsPerDay));
            if (averageOccupancyMilliSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(averageOccupancyMilliSeconds));
            if (faultVisitMultiplierPermille < 1000)
                throw new ArgumentOutOfRangeException(nameof(faultVisitMultiplierPermille));
            ExpectedVisitsPerDay = expectedVisitsPerDay;
            AverageOccupancyMilliSeconds = averageOccupancyMilliSeconds;
            FaultVisitMultiplierPermille = faultVisitMultiplierPermille;
        }

        public string StableId { get; }
        public IReadOnlyList<Vector2Int> ExclusiveFootprint { get; }
        public IReadOnlyList<Vector2Int> OperationalAccess { get; }
        public IReadOnlyList<Vector2Int> QueueAccess { get; }
        public int ExpectedVisitsPerDay { get; }
        public int AverageOccupancyMilliSeconds { get; }
        public int FaultVisitMultiplierPermille { get; }

        internal static IReadOnlyList<Vector2Int> FreezeCells(IEnumerable<Vector2Int> source)
        {
            if (source == null)
                return Array.Empty<Vector2Int>();
            return source.Distinct().OrderBy(value => value.y).ThenBy(value => value.x).ToArray();
        }

        internal static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A canonical stable ID is required.", parameterName);
            return value;
        }
    }

    [BalanceImmutableRecord]
    public sealed class FacilityRequirement
    {
        public FacilityRequirement(
            string stableId,
            IEnumerable<FacilityPlacementCandidate> candidates)
        {
            StableId = FacilityPlacementCandidate.RequireId(stableId, nameof(stableId));
            Candidates = (candidates ?? throw new ArgumentNullException(nameof(candidates)))
                .OrderBy(value => value?.StableId, StringComparer.Ordinal)
                .ToArray();
            if (Candidates.Count == 0 || Candidates.Any(value => value == null))
                throw new ArgumentException("A facility requirement needs candidates.", nameof(candidates));
            if (Candidates.Select(value => value.StableId).Distinct(StringComparer.Ordinal).Count()
                != Candidates.Count)
                throw new ArgumentException("Facility candidate IDs must be unique.", nameof(candidates));
        }

        public string StableId { get; }
        public IReadOnlyList<FacilityPlacementCandidate> Candidates { get; }
    }

    [BalanceImmutableRecord]
    public sealed class StockSpaceRequirement
    {
        public StockSpaceRequirement(string stableId, IEnumerable<Vector2Int> cells)
        {
            StableId = FacilityPlacementCandidate.RequireId(stableId, nameof(stableId));
            Cells = FacilityPlacementCandidate.FreezeCells(cells);
        }

        public string StableId { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }
    }

    [BalanceImmutableRecord]
    public sealed class OverflowRequirement
    {
        public OverflowRequirement(string stableId, IEnumerable<Vector2Int> cells)
        {
            StableId = FacilityPlacementCandidate.RequireId(stableId, nameof(stableId));
            Cells = FacilityPlacementCandidate.FreezeCells(cells);
        }

        public string StableId { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }
    }

    [BalanceImmutableRecord]
    public sealed class SurvivalContinuityPathSnapshot
    {
        public SurvivalContinuityPathSnapshot(
            string serviceId,
            string pathId,
            bool isPrimitive,
            int capacityPermille,
            int recurringMilliWuPerDay,
            IEnumerable<string> requiredPhysicalItemIds,
            int actionDurationMilliseconds = 0,
            int physicalInputQuantity = 0,
            int recoveryMilliUnits = 0,
            int moodDeltaMilliUnits = 0,
            int hygieneDeltaMilliUnits = 0,
            int wasteMilliUnits = 0,
            int stainMilliUnits = 0)
        {
            ServiceId = FacilityPlacementCandidate.RequireId(serviceId, nameof(serviceId));
            PathId = FacilityPlacementCandidate.RequireId(pathId, nameof(pathId));
            IsPrimitive = isPrimitive;
            if (capacityPermille < 0)
                throw new ArgumentOutOfRangeException(nameof(capacityPermille));
            if (recurringMilliWuPerDay < 0)
                throw new ArgumentOutOfRangeException(nameof(recurringMilliWuPerDay));
            if (actionDurationMilliseconds < 0
                || physicalInputQuantity < 0
                || recoveryMilliUnits < 0
                || wasteMilliUnits < 0
                || stainMilliUnits < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actionDurationMilliseconds),
                    "Continuity costs and positive outputs cannot be negative.");
            }
            CapacityPermille = capacityPermille;
            RecurringMilliWuPerDay = recurringMilliWuPerDay;
            RequiredPhysicalItemIds = (requiredPhysicalItemIds ?? Array.Empty<string>())
                .Select(value => FacilityPlacementCandidate.RequireId(value, nameof(requiredPhysicalItemIds)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            ActionDurationMilliseconds = actionDurationMilliseconds;
            PhysicalInputQuantity = physicalInputQuantity;
            RecoveryMilliUnits = recoveryMilliUnits;
            MoodDeltaMilliUnits = moodDeltaMilliUnits;
            HygieneDeltaMilliUnits = hygieneDeltaMilliUnits;
            WasteMilliUnits = wasteMilliUnits;
            StainMilliUnits = stainMilliUnits;
        }

        public string ServiceId { get; }
        public string PathId { get; }
        public bool IsPrimitive { get; }
        public int CapacityPermille { get; }
        public int RecurringMilliWuPerDay { get; }
        public IReadOnlyList<string> RequiredPhysicalItemIds { get; }
        public int ActionDurationMilliseconds { get; }
        public int PhysicalInputQuantity { get; }
        public int RecoveryMilliUnits { get; }
        public int MoodDeltaMilliUnits { get; }
        public int HygieneDeltaMilliUnits { get; }
        public int WasteMilliUnits { get; }
        public int StainMilliUnits { get; }
    }

    [BalanceImmutableRecord]
    public sealed class ServiceContinuityRequirement
    {
        public ServiceContinuityRequirement(
            string serviceId,
            string primaryPathId,
            string fallbackPathId,
            int outageCoverageHours)
        {
            ServiceId = FacilityPlacementCandidate.RequireId(serviceId, nameof(serviceId));
            PrimaryPathId = FacilityPlacementCandidate.RequireId(primaryPathId, nameof(primaryPathId));
            FallbackPathId = FacilityPlacementCandidate.RequireId(fallbackPathId, nameof(fallbackPathId));
            if (string.Equals(PrimaryPathId, FallbackPathId, StringComparison.Ordinal))
                throw new ArgumentException("Primary and fallback paths must be independent.");
            if (outageCoverageHours < 24)
                throw new ArgumentOutOfRangeException(nameof(outageCoverageHours));
            OutageCoverageHours = outageCoverageHours;
        }

        public string ServiceId { get; }
        public string PrimaryPathId { get; }
        public string FallbackPathId { get; }
        public int OutageCoverageHours { get; }
    }

    public interface ISurvivalContinuityCatalogQuery
    {
        IReadOnlyList<SurvivalContinuityPathSnapshot> CapturePaths(
            PopulationStageContext context);
    }

    [BalanceImmutableRecord]
    public readonly struct PopulationStageContext
    {
        public PopulationStageContext(int population, string researchTierId)
        {
            if (population <= 0)
                throw new ArgumentOutOfRangeException(nameof(population));
            Population = population;
            ResearchTierId = FacilityPlacementCandidate.RequireId(
                researchTierId,
                nameof(researchTierId));
        }

        public int Population { get; }
        public string ResearchTierId { get; }
    }

    [BalanceImmutableRecord]
    public sealed class PopulationStagePortfolio
    {
        public PopulationStagePortfolio(
            int population,
            string researchTierId,
            IEnumerable<Vector2Int> usableInteriorCells,
            IEnumerable<Vector2Int> emergencyEgressCells,
            IEnumerable<Vector2Int> fixedWorldFeatureCells,
            IEnumerable<FacilityRequirement> facilities,
            IEnumerable<StockSpaceRequirement> stockBuffers,
            IEnumerable<OverflowRequirement> overflowBuffers,
            IEnumerable<ServiceContinuityRequirement> criticalServices,
            int minimumHeadroomPermille = 300)
        {
            if (population <= 0)
                throw new ArgumentOutOfRangeException(nameof(population));
            if (minimumHeadroomPermille < 0 || minimumHeadroomPermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(minimumHeadroomPermille));
            Population = population;
            ResearchTierId = FacilityPlacementCandidate.RequireId(researchTierId, nameof(researchTierId));
            UsableInteriorCells = FacilityPlacementCandidate.FreezeCells(usableInteriorCells);
            EmergencyEgressCells = FacilityPlacementCandidate.FreezeCells(emergencyEgressCells);
            FixedWorldFeatureCells = FacilityPlacementCandidate.FreezeCells(fixedWorldFeatureCells);
            Facilities = Freeze(facilities, value => value.StableId);
            StockBuffers = Freeze(stockBuffers, value => value.StableId);
            OverflowBuffers = Freeze(overflowBuffers, value => value.StableId);
            CriticalServices = Freeze(criticalServices, value => value.ServiceId);
            MinimumHeadroomPermille = minimumHeadroomPermille;
            if (UsableInteriorCells.Count == 0)
                throw new ArgumentException("Usable interior cells are required.", nameof(usableInteriorCells));
        }

        public int Population { get; }
        public string ResearchTierId { get; }
        public IReadOnlyList<Vector2Int> UsableInteriorCells { get; }
        public IReadOnlyList<Vector2Int> EmergencyEgressCells { get; }
        public IReadOnlyList<Vector2Int> FixedWorldFeatureCells { get; }
        public IReadOnlyList<FacilityRequirement> Facilities { get; }
        public IReadOnlyList<StockSpaceRequirement> StockBuffers { get; }
        public IReadOnlyList<OverflowRequirement> OverflowBuffers { get; }
        public IReadOnlyList<ServiceContinuityRequirement> CriticalServices { get; }
        public int MinimumHeadroomPermille { get; }

        private static IReadOnlyList<T> Freeze<T>(
            IEnumerable<T> source,
            Func<T, string> id)
            where T : class
        {
            T[] values = (source ?? Array.Empty<T>())
                .OrderBy(id, StringComparer.Ordinal)
                .ToArray();
            if (values.Any(value => value == null)
                || values.Select(id).Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException("Portfolio stable IDs must be unique.");
            return values;
        }
    }

    [BalanceCaptureFactory]
    public static class PopulationStagePortfolioCatalog
    {
        public const int InitialInteriorColumns = 27;
        public const int TierOnePopulation = 12;
        public const int TierTwoPopulation = 18;
        public const int TierThreePopulation = 24;

        public const int TierOneInteriorColumns =
            DungeonSpaceExpansionCatalog.BasicSectorTargetColumns;
        public const int TierTwoInteriorColumns =
            DungeonSpaceExpansionCatalog.SupportedSectorTargetColumns;
        public const int TierThreeInteriorColumns =
            DungeonSpaceExpansionCatalog.DeepSectorTargetColumns;

        private static readonly int[] SupportedPopulations =
        {
            1, 3, 6, TierOnePopulation, TierTwoPopulation, TierThreePopulation
        };

        public static IReadOnlyList<int> PopulationStages => SupportedPopulations;

        public static int TierForPopulation(int population) => population switch
        {
            1 or 3 or 6 => 0,
            TierOnePopulation => 1,
            TierTwoPopulation => 2,
            TierThreePopulation => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(population))
        };

        // Diagnostic capacity requirement only. This does not unlock or mutate
        // dungeon space; the E-key grid expansion remains a developer tool.
        public static int InteriorColumnsForPopulation(int population) =>
            TierForPopulation(population) switch
            {
                0 => InitialInteriorColumns,
                1 => TierOneInteriorColumns,
                2 => TierTwoInteriorColumns,
                3 => TierThreeInteriorColumns,
                _ => throw new ArgumentOutOfRangeException(nameof(population))
            };

        public static PopulationStagePortfolio Capture(int population)
        {
            int tier = TierForPopulation(population);
            int columns = InteriorColumnsForPopulation(population);
            List<Vector2Int> usable = new(columns * 3);
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < columns; x++)
                usable.Add(new Vector2Int(x, y));

            int storageCells = population switch
            {
                1 => 3,
                3 => 3,
                6 => 4,
                12 => 6,
                18 => 8,
                24 => 10,
                _ => throw new ArgumentOutOfRangeException(nameof(population))
            };
            int overflowCells = population switch
            {
                1 or 3 => 1,
                6 => 2,
                12 => 4,
                18 => 5,
                24 => 6,
                _ => throw new ArgumentOutOfRangeException(nameof(population))
            };

            List<SpatialModuleSpec> modules = CreateModules(population);
            FacilityRequirement[] facilities = LayoutModules(
                modules,
                columns,
                storageCells,
                overflowCells);
            StockSpaceRequirement stock = new(
                $"storage:seven-day-survival:{population}",
                Enumerable.Range(0, storageCells)
                    .Select(offset => new Vector2Int(columns - 1 - offset, 0)));
            OverflowRequirement overflow = new(
                $"overflow:single-fault-burst:{population}",
                Enumerable.Range(0, overflowCells)
                    .Select(offset => new Vector2Int(columns - 1 - offset, 2)));

            return new PopulationStagePortfolio(
                population,
                $"capacity-stage:{tier}",
                usable,
                new[] { new Vector2Int(1, 1) },
                new[] { new Vector2Int(0, 1) },
                facilities,
                new[] { stock },
                new[] { overflow },
                CreateContinuityRequirements(),
                minimumHeadroomPermille: 300);
        }

        private static FacilityRequirement[] LayoutModules(
            IReadOnlyList<SpatialModuleSpec> modules,
            int columns,
            int storageCells,
            int overflowCells)
        {
            int[] cursors = { 2, 3, 2 };
            int[] reservedStarts =
            {
                columns - storageCells,
                columns,
                columns - overflowCells
            };
            List<FacilityRequirement> requirements = new(modules.Count);
            for (int index = 0; index < modules.Count; index++)
            {
                SpatialModuleSpec module = modules[index];
                int row = Enumerable.Range(0, 3)
                    .Where(candidateRow =>
                    {
                        int candidateEnd = cursors[candidateRow]
                            + module.Width - 1;
                        return candidateEnd + 1 < reservedStarts[candidateRow];
                    })
                    .OrderBy(candidateRow => cursors[candidateRow])
                    .ThenBy(candidateRow => candidateRow)
                    .DefaultIfEmpty(-1)
                    .First();
                if (row < 0)
                {
                    throw new InvalidOperationException(
                        $"DUNGEON_CAPACITY_MODEL_INVALID: {module.Id} cannot fit "
                        + $"inside the authored {columns}-column tier portfolio.");
                }
                int start = cursors[row];
                int end = checked(start + module.Width - 1);
                Vector2Int[] footprint = Enumerable.Range(start, module.Width)
                    .Select(x => new Vector2Int(x, row))
                    .ToArray();
                FacilityPlacementCandidate placement = new(
                    module.Id + ":canonical",
                    footprint,
                    new[]
                    {
                        new Vector2Int(start - 1, row),
                        new Vector2Int(end + 1, row)
                    },
                    Array.Empty<Vector2Int>(),
                    module.ExpectedVisitsPerDay,
                    module.AverageOccupancyMilliSeconds,
                    module.FaultVisitMultiplierPermille);
                requirements.Add(new FacilityRequirement(
                    module.Id,
                    new[] { placement }));
                cursors[row] = end + 2;
            }
            return requirements.ToArray();
        }

        private static List<SpatialModuleSpec> CreateModules(int population)
        {
            List<SpatialModuleSpec> modules = new();
            Add(modules, "facility:sleep", population, population, 4000, 1100);
            Add(modules, "facility:food", population switch
            {
                <= 6 => 2,
                <= 18 => 3,
                _ => 4
            }, population * 3, 1800, 1400);
            Add(modules, "facility:water", population switch
            {
                <= 6 => 1,
                <= 12 => 2,
                _ => 3
            }, population * 3, 900, 1500);
            Add(modules, "facility:hygiene", population switch
            {
                <= 6 => 1,
                <= 18 => 2,
                _ => 3
            }, population, 1200, 1400);
            Add(modules, "facility:excretion", population switch
            {
                <= 6 => 1,
                <= 18 => 2,
                _ => 3
            }, population, 1200, 1400);
            Add(modules, "facility:research", population <= 3 ? 1 : population <= 12 ? 2 : 3,
                Math.Max(1, population / 3), 2400, 1200);
            Add(modules, "facility:craft", population <= 3 ? 1 : population <= 12 ? 3 : 4,
                Math.Max(1, population), 1800, 1300);
            Add(modules, "facility:medical", population <= 3 ? 1 : population <= 6 ? 2 : population <= 12 ? 3 : 4,
                Math.Max(1, population / 2), 1800, 1600);
            if (population >= 6)
            {
                Add(modules, "facility:logistics", population <= 6 ? 2 : population <= 18 ? 3 : 4,
                    population * 4, 700, 1600);
                Add(modules, "facility:guard", population <= 6 ? 2 : population <= 18 ? 3 : 4,
                    population, 1300, 1700);
            }
            if (population >= 12)
            {
                Add(modules, "facility:power", population <= 18 ? 2 : 3,
                    population, 900, 1500);
                Add(modules, "facility:sanitation", population <= 18 ? 2 : 3,
                    population * 2, 900, 1600);
                Add(modules, "facility:dining", population <= 12 ? 3 : 4,
                    population * 3, 1300, 1500);
            }
            if (population >= 18)
            {
                Add(modules, "facility:surgery", 3, Math.Max(1, population / 3), 2200, 1700);
                Add(modules, "facility:industry", population <= 18 ? 3 : 4,
                    population * 2, 1700, 1500);
                Add(modules, "facility:maintenance", 2,
                    population, 1200, 1700);
                Add(modules, "facility:quarters", population <= 18 ? 2 : 3,
                    population, 1500, 1400);
            }
            if (population >= 24)
            {
                Add(modules, "facility:captivity", 3, 12, 1600, 1600);
                Add(modules, "facility:hospitality", 3, 18, 1600, 1500);
                Add(modules, "facility:defense", 3, 24, 1400, 1800);
            }
            return modules;
        }

        private static void Add(
            ICollection<SpatialModuleSpec> modules,
            string id,
            int width,
            int visits,
            int occupancy,
            int faultMultiplier) =>
            modules.Add(new SpatialModuleSpec(
                id,
                width,
                visits,
                occupancy,
                faultMultiplier));

        private static ServiceContinuityRequirement[]
            CreateContinuityRequirements() => new[]
        {
            new ServiceContinuityRequirement(
                "service:food", "facility:meal-service", "survival:field-meal", 24),
            new ServiceContinuityRequirement(
                "service:water", "facility:safe-drink", "survival:safe-drink", 24),
            new ServiceContinuityRequirement(
                "service:sleep", "facility:bed", "survival:floor-rest", 24),
            new ServiceContinuityRequirement(
                "service:hygiene", "facility:hygiene", "survival:bucket-wash", 24),
            new ServiceContinuityRequirement(
                "service:excretion", "facility:toilet", "survival:primitive-latrine", 24)
        };

        private readonly struct SpatialModuleSpec
        {
            public SpatialModuleSpec(
                string id,
                int width,
                int expectedVisitsPerDay,
                int averageOccupancyMilliSeconds,
                int faultVisitMultiplierPermille)
            {
                Id = FacilityPlacementCandidate.RequireId(id, nameof(id));
                if (width <= 0)
                    throw new ArgumentOutOfRangeException(nameof(width));
                Width = width;
                ExpectedVisitsPerDay = expectedVisitsPerDay;
                AverageOccupancyMilliSeconds = averageOccupancyMilliSeconds;
                FaultVisitMultiplierPermille = faultVisitMultiplierPermille;
            }

            public string Id { get; }
            public int Width { get; }
            public int ExpectedVisitsPerDay { get; }
            public int AverageOccupancyMilliSeconds { get; }
            public int FaultVisitMultiplierPermille { get; }
        }
    }

    /// <summary>
    /// Source-controlled projection of the worst successful actual-BuildingSO
    /// placement at each population stage.  The asset-backed 256-order audit
    /// must reproduce these counts before PlayMode may use them as the static
    /// side of the runtime-headroom calculation.  Dynamic actors, queues and
    /// unmanaged physical stock are charged separately by the PlayMode probe.
    /// </summary>
    public static class V27PopulationStageSpatialBaseline
    {
        // Production-live topology verification currently proves that all
        // authored world-resource nodes remain on exterior cells at every
        // supported research width. Any future interior node makes the live
        // verifier fail until this capacity projection and the asset solver are
        // deliberately updated together.
        public static int FixedWorldFeatureCells(int population) => population switch
        {
            1 or 3 or 6 or 12 or 18 or 24 => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(population))
        };

        public static int UsableCells(int population) => checked(
            PopulationStagePortfolioCatalog.InteriorColumnsForPopulation(population)
            * 3);

        public static int PlannedUsedCells(int population) => population switch
        {
            1 => 21,
            3 => 37,
            6 => 56,
            12 => 101,
            18 => 135,
            24 => 166,
            _ => throw new ArgumentOutOfRangeException(nameof(population))
        };

        public static int RuntimeHeadroomPermille(
            int population,
            int dynamicErosionCells)
        {
            if (dynamicErosionCells < 0)
                throw new ArgumentOutOfRangeException(nameof(dynamicErosionCells));
            int usable = UsableCells(population);
            int free = usable - PlannedUsedCells(population)
                - FixedWorldFeatureCells(population)
                - dynamicErosionCells;
            return checked(Math.Max(0, free) * 1000 / usable);
        }
    }

    [BalanceImmutableRecord]
    public sealed class DungeonSpaceCapacityAssessment
    {
        internal DungeonSpaceCapacityAssessment(
            bool succeeded,
            string failureCode,
            int usableCells,
            int effectiveUsedCells,
            int accessOverlapSavings,
            int headroomPermille,
            int peakNormalCellUtilizationPermille,
            int peakFaultCellUtilizationPermille,
            IReadOnlyList<FacilityPlacementCandidate> placements)
        {
            Succeeded = succeeded;
            FailureCode = failureCode ?? string.Empty;
            UsableCells = usableCells;
            EffectiveUsedCells = effectiveUsedCells;
            AccessOverlapSavings = accessOverlapSavings;
            HeadroomPermille = headroomPermille;
            PeakNormalCellUtilizationPermille = peakNormalCellUtilizationPermille;
            PeakFaultCellUtilizationPermille = peakFaultCellUtilizationPermille;
            Placements = placements ?? Array.Empty<FacilityPlacementCandidate>();
        }

        public bool Succeeded { get; }
        public string FailureCode { get; }
        public int UsableCells { get; }
        public int EffectiveUsedCells { get; }
        public int AccessOverlapSavings { get; }
        public int HeadroomPermille { get; }
        public int PeakNormalCellUtilizationPermille { get; }
        public int PeakFaultCellUtilizationPermille { get; }
        public IReadOnlyList<FacilityPlacementCandidate> Placements { get; }
    }

    public interface IDungeonSpaceCapacityQuery
    {
        DungeonSpaceCapacityAssessment Assess(
            PopulationStagePortfolio portfolio,
            int deterministicSeed);
    }

    [BalanceCaptureFactory]
    public sealed class DeterministicDungeonSpaceCapacityQuery :
        IDungeonSpaceCapacityQuery
    {
        private const long GameDayMilliSeconds = 180000L;
        private DungeonSpaceCapacityAssessment best;

        public DungeonSpaceCapacityAssessment Assess(
            PopulationStagePortfolio portfolio,
            int deterministicSeed)
        {
            if (portfolio == null)
                throw new ArgumentNullException(nameof(portfolio));
            if (deterministicSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(deterministicSeed));

            HashSet<Vector2Int> usable = new(portfolio.UsableInteriorCells);
            HashSet<Vector2Int> forbidden = new(portfolio.EmergencyEgressCells);
            forbidden.UnionWith(portfolio.FixedWorldFeatureCells);
            FacilityRequirement[] ordered = portfolio.Facilities.ToArray();
            DeterministicRandomSequence orderRandom = new(deterministicSeed);
            for (int index = ordered.Length - 1; index > 0; index--)
            {
                int swap = orderRandom.NextInt(0, index + 1);
                (ordered[index], ordered[swap]) = (ordered[swap], ordered[index]);
            }

            best = null;
            Search(0, ordered, new List<FacilityPlacementCandidate>(),
                new HashSet<Vector2Int>(), new HashSet<Vector2Int>(), usable, forbidden, portfolio);
            return best ?? new DungeonSpaceCapacityAssessment(
                false,
                "DUNGEON_CAPACITY_MODEL_INVALID",
                usable.Count,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<FacilityPlacementCandidate>());
        }

        private void Search(
            int index,
            IReadOnlyList<FacilityRequirement> requirements,
            List<FacilityPlacementCandidate> selected,
            HashSet<Vector2Int> exclusive,
            HashSet<Vector2Int> sharedAccess,
            HashSet<Vector2Int> usable,
            HashSet<Vector2Int> forbidden,
            PopulationStagePortfolio portfolio)
        {
            if (index == requirements.Count)
            {
                Evaluate(selected, exclusive, sharedAccess, usable, forbidden, portfolio);
                return;
            }

            FacilityRequirement requirement = requirements[index];
            foreach (FacilityPlacementCandidate candidate in requirement.Candidates)
            {
                if (!CanPlace(candidate, selected, exclusive, sharedAccess, usable, forbidden))
                    continue;
                HashSet<Vector2Int> nextExclusive = new(exclusive);
                nextExclusive.UnionWith(candidate.ExclusiveFootprint);
                HashSet<Vector2Int> nextAccess = new(sharedAccess);
                nextAccess.UnionWith(candidate.OperationalAccess);
                nextAccess.UnionWith(candidate.QueueAccess);
                selected.Add(candidate);
                Search(index + 1, requirements, selected, nextExclusive, nextAccess,
                    usable, forbidden, portfolio);
                selected.RemoveAt(selected.Count - 1);
            }
        }

        private static bool CanPlace(
            FacilityPlacementCandidate candidate,
            IReadOnlyList<FacilityPlacementCandidate> selected,
            HashSet<Vector2Int> exclusive,
            HashSet<Vector2Int> sharedAccess,
            HashSet<Vector2Int> usable,
            HashSet<Vector2Int> forbidden)
        {
            foreach (Vector2Int cell in candidate.ExclusiveFootprint)
            {
                if (!usable.Contains(cell) || forbidden.Contains(cell)
                    || exclusive.Contains(cell) || sharedAccess.Contains(cell))
                    return false;
            }
            foreach (Vector2Int cell in candidate.OperationalAccess.Concat(candidate.QueueAccess))
            {
                if (!usable.Contains(cell) || forbidden.Contains(cell) || exclusive.Contains(cell)
                    || candidate.ExclusiveFootprint.Contains(cell))
                    return false;
            }
            Vector2Int[] candidateAccess = candidate.OperationalAccess
                .Concat(candidate.QueueAccess)
                .Distinct()
                .ToArray();
            foreach (FacilityPlacementCandidate existing in selected)
            {
                Vector2Int[] existingAccess = existing.OperationalAccess
                    .Concat(existing.QueueAccess)
                    .Distinct()
                    .ToArray();
                if (!BuildingWorkAccessRules.CanShareOperationalAccess(
                        existingAccess,
                        candidateAccess))
                    return false;
            }
            return true;
        }

        private void Evaluate(
            IReadOnlyList<FacilityPlacementCandidate> selected,
            HashSet<Vector2Int> exclusive,
            HashSet<Vector2Int> sharedAccess,
            HashSet<Vector2Int> usable,
            HashSet<Vector2Int> forbidden,
            PopulationStagePortfolio portfolio)
        {
            HashSet<Vector2Int> used = new(exclusive);
            used.UnionWith(sharedAccess);
            used.UnionWith(portfolio.FixedWorldFeatureCells);
            int rawAccess = 0;
            Dictionary<Vector2Int, long> normalOccupancy = new();
            Dictionary<Vector2Int, long> faultOccupancy = new();
            foreach (FacilityPlacementCandidate placement in selected)
            {
                rawAccess += placement.OperationalAccess.Count + placement.QueueAccess.Count;
                Vector2Int[] accessCells = placement.OperationalAccess
                    .Concat(placement.QueueAccess)
                    .Distinct()
                    .ToArray();
                long totalNormal = checked(
                    (long)placement.ExpectedVisitsPerDay
                    * placement.AverageOccupancyMilliSeconds);
                long normalPerCell = DivideCeiling(
                    totalNormal,
                    accessCells.Length);
                long faultPerCell = DivideCeiling(
                    checked(totalNormal
                        * placement.FaultVisitMultiplierPermille),
                    checked(accessCells.Length * 1000L));
                foreach (Vector2Int cell in accessCells)
                {
                    Add(normalOccupancy, cell, normalPerCell);
                    Add(faultOccupancy, cell, faultPerCell);
                }
            }
            foreach (StockSpaceRequirement buffer in portfolio.StockBuffers)
            {
                if (buffer.Cells.Any(cell => !usable.Contains(cell)
                    || forbidden.Contains(cell)
                    || exclusive.Contains(cell)
                    || sharedAccess.Contains(cell)
                    || used.Contains(cell)))
                    return;
                used.UnionWith(buffer.Cells);
            }
            foreach (OverflowRequirement overflow in portfolio.OverflowBuffers)
            {
                if (overflow.Cells.Any(cell => !usable.Contains(cell)
                    || forbidden.Contains(cell)
                    || exclusive.Contains(cell)
                    || sharedAccess.Contains(cell)
                    || used.Contains(cell)))
                    return;
                used.UnionWith(overflow.Cells);
            }

            int headroom = (int)((long)(usable.Count - used.Count) * 1000L / usable.Count);
            int normalPeak = PeakPermille(normalOccupancy);
            int faultPeak = PeakPermille(faultOccupancy);
            if (headroom < portfolio.MinimumHeadroomPermille
                || normalPeak > 700
                || faultPeak > 900)
                return;

            FacilityPlacementCandidate[] placements = selected
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ToArray();
            DungeonSpaceCapacityAssessment candidate = new(
                true,
                string.Empty,
                usable.Count,
                used.Count,
                rawAccess - sharedAccess.Count,
                headroom,
                normalPeak,
                faultPeak,
                placements);
            if (best == null
                || candidate.HeadroomPermille > best.HeadroomPermille
                || (candidate.HeadroomPermille == best.HeadroomPermille
                    && candidate.PeakFaultCellUtilizationPermille
                    < best.PeakFaultCellUtilizationPermille)
                || (candidate.HeadroomPermille == best.HeadroomPermille
                    && candidate.PeakFaultCellUtilizationPermille
                    == best.PeakFaultCellUtilizationPermille
                    && ComparePlacements(candidate.Placements, best.Placements) < 0))
                best = candidate;
        }

        private static void Add(Dictionary<Vector2Int, long> values, Vector2Int cell, long amount)
        {
            values[cell] = checked((values.TryGetValue(cell, out long current) ? current : 0L) + amount);
        }

        private static long DivideCeiling(long numerator, long denominator)
        {
            if (numerator < 0 || denominator <= 0)
                throw new ArgumentOutOfRangeException(nameof(denominator));
            return checked((numerator + denominator - 1L) / denominator);
        }

        private static int PeakPermille(Dictionary<Vector2Int, long> values) =>
            values.Count == 0 ? 0 : (int)values.Values.Max(value => value * 1000L / GameDayMilliSeconds);

        private static int ComparePlacements(
            IReadOnlyList<FacilityPlacementCandidate> left,
            IReadOnlyList<FacilityPlacementCandidate> right)
        {
            int count = Math.Min(left.Count, right.Count);
            for (int index = 0; index < count; index++)
            {
                int compare = string.CompareOrdinal(left[index].StableId, right[index].StableId);
                if (compare != 0)
                    return compare;
            }
            return left.Count.CompareTo(right.Count);
        }
    }

    public sealed class DungeonSpaceLayoutSnapshot
    {
        private readonly IReadOnlyDictionary<Vector2Int, SpatialCellRole> rolesByCell;
        private readonly HashSet<Vector2Int> criticalAccessCells;

        public DungeonSpaceLayoutSnapshot(
            IEnumerable<KeyValuePair<Vector2Int, SpatialCellRole>> roles,
            IEnumerable<Vector2Int> criticalAccessCells,
            float cleanRunP95HaulDispatchAndDeliverySeconds,
            float gameDaySeconds = 180f)
        {
            if (cleanRunP95HaulDispatchAndDeliverySeconds < 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(cleanRunP95HaulDispatchAndDeliverySeconds));
            if (gameDaySeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(gameDaySeconds));
            Dictionary<Vector2Int, SpatialCellRole> captured = new();
            foreach (KeyValuePair<Vector2Int, SpatialCellRole> pair in
                     roles ?? Array.Empty<KeyValuePair<Vector2Int, SpatialCellRole>>())
            {
                if (pair.Value == SpatialCellRole.None)
                    continue;
                captured[pair.Key] = captured.TryGetValue(pair.Key, out SpatialCellRole current)
                    ? current | pair.Value
                    : pair.Value;
            }
            rolesByCell = captured;
            this.criticalAccessCells = new HashSet<Vector2Int>(
                criticalAccessCells ?? Array.Empty<Vector2Int>());
            CleanRunP95HaulDispatchAndDeliverySeconds =
                cleanRunP95HaulDispatchAndDeliverySeconds;
            GameDaySeconds = gameDaySeconds;
        }

        public float CleanRunP95HaulDispatchAndDeliverySeconds { get; }
        public float GameDaySeconds { get; }
        public SpatialCellRole GetRoles(Vector2Int cell) =>
            rolesByCell.TryGetValue(cell, out SpatialCellRole roles)
                ? roles
                : SpatialCellRole.None;
        public bool IsCriticalAccess(Vector2Int cell) =>
            criticalAccessCells.Contains(cell);
    }

    public sealed class FloorClutterStackAssessment
    {
        public FloorClutterStackAssessment(
            string stackId,
            Vector2Int position,
            int quantity,
            float ageSeconds,
            SpatialCellRole roles,
            bool immediateFailure,
            bool persistent,
            WorldItemDropDisposition dropDisposition = WorldItemDropDisposition.None,
            string recoveryOwnerOperationId = "",
            string recoveryCarrierPersistentId = "",
            WorldItemCarryInterruptionKind recoveryInterruptionKind =
                WorldItemCarryInterruptionKind.None,
            float recoveryDeadlineGameTime = 0f)
        {
            StackId = stackId ?? string.Empty;
            Position = position;
            Quantity = quantity;
            AgeSeconds = ageSeconds;
            Roles = roles;
            ImmediateFailure = immediateFailure;
            Persistent = persistent;
            DropDisposition = dropDisposition;
            RecoveryOwnerOperationId = recoveryOwnerOperationId ?? string.Empty;
            RecoveryCarrierPersistentId = recoveryCarrierPersistentId ?? string.Empty;
            RecoveryInterruptionKind = recoveryInterruptionKind;
            RecoveryDeadlineGameTime = recoveryDeadlineGameTime;
        }

        public string StackId { get; }
        public Vector2Int Position { get; }
        public int Quantity { get; }
        public float AgeSeconds { get; }
        public SpatialCellRole Roles { get; }
        public bool ImmediateFailure { get; }
        public bool Persistent { get; }
        public WorldItemDropDisposition DropDisposition { get; }
        public string RecoveryOwnerOperationId { get; }
        public string RecoveryCarrierPersistentId { get; }
        public WorldItemCarryInterruptionKind RecoveryInterruptionKind { get; }
        public float RecoveryDeadlineGameTime { get; }
    }

    public sealed class FloorClutterAssessment
    {
        public FloorClutterAssessment(
            float graceSeconds,
            int looseStackCount,
            int looseQuantity,
            IReadOnlyList<FloorClutterStackAssessment> outsideContainment)
        {
            GraceSeconds = graceSeconds;
            LooseStackCount = looseStackCount;
            LooseQuantity = looseQuantity;
            OutsideContainment = outsideContainment
                ?? Array.Empty<FloorClutterStackAssessment>();
        }

        public float GraceSeconds { get; }
        public int LooseStackCount { get; }
        public int LooseQuantity { get; }
        public IReadOnlyList<FloorClutterStackAssessment> OutsideContainment { get; }
        public int PersistentCount => OutsideContainment.Count(value => value.Persistent);
        public int ImmediateFailureCount => OutsideContainment.Count(value => value.ImmediateFailure);
        public bool Passed => PersistentCount == 0 && ImmediateFailureCount == 0;
    }

    public interface IFloorClutterDiagnosticsQuery
    {
        FloorClutterAssessment Capture(
            Grid grid,
            DungeonSpaceLayoutSnapshot layout,
            float currentGameTime);
    }

    public sealed class PairedRunWindowResult
    {
        public PairedRunWindowResult(
            int seed,
            string arm,
            int windowIndex,
            long travelMilliWu,
            long waitMilliWu,
            int replanCount,
            int stepAsideCount,
            int clutterCellSeconds,
            string semanticStateHash,
            string randomStateHash,
            string exogenousEventHash,
            long dispatchWaitMilliWu = 0L,
            long reservationWaitMilliWu = 0L,
            long facilityAccessWaitMilliWu = 0L,
            long noPathMilliWu = 0L,
            int burstDeliveredQuantity = 0,
            int burstOutstandingQuantity = 0,
            bool burstQuantityConserved = true)
        {
            if (seed == 0)
                throw new ArgumentOutOfRangeException(nameof(seed));
            if (windowIndex < 0 || windowIndex > 3)
                throw new ArgumentOutOfRangeException(nameof(windowIndex));
            if (travelMilliWu < 0L || waitMilliWu < 0L
                || dispatchWaitMilliWu < 0L
                || reservationWaitMilliWu < 0L
                || facilityAccessWaitMilliWu < 0L
                || noPathMilliWu < 0L
                || burstDeliveredQuantity < 0
                || burstOutstandingQuantity < 0
                || replanCount < 0 || stepAsideCount < 0 || clutterCellSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(waitMilliWu));
            long classifiedWait = checked(dispatchWaitMilliWu
                + reservationWaitMilliWu
                + facilityAccessWaitMilliWu
                + noPathMilliWu);
            if (classifiedWait != waitMilliWu)
                throw new ArgumentException(
                    "Classified wait must equal the union wait total.",
                    nameof(waitMilliWu));
            Seed = seed;
            Arm = FacilityPlacementCandidate.RequireId(arm, nameof(arm));
            WindowIndex = windowIndex;
            TravelMilliWu = travelMilliWu;
            WaitMilliWu = waitMilliWu;
            ReplanCount = replanCount;
            StepAsideCount = stepAsideCount;
            ClutterCellSeconds = clutterCellSeconds;
            SemanticStateHash = FacilityPlacementCandidate.RequireId(
                semanticStateHash, nameof(semanticStateHash));
            RandomStateHash = FacilityPlacementCandidate.RequireId(
                randomStateHash, nameof(randomStateHash));
            ExogenousEventHash = FacilityPlacementCandidate.RequireId(
                exogenousEventHash, nameof(exogenousEventHash));
            DispatchWaitMilliWu = dispatchWaitMilliWu;
            ReservationWaitMilliWu = reservationWaitMilliWu;
            FacilityAccessWaitMilliWu = facilityAccessWaitMilliWu;
            NoPathMilliWu = noPathMilliWu;
            BurstDeliveredQuantity = burstDeliveredQuantity;
            BurstOutstandingQuantity = burstOutstandingQuantity;
            BurstQuantityConserved = burstQuantityConserved;
        }

        public int Seed { get; }
        public string Arm { get; }
        public int WindowIndex { get; }
        public long TravelMilliWu { get; }
        public long WaitMilliWu { get; }
        public int ReplanCount { get; }
        public int StepAsideCount { get; }
        public int ClutterCellSeconds { get; }
        public string SemanticStateHash { get; }
        public string RandomStateHash { get; }
        public string ExogenousEventHash { get; }
        public long DispatchWaitMilliWu { get; }
        public long ReservationWaitMilliWu { get; }
        public long FacilityAccessWaitMilliWu { get; }
        public long NoPathMilliWu { get; }
        public int BurstDeliveredQuantity { get; }
        public int BurstOutstandingQuantity { get; }
        public bool BurstQuantityConserved { get; }
    }

    public sealed class PairedRunAttributionAssessment
    {
        internal PairedRunAttributionAssessment(
            bool passed,
            string failureCode,
            int sampleCount,
            int medianClutterDeltaPermille,
            int p95ClutterDeltaPermille,
            int maximumClutterDeltaPermille,
            int madPermille,
            bool requiresExpandedSample,
            IReadOnlyList<int> seedDeltasPermille)
        {
            Passed = passed;
            FailureCode = failureCode ?? string.Empty;
            SampleCount = sampleCount;
            MedianClutterDeltaPermille = medianClutterDeltaPermille;
            P95ClutterDeltaPermille = p95ClutterDeltaPermille;
            MaximumClutterDeltaPermille = maximumClutterDeltaPermille;
            MadPermille = madPermille;
            RequiresExpandedSample = requiresExpandedSample;
            SeedDeltasPermille = seedDeltasPermille ?? Array.Empty<int>();
        }

        public bool Passed { get; }
        public string FailureCode { get; }
        public int SampleCount { get; }
        public int MedianClutterDeltaPermille { get; }
        public int P95ClutterDeltaPermille { get; }
        public int MaximumClutterDeltaPermille { get; }
        public int MadPermille { get; }
        public bool RequiresExpandedSample { get; }
        public IReadOnlyList<int> SeedDeltasPermille { get; }
    }

    [BalanceCaptureFactory]
    public static class PairedRunAttributionEvaluator
    {
        private static readonly string[] RequiredArms =
        {
            "cleanRepeatA",
            "cleanRepeatB",
            "faultControl",
            "clutterStress"
        };

        public static PairedRunAttributionAssessment Evaluate(
            IEnumerable<PairedRunWindowResult> source)
        {
            PairedRunWindowResult[] rows = (source
                    ?? throw new ArgumentNullException(nameof(source)))
                .OrderBy(value => value.Seed)
                .ThenBy(value => value.Arm, StringComparer.Ordinal)
                .ThenBy(value => value.WindowIndex)
                .ToArray();
            int[] seeds = rows.Select(value => value.Seed).Distinct().ToArray();
            if (seeds.Length < 32)
                return Fail("PAIRED_RUN_STATISTICAL_POWER_INSUFFICIENT", seeds.Length);
            List<int> deltas = new(seeds.Length);
            foreach (int seed in seeds)
            {
                PairedRunWindowResult[] seedRows = rows
                    .Where(value => value.Seed == seed)
                    .ToArray();
                foreach (string arm in RequiredArms)
                {
                    if (seedRows.Count(value => value.Arm == arm) != 4)
                        return Fail("PAIRED_RUN_WINDOW_SET_INVALID", seeds.Length);
                }
                for (int window = 0; window < 4; window++)
                {
                    PairedRunWindowResult cleanA = Find(seedRows, "cleanRepeatA", window);
                    PairedRunWindowResult cleanB = Find(seedRows, "cleanRepeatB", window);
                    if (!EquivalentClean(cleanA, cleanB))
                        return Fail("PAIRED_RUN_NONDETERMINISTIC_BASELINE", seeds.Length);
                    PairedRunWindowResult control = Find(seedRows, "faultControl", window);
                    PairedRunWindowResult clutter = Find(seedRows, "clutterStress", window);
                    if (!string.Equals(
                            control.ExogenousEventHash,
                            clutter.ExogenousEventHash,
                            StringComparison.Ordinal))
                        return Fail("PAIRED_RUN_EXOGENOUS_EVENT_DIVERGENCE", seeds.Length);
                    if (!control.BurstQuantityConserved
                        || !clutter.BurstQuantityConserved)
                        return Fail("PAIRED_RUN_BURST_CONSERVATION_FAILED", seeds.Length);
                }
                long controlWait = seedRows
                    .Where(value => value.Arm == "faultControl")
                    .Sum(value => value.WaitMilliWu);
                long clutterWait = seedRows
                    .Where(value => value.Arm == "clutterStress")
                    .Sum(value => value.WaitMilliWu);
                long delta = checked(clutterWait - controlWait);
                long denominator = Math.Max(controlWait, 1L);
                deltas.Add(checked((int)(delta * 1000L / denominator)));
            }

            deltas.Sort();
            int median = Percentile(deltas, 500);
            int p95 = Percentile(deltas, 950);
            int maximum = deltas[deltas.Count - 1];
            int[] deviations = deltas.Select(value => Math.Abs(value - median))
                .OrderBy(value => value)
                .ToArray();
            int mad = Percentile(deviations, 500);
            bool boundary = (median >= 80 && median <= 120)
                || (p95 >= 80 && p95 <= 120);
            bool requiresExpanded = seeds.Length == 32 && boundary;
            if (requiresExpanded)
                return new PairedRunAttributionAssessment(
                    false,
                    "PAIRED_RUN_EXPANDED_SAMPLE_REQUIRED",
                    seeds.Length,
                    median,
                    p95,
                    maximum,
                    mad,
                    true,
                    deltas);
            bool passed = median < 100 && p95 < 100;
            return new PairedRunAttributionAssessment(
                passed,
                passed ? string.Empty : "PAIRED_RUN_CLUTTER_WAIT_WU_EXCEEDED",
                seeds.Length,
                median,
                p95,
                maximum,
                mad,
                false,
                deltas);
        }

        private static bool EquivalentClean(
            PairedRunWindowResult left,
            PairedRunWindowResult right) =>
            left.TravelMilliWu == right.TravelMilliWu
            && left.WaitMilliWu == right.WaitMilliWu
            && left.DispatchWaitMilliWu == right.DispatchWaitMilliWu
            && left.ReservationWaitMilliWu == right.ReservationWaitMilliWu
            && left.FacilityAccessWaitMilliWu == right.FacilityAccessWaitMilliWu
            && left.NoPathMilliWu == right.NoPathMilliWu
            && left.BurstDeliveredQuantity == right.BurstDeliveredQuantity
            && left.BurstOutstandingQuantity == right.BurstOutstandingQuantity
            && left.BurstQuantityConserved == right.BurstQuantityConserved
            && left.ReplanCount == right.ReplanCount
            && left.StepAsideCount == right.StepAsideCount
            && left.ClutterCellSeconds == right.ClutterCellSeconds
            && string.Equals(left.SemanticStateHash, right.SemanticStateHash, StringComparison.Ordinal)
            && string.Equals(left.RandomStateHash, right.RandomStateHash, StringComparison.Ordinal)
            && string.Equals(left.ExogenousEventHash, right.ExogenousEventHash, StringComparison.Ordinal);

        private static PairedRunWindowResult Find(
            IEnumerable<PairedRunWindowResult> rows,
            string arm,
            int window) => rows.Single(value =>
                value.Arm == arm && value.WindowIndex == window);

        private static int Percentile(IReadOnlyList<int> values, int permille)
        {
            int index = Math.Max(0, Math.Min(values.Count - 1,
                (int)Math.Ceiling(values.Count * permille / 1000m) - 1));
            return values[index];
        }

        private static PairedRunAttributionAssessment Fail(
            string code,
            int sampleCount) => new(
            false, code, sampleCount, 0, 0, 0, 0, false, Array.Empty<int>());
    }
}
