using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionProcessKind
{
    WorkOnly = 0,
    PassiveBatch = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionFlowRole
{
    Transform = 0,
    Source = 1,
    Sink = 2
}

[CreateAssetMenu(menuName = "DungeonStory/Economy/Production Recipe", order = 1)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionRecipeSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Economy/Recipes";

    [SerializeField] private string recipeId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private string facilityTag = string.Empty;
    [SerializeField] private string workstationTag = string.Empty;
    [SerializeField] private List<string> requiredSupportTags =
        new List<string>();
    [SerializeField] private string batchSupportTag = string.Empty;
    [SerializeField] private ProductionProcessKind processKind;
    [SerializeField] private ProductionFlowRole flowRole;
    [SerializeField] private ProductionProcessClass processClass;
    [SerializeField] private bool processClassAuthored;
    [SerializeField] private string workTypeId = "work:craft";
    [SerializeField] private ProficiencyWorkProfileAuthoring proficiency = new();
    [SerializeField] private string requiredResearchId = string.Empty;
    [Min(0.1f), SerializeField] private float requiredWork = 10f;
    [Min(0f), SerializeField] private float preparationWork;
    [Min(0f), SerializeField] private float finishingWork;
    [Min(0f), SerializeField] private float processingGameHours;
    [SerializeField] private Vector2 optimalTemperatureC = new Vector2(12f, 24f);
    [SerializeField] private Vector2 warningTemperatureC = new Vector2(4f, 32f);
    [Min(0f), SerializeField] private float cleanWaterPerCycle;
    [Min(0f), SerializeField] private float wastewaterPerCycle;
    [SerializeField] private ProcessWastewaterComposition wastewaterComposition;
    [SerializeField] private bool allowsManualWaterFallback;
    [SerializeField] private string spoilageItemId = string.Empty;
    [SerializeField] private ProductionMassExplanationAuthoring
        massExplanation;
    [SerializeField] private ProductionOutputCostAllocationAuthoring
        outputCostAllocation;
    [SerializeField] private List<ItemAmountDefinition> inputs = new List<ItemAmountDefinition>();
    [SerializeField] private List<ProductionOutputDefinition> outputs =
        new List<ProductionOutputDefinition>();

    public string RecipeId => recipeId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? RecipeId : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public string FacilityTag => facilityTag?.Trim() ?? string.Empty;
    public string WorkstationTag => string.IsNullOrWhiteSpace(workstationTag)
        ? FacilityTag
        : workstationTag.Trim();
    public IReadOnlyList<string> RequiredSupportTags =>
        requiredSupportTags ??= new List<string>();
    public string BatchSupportTag => batchSupportTag?.Trim() ?? string.Empty;
    public ProductionProcessKind ProcessKind => processKind;
    public ProductionFlowRole FlowRole => flowRole;
    public ProductionProcessClass ProcessClass => processClass;
    public bool HasAuthoredProcessClass => processClassAuthored;
    public WorkTypeId WorkTypeId => new WorkTypeId(
        string.IsNullOrWhiteSpace(workTypeId) ? "work:craft" : workTypeId);
    public ProficiencyWorkProfileAuthoring Proficiency =>
        proficiency ??= new ProficiencyWorkProfileAuthoring();
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;
    public float RequiredWork => Mathf.Max(0.1f, requiredWork);
    public float PreparationWork => processKind == ProductionProcessKind.PassiveBatch
        ? Mathf.Max(0.1f, preparationWork > 0f ? preparationWork : requiredWork)
        : RequiredWork;
    public float FinishingWork => processKind == ProductionProcessKind.PassiveBatch
        ? Mathf.Max(0f, finishingWork)
        : 0f;
    public float ProcessingGameHours => processKind == ProductionProcessKind.PassiveBatch
        ? Mathf.Max(0.1f, processingGameHours)
        : 0f;
    public float OptimalTemperatureMinimum => Mathf.Min(
        optimalTemperatureC.x,
        optimalTemperatureC.y);
    public float OptimalTemperatureMaximum => Mathf.Max(
        optimalTemperatureC.x,
        optimalTemperatureC.y);
    public float WarningTemperatureMinimum => Mathf.Min(
        warningTemperatureC.x,
        warningTemperatureC.y);
    public float WarningTemperatureMaximum => Mathf.Max(
        warningTemperatureC.x,
        warningTemperatureC.y);
    public float CleanWaterPerCycle => Mathf.Max(0f, cleanWaterPerCycle);
    public float WastewaterPerCycle => Mathf.Max(0f, wastewaterPerCycle);
    public ProcessWastewaterComposition WastewaterComposition =>
        wastewaterComposition;
    public bool AllowsManualWaterFallback => allowsManualWaterFallback;
    public string SpoilageItemId => spoilageItemId ?? string.Empty;
    public ProductionMassExplanationAuthoringSnapshot MassExplanation =>
        massExplanation?.Capture()
        ?? ProductionMassExplanationAuthoringSnapshot.Empty;
    public ProductionOutputCostAllocationAuthoringSnapshot OutputCostAllocation =>
        outputCostAllocation?.Capture()
        ?? ProductionOutputCostAllocationAuthoringSnapshot.Empty;
    public IReadOnlyList<ItemAmountDefinition> Inputs => inputs ??= new List<ItemAmountDefinition>();
    public IReadOnlyList<ProductionOutputDefinition> Outputs =>
        outputs ??= new List<ProductionOutputDefinition>();

    public IReadOnlyList<ProductionOutputDefinition> CaptureCanonicalOutputs()
    {
        ProductionOutputDefinition[] snapshot =
            (outputs ?? new List<ProductionOutputDefinition>()).ToArray();
        ValidateCanonicalOutputLinesOrThrow(RecipeId, snapshot);
        return snapshot;
    }

    public void ValidateCanonicalOutputLinesOrThrow() =>
        ValidateCanonicalOutputLinesOrThrow(
            RecipeId,
            outputs ?? new List<ProductionOutputDefinition>());

    private static void ValidateCanonicalOutputLinesOrThrow(
        string ownerRecipeId,
        IEnumerable<ProductionOutputDefinition> candidates)
    {
        string recipeContext = string.IsNullOrWhiteSpace(ownerRecipeId)
            ? "<missing-recipe-id>"
            : ownerRecipeId;
        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        int outputIndex = 0;
        foreach (ProductionOutputDefinition output in candidates)
        {
            if (output == null)
            {
                throw new InvalidOperationException(
                    $"Production recipe '{recipeContext}' has a null output "
                    + $"at index {outputIndex}.");
            }
            if (!output.HasCanonicalAuthoredValue)
            {
                throw new InvalidOperationException(
                    $"Production recipe '{recipeContext}' output at index "
                    + $"{outputIndex} is not canonically authored: "
                    + $"lineId='{output.OutputLineId}', itemId='{output.ItemId}', "
                    + $"role='{output.Role}'.");
            }
            if (!lineIds.Add(output.OutputLineId))
            {
                throw new InvalidOperationException(
                    $"Production recipe '{recipeContext}' contains duplicate "
                    + $"output line ID '{output.OutputLineId}'.");
            }

            outputIndex++;
        }
    }

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string name,
        string recipeDescription,
        string requiredFacilityTag,
        string requiredWorkTypeId,
        string researchId,
        float work,
        IEnumerable<ItemAmountDefinition> recipeInputs,
        IEnumerable<ProductionOutputDefinition> recipeOutputs)
    {
        recipeId = stableId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        description = recipeDescription?.Trim() ?? string.Empty;
        facilityTag = requiredFacilityTag?.Trim() ?? string.Empty;
        workTypeId = string.IsNullOrWhiteSpace(requiredWorkTypeId)
            ? "work:craft"
            : requiredWorkTypeId.Trim();
        requiredResearchId = researchId?.Trim() ?? string.Empty;
        requiredWork = Mathf.Max(0.1f, work);
        inputs = recipeInputs?.Where(input => input != null).ToList()
            ?? new List<ItemAmountDefinition>();
        ProductionOutputDefinition[] configuredOutputs =
            recipeOutputs?.ToArray()
            ?? Array.Empty<ProductionOutputDefinition>();
        ValidateCanonicalOutputLinesOrThrow(recipeId, configuredOutputs);
        outputs = configuredOutputs.ToList();
    }

    public void ConfigureWorkshop(
        string ownerWorkstationTag,
        IEnumerable<string> supportTags,
        ProductionProcessKind kind,
        string requiredBatchSupportTag = "",
        float prepareWork = 0f,
        float finishWork = 0f,
        float processGameHours = 0f,
        float optimalMinimumC = 12f,
        float optimalMaximumC = 24f,
        float warningMinimumC = 4f,
        float warningMaximumC = 32f,
        float cleanWater = 0f,
        float wastewater = 0f,
        bool allowManualWater = false,
        string failedBatchItemId = null,
        ProcessWastewaterComposition wastewaterKind =
            ProcessWastewaterComposition.None)
    {
        workstationTag = ownerWorkstationTag?.Trim() ?? string.Empty;
        requiredSupportTags = supportTags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(System.StringComparer.Ordinal)
            .ToList()
            ?? new List<string>();
        processKind = kind;
        batchSupportTag = requiredBatchSupportTag?.Trim() ?? string.Empty;
        preparationWork = Mathf.Max(0f, prepareWork);
        finishingWork = Mathf.Max(0f, finishWork);
        processingGameHours = Mathf.Max(0f, processGameHours);
        optimalTemperatureC = new Vector2(
            Mathf.Min(optimalMinimumC, optimalMaximumC),
            Mathf.Max(optimalMinimumC, optimalMaximumC));
        warningTemperatureC = new Vector2(
            Mathf.Min(warningMinimumC, warningMaximumC),
            Mathf.Max(warningMinimumC, warningMaximumC));
        cleanWaterPerCycle = Mathf.Max(0f, cleanWater);
        wastewaterPerCycle = Mathf.Max(0f, wastewater);
        wastewaterComposition = wastewaterKind;
        allowsManualWaterFallback = allowManualWater;
        if (kind == ProductionProcessKind.PassiveBatch)
        {
            string authoredSpoilageItemId = failedBatchItemId ?? string.Empty;
            RequireCanonicalSpoilageItemId(authoredSpoilageItemId);
            spoilageItemId = authoredSpoilageItemId;
        }
        else if (failedBatchItemId != null)
        {
            if (failedBatchItemId.Length > 0)
                RequireCanonicalSpoilageItemId(failedBatchItemId);
            spoilageItemId = failedBatchItemId;
        }
    }

    public void ConfigureProficiency(
        CharacterProficiencyId primary,
        CharacterProficiencyId secondary = default,
        float primaryWeight = 1f,
        CharacterProficiencyRank recommendedRank = CharacterProficiencyRank.Apprentice,
        CharacterProficiencyRank minimumRiskRank = CharacterProficiencyRank.Apprentice) =>
        (proficiency ??= new ProficiencyWorkProfileAuthoring()).Configure(
            primary,
            secondary,
            primaryWeight,
            recommendedRank,
            minimumRiskRank);

    public void ConfigureBalanceWork(float work)
    {
        requiredWork = Mathf.Max(0.1f, work);
    }

    public void ConfigureFlowRole(ProductionFlowRole role)
    {
        flowRole = role;
    }

    public void ConfigureProcessClass(ProductionProcessClass value)
    {
        processClass = value;
        processClassAuthored = true;
    }

    public void ConfigureMassExplanation(
        string capabilityId,
        int contractVersion,
        string canonicalPayload)
    {
        ProductionMassExplanationAuthoringSnapshot expected =
            string.IsNullOrEmpty(capabilityId)
            && contractVersion == 0
            && string.IsNullOrEmpty(canonicalPayload)
                ? ProductionMassExplanationAuthoringSnapshot.Empty
                : new ProductionMassExplanationAuthoringSnapshot(
                    capabilityId,
                    contractVersion,
                    canonicalPayload);
        ProductionMassExplanationAuthoringSnapshot current = MassExplanation;
        if (expected.IsEmpty)
        {
            massExplanation = null;
            return;
        }
        if (Same(current, expected))
            return;
        (massExplanation ??= new ProductionMassExplanationAuthoring()).Configure(
            expected.CapabilityId,
            expected.ContractVersion,
            expected.CanonicalPayload);
    }

    public void ConfigureOutputCostAllocation(
        string capabilityId,
        int contractVersion,
        string canonicalPayload)
    {
        ProductionOutputCostAllocationAuthoringSnapshot expected =
            string.IsNullOrEmpty(capabilityId)
            && contractVersion == 0
            && string.IsNullOrEmpty(canonicalPayload)
                ? ProductionOutputCostAllocationAuthoringSnapshot.Empty
                : new ProductionOutputCostAllocationAuthoringSnapshot(
                    capabilityId,
                    contractVersion,
                    canonicalPayload);
        ProductionOutputCostAllocationAuthoringSnapshot current =
            OutputCostAllocation;
        if (expected.IsEmpty)
        {
            outputCostAllocation = null;
            return;
        }
        if (Same(current, expected))
            return;
        (outputCostAllocation ??=
            new ProductionOutputCostAllocationAuthoring()).Configure(
            expected.CapabilityId,
            expected.ContractVersion,
            expected.CanonicalPayload);
    }

    public void NormalizeEmptyOptionalAuthoringContracts()
    {
        if (massExplanation?.IsEmpty == true)
            massExplanation = null;
        if (outputCostAllocation?.IsEmpty == true)
            outputCostAllocation = null;
    }

    private static bool Same(
        ProductionMassExplanationAuthoringSnapshot left,
        ProductionMassExplanationAuthoringSnapshot right) =>
        string.Equals(left.CapabilityId, right.CapabilityId,
            StringComparison.Ordinal)
        && left.ContractVersion == right.ContractVersion
        && string.Equals(left.CanonicalPayload, right.CanonicalPayload,
            StringComparison.Ordinal);

    private static bool Same(
        ProductionOutputCostAllocationAuthoringSnapshot left,
        ProductionOutputCostAllocationAuthoringSnapshot right) =>
        string.Equals(left.CapabilityId, right.CapabilityId,
            StringComparison.Ordinal)
        && left.ContractVersion == right.ContractVersion
        && string.Equals(left.CanonicalPayload, right.CanonicalPayload,
            StringComparison.Ordinal);

    private static void RequireCanonicalSpoilageItemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Passive production requires an explicitly authored canonical spoilage item ID.");
        }
    }
#endif
}
