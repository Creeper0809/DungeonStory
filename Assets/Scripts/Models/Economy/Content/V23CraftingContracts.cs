using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ConstructionBalanceClass
{
    Structure = 0,
    Decoration = 1,
    Furnishing = 2,
    Storage = 3,
    Workstation = 4,
    Service = 5,
    Environment = 6,
    Defense = 7,
    Medical = 8,
    Precision = 9,
    Industrial = 10,
    Arcane = 11,
    Landmark = 12
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ProductionProcessClass
{
    Gathering = 0,
    CuttingGrindingWashing = 1,
    CookingSimpleMixing = 2,
    SpinningWeavingWoodworking = 3,
    ForgingHeavyAssembly = 4,
    Chemical = 5,
    Precision = 6,
    Medical = 7,
    Rune = 8,
    HeavyIndustrial = 9
}

public interface IRecipeBalanceWorkCalculator
{
    float CalculateRecipe(ProductionRecipeSO recipe);
    float CalculateRecipe(
        ProductionRecipeSO recipe,
        ProductionProcessClass processClass);
}
