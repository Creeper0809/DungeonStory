using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class SurgicalProcedureEffectHandler<T> :
    ISurgicalProcedureEffectHandler
    where T : SurgicalProcedureEffect
{
    public Type EffectType => typeof(T);

    public bool CanApply(
        SurgeryOrder order,
        SurgicalProcedureEffect effect,
        out DomainFailure failure)
    {
        if (effect is not T typed)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                effect?.GetType().Name ?? string.Empty);
            return false;
        }

        return CanApplyTyped(order, typed, out failure);
    }

    public bool Apply(
        SurgeryOrder order,
        SurgicalProcedureEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (effect is not T typed)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                effect?.GetType().Name ?? string.Empty);
            return false;
        }

        if (!CanApplyTyped(order, typed, out failure))
        {
            return false;
        }

        return ApplyTyped(order, typed, facility, out failure);
    }

    protected virtual bool CanApplyTyped(
        SurgeryOrder order,
        T effect,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        return true;
    }

    protected abstract bool ApplyTyped(
        SurgeryOrder order,
        T effect,
        BuildableObject facility,
        out DomainFailure failure);
}

public sealed class HealSurgicalNodeEffectHandler :
    SurgicalProcedureEffectHandler<HealSurgicalNodeEffect>
{
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;

    public HealSurgicalNodeEffectHandler(
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        HealSurgicalNodeEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order.subject.kind == SurgicalSubjectKind.Character)
        {
            CharacterActor character = SurgicalSubjectResolver.FindCharacter(
                characters,
                order.subject.subjectId);
            anatomy.TryHealNode(
                character,
                order.targetNodeId,
                effect.health,
                effect.infectionReduction);
            return true;
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            order.subject.subjectId);
        wildlifeAnatomy.TryHealNode(
            animal,
            order.targetNodeId,
            effect.health,
            effect.infectionReduction);
        return true;
    }

    protected override bool CanApplyTyped(
        SurgeryOrder order,
        HealSurgicalNodeEffect effect,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order?.subject?.kind == SurgicalSubjectKind.Character)
        {
            CharacterActor character = SurgicalSubjectResolver.FindCharacter(
                characters,
                order.subject.subjectId);
            if (character == null || character.IsDead)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryLivingSubjectUnavailable,
                    order.subject.subjectId);
                return false;
            }

            if (HasAvailableNode(
                    anatomy.GetAnatomySnapshot(character),
                    order.targetNodeId))
            {
                return true;
            }

            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeUnavailable,
                order.targetNodeId);
            return false;
        }

        if (order?.subject?.kind == SurgicalSubjectKind.Wildlife)
        {
            WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
                wildlife,
                order.subject.subjectId);
            if (animal == null || !animal.IsAlive)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryWildlifeSubjectUnavailable,
                    order.subject.subjectId);
                return false;
            }

            if (HasAvailableNode(
                    wildlifeAnatomy.GetAnatomySnapshot(animal),
                    order.targetNodeId))
            {
                return true;
            }

            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeUnavailable,
                order.targetNodeId);
            return false;
        }

        failure = new DomainFailure(
            FailureCode.SurgeryLivingSubjectUnavailable,
            order?.subject?.subjectId ?? string.Empty);
        return false;
    }

    private static bool HasAvailableNode(
        AnatomyHealthSnapshot snapshot,
        string nodeId)
    {
        return snapshot.Nodes.Any(node => node != null
            && !node.missing
            && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal));
    }
}

