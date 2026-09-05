using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionFacilitySupplyRuleSnapshot
{
    private readonly IReadOnlyList<string> allowedItemIds;
    private readonly IReadOnlyList<string> forbiddenItemIds;
    private readonly IReadOnlyList<string> priorityItemIds;

    internal ProductionFacilitySupplyRuleSnapshot(FacilitySupplyProfile profile)
    {
        HasAuthoredProfile = profile != null;
        if (profile == null)
        {
            Kind = FacilitySupplyKind.Fuel;
            RequiredTags = ResourceIngredientTag.None;
            MinimumValue = 0f;
            BufferCapacity = 0;
            allowedItemIds = Array.Empty<string>();
            forbiddenItemIds = Array.Empty<string>();
            priorityItemIds = Array.Empty<string>();
            return;
        }
        if (float.IsNaN(profile.minimumValue)
            || float.IsInfinity(profile.minimumValue)
            || profile.minimumValue < 0f
            || profile.bufferCapacity < 1)
        {
            throw new InvalidOperationException(
                "Production support has an invalid facility supply profile.");
        }
        Kind = profile.kind;
        RequiredTags = profile.requiredTags;
        MinimumValue = profile.minimumValue;
        BufferCapacity = profile.bufferCapacity;
        allowedItemIds = CaptureIds(profile.allowedItemIds, "allowed");
        forbiddenItemIds = CaptureIds(profile.forbiddenItemIds, "forbidden");
        priorityItemIds = CaptureIds(profile.priorityItemIds, "priority");
    }

    public bool HasAuthoredProfile { get; }
    public FacilitySupplyKind Kind { get; }
    public ResourceIngredientTag RequiredTags { get; }
    public float MinimumValue { get; }
    public int BufferCapacity { get; }
    public IReadOnlyList<string> AllowedItemIds => allowedItemIds;
    public IReadOnlyList<string> ForbiddenItemIds => forbiddenItemIds;
    public IReadOnlyList<string> PriorityItemIds => priorityItemIds;

    public bool Allows(ResourceItemDefinitionSO item)
    {
        if (!HasAuthoredProfile || item == null
            || forbiddenItemIds.Contains(item.ItemId, StringComparer.Ordinal))
        {
            return false;
        }
        bool explicitlyAllowed = allowedItemIds.Count > 0
            && allowedItemIds.Contains(item.ItemId, StringComparer.Ordinal);
        bool tagsMatch = RequiredTags == ResourceIngredientTag.None
            || (item.IngredientTags & RequiredTags) == RequiredTags;
        float value = Kind == FacilitySupplyKind.Fuel
            ? item.FuelValue
            : item.FacilityNutritionValue;
        return (allowedItemIds.Count == 0 ? tagsMatch : explicitlyAllowed)
            && value >= MinimumValue;
    }

    internal void AppendTo(CanonicalSemanticDigestBuilder canonical)
    {
        canonical.Append(HasAuthoredProfile);
        canonical.AppendEnum(Kind);
        canonical.Append((int)RequiredTags);
        canonical.AppendFloat(MinimumValue);
        canonical.Append(BufferCapacity);
        AppendIds(canonical, allowedItemIds);
        AppendIds(canonical, forbiddenItemIds);
        AppendIds(canonical, priorityItemIds);
    }

    private static IReadOnlyList<string> CaptureIds(
        IEnumerable<string> source,
        string role)
    {
        string[] values = (source ?? Array.Empty<string>()).ToArray();
        if (values.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Production support has a noncanonical {role} supply ID.");
        }
        string[] ordered = values
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                $"Production support has duplicate {role} supply IDs.");
        }
        return Array.AsReadOnly(ordered);
    }

    private static void AppendIds(
        CanonicalSemanticDigestBuilder canonical,
        IReadOnlyList<string> values)
    {
        canonical.Append(values.Count);
        foreach (string value in values)
            canonical.Append(value);
    }
}

