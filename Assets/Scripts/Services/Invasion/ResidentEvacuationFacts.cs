using System;
using System.Collections.Generic;
using System.Globalization;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Immutable evidence for one terminal resident-evacuation transition.
/// Movement and pending-publication ownership remain with the invasion domain.
/// </summary>
public sealed class ResidentEvacuationTerminalFact
{
    private readonly ResidentEvacuationTerminalSaveData value;

    public ResidentEvacuationTerminalFact(
        ResidentEvacuationTerminalSaveData source)
    {
        value = Clone(source
            ?? throw new ArgumentNullException(nameof(source)));
        Validate(value);
    }

    public ResidentEvacuationTerminalSaveData Snapshot() => Clone(value);

    private static void Validate(ResidentEvacuationTerminalSaveData source)
    {
        bool invasion = source.intruderRuntimeIds?.Count > 0;
        bool fire = source.fireCells?.Count > 0;
        bool matchingThreat = source.threatKind switch
        {
            ResidentEvacuationThreatKind.Invasion => invasion && !fire,
            ResidentEvacuationThreatKind.Fire => !invasion && fire,
            ResidentEvacuationThreatKind.InvasionAndFire => invasion && fire,
            _ => false
        };

        if (!DomainFactValueSyntax.IsIdentity(source.operationId)
            || !DomainFactValueSyntax.IsIdentity(source.requestGroupId)
            || source.ownerRevision <= 0L
            || !DomainFactValueSyntax.IsIdentity(source.characterId)
            || !new CharacterId(source.characterId).IsValid
            || !DomainFactValueSyntax.IsText(
                source.characterDisplayName,
                DomainFactValueSyntax.MaximumNameCharacters,
                allowEmpty: false)
            || !Enum.IsDefined(
                typeof(ResidentEvacuationInitiationKind),
                source.initiationKind)
            || !Enum.IsDefined(
                typeof(ResidentEvacuationThreatKind),
                source.threatKind)
            || !Enum.IsDefined(
                typeof(ResidentEvacuationTerminalKind),
                source.terminalKind)
            || source.terminalKind == ResidentEvacuationTerminalKind.None
            || source.requestedAbsoluteDay < 1
            || source.terminalAbsoluteDay < source.requestedAbsoluteDay
            || !ValidateCanonicalStableIds(source.intruderRuntimeIds)
            || !ValidateCanonicalFireCells(source.fireCells)
            || !matchingThreat
            || !source.hasTarget
                && (source.targetX != 0 || source.targetY != 0))
        {
            throw new ArgumentException(
                "resident-evacuation-terminal-fact-invalid",
                nameof(source));
        }
    }

    private static bool ValidateCanonicalStableIds(
        IReadOnlyList<string> values)
    {
        if (values == null)
            return false;

        string previous = null;
        for (int index = 0; index < values.Count; index++)
        {
            string current = values[index];
            if (!DomainFactValueSyntax.IsIdentity(current)
                || previous != null
                    && string.CompareOrdinal(previous, current) >= 0)
            {
                return false;
            }
            previous = current;
        }
        return true;
    }

    private static bool ValidateCanonicalFireCells(
        IReadOnlyList<string> values)
    {
        if (values == null)
            return false;

        string previous = null;
        for (int index = 0; index < values.Count; index++)
        {
            string current = values[index];
            string[] parts = current?.Split(',');
            if (parts?.Length != 2
                || !int.TryParse(
                    parts[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _)
                || !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out _)
                || previous != null
                    && string.CompareOrdinal(previous, current) >= 0)
            {
                return false;
            }
            previous = current;
        }
        return true;
    }

    private static ResidentEvacuationTerminalSaveData Clone(
        ResidentEvacuationTerminalSaveData source) => new()
    {
        operationId = source.operationId ?? string.Empty,
        requestGroupId = source.requestGroupId ?? string.Empty,
        ownerRevision = source.ownerRevision,
        characterId = source.characterId ?? string.Empty,
        characterDisplayName = source.characterDisplayName ?? string.Empty,
        initiationKind = source.initiationKind,
        threatKind = source.threatKind,
        terminalKind = source.terminalKind,
        intruderRuntimeIds = source.intruderRuntimeIds == null
            ? null
            : new List<string>(source.intruderRuntimeIds),
        fireCells = source.fireCells == null
            ? null
            : new List<string>(source.fireCells),
        requestedAbsoluteDay = source.requestedAbsoluteDay,
        terminalAbsoluteDay = source.terminalAbsoluteDay,
        originX = source.originX,
        originY = source.originY,
        hasTarget = source.hasTarget,
        targetX = source.targetX,
        targetY = source.targetY,
        finalX = source.finalX,
        finalY = source.finalY
    };
}

public readonly struct ResidentEvacuationArrivedEvent
{
    public ResidentEvacuationArrivedEvent(CharacterActor actor) =>
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));

    public CharacterActor Actor { get; }
}

public readonly struct OwnerEvacuationArrivedEvent
{
    public OwnerEvacuationArrivedEvent(
        CharacterActor actor,
        bool usedAdministrationRoom)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        UsedAdministrationRoom = usedAdministrationRoom;
    }

    public CharacterActor Actor { get; }
    public bool UsedAdministrationRoom { get; }
}