public sealed class RecoverBloodLossEffectHandler :
    SurgicalProcedureEffectHandler<RecoverBloodLossEffect>
{
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterBodyHealthCommand bodyCommands;

    public RecoverBloodLossEffectHandler(
        ICharacterWorldQuery characters,
        ICharacterBodyHealthQuery bodyHealth,
        ICharacterBodyHealthCommand bodyCommands)
    {
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.bodyCommands = bodyCommands
            ?? throw new ArgumentNullException(nameof(bodyCommands));
    }

    protected override bool CanApplyTyped(
        SurgeryOrder order,
        RecoverBloodLossEffect effect,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order?.subject?.kind != SurgicalSubjectKind.Character)
        {
            failure = new DomainFailure(
                FailureCode.SurgerySubjectKindUnsupported,
                order?.subject?.kind.ToString() ?? string.Empty);
            return false;
        }

        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        if (character == null || character.IsDead)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryLivingSubjectUnavailable,
                order.subject.subjectId);
            return false;
        }

        string actualSpeciesId = character.Identity?.SpeciesTag?.Trim()
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(order.subject.speciesId)
            || !string.Equals(
                actualSpeciesId,
                order.subject.speciesId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            failure = new DomainFailure(
                FailureCode.SurgerySpeciesUnsupported,
                order.subject.speciesId ?? string.Empty);
            return false;
        }

        if (effect == null
            || float.IsNaN(effect.amount)
            || float.IsInfinity(effect.amount)
            || effect.amount <= 0f)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                effect?.EffectId ?? nameof(RecoverBloodLossEffect),
                "blood-loss-recovery-invalid");
            return false;
        }

        if (bodyHealth.GetSnapshot(character).BloodLoss <= 0f)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                effect?.EffectId ?? nameof(RecoverBloodLossEffect),
                "blood-loss-empty");
            return false;
        }

        return true;
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        RecoverBloodLossEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        bool applied = bodyCommands.ApplyTreatment(
            character,
            partHealthAmount: 0f,
            bloodLossReduction: effect.amount);
        failure = applied
            ? DomainFailure.None
            : new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                effect.EffectId,
                "blood-loss-recovery-not-applied");
        return applied;
    }
}

public sealed class StopSurgicalNodeBleedingEffectHandler :
    SurgicalProcedureEffectHandler<StopSurgicalNodeBleedingEffect>
{
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;

    public StopSurgicalNodeBleedingEffectHandler(
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy)
    {
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife
            ?? throw new ArgumentNullException(nameof(wildlife));
        this.anatomy = anatomy
            ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
    }

    protected override bool CanApplyTyped(
        SurgeryOrder order,
        StopSurgicalNodeBleedingEffect effect,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order?.subject?.kind == SurgicalSubjectKind.Character)
        {
            CharacterActor character = SurgicalSubjectResolver.FindCharacter(
                characters,
                order.subject.subjectId);
            if (character == null || character.IsDead)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryLivingSubjectUnavailable,
                    order.subject.subjectId);
                return false;
            }

            if (HasAvailableNode(
                    anatomy.GetAnatomySnapshot(character),
                    order.targetNodeId))
            {
                return true;
            }

            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeUnavailable,
                order.targetNodeId);
            return false;
        }

        if (order?.subject?.kind == SurgicalSubjectKind.Wildlife)
        {
            WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
                wildlife,
                order.subject.subjectId);
            if (animal == null || !animal.IsAlive)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryWildlifeSubjectUnavailable,
                    order.subject.subjectId);
                return false;
            }

            if (HasAvailableNode(
                    wildlifeAnatomy.GetAnatomySnapshot(animal),
                    order.targetNodeId))
            {
                return true;
            }

            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeUnavailable,
                order.targetNodeId);
            return false;
        }

        failure = new DomainFailure(
            FailureCode.SurgerySubjectKindUnsupported,
            order?.subject?.kind.ToString() ?? string.Empty);
        return false;
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        StopSurgicalNodeBleedingEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (order.subject.kind == SurgicalSubjectKind.Character)
        {
            CharacterActor character = SurgicalSubjectResolver.FindCharacter(
                characters,
                order.subject.subjectId);
            return anatomy.TryStopBleeding(
                character,
                order.targetNodeId,
                out failure);
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            order.subject.subjectId);
        return wildlifeAnatomy.TryStopBleeding(
            animal,
            order.targetNodeId,
            out failure);
    }

    private static bool HasAvailableNode(
        AnatomyHealthSnapshot snapshot,
        string nodeId)
    {
        return snapshot.Nodes.Any(node => node != null
            && !node.missing
            && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal));
    }
}