public sealed class ProductionAuthoredSupportProfileSnapshot
{
    internal ProductionAuthoredSupportProfileSnapshot(
        string supportId,
        ProductionSupportKind kind,
        int batchCapacity,
        int maximumLinkedInstancesPerWorkstation,
        ProductionOutputFactor workSpeedFactor,
        ProductionOutputFactor outputFactor,
        float cleanWaterPerCycle,
        float wastewaterPerCycle,
        ProcessWastewaterComposition wastewaterComposition,
        bool requiresFuel,
        string fallbackFuelItemId,
        int fuelPerCycle,
        ProductionFacilitySupplyRuleSnapshot fuelSupplyRule,
        string sourceDigest)
    {
        SupportId = supportId;
        Kind = kind;
        BatchCapacity = batchCapacity;
        MaximumLinkedInstancesPerWorkstation =
            maximumLinkedInstancesPerWorkstation;
        WorkSpeedFactor = workSpeedFactor;
        OutputFactor = outputFactor;
        CleanWaterPerCycle = cleanWaterPerCycle;
        WastewaterPerCycle = wastewaterPerCycle;
        WastewaterComposition = wastewaterComposition;
        RequiresFuel = requiresFuel;
        FallbackFuelItemId = fallbackFuelItemId;
        FuelPerCycle = fuelPerCycle;
        FuelSupplyRule = fuelSupplyRule;
        SourceDigest = sourceDigest;
    }

