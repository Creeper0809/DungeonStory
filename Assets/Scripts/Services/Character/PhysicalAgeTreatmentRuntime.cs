using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using VContainer.Unity;

public interface IPhysicalAgeTreatmentService
{
    bool TryApplyWholeBodyRegeneration(
        CharacterId characterId,
        string medicalFacilityDestinationId,
        string treatmentItemId,
        out IReadOnlyList<AgeConditionChange> changes,
        out DomainFailure failure);
    bool TryActivateTemporalStasis(
        CharacterId characterId,
        string facilityDestinationId,
        string stasisSealItemId,
        out DomainFailure failure);
}

public interface ITemporalStasisMaintenanceService
{
    void RefreshDailyMaintenance();
}

/// <summary>
/// Consumes authored physical treatment supplies before changing the life
/// aggregate. All fallible reference validation occurs before consumption.
/// </summary>
public sealed class PhysicalAgeTreatmentRuntime :
    IPhysicalAgeTreatmentService,
    ITemporalStasisMaintenanceService
{
    public const string WholeBodyRegenerationProcedureId =
        "procedure:whole-body-regeneration";
    public const string TemporalStasisProcedureId =
        "procedure:temporal-stasis";
    public const string RuneConductorItemId = "component:rune-conductor";
    public const string ManaCrystalItemId = "resource:mana-crystal";
    public const float RequiredRunePower = 10f;

    private readonly IItemDefinitionCatalog items;
    private readonly IItemTransferService transfers;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterLifeCommand lifeCommands;
    private readonly ICharacterLifeDefinitionCatalog lifeDefinitions;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IGameCalendar calendar;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IPowerInfrastructureQuery power;

    public PhysicalAgeTreatmentRuntime(
        IItemDefinitionCatalog items,
        IItemTransferService transfers,
        ICharacterLifeQuery life,
        ICharacterLifeCommand lifeCommands,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        ICharacterWorldQuery characterWorld,
        IAnatomyHealthRuntime anatomy,
        IGameCalendar calendar,
        IBuildingWorldQuery buildingWorld,
        IPowerInfrastructureQuery power)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.lifeCommands = lifeCommands
            ?? throw new ArgumentNullException(nameof(lifeCommands));
        this.lifeDefinitions = lifeDefinitions
            ?? throw new ArgumentNullException(nameof(lifeDefinitions));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
    }

    public bool TryApplyWholeBodyRegeneration(
        CharacterId characterId,
        string medicalFacilityDestinationId,
        string treatmentItemId,
        out IReadOnlyList<AgeConditionChange> changes,
        out DomainFailure failure)
    {
        changes = Array.Empty<AgeConditionChange>();
        if (!characterId.IsValid
            || !life.TryGet(characterId, out CharacterLifeRecord lifeRecord))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCharacterMissing,
                characterId.Value);
            return false;
        }

        ItemDefinitionId definitionId = (ItemDefinitionId)(
            treatmentItemId?.Trim() ?? string.Empty);
        if (!definitionId.IsValid
            || !items.TryGet(definitionId, out ItemDefinitionSO definition)
            || definition is not ResourceItemDefinitionSO treatment)
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentDefinitionMissing,
                definitionId.Value);
            return false;
        }

        if (!string.Equals(
                treatment.MedicalProcedureId,
                WholeBodyRegenerationProcedureId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentProcedureMismatch,
                definitionId.Value,
                treatment.MedicalProcedureId);
            return false;
        }

        CharacterActor actor = characterWorld.Characters.FirstOrDefault(candidate =>
            candidate != null && !candidate.IsDead
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && id.Equals(characterId));
        if (actor == null)
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCharacterMissing,
                characterId.Value);
            return false;
        }

        if (!TryBuildRegenerationRepairs(
                actor,
                lifeRecord,
                out Dictionary<string, float> repairs,
                out failure))
        {
            return false;
        }

        string destinationId = medicalFacilityDestinationId?.Trim()
            ?? string.Empty;
        if (!transfers.TryConsumeFacilityItemBuffer(
                destinationId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [definitionId.Value] = 1
                },
                out string consumeFailure))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentSupplyUnavailable,
                destinationId,
                definitionId.Value,
                consumeFailure ?? string.Empty);
            return false;
        }

        changes = lifeCommands.ApplyWholeBodyRegeneration(characterId);
        foreach ((string nodeId, float health) in repairs
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            AnatomyHealthSnapshot current = anatomy.GetAnatomySnapshot(actor);
            AnatomyNodeHealthState node = current.Nodes.First(value =>
                string.Equals(value.nodeId, nodeId, StringComparison.Ordinal));
            if (node.currentHealth >= node.maxHealth || health <= 0f)
            {
                continue;
            }
            if (!anatomy.TryHealNode(
                    actor,
                    nodeId,
                    Math.Min(health, node.maxHealth - node.currentHealth),
                    infectionReduction: 0f))
            {
                throw new InvalidOperationException(
                    $"Whole-body regeneration could not repair anatomy node '{nodeId}' for '{characterId.Value}'.");
            }
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryActivateTemporalStasis(
        CharacterId characterId,
        string facilityDestinationId,
        string stasisSealItemId,
        out DomainFailure failure)
    {
        if (!characterId.IsValid || !life.TryGet(characterId, out _))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCharacterMissing,
                characterId.Value);
            return false;
        }

        ItemDefinitionId definitionId = (ItemDefinitionId)(
            stasisSealItemId?.Trim() ?? string.Empty);
        if (!definitionId.IsValid
            || !items.TryGet(definitionId, out ItemDefinitionSO definition)
            || definition is not ResourceItemDefinitionSO seal)
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentDefinitionMissing,
                definitionId.Value);
            return false;
        }

        if (!string.Equals(
                seal.MedicalProcedureId,
                TemporalStasisProcedureId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentProcedureMismatch,
                definitionId.Value,
                seal.MedicalProcedureId);
            return false;
        }

        string facilityId = facilityDestinationId?.Trim() ?? string.Empty;
        BuildableObject facility = FindFacility(facilityId);
        if (facility == null)
        {
            failure = new DomainFailure(
                FailureCode.TemporalStasisFacilityMissing,
                facilityId);
            return false;
        }

        if (!HasRequiredPower(facility))
        {
            failure = new DomainFailure(
                FailureCode.TemporalStasisPowerInsufficient,
                facilityId,
                RequiredRunePower.ToString("0"));
            return false;
        }

        if (!transfers.TryConsumeFacilityItemBuffer(
                facilityId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [definitionId.Value] = 1
                },
                out string consumeFailure))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentSupplyUnavailable,
                facilityId,
                definitionId.Value,
                consumeFailure ?? string.Empty);
            return false;
        }

        lifeCommands.ConfigureTemporalStasis(
            characterId,
            facilityId,
            operational: true,
            nextMaintenanceAbsoluteDay:
                calendar.Day + GameCalendarRules.DaysPerSeason);
        failure = DomainFailure.None;
        return true;
    }

    public void RefreshDailyMaintenance()
    {
        CharacterLifeRecord[] assignments = life.Records
            .Where(value => value.RequestedAgingCareMode
                == AgingCareMode.TemporalStasis)
            .OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
            .ToArray();
        foreach (CharacterLifeRecord record in assignments)
        {
            string facilityId = record.TemporalStasisFacilityId;
            BuildableObject facility = FindFacility(facilityId);
            bool operational = facility != null && HasRequiredPower(facility);
            int nextMaintenance =
                record.TemporalStasisNextMaintenanceAbsoluteDay;
            if (operational && calendar.Day >= nextMaintenance)
            {
                operational = transfers.TryConsumeFacilityItemBuffer(
                    facilityId,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [RuneConductorItemId] = 1,
                        [ManaCrystalItemId] = 1
                    },
                    out _);
                if (operational)
                {
                    nextMaintenance = calendar.Day
                        + GameCalendarRules.DaysPerSeason;
                }
            }

            lifeCommands.ConfigureTemporalStasis(
                record.CharacterId,
                facilityId,
                operational,
                nextMaintenance);
        }
    }

    private BuildableObject FindFacility(string facilityId)
    {
        return buildingWorld.Buildings.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                facilityId,
                StringComparison.Ordinal));
    }

    private bool HasRequiredPower(BuildableObject facility)
    {
        return facility != null
            && power.TryGetNode(facility, out PowerNodeSnapshot node)
            && node.Powered
            && node.DemandPerSecond >= RequiredRunePower
            && node.SuppliedFraction >= 0.999f;
    }

    private bool TryBuildRegenerationRepairs(
        CharacterActor actor,
        CharacterLifeRecord lifeRecord,
        out Dictionary<string, float> repairs,
        out DomainFailure failure)
    {
        repairs = new Dictionary<string, float>(StringComparer.Ordinal);
        AnatomyHealthSnapshot anatomySnapshot = anatomy.GetAnatomySnapshot(actor);
        Dictionary<string, AnatomyNodeHealthState> anatomyNodes = anatomySnapshot.Nodes
            .Where(node => node != null)
            .ToDictionary(node => node.nodeId, StringComparer.Ordinal);

        foreach (CharacterAgeConditionState condition in lifeRecord.AgeConditions
                     .Where(value => value.Severity <= AgeConditionSeverity.Severe)
                     .OrderBy(value => value.ConditionId, StringComparer.Ordinal))
        {
            AgeConditionDefinition definition = lifeDefinitions.RequireAgeCondition(
                condition.ConditionId);
            string[] matchingNodeIds = definition.AffectedAnatomyNodeIds
                .Where(anatomyNodes.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (matchingNodeIds.Length == 0
                || matchingNodeIds.Any(nodeId => anatomyNodes[nodeId].missing))
            {
                failure = new DomainFailure(
                    FailureCode.AgeTreatmentAnatomyUnavailable,
                    lifeRecord.CharacterId.Value,
                    condition.ConditionId,
                    anatomySnapshot.ProfileId);
                return false;
            }

            float repairedFraction = condition.Severity switch
            {
                AgeConditionSeverity.Mild => 0.05f,
                AgeConditionSeverity.Moderate => 0.15f,
                AgeConditionSeverity.Severe => 0.30f,
                _ => 0f
            };
            foreach (string nodeId in matchingNodeIds)
            {
                float health = anatomyNodes[nodeId].maxHealth * repairedFraction;
                repairs[nodeId] = repairs.TryGetValue(nodeId, out float existing)
                    ? existing + health
                    : health;
            }
        }

        failure = DomainFailure.None;
        return true;
    }
}

