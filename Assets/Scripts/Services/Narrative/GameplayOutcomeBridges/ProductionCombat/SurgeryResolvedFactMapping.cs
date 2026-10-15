using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static class SurgeryResolvedFactMapping
{
    public static GameplayResultKey ResultKey(
        SurgeryResolvedFactData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate(proposal: true);
        return SurgeryOutcomeIds.ResultKey(source.orderId);
    }

    public static OutcomeWriteRequirements Reserve(
        SurgeryResolvedFact fact,
        long worldEpoch)
    {
        SurgeryResolvedFactData source = fact.Snapshot();
        source.Validate(proposal: true);
        return SurgeryOutcomeAdapter.Requirements(
            ResultKey(source),
            source.absoluteDay,
            worldEpoch,
            succeeded: true);
    }

    public static SurgeryOutcomeReceipt Capture(
        SurgeryResolvedFact fact)
    {
        SurgeryResolvedFactData source = fact.Snapshot();
        source.Validate();
        return new SurgeryOutcomeReceipt(
            ResultKey(source),
            source.absoluteDay,
            source.orderId,
            source.subjectId,
            source.subjectName,
            source.subjectKind,
            source.procedureId,
            source.procedureKind,
            source.doctorId,
            source.targetNodeId,
            source.resultId,
            source.severity,
            source.succeeded,
            FormatSurgeryMaterials(source),
            FormatSurgeryAnatomy(
                source.before,
                source.subjectKind,
                source.targetNodeId),
            FormatSurgeryAnatomy(
                source.after,
                source.subjectKind,
                source.targetNodeId),
            FormatSurgeryChildReferences(source),
            ComputeContextSha256(source));
    }

    public static GameplayDomainFactObserverBinding<
        SurgeryResolvedFact,
        SurgeryOutcomeReceipt> CreateBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:medical.surgery-resolved",
            Capture,
            registry,
            ledger,
            recorder,
            commits,
            Reserve);

    public static string ComputeContextSha256(
        SurgeryResolvedFactData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        SurgeryResolvedFactData data = source.Clone();

        var canonical = new StringBuilder();
        Append(canonical, "surgery-resolved-context-v1");
        Append(canonical, data.orderId);
        Append(canonical, data.subjectId);
        Append(canonical, data.subjectName);
        Append(canonical, (int)data.subjectKind);
        Append(canonical, data.procedureId);
        Append(canonical, (int)data.procedureKind);
        Append(canonical, data.doctorId);
        Append(canonical, data.facilityId);
        Append(canonical, data.targetNodeId);
        Append(canonical, (int)data.origin);
        Append(canonical, data.absoluteDay);
        Append(canonical, data.completedWork);
        Append(canonical, data.resultId);
        Append(canonical, (int)data.severity);
        Append(canonical, data.succeeded);
        Append(canonical, data.materialsConsumed);
        Append(canonical, data.processFluidConsumed);
        Append(canonical, data.anesthesiaConsumed);
        Append(canonical, data.materialSinkInputMassGrams);
        AppendMaterials(canonical, data.materials);
        Append(canonical, data.materialSinkOperationId);
        Append(canonical, data.materialSinkCommitId);
        Append(canonical, data.materialTerminalParentOperationId);
        Append(canonical, data.materialTerminalCommitId);
        Append(canonical, data.replacementOperationId);
        Append(canonical, data.replacementBatchCommitId);
        Append(canonical, data.replacementOutputStackId);
        Append(canonical, data.replacementOutputItemInstanceId);
        AppendAnatomy(canonical, data.before);
        AppendAnatomy(canonical, data.after);
        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static string FormatSurgeryMaterials(
        SurgeryResolvedFactData source) =>
        "consumed=" + (source.materialsConsumed ? "1" : "0")
        + "|sink-mass-grams=" + source.materialSinkInputMassGrams.ToString(
            CultureInfo.InvariantCulture)
        + "|process-fluid=" + (source.processFluidConsumed ? "1" : "0")
        + "|anesthesia=" + (source.anesthesiaConsumed ? "1" : "0")
        + "|items=" + string.Join(";", (source.materials
                ?? new List<SurgicalMaterialRequirement>())
            .Where(material => material != null)
            .Select(material => material.itemId + ":" + material.quantity
                + ":" + (material.optional ? "optional" : "required")));

    private static string FormatSurgeryChildReferences(
        SurgeryResolvedFactData source) =>
        "material-sink=" + (source.materialSinkCommitId ?? string.Empty)
        + "|material-sink-operation="
        + (source.materialSinkOperationId ?? string.Empty)
        + "|material-terminal-parent="
        + (source.materialTerminalParentOperationId ?? string.Empty)
        + "|material-terminal="
        + (source.materialTerminalCommitId ?? string.Empty)
        + "|replacement-operation="
        + (source.replacementOperationId ?? string.Empty)
        + "|replacement-batch="
        + (source.replacementBatchCommitId ?? string.Empty)
        + "|replacement-output-stack="
        + (source.replacementOutputStackId ?? string.Empty)
        + "|replacement-output-item="
        + (source.replacementOutputItemInstanceId ?? string.Empty)
        + "|labor-origin=" + source.origin
        + "|labor-worker=" + (source.doctorId ?? string.Empty)
        + "|labor-work=" + source.completedWork.ToString(
            "R",
            CultureInfo.InvariantCulture)
        + "|body-subject=" + (source.subjectId ?? string.Empty)
        + "|body-node=" + (source.targetNodeId ?? string.Empty);

    private static string FormatSurgeryAnatomy(
        SurgeryAnatomyFactData source,
        SurgicalSubjectKind subjectKind,
        string targetNodeId)
    {
        source.Validate();
        switch (source.availability)
        {
            case SurgeryAnatomyAvailability.CharacterUnavailable:
                return "character-unavailable";
            case SurgeryAnatomyAvailability.WildlifeUnavailable:
                return "wildlife-unavailable";
            case SurgeryAnatomyAvailability.NoLivingAnatomy:
                return "no-living-anatomy:" + subjectKind;
            case SurgeryAnatomyAvailability.NodeUnavailable:
                return "profile=" + source.profileId + "|target="
                    + targetNodeId + "|node-unavailable";
            case SurgeryAnatomyAvailability.Available:
                string encoded = "profile=" + source.profileId
                    + "|life=" + (source.alive ? "alive" : "dead")
                    + "|node=" + source.nodeId
                    + "|health=" + Number(source.currentHealth)
                    + "/" + Number(source.maxHealth)
                    + "|bleeding=" + Number(source.bleeding)
                    + "|infection=" + Number(source.infection)
                    + "|missing=" + (source.missing ? "1" : "0")
                    + "|part=" + (string.IsNullOrWhiteSpace(
                        source.installedPartId)
                            ? "none"
                            : source.installedPartId)
                    + "|part-kind=" + source.installedPartKind
                    + "|rejection=" + Number(source.rejection)
                    + "|mutation=" + Number(source.mutation);
                if (source.hasGolemState)
                {
                    encoded += "|species=Golem|charge="
                        + Number(source.charge)
                        + "|integrity=" + Number(source.integrity);
                }
                return encoded;
            default:
                throw new InvalidOperationException(
                    "surgery-anatomy-fact-unavailable");
        }
    }

    private static void AppendMaterials(
        StringBuilder target,
        IReadOnlyList<SurgicalMaterialRequirement> values)
    {
        Append(target, values.Count);
        foreach (SurgicalMaterialRequirement value in values)
        {
            Append(target, value.itemId);
            Append(target, value.quantity);
            Append(target, value.optional);
        }
    }

    private static void AppendAnatomy(
        StringBuilder target,
        SurgeryAnatomyFactData value)
    {
        Append(target, (int)value.availability);
        Append(target, value.profileId);
        Append(target, value.nodeId);
        Append(target, value.alive);
        Append(target, value.currentHealth);
        Append(target, value.maxHealth);
        Append(target, value.bleeding);
        Append(target, value.infection);
        Append(target, value.missing);
        Append(target, value.installedPartId);
        Append(target, (int)value.installedPartKind);
        Append(target, value.rejection);
        Append(target, value.mutation);
        Append(target, value.hasGolemState);
        Append(target, value.charge);
        Append(target, value.integrity);
    }

    private static string Number(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static void Append(StringBuilder target, string value)
    {
        string text = value ?? string.Empty;
        target.Append(Encoding.UTF8.GetByteCount(text).ToString(
                CultureInfo.InvariantCulture))
            .Append(':')
            .Append(text)
            .Append('|');
    }

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, long value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, float value) =>
        Append(target, Number(value));

    private static void Append(StringBuilder target, bool value) =>
        Append(target, value ? "1" : "0");
}