    public string SupportId { get; }
    public ProductionSupportKind Kind { get; }
    public int BatchCapacity { get; }
    public int MaximumLinkedInstancesPerWorkstation { get; }
    public ProductionOutputFactor WorkSpeedFactor { get; }
    public ProductionOutputFactor OutputFactor { get; }
    public float CleanWaterPerCycle { get; }
    public float WastewaterPerCycle { get; }
    public ProcessWastewaterComposition WastewaterComposition { get; }
    public bool RequiresFuel { get; }
    public string FallbackFuelItemId { get; }
    public int FuelPerCycle { get; }
    public ProductionFacilitySupplyRuleSnapshot FuelSupplyRule { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionAuthoredSupportAssignmentSnapshot
{
    internal ProductionAuthoredSupportAssignmentSnapshot(
        IReadOnlyList<ProductionAuthoredSupportProfileSnapshot> supports)
    {
        ProductionAuthoredSupportProfileSnapshot[] ordered = (supports
                ?? throw new ArgumentNullException(nameof(supports)))
            .OrderBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        Supports = Array.AsReadOnly(ordered);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-authored-support-assignment@2");
        digest.Append(ordered.Length);
        foreach (ProductionAuthoredSupportProfileSnapshot support in ordered)
        {
            digest.Append(support.SupportId);
            digest.Append(support.SourceDigest);
        }
        SourceDigest = digest.ComputeSha256();
    }

    public IReadOnlyList<ProductionAuthoredSupportProfileSnapshot> Supports { get; }
    public string SourceDigest { get; }
}

public interface IProductionMaximumOutputFactorCatalog
{
    int SupportDefinitionCount { get; }
    string SourceDigest { get; }
    ProductionOutputFactor ResolveMaximum(ProductionRecipeSO recipe);
    string CaptureRecipeSourceDigest(ProductionRecipeSO recipe);
    IReadOnlyList<ProductionAuthoredSupportAssignmentSnapshot>
        CaptureFeasibleAssignments(ProductionRecipeSO recipe);
    IReadOnlyList<ProductionAuthoredSupportProfileSnapshot>
        CaptureBatchSupportProfiles(ProductionRecipeSO recipe);
}

/// <summary>
/// Immutable authored maximum for production output modifiers. Required support
/// tags are solved as a deterministic bounded assignment problem so one provider
/// can cover several tags without multiplying itself more than once.
/// </summary>
public sealed class ProductionMaximumOutputFactorCatalog :
    IProductionMaximumOutputFactorCatalog
{
    public const string SourceDigestSchemaToken =
        "production-maximum-output-factor-catalog@4";
    public const string StateBudgetFailureCode =
        "PRODUCTION_SUPPORT_MAXIMUM_STATE_BUDGET_EXCEEDED";

    private const int MaximumRequiredSupportTags = 63;
    private const int MaximumDynamicProgrammingStates = 65_536;

    private readonly SupportProfile[] supports;

    public ProductionMaximumOutputFactorCatalog(IGameContentCatalog content)
        : this((content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<BuildingSO>())
    {
    }

    public ProductionMaximumOutputFactorCatalog(
        IEnumerable<BuildingSO> buildings)
    {
        if (buildings == null)
            throw new ArgumentNullException(nameof(buildings));

        supports = buildings
            .Where(value => value != null)
            .Where(value => value.GetProductionSupportAbility() != null)
            .Select(CreateProfile)
            .OrderBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        if (supports.Select(value => value.SupportId)
            .Distinct(StringComparer.Ordinal).Count() != supports.Length)
        {
            throw new InvalidOperationException(
                "Production support catalog contains duplicate support IDs.");
        }
        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(SourceDigestSchemaToken);
        canonical.Append(supports.Length);
        foreach (SupportProfile support in supports)
            support.AppendTo(canonical);
        SourceDigest = canonical.ComputeSha256();
    }

    public int SupportDefinitionCount => supports.Length;
    public string SourceDigest { get; }

    public ProductionOutputFactor ResolveMaximum(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));

        string[] requiredTags = CaptureModifierTags(recipe);
        for (int index = 0; index < requiredTags.Length; index++)
        {
            string requiredTag = requiredTags[index];
            bool providerExists = supports.Any(value =>
                value.Supports(recipe.WorkstationTag, requiredTag));
            if (!providerExists)
            {
                throw new InvalidOperationException(
                    $"Production recipe '{recipe.RecipeId}' has no authored support "
                    + $"provider for '{requiredTag}' at '{recipe.WorkstationTag}'.");
            }
        }

        ValidateBatchSupport(recipe);
        ProductionOutputFactor supportMaximum =
            ResolveMaximumSupportFactor(recipe, requiredTags);
        return ProductionOutputFactorAuthority.ResolveMaximumGrandProject(
                recipe.FacilityTag)
            .Multiply(supportMaximum);
    }

    public string CaptureRecipeSourceDigest(ProductionRecipeSO recipe)
    {
        ProductionOutputFactor maximum = ResolveMaximum(recipe);
        string[] requiredTags = CaptureModifierTags(recipe);
        string batchSupportTag = CaptureBatchSupportTag(recipe);
        SupportProfile[] modifierProviders = supports
            .Where(value => requiredTags.Any(tag =>
                value.Supports(recipe.WorkstationTag, tag)))
            .ToArray();
        SupportProfile[] batchProviders = string.IsNullOrEmpty(batchSupportTag)
            ? Array.Empty<SupportProfile>()
            : supports.Where(value =>
                    value.Kind == ProductionSupportKind.BatchProcessor
                    && value.Supports(recipe.WorkstationTag, batchSupportTag))
                .ToArray();
        SupportProfile[] providers = modifierProviders
            .Concat(batchProviders)
            .Distinct()
            .OrderBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append("production-maximum-output-factor-recipe-source@4");
        canonical.Append(recipe.WorkstationTag);
        canonical.Append(recipe.FacilityTag);
        canonical.Append(requiredTags.Length);
        foreach (string tag in requiredTags)
            canonical.Append(tag);
        canonical.Append(batchSupportTag);
        canonical.Append(providers.Length);
        foreach (SupportProfile provider in providers)
            provider.AppendTo(canonical);
        canonical.Append(maximum.Numerator);
        canonical.Append(maximum.Denominator);
        return canonical.ComputeSha256();
    }

    private ProductionOutputFactor ResolveMaximumSupportFactor(
        ProductionRecipeSO recipe,
        IReadOnlyList<string> requiredTags)
    {
        ProductionOutputFactor best = ProductionOutputFactor.One;
        bool found = false;
        foreach (ProductionAuthoredSupportAssignmentSnapshot assignment in
                 CaptureFeasibleAssignments(recipe))
        {
            ProductionOutputFactor factor = assignment.Supports.Aggregate(
                ProductionOutputFactor.One,
                (current, support) => current.Multiply(support.OutputFactor));
            if (!found || Compare(factor, best) > 0)
            {
                best = factor;
                found = true;
            }
        }
        if (!found)
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has no complete authored support assignment.");
        }
        return best;
    }

