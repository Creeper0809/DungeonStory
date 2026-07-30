using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class SurgicalProcedureEffectHandler<T> :
    ISurgicalProcedureEffectHandler
    where T : SurgicalProcedureEffect
{
    public Type EffectType => typeof(T);

    public bool Apply(
        SurgeryOrder order,
        SurgicalProcedureEffect effect,
        BuildableObject facility,
        out string failureReason)
    {
        if (effect is not T typed)
        {
            failureReason = $"수술 효과 형식이 맞지 않습니다: {effect?.GetType().Name}";
            return false;
        }

        return ApplyTyped(order, typed, facility, out failureReason);
    }

    protected abstract bool ApplyTyped(
        SurgeryOrder order,
        T effect,
        BuildableObject facility,
        out string failureReason);
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
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order?.subject?.subjectId);
        if (character != null)
        {
            return anatomy.TryHealNode(
                character,
                order.targetNodeId,
                effect.health,
                effect.infectionReduction);
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            order?.subject?.subjectId);
        if (animal != null && animal.IsAlive)
        {
            return wildlifeAnatomy.TryHealNode(
                animal,
                order.targetNodeId,
                effect.health,
                effect.infectionReduction);
        }

        failureReason = "치료할 수술 대상을 찾을 수 없습니다.";
        return false;
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

    protected override bool ApplyTyped(
        SurgeryOrder order,
        RemoveSurgicalNodeEffect effect,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order?.subject == null || facility == null)
        {
            failureReason = "적출 대상 또는 해부 시설이 없습니다.";
            return false;
        }

        string nodeId = order.targetNodeId;
        Vector2Int outputPosition = facility.centerPos;
        if (order.subject.kind is SurgicalSubjectKind.HumanoidCorpse
            or SurgicalSubjectKind.WildlifeCorpse)
        {
            if (extractionLedger.IsExtracted(order.subject.subjectId, nodeId))
            {
                failureReason = "이미 적출한 부위입니다.";
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
                    out failureReason))
            {
                return false;
            }

            return extractionLedger.TryMarkExtracted(
                order.subject.subjectId,
                nodeId,
                out failureReason);
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
                    out failureReason))
            {
                return false;
            }

            return !effect.createExtractedPart
                || parts.TryCreateExtractedPart(
                    order.subject,
                    nodeId,
                    SurgicalPartKind.NaturalOrgan,
                    quality,
                    outputPosition,
                    out _,
                    out failureReason);
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            order.subject.subjectId);
        if (animal == null || !animal.IsAlive)
        {
            failureReason = "적출할 생체 대상을 찾을 수 없습니다.";
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
                out failureReason))
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
                out failureReason))
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

    public InstallSurgicalPartEffectHandler(
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy,
        ISurgicalPartRuntime parts)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
    }

    protected override bool ApplyTyped(
        SurgeryOrder order,
        InstallSurgicalPartEffect effect,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
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
            failureReason = "이식할 환자를 찾을 수 없습니다.";
            return false;
        }

        if (!parts.TryGet(
                order.selectedPartInstanceId,
                out SurgicalPartInstance selected))
        {
            failureReason = "선택한 장기 또는 보철을 찾을 수 없습니다.";
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
            failureReason = "이식할 신체 부위를 찾을 수 없습니다.";
            return false;
        }

        if (!parts.TryConsumeForInstallation(
                selected.partInstanceId,
                order.orderId,
                character != null
                    ? character.Identity?.PersistentId
                    : animal.WildlifeId,
                out SurgicalPartInstance consumed,
                out failureReason))
        {
            return false;
        }

        float efficiency = Mathf.Clamp(
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
                out failureReason);
        }

        if (current.missing)
        {
            return anatomy.TryInstallPart(
                character,
                order.targetNodeId,
                consumed.partInstanceId,
                effect.partKind,
                efficiency,
                out failureReason);
        }

        return anatomy.TryReplaceNodePart(
            character,
            order.targetNodeId,
            consumed.partInstanceId,
            effect.partKind,
            efficiency,
            out _,
            out failureReason);
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
        out string failureReason)
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
                out failureReason);
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
            out failureReason);
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
        out string failureReason)
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
                out failureReason);
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
            out failureReason);
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
