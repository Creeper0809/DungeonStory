using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using VContainer;

/// <summary>
/// Definition-only projector used by the first mass-authority slice. Stateful
/// equipment, apparel, carcass, and packaged-lot projectors are registered by
/// their owning integration slices rather than inferred here.
/// </summary>
public sealed class GenericDefinitionPhysicalItemMassProjector :
    IPhysicalItemDefinitionMassProjector,
    IPackagedLotDefinitionQuery
{
    private readonly Dictionary<string, CapturedMass> masses =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, PackagedLotDefinitionSnapshot>
        packagedLots = new(StringComparer.Ordinal);

    private readonly struct CapturedMass
    {
        internal CapturedMass(PhysicalMassGrams mass)
        {
            Mass = mass;
            Failure = string.Empty;
        }

        internal CapturedMass(string failure)
        {
            Mass = default;
            Failure = failure ?? throw new ArgumentNullException(nameof(failure));
        }

        internal PhysicalMassGrams Mass { get; }
        internal string Failure { get; }
        internal bool IsValid => Failure.Length == 0;
    }

    public GenericDefinitionPhysicalItemMassProjector(
        IDungeonItemCatalogProvider catalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        DungeonItemDefinition[] definitions = catalog.All.ToArray();
        foreach (DungeonItemDefinition definition in definitions)
        {
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "Physical mass catalog contains a null definition.");
            }

            string itemId = RequireCanonicalItemId(definition.ItemId);
            CapturedMass captured;
            try
            {
                captured = new CapturedMass(
                    CaptureCanonicalUnitMass(itemId, definition.UnitWeight));
            }
            catch (Exception exception) when (
                exception is ArgumentOutOfRangeException
                || exception is OverflowException
                || exception is InvalidOperationException)
            {
                captured = new CapturedMass(exception.Message);
            }

            if (!masses.TryAdd(itemId, captured))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical mass definition '{itemId}'.");
            }
        }

        foreach (DungeonItemDefinition definition in definitions
                     .Where(value => value.IsPackagedLot)
                     .OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            CapturePackagedLot(definition, definitions);
        }
    }

    public PhysicalItemMassSubjectKind SubjectKind =>
        PhysicalItemMassSubjectKind.GenericDefinition;

    public PhysicalMassGrams GetUnitMass(PhysicalItemMassSubject subject)
    {
        if (subject.Kind != SubjectKind)
        {
            throw new ArgumentException(
                $"Projector '{SubjectKind}' cannot read subject '{subject.Kind}'.",
                nameof(subject));
        }

        return GetDefinitionUnitMass(subject.ItemId);
    }

    public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId)
    {
        string canonicalItemId = RequireCanonicalItemId(itemId.Value);
        if (!masses.TryGetValue(canonicalItemId, out CapturedMass captured))
        {
            throw new KeyNotFoundException(
                $"Unknown physical item definition '{canonicalItemId}'.");
        }
        if (!captured.IsValid)
        {
            throw new InvalidOperationException(captured.Failure);
        }

        return captured.Mass;
    }

    public bool TryGetPackagedLot(
        ItemDefinitionId itemId,
        out PackagedLotDefinitionSnapshot packagedLot) =>
        packagedLots.TryGetValue(
            RequireCanonicalItemId(itemId.Value),
            out packagedLot);

    private void CapturePackagedLot(
        DungeonItemDefinition definition,
        IReadOnlyList<DungeonItemDefinition> definitions)
    {
        string itemId = RequireCanonicalItemId(definition.ItemId);
        if (definition.PackageTareGrams <= 0
            || definition.PackageTareDisposition is
                PackageTareDisposition.None
                or PackageTareDisposition.BulkInfrastructureNotInUnit)
        {
            throw new InvalidOperationException(
                $"INVALID_PACKAGED_LOT_TARE:{itemId}");
        }

        bool requiresPhysicalOutput = definition.PackageTareDisposition is
            PackageTareDisposition.ReusableContainerReturn
            or PackageTareDisposition.DisposableWasteByproduct
            or PackageTareDisposition.TransferredWithOutput;
        ItemDefinitionId containerItemId = default;
        if (requiresPhysicalOutput)
        {
            string containerId = RequireCanonicalItemId(
                definition.PackageContainerItemId);
            DungeonItemDefinition container = definitions.SingleOrDefault(value =>
                string.Equals(value.ItemId, containerId, StringComparison.Ordinal));
            if (container == null || string.Equals(itemId, containerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PACKAGED_LOT_CONTAINER_MISSING:{itemId}:{containerId}");
            }
            PhysicalMassGrams containerMass = GetDefinitionUnitMass(
                (ItemDefinitionId)containerId);
            if (containerMass.Value != definition.PackageTareGrams)
            {
                throw new InvalidOperationException(
                    $"PACKAGED_LOT_TARE_MASS_MISMATCH:{itemId}:{definition.PackageTareGrams}:{containerMass.Value}");
            }
            containerItemId = (ItemDefinitionId)containerId;
        }

        PackagedLotDefinitionSnapshot snapshot = new(
            (ItemDefinitionId)itemId,
            GetDefinitionUnitMass((ItemDefinitionId)itemId),
            new PhysicalMassGrams(definition.PackageTareGrams),
            definition.PackageTareDisposition,
            containerItemId);
        if (!packagedLots.TryAdd(itemId, snapshot))
        {
            throw new InvalidOperationException(
                $"Duplicate packaged-lot definition '{itemId}'.");
        }
    }

    internal static PhysicalMassGrams CaptureCanonicalUnitMass(
        string itemId,
        float unitWeightKg)
    {
        string canonicalItemId = RequireCanonicalItemId(itemId);
        try
        {
            return PhysicalMassGrams.FromCanonicalKilograms(unitWeightKg);
        }
        catch (Exception exception) when (
            exception is ArgumentOutOfRangeException
            || exception is InvalidOperationException
            || exception is OverflowException)
        {
            throw new InvalidOperationException(
                $"NON_CANONICAL_ITEM_MASS:{canonicalItemId}:{unitWeightKg:R}kg",
                exception);
        }
    }

    private static string RequireCanonicalItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A non-empty pre-canonicalized item ID is required.",
                nameof(itemId));
        }

        return itemId;
    }
}

