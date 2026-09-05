#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Balance;

internal enum V27ConstructionRedistributionDisposition
{
    Normal,
    WarningMaterialDominated,
    WarningDensity,
    WarningCapitalProtected,
    CriticalDensityUnresolved
}

internal sealed class V27ConstructionRedistributionResult
{
    public string StableId { get; set; }
    public decimal BeforeWu { get; set; }
    public decimal PeriodCandidateWu { get; set; }
    public decimal AfterWu { get; set; }
    public long BeforeBomMilliEwu { get; set; }
    public long AfterBomMilliEwu { get; set; }
    public long TargetInvestmentMilliEwu { get; set; }
    public long InvestmentErrorMilliEwu { get; set; }
    public decimal DensityRatio { get; set; }
    public decimal AfterLaborShare { get; set; }
    public V27ConstructionRedistributionDisposition Disposition { get; set; }
    public IReadOnlyList<ItemAmountDefinition> BeforeMaterials { get; set; }
    public IReadOnlyList<ItemAmountDefinition> AfterMaterials { get; set; }

    public string SelectionReason => Disposition switch
    {
        V27ConstructionRedistributionDisposition.Normal =>
            "bounded integer redistribution; density ratio 0.80-1.25; total investment error <=2%",
        V27ConstructionRedistributionDisposition.WarningMaterialDominated =>
            "period preserved without artificial BOM inflation because material share is already >=60%",
        V27ConstructionRedistributionDisposition.WarningDensity =>
            "bounded integer redistribution; warning density ratio 0.67-1.50; total investment error <=2%",
        V27ConstructionRedistributionDisposition.WarningCapitalProtected =>
            "one-cell primitive infrastructure keeps its authored BOM to protect initial capital",
        V27ConstructionRedistributionDisposition.CriticalDensityUnresolved =>
            "best bounded candidate still exceeds the labor-density review band; explicit review required and automatic application forbidden",
        _ => throw new ArgumentOutOfRangeException()
    };
}

/// <summary>
/// Selects the V27 construction WU/BOM pair without introducing a second
/// gameplay authority. This class is Editor-only: it authors the approved
/// scalar and physical material quantities that the runtime later consumes.
/// </summary>
internal static class V27ConstructionRedistributionPolicy
{
    private const decimal MinimumWuMultiplier = 1.5m;
    private const decimal MaximumWuMultiplier = 2.25m;
    private const decimal NormalDensityMinimum = 0.80m;
    private const decimal NormalDensityMaximum = 1.25m;
    private const decimal WarningDensityMinimum = 0.67m;
    private const decimal WarningDensityMaximum = 1.50m;
    private const decimal MaterialDominatedLaborShareMaximum = 0.40m;
    private const long InvestmentTolerancePermille = 20L;

    public static V27ConstructionRedistributionResult Select(
        string stableId,
        BuildingSO building,
        decimal beforeWu,
        decimal beforeBomEwu,
        IReadOnlyList<ItemAmountDefinition> beforeMaterials,
        IReadOnlyDictionary<string, V27ItemValue> itemValues,
        IReadOnlyList<ItemAmountDefinition> currentApprovedMaterials = null,
        decimal? currentApprovedWu = null)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            throw new ArgumentException("Construction redistribution requires a stable ID.", nameof(stableId));
        if (building == null)
            throw new ArgumentNullException(nameof(building));
        if (beforeWu <= 0m || beforeBomEwu <= 0m)
            throw new ArgumentOutOfRangeException(nameof(beforeWu));
        if (beforeMaterials == null || beforeMaterials.Count == 0)
            throw new ArgumentException("Construction redistribution requires a physical BOM.", nameof(beforeMaterials));
        if (itemValues == null)
            throw new ArgumentNullException(nameof(itemValues));