public sealed class MaintainSurgicalPartEffectHandler :
    SurgicalProcedureEffectHandler<MaintainSurgicalPartEffect>
{
    private readonly ICharacterWorldQuery characters;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly ICharacterSpeciesQuery speciesQuery;
    private readonly ICharacterSpeciesCommand speciesCommands;

    public MaintainSurgicalPartEffectHandler(
        ICharacterWorldQuery characters,
        IAnatomyHealthRuntime anatomy,
        ICharacterSpeciesQuery speciesQuery,
        ICharacterSpeciesCommand speciesCommands)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.speciesQuery = speciesQuery
            ?? throw new ArgumentNullException(nameof(speciesQuery));
        this.speciesCommands = speciesCommands
            ?? throw new ArgumentNullException(nameof(speciesCommands));
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        MaintainSurgicalPartEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order?.subject?.subjectId);
        bool isPowerCore = string.Equals(
            order?.procedureId,
            "procedure:golem-power-core",
            StringComparison.Ordinal);
        CharacterId characterId = (CharacterId)(order?.subject?.subjectId
            ?? string.Empty);
        if (isPowerCore
            && (!speciesQuery.TryGet(characterId, out CharacterSpeciesRuntimeState state)
                || !state.SpeciesId.Equals(new CharacterSpeciesId("Golem"))))
        {
            failure = new DomainFailure(
                FailureCode.CharacterSpeciesRepairUnsupported,
                characterId.Value);
            return false;
        }
        if (!anatomy.TryMaintainNode(
            character,
            order?.targetNodeId,
            effect.durability,
            effect.contaminationReduction,
            out failure))
            return false;
        if (!isPowerCore)
            return true;
        if (!anatomy.TryReduceNodeBurden(
                character,
                order.targetNodeId,
                30f,
                0f,
                0f,
                out failure))
            return false;
        return speciesCommands.RepairIntegrity(
            characterId,
            30f,
            out failure);
    }
}

public sealed class ApplyAgeTreatmentEffectHandler :
    SurgicalProcedureEffectHandler<ApplyAgeTreatmentEffect>
{
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterLifeCommand commands;
    private readonly IGameCalendar calendar;
    private readonly IPowerInfrastructureQuery power;

    public ApplyAgeTreatmentEffectHandler(
        ICharacterLifeQuery life,
        ICharacterLifeCommand commands,
        IGameCalendar calendar,
        IPowerInfrastructureQuery power)
    {
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        ApplyAgeTreatmentEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        CharacterId characterId = (CharacterId)(order?.subject?.subjectId
            ?? string.Empty);
        if (!characterId.IsValid || !life.TryGet(characterId, out _))
        {
            failure = new DomainFailure(
                FailureCode.AgeTreatmentCharacterMissing,
                characterId.Value);
            return false;
        }

        switch (effect.treatment)
        {
            case AgeTreatmentEffectKind.OrganRegeneration:
                commands.ReduceAgeConditions(characterId, severityLevels: 2);
                break;
            case AgeTreatmentEffectKind.BloodRejuvenation:
                if (!commands.TryApplyBloodRejuvenation(
                        characterId,
                        calendar.Day,
                        out failure))
                {
                    return false;
                }
                break;
            case AgeTreatmentEffectKind.RuneHibernation:
                commands.ConfigureLongTermCare(
                    characterId,
                    geriatricMedicineActive: false,
                    chronicCareActive: false,
                    AgingCareMode.RuneHibernation);
                break;
            case AgeTreatmentEffectKind.WholeBodyRegeneration:
                commands.ApplyWholeBodyRegeneration(characterId);
                break;
            case AgeTreatmentEffectKind.TemporalStasis:
                if (facility == null
                    || !power.TryGetNode(facility, out PowerNodeSnapshot node)
                    || !node.Powered
                    || node.SuppliedFraction < 0.999f)
                {
                    failure = new DomainFailure(
                        FailureCode.TemporalStasisPowerInsufficient,
                        facility?.PersistentInstanceId.Value ?? string.Empty,
                        PhysicalAgeTreatmentRuntime.RequiredRunePower.ToString("0"));
                    return false;
                }
                commands.ConfigureTemporalStasis(
                    characterId,
                    facility.RequirePersistentInstanceId().Value,
                    operational: true,
                    nextMaintenanceAbsoluteDay:
                        calendar.Day + GameCalendarRules.DaysPerSeason);
                break;
            default:
                failure = new DomainFailure(
                    FailureCode.SurgeryEffectFailed,
                    effect.treatment.ToString());
                return false;
        }

        failure = DomainFailure.None;
        return true;
    }
}

