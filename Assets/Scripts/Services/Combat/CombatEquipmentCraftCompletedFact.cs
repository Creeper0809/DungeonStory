using System;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>The exact completed attempt and preallocated physical output, before any observation.</summary>
public sealed class CombatEquipmentCraftCompletedFact
{
    private readonly CombatEquipmentCraftCompletionFactData data;
    public CombatEquipmentCraftCompletedFact(CombatEquipmentCraftCompletionFactData data)
    { Validate(data); this.data = data.Clone(); }
    public string OwnerIdentity => "combat-craft:order=" + data.orderId + ":attempt="
        + data.qualityAttemptIndex.ToString("D4", CultureInfo.InvariantCulture);
    public long OwnerRevision => data.ownerRevision;
    public CombatEquipmentCraftCompletionFactData Snapshot() => data.Clone();

    public static CombatEquipmentCraftCompletedFact Capture(CombatEquipmentCraftOrderSaveData order,
        ProductionDomainOutputFactPreview preview) => Capture(order, preview, null);

    private static CombatEquipmentCraftCompletedFact Capture(CombatEquipmentCraftOrderSaveData order,
        ProductionDomainOutputFactPreview preview, long? commandRevision)
    {
        if (order == null || !order.attemptOutcomeResolved || !order.qualityOutcomeCommitted
            || !preview.Physical.IsValid || preview.OwnerId != CombatEquipmentCraftOutputTransaction.OwnerStableId(order)
            || preview.Fingerprint != CombatEquipmentCraftOutputTransaction.CaptureOutcomeFingerprint(order)
            || preview.Physical.Receipt.BatchCommitId != CombatEquipmentCraftOutputTransaction.BatchCommitId(order))
            throw new InvalidOperationException("combat-craft-completion-preview-invalid");
        var outputs = preview.Physical.Facts.Select(value => new ProductionDomainPublishedStackSaveData
        {
            outputLineId = value.Stack.OutputLineId, itemId = value.Stack.ItemDefinitionId.Value,
            itemInstanceId = value.Stack.ItemInstanceId, stackId = value.Stack.StackId,
            quantity = value.Stack.Quantity, massGrams = value.Stack.MassGrams, displayText = value.DisplayName
        }).ToArray();
        return FromOwner(order, outputs, preview.Fingerprint, preview.Physical.OwnerRevision,
            preview.Physical.Receipt.BatchCommitId, commandRevision);
    }

    public static CombatEquipmentCraftCompletedFact Restore(CombatEquipmentCraftOrderSaveData order)
    {
        var fact = new CombatEquipmentCraftCompletedFact(order?.completionFact);
        var owner = order.outputPublication;
        long commandRevision = fact.LastCommandRevision;
        if (owner?.observationExpected != true || !owner.outputPublished || !owner.admissionCommitted
            || owner.ownerFacilityId != order.facilityPersistentId || owner.batchCommitId != CombatEquipmentCraftOutputTransaction.BatchCommitId(order)
            || owner.outcomeFingerprint != CombatEquipmentCraftOutputTransaction.CaptureOutcomeFingerprint(order)
            || owner.outputMassGrams != fact.data.outputMassGrams || owner.observationOwnerRevision != fact.OwnerRevision
            || owner.stacks == null || owner.stacks.Count == 0 || owner.stacks.Any(value => value == null)
            || order.commandRevision < commandRevision)
            throw new InvalidOperationException("combat-craft-completion-owner-conflict");
        var current = FromOwner(order, owner.stacks.ToArray(), owner.outcomeFingerprint,
            owner.observationOwnerRevision, owner.batchCommitId, commandRevision);
        if (current.ContentFingerprint() != fact.ContentFingerprint())
            throw new InvalidOperationException("combat-craft-completion-frozen-state-conflict");
        return fact;
    }

    public static CombatEquipmentCraftCompletedFact Restore(CombatEquipmentCraftOrderSaveData order,
        ProductionDomainOutputFactPreview preview)
    {
        var fact = Restore(order);
        if (Capture(order, preview, fact.LastCommandRevision).ContentFingerprint() != fact.ContentFingerprint())
            throw new InvalidOperationException("combat-craft-completion-replay-physical-conflict");
        return fact;
    }

    private static CombatEquipmentCraftCompletedFact FromOwner(CombatEquipmentCraftOrderSaveData order,
        ProductionDomainPublishedStackSaveData[] outputs, string fingerprint, long revision, string commitId,
        long? commandRevision = null) => new(new()
    {
        orderId = order.orderId, qualityAttemptIndex = order.qualityAttemptIndex,
        facilityPersistentId = order.facilityPersistentId, definitionId = order.definitionId, materialId = order.materialId,
        completedWork = order.completedWork, resolvedQuality = order.resolvedQuality, minimumQuality = order.minimumQuality,
        rejectedDisposition = order.rejectedDisposition, resolvedMakerCharacterId = order.resolvedMakerCharacterId,
        resolvedMakerDisplayName = order.resolvedMakerDisplayName, resolvedAbsoluteDay = order.resolvedAbsoluteDay,
        materialTransferCommitId = order.materialTransferCommitId, materialTransferMassGrams = order.materialTransferMassGrams,
        materialTransferInputs = order.materialTransferInputs, contributions = order.contributions,
        // Worker policy may change for the next attempt while delivery is pending.
        // Keep the command prefix that actually existed when this output completed.
        commandReceipts = commandRevision.HasValue
            ? order.commandReceipts?.Where(value => value == null || value.revision <= commandRevision.Value).ToList()
            : order.commandReceipts,
        qualityOutcomeResultKey = order.qualityOutcomeResultKey, outputCommitId = commitId, outputQuantity = order.outputQuantity,
        outputMassGrams = outputs.Sum(value => value.massGrams), ownerRevision = revision, outcomeFingerprint = fingerprint,
        outputs = outputs.ToList()
    });

