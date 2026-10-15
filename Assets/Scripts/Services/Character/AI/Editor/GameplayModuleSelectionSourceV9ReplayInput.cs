#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal sealed class GameplayModuleSelectionEquipmentReplayInput
{
    internal string SourceManifestSha256 { get; set; }
    internal string ResponseManifestSha256 { get; set; }
    internal string ProfileId { get; set; }
    internal string RequestId { get; set; }
    internal string ResponseSha256 { get; set; }
    internal string SelectionSourceId { get; set; }
    internal string EquipmentInstanceId { get; set; }
    internal string OwnerId { get; set; }
    internal string FormulaId { get; set; }
    internal string AcceptedResponseJson { get; set; }
    internal string SourceWorldPath { get; set; }
    internal string SourceWorldSha256 { get; set; }
    internal string SourceWireSha256 { get; set; }
    internal string[] SelectedModuleIds { get; set; }
    internal string[] SelectedEvidenceFactIds { get; set; }
    internal JObject ReplayCodeIdentity { get; set; }
    internal EquipmentEvolutionModuleSelectionResponseDto Response { get; set; }
}

internal sealed class GameplayModuleSelectionSkillReplayInput
{
    internal string SourceManifestSha256 { get; set; }
    internal string ResponseManifestSha256 { get; set; }
    internal string ProfileId { get; set; }
    internal string RequestId { get; set; }
    internal string RequestKey { get; set; }
    internal string ResponseSha256 { get; set; }
    internal string SelectionSourceId { get; set; }
    internal string OwnerId { get; set; }
    internal string AcceptedResponseJson { get; set; }
    internal string SourceWorldPath { get; set; }
    internal string SourceWorldSha256 { get; set; }
    internal string SourceWireSha256 { get; set; }
    internal string[] SelectedModuleIds { get; set; }
    internal string[] SelectedEvidenceFactIds { get; set; }
    internal string[] FormulaIds { get; set; }
    internal JObject ReplayCodeIdentity { get; set; }
    internal CharacterSkillModuleSelectionBatchResponseDto Response { get; set; }
}

internal sealed class GameplayModuleSelectionTraitReplayInput
{
    internal string MechanicTargetId { get; set; }
    internal string MechanicBindingId { get; set; }
    internal bool IntegrationFixture { get; set; }
    internal string AuthoringOrigin { get; set; }
    internal string SourceManifestSha256 { get; set; }
    internal string ResponseManifestSha256 { get; set; }
    internal string ProfileId { get; set; }
    internal string RequestId { get; set; }
    internal string TraitRequestId { get; set; }
    internal string ResponseSha256 { get; set; }
    internal string SelectionSourceId { get; set; }
    internal string OwnerId { get; set; }
    internal string AcceptedResponseJson { get; set; }
    internal string SourceWorldPath { get; set; }
    internal string SourceWorldSha256 { get; set; }
    internal string SourceWireSha256 { get; set; }
    internal string[] SelectedModuleIds { get; set; }
    internal string[] SelectedEvidenceFactIds { get; set; }
    internal GameplayOutcomeEvidenceBindingSnapshot[] SelectedEvidenceBindings { get; set; }
    internal string[] FormulaIds { get; set; }
    internal JObject ReplayCodeIdentity { get; set; }
    internal CharacterAcquiredTraitModuleSelectionResponseDto Response { get; set; }
}

internal sealed class GameplayModuleSelectionFacilityReplayInput
{
    internal string SourceManifestSha256 { get; set; }
    internal string ResponseManifestSha256 { get; set; }
    internal string ProfileId { get; set; }
    internal string RequestId { get; set; }
    internal string ResponseSha256 { get; set; }
    internal string SelectionSourceId { get; set; }
    internal string FacilityId { get; set; }
    internal string RecipeId { get; set; }
    internal string FormulaId { get; set; }
    internal string AcceptedResponseJson { get; set; }
    internal string SourceWorldPath { get; set; }
    internal string SourceWorldSha256 { get; set; }
    internal string SourceWireSha256 { get; set; }
    internal string[] SelectedModuleIds { get; set; }
    internal string[] SelectedEvidenceFactIds { get; set; }
    internal GameplayOutcomeEvidenceBindingSnapshot[] SelectedEvidenceBindings { get; set; }
    internal FacilityEvolutionFormulaPresentationPendingSnapshot Pending { get; set; }
    internal JObject ReplayCodeIdentity { get; set; }
    internal FacilityFormulaEvolutionModuleSelectionDto Response { get; set; }
}

/// <summary>
/// Strict source-v9 plus authored-response intake for the Unity replay process.
/// This reads externally fixed bytes and never authors or chooses a response.
/// </summary>
internal static class GameplayModuleSelectionSourceV9ReplayInput
{
    internal const string SourceSchema =
        "dungeonstory-gameplay-module-selection-source@9";
    internal const string ResponseSchema =
        "dungeonstory-module-selection-authored-response@2";

