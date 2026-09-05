using System;

/// <summary>
/// Immutable, fixed-point observation used to size a physical production
/// output buffer from measured haul clearance. One milli-hour is 1/1000 hour.
/// The source digest identifies the measurement population and window; the
/// runtime projector never invents a fallback observation.
/// </summary>
public readonly struct ProductionOutputClearanceProfileSnapshot
{
    public ProductionOutputClearanceProfileSnapshot(
        long p95HaulClearanceMilliHours,
        long peakOutputMassGramsPerHour,
        string sourceDigest)
    {
        if (p95HaulClearanceMilliHours <= 0L)
            throw new ArgumentOutOfRangeException(
                nameof(p95HaulClearanceMilliHours));
        if (peakOutputMassGramsPerHour <= 0L)
            throw new ArgumentOutOfRangeException(
                nameof(peakOutputMassGramsPerHour));
        if (!IsSha256(sourceDigest))
            throw new ArgumentException(
                "Production output clearance source digest must be SHA-256.",
                nameof(sourceDigest));

        P95HaulClearanceMilliHours = p95HaulClearanceMilliHours;
        PeakOutputMassGramsPerHour = peakOutputMassGramsPerHour;
        SourceDigest = sourceDigest;
    }

    public long P95HaulClearanceMilliHours { get; }
    public long PeakOutputMassGramsPerHour { get; }
    public string SourceDigest { get; }

    private static bool IsSha256(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            bool hexadecimal = c is >= '0' and <= '9'
                || c is >= 'a' and <= 'f';
            if (!hexadecimal)
                return false;
        }
        return true;
    }
}

public interface IProductionOutputClearanceProfileSource
{
    /// <summary>
    /// Digest of the complete immutable profile authority, not the selected
    /// row. Runtime capacity sources bind both this digest and the selected
    /// row digest so replacing an otherwise identical-looking catalog cannot
    /// pass restore validation silently.
    /// </summary>
    string AuthorityDigest { get; }

    ProductionOutputClearanceProfileSnapshot Capture(
        ProductionFacilityCapacitySubject facility);
}

public enum ProductionOutputClearanceDisposition
{
    Accepted = 0,
    BackpressureExpected = 1,
    Critical = 2
}

/// <summary>
/// Pure assessment. Demand above four cycles remains visible as a nonblocking
/// backpressure diagnostic while the bounded publication target stays at four
/// complete output cycles. Structural and authored-capacity failures remain
/// blocking Critical results.
/// </summary>
public readonly struct ProductionOutputClearanceRequirementAssessment
{
    internal ProductionOutputClearanceRequirementAssessment(
        ProductionOutputClearanceDisposition disposition,
        long maximumCycleCompletionFootprintGrams,
        long measuredClearanceDemandGrams,
        long requiredCapacityGrams,
        long publishedCapacityGrams,
        long requiredCycleMilliCycles,
        long requiredWholeCycles,
        long publishedWholeCycles,
        string failureCode,
        string diagnosticCode,
        string sourceDigest)
    {
        Disposition = disposition;
        MaximumCycleCompletionFootprintGrams =
            maximumCycleCompletionFootprintGrams;
        MeasuredClearanceDemandGrams = measuredClearanceDemandGrams;
        RequiredCapacityGrams = requiredCapacityGrams;
        PublishedCapacityGrams = publishedCapacityGrams;
        RequiredCycleMilliCycles = requiredCycleMilliCycles;
        RequiredWholeCycles = requiredWholeCycles;
        PublishedWholeCycles = publishedWholeCycles;
        FailureCode = failureCode ?? string.Empty;
        DiagnosticCode = diagnosticCode ?? string.Empty;
        SourceDigest = sourceDigest ?? string.Empty;
    }

    public ProductionOutputClearanceDisposition Disposition { get; }
    public bool IsAccepted =>
        Disposition == ProductionOutputClearanceDisposition.Accepted;
    public bool MeetsClearanceTarget => IsAccepted;
    public bool CanPublishBoundedCapacity =>
        Disposition != ProductionOutputClearanceDisposition.Critical;
    public bool IsBlockingCritical =>
        Disposition == ProductionOutputClearanceDisposition.Critical;
    public bool RequiresBackpressure =>
        Disposition == ProductionOutputClearanceDisposition.BackpressureExpected;
    public long MaximumCycleCompletionFootprintGrams { get; }
    public long MeasuredClearanceDemandGrams { get; }
    public long RequiredCapacityGrams { get; }
    public long PublishedCapacityGrams { get; }
    public long RequiredCycleMilliCycles { get; }
    public long RequiredWholeCycles { get; }
    public long PublishedWholeCycles { get; }
    public string FailureCode { get; }
    public string DiagnosticCode { get; }
    public string SourceDigest { get; }
}

