using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.FacilityEvolution;

public readonly struct FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot
{
    public FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot(
        string buildingDefinitionId,
        WorkTypeId workTypeId,
        FacilityRole facilityRoles,
        bool appliesServiceSpeed,
        double maximumMultiplier,
        int maximumActiveNodeCount,
        string sourceDigest)
    {
        if (string.IsNullOrWhiteSpace(buildingDefinitionId)
            || !string.Equals(
                buildingDefinitionId,
                buildingDefinitionId.Trim(),
                StringComparison.Ordinal)
            || !workTypeId.IsValid
            || double.IsNaN(maximumMultiplier)
            || double.IsInfinity(maximumMultiplier)
            || maximumMultiplier <= 0d
            || maximumActiveNodeCount != FacilityEvolutionRestoreRules.MaximumNodes
            || !IsLowercaseSha256(sourceDigest))
        {
            throw new ArgumentException(
                "Facility-evolution work-speed maximum is invalid.");
        }

        BuildingDefinitionId = buildingDefinitionId;
        WorkTypeId = workTypeId;
        FacilityRoles = facilityRoles;
        AppliesServiceSpeed = appliesServiceSpeed;
        MaximumMultiplier = maximumMultiplier;
        MaximumActiveNodeCount = maximumActiveNodeCount;
        SourceDigest = sourceDigest;
    }

    public string BuildingDefinitionId { get; }
    public WorkTypeId WorkTypeId { get; }
    public FacilityRole FacilityRoles { get; }
    public bool AppliesServiceSpeed { get; }
    public double MaximumMultiplier { get; }
    public int MaximumActiveNodeCount { get; }
    public string SourceDigest { get; }

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if ((character < '0' || character > '9')
                && (character < 'a' || character > 'f'))
            {
                return false;
            }
        }
        return true;
    }
}

public interface IFacilityEvolutionWorkSpeedDefinitionMaximumQuery
{
    FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot Capture(
        BuildingSO definition,
        WorkTypeId workTypeId);
}

