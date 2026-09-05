using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputMaximumMassRegistryDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/Diagnostics/Run Output Maximum Mass Registry Scenarios")]
    public static void RunAll()
    {
        VerifyAutomaticAndDeclaredProjection();
        VerifyOrderAndCultureDeterminism();
        VerifyDuplicateMissingAndAmbiguousCapabilitiesFail();
        VerifyDescriptorDriftAndOverflowFail();
        Debug.Log("Production output maximum-mass registry scenarios PASS.");
    }

    private static void VerifyAutomaticAndDeclaredProjection()
    {
        FixedMassQuery mass = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            ["item:ordinary"] = 1_000L,
            ["item:special"] = 1_500L
        });
        ProductionOutputMaximumMassRegistry registry = CreateRegistry(mass);

        ProductionOutputMaximumMassProjection ordinary =
            registry.CaptureAutomatic(
                "output:qa:ordinary",
                "item:ordinary",
                3);
        Require(ordinary.MaximumMassGrams == 3_000L
            && ordinary.DefinitionUnitMassGrams == 1_000L
            && ordinary.MaximumQuantity == 3
            && ordinary.MassAuthorityRevision == mass.AuthorityRevision
            && IsLowercaseSha256(ordinary.SourceDigest),
            "Standard automatic maximum-mass projection drifted.");

        ProductionOutputMaximumMassProjection special =
            registry.CaptureAutomatic(
                "output:qa:special",
                "item:special",
                2);
        Require(special.MaximumMassGrams == 3_000L
            && string.Equals(
                special.Descriptor.CapabilityId,
                FakeSpecialCapability.Id,
                StringComparison.Ordinal),
            "Special automatic maximum-mass capability was not selected.");
        ProductionOutputMaximumMassProjection declared =
            registry.CaptureDeclared(special.Descriptor, 2);
        Require(declared.MaximumMassGrams == special.MaximumMassGrams
            && string.Equals(
                declared.SourceDigest,
                special.SourceDigest,
                StringComparison.Ordinal),
            "Declared maximum-mass replay drifted from the frozen descriptor.");

        ProductionOutputMaximumMassProjection selected =
            registry.CaptureForCapability(
                "output:qa:special",
                "item:special",
                FakeSpecialCapability.Id,
                2);
        Require(selected.MaximumMassGrams == special.MaximumMassGrams
            && string.Equals(
                selected.Descriptor.Fingerprint,
                special.Descriptor.Fingerprint,
                StringComparison.Ordinal),
            "Explicit capability maximum-mass projection drifted.");
        ExpectThrows<InvalidOperationException>(() =>
            registry.CaptureForCapability(
                "output:qa:ordinary",
                "item:ordinary",
                FakeSpecialCapability.Id,
                1));
    }

    private static void VerifyOrderAndCultureDeterminism()
    {
        FixedMassQuery mass = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            ["item:ordinary"] = 1_000L,
            ["item:special"] = 1_500L
        });
        IProductionOutputMaximumMassCapability standard =
            new FakeStandardCapability();
        IProductionOutputMaximumMassCapability special =
            new FakeSpecialCapability();
        ProductionOutputMaximumMassRegistry forward = new(
            new[] { standard, special }, mass);
        ProductionOutputMaximumMassRegistry reverse = new(
            new[] { special, standard }, mass);
        Require(forward.CapabilityIds.SequenceEqual(reverse.CapabilityIds)
            && string.Equals(
                forward.RegistryFingerprint,
                reverse.RegistryFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                forward.CaptureAutomatic(
                        "output:qa:special",
                        "item:special",
                        2)
                    .SourceDigest,
                reverse.CaptureAutomatic(
                        "output:qa:special",
                        "item:special",
                        2)
                    .SourceDigest,
                StringComparison.Ordinal),
            "Maximum-mass registry depends on provider insertion order.");
    }

    private static void VerifyDuplicateMissingAndAmbiguousCapabilitiesFail()
    {
        FixedMassQuery mass = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            ["item:ordinary"] = 1_000L,
            ["item:special"] = 1_500L
        });
        ExpectThrows<InvalidOperationException>(() => new
            ProductionOutputMaximumMassRegistry(
                new IProductionOutputMaximumMassCapability[]
                {
                    new FakeStandardCapability(),
                    new FakeStandardCapability()
                },
                mass));
        ExpectThrows<InvalidOperationException>(() => new
            ProductionOutputMaximumMassRegistry(
                new IProductionOutputMaximumMassCapability[]
                {
                    new FakeSpecialCapability()
                },
                mass));

        ProductionOutputMaximumMassRegistry ambiguous = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new FakeStandardCapability(),
                new FakeSpecialCapability(),
                new FakeSecondSpecialCapability()
            },
            mass);
        ExpectThrows<InvalidOperationException>(() =>
            ambiguous.CaptureAutomatic(
                "output:qa:special",
                "item:special",
                1));
    }

    private static void VerifyDescriptorDriftAndOverflowFail()
    {
        FixedMassQuery mass = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            ["item:ordinary"] = 1_000L,
            ["item:special"] = 1_500L,
            ["item:overflow"] = long.MaxValue
        });
        ProductionOutputMaximumMassRegistry registry = CreateRegistry(mass);
        ProductionOutputMaximumMassProjection special =
            registry.CaptureAutomatic(
                "output:qa:special",
                "item:special",
                1);
        ProductionOutputCapabilityDescriptor drifted = new(
            special.Descriptor.OutputLineId,
            special.Descriptor.ItemId,
            special.Descriptor.CapabilityId,
            special.Descriptor.CapabilityVersion + 1,
            special.Descriptor.ComponentCodecId,
            special.Descriptor.ComponentCodecVersion,
            special.Descriptor.Fingerprint);
        ExpectThrows<InvalidOperationException>(() =>
            registry.CaptureDeclared(drifted, 1));

        ProductionOutputMaximumMassRegistry overflow = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new FakeStandardCapability(includeOverflow: true)
            },
            mass);
        ExpectThrows<OverflowException>(() => overflow.CaptureAutomatic(
            "output:qa:overflow",
            "item:overflow",
            2));
    }

    private static ProductionOutputMaximumMassRegistry CreateRegistry(
        FixedMassQuery mass) => new(
        new IProductionOutputMaximumMassCapability[]
        {
            new FakeSpecialCapability(),
            new FakeStandardCapability()
        },
        mass);

    private sealed class FakeStandardCapability :
        IProductionOutputMaximumMassCapability
    {
        private readonly bool includeOverflow;

        public FakeStandardCapability(bool includeOverflow = false)
        {
            this.includeOverflow = includeOverflow;
        }

        public string CapabilityId =>
            ProductionOutputCapabilityIds.StandardDefinition;
        public int ContractVersion =>
            ProductionOutputCapabilityIds.StandardDefinitionVersion;
        public string ComponentCodecId =>
            ProductionOutputCapabilityIds.DefinitionOnlyCodec;
        public int ComponentCodecVersion =>
            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion;
        public bool SupportsAutomaticSelection => true;
        public bool CanHandle(string itemId) =>
            string.Equals(itemId, "item:ordinary", StringComparison.Ordinal)
            || includeOverflow
                && string.Equals(itemId, "item:overflow", StringComparison.Ordinal);

        public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity,
            IPhysicalItemMassQuery massQuery) =>
            ProductionOutputDefinitionMaximumMassProjection.Capture(
                this, descriptor, maximumQuantity, massQuery);
    }

    private sealed class FakeSpecialCapability :
        IProductionOutputMaximumMassCapability
    {
        internal const string Id = "production-output:qa-special";
        public string CapabilityId => Id;
        public int ContractVersion => 2;
        public string ComponentCodecId => "production-output-codec:qa-special";
        public int ComponentCodecVersion => 3;
        public bool SupportsAutomaticSelection => true;
        public bool CanHandle(string itemId) =>
            string.Equals(itemId, "item:special", StringComparison.Ordinal);

        public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity,
            IPhysicalItemMassQuery massQuery) =>
            ProductionOutputDefinitionMaximumMassProjection.Capture(
                this, descriptor, maximumQuantity, massQuery);
    }

    private sealed class FakeSecondSpecialCapability :
        IProductionOutputMaximumMassCapability
    {
        public string CapabilityId => "production-output:qa-special-2";
        public int ContractVersion => 1;
        public string ComponentCodecId => "production-output-codec:qa-special-2";
        public int ComponentCodecVersion => 1;
        public bool SupportsAutomaticSelection => true;
        public bool CanHandle(string itemId) =>
            string.Equals(itemId, "item:special", StringComparison.Ordinal);

        public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity,
            IPhysicalItemMassQuery massQuery) =>
            ProductionOutputDefinitionMaximumMassProjection.Capture(
                this, descriptor, maximumQuantity, massQuery);
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly IReadOnlyDictionary<string, long> byItemId;

        public FixedMassQuery(IReadOnlyDictionary<string, long> byItemId)
        {
            this.byItemId = byItemId;
        }

        public long AuthorityRevision => 17L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId)
        {
            if (!byItemId.TryGetValue(itemId.Value, out long grams))
                throw new InvalidOperationException("Missing fixture mass.");
            return new PhysicalMassGrams(grams);
        }

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(itemId);

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => GetDefinitionUnitMass(itemId).Multiply(quantity);
    }

    private static bool IsLowercaseSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    private static void ExpectThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected exception was not thrown: " + typeof(TException).Name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