/// <summary>
/// Applies the V27 output-buffer contract using integer arithmetic only:
/// max(two cycles, ceil(p95 hours * peak grams/hour)), capped at four cycles.
/// </summary>
public static class ProductionOutputClearanceRequirementProjector
{
    public const string Schema =
        "production-output-clearance-requirement@3";
    public const string BackpressureExpectedDiagnosticCode =
        "PRODUCTION_OUTPUT_CLEARANCE_BACKPRESSURE_EXPECTED";
    public const long MilliHoursPerHour = 1_000L;
    public const long MilliCyclesPerCycle = 1_000L;
    public const long MinimumBufferCycles = 2L;
    public const long MaximumBufferCycles = 4L;

    public static ProductionOutputClearanceRequirementAssessment Assess(
        long maximumCycleCompletionFootprintGrams,
        ProductionOutputClearanceProfileSnapshot profile)
    {
        if (maximumCycleCompletionFootprintGrams <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCycleCompletionFootprintGrams));
        }

        long measuredClearanceDemandGrams = DivideCeiling(
            checked(profile.P95HaulClearanceMilliHours
                * profile.PeakOutputMassGramsPerHour),
            MilliHoursPerHour);
        long twoCycleCapacity = checked(
            maximumCycleCompletionFootprintGrams * MinimumBufferCycles);
        long fourCycleCapacity = checked(
            maximumCycleCompletionFootprintGrams * MaximumBufferCycles);
        long requiredCapacity = Math.Max(
            twoCycleCapacity,
            measuredClearanceDemandGrams);
        long requiredCycleMilliCycles = DivideCeiling(
            checked(requiredCapacity * MilliCyclesPerCycle),
            maximumCycleCompletionFootprintGrams);
        long requiredWholeCycles = DivideCeiling(
            requiredCycleMilliCycles,
            MilliCyclesPerCycle);
        bool exceedsFourCycles = requiredWholeCycles > MaximumBufferCycles;
        long publishedWholeCycles = Math.Min(
            requiredWholeCycles,
            MaximumBufferCycles);
        long publishedCapacity = checked(
            maximumCycleCompletionFootprintGrams * publishedWholeCycles);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(maximumCycleCompletionFootprintGrams);
        digest.Append(profile.P95HaulClearanceMilliHours);
        digest.Append(profile.PeakOutputMassGramsPerHour);
        digest.Append(profile.SourceDigest);
        digest.Append(measuredClearanceDemandGrams);
        digest.Append(requiredCapacity);
        digest.Append(publishedCapacity);
        digest.Append(requiredCycleMilliCycles);
        digest.Append(requiredWholeCycles);
        digest.Append(publishedWholeCycles);
        ProductionOutputClearanceDisposition disposition = exceedsFourCycles
            ? ProductionOutputClearanceDisposition.BackpressureExpected
            : ProductionOutputClearanceDisposition.Accepted;
        string diagnosticCode = exceedsFourCycles
            ? BackpressureExpectedDiagnosticCode
            : string.Empty;
        digest.AppendEnum(disposition);
        digest.Append(string.Empty);
        digest.Append(diagnosticCode);

        return new ProductionOutputClearanceRequirementAssessment(
            disposition,
            maximumCycleCompletionFootprintGrams,
            measuredClearanceDemandGrams,
            requiredCapacity,
            publishedCapacity,
            requiredCycleMilliCycles,
            requiredWholeCycles,
            publishedWholeCycles,
            string.Empty,
            diagnosticCode,
            digest.ComputeSha256());
    }

    private static long DivideCeiling(long numerator, long denominator)
    {
        if (numerator < 0L)
            throw new ArgumentOutOfRangeException(nameof(numerator));
        if (denominator <= 0L)
            throw new ArgumentOutOfRangeException(nameof(denominator));
        if (numerator == 0L)
            return 0L;
        return checked(1L + ((numerator - 1L) / denominator));
    }
}

