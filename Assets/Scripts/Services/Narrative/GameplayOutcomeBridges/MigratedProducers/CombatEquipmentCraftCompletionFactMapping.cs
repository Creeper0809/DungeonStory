using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class CombatEquipmentCraftCompletionFactMapping
{
    private static readonly GameplayOutcomeFactId MaterialFact =
        new("material-id");
    private static readonly GameplayOutcomeFactId InputCommitFact =
        new("input-batch-commit-id");
    private static readonly GameplayOutcomeFactId InputSourcesFact =
        new("input-source-stacks");
    private static readonly GameplayOutcomeFactId QualityResultFact =
        new("quality-result-key");
    private static readonly GameplayOutcomeFactId OutputStacksFact =
        new("output-stacks");
    private static readonly GameplayOutcomeFactId DispositionFact =
        new("output-disposition");
    private static readonly GameplayOutcomeFactId CommandContextFact =
        new("craft-command-context");

    public static MigratedDomainFactCapture Capture(
        CombatEquipmentCraftCompletedFact fact)
    {
        CombatEquipmentCraftCompletedFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        CombatEquipmentCraftCompletionFactData data = value.Snapshot();
        bool accepted = (int)data.resolvedQuality >= (int)data.minimumQuality;
        List<MigratedProducerOutcomeSubject> subjects = new()
        {
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.FacilityKind,
                data.facilityPersistentId,
                "전투 장비 제작 시설",
                MigratedProducerOutcomeIds.FacilityRole)
        };
        if (GameplayOutcomeStableIdSyntax.IsValid(
                data.resolvedMakerCharacterId))
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.CharacterKind,
                data.resolvedMakerCharacterId,
                string.IsNullOrWhiteSpace(data.resolvedMakerDisplayName)
                    ? data.resolvedMakerCharacterId
                    : data.resolvedMakerDisplayName,
                MigratedProducerOutcomeIds.ActorRole));
        }
        ProductionDomainPublishedStackSaveData[] outputs =
            (data.outputs
                ?? new List<ProductionDomainPublishedStackSaveData>())
            .Where(output => output != null)
            .OrderBy(output => output.stackId, StringComparer.Ordinal)
            .ToArray();
        foreach (ProductionDomainPublishedStackSaveData output in outputs
                     .Take(16 - subjects.Count))
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.ItemKind,
                string.IsNullOrWhiteSpace(output.itemInstanceId)
                    ? output.stackId
                    : output.itemInstanceId,
                string.IsNullOrWhiteSpace(output.displayText)
                    ? output.itemId
                    : output.displayText,
                MigratedProducerOutcomeIds.ItemRole));
        }

        GameplayEntityId facilitySubject = subjects[0].EntityId;
        string inputSources = string.Join(",", (data.materialTransferInputs
                ?? new List<CombatEquipmentCraftMaterialTransferInput>())
            .Where(input => input != null)
            .OrderBy(input => input.sourceStackId, StringComparer.Ordinal)
            .Select(input => input.sourceStackId + "=" + input.itemId + "="
                + input.quantity.ToString(CultureInfo.InvariantCulture)));
        string outputStacks = string.Join(",", outputs.Select(output =>
            output.stackId + "=" + output.itemId + "="
            + output.quantity.ToString(CultureInfo.InvariantCulture) + "="
            + (string.IsNullOrWhiteSpace(output.itemInstanceId)
                ? "none"
                : output.itemInstanceId)));
        string commandContext = CanonicalCommands(data.commandReceipts);
        if (inputSources.Length > 3800
            || outputStacks.Length > 3800
            || commandContext.Length > 3800)
        {
            throw new InvalidOperationException(
                "combat-craft-completion-fact-capacity-exceeded");
        }

        GameplayOutcomeFact[] facts =
        {
            Fact(MigratedProducerOutcomeIds.WorkOrderFact, data.orderId),
            Fact(MigratedProducerOutcomeIds.FacilityFact,
                data.facilityPersistentId),
            Fact(MigratedProducerOutcomeIds.EquipmentDefinitionFact,
                data.definitionId),
            Fact(MaterialFact, string.IsNullOrWhiteSpace(data.materialId)
                ? "none"
                : data.materialId),
            Fact(InputCommitFact,
                string.IsNullOrWhiteSpace(data.materialTransferCommitId)
                    ? "none"
                    : data.materialTransferCommitId),
            Fact(InputSourcesFact, inputSources.Length == 0
                ? "none"
                : inputSources),
            Fact(QualityResultFact, data.qualityOutcomeResultKey),
            Fact(MigratedProducerOutcomeIds.BatchFact, data.outputCommitId),
            Fact(OutputStacksFact, outputStacks.Length == 0
                ? "none"
                : outputStacks),
            Fact(MigratedProducerOutcomeIds.QualityFact,
                data.resolvedQuality.ToString()),
            Fact(DispositionFact, accepted
                ? "accepted"
                : data.rejectedDisposition.ToString()),
            Fact(MigratedProducerOutcomeIds.ContributorsFact,
                CanonicalContributors(data.contributions)),
            Fact(CommandContextFact, commandContext)
        };
        int inputQuantity = (data.materialTransferInputs
                ?? new List<CombatEquipmentCraftMaterialTransferInput>())
            .Where(input => input != null)
            .Sum(input => Math.Max(0, input.quantity));
        var details = new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CompletedWorkMetric,
                    Math.Round(data.completedWork * 1000d),
                    MigratedProducerOutcomeIds.MilliWorkUnit,
                    facilitySubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.InputQuantityMetric,
                    inputQuantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    facilitySubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.InputMassMetric,
                    data.materialTransferMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    facilitySubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.OutputQuantityMetric,
                    data.outputQuantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    facilitySubject),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.OutputMassMetric,
                    data.outputMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    facilitySubject)
            },
            facts,
            accepted
                ? "전투 장비 제작 시도가 완료되어 합격 산출물이 확정되었다."
                : "전투 장비 제작 시도가 완료되었으나 품질 기준에 미달했다.",
            provenance: new[] { new GameplayOutcomeProvenanceReference("domain-fact", value.ContentFingerprint()) });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.CombatEquipmentCraftCompletionResult,
            value.OwnerIdentity,
            data.resolvedAbsoluteDay,
            accepted
                ? GameplayOutcomeStatus.Succeeded
                : GameplayOutcomeStatus.Failed,
            details);
    }

    private static string CanonicalCommands(
        IEnumerable<CombatCraftCommandReceiptSaveData> values)
    {
        string result = string.Join(",", (values
                ?? Array.Empty<CombatCraftCommandReceiptSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.revision)
            .Select(value => value.revision.ToString(
                    CultureInfo.InvariantCulture)
                + ":" + value.commandKind + ":"
                + value.beforeValue.Length.ToString(CultureInfo.InvariantCulture)
                + ":" + value.beforeValue
                + value.afterValue.Length.ToString(CultureInfo.InvariantCulture)
                + ":" + value.afterValue));
        return result.Length == 0 ? "none" : result;
    }

    private static string CanonicalContributors(
        IEnumerable<CraftContributionSaveData> values)
    {
        string result = string.Join(",", (values
                ?? Array.Empty<CraftContributionSaveData>())
            .Where(value => value != null
                && GameplayOutcomeStableIdSyntax.IsValid(value.characterId)
                && value.contributedWork > 0f)
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId + "="
                + value.contributedWork.ToString(
                    "R",
                    CultureInfo.InvariantCulture)));
        return result.Length == 0 ? "none" : result;
    }

    private static GameplayOutcomeFact Fact(
        GameplayOutcomeFactId id,
        string value) => new(id, value);
}