    public IReadOnlyList<ProductionAuthoredSupportAssignmentSnapshot>
        CaptureFeasibleAssignments(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        string[] requiredTags = CaptureModifierTags(recipe);
        if (requiredTags.Length == 0)
        {
            return Array.AsReadOnly(new[]
            {
                new ProductionAuthoredSupportAssignmentSnapshot(
                    Array.Empty<ProductionAuthoredSupportProfileSnapshot>())
            });
        }
        if (requiredTags.Length > MaximumRequiredSupportTags)
        {
            throw new InvalidOperationException(
                $"{StateBudgetFailureCode}: recipe '{recipe.RecipeId}' has "
                + $"{requiredTags.Length} output-modifying support tags.");
        }

        SupportProfile[][] candidates = requiredTags
            .Select(tag => supports
                .Where(value => value.Supports(recipe.WorkstationTag, tag))
                .OrderBy(value => value.SupportId, StringComparer.Ordinal)
                .ToArray())
            .ToArray();
        for (int index = 0; index < candidates.Length; index++)
        {
            if (candidates[index].Length == 0)
            {
                throw new InvalidOperationException(
                    $"Production recipe '{recipe.RecipeId}' has no authored support "
                    + $"provider for '{requiredTags[index]}' at '{recipe.WorkstationTag}'.");
            }
        }

        HashSet<string> visited = new(StringComparer.Ordinal);
        Dictionary<string, SupportProfile[]> assignments = new(
            StringComparer.Ordinal);
        SortedSet<int> selected = new();
        void Resolve(int tagIndex)
        {
            string selectedKey = string.Join(",", selected);
            string stateKey = tagIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ":" + selectedKey;
            if (!visited.Add(stateKey))
                return;
            if (visited.Count > MaximumDynamicProgrammingStates)
            {
                throw new InvalidOperationException(
                    $"{StateBudgetFailureCode}: recipe '{recipe.RecipeId}' exceeded "
                    + $"{MaximumDynamicProgrammingStates} reachable support states.");
            }
            if (tagIndex == candidates.Length)
            {
                assignments.TryAdd(
                    selectedKey,
                    selected.Select(index => supports[index]).ToArray());
                return;
            }
            foreach (SupportProfile candidate in candidates[tagIndex])
            {
                int supportIndex = Array.IndexOf(supports, candidate);
                bool added = selected.Add(supportIndex);
                Resolve(tagIndex + 1);
                if (added)
                    selected.Remove(supportIndex);
            }
        }
        Resolve(0);

        return Array.AsReadOnly(assignments.Values
            .Select(value => new ProductionAuthoredSupportAssignmentSnapshot(
                value.Select(profile => profile.Snapshot).ToArray()))
            .OrderBy(value => value.SourceDigest, StringComparer.Ordinal)
            .ToArray());
    }

