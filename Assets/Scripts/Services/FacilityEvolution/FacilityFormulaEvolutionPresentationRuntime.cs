using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

[Serializable]
public sealed class FacilityFormulaEvolutionPresentationDto : ILlmJsonPayload
{
    public string presentationId = string.Empty;
    public string displayName = string.Empty;
    public string narrativeFlavor = string.Empty;

    public bool Validate(out string error)
    {
        const string prefix = "presentation:facility:";
        error = string.Empty;
        if (presentationId == null || presentationId.Length != prefix.Length + 64
            || !presentationId.StartsWith(prefix, StringComparison.Ordinal)
            || presentationId.Skip(prefix.Length).Any(value =>
                !((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f'))))
        {
            error = "Facility presentationId is not canonical.";
            return false;
        }
        if (!IsText(displayName, 32) || !IsText(narrativeFlavor, 180)
            || (displayName + narrativeFlavor).Any(value => value is >= '0' and <= '9'
                or >= '０' and <= '９' or '%' or '％'))
        {
            error = "Facility presentation text is invalid or restates mechanical numbers.";
            return false;
        }
        return true;
    }

    private static bool IsText(string value, int maximum) => !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal) && value.Length <= maximum;
}

[Serializable]
public sealed class FacilityFormulaEvolutionModuleSelectionDto : ILlmJsonPayload
{
    public string selectionId = string.Empty;
    public List<string> positiveModuleIds = new();
    public List<string> drawbackModuleIds = new();
    public List<string> evidenceFactIds = new();
    public string displayName = string.Empty;
    public string narrativeFlavor = string.Empty;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(selectionId)
            || positiveModuleIds == null || drawbackModuleIds == null
            || evidenceFactIds == null || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(narrativeFlavor))
        {
            error = "The six facility module-selection fields are required.";
            return false;
        }
        if (!string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal)
            || displayName.Length > 32
            || !string.Equals(narrativeFlavor, narrativeFlavor.Trim(), StringComparison.Ordinal)
            || narrativeFlavor.Length > 180
            || (displayName + narrativeFlavor).Any(value => char.IsDigit(value)
                || value is '%' or '％'))
        {
            error = "Facility selection prose is non-canonical, too long, or contains mechanical numbers.";
            return false;
        }
        return true;
    }
}

/// <summary>Owns only facility v1 presentation delivery. C# has already frozen
/// the recipe, module, potency, cost and evidence before this runtime submits.</summary>
public sealed class FacilityFormulaEvolutionPresentationRuntime : IStartable, ITickable
{
    private readonly IBuildingWorldQuery buildings;
    private readonly ILocalLlmRuntimeProvider llm;
    private readonly FacilityFeatureSceneRuntimeReferences scene;
    private readonly IUiClock clock;
    private readonly HashSet<string> inFlight = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> retryAt = new(StringComparer.Ordinal);

    public FacilityFormulaEvolutionPresentationRuntime(
        IBuildingWorldQuery buildings,
        ILocalLlmRuntimeProvider llm,
        FacilityFeatureSceneRuntimeReferences scene,
        IUiClock clock)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.llm = llm ?? throw new ArgumentNullException(nameof(llm));
        this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void Start() { }

