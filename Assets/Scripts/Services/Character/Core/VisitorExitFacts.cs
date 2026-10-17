using System;
using System.Globalization;
using DungeonStory.Foundation.Facts;

// Mechanical departure reasons live with the character domain, not the logging adapter.
public enum VisitorDepartureTerminalKind
{
    None = 0, Departed = 1, CaptiveBlocked = 2, IncidentBlocked = 3, GridUnavailable = 4,
    DestinationUnavailable = 5, PathUnavailable = 6, DoorPassageFailed = 7, HandoffFailed = 8
}

public readonly struct FloorItemStolenFact
{
    private readonly FloorItemStolenFactData data;
    public FloorItemStolenFact(FloorItemStolenFactData data) =>
        this.data = (data ?? throw new ArgumentNullException(nameof(data))).Clone();
    public FloorItemStolenFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("floor-stolen-fact-uninitialized")).Clone();
}
[Serializable]
public sealed class FloorItemStolenFactData
{
    public string operationId = string.Empty, parentOperationId = string.Empty, characterId = string.Empty,
        characterDisplayName = string.Empty, sourceStackId = string.Empty, carriedStackId = string.Empty,
        itemInstanceId = string.Empty, itemDefinitionId = string.Empty, componentFingerprint = string.Empty;
    public long ownerRevision, unitMassGrams;
    public int absoluteDay, sourceQuantityBefore, sourceQuantityAfter, carryQuantityBefore, carryQuantityAfter,
        positionX, positionY;
    public void Validate()
    {
        VisitorDepartureFactData.ValidateIdentity(parentOperationId, ownerRevision, characterId, characterDisplayName, absoluteDay);
        if (operationId != parentOperationId + ":floor-theft:1" || carriedStackId != operationId + ":carry"
            || !DomainFactValueSyntax.IsIdentity(sourceStackId) || !DomainFactValueSyntax.IsIdentity(itemDefinitionId)
            || !DomainFactValueSyntax.IsText(itemInstanceId, 192, true)
            || itemInstanceId.Length != 0 && !DomainFactValueSyntax.IsIdentity(itemInstanceId)
            || !DomainFactValueSyntax.IsText(componentFingerprint, 2048, true)
            || sourceQuantityBefore <= 0 || sourceQuantityAfter != sourceQuantityBefore - 1
            || carryQuantityBefore != 0 || carryQuantityAfter != 1 || unitMassGrams <= 0)
            throw new ArgumentException("floor-stolen-fact-invalid");
    }
    public FloorItemStolenFactData Clone() { Validate(); return (FloorItemStolenFactData)MemberwiseClone(); }
}

public readonly struct VisitorDepartureFact
{
    private readonly VisitorDepartureFactData data;
    public VisitorDepartureFact(VisitorDepartureFactData data) =>
        this.data = (data ?? throw new ArgumentNullException(nameof(data))).Clone();
    public VisitorDepartureFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("visitor-departure-fact-uninitialized")).Clone();
}
[Serializable]
public sealed class VisitorDepartureFactData
{
    public string operationId = string.Empty, characterId = string.Empty, characterDisplayName = string.Empty,
        sourceMacro = string.Empty, failureReason = string.Empty, handoffKind = string.Empty, theftPublicationKey = string.Empty;
    public long ownerRevision;
    public int absoluteDay, visitCountBefore, visitCountAfter, equipmentCountBefore, equipmentCountAfter;
    public VisitorDepartureTerminalKind terminalKind;
    public CharacterLifecycleState lifecycleBefore, lifecycleAfter;
    public bool visitingBefore, visitingAfter, equipmentRetired;
    public void Validate()
    {
        ValidateIdentity(operationId, ownerRevision, characterId, characterDisplayName, absoluteDay);
        if (!Enum.IsDefined(typeof(VisitorDepartureTerminalKind), terminalKind) || terminalKind == VisitorDepartureTerminalKind.None
            || !Enum.IsDefined(typeof(CharacterLifecycleState), lifecycleBefore)
            || !Enum.IsDefined(typeof(CharacterLifecycleState), lifecycleAfter)
            || !DomainFactValueSyntax.IsText(sourceMacro, 2048, false)
            || !DomainFactValueSyntax.IsText(handoffKind, 2048, false)
            || !DomainFactValueSyntax.IsText(theftPublicationKey, 1024, true)
            || !DomainFactValueSyntax.IsText(failureReason, 2048, terminalKind == VisitorDepartureTerminalKind.Departed)
            || visitCountBefore < 0 || visitCountAfter < 0 || equipmentCountBefore < 0 || equipmentCountAfter < 0)
            throw new ArgumentException("visitor-departure-fact-invalid");
        if (terminalKind == VisitorDepartureTerminalKind.Departed
            ? failureReason.Length != 0 || lifecycleAfter != CharacterLifecycleState.Despawned || visitingAfter
                || (long)visitCountAfter != (long)visitCountBefore + 1 || equipmentCountAfter != 0
                || equipmentRetired != (equipmentCountBefore > 0)
            : visitingAfter != visitingBefore || visitCountAfter != visitCountBefore
                || equipmentRetired || handoffKind != "none")
            throw new ArgumentException("visitor-departure-fact-effects-invalid");
    }
    public VisitorDepartureFactData Clone() { Validate(); return (VisitorDepartureFactData)MemberwiseClone(); }
    public static void ValidateIdentity(string operation, long revision, string character, string name, int day)
    {
        if (revision <= 0 || day < 1 || !DomainFactValueSyntax.IsIdentity(character)
            || !DomainFactValueSyntax.IsText(name, 4096, false)
            || operation != "visitor-exit:" + character + ":" + revision.ToString(CultureInfo.InvariantCulture)
            || !DomainFactValueSyntax.IsIdentity(operation))
            throw new ArgumentException("visitor-exit-fact-identity-invalid");
    }
}

public readonly struct VisitorDepartureAppliedEvent
{
    public VisitorDepartureAppliedEvent(string characterId, string displayName, int absoluteDay)
    { CharacterId = characterId; DisplayName = displayName; AbsoluteDay = absoluteDay; }
    public string CharacterId { get; }
    public string DisplayName { get; }
    public int AbsoluteDay { get; }
}
