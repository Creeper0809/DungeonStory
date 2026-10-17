using UnityEngine;

public readonly struct ItemExtractionReceipt
{
    public ItemExtractionReceipt(
        string leaseId,
        string sourceStackId,
        string extractedStackId,
        string itemId,
        int extractedQuantity,
        int sourceRemainingQuantity,
        bool sourceIdentityTransferred)
    {
        LeaseId = leaseId?.Trim() ?? string.Empty;
        SourceStackId = sourceStackId?.Trim() ?? string.Empty;
        ExtractedStackId = extractedStackId?.Trim() ?? string.Empty;
        ItemId = itemId?.Trim() ?? string.Empty;
        ExtractedQuantity = Mathf.Max(0, extractedQuantity);
        SourceRemainingQuantity = Mathf.Max(0, sourceRemainingQuantity);
        SourceIdentityTransferred = sourceIdentityTransferred;
    }

    public string LeaseId { get; }
    public string SourceStackId { get; }
    public string ExtractedStackId { get; }
    public string ItemId { get; }
    public int ExtractedQuantity { get; }
    public int SourceRemainingQuantity { get; }
    public bool SourceIdentityTransferred { get; }
}

public interface IReservedItemTransferService
{
    bool TryExtractReservedQuantity(
        string leaseId,
        int quantity,
        ItemTransitDestination destination,
        out ItemExtractionReceipt receipt,
        out DomainFailure failure);

    bool TryConsumeReservedQuantity(
        string leaseId,
        int quantity,
        out DomainFailure failure);
}

public readonly struct ReservedItemConsumption
{
    public ReservedItemConsumption(string stackId, int quantity)
    {
        StackId = stackId?.Trim() ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
    }

    public string StackId { get; }
    public int Quantity { get; }
    public bool IsValid => !string.IsNullOrEmpty(StackId) && Quantity > 0;
}