public readonly struct ProductionOutputClearanceCapacityGateAssessment
{
    internal ProductionOutputClearanceCapacityGateAssessment(
        ProductionOutputClearanceDisposition disposition,
        ProductionOutputClearanceRequirementAssessment requirement,
        int authoredWholeCycles,
        long authoredCapacityGrams,
        string failureCode,
        string diagnosticCode,
        string sourceDigest)
    {
        Disposition = disposition;
        Requirement = requirement;
        AuthoredWholeCycles = authoredWholeCycles;
        AuthoredCapacityGrams = authoredCapacityGrams;
        FailureCode = failureCode ?? string.Empty;
        DiagnosticCode = diagnosticCode ?? string.Empty;
        SourceDigest = sourceDigest ?? string.Empty;
    }

    public ProductionOutputClearanceDisposition Disposition { get; }
    public bool IsAccepted =>
        Disposition == ProductionOutputClearanceDisposition.Accepted;
    public bool CanPublishBoundedCapacity =>
        Disposition != ProductionOutputClearanceDisposition.Critical;
    public bool IsBlockingCritical =>
        Disposition == ProductionOutputClearanceDisposition.Critical;
    public bool RequiresBackpressure =>
        Disposition == ProductionOutputClearanceDisposition.BackpressureExpected;
    public ProductionOutputClearanceRequirementAssessment Requirement { get; }
    public int AuthoredWholeCycles { get; }
    public long AuthoredCapacityGrams { get; }
    public string FailureCode { get; }
    public string DiagnosticCode { get; }
    public string SourceDigest { get; }
}

/// <summary>
/// Reconciles measured clearance demand with the existing authored integer
/// output-buffer authority. It never mutates authored capacity. A demand above
/// four cycles can publish the bounded authored capacity with explicit
/// backpressure; authored capacity below the bounded target remains Critical.
/// </summary>
public static class ProductionOutputClearanceCapacityGate
{
    public const string Schema = "production-output-clearance-capacity-gate@2";
    public const string AuthoredCapacityUndersizedFailureCode =
        "PRODUCTION_OUTPUT_CLEARANCE_AUTHORED_CAPACITY_UNDERSIZED";

    public static ProductionOutputClearanceCapacityGateAssessment Assess(
        ProductionFacilityCapacitySubject facility,
        long maximumCycleCompletionFootprintGrams,
        ProductionOutputClearanceProfileSnapshot profile)
    {
        if (!facility.FacilityId.IsValid
            || string.IsNullOrEmpty(facility.DefinitionId)
            || string.IsNullOrEmpty(facility.WorkstationTag)
            || facility.OutputBufferCycleCapacity is < 2 or > 4)
        {
            throw new InvalidOperationException(
                "Production output-clearance facility subject is incomplete.");
        }

        ProductionOutputClearanceRequirementAssessment requirement =
            ProductionOutputClearanceRequirementProjector.Assess(
                maximumCycleCompletionFootprintGrams,
                profile);
        bool authoredUndersized = facility.OutputBufferCycleCapacity
            < requirement.PublishedWholeCycles;
        string failureCode = authoredUndersized
                ? AuthoredCapacityUndersizedFailureCode
                : string.Empty;
        ProductionOutputClearanceDisposition disposition = authoredUndersized
            ? ProductionOutputClearanceDisposition.Critical
            : requirement.Disposition;
        string diagnosticCode = authoredUndersized
            ? string.Empty
            : requirement.DiagnosticCode;
        long authoredCapacityGrams = checked(
            maximumCycleCompletionFootprintGrams
            * facility.OutputBufferCycleCapacity);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(facility.DefinitionId);
        digest.Append(facility.WorkstationTag);
        digest.Append(facility.OutputBufferCycleCapacity);
        digest.Append(maximumCycleCompletionFootprintGrams);
        digest.Append(profile.SourceDigest);
        digest.Append(requirement.SourceDigest);
        digest.Append(requirement.RequiredCycleMilliCycles);
        digest.Append(requirement.RequiredWholeCycles);
        digest.Append(authoredCapacityGrams);
        digest.AppendEnum(disposition);
        digest.Append(failureCode);
        digest.Append(diagnosticCode);

        return new ProductionOutputClearanceCapacityGateAssessment(
            disposition,
            requirement,
            facility.OutputBufferCycleCapacity,
            authoredCapacityGrams,
            failureCode,
            diagnosticCode,
            digest.ComputeSha256());
    }
}