        Dictionary<string, int> currentAmounts = (currentApprovedMaterials ?? beforeMaterials)
            .Where(value => value != null && value.Amount > 0)
            .ToDictionary(value => value.ItemId, value => value.Amount, StringComparer.Ordinal);
        MaterialOption[] options = beforeMaterials
            .Where(value => value != null && value.Amount > 0)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(value =>
            {
                if (!itemValues.TryGetValue(value.ItemId, out V27ItemValue item))
                {
                    throw new InvalidOperationException(
                        $"Construction redistribution item value is missing: {stableId}:{value.ItemId}.");
                }
                if (!currentAmounts.TryGetValue(value.ItemId, out int currentAmount))
                {
                    throw new InvalidOperationException(
                        $"Current approved construction BOM is missing an historical item: {stableId}:{value.ItemId}.");
                }
                int maximumAmount = checked((value.Amount * 3 + 1) / 2);
                if (currentAmount < value.Amount || currentAmount > maximumAmount)
                {
                    throw new InvalidOperationException(
                        $"Current approved construction BOM escaped historical bounds: {stableId}:{value.ItemId}; "
                        + $"before={value.Amount}; current={currentAmount}; maximum={maximumAmount}.");
                }
                return new MaterialOption(
                    value.ItemId,
                    value.Amount,
                    currentAmount,
                    maximumAmount,
                    item.AcquisitionCost.MilliEwu);
            })
            .ToArray();
        if (options.Length != beforeMaterials.Count)
            throw new InvalidOperationException($"Construction BOM contains invalid rows: {stableId}.");
        if (currentAmounts.Count != options.Length)
            throw new InvalidOperationException(
                $"Current approved construction BOM changed item kinds: {stableId}.");

        long baseBomMilli = options.Sum(value => checked(value.UnitMilliEwu * value.BeforeAmount));
        long periodWu = checked((long)decimal.Ceiling(beforeWu * MaximumWuMultiplier));
        long minimumWu = checked((long)decimal.Ceiling(beforeWu * MinimumWuMultiplier));
        long targetInvestment = checked(periodWu * 1000L + baseBomMilli);
        bool primitiveCapitalProtected =
            V23BalanceWorkCalculator.ResolveConstructionClass(building)
                == ConstructionBalanceClass.Structure
            && Math.Max(1, building.width) * Math.Max(1, building.height) == 1
            && options.Sum(value => value.BeforeAmount) <= 2;

        if (primitiveCapitalProtected)
        {
            return CreatePeriodResult(
                stableId,
                beforeWu,
                beforeBomEwu,
                options,
                periodWu,
                baseBomMilli,
                targetInvestment,
                V27ConstructionRedistributionDisposition.WarningCapitalProtected);
        }
        int[] amounts = options.Select(value => value.BeforeAmount).ToArray();
        Candidate best = null;
        Enumerate(0, baseBomMilli);
        if (best == null)
        {
            throw new InvalidOperationException(
                $"No bounded V27 construction redistribution exists: {stableId}; "
                + $"beforeWu={Token(beforeWu)}; beforeBom={Token(beforeBomEwu)}; "
                + $"periodWu={periodWu}; target={targetInvestment}mEWU.");
        }

        return new V27ConstructionRedistributionResult
        {
            StableId = stableId,
            BeforeWu = beforeWu,
            PeriodCandidateWu = periodWu,
            AfterWu = best.Wu,
            BeforeBomMilliEwu = baseBomMilli,
            AfterBomMilliEwu = best.BomMilliEwu,
            TargetInvestmentMilliEwu = targetInvestment,
            InvestmentErrorMilliEwu = best.InvestmentErrorMilliEwu,
            DensityRatio = best.DensityRatio,
            AfterLaborShare = best.AfterLaborShare,
            Disposition = best.Disposition,
            BeforeMaterials = Array.AsReadOnly(options
                .Select(value => new ItemAmountDefinition(value.ItemId, value.BeforeAmount))
                .ToArray()),
            AfterMaterials = Array.AsReadOnly(options
                .Select((value, index) => new ItemAmountDefinition(value.ItemId, best.Amounts[index]))
                .ToArray())
        };

