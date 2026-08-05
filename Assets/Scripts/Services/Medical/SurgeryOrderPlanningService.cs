using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class SurgeryOrderPlanningService
{
    private const string CleanWaterItemId = "resource:clean-water";
    private const string ManaCrystalItemId = "resource:mana-crystal";

    private readonly SurgeryContentServices content;
    private readonly SurgeryWorldServices world;
    private readonly SurgeryResourceServices resources;

    public SurgeryOrderPlanningService(
        SurgeryContentServices content,
        SurgeryWorldServices world,
        SurgeryResourceServices resources)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
    }

    public bool RequiresInstalledPart(SurgicalProcedureSO procedure)
    {
        return procedure?.Kind is SurgicalProcedureKind.TransplantOrgan
            or SurgicalProcedureKind.InstallProsthetic
            or SurgicalProcedureKind.InstallImplant
            or SurgicalProcedureKind.ArcaneModification;
    }

    public bool ValidateSelectedPart(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        string targetNodeId,
        string partInstanceId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!content.Parts.TryGet(partInstanceId, out SurgicalPartInstance part)
            || part.installed)
        {
            failure = new DomainFailure(FailureCode.SurgeryPartUnavailable);
            return false;
        }

        bool kindMatches = procedure.Kind switch
        {
            SurgicalProcedureKind.TransplantOrgan => part.kind == SurgicalPartKind.NaturalOrgan,
            SurgicalProcedureKind.InstallProsthetic => part.kind == SurgicalPartKind.Prosthetic,
            SurgicalProcedureKind.InstallImplant => part.kind == SurgicalPartKind.Implant,
            SurgicalProcedureKind.ArcaneModification =>
                part.kind == SurgicalPartKind.ArcaneGraft
                || part.kind == SurgicalPartKind.Implant,
            _ => true
        };
        if (!kindMatches)
        {
            failure = new DomainFailure(FailureCode.SurgeryPartKindMismatch);
            return false;
        }

        string target = string.IsNullOrWhiteSpace(targetNodeId)
            ? procedure.TargetNodeId
            : targetNodeId.Trim();
        if (string.Equals(part.nodeId, target, StringComparison.Ordinal))
        {
            return true;
        }

        AnatomyProfileDefinition recipient = content.AnatomyProfiles.GetForSpecies(subject?.speciesId);
        if (recipient.TryGetNode(target, out AnatomyNodeDefinition targetNode)
            && recipient.TryGetNode(part.nodeId, out AnatomyNodeDefinition partNode)
            && !string.IsNullOrWhiteSpace(targetNode.PairedGroupId)
            && string.Equals(
                targetNode.PairedGroupId,
                partNode.PairedGroupId,
                StringComparison.Ordinal))
        {
            return true;
        }

        failure = new DomainFailure(FailureCode.SurgeryPartNodeMismatch);
        return false;
    }

    public bool ValidateSubject(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        string targetNodeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        bool corpse = subject.kind is SurgicalSubjectKind.HumanoidCorpse
            or SurgicalSubjectKind.WildlifeCorpse;
        if (corpse && !procedure.AllowsCorpseSubject
            || !corpse && !procedure.AllowsLivingSubject
            || subject.kind is SurgicalSubjectKind.Wildlife
                or SurgicalSubjectKind.WildlifeCorpse
                && !procedure.AllowsWildlife)
        {
            failure = new DomainFailure(
                FailureCode.SurgerySubjectKindUnsupported);
            return false;
        }

        AnatomyProfileDefinition profile =
            !string.IsNullOrWhiteSpace(subject.anatomyProfileId)
            && content.AnatomyProfiles.TryGet(subject.anatomyProfileId, out AnatomyProfileDefinition explicitProfile)
                ? explicitProfile
                : content.AnatomyProfiles.GetForSpecies(subject.speciesId);
        string family = profile?.AnatomyFamily ?? string.Empty;
        if (procedure.AllowedAnatomyFamilies.Count > 0
            && !procedure.AllowedAnatomyFamilies.Any(value => string.Equals(
                value,
                family,
                StringComparison.OrdinalIgnoreCase)))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryAnatomyFamilyUnsupported,
                family);
            return false;
        }

        if (procedure.AllowedSpeciesIds.Count > 0
            && !procedure.AllowedSpeciesIds.Any(value => string.Equals(
                value,
                subject.speciesId,
                StringComparison.OrdinalIgnoreCase)))
        {
            failure = new DomainFailure(
                FailureCode.SurgerySpeciesUnsupported,
                subject.speciesId);
            return false;
        }

        bool construct = string.Equals(family, "construct", StringComparison.OrdinalIgnoreCase);
        if (construct != (procedure.Family == MedicalProcedureFamily.Construct)
            && (construct || procedure.Family == MedicalProcedureFamily.Construct))
        {
            failure = new DomainFailure(
                construct
                    ? FailureCode.SurgeryConstructProcedureRequired
                    : FailureCode.SurgeryConstructProcedureBiologicalMismatch);
            return false;
        }

        string nodeId = string.IsNullOrWhiteSpace(targetNodeId)
            ? procedure.TargetNodeId
            : targetNodeId.Trim();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing);
            return false;
        }

        return corpse
            ? ValidateCorpse(subject, nodeId, out failure)
            : subject.kind == SurgicalSubjectKind.Character
                ? ValidateCharacter(subject, nodeId, out failure)
                : ValidateWildlife(subject, nodeId, out failure);
    }

    public bool ValidateResearch(
        SurgicalProcedureSO procedure,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (string.IsNullOrWhiteSpace(procedure.RequiredResearchId))
        {
            return true;
        }

        try
        {
            if (resources.Research.GetState().Projects.IsCompleted(
                new ResearchProjectId(procedure.RequiredResearchId)))
            {
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryResearchStateUnavailable);
            return false;
        }

        failure = new DomainFailure(
            FailureCode.SurgeryResearchIncomplete,
            procedure.RequiredResearchId);
        return false;
    }

    public List<SurgicalMaterialRequirement> BuildMaterials(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        SurgicalFacilitySnapshot facility)
    {
        Dictionary<string, SurgicalMaterialRequirement> merged =
            new Dictionary<string, SurgicalMaterialRequirement>(StringComparer.Ordinal);
        foreach (SurgicalMaterialRequirement requirement in procedure.Materials)
        {
            Add(merged, requirement?.itemId, requirement?.quantity ?? 0, requirement?.optional ?? false);
        }

        if (procedure.RequiresAnesthesia || subject != null && !subject.willing)
        {
            Add(merged, SurgeryItemDefinitions.AnestheticId, 1, false);
        }

        bool restrained = subject?.kind == SurgicalSubjectKind.Character
            && world.Captivity.TryGetCaptive(subject.subjectId, out CaptiveState captive)
            && captive.restrained;
        if (procedure.RequiresRestraintForUnwilling
            && subject != null
            && !subject.willing
            && !restrained)
        {
            Add(merged, CaptivityItemDefinitions.RestraintsItemId, 1, false);
        }

        foreach (BuildableObject support in facility.SupportFacilities
            .Append(facility.PrimaryFacility)
            .Where(building => building != null))
        {
            BuildingSterilizationAbility sterilization =
                support.BuildingData?.GetAbility<BuildingSterilizationAbility>();
            if (sterilization != null)
            {
                Add(merged, CleanWaterItemId, sterilization.waterCost, false);
                Add(merged, SurgeryItemDefinitions.DisinfectantId, sterilization.disinfectantCost, false);
            }

            BuildingTransplantSupportAbility transplant =
                support.BuildingData?.GetAbility<BuildingTransplantSupportAbility>();
            if (transplant != null
                && (procedure.RequiredFacilityTags & SurgeryFacilityTag.Transplant) != 0)
            {
                Add(merged, SurgeryItemDefinitions.BloodPackId, transplant.bloodCost, false);
                Add(
                    merged,
                    SurgeryItemDefinitions.ImmunosuppressantId,
                    transplant.immunosuppressantCost,
                    false);
            }

            BuildingArcaneSurgeryAbility arcane =
                support.BuildingData?.GetAbility<BuildingArcaneSurgeryAbility>();
            if (arcane != null)
            {
                Add(merged, ManaCrystalItemId, arcane.manaCrystalCost, false);
            }
        }

        return merged.Values
            .Where(requirement => requirement.quantity > 0)
            .OrderBy(requirement => requirement.itemId, StringComparer.Ordinal)
            .ToList();
    }

    public float ResolvePatientInstability(SurgicalSubjectRef subject)
    {
        CharacterActor actor = SurgicalSubjectResolver.FindCharacter(
            world.Characters,
            subject?.subjectId);
        if (actor != null)
        {
            CharacterBodyHealthSnapshot snapshot =
                world.BodyHealthQuery.GetSnapshot(actor);
            return Mathf.Clamp01(Mathf.Max(
                1f - snapshot.Consciousness,
                snapshot.BloodLoss / 100f));
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            world.Wildlife,
            subject?.subjectId);
        return animal != null
            ? 1f - animal.CurrentHealth / Mathf.Max(1f, animal.MaxHealth)
            : 0f;
    }

    public float ResolveCompatibilityPenalty(SurgeryOrder order)
    {
        if (order == null
            || string.IsNullOrWhiteSpace(order.selectedPartInstanceId)
            || !content.Parts.TryGet(order.selectedPartInstanceId, out SurgicalPartInstance part))
        {
            return 0f;
        }

        if (string.Equals(
            part.donorSpeciesId,
            order.subject?.speciesId,
            StringComparison.OrdinalIgnoreCase))
        {
            return 0f;
        }

        AnatomyProfileDefinition recipient = content.AnatomyProfiles.GetForSpecies(order.subject?.speciesId);
        float compatibility = string.Equals(
            part.anatomyFamily,
            recipient.AnatomyFamily,
            StringComparison.OrdinalIgnoreCase)
                ? 0.75f
                : string.Equals(recipient.AnatomyFamily, "slime", StringComparison.OrdinalIgnoreCase)
                    ? 0.2f
                    : 0.45f;
        return (1f - compatibility) * 0.35f;
    }

    private bool ValidateCorpse(
        SurgicalSubjectRef subject,
        string nodeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        WorldItemStackSnapshot stack = resources.Items.GetAllStacks().FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.StackId, subject.subjectId, StringComparison.Ordinal));
        if (stack == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryCorpseMissing,
                subject.subjectId);
            return false;
        }
        if (!world.CorpseFreshness.TryGetFreshness(subject.subjectId, out _, out bool fresh)
            || !fresh)
        {
            failure = new DomainFailure(FailureCode.SurgeryCorpseStale);
            return false;
        }
        if (resources.ExtractionLedger.IsExtracted(subject.subjectId, nodeId))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryNodeAlreadyExtracted,
                nodeId);
            return false;
        }
        return true;
    }

    private bool ValidateCharacter(
        SurgicalSubjectRef subject,
        string nodeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterActor actor = SurgicalSubjectResolver.FindCharacter(world.Characters, subject.subjectId);
        if (actor == null || actor.IsDead)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryLivingSubjectUnavailable,
                subject.subjectId);
            return false;
        }
        if (!resources.Anatomy.GetAnatomySnapshot(actor).Nodes.Any(node =>
            node != null && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeUnavailable,
                nodeId);
            return false;
        }
        return true;
    }

    private bool ValidateWildlife(
        SurgicalSubjectRef subject,
        string nodeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(world.Wildlife, subject.subjectId);
        if (animal == null || !animal.IsAlive)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryWildlifeSubjectUnavailable,
                subject.subjectId);
            return false;
        }
        if (!resources.WildlifeAnatomy.GetAnatomySnapshot(animal).Nodes.Any(node =>
            node != null && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeUnavailable,
                nodeId);
            return false;
        }
        return true;
    }

    private static void Add(
        IDictionary<string, SurgicalMaterialRequirement> merged,
        string itemId,
        int quantity,
        bool optional)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return;
        }

        string id = itemId.Trim();
        if (!merged.TryGetValue(id, out SurgicalMaterialRequirement entry))
        {
            entry = new SurgicalMaterialRequirement { itemId = id, optional = optional };
            merged.Add(id, entry);
        }
        entry.quantity += quantity;
        entry.optional &= optional;
    }
}