public sealed class TemporalStasisMaintenanceAdapter : IStartable, IDisposable
{
    private readonly ITemporalStasisMaintenanceService maintenance;
    private readonly ICharacterLifeQuery life;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly IGameEventBus events;
    private IDisposable dayStartedSubscription;
    private IDisposable dayEndedSubscription;

    public TemporalStasisMaintenanceAdapter(
        ITemporalStasisMaintenanceService maintenance,
        ICharacterLifeQuery life,
        IMilestoneGameplayModifierQuery milestoneModifiers,
        IGameEventBus events)
    {
        this.maintenance = maintenance
            ?? throw new ArgumentNullException(nameof(maintenance));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.milestoneModifiers = milestoneModifiers
            ?? throw new ArgumentNullException(nameof(milestoneModifiers));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        dayStartedSubscription = events.Subscribe<OperatingDayStartedEvent>(
            OnDayStarted);
        dayEndedSubscription = events.Subscribe<OperatingDayEndedEvent>(
            _ => maintenance.RefreshDailyMaintenance());
    }

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        int warningDays = Math.Max(
            0,
            milestoneModifiers.TemporalStasisWarningDays);
        if (warningDays == 0)
        {
            return;
        }

        foreach (CharacterLifeRecord record in life.Records
                     .Where(value => value != null
                         && value.RequestedAgingCareMode
                             == AgingCareMode.TemporalStasis)
                     .OrderBy(value => value.CharacterId.Value,
                         StringComparer.Ordinal))
        {
            int remaining = record.TemporalStasisNextMaintenanceAbsoluteDay
                - started.day;
            if (remaining <= 0 || remaining > warningDays)
            {
                continue;
            }

            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "시간 고정 유지보수 예고",
                $"{record.CharacterId.Value}의 시간 고정 촉매 교체까지 {remaining}일 남았습니다. "
                    + $"{PhysicalAgeTreatmentRuntime.RuneConductorItemId}와 "
                    + $"{PhysicalAgeTreatmentRuntime.ManaCrystalItemId}을 시설 버퍼에 준비해야 합니다.",
                EventAlertImportance.High,
                "V21 시간 고정",
                sourceId: $"temporal-stasis-maintenance-warning:{record.CharacterId.Value}")));
        }
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }
}
