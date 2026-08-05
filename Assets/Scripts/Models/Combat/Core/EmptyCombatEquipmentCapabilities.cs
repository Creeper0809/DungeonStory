using System.Collections.Generic;

/// <summary>Explicit no-evolution capability for isolated fixtures.</summary>
public sealed class EmptyEvolutionModuleRegistry : IEvolutionModuleRegistry
{
    public static readonly EmptyEvolutionModuleRegistry Instance = new();
    private static readonly IReadOnlyList<EvolutionModuleDefinition> Empty =
        new EvolutionModuleDefinition[0];

    private EmptyEvolutionModuleRegistry()
    {
    }

    public IReadOnlyList<EvolutionModuleDefinition> All => Empty;

    public bool TryGet(string moduleId, out EvolutionModuleDefinition definition)
    {
        definition = null;
        return false;
    }
}

/// <summary>Explicit no-equipment-module capability for isolated fixtures.</summary>
public sealed class EmptyEquipmentModuleCatalog : IEquipmentModuleCatalog
{
    public static readonly EmptyEquipmentModuleCatalog Instance = new();
    private static readonly IReadOnlyList<EquipmentModuleDefinitionSO> Empty =
        new EquipmentModuleDefinitionSO[0];

    private EmptyEquipmentModuleCatalog()
    {
    }

    public IReadOnlyList<EquipmentModuleDefinitionSO> All => Empty;

    public bool TryGet(string moduleId, out EquipmentModuleDefinitionSO definition)
    {
        definition = null;
        return false;
    }
}
