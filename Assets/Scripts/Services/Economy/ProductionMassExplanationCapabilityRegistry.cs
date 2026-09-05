using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public readonly struct ProductionMassExplanationEquationSubject
{
    public ProductionMassExplanationEquationSubject(
        string ownerStableId,
        long physicalInputGrams,
        long infrastructureInputGrams,
        long declaredExternalInputGrams,
        long physicalOutputGrams,
        long physicalByproductGrams,
        long terminalSinkGrams)
    {
        OwnerStableId = RequireCanonical(ownerStableId);
        PhysicalInputGrams = RequireNonNegative(physicalInputGrams);
        InfrastructureInputGrams = RequireNonNegative(infrastructureInputGrams);
        DeclaredExternalInputGrams = RequireNonNegative(declaredExternalInputGrams);
        PhysicalOutputGrams = RequireNonNegative(physicalOutputGrams);
        PhysicalByproductGrams = RequireNonNegative(physicalByproductGrams);
        TerminalSinkGrams = RequireNonNegative(terminalSinkGrams);
    }

    public string OwnerStableId { get; }
    public long PhysicalInputGrams { get; }
    public long InfrastructureInputGrams { get; }
    public long DeclaredExternalInputGrams { get; }
    public long PhysicalOutputGrams { get; }
    public long PhysicalByproductGrams { get; }
    public long TerminalSinkGrams { get; }
    public long TotalInputGrams => checked(
        checked(PhysicalInputGrams + InfrastructureInputGrams)
        + DeclaredExternalInputGrams);
    public long AccountedWithoutLossGrams => checked(
        checked(PhysicalOutputGrams + PhysicalByproductGrams)
        + TerminalSinkGrams);

    public string CaptureFingerprint()
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-mass-equation-subject@1");
        digest.Append(OwnerStableId);
        digest.Append(PhysicalInputGrams);
        digest.Append(InfrastructureInputGrams);
        digest.Append(DeclaredExternalInputGrams);
        digest.Append(PhysicalOutputGrams);
        digest.Append(PhysicalByproductGrams);
        digest.Append(TerminalSinkGrams);
        return digest.ComputeSha256();
    }

    private static string RequireCanonical(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Mass equation owner ID is noncanonical.");
        return value;
    }

    private static long RequireNonNegative(long value)
    {
        if (value < 0L)
            throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }
}

public readonly struct ProductionMassExplanationDisposition
{
    internal ProductionMassExplanationDisposition(
        string capabilityId,
        int contractVersion,
        PhysicalMassLossKind lossKind,
        long declaredLossGrams,
        string reasonCode,
        string canonicalReceiptPayload,
        string fingerprint)
        : this(
            capabilityId,
            contractVersion,
            PhysicalMassExternalInputKind.None,
            0L,
            lossKind,
            declaredLossGrams,
            reasonCode,
            canonicalReceiptPayload,
            fingerprint)
    {
    }

    internal ProductionMassExplanationDisposition(
        string capabilityId,
        int contractVersion,
        PhysicalMassExternalInputKind externalInputKind,
        long declaredExternalInputGrams,
        PhysicalMassLossKind lossKind,
        long declaredLossGrams,
        string reasonCode,
        string canonicalReceiptPayload,
        string fingerprint)
    {
        if (!Enum.IsDefined(
                typeof(PhysicalMassExternalInputKind),
                externalInputKind)
            || !Enum.IsDefined(typeof(PhysicalMassLossKind), lossKind)
            || declaredExternalInputGrams < 0L
            || declaredLossGrams < 0L
            || (declaredExternalInputGrams == 0L)
                != (externalInputKind == PhysicalMassExternalInputKind.None)
            || (declaredLossGrams == 0L)
                != (lossKind == PhysicalMassLossKind.None)
            || declaredExternalInputGrams > 0L && declaredLossGrams > 0L)
        {
            throw new ArgumentException(
                "Production mass disposition must contain exactly one typed non-negative residual.");
        }
        CapabilityId = capabilityId;
        ContractVersion = contractVersion;
        ExternalInputKind = externalInputKind;
        DeclaredExternalInputGrams = declaredExternalInputGrams;
        LossKind = lossKind;
        DeclaredLossGrams = declaredLossGrams;
        ReasonCode = reasonCode;
        CanonicalReceiptPayload = canonicalReceiptPayload;
        Fingerprint = fingerprint;
    }

    public string CapabilityId { get; }
    public int ContractVersion { get; }
    public PhysicalMassExternalInputKind ExternalInputKind { get; }
    public long DeclaredExternalInputGrams { get; }
    public PhysicalMassLossKind LossKind { get; }
    public long DeclaredLossGrams { get; }
    public string ReasonCode { get; }
    public string CanonicalReceiptPayload { get; }
    public string Fingerprint { get; }
    public bool HasDisposition => DeclaredExternalInputGrams > 0L
        || DeclaredLossGrams > 0L;
}

