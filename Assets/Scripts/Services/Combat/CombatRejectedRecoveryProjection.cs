using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public sealed class CombatCraftConcreteInputSnapshot
{
    public CombatCraftConcreteInputSnapshot(
        string craftDefinitionId,
        string materialId,
        IEnumerable<ItemAmountDefinition> inputs)
    {
        CraftDefinitionId = RequireCanonical(
            craftDefinitionId,
            nameof(craftDefinitionId));
        MaterialId = materialId ?? string.Empty;
        if (MaterialId.Length > 0
            && !string.Equals(MaterialId, MaterialId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Material ID must already be canonicalized.",
                nameof(materialId));
        }

        ItemAmountDefinition[] ordered = (inputs
                ?? throw new ArgumentNullException(nameof(inputs)))
            .OrderBy(value => value?.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null
                || !Canonical(value.ItemId)
                || value.Amount <= 0)
            || ordered.Select(value => value.ItemId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Combat craft concrete inputs must be positive, canonical and unique.");
        }

        Inputs = Array.AsReadOnly(ordered.Select(value =>
            new ItemAmountDefinition(value.ItemId, value.Amount)).ToArray());
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-craft-concrete-input@1");
        digest.Append(CraftDefinitionId);
        digest.Append(MaterialId);
        digest.Append(ordered.Length);
        foreach (ItemAmountDefinition input in ordered)
        {
            digest.Append(input.ItemId);
            digest.Append(input.Amount);
        }
        SourceDigest = digest.ComputeSha256();
    }

    public string CraftDefinitionId { get; }
    public string MaterialId { get; }
    public IReadOnlyList<ItemAmountDefinition> Inputs { get; }
    public string SourceDigest { get; }

    private static string RequireCanonical(string value, string parameter)
    {
        if (!Canonical(value))
            throw new ArgumentException("A canonical ID is required.", parameter);
        return value;
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public static class CombatCraftConcreteInputProjection
{
    public static bool TryCapture(
        CombatEquipmentDefinitionSO definition,
        string craftDefinitionId,
        CraftMaterialDefinitionSO material,
        out CombatCraftConcreteInputSnapshot snapshot,
        out string failureReason)
    {
        snapshot = null;
        failureReason = string.Empty;
        if (!Canonical(craftDefinitionId))
        {
            failureReason = "equipment.craft.definition_noncanonical";
            return false;
        }

        Dictionary<string, int> inputs = new(StringComparer.Ordinal);
        if (CombatAmmunitionCraftDefinitions.TryGetExact(
                craftDefinitionId,
                out CombatCraftDefinitionSnapshot ammunition))
        {
            foreach (KeyValuePair<string, int> input in ammunition.FixedInputs)
                inputs.Add(input.Key, input.Value);
            snapshot = Create(craftDefinitionId, string.Empty, inputs);
            return true;
        }

        if (definition == null
            || !string.Equals(
                definition.EquipmentId,
                craftDefinitionId,
                StringComparison.Ordinal))
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        if (definition.CraftMaterials.Count > 0)
        {
            failureReason = "equipment.craft.legacy_stock_category_input";
            return false;
        }
        if (material != null
            && (!Canonical(material.MaterialId)
                || !Canonical(material.ItemId)
                || !definition.AllowsMaterial(material)))
        {
            failureReason = "equipment.material.not_allowed";
            return false;
        }

        if (material != null)
            Add(inputs, material.ItemId, definition.PrimaryMaterialAmount);
        foreach (ItemAmountDefinition component in
                 definition.RequiredComponentInputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (component == null
                || !Canonical(component.ItemId)
                || component.Amount <= 0)
            {
                failureReason = "equipment.craft.component_input_invalid";
                return false;
            }
            Add(inputs, component.ItemId, component.Amount);
        }

        snapshot = Create(
            craftDefinitionId,
            material?.MaterialId ?? string.Empty,
            inputs);
        return true;
    }

    private static CombatCraftConcreteInputSnapshot Create(
        string craftDefinitionId,
        string materialId,
        IReadOnlyDictionary<string, int> inputs) =>
        new(
            craftDefinitionId,
            materialId,
            inputs.OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => new ItemAmountDefinition(
                    value.Key,
                    value.Value)));

    private static void Add(
        IDictionary<string, int> inputs,
        string itemId,
        int amount)
    {
        if (!Canonical(itemId) || amount <= 0)
            throw new InvalidOperationException("Combat craft input is invalid.");
        inputs.TryGetValue(itemId, out int current);
        inputs[itemId] = checked(current + amount);
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class CombatRejectedRecoveryOutput
{
    public CombatRejectedRecoveryOutput(
        string outputLineId,
        string itemId,
        int quantity,
        long unitMassGrams)
    {
        if (!ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || !Canonical(itemId)
            || quantity <= 0
            || unitMassGrams <= 0L)
        {
            throw new ArgumentException("Combat rejected-recovery output is invalid.");
        }
        OutputLineId = outputLineId;
        ItemId = itemId;
        Quantity = quantity;
        UnitMassGrams = unitMassGrams;
        TotalMassGrams = checked(unitMassGrams * quantity);
    }

    public string OutputLineId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public long UnitMassGrams { get; }
    public long TotalMassGrams { get; }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public sealed class CombatRejectedRecoveryProjection
{
    public CombatRejectedRecoveryProjection(
        string craftDefinitionId,
        string materialId,
        long inputEquipmentMassGrams,
        long desiredOutputMassGrams,
        IReadOnlyList<CombatRejectedRecoveryOutput> outputs,
        string sourceDigest)
    {
        if (string.IsNullOrWhiteSpace(craftDefinitionId)
            || !string.Equals(
                craftDefinitionId,
                craftDefinitionId.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(materialId)
            || !string.Equals(materialId, materialId.Trim(), StringComparison.Ordinal)
            || inputEquipmentMassGrams <= 0L
            || desiredOutputMassGrams < 0L
            || string.IsNullOrWhiteSpace(sourceDigest)
            || !string.Equals(sourceDigest, sourceDigest.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Combat rejected-recovery projection is invalid.");
        }
        CombatRejectedRecoveryOutput[] ordered = (outputs
                ?? throw new ArgumentNullException(nameof(outputs)))
            .OrderBy(value => value?.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.ItemId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Combat rejected-recovery outputs must be unique and canonical.");
        }
        long clamped = 0L;
        foreach (CombatRejectedRecoveryOutput output in ordered)
            clamped = checked(clamped + output.TotalMassGrams);
        if (clamped > inputEquipmentMassGrams)
        {
            throw new InvalidOperationException(
                "Combat rejected recovery exceeds the consumed equipment mass.");
        }

        CraftDefinitionId = craftDefinitionId;
        MaterialId = materialId;
        InputEquipmentMassGrams = inputEquipmentMassGrams;
        DesiredOutputMassGrams = desiredOutputMassGrams;
        ClampedOutputMassGrams = clamped;
        DeclaredLossMassGrams = inputEquipmentMassGrams - clamped;
        Outputs = Array.AsReadOnly(ordered);
        SourceDigest = sourceDigest;
    }

    public string CraftDefinitionId { get; }
    public string MaterialId { get; }
    public long InputEquipmentMassGrams { get; }
    public long DesiredOutputMassGrams { get; }
    public long ClampedOutputMassGrams { get; }
    public long DeclaredLossMassGrams { get; }
    public IReadOnlyList<CombatRejectedRecoveryOutput> Outputs { get; }
    public string SourceDigest { get; }
}

public interface ICombatRejectedRecoveryProjector
{
    void ValidateActualFactors(
        float workerSkill,
        float salvageYieldMultiplier);

    CombatRejectedRecoveryProjection ProjectActual(
        string craftDefinitionId,
        string materialId,
        float workerSkill,
        float salvageYieldMultiplier,
        PhysicalMassGrams consumedEquipmentMass);

    CombatRejectedRecoveryProjection ProjectDefinitionMaximum(
        string craftDefinitionId,
        string materialId);

    IReadOnlyList<CombatRejectedRecoveryProjection>
        CaptureDefinitionMaximums(string craftDefinitionId);
}

public sealed class CombatRejectedRecoveryProjector :
    ICombatRejectedRecoveryProjector
{
    public const string OutputLinePrefix = "output:combat-rejected-recovery/";
    public const int ContractVersion = 1;

    private readonly ICombatEquipmentCatalog equipment;
    private readonly IResourceEconomyContentCatalog economy;
    private readonly IMaterialSalvageCalculator salvage;
    private readonly IPhysicalItemMassQuery mass;
    private readonly IGameplayEffectResultBoundsQuery effectBounds;

    public CombatRejectedRecoveryProjector(
        ICombatEquipmentCatalog equipment,
        IResourceEconomyContentCatalog economy,
        IMaterialSalvageCalculator salvage,
        IPhysicalItemMassQuery mass,
        IGameplayEffectResultBoundsQuery effectBounds)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.economy = economy
            ?? throw new ArgumentNullException(nameof(economy));
        this.salvage = salvage
            ?? throw new ArgumentNullException(nameof(salvage));
        this.mass = mass
            ?? throw new ArgumentNullException(nameof(mass));
        this.effectBounds = effectBounds
            ?? throw new ArgumentNullException(nameof(effectBounds));
    }

    public CombatRejectedRecoveryProjection ProjectActual(
        string craftDefinitionId,
        string materialId,
        float workerSkill,
        float salvageYieldMultiplier,
        PhysicalMassGrams consumedEquipmentMass)
    {
        ValidateActualFactors(workerSkill, salvageYieldMultiplier);
        return Project(
            "actual",
            craftDefinitionId,
            materialId,
            workerSkill,
            salvageYieldMultiplier,
            consumedEquipmentMass);
    }

    public void ValidateActualFactors(
        float workerSkill,
        float salvageYieldMultiplier)
    {
        RequireFinite(workerSkill, nameof(workerSkill));
        RequireFinite(salvageYieldMultiplier, nameof(salvageYieldMultiplier));
        if (workerSkill < 0f || salvageYieldMultiplier < 0f)
        {
            throw new ArgumentOutOfRangeException(
                workerSkill < 0f ? nameof(workerSkill) : nameof(salvageYieldMultiplier));
        }
        float maximum = effectBounds.RequireFiniteMaximum(
            GameplayEffectTargetIds.SalvageYield);
        RequireFinite(maximum, nameof(maximum));
        if (maximum < 0f || salvageYieldMultiplier > maximum)
        {
            throw new InvalidOperationException(
                "Combat salvage-yield factor exceeds its authored bounds.");
        }
    }

    public CombatRejectedRecoveryProjection ProjectDefinitionMaximum(
        string craftDefinitionId,
        string materialId)
    {
        Resolve(
            craftDefinitionId,
            materialId,
            out CombatEquipmentDefinitionSO definition,
            out _,
            out _);
        PhysicalMassGrams sourceMass = mass.GetDefinitionUnitMass(
            (ItemDefinitionId)PhysicalItemIds.ForEquipment(
                definition.EquipmentId));
        float maximum = effectBounds.RequireFiniteMaximum(
            GameplayEffectTargetIds.SalvageYield);
        RequireFinite(maximum, nameof(maximum));
        if (maximum < 0f)
            throw new InvalidOperationException("Salvage-yield maximum is negative.");
        return Project(
            "definition-maximum",
            craftDefinitionId,
            materialId,
            100f,
            maximum,
            sourceMass);
    }

    public IReadOnlyList<CombatRejectedRecoveryProjection>
        CaptureDefinitionMaximums(string craftDefinitionId)
    {
        if (!Canonical(craftDefinitionId)
            || !equipment.TryGet(
                craftDefinitionId,
                out CombatEquipmentDefinitionSO definition)
            || !string.Equals(
                definition.EquipmentId,
                craftDefinitionId,
                StringComparison.Ordinal))
        {
            throw new KeyNotFoundException(
                "Unknown combat equipment definition '"
                + (craftDefinitionId ?? string.Empty) + "'.");
        }
        CombatRejectedRecoveryProjection[] projections = economy.Materials
            .Where(value => value != null && definition.AllowsMaterial(value))
            .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
            .Select(value => ProjectDefinitionMaximum(
                craftDefinitionId,
                value.MaterialId))
            .ToArray();
        if (projections.Length == 0)
        {
            throw new InvalidOperationException(
                "Combat equipment has no reachable recovery material: "
                + craftDefinitionId);
        }
        return Array.AsReadOnly(projections);
    }

    private CombatRejectedRecoveryProjection Project(
        string projectionKind,
        string craftDefinitionId,
        string materialId,
        float workerSkill,
        float salvageYieldMultiplier,
        PhysicalMassGrams sourceMass)
    {
        if (sourceMass.Value <= 0L)
        {
            throw new InvalidOperationException(
                "Combat rejected recovery requires positive source mass.");
        }
        Resolve(
            craftDefinitionId,
            materialId,
            out CombatEquipmentDefinitionSO definition,
            out _,
            out CombatCraftConcreteInputSnapshot inputs);
        MaterialSalvageResult recovered = salvage.Calculate(
            DismantleTargetKind.CombatEquipment,
            definition.RequiredCraftWork,
            inputs.Inputs,
            workerSkill);

        Dictionary<string, int> desiredByItem = new(StringComparer.Ordinal);
        foreach (ItemAmountDefinition output in recovered.RecoveredMaterials
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (output == null
                || !Canonical(output.ItemId)
                || output.Amount <= 0)
            {
                throw new InvalidOperationException(
                    "Combat salvage calculator returned an invalid output.");
            }
            double scaled = Math.Floor(output.Amount * (double)salvageYieldMultiplier);
            if (scaled < 0d || scaled > int.MaxValue)
                throw new OverflowException("Combat recovery quantity overflowed.");
            int quantity = (int)scaled;
            if (quantity <= 0)
                continue;
            desiredByItem.TryGetValue(output.ItemId, out int current);
            desiredByItem[output.ItemId] = checked(current + quantity);
        }

        DesiredLine[] desired = desiredByItem
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new DesiredLine(
                value.Key,
                value.Value,
                RequirePositiveUnitMass(value.Key)))
            .ToArray();
        BigInteger desiredMassBig = desired.Aggregate(
            BigInteger.Zero,
            (total, line) => total
                + (BigInteger)line.Quantity * line.UnitMassGrams);
        if (desiredMassBig > long.MaxValue)
            throw new OverflowException("Combat recovery desired mass overflowed.");
        long desiredMass = (long)desiredMassBig;
        int[] quantities = ClampQuantities(
            desired,
            desiredMassBig,
            sourceMass.Value);

        List<CombatRejectedRecoveryOutput> outputs = new(desired.Length);
        for (int index = 0; index < desired.Length; index++)
        {
            if (quantities[index] <= 0)
                continue;
            DesiredLine line = desired[index];
            outputs.Add(new CombatRejectedRecoveryOutput(
                OutputLinePrefix + line.ItemId,
                line.ItemId,
                quantities[index],
                line.UnitMassGrams));
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-rejected-recovery-projection@1");
        digest.Append(ContractVersion);
        digest.Append(projectionKind);
        digest.Append(inputs.SourceDigest);
        digest.Append(mass.AuthorityRevision);
        digest.AppendFloat(workerSkill);
        digest.AppendFloat(salvageYieldMultiplier);
        digest.Append(sourceMass.Value);
        digest.Append(desiredMass);
        digest.Append(desired.Length);
        for (int index = 0; index < desired.Length; index++)
        {
            digest.Append(desired[index].ItemId);
            digest.Append(desired[index].Quantity);
            digest.Append(desired[index].UnitMassGrams);
            digest.Append(quantities[index]);
        }

        return new CombatRejectedRecoveryProjection(
            craftDefinitionId,
            materialId,
            sourceMass.Value,
            desiredMass,
            outputs,
            digest.ComputeSha256());
    }

    private void Resolve(
        string craftDefinitionId,
        string materialId,
        out CombatEquipmentDefinitionSO definition,
        out CraftMaterialDefinitionSO material,
        out CombatCraftConcreteInputSnapshot inputs)
    {
        if (!Canonical(craftDefinitionId)
            || !equipment.TryGet(craftDefinitionId, out definition)
            || !string.Equals(
                definition.EquipmentId,
                craftDefinitionId,
                StringComparison.Ordinal))
        {
            throw new KeyNotFoundException(
                "Unknown combat equipment definition '"
                + (craftDefinitionId ?? string.Empty) + "'.");
        }
        if (!Canonical(materialId)
            || !economy.TryGetMaterial(materialId, out material)
            || !string.Equals(material.MaterialId, materialId, StringComparison.Ordinal)
            || !definition.AllowsMaterial(material))
        {
            throw new InvalidOperationException(
                "Combat equipment recovery material is unknown or not allowed: "
                + (materialId ?? string.Empty));
        }
        if (!CombatCraftConcreteInputProjection.TryCapture(
                definition,
                craftDefinitionId,
                material,
                out inputs,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
    }

    private long RequirePositiveUnitMass(string itemId)
    {
        long unitMass = mass.GetDefinitionUnitMass((ItemDefinitionId)itemId).Value;
        if (unitMass <= 0L)
        {
            throw new InvalidOperationException(
                "Combat recovery item has no positive mass: " + itemId);
        }
        return unitMass;
    }

    private static int[] ClampQuantities(
        IReadOnlyList<DesiredLine> desired,
        BigInteger desiredMass,
        long budgetGrams)
    {
        int[] result = new int[desired.Count];
        if (desired.Count == 0 || desiredMass.IsZero)
            return result;
        if (desiredMass <= budgetGrams)
        {
            for (int index = 0; index < desired.Count; index++)
                result[index] = desired[index].Quantity;
            return result;
        }

        BigInteger budget = budgetGrams;
        BigInteger used = BigInteger.Zero;
        List<RemainderLine> remainders = new(desired.Count);
        for (int index = 0; index < desired.Count; index++)
        {
            DesiredLine line = desired[index];
            BigInteger numerator = (BigInteger)line.Quantity * budget;
            BigInteger baseQuantity = BigInteger.DivRem(
                numerator,
                desiredMass,
                out BigInteger remainder);
            if (baseQuantity > int.MaxValue)
                throw new OverflowException("Combat recovery clamp quantity overflowed.");
            result[index] = (int)baseQuantity;
            used += baseQuantity * line.UnitMassGrams;
            remainders.Add(new RemainderLine(index, remainder, line.ItemId));
        }

        long remaining = checked(budgetGrams - (long)used);
        foreach (RemainderLine candidate in remainders
                     .OrderByDescending(value => value.Remainder)
                     .ThenBy(value => value.ItemId, StringComparer.Ordinal))
        {
            DesiredLine line = desired[candidate.Index];
            if (result[candidate.Index] >= line.Quantity
                || line.UnitMassGrams > remaining)
            {
                continue;
            }
            result[candidate.Index]++;
            remaining -= line.UnitMassGrams;
        }

        BigInteger finalMass = BigInteger.Zero;
        for (int index = 0; index < desired.Count; index++)
        {
            if (result[index] < 0 || result[index] > desired[index].Quantity)
                throw new InvalidOperationException("Combat recovery clamp escaped bounds.");
            finalMass += (BigInteger)result[index] * desired[index].UnitMassGrams;
        }
        if (finalMass > budget)
        {
            throw new InvalidOperationException(
                "Combat recovery clamp exceeded its source-mass budget.");
        }
        return result;
    }

    private static void RequireFinite(float value, string parameter)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameter);
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private readonly struct DesiredLine
    {
        public DesiredLine(string itemId, int quantity, long unitMassGrams)
        {
            ItemId = itemId;
            Quantity = quantity;
            UnitMassGrams = unitMassGrams;
        }

        public string ItemId { get; }
        public int Quantity { get; }
        public long UnitMassGrams { get; }
    }

    private readonly struct RemainderLine
    {
        public RemainderLine(int index, BigInteger remainder, string itemId)
        {
            Index = index;
            Remainder = remainder;
            ItemId = itemId;
        }

        public int Index { get; }
        public BigInteger Remainder { get; }
        public string ItemId { get; }
    }
}
