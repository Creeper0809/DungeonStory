using System;
using System.Globalization;
using System.Text;

public static class PhysicalItemTransformedFactMapping
{
    public static MigratedDomainFactCapture Capture(
        PhysicalItemTransformedFact fact)
    {
        PhysicalItemTransformationData data = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        data.Validate();

        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.OperationKind,
            data.operationId,
            data.operationId,
            MigratedProducerOutcomeIds.OperationRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            CreateOutcomeSummary(data),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "physical-transform-context-sha256",
                    ComputeContextSha256(data))
            });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
            data.operationId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static string ComputeContextSha256(
        PhysicalItemTransformationData source)
    {
        PhysicalItemTransformationData data = (source
            ?? throw new ArgumentNullException(nameof(source))).Clone();
        data.Validate();

        var canonical = new StringBuilder();
        Append(canonical, "physical-item-transformation-context-v1");
        Append(canonical, data.operationId);
        Append(canonical, data.reasonCode);
        Append(canonical, data.absoluteDay);
        Append(canonical, data.wholeStack);
        Append(canonical, data.inputQuantity);
        Append(canonical, data.inputMassGrams);
        Append(canonical, data.outputMassGrams);
        Append(canonical, data.outputQuantity);

        Append(canonical, data.inputs.Count);
        foreach (PhysicalItemTransformationInputData input in data.inputs)
        {
            Append(canonical, input.stackId);
            Append(canonical, input.itemId);
            Append(canonical, input.itemInstanceId);
            Append(canonical, input.quantity);
        }

        Append(canonical, data.outputs.Count);
        foreach (PhysicalItemTransformationOutputData output in data.outputs)
        {
            Append(canonical, output.itemId);
            Append(canonical, output.quantity);
            Append(canonical, output.x);
            Append(canonical, output.y);
            Append(canonical, output.state);
            Append(canonical, output.destinationId);
        }

        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static string CreateOutcomeSummary(
        PhysicalItemTransformationData data) =>
        $"{data.reasonCode} 변환으로 입력 {data.inputQuantity}개 "
        + $"({data.inputMassGrams}g)가 출력 {data.outputQuantity}개 "
        + $"({data.outputMassGrams}g)로 확정됐다.";

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

    private static void Append(StringBuilder target, bool value) =>
        Append(target, value ? "1" : "0");
}
