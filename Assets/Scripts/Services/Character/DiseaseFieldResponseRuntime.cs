using System;
using System.Collections.Generic;
using System.Linq;

public interface IDiseaseFieldResponseCommand
{
    bool TryApply(
        CharacterId characterId,
        string diseaseId,
        string responseId,
        string facilityInstanceId,
        out DomainFailure failure);
}

/// <summary>
/// Executes authored disease field responses through operational medical
/// facilities and their physical input buffers. Population health is staged on
/// a detached aggregate and published only after the exact supply consumption
/// succeeds.
/// </summary>
public sealed class DiseaseFieldResponseRuntime : IDiseaseFieldResponseCommand
{
    private readonly struct ResponseRule
    {
        public ResponseRule(string itemId, int amount, float severityReduction)
        {
            ItemId = itemId ?? string.Empty;
            Amount = Math.Max(0, amount);
            SeverityReduction = Math.Max(1f, severityReduction);
        }
        public string ItemId { get; }
        public int Amount { get; }
        public float SeverityReduction { get; }
    }

    private static readonly IReadOnlyDictionary<string, ResponseRule> Rules =
        new Dictionary<string, ResponseRule>(StringComparer.Ordinal)
        {
            ["response:respirator"] = new("medical:isolation-care-kit", 1, 10f),
            ["response:wet-cleaning"] = new("resource:clean-water", 2, 14f),
            ["response:boil-water"] = new("resource:clean-water", 2, 12f),
            ["response:antiparasitic"] = new("medicine:antidote", 1, 26f),
            ["response:fungicide-wash"] = new("supply:fungicide", 1, 24f),
            ["response:dry-isolation"] = new("medical:isolation-care-kit", 1, 16f),
            ["response:mana-shield"] = new("medical:isolation-care-kit", 1, 12f),
            ["response:blood-filtration"] = new("medicine:blood-pack", 1, 24f),
            ["response:sealed-blood-reserve"] = new("medicine:blood-pack", 1, 18f),
            ["response:night-watch"] = new("medical:isolation-care-kit", 1, 10f),
            ["response:hot-wash"] = new("resource:clean-water", 2, 18f),
            ["response:pest-lure"] = new("supply:pest-lure", 1, 20f),
            ["response:cooling-bed"] = new("medical:isolation-care-kit", 1, 18f),
            ["response:vent-seal"] = new("component:reclaimed-water-filter", 1, 16f),
            ["response:dreamless-sedative"] = new("drug:dreamleaf-analgesic", 1, 22f),
            ["response:spore-filter"] = new("component:reclaimed-water-filter", 1, 18f)
        };

    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly IPopulationHealthQuery health;
    private readonly IPopulationHealthPersistence persistence;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IItemDefinitionCatalog items;
    private readonly IItemTransferService transfers;
    private readonly IPhysicalVaccinationService vaccination;
    private readonly IPopulationDiseaseModifierQuery modifiers;

    public DiseaseFieldResponseRuntime(
        IDiseaseDefinitionCatalog diseases,
        IPopulationHealthQuery health,
        IPopulationHealthPersistence persistence,
        IFacilityCapabilityQuery facilities,
        IItemDefinitionCatalog items,
        IItemTransferService transfers,
        IPhysicalVaccinationService vaccination,
        IPopulationDiseaseModifierQuery modifiers)
    {
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
        this.health = health ?? throw new ArgumentNullException(nameof(health));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.vaccination = vaccination ?? throw new ArgumentNullException(nameof(vaccination));
        this.modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
    }

