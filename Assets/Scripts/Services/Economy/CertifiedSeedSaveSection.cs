using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

public sealed class CertifiedSeedSaveSection :
    DungeonStrictJsonSaveSection<
        CertifiedSeedWorldSaveData,
        CertifiedSeedRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.certified-seeds";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly ICertifiedSeedPersistence persistence;
    private readonly ICertifiedSeedInputOwnerDescriptorSource inputOwnerSource;
    private readonly ICertifiedSeedInputOwnerRuntime inputOwners;
    private readonly IProductionDomainOutputRestoreJoin outputRestoreJoin;
    private readonly IProductionOutputMaximumMassRegistry maximumMass;
    private readonly IProductionOutputDetachedFacilityCapacityRestoreGuard
        detachedCapacity;

    public CertifiedSeedSaveSection(
        ICertifiedSeedPersistence persistence,
        ICertifiedSeedInputOwnerDescriptorSource inputOwnerSource,
        ICertifiedSeedInputOwnerRuntime inputOwners,
        IProductionDomainOutputRestoreJoin outputRestoreJoin,
        IProductionOutputMaximumMassRegistry maximumMass,
        IProductionOutputDetachedFacilityCapacityRestoreGuard
            detachedCapacity)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.inputOwnerSource = inputOwnerSource
            ?? throw new ArgumentNullException(nameof(inputOwnerSource));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        this.outputRestoreJoin = outputRestoreJoin
            ?? throw new ArgumentNullException(nameof(outputRestoreJoin));
        this.maximumMass = maximumMass
            ?? throw new ArgumentNullException(nameof(maximumMass));
        this.detachedCapacity = detachedCapacity
            ?? throw new ArgumentNullException(nameof(detachedCapacity));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CertifiedSeedWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CertifiedSeedWorldSaveData CapturePayload() =>
        persistence.Capture();

    protected override void ValidateRawPayload(string payloadJson) => ValidatePublicationJson(payloadJson);

    internal static void ValidatePublicationJson(string payloadJson)
    {
        var root = JObject.Parse(payloadJson);
        RequireFields(root, typeof(CertifiedSeedWorldSaveData));
        if (root["orders"] is not JArray orders)
            throw new InvalidOperationException("Certified-seed V7 orders are missing.");
        foreach (var row in orders)
        {
            if (row is not JObject order || order["completionFact"] is not JObject fact
                || order["outputPublication"] is not JObject output)
                throw new InvalidOperationException("Certified-seed V7 observation fields are missing.");
            RequireFields(fact, typeof(CertifiedSeedCompletionFactData));
            RequireFields(output, typeof(ProductionDomainOutputPublicationSaveData));
            if (output["stacks"] is not JArray stacks)
                throw new InvalidOperationException("Certified-seed output stacks are missing.");
            foreach (var item in stacks)
            {
                if (item is not JObject stack)
                    throw new InvalidOperationException("Certified-seed output stack is malformed.");
                RequireFields(stack, typeof(ProductionDomainPublishedStackSaveData));
            }
        }
    }

    private static void RequireFields(JObject value, Type type)
    {
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (field.IsDefined(typeof(NonSerializedAttribute), false)) continue;
            var token = value[field.Name];
            var expected = field.FieldType == typeof(string) ? JTokenType.String
                : field.FieldType == typeof(bool) ? JTokenType.Boolean
                : field.FieldType.IsEnum || field.FieldType == typeof(int) || field.FieldType == typeof(long)
                    ? JTokenType.Integer : JTokenType.Array;
            if (token == null || token.Type != expected)
                throw new InvalidOperationException("Certified-seed fact field is missing or malformed: " + field.Name);
        }
    }

    protected override void ValidateParsedPayload(
        CertifiedSeedWorldSaveData payload) =>
        _ = persistence.BuildRestore(payload);

    protected override CertifiedSeedRestoreCandidate BuildRestoreCandidate(
        CertifiedSeedWorldSaveData payload)
    {
        CertifiedSeedRestoreCandidate validated =
            persistence.BuildRestore(payload);
        IReadOnlyList<CertifiedSeedInputOwnerDescriptor> inputDescriptors =
            inputOwnerSource.BuildInputOwnerDescriptors(validated.Orders);
        if (!inputOwners.TryReplaceForRestore(
                inputDescriptors,
                out string inputOwnerFailure))
        {
            throw new InvalidOperationException(
                "Certified-seed input owner restore join failed: "
                + inputOwnerFailure);
        }
        List<ProductionDomainOutputRestoreAcknowledgement>
            acknowledgements = new();
        foreach (CertifiedSeedOrderSaveData order in validated.Orders
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            ValidateDetachedCapacity(order);
            switch (order.phase)
            {
                case CertifiedSeedOrderPhase.OutputPublished:
                    var acknowledgement = outputRestoreJoin.AdoptPending(order.outputPublication);
                    // An uncertain observer reply must retain its physical marker for exact replay.
                    if (order.outputPublication.observationDelivered)
                    {
                        acknowledgements.Add(acknowledgement);
                        order.outputPublication.outputAcknowledged = true;
                        order.outputPublication.restoredInCurrentTransaction = true;
                        order.phase = CertifiedSeedOrderPhase.OutputRestoredAwaitingInputAcknowledgement;
                    }
                    break;

                case CertifiedSeedOrderPhase
                        .OutputRestoredAwaitingInputAcknowledgement:
                    outputRestoreJoin.RequireNoPending(order.outputPublication);
                    break;

                default:
                    outputRestoreJoin.RequireNoPending(order.outputPublication);
                    break;
            }
        }

        CertifiedSeedRestoreCandidate normalized = persistence.BuildRestore(
            new CertifiedSeedWorldSaveData
            {
                nextOrderSequence = validated.NextOrderSequence,
                lastProcessedOperatingDay =
                    validated.LastProcessedOperatingDay,
                orders = validated.Orders
                    .OrderBy(value => value.orderId, StringComparer.Ordinal)
                    .Select(value => value.DeepClone())
                    .ToList()
            });
        return new CertifiedSeedRestoreCandidate(
            normalized.NextOrderSequence,
            normalized.Orders,
            acknowledgements
                .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
                .ToArray(),
            normalized.LastProcessedOperatingDay);
    }

    private void ValidateDetachedCapacity(CertifiedSeedOrderSaveData order)
    {
        ProductionDomainOutputPublicationSaveData owner =
            order?.outputPublication;
        if (owner == null || owner.IsEmpty)
            return;
        ProductionOutputMaximumMassProjection projection = maximumMass
            .CaptureDeclared(order.outputCapability.ToDescriptor(), 1);
        ProductionOutputBatchMaximumMassProof proof = new(new[]
        {
            projection
        });
        if (!string.Equals(
                proof.SourceDigest,
                owner.maximumMassProofDigest,
                StringComparison.Ordinal)
            || proof.MaximumBatchMassGrams != owner.maximumBatchMassGrams
            || !string.Equals(
                owner.ownerFacilityId,
                order.facilityInstanceId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Certified-seed detached output capacity proof is stale: "
                + (order.orderId ?? string.Empty));
        }
        detachedCapacity.Validate(
            order.orderId,
            order.facilityInstanceId,
            proof,
            owner.capacitySourceDigest,
            owner.requiredMinimumCapacityGrams);
    }

    protected override void PublishRestoreCandidate(
        CertifiedSeedRestoreCandidate candidate)
    {
        outputRestoreJoin.Acknowledge(candidate.OutputAcknowledgements);
        // The certified-seed runtime is not aggregate-root staged. Keep its
        // live dictionary untouched until every fallible physical-marker
        // acknowledgement has succeeded against the staged Items aggregate.
        persistence.Restore(candidate);
    }
}