public interface IProductionMassExplanationCapability
{
    string CapabilityId { get; }
    int ContractVersion { get; }
    ProductionMassExplanationDisposition Resolve(
        ProductionMassExplanationAuthoringSnapshot authoring,
        ProductionMassExplanationEquationSubject subject);
}

public sealed class ProductionMassExplanationCapabilityRegistry
{
    public const string Schema = "production-mass-explanation-registry@1";

    private readonly Dictionary<string, IProductionMassExplanationCapability>
        capabilities;

    public ProductionMassExplanationCapabilityRegistry(
        IEnumerable<IProductionMassExplanationCapability> source)
    {
        IProductionMassExplanationCapability[] ordered = (source
                ?? throw new ArgumentNullException(nameof(source)))
            .OrderBy(value => value?.CapabilityId, StringComparer.Ordinal)
            .ThenBy(value => value?.ContractVersion ?? 0)
            .ToArray();
        if (ordered.Any(value => value == null))
            throw new InvalidOperationException("Mass explanation registry contains null.");
        capabilities = new Dictionary<string, IProductionMassExplanationCapability>(
            StringComparer.Ordinal);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ordered.Length);
        foreach (IProductionMassExplanationCapability capability in ordered)
        {
            ProductionMassExplanationAuthoringSnapshot metadata = new(
                capability.CapabilityId,
                capability.ContractVersion,
                "registry-metadata");
            string key = BuildKey(metadata.CapabilityId, metadata.ContractVersion);
            if (!capabilities.TryAdd(key, capability))
                throw new InvalidOperationException("Duplicate mass explanation capability: " + key);
            digest.Append(capability.CapabilityId);
            digest.Append(capability.ContractVersion);
            digest.Append(capability.GetType().FullName ?? string.Empty);
        }
        RegistryFingerprint = digest.ComputeSha256();
    }

    public string RegistryFingerprint { get; }

    public static ProductionMassExplanationCapabilityRegistry CreateDefault() =>
        new(new IProductionMassExplanationCapability[]
        {
            new ProcessAdditionProductionMassExplanationCapability(),
            new ProcessLossProductionMassExplanationCapability()
        });

    public ProductionMassExplanationDisposition Resolve(
        ProductionMassExplanationAuthoringSnapshot authoring,
        ProductionMassExplanationEquationSubject subject)
    {
        if (authoring.IsEmpty)
            return default;
        string key = BuildKey(authoring.CapabilityId, authoring.ContractVersion);
        if (!capabilities.TryGetValue(key, out IProductionMassExplanationCapability capability))
            throw new InvalidOperationException("Unknown production mass explanation capability: " + key);
        return capability.Resolve(authoring, subject);
    }

    private static string BuildKey(string id, int version) =>
        id + "@" + version.ToString(CultureInfo.InvariantCulture);
}