    internal static GameplayModuleSelectionTraitReplayInput LoadTrait(
        string projectRoot,
        string sourceRoot,
        string responseRoot,
        string expectedWorldPath)
    {
        string project = RequireDirectory(projectRoot, "project root");
        string source = RequireQaDirectory(project, sourceRoot, "source-v9 package");
        string response = RequireQaDirectory(project, responseRoot, "response package");
        string expectedWorld = RequireQaFile(project, expectedWorldPath,
            "campaign resume world");

        byte[] sourceManifestBytes = ReadRequiredFile(source, "manifest.json");
        JObject sourceManifest = ParseObject(sourceManifestBytes, "source manifest");
        if (!string.Equals((string)sourceManifest["schema"], SourceSchema,
                StringComparison.Ordinal)
            || (int?)sourceManifest["selectionCount"] is not int selectionCount
            || selectionCount < 1
            || (bool?)sourceManifest["trainingEligible"] != false
            || (bool?)sourceManifest["humanApprovalClaimed"] != false
            || (bool?)sourceManifest["dpoPerformed"] != false
            || (bool?)sourceManifest["releaseReady"] != false)
        {
            throw new InvalidOperationException(
                "Source-v9 trait package schema/count/gates are invalid.");
        }
        string sourceManifestSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(sourceManifestBytes);
        JObject mechanics = ParseObject(
            ReadDescriptor(source, RequireObject(sourceManifest, "mechanics"),
                "source mechanics"),
            "source mechanics");
        if (!string.Equals((string)mechanics["schema"],
                "dungeonstory-mechanics-source-v9@1", StringComparison.Ordinal)
            || mechanics["codeIdentity"] is not JArray sourceCodeIdentity
            || sourceCodeIdentity.Count == 0
            || (bool?)mechanics["trainingEligible"] != false
            || (bool?)mechanics["humanApprovalClaimed"] != false)
        {
            throw new InvalidOperationException(
                "Source-v9 trait mechanics identity or gates are invalid.");
        }
        JObject selectionRoot = ParseObject(
            ReadDescriptor(source, RequireObject(sourceManifest, "selectionSource"),
                "selection source"),
            "selection source");
        if (!string.Equals((string)selectionRoot["schema"],
                "dungeonstory-selection-source-v9@1", StringComparison.Ordinal)
            || selectionRoot["selections"] is not JArray selections
            || selections.Count != selectionCount
            || (bool?)selectionRoot["responseDataIncluded"] != false
            || (bool?)selectionRoot["responseApplied"] != false
            || (bool?)selectionRoot["trainingEligible"] != false)
        {
            throw new InvalidOperationException(
                "Selection source is not a response-free source-v9 package.");
        }
        JObject[] traitRows = selections.OfType<JObject>().Where(value =>
                string.Equals((string)value["profileId"],
                    LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                    StringComparison.Ordinal))
            .ToArray();
        if (traitRows.Length != 1)
            throw new InvalidOperationException(
                "Source-v9 package must contain exactly one acquired-trait module selection.");
        JObject selection = traitRows[0];
        string profileId = RequireText(selection, "profileId");
        string requestId = RequireText(selection, "correlationId");
        string traitRequestId = RequireText(selection, "triggerId");
        string selectionSourceId = RequireText(selection, "selectionSourceId");
        string ownerId = RequireText(selection, "ownerId");
        if (!string.Equals(ownerId, RequireText(selection, "targetId"),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Trait source owner and target differ.");
        JObject capture = RequireObject(selection, "capture");
        JObject worldDescriptor = RequireObject(capture, "officialWorldCheckpoint");
        byte[] worldBytes = ReadDescriptor(source, worldDescriptor,
            "official world checkpoint");
        string worldPath = DescriptorPath(source, worldDescriptor,
            "official world checkpoint");
        if (!string.Equals(worldPath, expectedWorld,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The launched resume world is not the trait source checkpoint.");
        byte[] pendingBytes = ReadDescriptor(source,
            RequireObject(capture, "pending"), "trait pending");
        CharacterAcquiredTraitPendingRequestState pending =
            JsonUtility.FromJson<CharacterAcquiredTraitPendingRequestState>(
                Encoding.UTF8.GetString(pendingBytes));
        if (pending == null
            || !string.Equals(pending.requestId, traitRequestId,
                StringComparison.Ordinal)
            || !string.Equals(pending.requestKey, requestId,
                StringComparison.Ordinal)
            || pending.ownerBinding == null
            || !string.Equals(pending.ownerBinding.persistentId, ownerId,
                StringComparison.Ordinal)
            || pending.presentationState
                != CharacterAcquiredTraitPresentationState.ModuleSelectionPending
            || pending.formulaVersion
                < CharacterAcquiredTraitFormulaGeneration.ModuleSelectionFormulaVersion
            || (pending.moduleOffers?.Count ?? 0) < 1
            || (pending.evidenceFactIds?.Count ?? 0) < 1)
        {
            throw new InvalidOperationException(
                "Trait source pending state is not module-selection ready.");
        }
        string[] formulaIds = RequireStringArray(selection, "selectionIds");
        if (formulaIds.Length != 1
            || !string.Equals(formulaIds[0], pending.moduleSelectionId,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Trait selection ID differs from the pending formula identity.");
        JObject wire = RequireObject(selection, "wire");
        byte[] wireBytes = ReadDescriptor(source, RequireObject(wire, "original"),
            "source wire");
        byte[] replayWireBytes = ReadDescriptor(source,
            RequireObject(wire, "restoredReplay"), "restored source wire");
        if ((bool?)wire["byteExactReplay"] != true
            || !wireBytes.SequenceEqual(replayWireBytes))
            throw new InvalidOperationException(
                "Trait source wire was not byte-exact on restore.");
        string sourceWireSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(wireBytes);

        byte[] responseManifestBytes = ReadRequiredFile(response, "manifest.json");
        JObject responseManifest = ParseObject(responseManifestBytes,
            "response manifest");
        if (!string.Equals((string)responseManifest["schema"], ResponseSchema,
                StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest,
                    "sourceManifestSha256"), sourceManifestSha,
                StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "selectionSourceId"),
                selectionSourceId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "profileId"),
                profileId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "requestId"),
                requestId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "wireSha256"),
                sourceWireSha, StringComparison.Ordinal)
            || !HasAllowedResponseOrigin(responseManifest)
            || (bool?)responseManifest["humanApprovalClaimed"] != false
            || (bool?)responseManifest["trainingEligible"] != false
            || (bool?)responseManifest["dpoPerformed"] != false
            || (bool?)responseManifest["releaseReady"] != false)
        {
            throw new InvalidOperationException(
                "Authored trait response is not bound to this source request.");
        }
        byte[] acceptedBytes = ReadDescriptor(response,
            RequireObject(responseManifest, "acceptedResponse"),
            "accepted response");
        string acceptedJson = Encoding.UTF8.GetString(acceptedBytes)
            .TrimEnd('\r', '\n');
        JObject accepted = ParseObject(Encoding.UTF8.GetBytes(acceptedJson),
            "accepted response");
        ValidateResponseAuthoring(response, responseManifest, wireBytes, accepted);
        byte[] canonicalAccepted = GameplayModuleSelectionCanonicalJson
            .ToUtf8(accepted);
        if (!acceptedBytes.SequenceEqual(canonicalAccepted
                .Concat(new[] { (byte)'\n' }).ToArray()))
            throw new InvalidOperationException(
                "Accepted trait response must be canonical JSON plus one newline.");
        string responseSha = GameplayModuleSelectionCanonicalJson.Hash(accepted);
        if (!string.Equals(RequireText(responseManifest,
                "acceptedResponseSha256"), responseSha,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Accepted trait response hash differs from its manifest.");
        if ((bool)responseManifest["integrationFixture"] == false)
        {
            JObject resolved = ResolveObservedTraitContent(source, selection, wireBytes, accepted);
            JObject resolution = RequireObject(responseManifest, "modelReferenceResolution");
            JObject storedResolved = ParseObject(ReadDescriptor(response,
                RequireObject(resolution, "resolvedResponse"), "resolved Trait response"), "resolved Trait response");
            if ((string)resolution["schema"] != "dungeonstory-trait-model-reference-resolution@1"
                || (bool?)resolution["choiceOrProseRepair"] != false
                || (string)resolution["sourcePromptSha256"] != (string)selection["capture"]["originalPrompt"]["sha256"]
                || (string)resolution["resolvedResponseSha256"] != GameplayModuleSelectionCanonicalJson.Hash(resolved)
                || !JToken.DeepEquals(resolved, storedResolved))
                throw new InvalidOperationException("Observed Trait response reference resolution differs.");
            acceptedJson = resolved.ToString();
        }
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                profileId, acceptedJson, out string canonicalResponse,
                out string responseError))
            throw new InvalidOperationException(
                "Accepted trait response is invalid: " + responseError);
        CharacterAcquiredTraitModuleSelectionResponseDto parsed;
        try
        {
            parsed = JsonUtility.FromJson<
                CharacterAcquiredTraitModuleSelectionResponseDto>(
                canonicalResponse);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "Accepted trait response JSON parse failed.", error);
        }
        string validationError = "parsed response is null";
        if (parsed == null || !parsed.Validate(out validationError))
            throw new InvalidOperationException(
                "Accepted trait response payload is invalid: " + validationError);
        if (!string.Equals(parsed.selectionId, pending.moduleSelectionId,
                StringComparison.Ordinal)
            || parsed.positiveModuleIds.Count != 1
            || parsed.drawbackModuleIds.Count > 1
            || parsed.evidenceFactIds.Count < 1
            || parsed.positiveModuleIds.Any(value =>
                !pending.offeredBenefitModuleIds.Contains(value,
                    StringComparer.Ordinal))
            || parsed.drawbackModuleIds.Any(value =>
                !pending.offeredDrawbackModuleIds.Contains(value,
                    StringComparer.Ordinal))
            || parsed.evidenceFactIds.Any(value =>
                !pending.evidenceFactIds.Contains(value,
                    StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Accepted trait response selected an illegal offer value.");
        string[] selectedModules = parsed.positiveModuleIds
            .Concat(parsed.drawbackModuleIds).ToArray();
        string mechanicModule = parsed.positiveModuleIds.Single();
        CharacterAcquiredTraitModuleSO definition = Resources
            .LoadAll<CharacterAcquiredTraitModuleSO>("SO/V25/AcquiredTraits/Modules")
            .SingleOrDefault(value => value != null && value.ModuleId == mechanicModule);
        GameplayEffectBinding primaryEffect = definition?.Effects
            .SingleOrDefault(value => value != null
                && !definition.DrawbackBindingIds.Contains(value.bindingId, StringComparer.Ordinal));
        if (primaryEffect?.definition == null)
            throw new InvalidOperationException(
                "Selected legal trait has no unique authored primary effect: " + mechanicModule);
        string mechanicTarget = primaryEffect.definition.TargetId;
        GameplayOutcomeEvidenceBindingSnapshot[] selectedBindings =
            (pending.evidenceBindings
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null
                && parsed.evidenceFactIds.Contains(value.publicFactId,
                    StringComparer.Ordinal))
            .Select(value => value.Clone())
            .ToArray();
        if (selectedBindings.Length == 0
            || selectedBindings.Any(value => value.sourceKind != "exact"))
            throw new InvalidOperationException(
                "Trait replay requires selected exact gameplay-outcome evidence.");

        string catalogSha = RequireText(
            RequireObject(mechanics, "b1EvidenceManifest"), "sha256");
        JObject codeIdentity = GameplayModuleSelectionReplayProofV2Contract
            .CaptureCurrentCodeIdentity(
                project,
                sourceCodeIdentity,
                catalogSha,
                "sha256:" + RequireText(selection, "schemaHash"),
                new JObject
                {
                    ["mode"] = "source-v9-trait-integration-replay",
                    ["sourceSchema"] = 9,
                    ["proofSchema"] = 2,
                    ["networkSent"] = false,
                    ["integrationFixture"] = (bool)responseManifest["integrationFixture"],
                    ["authoringOrigin"] = (string)responseManifest["authoringOrigin"],
                    ["modelReferenceResolution"] = responseManifest["modelReferenceResolution"]?.DeepClone()
                });
        return new GameplayModuleSelectionTraitReplayInput
        {
            MechanicTargetId = mechanicTarget,
            MechanicBindingId = primaryEffect.bindingId,
            IntegrationFixture = (bool)responseManifest["integrationFixture"],
            AuthoringOrigin = (string)responseManifest["authoringOrigin"],
            SourceManifestSha256 = sourceManifestSha,
            ResponseManifestSha256 = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(responseManifestBytes),
            ProfileId = profileId,
            RequestId = requestId,
            TraitRequestId = traitRequestId,
            ResponseSha256 = responseSha,
            SelectionSourceId = selectionSourceId,
            OwnerId = ownerId,
            AcceptedResponseJson = canonicalResponse,
            SourceWorldPath = worldPath,
            SourceWorldSha256 = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(worldBytes),
            SourceWireSha256 = sourceWireSha,
            SelectedModuleIds = selectedModules,
            SelectedEvidenceFactIds = parsed.evidenceFactIds.ToArray(),
            SelectedEvidenceBindings = selectedBindings,
            FormulaIds = formulaIds,
            ReplayCodeIdentity = codeIdentity,
            Response = parsed
        };
    }

    internal static GameplayModuleSelectionSkillReplayInput LoadSkill(
        string projectRoot,
        string sourceRoot,
        string responseRoot,
        string expectedWorldPath)
    {
        string project = RequireDirectory(projectRoot, "project root");
        string source = RequireQaDirectory(project, sourceRoot, "source-v9 package");
        string response = RequireQaDirectory(project, responseRoot, "response package");
        string expectedWorld = RequireQaFile(project, expectedWorldPath,
            "campaign resume world");

        byte[] sourceManifestBytes = ReadRequiredFile(source, "manifest.json");
        JObject sourceManifest = ParseObject(sourceManifestBytes, "source manifest");
        if (!string.Equals((string)sourceManifest["schema"], SourceSchema,
                StringComparison.Ordinal)
            || (int?)sourceManifest["selectionCount"] is not int selectionCount
            || selectionCount < 1
            || (bool?)sourceManifest["trainingEligible"] != false
            || (bool?)sourceManifest["humanApprovalClaimed"] != false
            || (bool?)sourceManifest["dpoPerformed"] != false
            || (bool?)sourceManifest["releaseReady"] != false)
        {
            throw new InvalidOperationException(
                "Source-v9 skill package schema/count/gates are invalid.");
        }
        string sourceManifestSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(sourceManifestBytes);
        JObject mechanics = ParseObject(
            ReadDescriptor(source, RequireObject(sourceManifest, "mechanics"),
                "source mechanics"),
            "source mechanics");
        if (!string.Equals((string)mechanics["schema"],
                "dungeonstory-mechanics-source-v9@1", StringComparison.Ordinal)
            || mechanics["codeIdentity"] is not JArray sourceCodeIdentity
            || sourceCodeIdentity.Count == 0
            || (bool?)mechanics["trainingEligible"] != false
            || (bool?)mechanics["humanApprovalClaimed"] != false)
        {
            throw new InvalidOperationException(
                "Source-v9 skill mechanics identity or gates are invalid.");
        }
        JObject selectionRoot = ParseObject(
            ReadDescriptor(source, RequireObject(sourceManifest, "selectionSource"),
                "selection source"),
            "selection source");
        if (!string.Equals((string)selectionRoot["schema"],
                "dungeonstory-selection-source-v9@1", StringComparison.Ordinal)
            || selectionRoot["selections"] is not JArray selections
            || selections.Count != selectionCount
            || (bool?)selectionRoot["responseDataIncluded"] != false
            || (bool?)selectionRoot["responseApplied"] != false
            || (bool?)selectionRoot["trainingEligible"] != false)
        {
            throw new InvalidOperationException(
                "Selection source is not a response-free source-v9 package.");
        }
        JObject[] skillRows = selections.OfType<JObject>().Where(value =>
                string.Equals((string)value["profileId"],
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                    StringComparison.Ordinal))
            .ToArray();
        if (skillRows.Length != 1)
            throw new InvalidOperationException(
                "Source-v9 package must contain exactly one skill module selection.");
        JObject selection = skillRows[0];
        string profileId = RequireText(selection, "profileId");
        string requestId = RequireText(selection, "correlationId");
        string requestKey = RequireText(selection, "triggerId");
        string selectionSourceId = RequireText(selection, "selectionSourceId");
        string ownerId = RequireText(selection, "ownerId");
        if (!string.Equals(ownerId, RequireText(selection, "targetId"),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Skill source owner and target differ.");
        JObject capture = RequireObject(selection, "capture");
        JObject worldDescriptor = RequireObject(capture, "officialWorldCheckpoint");
        byte[] worldBytes = ReadDescriptor(source, worldDescriptor,
            "official world checkpoint");
        string worldPath = DescriptorPath(source, worldDescriptor,
            "official world checkpoint");
        if (!string.Equals(worldPath, expectedWorld,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The launched resume world is not the skill source checkpoint.");
        byte[] pendingBytes = ReadDescriptor(source,
            RequireObject(capture, "pending"), "skill pending");
        CharacterSkillDraft pending = JsonUtility.FromJson<CharacterSkillDraft>(
            Encoding.UTF8.GetString(pendingBytes));
        if (pending == null
            || !string.Equals(pending.requestKey, requestKey,
                StringComparison.Ordinal)
            || pending.ownerBinding == null
            || !string.Equals(pending.ownerBinding.persistentId, ownerId,
                StringComparison.Ordinal)
            || pending.isReady || pending.permanentlyChosen
            || pending.requestSubmitted != true
            || pending.nextPresentationIndex != 0
            || (pending.candidates?.Count ?? 0) != 0
            || (pending.moduleSelectionOffers?.Count ?? 0) != 3)
        {
            throw new InvalidOperationException(
                "Skill source pending state is not candidate-store ready.");
        }
        string[] formulaIds = RequireStringArray(selection, "selectionIds");
        if (formulaIds.Length != 3
            || !formulaIds.SequenceEqual(
                pending.moduleSelectionOffers.Select(value => value.selectionId),
                StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Skill selection IDs differ from the pending offer order.");
        JObject wire = RequireObject(selection, "wire");
        byte[] wireBytes = ReadDescriptor(source, RequireObject(wire, "original"),
            "source wire");
        byte[] replayWireBytes = ReadDescriptor(source,
            RequireObject(wire, "restoredReplay"), "restored source wire");
        if ((bool?)wire["byteExactReplay"] != true
            || !wireBytes.SequenceEqual(replayWireBytes))
            throw new InvalidOperationException(
                "Skill source wire was not byte-exact on restore.");
        string sourceWireSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(wireBytes);

        byte[] responseManifestBytes = ReadRequiredFile(response, "manifest.json");
        JObject responseManifest = ParseObject(responseManifestBytes,
            "response manifest");
        if (!string.Equals((string)responseManifest["schema"], ResponseSchema,
                StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest,
                    "sourceManifestSha256"), sourceManifestSha,
                StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "selectionSourceId"),
                selectionSourceId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "profileId"),
                profileId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "requestId"),
                requestId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "wireSha256"),
                sourceWireSha, StringComparison.Ordinal)
            || !HasAllowedResponseOrigin(responseManifest)
            || (bool?)responseManifest["humanApprovalClaimed"] != false
            || (bool?)responseManifest["trainingEligible"] != false
            || (bool?)responseManifest["dpoPerformed"] != false
            || (bool?)responseManifest["releaseReady"] != false)
        {
            throw new InvalidOperationException(
                "Authored skill response is not bound to this source request.");
        }
        byte[] acceptedBytes = ReadDescriptor(response,
            RequireObject(responseManifest, "acceptedResponse"),
            "accepted response");
        string acceptedJson = Encoding.UTF8.GetString(acceptedBytes)
            .TrimEnd('\r', '\n');
        JObject accepted = ParseObject(Encoding.UTF8.GetBytes(acceptedJson),
            "accepted response");
        ValidateResponseAuthoring(response, responseManifest, wireBytes, accepted);
        byte[] canonicalAccepted = GameplayModuleSelectionCanonicalJson
            .ToUtf8(accepted);
        if (!acceptedBytes.SequenceEqual(canonicalAccepted
                .Concat(new[] { (byte)'\n' }).ToArray()))
            throw new InvalidOperationException(
                "Accepted skill response must be canonical JSON plus one newline.");
        string responseSha = GameplayModuleSelectionCanonicalJson.Hash(accepted);
        if (!string.Equals(RequireText(responseManifest,
                "acceptedResponseSha256"), responseSha,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Accepted skill response hash differs from its manifest.");
        if ((bool)responseManifest["integrationFixture"] == false)
        {
            JObject resolved = ResolveObservedSkillContent(source, selection, wireBytes, accepted);
            JObject resolution = RequireObject(responseManifest, "modelReferenceResolution");
            JObject storedResolved = ParseObject(ReadDescriptor(response,
                RequireObject(resolution, "resolvedResponse"), "resolved Skill response"), "resolved Skill response");
            if ((string)resolution["schema"] != "dungeonstory-skill-model-reference-resolution@1"
                || (bool?)resolution["choiceOrProseRepair"] != false
                || (string)resolution["sourcePromptSha256"] != (string)selection["capture"]["originalPrompt"]["sha256"]
                || (string)resolution["resolvedResponseSha256"] != GameplayModuleSelectionCanonicalJson.Hash(resolved)
                || !JToken.DeepEquals(resolved, storedResolved))
                throw new InvalidOperationException("Observed Skill response reference resolution differs.");
            acceptedJson = resolved.ToString();
        }
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                profileId, acceptedJson, out string canonicalResponse,
                out string responseError))
            throw new InvalidOperationException(
                "Accepted skill response is invalid: " + responseError);
        CharacterSkillModuleSelectionBatchResponseDto parsed;
        try
        {
            parsed = JsonUtility.FromJson<
                CharacterSkillModuleSelectionBatchResponseDto>(canonicalResponse);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "Accepted skill response JSON parse failed.", error);
        }
        if (parsed?.candidates == null || parsed.candidates.Count != 3)
            throw new InvalidOperationException(
                "Accepted skill response must contain exactly three candidates.");
        var selectedModules = new List<string>();
        var selectedEvidence = new List<string>();
        for (int index = 0; index < parsed.candidates.Count; index++)
        {
            CharacterSkillModuleSelectionResponseDto candidate =
                parsed.candidates[index];
            CharacterSkillModuleOfferState offer =
                pending.moduleSelectionOffers[index];
            if (candidate == null
                || !string.Equals(candidate.selectionId, offer.selectionId,
                    StringComparison.Ordinal)
                || candidate.positiveModuleIds == null
                || candidate.positiveModuleIds.Count is < 1 or > 3
                || candidate.drawbackModuleIds == null
                || candidate.drawbackModuleIds.Count > 1
                || candidate.evidenceFactIds == null
                || candidate.evidenceFactIds.Count is < 1 or > 2
                || candidate.positiveModuleIds.Any(value =>
                    !offer.positiveModuleIds.Contains(value,
                        StringComparer.Ordinal))
                || candidate.drawbackModuleIds.Any(value =>
                    !offer.drawbackModuleIds.Contains(value,
                        StringComparer.Ordinal))
                || candidate.evidenceFactIds.Any(value =>
                    !offer.evidenceFactIds.Contains(value,
                        StringComparer.Ordinal)))
                throw new InvalidOperationException(
                    "Accepted skill candidate selected an illegal offer value.");
            selectedModules.AddRange(candidate.positiveModuleIds);
            selectedModules.AddRange(candidate.drawbackModuleIds);
            selectedEvidence.AddRange(candidate.evidenceFactIds);
        }
        string catalogSha = RequireText(
            RequireObject(mechanics, "b1EvidenceManifest"), "sha256");
        JObject codeIdentity = GameplayModuleSelectionReplayProofV2Contract
            .CaptureCurrentCodeIdentity(
                project,
                sourceCodeIdentity,
                catalogSha,
                "sha256:" + RequireText(selection, "schemaHash"),
                new JObject
                {
                    ["mode"] = "source-v9-skill-integration-replay",
                    ["sourceSchema"] = 9,
                    ["proofSchema"] = 2,
                    ["networkSent"] = false,
                    ["integrationFixture"] = (bool)responseManifest["integrationFixture"],
                    ["authoringOrigin"] = (string)responseManifest["authoringOrigin"],
                    ["modelReferenceResolution"] = responseManifest["modelReferenceResolution"]?.DeepClone()
                });
        return new GameplayModuleSelectionSkillReplayInput
        {
            SourceManifestSha256 = sourceManifestSha,
            ResponseManifestSha256 = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(responseManifestBytes),
            ProfileId = profileId,
            RequestId = requestId,
            RequestKey = requestKey,
            ResponseSha256 = responseSha,
            SelectionSourceId = selectionSourceId,
            OwnerId = ownerId,
            AcceptedResponseJson = canonicalResponse,
            SourceWorldPath = worldPath,
            SourceWorldSha256 = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(worldBytes),
            SourceWireSha256 = sourceWireSha,
            SelectedModuleIds = selectedModules.Distinct(StringComparer.Ordinal)
                .ToArray(),
            SelectedEvidenceFactIds = selectedEvidence
                .Distinct(StringComparer.Ordinal).ToArray(),
            FormulaIds = formulaIds,
            ReplayCodeIdentity = codeIdentity,
            Response = parsed
        };
    }

    internal static GameplayModuleSelectionEquipmentReplayInput LoadEquipment(
        string projectRoot,
        string sourceRoot,
        string responseRoot,
        string expectedWorldPath)
    {
        string project = RequireDirectory(projectRoot, "project root");
        string source = RequireQaDirectory(project, sourceRoot, "source-v9 package");
        string response = RequireQaDirectory(project, responseRoot, "response package");
        string expectedWorld = RequireQaFile(project, expectedWorldPath,
            "campaign resume world");

        byte[] sourceManifestBytes = ReadRequiredFile(source, "manifest.json");
        JObject sourceManifest = ParseObject(sourceManifestBytes, "source manifest");
        if (!string.Equals((string)sourceManifest["schema"], SourceSchema,
                StringComparison.Ordinal)
            || (int?)sourceManifest["selectionCount"] != 1
            || (bool?)sourceManifest["trainingEligible"] != false
            || (bool?)sourceManifest["humanApprovalClaimed"] != false
            || (bool?)sourceManifest["dpoPerformed"] != false
            || (bool?)sourceManifest["releaseReady"] != false)
        {
            throw new InvalidOperationException(
                "Source-v9 package schema/count/gates are invalid.");
        }
        string sourceManifestSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(sourceManifestBytes);

        JObject mechanics = ParseObject(
            ReadDescriptor(source, RequireObject(sourceManifest, "mechanics"),
                "source mechanics"),
            "source mechanics");
        if (!string.Equals((string)mechanics["schema"],
                "dungeonstory-mechanics-source-v9@1",
                StringComparison.Ordinal)
            || mechanics["codeIdentity"] is not JArray sourceCodeIdentity
            || sourceCodeIdentity.Count == 0
            || (bool?)mechanics["trainingEligible"] != false
            || (bool?)mechanics["humanApprovalClaimed"] != false)
        {
            throw new InvalidOperationException(
                "Source-v9 mechanics identity or gates are invalid.");
        }
        JObject selectionRoot = ParseObject(
            ReadDescriptor(source,
                RequireObject(sourceManifest, "selectionSource"),
                "selection source"),
            "selection source");
        if (!string.Equals((string)selectionRoot["schema"],
                "dungeonstory-selection-source-v9@1",
                StringComparison.Ordinal)
            || selectionRoot["selections"] is not JArray selections
            || selections.Count != 1
            || (bool?)selectionRoot["responseDataIncluded"] != false
            || (bool?)selectionRoot["responseApplied"] != false
            || (bool?)selectionRoot["trainingEligible"] != false)
        {
            throw new InvalidOperationException(
                "Selection source is not a response-free source-v9 selection.");
        }
        JObject selection = selections[0] as JObject
            ?? throw new InvalidOperationException("Equipment selection row is missing.");
        string profileId = RequireText(selection, "profileId");
        if (!string.Equals(profileId,
                LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Replay intake requires the equipment module-selection profile.");
        }
        string requestId = RequireText(selection, "correlationId");
        if (!string.Equals(requestId, RequireText(selection, "triggerId"),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Equipment trigger and correlation identity differ.");
        }
        string selectionSourceId = RequireText(selection, "selectionSourceId");
        string equipmentId = RequireText(selection, "targetId");
        string ownerId = RequireText(selection, "ownerId");
        JObject capture = RequireObject(selection, "capture");
        JObject wire = RequireObject(selection, "wire");
        JObject worldDescriptor = RequireObject(capture, "officialWorldCheckpoint");
        byte[] worldBytes = ReadDescriptor(source, worldDescriptor,
            "official world checkpoint");
        string worldPath = DescriptorPath(source, worldDescriptor,
            "official world checkpoint");
        if (!string.Equals(worldPath, expectedWorld,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The launched resume world is not the source package checkpoint.");
        }
        JObject pending = ParseObject(
            ReadDescriptor(source, RequireObject(capture, "pending"),
                "selection pending"),
            "selection pending");
        string formulaId = RequireText(pending, "nodeId");
        if (!string.Equals(RequireText(pending, "presentationId"), requestId,
                StringComparison.Ordinal)
            || !string.Equals(
                RequireText(pending, "targetPersistentId"),
                equipmentId,
                StringComparison.Ordinal)
            || !string.Equals(
                RequireText(pending, "attunementOwnerPersistentId"),
                ownerId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Equipment pending owner/target/request binding differs.");
        }
        byte[] wireBytes = ReadDescriptor(source,
            RequireObject(wire, "original"), "source wire");
        ReadDescriptor(source, RequireObject(wire, "restoredReplay"),
            "restored source wire");
        if ((bool?)wire["byteExactReplay"] != true)
            throw new InvalidOperationException("Source wire was not byte-exact on restore.");
        string sourceWireSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(wireBytes);

        byte[] responseManifestBytes = ReadRequiredFile(response, "manifest.json");
        JObject responseManifest = ParseObject(responseManifestBytes,
            "response manifest");
        if (!string.Equals((string)responseManifest["schema"], ResponseSchema,
                StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest,
                    "sourceManifestSha256"), sourceManifestSha,
                StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "selectionSourceId"),
                selectionSourceId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "profileId"),
                profileId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "requestId"),
                requestId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "wireSha256"),
                sourceWireSha, StringComparison.Ordinal)
            || !HasAllowedResponseOrigin(responseManifest)
            || (bool?)responseManifest["humanApprovalClaimed"] != false
            || (bool?)responseManifest["trainingEligible"] != false
            || (bool?)responseManifest["dpoPerformed"] != false
            || (bool?)responseManifest["releaseReady"] != false)
        {
            throw new InvalidOperationException(
                "Authored response manifest is not bound to this source request.");
        }
        byte[] acceptedBytes = ReadDescriptor(response,
            RequireObject(responseManifest, "acceptedResponse"),
            "accepted response");
        string acceptedJson = Encoding.UTF8.GetString(acceptedBytes).TrimEnd('\r', '\n');
        JObject accepted = ParseObject(Encoding.UTF8.GetBytes(acceptedJson),
            "accepted response");
        ValidateResponseAuthoring(response, responseManifest, wireBytes, accepted);
        byte[] canonicalAccepted = GameplayModuleSelectionCanonicalJson.ToUtf8(accepted);
        byte[] expectedAcceptedBytes = canonicalAccepted
            .Concat(new[] { (byte)'\n' })
            .ToArray();
        if (!acceptedBytes.SequenceEqual(expectedAcceptedBytes))
            throw new InvalidOperationException(
                "Accepted equipment response must be canonical JSON plus one newline.");
        string responseSha = GameplayModuleSelectionCanonicalJson.Hash(accepted);
        if (!string.Equals(
                RequireText(responseManifest, "acceptedResponseSha256"),
                responseSha,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Accepted equipment response hash differs from its manifest.");
        }
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                profileId,
                acceptedJson,
                out string canonicalResponse,
                out string responseError)
            || !LlmJsonResponseParser.TryParse(
                canonicalResponse,
                out EquipmentEvolutionModuleSelectionResponseDto parsed,
                out responseError))
        {
            throw new InvalidOperationException(
                "Accepted equipment response is invalid: " + responseError);
        }
        if (!string.Equals(parsed.selectionId, requestId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Accepted response selectionId differs from the source request.");
        string[] legalModules = RequireStringArray(selection, "selectionIds");
        string[] selectedModules = parsed.positiveModuleIds
            .Concat(parsed.drawbackModuleIds)
            .ToArray();
        if (selectedModules.Length == 0
            || selectedModules.Distinct(StringComparer.Ordinal).Count()
                != selectedModules.Length
            || selectedModules.Any(value => !legalModules.Contains(value,
                StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Accepted response selected an illegal or duplicate module.");
        }
        string[] legalEvidence = RequireStringArray(pending, "evidenceIds");
        string[] selectedEvidence = parsed.evidenceFactIds.ToArray();
        if (selectedEvidence.Length == 0
            || selectedEvidence.Distinct(StringComparer.Ordinal).Count()
                != selectedEvidence.Length
            || selectedEvidence.Any(value => !legalEvidence.Contains(value,
                StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Accepted response selected illegal or duplicate evidence.");
        }

        string catalogSha = RequireText(
            RequireObject(mechanics, "b1EvidenceManifest"), "sha256");
        string applicationSha = "sha256:" + RequireText(selection, "schemaHash");
        var runnerSettings = new JObject
        {
            ["mode"] = "source-v9-equipment-integration-replay",
            ["sourceSchema"] = 9,
            ["proofSchema"] = 2,
            ["networkSent"] = false,
            ["integrationFixture"] = (bool)responseManifest["integrationFixture"],
                    ["authoringOrigin"] = (string)responseManifest["authoringOrigin"]
        };
        JObject codeIdentity = GameplayModuleSelectionReplayProofV2Contract
            .CaptureCurrentCodeIdentity(
                project,
                sourceCodeIdentity,
                catalogSha,
                applicationSha,
                runnerSettings);

        return new GameplayModuleSelectionEquipmentReplayInput
        {
            SourceManifestSha256 = sourceManifestSha,
            ResponseManifestSha256 = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(responseManifestBytes),
            ProfileId = profileId,
            RequestId = requestId,
            ResponseSha256 = responseSha,
            SelectionSourceId = selectionSourceId,
            EquipmentInstanceId = equipmentId,
            OwnerId = ownerId,
            FormulaId = formulaId,
            AcceptedResponseJson = canonicalResponse,
            SourceWorldPath = worldPath,
            SourceWorldSha256 = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(worldBytes),
            SourceWireSha256 = sourceWireSha,
            SelectedModuleIds = selectedModules,
            SelectedEvidenceFactIds = selectedEvidence,
            ReplayCodeIdentity = codeIdentity,
            Response = parsed
        };
    }

    internal static GameplayModuleSelectionFacilityReplayInput LoadFacility(
        string projectRoot,
        string sourceRoot,
        string responseRoot,
        string expectedWorldPath)
    {
        string project = RequireDirectory(projectRoot, "project root");
        string source = RequireQaDirectory(project, sourceRoot, "source-v9 package");
        string response = RequireQaDirectory(project, responseRoot, "response package");
        string expectedWorld = RequireQaFile(project, expectedWorldPath,
            "campaign resume world");

        byte[] sourceManifestBytes = ReadRequiredFile(source, "manifest.json");
        JObject sourceManifest = ParseObject(sourceManifestBytes, "source manifest");
        if (!string.Equals((string)sourceManifest["schema"], SourceSchema,
                StringComparison.Ordinal)
            || (int?)sourceManifest["selectionCount"] != 1
            || (bool?)sourceManifest["trainingEligible"] != false
            || (bool?)sourceManifest["humanApprovalClaimed"] != false
            || (bool?)sourceManifest["dpoPerformed"] != false
            || (bool?)sourceManifest["releaseReady"] != false)
        {
            throw new InvalidOperationException(
                "Source-v9 facility package schema/count/gates are invalid.");
        }
        string sourceManifestSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(sourceManifestBytes);
        JObject mechanics = ParseObject(
            ReadDescriptor(source, RequireObject(sourceManifest, "mechanics"),
                "source mechanics"),
            "source mechanics");
        if (!string.Equals((string)mechanics["schema"],
                "dungeonstory-mechanics-source-v9@1", StringComparison.Ordinal)
            || mechanics["codeIdentity"] is not JArray sourceCodeIdentity
            || sourceCodeIdentity.Count == 0
            || (bool?)mechanics["trainingEligible"] != false
            || (bool?)mechanics["humanApprovalClaimed"] != false)
        {
            throw new InvalidOperationException(
                "Source-v9 facility mechanics identity or gates are invalid.");
        }
        JObject selectionRoot = ParseObject(
            ReadDescriptor(source, RequireObject(sourceManifest, "selectionSource"),
                "selection source"),
            "selection source");
        if (!string.Equals((string)selectionRoot["schema"],
                "dungeonstory-selection-source-v9@1", StringComparison.Ordinal)
            || selectionRoot["selections"] is not JArray selections
            || selections.Count != 1
            || (bool?)selectionRoot["responseDataIncluded"] != false
            || (bool?)selectionRoot["responseApplied"] != false
            || (bool?)selectionRoot["networkSent"] != false
            || (bool?)selectionRoot["trainingEligible"] != false)
        {
            throw new InvalidOperationException(
                "Facility selection source is not a response-free source-v9 selection.");
        }
        JObject selection = selections[0] as JObject
            ?? throw new InvalidOperationException("Facility selection row is missing.");
        string profileId = RequireText(selection, "profileId");
        if (!string.Equals(profileId,
                LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Replay intake requires the facility module-selection profile.");
        }
        string requestId = RequireText(selection, "correlationId");
        if (!string.Equals(requestId, RequireText(selection, "triggerId"),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Facility trigger and correlation identity differ.");
        }
        string selectionSourceId = RequireText(selection, "selectionSourceId");
        string facilityId = RequireText(selection, "targetId");
        if (!string.Equals(facilityId, RequireText(selection, "ownerId"),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Facility source owner and target differ.");
        JObject capture = RequireObject(selection, "capture");
        JObject worldDescriptor = RequireObject(capture, "officialWorldCheckpoint");
        byte[] worldBytes = ReadDescriptor(source, worldDescriptor,
            "official world checkpoint");
        string worldPath = DescriptorPath(source, worldDescriptor,
            "official world checkpoint");
        if (!string.Equals(worldPath, expectedWorld,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The launched resume world is not the facility source checkpoint.");
        }
        byte[] pendingBytes = ReadDescriptor(source,
            RequireObject(capture, "pending"), "facility pending");
        FacilityEvolutionFormulaPresentationPendingSnapshot pending =
            JsonUtility.FromJson<FacilityEvolutionFormulaPresentationPendingSnapshot>(
                Encoding.UTF8.GetString(pendingBytes));
        if (pending?.node == null
            || !string.Equals(pending.presentationId, requestId,
                StringComparison.Ordinal)
            || !string.Equals(pending.sourceFacilityPersistentId, facilityId,
                StringComparison.Ordinal)
            || !string.Equals(pending.node.presentationId, requestId,
                StringComparison.Ordinal)
            || !string.Equals(pending.node.moduleSelectionId, requestId,
                StringComparison.Ordinal)
            || pending.node.presentationState
                != EquipmentEvolutionPresentationState.ModuleSelectionPending
            || pending.node.formulaVersion < 1
            || (pending.node.moduleSelectionOffers?.Count ?? 0) < 1
            || (pending.node.evidenceIds?.Count ?? 0) < 1)
        {
            throw new InvalidOperationException(
                "Facility source pending state is not module-selection ready.");
        }
        string[] legalModules = RequireStringArray(selection, "selectionIds");
        string[] pendingModules = pending.node.moduleSelectionOffers
            .Where(value => value != null)
            .Select(value => value.moduleId)
            .ToArray();
        if (legalModules.Length != pendingModules.Length
            || legalModules.Except(pendingModules, StringComparer.Ordinal).Any()
            || pendingModules.Except(legalModules, StringComparer.Ordinal).Any())
        {
            throw new InvalidOperationException(
                "Facility selection source and pending offers differ.");
        }
        JObject wire = RequireObject(selection, "wire");
        byte[] wireBytes = ReadDescriptor(source, RequireObject(wire, "original"),
            "source wire");
        byte[] replayWireBytes = ReadDescriptor(source,
            RequireObject(wire, "restoredReplay"), "restored source wire");
        if ((bool?)wire["byteExactReplay"] != true
            || !wireBytes.SequenceEqual(replayWireBytes))
            throw new InvalidOperationException(
                "Facility source wire was not byte-exact on restore.");
        string sourceWireSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(wireBytes);

        byte[] responseManifestBytes = ReadRequiredFile(response, "manifest.json");
        JObject responseManifest = ParseObject(responseManifestBytes,
            "response manifest");
        if (!string.Equals((string)responseManifest["schema"], ResponseSchema,
                StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest,
                    "sourceManifestSha256"), sourceManifestSha,
                StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "selectionSourceId"),
                selectionSourceId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "profileId"),
                profileId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "requestId"),
                requestId, StringComparison.Ordinal)
            || !string.Equals(RequireText(responseManifest, "wireSha256"),
                sourceWireSha, StringComparison.Ordinal)
            || !HasAllowedResponseOrigin(responseManifest)
            || (bool?)responseManifest["humanApprovalClaimed"] != false
            || (bool?)responseManifest["trainingEligible"] != false
            || (bool?)responseManifest["dpoPerformed"] != false
            || (bool?)responseManifest["releaseReady"] != false)
        {
            throw new InvalidOperationException(
                "Authored facility response manifest is not bound to this source request.");
        }
        byte[] acceptedBytes = ReadDescriptor(response,
            RequireObject(responseManifest, "acceptedResponse"),
            "accepted response");
        string acceptedJson = Encoding.UTF8.GetString(acceptedBytes).TrimEnd('\r', '\n');
        JObject accepted = ParseObject(Encoding.UTF8.GetBytes(acceptedJson),
            "accepted response");
        ValidateResponseAuthoring(response, responseManifest, wireBytes, accepted);
        byte[] canonicalAccepted = GameplayModuleSelectionCanonicalJson.ToUtf8(accepted);
        byte[] expectedAcceptedBytes = canonicalAccepted
            .Concat(new[] { (byte)'\n' })
            .ToArray();
        if (!acceptedBytes.SequenceEqual(expectedAcceptedBytes))
            throw new InvalidOperationException(
                "Accepted facility response must be canonical JSON plus one newline.");
        string responseSha = GameplayModuleSelectionCanonicalJson.Hash(accepted);
        if (!string.Equals(
                RequireText(responseManifest, "acceptedResponseSha256"),
                responseSha,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Accepted facility response hash differs from its manifest.");
        }
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                profileId,
                acceptedJson,
                out string canonicalResponse,
                out string responseError)
            || !LlmJsonResponseParser.TryParse(
                canonicalResponse,
                out FacilityFormulaEvolutionModuleSelectionDto parsed,
                out responseError)
            || !parsed.Validate(out responseError))
        {
            throw new InvalidOperationException(
                "Accepted facility response is invalid: " + responseError);
        }
        if (!string.Equals(parsed.selectionId, requestId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Accepted facility response selectionId differs from the source request.");
        string[] selectedModules = parsed.positiveModuleIds
            .Concat(parsed.drawbackModuleIds)
            .ToArray();
        if (parsed.positiveModuleIds.Count != 1
            || parsed.drawbackModuleIds.Count != 0
            || selectedModules.Distinct(StringComparer.Ordinal).Count()
                != selectedModules.Length
            || selectedModules.Any(value => !legalModules.Contains(value,
                StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Accepted facility response did not select one legal positive module and no drawback.");
        }
        EquipmentEvolutionModuleOfferState selectedOffer = pending.node
            .moduleSelectionOffers.SingleOrDefault(value => value != null
                && string.Equals(value.moduleId, selectedModules[0],
                    StringComparison.Ordinal));
        if (selectedOffer == null
            || selectedOffer.polarity != EvolutionModuleOfferPolarity.Positive)
            throw new InvalidOperationException(
                "Accepted facility response selected a non-positive offer.");
        string[] selectedEvidence = parsed.evidenceFactIds.ToArray();
        string[] legalEvidence = pending.node.evidenceIds.ToArray();
        if (selectedEvidence.Length == 0
            || selectedEvidence.Distinct(StringComparer.Ordinal).Count()
                != selectedEvidence.Length
            || selectedEvidence.Any(value => !legalEvidence.Contains(value,
                StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Accepted facility response selected illegal or duplicate evidence.");
        }
        GameplayOutcomeEvidenceBindingSnapshot[] selectedBindings = pending.node
            .gameplayOutcomeEvidence
            .Where(value => value != null
                && selectedEvidence.Contains(value.publicFactId,
                    StringComparer.Ordinal))
            .Select(value => value.Clone())
            .ToArray();
        if (selectedBindings.Length != selectedEvidence.Length
            || selectedBindings.Any(value => !string.Equals(value.sourceKind,
                "exact", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Facility replay requires one exact binding per selected evidence fact.");
        }

        string catalogSha = RequireText(
            RequireObject(mechanics, "b1EvidenceManifest"), "sha256");
        string applicationSha = "sha256:" + RequireText(selection, "schemaHash");
        var runnerSettings = new JObject
        {
            ["mode"] = "source-v9-facility-integration-replay",
            ["sourceSchema"] = 9,
            ["proofSchema"] = 2,
            ["networkSent"] = false,
            ["integrationFixture"] = (bool)responseManifest["integrationFixture"],
                    ["authoringOrigin"] = (string)responseManifest["authoringOrigin"]
        };
        JObject codeIdentity = GameplayModuleSelectionReplayProofV2Contract
            .CaptureCurrentCodeIdentity(
                project,
                sourceCodeIdentity,
                catalogSha,
                applicationSha,
                runnerSettings);

        return new GameplayModuleSelectionFacilityReplayInput
        {
            SourceManifestSha256 = sourceManifestSha,
            ResponseManifestSha256 = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(responseManifestBytes),
            ProfileId = profileId,
            RequestId = requestId,
            ResponseSha256 = responseSha,
            SelectionSourceId = selectionSourceId,
            FacilityId = facilityId,
            RecipeId = pending.recipeId,
            FormulaId = pending.node.nodeId,
            AcceptedResponseJson = canonicalResponse,
            SourceWorldPath = worldPath,
            SourceWorldSha256 = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(worldBytes),
            SourceWireSha256 = sourceWireSha,
            SelectedModuleIds = selectedModules,
            SelectedEvidenceFactIds = selectedEvidence,
            SelectedEvidenceBindings = selectedBindings,
            Pending = pending.Clone(),
            ReplayCodeIdentity = codeIdentity,
            Response = parsed
        };
    }


    internal const string ActualModelOrigin = "local-model-observed";

    private static bool HasAllowedResponseOrigin(JObject manifest) =>
        manifest["authoringOrigin"]?.Type == JTokenType.String
        && manifest["integrationFixture"]?.Type == JTokenType.Boolean
        && (((string)manifest["authoringOrigin"] == "integration-fixture-explicit"
            && (bool?)manifest["integrationFixture"] == true)
        || ((string)manifest["authoringOrigin"] == ActualModelOrigin
            && (bool?)manifest["integrationFixture"] == false));

    internal static JObject ResolveObservedSkillContent(string source, JObject selection,
        byte[] wireBytes, JObject observed)
        => ResolveObservedReferenceContent(source, selection, wireBytes, observed,
            LocalLlmRequestProfiles.CharacterSkillModuleSelection, "Skill");

    internal static JObject ResolveObservedTraitContent(string source, JObject selection,
        byte[] wireBytes, JObject observed)
        => ResolveObservedReferenceContent(source, selection, wireBytes, observed,
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection, "Trait");

    private static JObject ResolveObservedReferenceContent(string source, JObject selection,
        byte[] wireBytes, JObject observed, LocalLlmRequestProfile profile, string label)
    {
        string prompt = Encoding.UTF8.GetString(ReadDescriptor(source,
            RequireObject(RequireObject(selection, "capture"), "originalPrompt"), "original " + label + " prompt"));
        JObject wire = ParseObject(wireBytes, "original " + label + " wire");
        byte[] reconstructed = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
            RequireText(wire, "model"), profile, LlmStaticSchemaCatalog.Require(profile.Id), prompt);
        if (!wireBytes.SequenceEqual(reconstructed))
            throw new InvalidOperationException("Original " + label + " prompt does not reproduce the exact observed wire.");
        if (!DungeonStoryHostStructuredChatBackend.TryResolveModelContent(profile, prompt,
            observed.ToString(), out string resolved, out string error))
            throw new InvalidOperationException("Observed " + label + " reference reject: " + error);
        return ParseObject(Encoding.UTF8.GetBytes(resolved), "resolved " + label + " response");
    }

    // Authorship and gameplay legality are separate gates. These bytes prove
    // which observed model content was adopted; the existing profile/pending
    // validators below still reject illegal IDs, combinations and effects.
    internal static void ValidateResponseAuthoring(
        string responseRoot,
        JObject manifest,
        byte[] sourceWire,
        JObject accepted)
    {
        if (!HasAllowedResponseOrigin(manifest))
            throw new InvalidOperationException("Response authorship origin is invalid.");
        if ((bool)manifest["integrationFixture"])
            return;

        JObject author = RequireObject(manifest, "authoring");
        if ((string)author["schema"] != "dungeonstory-local-model-authoring@1"
            || (string)author["author"] != "bundled-local-model"
            || (bool?)author["networkSent"] != true
            || (int?)author["adoptedAttempt"] != 1
            || author["attempts"] is not JArray attempts || attempts.Count != 1
            || attempts[0] is not JObject attempt
            || (int?)attempt["attempt"] != 1
            || (int?)attempt["httpStatus"] != 200
            || (bool?)attempt["networkSent"] != true
            || (long?)attempt["elapsedMilliseconds"] is not long elapsed || elapsed < 0)
            throw new InvalidOperationException("Observed model attempt metadata is invalid.");

        byte[] sentWire = ReadDescriptor(responseRoot,
            RequireObject(author, "requestWire"), "observed model request");
        if (!sentWire.SequenceEqual(sourceWire)
            || (string)attempt["wireSha256"]
                != GameplayModuleSelectionCanonicalJson.Sha256Prefixed(sourceWire))
            throw new InvalidOperationException("Observed model request differs from exact source wire.");

        byte[] raw = ReadDescriptor(responseRoot,
            RequireObject(author, "responseEnvelope"), "observed model envelope");
        if ((string)attempt["responseEnvelopeSha256"]
                != GameplayModuleSelectionCanonicalJson.Sha256Prefixed(raw))
            throw new InvalidOperationException("Observed model attempt/envelope hash differs.");
        var backend = new DungeonStoryHostStructuredChatBackend();
        if (!backend.TryExtractContent(Encoding.UTF8.GetString(raw),
                out string content, out string extractionError))
            throw new InvalidOperationException("Observed model envelope is invalid: " + extractionError);
        JObject generated = ParseObject(Encoding.UTF8.GetBytes(content), "observed model content");
        if (!JToken.DeepEquals(generated, accepted))
            throw new InvalidOperationException("Accepted response differs from observed model content.");

        JObject host = ParseObject(ReadDescriptor(responseRoot,
            RequireObject(author, "hostManifest"), "observed host manifest"), "host manifest");
        JObject wire = ParseObject(sourceWire, "source model wire");
        string alias = Path.GetFileNameWithoutExtension(RequireText(host, "modelFile"));
        if ((string)author["model"] != alias || (string)wire["model"] != alias
            || (string)author["modelVersion"] != RequireText(host, "modelVersion")
            || (string)author["trainingState"] != "base-untrained"
            || (string)host["trainingState"] != "base-untrained"
            || (bool?)author["releaseCertified"] != false
            || (bool?)host["releaseCertified"] != false)
            throw new InvalidOperationException("Observed model identity differs from source/host manifest.");
    }

    internal static byte[] ReadDescriptor(
        string root,
        JObject descriptor,
        string label)
    {
        string path = DescriptorPath(root, descriptor, label);
        byte[] bytes = File.ReadAllBytes(path);
        int expectedBytes = (int?)descriptor["bytes"]
            ?? throw new InvalidOperationException(label + " byte count is missing.");
        string expectedSha = RequireText(descriptor, "sha256");
        if (bytes.Length != expectedBytes
            || !string.Equals(
                GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes),
                expectedSha,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(label + " bytes/hash differ.");
        }
        return bytes;
    }

    private static string DescriptorPath(
        string root,
        JObject descriptor,
        string label)
    {
        string relative = RequireText(descriptor, "path").Replace('/',
            Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relative)
            || relative.Split(Path.DirectorySeparatorChar).Contains(".."))
            throw new InvalidOperationException(label + " path escapes its package.");
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(full)
            || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(label + " file is unavailable or unsafe.");
        }
        return full;
    }

    private static byte[] ReadRequiredFile(string root, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name))
            throw new InvalidOperationException("Required package file name is invalid.");
        string path = Path.GetFullPath(Path.Combine(root, name));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path)
            || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new FileNotFoundException("Required package file is missing.", path);
        return File.ReadAllBytes(path);
    }

    internal static JObject ParseObject(byte[] bytes, string label)
    {
        try
        {
            return JObject.Parse(new UTF8Encoding(false, true).GetString(bytes));
        }
        catch (Exception error) when (error is DecoderFallbackException
            or Newtonsoft.Json.JsonException)
        {
            throw new InvalidOperationException(label + " is not canonical UTF-8 JSON.",
                error);
        }
    }

    internal static JObject RequireObject(JObject owner, string key) =>
        owner?[key] as JObject
        ?? throw new InvalidOperationException("Required object is missing: " + key);

    private static string[] RequireStringArray(JObject owner, string key)
    {
        string[] values = (owner?[key] as JArray)?.Values<string>().ToArray()
            ?? throw new InvalidOperationException("Required array is missing: " + key);
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException("Required array is invalid: " + key);
        return values;
    }

    internal static string RequireText(JObject owner, string key)
    {
        string value = (string)owner?[key] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Required text is missing: " + key);
        return value;
    }

    private static string RequireDirectory(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new DirectoryNotFoundException(label + " is missing.");
        string full = Path.GetFullPath(path.Trim());
        if (!Directory.Exists(full)
            || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new DirectoryNotFoundException(label + " is missing or unsafe: " + full);
        return full;
    }

    internal static string RequireQaDirectory(
        string projectRoot,
        string path,
        string label)
    {
        string full = RequireDirectory(path, label);
        string qa = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts", "QA"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(qa, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(label + " must be inside project QA evidence.");
        return full;
    }

    private static string RequireQaFile(
        string projectRoot,
        string path,
        string label)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(label + " is missing.");
        string full = Path.GetFullPath(path.Trim());
        string qa = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts", "QA"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(qa, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(full)
            || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(label + " must be a safe project QA file.");
        }
        return full;
    }
}
#endif