public sealed class PhysicalItemMassQuery :
    IPhysicalItemMassQuery,
    IPackagedLotDefinitionQuery
{
    private const long DefinitionAuthorityRevision = 3L;
    private readonly Dictionary<PhysicalItemMassSubjectKind, IPhysicalItemMassProjector>
        projectors;
    private readonly IPhysicalItemDefinitionMassProjector definitionProjector;

    public PhysicalItemMassQuery(IDungeonItemCatalogProvider catalog)
        : this(
            new IPhysicalItemMassProjector[]
            {
                new GenericDefinitionPhysicalItemMassProjector(catalog)
            },
            requireInjectionBoundary: false)
    {
    }

    [Inject]
    public PhysicalItemMassQuery(
        IEnumerable<IPhysicalItemMassProjector> projectors)
        : this(projectors, requireInjectionBoundary: true)
    {
    }

    private PhysicalItemMassQuery(
        IEnumerable<IPhysicalItemMassProjector> projectors,
        bool requireInjectionBoundary)
    {
        if (projectors == null)
        {
            throw new ArgumentNullException(nameof(projectors));
        }

        this.projectors = new Dictionary<
            PhysicalItemMassSubjectKind,
            IPhysicalItemMassProjector>();
        IPhysicalItemDefinitionMassProjector capturedDefinitionProjector = null;
        foreach (IPhysicalItemMassProjector projector in projectors)
        {
            if (projector == null)
            {
                throw new InvalidOperationException(
                    "Physical mass projector collection contains null.");
            }
            if (!this.projectors.TryAdd(projector.SubjectKind, projector))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical mass projector '{projector.SubjectKind}'.");
            }
            if (projector.SubjectKind
                    == PhysicalItemMassSubjectKind.GenericDefinition)
            {
                capturedDefinitionProjector = projector
                    as IPhysicalItemDefinitionMassProjector
                    ?? throw new InvalidOperationException(
                        "Generic mass projector must implement the definition projector contract.");
            }
        }

        if (!this.projectors.ContainsKey(
            PhysicalItemMassSubjectKind.GenericDefinition))
        {
            throw new InvalidOperationException(
                "Generic-definition physical mass projector is required.");
        }
        definitionProjector = capturedDefinitionProjector;
        _ = requireInjectionBoundary;
    }

    public long AuthorityRevision => DefinitionAuthorityRevision;

    public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
        definitionProjector.GetDefinitionUnitMass(itemId);

    public bool TryGetPackagedLot(
        ItemDefinitionId itemId,
        out PackagedLotDefinitionSnapshot packagedLot)
    {
        if (definitionProjector is IPackagedLotDefinitionQuery query)
        {
            return query.TryGetPackagedLot(itemId, out packagedLot);
        }

        packagedLot = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysicalMassGrams GetPreparedStackUnitMass(
        PhysicalItemMassSubject subject)
    {
        if (subject == null || !subject.HasPreparedUnitMass)
        {
            return ThrowMissingPreparedUnitMass();
        }

        return subject.PreparedUnitMass;
    }

    public PhysicalMassGrams GetStackUnitMass(
        ItemDefinitionId itemId,
        PhysicalItemMassSubject subject)
    {
        if (subject == null)
        {
            throw new ArgumentNullException(nameof(subject));
        }
        if (subject.HasPreparedUnitMass)
        {
            return GetPreparedStackUnitMass(subject);
        }
        RequireMatchingItem(itemId, subject);
        if (!projectors.TryGetValue(subject.Kind, out IPhysicalItemMassProjector projector))
        {
            throw new InvalidOperationException(
                $"No physical mass projector is registered for '{subject.Kind}'.");
        }

        return projector.GetUnitMass(subject);
    }

    public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
        GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);

    public PhysicalMassGrams GetQuantityMass(
        ItemDefinitionId itemId,
        PhysicalItemMassSubject subject,
        int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        return GetStackUnitMass(itemId, subject).Multiply(quantity);
    }

    private static void RequireMatchingItem(
        ItemDefinitionId itemId,
        PhysicalItemMassSubject subject)
    {
        if (!itemId.IsValid)
        {
            throw new ArgumentException(
                "A valid item definition ID is required.",
                nameof(itemId));
        }
        if (subject == null
            || !subject.ItemId.IsValid
            || !itemId.Equals(subject.ItemId))
        {
            throw new ArgumentException(
                "Physical mass subject item ID does not match the requested item ID.",
                nameof(subject));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PhysicalMassGrams ThrowMissingPreparedUnitMass()
    {
        throw new ArgumentException(
            "A validated prepared physical mass subject is required.",
            "subject");
    }
}