/// <summary>
/// Scene-free upper envelope for FacilityEvolutionModifierQuery work speed.
/// It assumes every restore-valid active, nonhistorical node can use the most
/// favorable authored module pair at the current candidate potency of one.
/// </summary>
public sealed class FacilityEvolutionWorkSpeedDefinitionMaximumQuery :
    IFacilityEvolutionWorkSpeedDefinitionMaximumQuery
{
    public const string Schema =
        "facility-evolution-work-speed-definition-maximum@1";
    private const string ServiceSpeedStatId = "service.speed";
    private const double CandidatePotency = 1d;
    private const double MinimumMultiplier = 0.1d;
    private const double MaximumMultiplier = 8d;
    private const FacilityRole ServiceRoles =
        FacilityRole.Meal
        | FacilityRole.Purchase
        | FacilityRole.Rest
        | FacilityRole.Training
        | FacilityRole.Toilet
        | FacilityRole.Hygiene;

    private readonly IEvolutionModuleRegistry modules;

    public FacilityEvolutionWorkSpeedDefinitionMaximumQuery(
        IEvolutionModuleRegistry modules)
    {
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot Capture(
        BuildingSO definition,
        WorkTypeId workTypeId)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (!WorkTypeCatalog.TryGet(
                workTypeId,
                out WorkTypeDefinition workDefinition))
        {
            throw new InvalidOperationException(
                "Unknown work type has no facility-evolution maximum: "
                + workTypeId.Value);
        }

        string buildingDefinitionId = BuildingDefinitionIdentity.Resolve(definition);

        FacilityRole roles = definition.Facility?.roles ?? FacilityRole.None;
        bool appliesServiceSpeed = workDefinition.WorkTypeId
                == BuiltInWorkTypeIds.Operate
            && (roles & ServiceRoles) != 0;
        ModuleProjection[] projections = CaptureCanonicalModuleProjections();
        double maximum = appliesServiceSpeed
            ? ProjectMaximum(projections)
            : 1d;
        if (!IsFinite(maximum) || maximum <= 0d)
            throw new InvalidOperationException(
                "Facility-evolution work-speed maximum is not finite and positive.");

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(buildingDefinitionId);
        digest.Append(workDefinition.WorkTypeId.Value);
        digest.Append((int)roles);
        digest.Append((int)ServiceRoles);
        digest.Append(appliesServiceSpeed);
        digest.Append(ServiceSpeedStatId);
        digest.AppendDouble(CandidatePotency);
        digest.Append(FacilityEvolutionRestoreRules.MaximumNodes);
        digest.AppendDouble(MinimumMultiplier);
        digest.AppendDouble(MaximumMultiplier);
        digest.Append(projections.Length);
        foreach (ModuleProjection projection in projections)
            projection.AppendDigest(digest);
        digest.AppendDouble(maximum);

        return new FacilityEvolutionWorkSpeedDefinitionMaximumSnapshot(
            buildingDefinitionId,
            workDefinition.WorkTypeId,
            roles,
            appliesServiceSpeed,
            maximum,
            FacilityEvolutionRestoreRules.MaximumNodes,
            digest.ComputeSha256());
    }

    private ModuleProjection[] CaptureCanonicalModuleProjections()
    {
        IReadOnlyList<EvolutionModuleDefinition> all = modules.All
            ?? throw new InvalidOperationException(
                "Evolution module registry returned a null definition list.");
        EvolutionModuleDefinition[] ordered = all.ToArray();
        if (ordered.Any(module => module == null))
            throw new InvalidOperationException(
                "Evolution module registry contains a null definition.");
        Array.Sort(
            ordered,
            (left, right) => string.CompareOrdinal(left.ModuleId, right.ModuleId));
        if (ordered.Select(module => module.ModuleId)
            .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Evolution module registry contains a duplicate module ID.");
        }

        List<ModuleProjection> projections = new();
        foreach (EvolutionModuleDefinition module in ordered)
        {
            ModifierProjection[] benefits = CaptureModifiers(
                module,
                module.Benefits,
                "benefit");
            ModifierProjection[] burdens = CaptureModifiers(
                module,
                module.Burdens,
                "burden");
            if (benefits.Length == 0 && burdens.Length == 0)
                continue;
            projections.Add(new ModuleProjection(
                module.ModuleId,
                module.RoleTag,
                module.ActivationRule,
                benefits,
                burdens));
        }
        return projections.ToArray();
    }

    private static ModifierProjection[] CaptureModifiers(
        EvolutionModuleDefinition module,
        IReadOnlyList<EvolutionEffectModifier> modifiers,
        string sourceKind)
    {
        if (modifiers == null)
            throw new InvalidOperationException(
                "Evolution module modifier list is null: " + module.ModuleId);
        List<ModifierProjection> captured = new();
        foreach (EvolutionEffectModifier modifier in modifiers)
        {
            if (modifier == null)
                throw new InvalidOperationException(
                    "Evolution module contains a null modifier: " + module.ModuleId);
            if (!string.Equals(
                    modifier.statId,
                    ServiceSpeedStatId,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!IsFinite(modifier.additive)
                || !IsFinite(modifier.multiplier)
                || modifier.multiplier < 0f)
            {
                throw new InvalidOperationException(
                    "Evolution module contains an invalid service.speed modifier: "
                    + module.ModuleId);
            }
            captured.Add(new ModifierProjection(
                sourceKind,
                modifier.additive,
                modifier.multiplier));
        }
        return captured
            .OrderBy(value => value.SourceKind, StringComparer.Ordinal)
            .ThenBy(value => value.AdditiveBits)
            .ThenBy(value => value.MultiplierBits)
            .ToArray();
    }

    private static double ProjectMaximum(
        IReadOnlyList<ModuleProjection> projections)
    {
        double maximumPerNode = 1d;
        foreach (ModuleProjection effect in projections)
        {
            double bestDistinctBurden = 1d;
            foreach (ModuleProjection burden in projections)
            {
                if (!string.Equals(
                        effect.ModuleId,
                        burden.ModuleId,
                        StringComparison.Ordinal))
                {
                    bestDistinctBurden = Math.Max(
                        bestDistinctBurden,
                        burden.PositiveBurdenFactor);
                }
            }
            maximumPerNode = Math.Max(
                maximumPerNode,
                effect.PositiveEffectFactor * bestDistinctBurden);
        }
        if (!IsFinite(maximumPerNode) || maximumPerNode <= 0d)
            throw new InvalidOperationException(
                "Evolution module candidates do not have a finite positive maximum.");

        double maximum = 1d;
        for (int node = 0;
             node < FacilityEvolutionRestoreRules.MaximumNodes;
             node++)
        {
            if (maximumPerNode <= 1d)
                break;
            if (maximum >= MaximumMultiplier / maximumPerNode)
                return MaximumMultiplier;
            maximum *= maximumPerNode;
        }
        return Math.Max(
            MinimumMultiplier,
            Math.Min(MaximumMultiplier, maximum));
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private readonly struct ModifierProjection
    {
        public ModifierProjection(
            string sourceKind,
            float additive,
            float multiplier)
        {
            SourceKind = sourceKind;
            Additive = additive;
            Multiplier = multiplier;
            AdditiveBits = BitConverter.SingleToInt32Bits(additive);
            MultiplierBits = BitConverter.SingleToInt32Bits(multiplier);
        }

        public string SourceKind { get; }
        public float Additive { get; }
        public float Multiplier { get; }
        public int AdditiveBits { get; }
        public int MultiplierBits { get; }

        public double PositiveFactor => Multiplier > 1f
            ? 1d + (Multiplier - 1d) * CandidatePotency
            : 1d;

        public void AppendDigest(CanonicalSemanticDigestBuilder digest)
        {
            digest.Append(SourceKind);
            digest.Append(ServiceSpeedStatId);
            digest.AppendFloat(Additive);
            digest.AppendFloat(Multiplier);
        }
    }

    private sealed class ModuleProjection
    {
        public ModuleProjection(
            string moduleId,
            string roleTag,
            EvolutionModuleActivationRule activationRule,
            IReadOnlyList<ModifierProjection> benefits,
            IReadOnlyList<ModifierProjection> burdens)
        {
            ModuleId = moduleId;
            RoleTag = roleTag ?? string.Empty;
            ActivationRule = activationRule
                ?? throw new InvalidOperationException(
                    "Evolution module activation rule is null: " + moduleId);
            Benefits = benefits ?? throw new ArgumentNullException(nameof(benefits));
            Burdens = burdens ?? throw new ArgumentNullException(nameof(burdens));
            PositiveEffectFactor = MultiplyPositive(Benefits)
                * MultiplyPositive(Burdens);
            PositiveBurdenFactor = MultiplyPositive(Burdens);
        }

        public string ModuleId { get; }
        public string RoleTag { get; }
        public EvolutionModuleActivationRule ActivationRule { get; }
        public IReadOnlyList<ModifierProjection> Benefits { get; }
        public IReadOnlyList<ModifierProjection> Burdens { get; }
        public double PositiveEffectFactor { get; }
        public double PositiveBurdenFactor { get; }

        public void AppendDigest(CanonicalSemanticDigestBuilder digest)
        {
            digest.Append(ModuleId);
            digest.Append(RoleTag);
            digest.AppendEnum(ActivationRule.kind);
            AppendStrings(digest, ActivationRule.requiredRoomTags);
            AppendStrings(digest, ActivationRule.optionalRoomTags);
            AppendStrings(digest, ActivationRule.forbiddenRoomTags);
            digest.AppendFloat(ActivationRule.minimumCleanliness);
            digest.AppendFloat(ActivationRule.minimumBeauty);
            digest.AppendFloat(ActivationRule.minimumTemperature);
            digest.AppendFloat(ActivationRule.minimumSpace);
            digest.Append(Benefits.Count);
            foreach (ModifierProjection modifier in Benefits)
                modifier.AppendDigest(digest);
            digest.Append(Burdens.Count);
            foreach (ModifierProjection modifier in Burdens)
                modifier.AppendDigest(digest);
            digest.AppendDouble(PositiveEffectFactor);
            digest.AppendDouble(PositiveBurdenFactor);
        }

        private static double MultiplyPositive(
            IEnumerable<ModifierProjection> modifiers)
        {
            double result = 1d;
            foreach (ModifierProjection modifier in modifiers)
            {
                result *= modifier.PositiveFactor;
                if (!IsFinite(result))
                    throw new InvalidOperationException(
                        "Evolution modifier product is not finite.");
            }
            return result;
        }

        private static void AppendStrings(
            CanonicalSemanticDigestBuilder digest,
            IEnumerable<string> values)
        {
            string[] ordered = (values ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            digest.Append(ordered.Length);
            foreach (string value in ordered)
                digest.Append(value);
        }
    }
}