public sealed class ProcessAdditionProductionMassExplanationCapability :
    IProductionMassExplanationCapability
{
    public const string Id = "process-addition";
    public const int Version = 1;

    public string CapabilityId => Id;
    public int ContractVersion => Version;

    public static string BuildPayload(
        PhysicalMassExternalInputKind externalInputKind,
        string reasonCode)
    {
        if (!Enum.IsDefined(
                typeof(PhysicalMassExternalInputKind),
                externalInputKind)
            || externalInputKind == PhysicalMassExternalInputKind.None
            || !IsCanonicalReason(reasonCode))
        {
            throw new ArgumentException("Process-addition authoring is invalid.");
        }
        return "process-addition@1|mode=residual|externalInputKind="
            + externalInputKind
            + "|reason=" + reasonCode
            + "|physicalSource=false";
    }

    public ProductionMassExplanationDisposition Resolve(
        ProductionMassExplanationAuthoringSnapshot authoring,
        ProductionMassExplanationEquationSubject subject)
    {
        if (!string.Equals(authoring.CapabilityId, Id, StringComparison.Ordinal)
            || authoring.ContractVersion != Version)
        {
            throw new InvalidOperationException(
                "Process-addition capability metadata drifted.");
        }
        ParsePayload(
            authoring.CanonicalPayload,
            out PhysicalMassExternalInputKind externalInputKind,
            out string reason);
        long residual = checked(subject.AccountedWithoutLossGrams
            - subject.TotalInputGrams);
        if (residual <= 0L)
        {
            throw new InvalidOperationException(
                "Process-addition explanation does not close the exact mass equation.");
        }
        string equationFingerprint = subject.CaptureFingerprint();
        string receipt = authoring.CanonicalPayload
            + "|equation=" + equationFingerprint;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-process-addition-disposition@1");
        digest.Append(authoring.CapabilityId);
        digest.Append(authoring.ContractVersion);
        digest.Append(authoring.CanonicalPayload);
        digest.Append(equationFingerprint);
        return new ProductionMassExplanationDisposition(
            Id,
            Version,
            externalInputKind,
            residual,
            PhysicalMassLossKind.None,
            0L,
            reason,
            receipt,
            digest.ComputeSha256());
    }

    private static void ParsePayload(
        string payload,
        out PhysicalMassExternalInputKind externalInputKind,
        out string reason)
    {
        string[] parts = (payload ?? string.Empty).Split('|');
        if (parts.Length != 5
            || parts[0] != "process-addition@1"
            || parts[1] != "mode=residual"
            || !parts[2].StartsWith(
                "externalInputKind=",
                StringComparison.Ordinal)
            || !parts[3].StartsWith("reason=", StringComparison.Ordinal)
            || parts[4] != "physicalSource=false")
        {
            throw new InvalidOperationException(
                "Process-addition payload grammar is invalid.");
        }
        string kindToken = parts[2].Substring("externalInputKind=".Length);
        reason = parts[3].Substring("reason=".Length);
        if (!Enum.TryParse(
                kindToken,
                ignoreCase: false,
                out externalInputKind)
            || !Enum.IsDefined(
                typeof(PhysicalMassExternalInputKind),
                externalInputKind)
            || externalInputKind == PhysicalMassExternalInputKind.None
            || !string.Equals(
                externalInputKind.ToString(),
                kindToken,
                StringComparison.Ordinal)
            || !IsCanonicalReason(reason)
            || !string.Equals(
                payload,
                BuildPayload(externalInputKind, reason),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Process-addition payload values are invalid.");
        }
    }

    private static bool IsCanonicalReason(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return false;
        return value.All(character => character is >= 'a' and <= 'z'
            || character is >= '0' and <= '9'
            || character == '-');
    }
}

public sealed class ProcessLossProductionMassExplanationCapability :
    IProductionMassExplanationCapability
{
    public const string Id = "process-loss";
    public const int Version = 1;

    public string CapabilityId => Id;
    public int ContractVersion => Version;

    public static string BuildPayload(
        PhysicalMassLossKind lossKind,
        string reasonCode)
    {
        if (!Enum.IsDefined(typeof(PhysicalMassLossKind), lossKind)
            || lossKind == PhysicalMassLossKind.None
            || !IsCanonicalReason(reasonCode))
            throw new ArgumentException("Process-loss authoring is invalid.");
        return "process-loss@1|mode=residual|lossKind=" + lossKind
            + "|reason=" + reasonCode
            + "|physicalByproduct=false";
    }

    public ProductionMassExplanationDisposition Resolve(
        ProductionMassExplanationAuthoringSnapshot authoring,
        ProductionMassExplanationEquationSubject subject)
    {
        if (!string.Equals(authoring.CapabilityId, Id, StringComparison.Ordinal)
            || authoring.ContractVersion != Version)
            throw new InvalidOperationException("Process-loss capability metadata drifted.");
        ParsePayload(
            authoring.CanonicalPayload,
            out PhysicalMassLossKind lossKind,
            out string reason);
        long residual = checked(subject.TotalInputGrams
            - subject.AccountedWithoutLossGrams);
        if (residual <= 0L)
        {
            throw new InvalidOperationException(
                "Process-loss explanation does not close the exact mass equation: "
                + "owner=" + subject.OwnerStableId
                + "; physicalInput=" + subject.PhysicalInputGrams
                    .ToString(CultureInfo.InvariantCulture)
                + "; infrastructureInput=" + subject.InfrastructureInputGrams
                    .ToString(CultureInfo.InvariantCulture)
                + "; externalInput=" + subject.DeclaredExternalInputGrams
                    .ToString(CultureInfo.InvariantCulture)
                + "; physicalOutput=" + subject.PhysicalOutputGrams
                    .ToString(CultureInfo.InvariantCulture)
                + "; physicalByproduct=" + subject.PhysicalByproductGrams
                    .ToString(CultureInfo.InvariantCulture)
                + "; terminalSink=" + subject.TerminalSinkGrams
                    .ToString(CultureInfo.InvariantCulture) + ".");
        }
        string equationFingerprint = subject.CaptureFingerprint();
        string receipt = authoring.CanonicalPayload
            + "|equation=" + equationFingerprint;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-process-loss-disposition@1");
        digest.Append(authoring.CapabilityId);
        digest.Append(authoring.ContractVersion);
        digest.Append(authoring.CanonicalPayload);
        digest.Append(equationFingerprint);
        string fingerprint = digest.ComputeSha256();
        return new ProductionMassExplanationDisposition(
            Id,
            Version,
            lossKind,
            residual,
            reason,
            receipt,
            fingerprint);
    }

    private static void ParsePayload(
        string payload,
        out PhysicalMassLossKind lossKind,
        out string reason)
    {
        string[] parts = (payload ?? string.Empty).Split('|');
        if (parts.Length != 5
            || parts[0] != "process-loss@1"
            || parts[1] != "mode=residual"
            || !parts[2].StartsWith("lossKind=", StringComparison.Ordinal)
            || !parts[3].StartsWith("reason=", StringComparison.Ordinal)
            || parts[4] != "physicalByproduct=false")
            throw new InvalidOperationException("Process-loss payload grammar is invalid.");
        string lossToken = parts[2].Substring("lossKind=".Length);
        reason = parts[3].Substring("reason=".Length);
        if (!Enum.TryParse(lossToken, ignoreCase: false, out lossKind)
            || !Enum.IsDefined(typeof(PhysicalMassLossKind), lossKind)
            || lossKind == PhysicalMassLossKind.None
            || !string.Equals(lossKind.ToString(), lossToken, StringComparison.Ordinal)
            || !IsCanonicalReason(reason)
            || !string.Equals(
                payload,
                BuildPayload(lossKind, reason),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Process-loss payload values are invalid.");
    }

    private static bool IsCanonicalReason(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            return false;
        return value.All(character => character is >= 'a' and <= 'z'
            || character is >= '0' and <= '9'
            || character == '-');
    }
}