public sealed class RemoveSurgicalNodeEffectHandler :
    SurgicalProcedureEffectHandler<RemoveSurgicalNodeEffect>
{
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;
    private readonly ISurgicalPartRuntime parts;
    private readonly ISurgeryExtractionLedger extractionLedger;

    public RemoveSurgicalNodeEffectHandler(
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy,
        ISurgicalPartRuntime parts,
        ISurgeryExtractionLedger extractionLedger)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        this.extractionLedger = extractionLedger
            ?? throw new ArgumentNullException(nameof(extractionLedger));
    }

    protected override bool CanApplyTyped(
        SurgeryOrder order,
        RemoveSurgicalNodeEffect effect,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order?.subject?.kind != SurgicalSubjectKind.Character)
        {
            return true;
        }

        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        AnatomyNodeHealthState node = character != null
            ? anatomy.GetAnatomySnapshot(character).Nodes.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.nodeId,
                    order.targetNodeId,
                    StringComparison.Ordinal))
            : null;
        if (node == null || string.IsNullOrWhiteSpace(node.installedPartId))
        {
            return true;
        }

        failure = new DomainFailure(
            FailureCode.SurgeryTargetNodeUnavailable,
            node.nodeId);
        return false;
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        RemoveSurgicalNodeEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order?.subject == null || facility == null)
        {
            failure = new DomainFailure(FailureCode.SurgeryFacilityOrProcedureMissing);
            return false;
        }

        string nodeId = order.targetNodeId;
        Vector2Int outputPosition = facility.centerPos;
        if (order.subject.kind is SurgicalSubjectKind.HumanoidCorpse
            or SurgicalSubjectKind.WildlifeCorpse)
        {
            if (extractionLedger.IsExtracted(order.subject.subjectId, nodeId))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryNodeAlreadyExtracted,
                    order.subject.subjectId,
                    nodeId);
                return false;
            }

            if (effect.createExtractedPart
                && !parts.TryCreateExtractedPart(
                    order.subject,
                    nodeId,
                    SurgicalPartKind.NaturalOrgan,
                    0.75f,
                    outputPosition,
                    out _,
                    out failure))
            {
                return false;
            }

            return extractionLedger.TryMarkExtracted(
                order.subject.subjectId,
                nodeId,
                out failure);
        }

        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        if (character != null)
        {
            AnatomyHealthSnapshot before = anatomy.GetAnatomySnapshot(character);
            AnatomyNodeHealthState node = before.Nodes.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.nodeId, nodeId, StringComparison.Ordinal));
            float quality = node?.HealthRatio ?? 0.5f;
            if (!anatomy.TryRemoveNode(
                    character,
                    nodeId,
                    out _,
                    out failure))
            {
                return false;
            }

            if (!effect.createExtractedPart)
            {
                return true;
            }

            bool created = parts.TryCreateExtractedPart(
                order.subject,
                nodeId,
                SurgicalPartKind.NaturalOrgan,
                quality,
                outputPosition,
                out _,
                out failure);
            return created;
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            order.subject.subjectId);
        if (animal == null || !animal.IsAlive)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryWildlifeSubjectUnavailable,
                order.subject.subjectId);
            return false;
        }

        AnatomyNodeHealthState animalNode = wildlifeAnatomy
            .GetAnatomySnapshot(animal)
            .Nodes
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(candidate.nodeId, nodeId, StringComparison.Ordinal));
        float animalQuality = animalNode?.HealthRatio
            ?? animal.CurrentHealth / Mathf.Max(1f, animal.MaxHealth);
        if (!wildlifeAnatomy.TryRemoveNode(
                animal,
                nodeId,
                out _,
                out failure))
        {
            return false;
        }

        if (effect.createExtractedPart
            && !parts.TryCreateExtractedPart(
                order.subject,
                nodeId,
                SurgicalPartKind.NaturalOrgan,
                animalQuality,
                outputPosition,
                out _,
                out failure))
        {
            return false;
        }

        return true;
    }
}

