using System;
using System.Collections.Generic;
using DungeonStory.Buildings;

public abstract class BuildingAbilityWorkCompletedHandler<TAbility> :
    IBuildingAbilityWorkCompletedHandler
    where TAbility : BuildingAbility
{
    private static readonly Type[] Types = { typeof(TAbility) };

    public IReadOnlyCollection<Type> AbilityTypes => Types;

    public int Apply(BuildingAbility ability, BuildingAbilityWorkContext context)
    {
        if (ability is not TAbility typedAbility)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} cannot handle '{ability?.GetType().FullName ?? "null"}'.");
        }

        return Apply(typedAbility, context);
    }

    protected abstract int Apply(TAbility ability, BuildingAbilityWorkContext context);

    protected static BuildingCoreAbilityWorkContext Adapt(BuildingAbilityWorkContext context)
    {
        return new BuildingCoreAbilityWorkContext(
            context.Actor,
            context.Building != null
                ? new BuildingCoreWorkTargetAdapter(context.Building)
                : null,
            context.WorkTypeId.Value);
    }
}

public sealed class ProductionBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingProductionAbility>
{
    private readonly IFacilityEvolutionModifierQuery evolutionModifiers;
    private readonly DungeonStory.Buildings.ProductionBuildingAbilityHandler core;

    public ProductionBuildingAbilityHandler(
        IFacilityEvolutionModifierQuery evolutionModifiers,
        DungeonStory.Buildings.ProductionBuildingAbilityHandler core)
    {
        this.evolutionModifiers = evolutionModifiers
            ?? throw new ArgumentNullException(nameof(evolutionModifiers));
        this.core = core ?? throw new ArgumentNullException(nameof(core));
    }

    protected override int Apply(
        BuildingProductionAbility ability,
        BuildingAbilityWorkContext context)
    {
        float multiplier = context.Building != null
            ? evolutionModifiers.GetOutputMultiplier(context.Building, context.WorkTypeId)
            : 1f;
        return core.Apply(
            new ProductionBuildingAbilityWork(
                ability.AbilityId,
                ability.outputCategory,
                ability.amount),
            Adapt(context),
            multiplier);
    }
}

public sealed class CleaningBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingCleaningAbility>
{
    private readonly DungeonStory.Buildings.CleaningBuildingAbilityHandler core;

    public CleaningBuildingAbilityHandler(
        DungeonStory.Buildings.CleaningBuildingAbilityHandler core)
    {
        this.core = core ?? throw new ArgumentNullException(nameof(core));
    }

    protected override int Apply(
        BuildingCleaningAbility ability,
        BuildingAbilityWorkContext context)
    {
        return core.Apply(
            new CleaningBuildingAbilityWork(ability.restoredCleanliness),
            Adapt(context));
    }
}

public sealed class SecurityBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingSecurityAbility>
{
    private readonly DungeonStory.Buildings.SecurityBuildingAbilityHandler core;

    public SecurityBuildingAbilityHandler(
        DungeonStory.Buildings.SecurityBuildingAbilityHandler core)
    {
        this.core = core ?? throw new ArgumentNullException(nameof(core));
    }

    protected override int Apply(
        BuildingSecurityAbility ability,
        BuildingAbilityWorkContext context)
    {
        return core.Apply(
            new SecurityBuildingAbilityWork(
                ability.maxAlarmCharges,
                ability.chargesPerGuardWork,
                ability.AbilityId),
            Adapt(context));
    }
}

public sealed class ReceptionBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingReceptionAbility>
{
    private readonly DungeonStory.Buildings.ReceptionBuildingAbilityHandler core;

    public ReceptionBuildingAbilityHandler(
        DungeonStory.Buildings.ReceptionBuildingAbilityHandler core)
    {
        this.core = core ?? throw new ArgumentNullException(nameof(core));
    }

    protected override int Apply(
        BuildingReceptionAbility ability,
        BuildingAbilityWorkContext context)
    {
        return core.Apply(
            new ReceptionBuildingAbilityWork(
                ability.readinessGain,
                ability.firstImpressionBonus,
                ability.moodBonus,
                ability.moodDurationSeconds),
            Adapt(context));
    }
}

public sealed class PatrolPostBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingPatrolPostAbility>
{
    private readonly DungeonStory.Buildings.PatrolPostBuildingAbilityHandler core;

    public PatrolPostBuildingAbilityHandler(
        DungeonStory.Buildings.PatrolPostBuildingAbilityHandler core)
    {
        this.core = core ?? throw new ArgumentNullException(nameof(core));
    }

    protected override int Apply(
        BuildingPatrolPostAbility ability,
        BuildingAbilityWorkContext context)
    {
        return core.Apply(
            new PatrolPostBuildingAbilityWork(
                ability.patrolReadinessGain,
                ability.incidentDetectionBonus),
            Adapt(context));
    }
}

public sealed class OutdoorRestBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingOutdoorRestAbility>
{
    private readonly DungeonStory.Buildings.OutdoorRestBuildingAbilityHandler core;