    public void Tick()
    {
        FacilityEvolutionRuntime runtime = scene.Evolution;
        if (runtime == null || !llm.TryGetRuntime(out ILocalLlmRuntime generator)) return;
        foreach (BuildableObject facility in buildings.Buildings
                     .Where(value => value != null && !value.isDestroy)
                     .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal))
        {
            FacilityEvolutionStateComponent state = facility.GetComponent<FacilityEvolutionStateComponent>();
            FacilityEvolutionFormulaPresentationPendingSnapshot pending = state?.PendingFormulaPresentation;
            if (pending == null || pending.failureCount >= 5
                || pending.node.presentationState is not
                    (EquipmentEvolutionPresentationState.PresentationPending
                    or EquipmentEvolutionPresentationState.ModuleSelectionPending)
                || inFlight.Contains(pending.presentationId)
                || retryAt.TryGetValue(pending.presentationId, out float due) && clock.Time < due)
                continue;
            string prompt;
            try { prompt = BuildPrompt(facility, pending); }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                RegisterFailure(runtime, facility, pending, exception.Message);
                continue;
            }
            inFlight.Add(pending.presentationId);
            bool accepted;
            if (pending.node.presentationState
                == EquipmentEvolutionPresentationState.ModuleSelectionPending)
            {
                accepted = generator is ICorrelatedFacilityEvolutionModuleSelectionLlmRuntime selector
                    && selector.GenerateFacilityEvolutionModuleSelectionAsync(
                        pending.presentationId, prompt, result =>
                            HandleResult(runtime, facility, pending.presentationId, result));
            }
            else
            {
                accepted = generator.GenerateFacilityEvolutionAsync(prompt, result =>
                    HandleResult(runtime, facility, pending.presentationId, result));
            }
            if (!accepted)
            {
                inFlight.Remove(pending.presentationId);
                RegisterFailure(runtime, facility, pending, "FacilityEvolution presentation request was not accepted.");
            }
            return;
        }
    }

    private void HandleResult(
        FacilityEvolutionRuntime runtime,
        BuildableObject facility,
        string presentationId,
        LocalLlmResult result)
    {
        inFlight.Remove(presentationId);
        if (facility == null || facility.isDestroy) return;
        FacilityEvolutionFormulaPresentationPendingSnapshot pending = facility
            .GetComponent<FacilityEvolutionStateComponent>()?.PendingFormulaPresentation;
        if (pending == null || !string.Equals(pending.presentationId, presentationId,
                StringComparison.Ordinal)) return;
        string error = string.Empty;
        string exactJson = string.Empty;
        bool moduleSelection = pending.node.presentationState
            == EquipmentEvolutionPresentationState.ModuleSelectionPending;
        bool accepted;
        if (moduleSelection)
        {
            FacilityFormulaEvolutionModuleSelectionDto payload = null;
            accepted = result.IsSuccess
                && NarrativeExactKeyContract.TryValidateProfileResponse(
                    LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                    result.Content, out exactJson, out error)
                && LlmJsonResponseParser.TryParse(exactJson, out payload, out error)
                && payload.Validate(out error)
                && string.Equals(payload.selectionId, presentationId, StringComparison.Ordinal)
                && runtime.TryCommitFormulaModuleSelection(
                    facility, payload, out _, out error);
        }
        else
        {
            FacilityFormulaEvolutionPresentationDto payload = null;
            accepted = result.IsSuccess
                && NarrativeExactKeyContract.TryValidateProfileResponse(
                    LocalLlmRequestProfiles.FacilityEvolution.Id, result.Content,
                    out exactJson, out error)
                && LlmJsonResponseParser.TryParse(exactJson, out payload, out error)
                && payload.Validate(out error)
                && string.Equals(payload.presentationId, presentationId, StringComparison.Ordinal)
                && runtime.TryCommitFormulaPresentation(facility, presentationId,
                    payload.displayName, payload.narrativeFlavor, out _, out error);
        }
        if (!accepted)
        {
            RegisterFailure(runtime, facility, pending,
                result.IsSuccess ? error : result.Status + ": " + result.Error);
            return;
        }
        retryAt.Remove(presentationId);
    }

    private void RegisterFailure(
        FacilityEvolutionRuntime runtime,
        BuildableObject facility,
        FacilityEvolutionFormulaPresentationPendingSnapshot pending,
        string reason)
    {
        if (!runtime.TryRegisterFormulaPresentationFailure(facility,
                pending.presentationId, reason, out bool awaiting) || awaiting)
        {
            retryAt.Remove(pending.presentationId);
            return;
        }
        retryAt[pending.presentationId] = clock.Time + Mathf.Min(120f,
            2f * Mathf.Pow(2f, Mathf.Min(6, pending.failureCount + 1)));
    }

    private static string BuildPrompt(
        BuildableObject facility,
        FacilityEvolutionFormulaPresentationPendingSnapshot pending)
    {
        if (!string.Equals(facility.PersistentInstanceId.Value,
                pending.sourceFacilityPersistentId, StringComparison.Ordinal)
            || !string.Equals(pending.presentationId, pending.node.presentationId,
                StringComparison.Ordinal))
            throw new InvalidOperationException("Facility formula presentation owner or identity changed.");
        if (pending.node.presentationState
            == EquipmentEvolutionPresentationState.ModuleSelectionPending)
        {
            StringBuilder selection = new(2400);
            selection.AppendLine("C# has supplied every legal authored facility module. Select the module that best follows from the actual ledger facts. C# will allocate all numeric values only after this selection.");
            selection.AppendLine("Select exactly one positive module and no independent drawback module. Each positive facility module already carries its inseparable authored burden.");
            selection.AppendLine("Return exactly one JSON object with only selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds, displayName, narrativeFlavor.");
            selection.AppendLine("{\"selectionId\":\"provided value unchanged\",\"positiveModuleIds\":[\"one offered ID\"],\"drawbackModuleIds\":[],\"evidenceFactIds\":[\"provided fact IDs\"],\"displayName\":\"Korean name within 32 chars\",\"narrativeFlavor\":\"Korean origin within 180 chars\"}");
            selection.Append("profile=").AppendLine(
                LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id);
            selection.Append("selectionId=").AppendLine(pending.node.moduleSelectionId);
            foreach (EquipmentEvolutionModuleOfferState offer in pending.node.moduleSelectionOffers
                         .Where(value => value != null)
                         .OrderBy(value => value.moduleId, StringComparer.Ordinal))
                selection.Append("module=").Append(offer.moduleId).Append(" | ")
                    .AppendLine(offer.semanticDescription);
            foreach (string evidenceId in pending.node.evidenceIds
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                GameplayOutcomeEvidenceBindingSnapshot exact =
                    pending.node.gameplayOutcomeEvidence?.SingleOrDefault(value =>
                        value != null && string.Equals(
                            value.publicFactId, evidenceId, StringComparison.Ordinal));
                selection.Append("evidence=").Append(evidenceId);
                if (exact != null)
                    selection.Append(" | authoritativeFact=")
                        .Append(exact.canonicalFactText);
                selection.AppendLine();
            }
            return selection.ToString();
        }
        StringBuilder builder = new(1600);
        builder.AppendLine("C# rules have frozen this facility evolution's effect, potency, cost, evidence and legal recipe.");
        builder.AppendLine("Write only its Korean name and short origin. Do not invent facts or restate numbers, percentages, costs, effects, recipes or tags.");
        builder.AppendLine("Return exactly one JSON object with only presentationId, displayName, narrativeFlavor.");
        builder.AppendLine("{\"presentationId\":\"provided value unchanged\",\"displayName\":\"Korean name within 32 chars\",\"narrativeFlavor\":\"Korean origin within 180 chars\"}");
        builder.Append("profile=").AppendLine(LocalLlmRequestProfiles.FacilityEvolution.Id);
        builder.Append("presentationId=").AppendLine(pending.presentationId);
        builder.Append("mechanicalDescription=").AppendLine(pending.node.mechanicalDescription);
        foreach (string evidenceId in pending.node.evidenceIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            GameplayOutcomeEvidenceBindingSnapshot exact =
                pending.node.gameplayOutcomeEvidence?.SingleOrDefault(value =>
                    value != null && string.Equals(
                        value.publicFactId, evidenceId, StringComparison.Ordinal));
            builder.Append("evidence=").Append(evidenceId);
            if (exact != null)
                builder.Append(" | authoritativeFact=")
                    .Append(exact.canonicalFactText);
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
