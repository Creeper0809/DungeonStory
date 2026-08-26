using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "DungeonStory.Economy")]
public readonly struct PhysicalMassGrams :
    IEquatable<PhysicalMassGrams>,
    IComparable<PhysicalMassGrams>
{
    public PhysicalMassGrams(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Physical mass must be positive.");
        }

        Value = value;
    }

    public long Value { get; }

    public static PhysicalMassGrams FromCanonicalKilograms(float kilograms)
    {
        if (float.IsNaN(kilograms)
            || float.IsInfinity(kilograms)
            || kilograms <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kilograms),
                kilograms,
                "Physical mass kilograms must be positive and finite.");
        }

        double rawGrams = (double)kilograms * 1000d;
        long grams = checked((long)Math.Round(
            rawGrams,
            0,
            MidpointRounding.AwayFromZero));
        if (grams <= 0L || BitConverter.SingleToInt32Bits(kilograms)
            != BitConverter.SingleToInt32Bits(grams / 1000f))
        {
            throw new InvalidOperationException(
                $"Kilogram value '{kilograms:R}' is not an exact 1g projection.");
        }

        return new PhysicalMassGrams(grams);
    }

    public PhysicalMassGrams Multiply(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Physical mass quantity must be positive.");
        }

        return new PhysicalMassGrams(checked(Value * quantity));
    }

    public PhysicalMassGrams Add(PhysicalMassGrams other) =>
        new(checked(Value + other.Value));

    public int CompareTo(PhysicalMassGrams other) =>
        Value.CompareTo(other.Value);

    public bool Equals(PhysicalMassGrams other) => Value == other.Value;

    public override bool Equals(object obj) =>
        obj is PhysicalMassGrams other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => $"{Value}g";
}

public enum PhysicalItemMassSubjectKind
{
    GenericDefinition = 0,
    CombatEquipment = 1,
    Apparel = 2,
    WildlifeCarcass = 3,
    PackagedLot = 4
}

public readonly struct PhysicalItemMassContribution
{
    public PhysicalItemMassContribution(ItemDefinitionId itemId, int quantity)
    {
        if (!itemId.IsValid)
        {
            throw new ArgumentException(
                "A valid contribution item definition ID is required.",
                nameof(itemId));
        }
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        ItemId = itemId;
        Quantity = quantity;
    }

    public ItemDefinitionId ItemId { get; }
    public int Quantity { get; }
}

public readonly struct PhysicalItemComponentSnapshot
{
    private static readonly IReadOnlyList<PhysicalItemMassContribution>
        EmptyMassContributions = Array.AsReadOnly(
            Array.Empty<PhysicalItemMassContribution>());

    private readonly IReadOnlyList<PhysicalItemMassContribution>
        massContributions;

    public PhysicalItemComponentSnapshot(
        string componentTypeId,
        int schemaVersion,
        string canonicalPayload,
        string fingerprint,
        IEnumerable<PhysicalItemMassContribution> massContributions = null,
        PhysicalMassGrams? preparedUnitMass = null)
    {
        ComponentTypeId = RequireCanonicalToken(
            componentTypeId,
            nameof(componentTypeId));
        if (schemaVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        CanonicalPayload = canonicalPayload
            ?? throw new ArgumentNullException(nameof(canonicalPayload));
        Fingerprint = RequireCanonicalToken(fingerprint, nameof(fingerprint));
        SchemaVersion = schemaVersion;
        PhysicalItemMassContribution[] copied = (massContributions
                ?? Enumerable.Empty<PhysicalItemMassContribution>())
            .ToArray();
        this.massContributions = copied.Length == 0
            ? EmptyMassContributions
            : Array.AsReadOnly(copied);
        PreparedUnitMass = preparedUnitMass;
    }

    public string ComponentTypeId { get; }
    public int SchemaVersion { get; }
    public string CanonicalPayload { get; }
    public string Fingerprint { get; }
    public IReadOnlyList<PhysicalItemMassContribution> MassContributions =>
        massContributions ?? EmptyMassContributions;
    public PhysicalMassGrams? PreparedUnitMass { get; }

    private static string RequireCanonicalToken(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A non-empty pre-canonicalized token is required.",
                parameter);
        }

        return value;
    }
}

public sealed class PhysicalItemMassSubject
{
    private static readonly IReadOnlyList<PhysicalItemComponentSnapshot>
        EmptyComponents = Array.AsReadOnly(
            Array.Empty<PhysicalItemComponentSnapshot>());

    private readonly IReadOnlyList<PhysicalItemComponentSnapshot> components;