    public OutdoorRestBuildingAbilityHandler(
        DungeonStory.Buildings.OutdoorRestBuildingAbilityHandler core)
    {
        this.core = core ?? throw new ArgumentNullException(nameof(core));
    }

    protected override int Apply(
        BuildingOutdoorRestAbility ability,
        BuildingAbilityWorkContext context)
    {
        return core.Apply(
            new OutdoorRestBuildingAbilityWork(
                ability.moodBonus,
                ability.stressRecovery,
                ability.moodDurationSeconds),
            Adapt(context));
    }
}

public sealed class ExteriorMaintenanceBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingExteriorMaintenanceAbility>
{
    private readonly DungeonStory.Buildings.ExteriorMaintenanceBuildingAbilityHandler core;

    public ExteriorMaintenanceBuildingAbilityHandler(
        DungeonStory.Buildings.ExteriorMaintenanceBuildingAbilityHandler core)
    {
        this.core = core ?? throw new ArgumentNullException(nameof(core));
    }

    protected override int Apply(
        BuildingExteriorMaintenanceAbility ability,
        BuildingAbilityWorkContext context)
    {
        return core.Apply(
            new ExteriorMaintenanceBuildingAbilityWork(
                ability.cleanlinessGain,
                ability.damageReduction),
            Adapt(context));
    }
}

public sealed class ModularBuildingCoreFacilityEffectsAdapter :
    IBuildingCoreFacilityEffectsPort
{
    public int ApplyProduction(
        IBuildingVisitorPort actor,
        IBuildingCoreWorkTargetPort target,
        ProductionBuildingAbilityWork ability,
        string workTypeId,
        float evolutionOutputMultiplier)
    {
        return ModularFacilityRuntimeEffects.ApplyProduction(
            actor,
            RequireBuilding(target),
            ability.AbilityId,
            ability.OutputCategory,
            ability.Amount,
            new WorkTypeId(workTypeId),
            evolutionOutputMultiplier);
    }

    public int ApplyCleaning(
        IBuildingVisitorPort actor,
        IBuildingCoreWorkTargetPort target,
        CleaningBuildingAbilityWork ability,
        string workTypeId)
    {
        return ModularFacilityRuntimeEffects.ApplyCleaning(
            actor,
            RequireBuilding(target),
            ability.RestoredCleanliness,
            new WorkTypeId(workTypeId));
    }

    public int ApplySecurity(
        IBuildingVisitorPort actor,
        IBuildingCoreWorkTargetPort target,
        SecurityBuildingAbilityWork ability,
        string workTypeId)
    {
        return ModularFacilityRuntimeEffects.ApplySecurity(
            actor,
            RequireBuilding(target),
            ability.AbilityId,
            ability.MaxAlarmCharges,
            ability.ChargesPerGuardWork,
            new WorkTypeId(workTypeId));
    }

    private static BuildableObject RequireBuilding(IBuildingCoreWorkTargetPort target)
    {
        return target is BuildingCoreWorkTargetAdapter adapter
            ? adapter.Building
            : throw new InvalidOperationException(
                "Building core effects require the Unity building target adapter.");
    }

}

internal sealed class BuildingCoreWorkTargetAdapter : IBuildingCoreWorkTargetPort
{
    public BuildingCoreWorkTargetAdapter(BuildableObject building)
    {
        Building = building ?? throw new ArgumentNullException(nameof(building));
    }

    internal BuildableObject Building { get; }
    private ExteriorZoneMarker Exterior => Building as ExteriorZoneMarker;

    public BuildingInstanceId BuildingInstanceId =>
        ((IBuildingWorldEntryPort)Building).BuildingInstanceId;
    public bool IsBuildingDestroyed => Building.isDestroy;
    public string DisplayName => Exterior != null
        ? Exterior.DisplayName
        : Building.BuildingData?.objectName ?? Building.name;
    public bool IsExteriorZone => Exterior != null;
    public string ExteriorZoneId => Exterior?.ZoneId ?? string.Empty;
    public float ReceptionReadiness => Exterior?.ReceptionReadiness ?? 0f;
    public float PatrolReadiness => Exterior?.PatrolReadiness ?? 0f;
    public float Cleanliness => Exterior?.Cleanliness ?? 0f;
    public float Damage => Exterior?.Damage ?? 0f;

    public void ApplyReceptionWork(float readinessGain, float impressionBonus) =>
        RequireExterior().ApplyReceptionWork(readinessGain, impressionBonus);

    public void ApplyPatrolWork(float readinessGain, float detectionBonus) =>
        RequireExterior().ApplyPatrolWork(readinessGain, detectionBonus);

    public void RecordOutdoorRest() => RequireExterior().RecordOutdoorRest();

    public void ApplyExteriorCleanWork(float amount) =>
        RequireExterior().ApplyExteriorCleanWork(amount);

    public void ApplyExteriorRepairWork(float amount) =>
        RequireExterior().ApplyExteriorRepairWork(amount);

    private ExteriorZoneMarker RequireExterior()
    {
        return Exterior ?? throw new InvalidOperationException(
            "Exterior building work requires an exterior-zone target.");
    }
}