        void Enumerate(int index, long bomMilli)
        {
            if (index < options.Length)
            {
                MaterialOption option = options[index];
                long withoutCurrent = checked(
                    bomMilli - option.UnitMilliEwu * option.BeforeAmount);
                for (int amount = option.BeforeAmount; amount <= option.MaximumAmount; amount++)
                {
                    amounts[index] = amount;
                    Enumerate(
                        index + 1,
                        checked(withoutCurrent + option.UnitMilliEwu * amount));
                }
                amounts[index] = option.BeforeAmount;
                return;
            }

            long idealWuMilli = checked(targetInvestment - bomMilli);
            long nearestWu = idealWuMilli >= 0L
                ? checked((idealWuMilli + 500L) / 1000L)
                : minimumWu;
            List<long> wuCandidates = new List<long>
            {
                minimumWu,
                periodWu,
                nearestWu - 1L,
                nearestWu,
                nearestWu + 1L
            };
            if (currentApprovedWu.HasValue)
            {
                decimal exactCurrent = currentApprovedWu.Value;
                if (exactCurrent <= 0m || exactCurrent != decimal.Truncate(exactCurrent))
                {
                    throw new InvalidOperationException(
                        $"Current approved construction WU must be a positive integer: {stableId}; current={Token(exactCurrent)}.");
                }
                wuCandidates.Add(checked((long)exactCurrent));
            }
            AddDensityBoundaryCandidates(WarningDensityMinimum);
            AddDensityBoundaryCandidates(NormalDensityMinimum);
            AddDensityBoundaryCandidates(1m);
            AddDensityBoundaryCandidates(NormalDensityMaximum);
            AddDensityBoundaryCandidates(WarningDensityMaximum);
            foreach (long rawWu in wuCandidates)
            {
                long wu = Math.Max(minimumWu, Math.Min(periodWu, rawWu));
                Evaluate(wu, bomMilli);
            }

            void AddDensityBoundaryCandidates(decimal densityRatio)
            {
                decimal exactWu = densityRatio
                    * bomMilli
                    * beforeWu
                    / (1000m * beforeBomEwu);
                long floor = checked((long)decimal.Floor(exactWu));
                long ceiling = checked((long)decimal.Ceiling(exactWu));
                wuCandidates.Add(floor - 1L);
                wuCandidates.Add(floor);
                wuCandidates.Add(ceiling);
                wuCandidates.Add(ceiling + 1L);
            }
        }