    private PhysicalItemMassSubject(ItemDefinitionId itemId)
    {
        if (!itemId.IsValid)
        {
            throw new ArgumentException(
                "A valid item definition ID is required.",
                nameof(itemId));
        }

        ItemId = itemId;
        ItemInstanceId = string.Empty;
        Kind = PhysicalItemMassSubjectKind.GenericDefinition;
        components = EmptyComponents;
        ComponentFingerprint = string.Empty;
        HasPreparedUnitMass = false;
        PreparedUnitMass = default;
    }

    public PhysicalItemMassSubject(
        ItemDefinitionId itemId,
        string itemInstanceId,
        PhysicalItemMassSubjectKind kind,
        IEnumerable<PhysicalItemComponentSnapshot> components,
        string componentFingerprint)
    {
        if (!itemId.IsValid)
        {
            throw new ArgumentException(
                "A valid item definition ID is required.",
                nameof(itemId));
        }
        if (!Enum.IsDefined(typeof(PhysicalItemMassSubjectKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        string normalizedInstanceId = itemInstanceId ?? string.Empty;
        if (normalizedInstanceId.Length > 0
            && !string.Equals(
                normalizedInstanceId,
                normalizedInstanceId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Item instance ID must already be canonicalized.",
                nameof(itemInstanceId));
        }

        PhysicalItemComponentSnapshot[] copied = (components
                ?? Enumerable.Empty<PhysicalItemComponentSnapshot>())
            .ToArray();
        string fingerprint = componentFingerprint ?? string.Empty;
        if (fingerprint.Length > 0
            && !string.Equals(fingerprint, fingerprint.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Component fingerprint must already be canonicalized.",
                nameof(componentFingerprint));
        }

        if (kind == PhysicalItemMassSubjectKind.GenericDefinition)
        {
            if (copied.Length != 0 || fingerprint.Length != 0)
            {
                throw new ArgumentException(
                    "Generic-definition mass subjects cannot carry instance components.");
            }
        }
        else if (copied.Length == 0 || fingerprint.Length == 0)
        {
            throw new ArgumentException(
                "Stateful mass subjects require components and a fingerprint.");
        }

        ItemId = itemId;
        ItemInstanceId = normalizedInstanceId;
        Kind = kind;
        this.components = copied.Length == 0
            ? EmptyComponents
            : Array.AsReadOnly(copied);
        ComponentFingerprint = fingerprint;
        PhysicalMassGrams? preparedUnitMass = copied.Length == 1
            ? copied[0].PreparedUnitMass
            : null;
        HasPreparedUnitMass = preparedUnitMass.HasValue;
        PreparedUnitMass = preparedUnitMass.GetValueOrDefault();
    }

    public ItemDefinitionId ItemId { get; }
    public string ItemInstanceId { get; }
    public PhysicalItemMassSubjectKind Kind { get; }
    public IReadOnlyList<PhysicalItemComponentSnapshot> Components =>
        components ?? EmptyComponents;
    public string ComponentFingerprint { get; }
    public bool HasPreparedUnitMass { get; }
    public PhysicalMassGrams PreparedUnitMass { get; }

    public static PhysicalItemMassSubject ForDefinition(ItemDefinitionId itemId) =>
        new(itemId);
}

public readonly struct PhysicalItemLotSnapshot
{
    public PhysicalItemLotSnapshot(
        PhysicalItemMassSubject subject,
        int quantity,
        string diagnosticLotId)
    {
        if (subject == null || !subject.ItemId.IsValid)
        {
            throw new ArgumentException(
                "A valid physical mass subject is required.",
                nameof(subject));
        }
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        string lotId = diagnosticLotId ?? string.Empty;
        if (lotId.Length > 0
            && !string.Equals(lotId, lotId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Diagnostic lot ID must already be canonicalized.",
                nameof(diagnosticLotId));
        }

        Subject = subject;
        Quantity = quantity;
        DiagnosticLotId = lotId;
    }

    public PhysicalItemMassSubject Subject { get; }
    public int Quantity { get; }
    public string DiagnosticLotId { get; }
}

public interface IPhysicalItemMassProjector
{
    PhysicalItemMassSubjectKind SubjectKind { get; }
    PhysicalMassGrams GetUnitMass(PhysicalItemMassSubject subject);
}

public interface IPhysicalItemDefinitionMassProjector :
    IPhysicalItemMassProjector
{
    PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId);
}

public interface IPhysicalItemMassQuery
{
    long AuthorityRevision { get; }

    PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId);

    PhysicalMassGrams GetPreparedStackUnitMass(
        PhysicalItemMassSubject subject);

    PhysicalMassGrams GetStackUnitMass(
        ItemDefinitionId itemId,
        PhysicalItemMassSubject subject);

    PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot);

    PhysicalMassGrams GetQuantityMass(
        ItemDefinitionId itemId,
        PhysicalItemMassSubject subject,
        int quantity);
}