public sealed class InstallSurgicalPartEffectHandler :
    SurgicalProcedureEffectHandler<InstallSurgicalPartEffect>
{
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;
    private readonly ISurgicalPartRuntime parts;
    private readonly ISurgicalPartReplacementRuntime replacementParts;
    private readonly IFacilityBufferMassAdmissionService outputAdmission;
    private readonly IFacilityBufferPlannedOutputPublicationService
        outputPublication;

    public InstallSurgicalPartEffectHandler(
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy,
        ISurgicalPartRuntime parts,
        IFacilityBufferMassAdmissionService outputAdmission,
        IFacilityBufferPlannedOutputPublicationService outputPublication)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        replacementParts = parts as ISurgicalPartReplacementRuntime
            ?? throw new ArgumentException(
                "Surgical part runtime lacks replacement authority.",
                nameof(parts));
        this.outputAdmission = outputAdmission
            ?? throw new ArgumentNullException(nameof(outputAdmission));
        this.outputPublication = outputPublication
            ?? throw new ArgumentNullException(nameof(outputPublication));
    }

    protected override bool CanApplyTyped(
        SurgeryOrder order,
        InstallSurgicalPartEffect effect,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order?.subject?.subjectId);
        WildlifeActor animal = character == null
            ? SurgicalSubjectResolver.FindWildlife(
                wildlife,
                order?.subject?.subjectId)
            : null;
        if (character == null && animal == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryLivingSubjectUnavailable,
                order?.subject?.subjectId ?? string.Empty);
            return false;
        }
        // Planning validates effect compatibility before an order ID and its
        // concrete selected part reservation exist. The completed-work
        // preflight below supplies both and performs the exact physical check.
        if (string.IsNullOrEmpty(order?.orderId))
        {
            return true;
        }
        bool commitForward = order.replacementPhase is
            SurgicalPartReplacementPhase.BodyCommitted
            or SurgicalPartReplacementPhase.OutputPublished;
        if (!parts.TryGet(
                order.selectedPartInstanceId,
                out SurgicalPartInstance selected)
            || commitForward
                && !string.Equals(
                    order.replacementIncomingPartId,
                    order.selectedPartInstanceId,
                    StringComparison.Ordinal)
            || !commitForward
                && !parts.TryValidateReservationForOrder(
                    order.selectedPartInstanceId,
                    order.orderId,
                    out failure))
        {
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    order?.selectedPartInstanceId ?? string.Empty);
            }
            return false;
        }
        string requiredItemId = effect.requiredItemDefinitionId?.Trim()
            ?? string.Empty;
        if (selected.kind != effect.partKind)
        {
            failure = new DomainFailure(FailureCode.SurgeryPartKindMismatch);
            return false;
        }
        if (requiredItemId.Length > 0
            && !string.Equals(
                selected.itemDefinitionId,
                requiredItemId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                selected.itemDefinitionId,
                requiredItemId);
            return false;
        }

        AnatomyNodeHealthState current = (character != null
                ? anatomy.GetAnatomySnapshot(character)
                : wildlifeAnatomy.GetAnatomySnapshot(animal))
            .Nodes
            .FirstOrDefault(node => node != null
                && string.Equals(
                    node.nodeId,
                    order.targetNodeId,
                    StringComparison.Ordinal));
        if (current == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeMissing,
                order.targetNodeId);
            return false;
        }
        if (character == null && selected.detachedDurabilityMaximum > 0f)
        {
            failure = new DomainFailure(
                FailureCode.SurgerySubjectKindUnsupported,
                selected.partInstanceId,
                "recovered-part-reinstall-character-only");
            return false;
        }
        if (order.replacementPhase != SurgicalPartReplacementPhase.None
            && character == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgerySubjectKindUnsupported,
                order.subject.subjectId,
                "replacement-recovery-character-only");
            return false;
        }
        return true;
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        InstallSurgicalPartEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order?.subject?.subjectId);
        WildlifeActor animal = character == null
            ? SurgicalSubjectResolver.FindWildlife(
                wildlife,
                order?.subject?.subjectId)
            : null;
        if (character == null && animal == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryLivingSubjectUnavailable,
                order?.subject?.subjectId ?? string.Empty);
            return false;
        }

        if (!parts.TryGet(
                order.selectedPartInstanceId,
                out SurgicalPartInstance selected))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                order?.selectedPartInstanceId ?? string.Empty);
            return false;
        }

        AnatomyNodeHealthState current = (character != null
                ? anatomy.GetAnatomySnapshot(character)
                : wildlifeAnatomy.GetAnatomySnapshot(animal))
            .Nodes
            .FirstOrDefault(node => node != null
                && string.Equals(
                    node.nodeId,
                    order.targetNodeId,
                    StringComparison.Ordinal));
        if (current == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeMissing,
                order.targetNodeId);
            return false;
        }

        if (character == null && selected.detachedDurabilityMaximum > 0f)
        {
            failure = new DomainFailure(
                FailureCode.SurgerySubjectKindUnsupported,
                selected.partInstanceId,
                "recovered-part-reinstall-character-only");
            return false;
        }

        float efficiency = Mathf.Clamp(
            effect.efficiency * selected.quality,
            0.1f,
            1.5f);
        if (order.replacementPhase != SurgicalPartReplacementPhase.None)
        {
            if (character == null)
            {
                failure = new DomainFailure(
                    FailureCode.SurgerySubjectKindUnsupported,
                    order.subject.subjectId,
                    "replacement-recovery-character-only");
                return false;
            }
            return replacementParts.TryCommitReplacement(
                order,
                character,
                effect.partKind,
                efficiency,
                anatomy,
                outputAdmission,
                outputPublication,
                out failure);
        }

        if (!parts.TryConsumeForInstallation(
                selected.partInstanceId,
                order.orderId,
                character != null
                    ? character.Identity?.PersistentId
                    : animal.WildlifeId,
                out SurgicalPartInstance consumed,
                out failure))
        {
            return false;
        }

        efficiency = Mathf.Clamp(
            effect.efficiency * consumed.quality,
            0.1f,
            1.5f);
        if (character == null)
        {
            return wildlifeAnatomy.TryInstallPart(
                animal,
                order.targetNodeId,
                consumed.partInstanceId,
                effect.partKind,
                efficiency,
                out failure);
        }

        if (current.missing)
        {
            return anatomy.TryInstallPart(
                character,
                order.targetNodeId,
                consumed.partInstanceId,
                effect.partKind,
                efficiency,
                consumed.detachedDurabilityCurrent,
                consumed.detachedDurabilityMaximum > 0f,
                out failure);
        }

        bool replaced = anatomy.TryReplaceNodePart(
            character,
            order.targetNodeId,
            current.installedPartId,
            current.currentHealth,
            current.maxHealth,
            consumed.partInstanceId,
            effect.partKind,
            efficiency,
            consumed.detachedDurabilityCurrent,
            consumed.detachedDurabilityMaximum > 0f,
            out _,
            out failure);
        if (replaced)
        {
            character.Stats.RefreshDerivedMaximumHealthPreservingCurrent();
        }
        return replaced;
    }
}