    public bool TryApply(
        CharacterId characterId,
        string diseaseId,
        string responseId,
        string facilityInstanceId,
        out DomainFailure failure)
    {
        DiseaseDefinition disease;
        try { disease = diseases.Require(diseaseId); }
        catch (Exception)
        {
            failure = new DomainFailure(FailureCode.VaccineDiseaseMismatch, diseaseId);
            return false;
        }
        string response = responseId?.Trim() ?? string.Empty;
        if (!disease.FieldResponseIds.Contains(response, StringComparer.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.VaccineDiseaseMismatch,
                disease.Id,
                response);
            return false;
        }

        string preferredFacility = response.StartsWith(
                "response:vaccine:",
                StringComparison.Ordinal)
            ? "building:8876"
            : response.StartsWith("response:isolate:", StringComparison.Ordinal)
                ? "building:8874"
                : "building:8873";
        string destination = ResolveFacility(facilityInstanceId, preferredFacility);
        if (destination.Length == 0)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing, response);
            return false;
        }

        if (response.StartsWith("response:vaccine:", StringComparison.Ordinal))
        {
            return vaccination.TryVaccinate(
                characterId,
                destination,
                "medicine:vaccine:" + disease.Id.Substring("disease:".Length),
                out failure);
        }

        if (!health.TryGetCharacterSnapshot(characterId, out PopulationCharacterHealthSnapshot state)
            || !state.ActiveDiseases.Any(value =>
                string.Equals(value.DiseaseId, disease.Id, StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.PopulationHealthCharacterMissing,
                characterId.Value,
                disease.Id);
            return false;
        }

        ResponseRule rule = ResolveRule(response);
        if (rule.ItemId.Length > 0
            && (!items.TryGet((ItemDefinitionId)rule.ItemId, out _)))
        {
            failure = new DomainFailure(FailureCode.VaccineDefinitionMissing, rule.ItemId);
            return false;
        }

        PopulationHealthAggregateState candidate;
        try
        {
            candidate = persistence.PrepareRestore(persistence.Capture());
            candidate.ApplyFieldResponse(
                characterId,
                disease.Id,
                rule.SeverityReduction,
                diseases,
                modifiers.Resolve(characterId, disease));
        }
        catch (Exception)
        {
            failure = new DomainFailure(
                FailureCode.PopulationHealthCharacterMissing,
                characterId.Value,
                disease.Id);
            return false;
        }

        if (rule.Amount > 0
            && !transfers.TryConsumeFacilityItemBuffer(
                destination,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [rule.ItemId] = rule.Amount
                },
                out string consumeFailure))
        {
            failure = new DomainFailure(
                FailureCode.VaccineDoseUnavailable,
                destination,
                rule.ItemId,
                consumeFailure ?? string.Empty);
            return false;
        }

        persistence.PublishRestore(candidate);
        failure = DomainFailure.None;
        return true;
    }

    private string ResolveFacility(string requestedId, string preferredDefinitionId)
    {
        string requested = requestedId?.Trim() ?? string.Empty;
        IReadOnlyList<BuildableObject> exact =
            preferredDefinitionId == "building:8873"
                ? facilities.FindOperational(
                    ResearchFacilityCommandKind.PathogenDiagnosis)
                : facilities.FindOperational(
                    FacilityCapabilityKind.Medical,
                    preferredDefinitionId);
        BuildableObject selected = requested.Length == 0
            ? exact.FirstOrDefault()
            : exact.FirstOrDefault(value => string.Equals(
                value.PersistentInstanceId.Value,
                requested,
                StringComparison.Ordinal));
        if (selected == null && preferredDefinitionId == "building:8873")
        {
            selected = facilities.FindOperational(
                    FacilityCapabilityKind.Medical,
                    "building:8874")
                .FirstOrDefault(value => requested.Length == 0 || string.Equals(
                    value.PersistentInstanceId.Value,
                    requested,
                    StringComparison.Ordinal));
        }
        return selected?.PersistentInstanceId.Value ?? string.Empty;
    }

    private static ResponseRule ResolveRule(string response)
    {
        if (Rules.TryGetValue(response, out ResponseRule rule)) return rule;
        if (response.StartsWith("response:isolate:", StringComparison.Ordinal))
            return new ResponseRule("medical:isolation-care-kit", 1, 16f);
        if (response.StartsWith("response:environment:", StringComparison.Ordinal))
            return new ResponseRule("medical:isolation-care-kit", 1, 20f);
        return new ResponseRule("medical:isolation-care-kit", 1, 12f);
    }
}