    public IReadOnlyList<ProductionAuthoredSupportProfileSnapshot>
        CaptureBatchSupportProfiles(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));

        string batchSupportTag = CaptureBatchSupportTag(recipe);
        if (string.IsNullOrEmpty(batchSupportTag))
            return Array.Empty<ProductionAuthoredSupportProfileSnapshot>();

        ProductionAuthoredSupportProfileSnapshot[] result = supports
            .Where(value => value.Kind == ProductionSupportKind.BatchProcessor
                && value.Supports(recipe.WorkstationTag, batchSupportTag))
            .OrderBy(value => value.SupportId, StringComparer.Ordinal)
            .Select(value => value.Snapshot)
            .ToArray();
        if (result.Length == 0)
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has no authored batch "
                + $"support provider for '{batchSupportTag}' at "
                + $"'{recipe.WorkstationTag}'.");
        }
        return Array.AsReadOnly(result);
    }

    private static ulong CaptureCoverage(
        SupportProfile profile,
        string workstationTag,
        IReadOnlyList<string> requiredTags)
    {
        ulong result = 0UL;
        for (int index = 0; index < requiredTags.Count; index++)
        {
            if (profile.Supports(workstationTag, requiredTags[index]))
                result |= 1UL << index;
        }
        return result;
    }

    private void ValidateBatchSupport(ProductionRecipeSO recipe)
    {
        string batchSupportTag = CaptureBatchSupportTag(recipe);
        if (string.IsNullOrEmpty(batchSupportTag))
            return;
        if (!supports.Any(value =>
                value.Kind == ProductionSupportKind.BatchProcessor
                && value.Supports(recipe.WorkstationTag, batchSupportTag)))
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has no authored batch "
                + $"support provider for '{batchSupportTag}' at "
                + $"'{recipe.WorkstationTag}'.");
        }
    }

    private static int Compare(
        ProductionOutputFactor left,
        ProductionOutputFactor right)
    {
        long leftNumerator = left.Numerator;
        long leftDenominator = left.Denominator;
        long rightNumerator = right.Numerator;
        long rightDenominator = right.Denominator;
        int direction = 1;
        while (true)
        {
            long leftWhole = leftNumerator / leftDenominator;
            long rightWhole = rightNumerator / rightDenominator;
            if (leftWhole != rightWhole)
                return direction * leftWhole.CompareTo(rightWhole);

            long leftRemainder = leftNumerator % leftDenominator;
            long rightRemainder = rightNumerator % rightDenominator;
            if (leftRemainder == 0L || rightRemainder == 0L)
            {
                if (leftRemainder == rightRemainder)
                    return 0;
                return direction * (leftRemainder == 0L ? -1 : 1);
            }

            leftNumerator = leftDenominator;
            leftDenominator = leftRemainder;
            rightNumerator = rightDenominator;
            rightDenominator = rightRemainder;
            direction = -direction;
        }
    }

    private static string[] CaptureModifierTags(ProductionRecipeSO recipe)
    {
        string[] source = (recipe.RequiredSupportTags
                ?? Array.Empty<string>())
            .ToArray();
        if (source.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has noncanonical support tags.");
        }
        string[] canonical = source
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return canonical;
    }

    private static string CaptureBatchSupportTag(ProductionRecipeSO recipe)
    {
        string source = recipe.BatchSupportTag ?? string.Empty;
        if (!string.Equals(source, source.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has a noncanonical "
                + "batch support tag.");
        }
        return source;
    }

    private static SupportProfile CreateProfile(BuildingSO definition)
    {
        BuildingProductionSupportAbility ability =
            definition?.GetProductionSupportAbility();
        if (ability == null
            || !ability.IsValid
            || string.IsNullOrEmpty(ability.SupportId)
            || !string.Equals(
                ability.supportId,
                ability.SupportId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production support has a noncanonical or invalid identity.");
        }

        string[] featureTags = CanonicalizeTags(
            ability.featureTags,
            ability.SupportId,
            "feature");
        string[] workstationTags = CanonicalizeTags(
            ability.compatibleWorkstationTags,
            ability.SupportId,
            "workstation");
        return new SupportProfile(
            ability.SupportId,
            featureTags,
            workstationTags,
            ability.kind,
            ability.BatchCapacity,
            ability.MaximumLinkedInstancesPerWorkstation,
            ProductionOutputFactor.FromAuthoredMultiplier(
                ability.workSpeedMultiplier),
            ProductionOutputFactor.FromAuthoredMultiplier(
                ability.outputMultiplier),
            ability.cleanWaterPerCycle,
            ability.wastewaterPerCycle,
            ability.wastewaterComposition,
            ability.requiresFuel,
            ability.fuelItemId,
            ability.fuelPerCycle,
            new ProductionFacilitySupplyRuleSnapshot(
                definition.GetFacilitySupplyAbility()
                    ?.GetProfile(FacilitySupplyKind.Fuel)));
    }

    private static string[] CanonicalizeTags(
        IEnumerable<string> values,
        string supportId,
        string role)
    {
        string[] source = (values ?? Array.Empty<string>()).ToArray();
        if (source.Length == 0
            || source.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Production support '{supportId}' has invalid {role} tags.");
        }
        string[] canonical = source
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (canonical.Length != source.Length)
        {
            throw new InvalidOperationException(
                $"Production support '{supportId}' has duplicate {role} tags.");
        }
        return canonical;
    }

    private sealed class SupportProfile
    {
        public SupportProfile(
            string supportId,
            IReadOnlyCollection<string> featureTags,
            IReadOnlyCollection<string> workstationTags,
            ProductionSupportKind kind,
            int batchCapacity,
            int maximumLinkedInstancesPerWorkstation,
            ProductionOutputFactor workSpeedFactor,
            ProductionOutputFactor outputFactor,
            float cleanWaterPerCycle,
            float wastewaterPerCycle,
            ProcessWastewaterComposition wastewaterComposition,
            bool requiresFuel,
            string fallbackFuelItemId,
            int fuelPerCycle,
            ProductionFacilitySupplyRuleSnapshot fuelSupplyRule)
        {
            if (float.IsNaN(cleanWaterPerCycle)
                || float.IsInfinity(cleanWaterPerCycle)
                || cleanWaterPerCycle < 0f
                || float.IsNaN(wastewaterPerCycle)
                || float.IsInfinity(wastewaterPerCycle)
                || wastewaterPerCycle < 0f
                || (wastewaterPerCycle > 0f
                    && wastewaterComposition == ProcessWastewaterComposition.None)
                || requiresFuel && fuelPerCycle < 1
                || maximumLinkedInstancesPerWorkstation < 1
                || kind == ProductionSupportKind.BatchProcessor
                    && batchCapacity < 1
                || kind != ProductionSupportKind.BatchProcessor
                    && batchCapacity != 0)
            {
                throw new InvalidOperationException(
                    $"Production support '{supportId}' has invalid fluid or fuel authoring.");
            }
            string canonicalFuelId = fallbackFuelItemId ?? string.Empty;
            if (requiresFuel
                && (string.IsNullOrWhiteSpace(canonicalFuelId)
                    || !string.Equals(
                        canonicalFuelId,
                        canonicalFuelId.Trim(),
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Production support '{supportId}' has a noncanonical fallback fuel ID.");
            }
            SupportId = supportId;
            FeatureTags = featureTags;
            WorkstationTags = workstationTags;
            Kind = kind;
            BatchCapacity = batchCapacity;
            MaximumLinkedInstancesPerWorkstation =
                maximumLinkedInstancesPerWorkstation;
            WorkSpeedFactor = workSpeedFactor;
            OutputFactor = outputFactor;
            CleanWaterPerCycle = cleanWaterPerCycle;
            WastewaterPerCycle = wastewaterPerCycle;
            WastewaterComposition = wastewaterComposition;
            RequiresFuel = requiresFuel;
            FallbackFuelItemId = requiresFuel ? canonicalFuelId : string.Empty;
            FuelPerCycle = requiresFuel ? fuelPerCycle : 0;
            FuelSupplyRule = fuelSupplyRule
                ?? throw new ArgumentNullException(nameof(fuelSupplyRule));

            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("production-authored-support-profile@2");
            AppendTo(digest);
            Snapshot = new ProductionAuthoredSupportProfileSnapshot(
                SupportId,
                Kind,
                BatchCapacity,
                MaximumLinkedInstancesPerWorkstation,
                WorkSpeedFactor,
                OutputFactor,
                CleanWaterPerCycle,
                WastewaterPerCycle,
                WastewaterComposition,
                RequiresFuel,
                FallbackFuelItemId,
                FuelPerCycle,
                FuelSupplyRule,
                digest.ComputeSha256());
        }

        public string SupportId { get; }
        private IReadOnlyCollection<string> FeatureTags { get; }
        private IReadOnlyCollection<string> WorkstationTags { get; }
        public ProductionSupportKind Kind { get; }
        public int BatchCapacity { get; }
        public int MaximumLinkedInstancesPerWorkstation { get; }
        public ProductionOutputFactor WorkSpeedFactor { get; }
        public ProductionOutputFactor OutputFactor { get; }
        public float CleanWaterPerCycle { get; }
        public float WastewaterPerCycle { get; }
        public ProcessWastewaterComposition WastewaterComposition { get; }
        public bool RequiresFuel { get; }
        public string FallbackFuelItemId { get; }
        public int FuelPerCycle { get; }
        public ProductionFacilitySupplyRuleSnapshot FuelSupplyRule { get; }
        public ProductionAuthoredSupportProfileSnapshot Snapshot { get; }

        public void AppendTo(CanonicalSemanticDigestBuilder canonical)
        {
            if (canonical == null)
                throw new ArgumentNullException(nameof(canonical));
            canonical.Append(SupportId);
            canonical.Append(FeatureTags.Count);
            foreach (string tag in FeatureTags)
                canonical.Append(tag);
            canonical.Append(WorkstationTags.Count);
            foreach (string tag in WorkstationTags)
                canonical.Append(tag);
            canonical.Append((int)Kind);
            canonical.Append(BatchCapacity);
            canonical.Append(MaximumLinkedInstancesPerWorkstation);
            canonical.Append(WorkSpeedFactor.Numerator);
            canonical.Append(WorkSpeedFactor.Denominator);
            canonical.Append(OutputFactor.Numerator);
            canonical.Append(OutputFactor.Denominator);
            canonical.AppendFloat(CleanWaterPerCycle);
            canonical.AppendFloat(WastewaterPerCycle);
            canonical.AppendEnum(WastewaterComposition);
            canonical.Append(RequiresFuel);
            canonical.Append(FallbackFuelItemId);
            canonical.Append(FuelPerCycle);
            FuelSupplyRule.AppendTo(canonical);
        }

        public bool Supports(string workstationTag, string featureTag) =>
            WorkstationTags.Contains(workstationTag, StringComparer.Ordinal)
            && FeatureTags.Contains(featureTag, StringComparer.Ordinal);
    }

    private readonly struct SupportCoverage
    {
        public SupportCoverage(SupportProfile profile, ulong coverage)
        {
            Profile = profile;
            Coverage = coverage;
        }

        public SupportProfile Profile { get; }
        public ulong Coverage { get; }
    }
}