public sealed class ApplySurgicalBurdenEffectHandler :
    SurgicalProcedureEffectHandler<ApplySurgicalBurdenEffect>
{
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;
    private readonly ISurgicalFacilityQuery facilities;

    public ApplySurgicalBurdenEffectHandler(
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy,
        ISurgicalFacilityQuery facilities)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        ApplySurgicalBurdenEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        SurgicalFacilitySnapshot snapshot = facilities.Evaluate(
            facility,
            SurgeryFacilityTag.None);
        float rejectionReduction = snapshot.SupportFacilities
            .Append(snapshot.PrimaryFacility)
            .Where(building => building != null)
            .Select(building => building.BuildingData?
                .GetAbility<BuildingTransplantSupportAbility>())
            .Where(ability => ability != null && ability.immuneControl)
            .Select(ability => ability.rejectionReduction)
            .DefaultIfEmpty(0f)
            .Max();
        float rejection = effect.rejection
            * (1f - Mathf.Clamp01(rejectionReduction));
        float mutation = effect.mutation;
        float minimumMutation = snapshot.SupportFacilities
            .Append(snapshot.PrimaryFacility)
            .Where(building => building != null)
            .Select(building => building.BuildingData?
                .GetAbility<BuildingArcaneSurgeryAbility>())
            .Where(ability => ability != null)
            .Select(ability => ability.minimumMutationRisk * 100f)
            .DefaultIfEmpty(0f)
            .Max();
        if (minimumMutation > 0f)
        {
            mutation = Mathf.Max(mutation, minimumMutation);
        }

        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order?.subject?.subjectId);
        if (character != null)
        {
            return anatomy.TryAddNodeBurden(
                character,
                order?.targetNodeId,
                rejection,
                mutation,
                effect.infection,
                out failure);
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            order?.subject?.subjectId);
        return wildlifeAnatomy.TryAddNodeBurden(
            animal,
            order?.targetNodeId,
            rejection,
            mutation,
            effect.infection,
            out failure);
    }
}