    private long LastCommandRevision => data.commandReceipts.Count == 0 ? 0L : data.commandReceipts.Max(value => value.revision);

    public string ContentFingerprint()
    {
        var d = new CanonicalSemanticDigestBuilder(); d.Append("combat-craft-completion-fact@1");
        d.Append(data.orderId); d.Append(data.qualityAttemptIndex); d.Append(data.facilityPersistentId); d.Append(data.definitionId);
        d.Append(data.materialId); d.AppendFloat(data.completedWork); d.AppendEnum(data.resolvedQuality); d.AppendEnum(data.minimumQuality);
        d.AppendEnum(data.rejectedDisposition); d.Append(data.resolvedMakerCharacterId); d.Append(data.resolvedMakerDisplayName);
        d.Append(data.resolvedAbsoluteDay); d.Append(data.materialTransferCommitId); d.Append(data.materialTransferMassGrams);
        d.Append(data.qualityOutcomeResultKey); d.Append(data.outputCommitId); d.Append(data.outputQuantity); d.Append(data.outputMassGrams);
        d.Append(data.ownerRevision); d.Append(data.outcomeFingerprint);
        d.Append(data.materialTransferInputs.Count);
        foreach (var v in data.materialTransferInputs.OrderBy(v => v.sourceStackId, StringComparer.Ordinal))
        { d.Append(v.sourceStackId); d.Append(v.itemId); d.Append(v.quantity); }
        d.Append(data.contributions.Count);
        foreach (var v in data.contributions.OrderBy(v => v.characterId, StringComparer.Ordinal))
        { d.Append(v.characterId); d.AppendFloat(v.contributedWork); d.AppendFloat(v.relevantSkill); }
        d.Append(data.commandReceipts.Count);
        foreach (var v in data.commandReceipts.OrderBy(v => v.revision))
        { d.Append(v.commandKind); d.Append(v.revision); d.Append(v.beforeValue); d.Append(v.afterValue); d.Append(v.absoluteDay); }
        d.Append(data.outputs.Count);
        foreach (var v in data.outputs.OrderBy(v => v.stackId, StringComparer.Ordinal))
        { d.Append(v.outputLineId); d.Append(v.itemId); d.Append(v.itemInstanceId); d.Append(v.stackId);
            d.Append(v.quantity); d.Append(v.massGrams); d.Append(v.displayText); }
        return d.ComputeSha256();
    }

    private static void Validate(CombatEquipmentCraftCompletionFactData v)
    {
        if (v == null || !Id(v.orderId) || v.qualityAttemptIndex < 0 || !Id(v.facilityPersistentId) || !Id(v.definitionId)
            || !OptionalId(v.materialId) || !float.IsFinite(v.completedWork) || v.completedWork < 0
            || !Enum.IsDefined(typeof(CombatEquipmentQuality), v.resolvedQuality)
            || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), v.minimumQuality)
            || !Enum.IsDefined(typeof(RejectedOutputDisposition), v.rejectedDisposition)
            || !OptionalId(v.resolvedMakerCharacterId) || !Text(v.resolvedMakerDisplayName, 256, v.resolvedMakerCharacterId == string.Empty)
            || v.resolvedAbsoluteDay < 1 || !OptionalId(v.materialTransferCommitId) || v.materialTransferMassGrams < 0
            || !Text(v.qualityOutcomeResultKey, 2048, false) || !Id(v.outputCommitId) || v.outputQuantity <= 0
            || v.outputMassGrams <= 0 || v.ownerRevision <= 0 || !Sha(v.outcomeFingerprint)
            || v.materialTransferInputs == null || v.contributions == null || v.commandReceipts == null || v.outputs == null
            || v.outputs.Count == 0 || v.outputs.Any(x => x == null || !Id(x.outputLineId) || !Id(x.itemId)
                || !OptionalId(x.itemInstanceId) || !Id(x.stackId) || x.quantity <= 0 || x.massGrams <= 0 || !Text(x.displayText,256,false))
            || v.outputs.Select(x => x.stackId).Distinct(StringComparer.Ordinal).Count() != v.outputs.Count
            || v.outputs.Sum(x => (long)x.quantity) != v.outputQuantity || v.outputs.Sum(x => x.massGrams) != v.outputMassGrams
            || v.materialTransferInputs.Any(x => x == null || !Id(x.itemId) || !Id(x.sourceStackId) || x.quantity <= 0)
            || v.contributions.Any(x => x == null || !Id(x.characterId) || !float.IsFinite(x.contributedWork)
                || x.contributedWork <= 0 || !float.IsFinite(x.relevantSkill) || x.relevantSkill < 0)
            || v.commandReceipts.Any(x => x == null || !Text(x.commandKind,256,false) || x.revision < 1 || x.absoluteDay < 0
                || !Text(x.beforeValue,3800,true) || !Text(x.afterValue,3800,true))
            || v.commandReceipts.Select(x => x.revision).Distinct().Count() != v.commandReceipts.Count)
            throw new ArgumentException("combat-craft-completion-fact-invalid");
    }
    private static bool Id(string v) => DomainFactValueSyntax.IsIdentity(v);
    private static bool OptionalId(string v) => v == string.Empty || Id(v);
    private static bool Text(string v, int bound, bool empty) => DomainFactValueSyntax.IsText(v,bound,empty);
    private static bool Sha(string v) => v?.Length == 64 && v.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
}
