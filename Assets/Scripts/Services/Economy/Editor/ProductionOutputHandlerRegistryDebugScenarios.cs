using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputHandlerRegistryDebugScenarios
{
    private const string CanaryItemA = "item:qa:output-canary-a";
    private const string CanaryItemB = "item:qa:output-canary-b";

    [MenuItem(
        "DungeonStory/V27/Production/Verify Output Capability Registry")]
    public static void RunAll()
    {
        FakeHandler alpha = new(
            "production-output:qa-alpha",
            new[] { CanaryItemA, CanaryItemB });
        FakeHandler omega = new(
            "production-output:qa-omega",
            new[] { "item:qa:output-omega" });
        FakeStandardCapability standard = new(
            new[] { "item:qa:ordinary-definition-only" });

        ProductionOutputHandlerRegistry forward = new(
            new IProductionOutputCapability[] { alpha, omega, standard });
        ProductionOutputHandlerRegistry reverse = new(
            new IProductionOutputCapability[] { standard, omega, alpha });

        Require(
            forward.CapabilityIds.SequenceEqual(
                new[]
                {
                    "production-output:qa-alpha",
                    "production-output:qa-omega",
                    ProductionOutputCapabilityIds.StandardDefinition
                },
                StringComparer.Ordinal),
            "capability IDs were not ordinally frozen");
        Require(
            string.Equals(
                forward.RegistryFingerprint,
                reverse.RegistryFingerprint,
                StringComparison.Ordinal),
            "registry fingerprint depended on DI enumeration order");
        Require(
            forward.TryResolve(CanaryItemA, out IProductionOutputHandler first)
            && ReferenceEquals(first, alpha)
            && forward.TryResolve(
                CanaryItemB,
                out IProductionOutputHandler composed)
            && ReferenceEquals(composed, alpha),
            "parameter canary items did not join the declared capability");
        Require(
            !forward.TryResolve(
                "item:qa:ordinary-definition-only",
                out _),
            "descriptor-only standard capability was exposed as a legacy executable handler");
        ProductionOutputCapabilityDescriptor ordinaryFrozen =
            forward.CaptureDescriptor(
                "output:qa-ordinary",
                "item:qa:ordinary-definition-only");
        Require(
            forward.TryValidateExact(
                ordinaryFrozen,
                out IProductionOutputCapability ordinary,
                out DomainFailure ordinaryFailure)
            && ReferenceEquals(ordinary, standard)
            && !ordinaryFailure.IsFailure
            && !forward.TryResolveExact(
                ordinaryFrozen,
                out _,
                out DomainFailure standardExecutionFailure)
            && standardExecutionFailure.IsFailure,
            "ordinary definition-only capability was not descriptor-only and exact");

        FakeAutomaticPreparedCapability automaticPrepared = new(
            "production-output:qa-automatic-prepared",
            "production-output-codec:qa-automatic-prepared",
            new[] { "item:qa:automatic-prepared" });
        ProductionOutputHandlerRegistry automaticPreparedRegistry = new(
            new IProductionOutputCapability[]
            {
                standard,
                automaticPrepared
            });
        ProductionOutputCapabilityDescriptor automaticPreparedFrozen =
            automaticPreparedRegistry.CaptureDescriptor(
                "output:qa-automatic-prepared",
                "item:qa:automatic-prepared");
        Require(
            string.Equals(
                automaticPreparedFrozen.CapabilityId,
                automaticPrepared.CapabilityId,
                StringComparison.Ordinal)
            && !automaticPreparedRegistry.TryResolve(
                automaticPreparedFrozen.ItemId,
                out _)
            && ProductionPreparedOutputCapabilitySelection
                .ClassifyPhysicalCapabilities(
                    new[] { automaticPreparedFrozen },
                    automaticPreparedRegistry.CapabilityContracts)
                == ProductionOutputCapabilityRoute.PreparedBatch,
            "automatic descriptor-only prepared capability was not selected without becoming a legacy handler");
        ProductionOutputHandlerRegistry ambiguousPrepared = new(
            new IProductionOutputCapability[]
            {
                standard,
                automaticPrepared,
                new FakeAutomaticPreparedCapability(
                    "production-output:qa-automatic-prepared-overlap",
                    "production-output-codec:qa-automatic-prepared-overlap",
                    new[] { automaticPreparedFrozen.ItemId })
            });
        RequireThrows(
            () => ambiguousPrepared.CaptureDescriptor(
                automaticPreparedFrozen.OutputLineId,
                automaticPreparedFrozen.ItemId),
            "overlapping automatic prepared capabilities were accepted");

        ProductionOutputCapabilityDescriptor frozen =
            forward.CaptureDescriptor("output:qa-canary", CanaryItemA);
        Require(
            reverse.TryResolveExact(frozen, out IProductionOutputHandler exact,
                out DomainFailure exactFailure)
            && ReferenceEquals(exact, alpha)
            && !exactFailure.IsFailure,
            "frozen output capability did not survive registry reordering");
        ProductionOutputCapabilityDescriptor drifted = new(
            frozen.OutputLineId,
            frozen.ItemId,
            frozen.CapabilityId,
            frozen.CapabilityVersion + 1,
            frozen.ComponentCodecId,
            frozen.ComponentCodecVersion,
            frozen.Fingerprint);
        Require(
            !reverse.TryResolveExact(drifted, out _, out DomainFailure drift)
            && drift.IsFailure,
            "frozen output capability version drift was accepted");

        FakeDeclaredCapability declared = new(
            "production-output:qa-declared",
            new[] { CanaryItemA });
        ProductionOutputHandlerRegistry declaredRegistry = new(
            new IProductionOutputCapability[]
            {
                standard,
                alpha,
                declared
            });
        Require(
            declaredRegistry.TryResolve(
                CanaryItemA,
                out IProductionOutputHandler automatic)
            && ReferenceEquals(automatic, alpha),
            "declared-only capability polluted generic auto-selection");
        ProductionOutputCapabilityDescriptor declaredFrozen =
            declaredRegistry.CaptureDeclaredDescriptor(
                "output:qa-declared",
                CanaryItemA,
                declared.CapabilityId);
        Require(
            declaredRegistry.TryValidateExact(
                declaredFrozen,
                out IProductionOutputCapability declaredExact,
                out DomainFailure declaredFailure)
            && ReferenceEquals(declaredExact, declared)
            && !declaredFailure.IsFailure,
            "declared-only capability did not preserve exact provenance");
        Require(
            !declaredRegistry.TryResolveExact(
                declaredFrozen,
                out _,
                out DomainFailure executionFailure)
            && executionFailure.IsFailure,
            "declared-only capability was executable as a generic bill handler");

        Require(
            ProductionPreparedOutputCapabilitySelection
                .ClassifyPhysicalCapabilities(
                    new[] { ordinaryFrozen },
                    forward.CapabilityContracts)
                == ProductionOutputCapabilityRoute.PreparedBatch,
            "standard descriptor vector was not routed to prepared output");
        Require(
            ProductionPreparedOutputCapabilitySelection
                .ClassifyPhysicalCapabilities(
                    new[] { frozen },
                    forward.CapabilityContracts)
                == ProductionOutputCapabilityRoute.ExactCapability,
            "special descriptor vector was not routed to exact capability execution");
        RequireThrows(
            () => ProductionPreparedOutputCapabilitySelection
                .ClassifyPhysicalCapabilities(
                    new[] { ordinaryFrozen, frozen },
                    forward.CapabilityContracts),
            "mixed standard and special output vector was accepted");

        RequireThrows(
            () => new ProductionOutputHandlerRegistry(
                new IProductionOutputCapability[]
                {
                    standard,
                    alpha,
                    new FakeHandler(
                        alpha.CapabilityId,
                        new[] { "item:qa:duplicate-capability" })
                }),
            "duplicate capability ID was accepted");
        RequireThrows(
            () => new ProductionOutputHandlerRegistry(
                new IProductionOutputCapability[]
                {
                    standard,
                    new NonIdempotentHandler()
                }),
            "non-idempotent output capability was accepted");
        RequireThrows(
            () => new ProductionOutputHandlerRegistry(
                new IProductionOutputCapability[]
                {
                    standard,
                    new FakePreparedHandler(
                        "production-output:qa-dual-owner",
                        new[] { CanaryItemA })
                }),
            "prepared capability with per-line execution ownership was accepted");
        RequireThrows(
            () => new ProductionOutputHandlerRegistry(
                new IProductionOutputCapability[]
                {
                    standard,
                    new FakeHandler(
                        " production-output:qa-invalid",
                        new[] { CanaryItemA })
                }),
            "noncanonical capability ID was accepted");

        ProductionOutputHandlerRegistry ambiguous = new(
            new IProductionOutputCapability[]
            {
                standard,
                new FakeHandler(
                    "production-output:qa-overlap-a",
                    new[] { CanaryItemA }),
                new FakeHandler(
                    "production-output:qa-overlap-b",
                    new[] { CanaryItemA })
            });
        RequireThrows(
            () => ambiguous.TryResolve(CanaryItemA, out _),
            "overlapping output capability claims were accepted");

        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture =
                CultureInfo.GetCultureInfo("tr-TR");
            ProductionOutputHandlerRegistry culture = new(
                new IProductionOutputCapability[] { omega, standard, alpha });
            Require(
                string.Equals(
                    forward.RegistryFingerprint,
                    culture.RegistryFingerprint,
                    StringComparison.Ordinal),
                "registry fingerprint depended on locale");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }

        Debug.Log(
            "Production output capability registry contracts passed."
            + " capabilities="
            + forward.CapabilityIds.Count.ToString(CultureInfo.InvariantCulture)
            + " fingerprint="
            + forward.RegistryFingerprint);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private class FakeHandler :
        IProductionOutputHandler,
        IIdempotentProductionOutputHandler
    {
        private readonly HashSet<string> itemIds;

        internal FakeHandler(
            string capabilityId,
            IEnumerable<string> itemIds,
            int contractVersion = 1,
            string componentCodecId = "production-output-codec:qa",
            int componentCodecVersion = 1)
        {
            CapabilityId = capabilityId;
            ContractVersion = contractVersion;
            ComponentCodecId = componentCodecId;
            ComponentCodecVersion = componentCodecVersion;
            this.itemIds = new HashSet<string>(
                itemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
        }

        public string CapabilityId { get; }
        public int ContractVersion { get; }
        public string ComponentCodecId { get; }
        public int ComponentCodecVersion { get; }
        public bool SupportsAutomaticSelection => true;

        public bool CanHandle(string itemId) => itemIds.Contains(itemId);

        public bool TryProduce(
            ProductionOutputContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryProduceIdempotent(
            ProductionOutputContext context,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryAcknowledge(
            string commitId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryCaptureCommittedOutput(
            ProductionOutputContext context,
            out ProductionCommittedOutputSnapshot snapshot,
            out DomainFailure failure)
        {
            snapshot = null;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                context.CommitId,
                "registry-fixture-has-no-physical-publication");
            return false;
        }
    }

    private sealed class FakePreparedHandler :
        FakeHandler,
        IProductionPreparedOutputParticipantCapability
    {
        internal FakePreparedHandler(
            string capabilityId,
            IEnumerable<string> itemIds)
            : base(capabilityId, itemIds)
        {
        }
    }

    private sealed class FakeStandardCapability :
        IProductionOutputCapability,
        IProductionPreparedOutputParticipantCapability
    {
        private readonly HashSet<string> itemIds;

        internal FakeStandardCapability(IEnumerable<string> itemIds)
        {
            this.itemIds = new HashSet<string>(
                itemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
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
        public bool CanHandle(string itemId) => itemIds.Contains(itemId);
    }

    private sealed class NonIdempotentHandler : IProductionOutputHandler
    {
        public string CapabilityId => "production-output:qa-non-idempotent";
        public int ContractVersion => 1;
        public string ComponentCodecId => "production-output-codec:qa";
        public int ComponentCodecVersion => 1;
        public bool SupportsAutomaticSelection => true;

        public bool CanHandle(string itemId) => false;

        public bool TryProduce(
            ProductionOutputContext context,
            out string failureReason)
        {
            failureReason = string.Empty;
            return false;
        }
    }

    private sealed class FakeDeclaredCapability : IProductionOutputCapability
    {
        private readonly HashSet<string> itemIds;

        internal FakeDeclaredCapability(
            string capabilityId,
            IEnumerable<string> itemIds)
        {
            CapabilityId = capabilityId;
            this.itemIds = new HashSet<string>(
                itemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
        }

        public string CapabilityId { get; }
        public int ContractVersion => 1;
        public string ComponentCodecId => "production-output-codec:qa-declared";
        public int ComponentCodecVersion => 1;
        public bool SupportsAutomaticSelection => false;
        public bool CanHandle(string itemId) => itemIds.Contains(itemId);
    }

    private sealed class FakeAutomaticPreparedCapability :
        IProductionOutputCapability,
        IProductionPreparedOutputParticipantCapability
    {
        private readonly HashSet<string> itemIds;

        internal FakeAutomaticPreparedCapability(
            string capabilityId,
            string componentCodecId,
            IEnumerable<string> itemIds)
        {
            CapabilityId = capabilityId;
            ComponentCodecId = componentCodecId;
            this.itemIds = new HashSet<string>(
                itemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
        }

        public string CapabilityId { get; }
        public int ContractVersion => 1;
        public string ComponentCodecId { get; }
        public int ComponentCodecVersion => 1;
        public bool SupportsAutomaticSelection => true;
        public bool CanHandle(string itemId) => itemIds.Contains(itemId);
    }
}
