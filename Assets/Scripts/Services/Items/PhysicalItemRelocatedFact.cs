using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>Frozen physical movement; no recorder, presentation or salience authority.</summary>
public readonly struct PhysicalItemRelocatedFact
{
    public PhysicalItemRelocatedFact(PreparedPhysicalItemRelocationPreview preview, int absoluteDay,
        WorldItemStackState destinationState, string destinationId)
    {
        var moved = preview.Receipt;
        if (!preview.IsValid || absoluteDay < 1
            || !Enum.IsDefined(typeof(WorldItemStackState), destinationState)
            || destinationState is WorldItemStackState.Carried or WorldItemStackState.InTransit
            || !DomainFactValueSyntax.IsText(destinationId, 256, true)
            || destinationId != destinationId.Trim()
            || !DomainFactValueSyntax.IsIdentity(moved.OperationId)
            || !DomainFactValueSyntax.IsIdentity(moved.ReasonCode)
            || !DomainFactValueSyntax.IsIdentity(moved.SourceStackId)
            || !DomainFactValueSyntax.IsIdentity(moved.DestinationStackId)
            || !DomainFactValueSyntax.IsIdentity(moved.ItemId)
            || (!string.IsNullOrEmpty(preview.ItemInstanceId)
                && !DomainFactValueSyntax.IsIdentity(preview.ItemInstanceId)))
            throw new ArgumentException("physical-relocation-fact-invalid");
        Preview = preview;
        AbsoluteDay = absoluteDay;
        DestinationState = destinationState;
        DestinationId = destinationId;
    }

    public PreparedPhysicalItemRelocationPreview Preview { get; }
    public int AbsoluteDay { get; }
    public WorldItemStackState DestinationState { get; }
    public string DestinationId { get; }
    public string RequestFingerprint => CreateRequestFingerprint(
        Preview.Receipt.SourceStackId, Preview.Receipt.Quantity, Preview.Receipt.DestinationPosition,
        DestinationState, DestinationId, Preview.Receipt.OperationId, Preview.Receipt.ReasonCode);

    public static PhysicalItemRelocatedFact FromSave(PhysicalItemRelocationSaveData saved)
    {
        if (saved == null) throw new ArgumentNullException(nameof(saved));
        return new PhysicalItemRelocatedFact(new PreparedPhysicalItemRelocationPreview(
            new PhysicalItemRelocationReceipt(saved.operationId, saved.reasonCode,
                saved.sourceStackId, saved.destinationStackId, saved.itemDefinitionId,
                saved.quantity, saved.massGrams, new Vector2Int(saved.sourceX, saved.sourceY),
                new Vector2Int(saved.destinationX, saved.destinationY)),
            saved.itemInstanceId, saved.displayText, saved.ownerRevision), saved.absoluteDay,
            (WorldItemStackState)saved.destinationState, saved.destinationId);
    }
    internal static string CreateRequestFingerprint(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode)
    {
        StringBuilder canonical = new();
        Append(canonical, operationId);
        Append(canonical, reasonCode);
        Append(canonical, sourceStackId);
        Append(canonical, quantity.ToString(CultureInfo.InvariantCulture));
        Append(canonical, destinationPosition.x.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, destinationPosition.y.ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, ((int)destinationState).ToString(
            CultureInfo.InvariantCulture));
        Append(canonical, destinationId);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(
                sha.ComputeHash(Encoding.UTF8.GetBytes(
                    canonical.ToString())))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length)
            .Append(':')
            .Append(safe)
            .Append('|');
    }

}