public sealed class ReduceSurgicalBurdenEffectHandler :
    SurgicalProcedureEffectHandler<ReduceSurgicalBurdenEffect>
{
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;

    public ReduceSurgicalBurdenEffectHandler(
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        ReduceSurgicalBurdenEffect effect,
        BuildableObject facility,
        out DomainFailure failure)
    {
        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order?.subject?.subjectId);
        if (character != null)
        {
            return anatomy.TryReduceNodeBurden(
                character,
                order?.targetNodeId,
                effect.rejection,
                effect.mutation,
                effect.infection,
                out failure);
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            order?.subject?.subjectId);
        return wildlifeAnatomy.TryReduceNodeBurden(
            animal,
            order?.targetNodeId,
            effect.rejection,
            effect.mutation,
            effect.infection,
            out failure);
    }
}

internal static class SurgicalSubjectResolver
{
    public static CharacterActor FindCharacter(
        ICharacterWorldQuery query,
        string subjectId)
    {
        return query?.Characters?.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.Identity?.PersistentId,
                subjectId,
                StringComparison.Ordinal));
    }

    public static WildlifeActor FindWildlife(
        IWildlifeWorldQuery query,
        string subjectId)
    {
        return query?.Wildlife?.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.WildlifeId,
                subjectId,
                StringComparison.Ordinal));
    }
}