        void Evaluate(long wu, long bomMilli)
        {
            long total = checked(wu * 1000L + bomMilli);
            long error = Math.Abs(checked(total - targetInvestment));
            if (checked(error * 1000L) > checked(targetInvestment * InvestmentTolerancePermille))
                return;

            decimal densityRatio = ((wu * 1000m) / bomMilli) / (beforeWu / beforeBomEwu);
            decimal laborShare = (wu * 1000m) / total;
            V27ConstructionRedistributionDisposition disposition;
            int dispositionRank;
            if (densityRatio >= NormalDensityMinimum && densityRatio <= NormalDensityMaximum)
            {
                disposition = V27ConstructionRedistributionDisposition.Normal;
                dispositionRank = 0;
            }
            else if (densityRatio >= WarningDensityMinimum
                     && densityRatio <= WarningDensityMaximum)
            {
                disposition = V27ConstructionRedistributionDisposition.WarningDensity;
                dispositionRank = 1;
            }
            else if (laborShare <= MaterialDominatedLaborShareMaximum)
            {
                disposition = V27ConstructionRedistributionDisposition.WarningMaterialDominated;
                dispositionRank = 1;
            }
            else
            {
                // A mathematically valid WU/BOM candidate can remain outside the
                // authored density band when the 50% BOM cap is too small. Keep
                // the best such candidate visible as a Critical review row instead
                // of aborting the whole ledger at the first facility. This is not
                // an approval fallback: the audit maps this disposition to a root
                // Critical and ApplyApproved still requires an exact approval key.
                disposition = V27ConstructionRedistributionDisposition.CriticalDensityUnresolved;
                dispositionRank = 2;
            }

            int changedRows = 0;
            int addedUnits = 0;
            for (int index = 0; index < options.Length; index++)
            {
                int added = Math.Abs(amounts[index] - options[index].CurrentAmount);
                if (added != 0)
                    changedRows++;
                addedUnits = checked(addedUnits + added);
            }
            if (currentApprovedWu.HasValue && wu != currentApprovedWu.Value)
                changedRows++;
            decimal densityDrift = Math.Abs(densityRatio - 1m);
            Candidate candidate = new Candidate(
                wu,
                bomMilli,
                error,
                densityRatio,
                densityDrift,
                laborShare,
                disposition,
                dispositionRank,
                changedRows,
                addedUnits,
                (int[])amounts.Clone());
            if (best == null || candidate.CompareTo(best) < 0)
                best = candidate;
        }
    }

    private static V27ConstructionRedistributionResult CreatePeriodResult(
        string stableId,
        decimal beforeWu,
        decimal beforeBomEwu,
        IReadOnlyList<MaterialOption> options,
        long periodWu,
        long bomMilli,
        long targetInvestment,
        V27ConstructionRedistributionDisposition disposition)
    {
        decimal densityRatio = ((periodWu * 1000m) / bomMilli) / (beforeWu / beforeBomEwu);
        ItemAmountDefinition[] materials = options
            .Select(value => new ItemAmountDefinition(value.ItemId, value.BeforeAmount))
            .ToArray();
        return new V27ConstructionRedistributionResult
        {
            StableId = stableId,
            BeforeWu = beforeWu,
            PeriodCandidateWu = periodWu,
            AfterWu = periodWu,
            BeforeBomMilliEwu = bomMilli,
            AfterBomMilliEwu = bomMilli,
            TargetInvestmentMilliEwu = targetInvestment,
            InvestmentErrorMilliEwu = 0L,
            DensityRatio = densityRatio,
            AfterLaborShare = (periodWu * 1000m) / targetInvestment,
            Disposition = disposition,
            BeforeMaterials = Array.AsReadOnly(materials),
            AfterMaterials = Array.AsReadOnly(materials
                .Select(value => new ItemAmountDefinition(value.ItemId, value.Amount))
                .ToArray())
        };
    }

    private static string Token(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    private sealed class MaterialOption
    {
        public MaterialOption(
            string itemId,
            int beforeAmount,
            int currentAmount,
            int maximumAmount,
            long unitMilliEwu)
        {
            ItemId = itemId;
            BeforeAmount = beforeAmount;
            CurrentAmount = currentAmount;
            MaximumAmount = maximumAmount;
            UnitMilliEwu = unitMilliEwu;
        }

        public string ItemId { get; }
        public int BeforeAmount { get; }
        public int CurrentAmount { get; }
        public int MaximumAmount { get; }
        public long UnitMilliEwu { get; }
    }

    private sealed class Candidate : IComparable<Candidate>
    {
        public Candidate(
            long wu,
            long bomMilliEwu,
            long investmentErrorMilliEwu,
            decimal densityRatio,
            decimal densityDrift,
            decimal afterLaborShare,
            V27ConstructionRedistributionDisposition disposition,
            int dispositionRank,
            int changedRows,
            int addedUnits,
            int[] amounts)
        {
            Wu = wu;
            BomMilliEwu = bomMilliEwu;
            InvestmentErrorMilliEwu = investmentErrorMilliEwu;
            DensityRatio = densityRatio;
            DensityDrift = densityDrift;
            AfterLaborShare = afterLaborShare;
            Disposition = disposition;
            DispositionRank = dispositionRank;
            ChangedRows = changedRows;
            AddedUnits = addedUnits;
            Amounts = amounts;
        }

        public long Wu { get; }
        public long BomMilliEwu { get; }
        public long InvestmentErrorMilliEwu { get; }
        public decimal DensityRatio { get; }
        public decimal DensityDrift { get; }
        public decimal AfterLaborShare { get; }
        public V27ConstructionRedistributionDisposition Disposition { get; }
        public int DispositionRank { get; }
        public int ChangedRows { get; }
        public int AddedUnits { get; }
        public int[] Amounts { get; }

        public int CompareTo(Candidate other)
        {
            if (other == null) return -1;
            int comparison = (DispositionRank == 2 ? 1 : 0)
                .CompareTo(other.DispositionRank == 2 ? 1 : 0);
            if (comparison != 0) return comparison;
            if (Disposition == V27ConstructionRedistributionDisposition.CriticalDensityUnresolved
                && other.Disposition
                    == V27ConstructionRedistributionDisposition.CriticalDensityUnresolved)
            {
                comparison = DensityDrift.CompareTo(other.DensityDrift);
                if (comparison != 0) return comparison;
                comparison = InvestmentErrorMilliEwu.CompareTo(other.InvestmentErrorMilliEwu);
                if (comparison != 0) return comparison;
            }
            comparison = ChangedRows.CompareTo(other.ChangedRows);
            if (comparison != 0) return comparison;
            comparison = DispositionRank.CompareTo(other.DispositionRank);
            if (comparison != 0) return comparison;
            comparison = DensityDrift.CompareTo(other.DensityDrift);
            if (comparison != 0) return comparison;
            comparison = InvestmentErrorMilliEwu.CompareTo(other.InvestmentErrorMilliEwu);
            if (comparison != 0) return comparison;
            comparison = AddedUnits.CompareTo(other.AddedUnits);
            if (comparison != 0) return comparison;
            comparison = other.Wu.CompareTo(Wu);
            if (comparison != 0) return comparison;
            for (int index = 0; index < Amounts.Length; index++)
            {
                comparison = Amounts[index].CompareTo(other.Amounts[index]);
                if (comparison != 0) return comparison;
            }
            return 0;
        }
    }
}
#endif
