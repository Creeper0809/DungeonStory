using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DungeonStory.Balance
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class BalanceImmutableRecordAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class BalanceCaptureFactoryAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BalanceSerializationLayerAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BalancePresentationLayerAttribute : Attribute
    {
    }

    public enum BalanceLedgerExecutionMode
    {
        AuditOnly = 0,
        ApplyApproved = 1,
        VerifyApplied = 2,
        RegenerateArtifacts = 3
    }

    [BalanceImmutableRecord]
    public readonly struct EwuAmount : IEquatable<EwuAmount>, IComparable<EwuAmount>
    {
        public const long MilliEwuPerEwu = 1000L;

        private EwuAmount(long milliEwu)
        {
            if (milliEwu < 0L)
                throw new ArgumentOutOfRangeException(nameof(milliEwu));
            MilliEwu = milliEwu;
        }

        public long MilliEwu { get; }
        public static EwuAmount Zero => new EwuAmount(0L);

        public static EwuAmount FromMilliEwu(long milliEwu) =>
            new EwuAmount(milliEwu);

        public static EwuAmount operator +(EwuAmount left, EwuAmount right) =>
            new EwuAmount(checked(left.MilliEwu + right.MilliEwu));

        public static EwuAmount operator *(EwuAmount value, long quantity)
        {
            if (quantity < 0L)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            return new EwuAmount(checked(value.MilliEwu * quantity));
        }

        public int CompareTo(EwuAmount other) => MilliEwu.CompareTo(other.MilliEwu);
        public bool Equals(EwuAmount other) => MilliEwu == other.MilliEwu;
        public override bool Equals(object obj) => obj is EwuAmount other && Equals(other);
        public override int GetHashCode() => MilliEwu.GetHashCode();
        public static bool operator ==(EwuAmount left, EwuAmount right) => left.Equals(right);
        public static bool operator !=(EwuAmount left, EwuAmount right) => !left.Equals(right);
        public static bool operator <(EwuAmount left, EwuAmount right) =>
            left.MilliEwu < right.MilliEwu;
        public static bool operator >(EwuAmount left, EwuAmount right) =>
            left.MilliEwu > right.MilliEwu;

        public string ToCanonicalEwuToken() =>
            (MilliEwu / (decimal)MilliEwuPerEwu)
            .ToString("0.###", CultureInfo.InvariantCulture);

        public override string ToString() => ToCanonicalEwuToken();
    }

    public static class V27EwuQuantizer
    {
        public static EwuAmount QuantizeInputDebit(decimal ewu) =>
            EwuAmount.FromMilliEwu(Quantize(ewu, roundUp: true));

        public static EwuAmount QuantizeOutputCredit(decimal ewu) =>
            EwuAmount.FromMilliEwu(Quantize(ewu, roundUp: false));

        public static EwuAmount DivideInputCost(long totalMilliEwu, long outputUnits) =>
            EwuAmount.FromMilliEwu(Divide(totalMilliEwu, outputUnits, roundUp: true));

        public static EwuAmount DivideOutputValue(long totalMilliEwu, long outputUnits) =>
            EwuAmount.FromMilliEwu(Divide(totalMilliEwu, outputUnits, roundUp: false));

        public static EwuAmount DivideInputCost(
            long totalMilliEwu,
            EwuRational outputUnits) =>
            EwuAmount.FromMilliEwu(Divide(totalMilliEwu, outputUnits, roundUp: true));

        public static EwuAmount DivideOutputValue(
            long totalMilliEwu,
            EwuRational outputUnits) =>
            EwuAmount.FromMilliEwu(Divide(totalMilliEwu, outputUnits, roundUp: false));

        public static EwuAmount MultiplyInputDebit(
            EwuAmount unitCost,
            decimal quantity)
        {
            if (quantity < 0m)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            decimal ewu = unitCost.MilliEwu / (decimal)EwuAmount.MilliEwuPerEwu;
            return QuantizeInputDebit(checked(ewu * quantity));
        }

        public static EwuAmount MultiplyOutputCredit(
            EwuAmount unitValue,
            decimal quantity)
        {
            if (quantity < 0m)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            decimal ewu = unitValue.MilliEwu / (decimal)EwuAmount.MilliEwuPerEwu;
            return QuantizeOutputCredit(checked(ewu * quantity));
        }

        private static long Quantize(decimal ewu, bool roundUp)
        {
            if (ewu < 0m)
                throw new ArgumentOutOfRangeException(nameof(ewu));
            decimal scaled = checked(ewu * EwuAmount.MilliEwuPerEwu);
            decimal integral = roundUp
                ? decimal.Ceiling(scaled)
                : decimal.Floor(scaled);
            if (integral > long.MaxValue)
                throw new OverflowException("EWU amount exceeds Int64 mEWU authority.");
            return decimal.ToInt64(integral);
        }

        private static long Divide(long totalMilliEwu, long outputUnits, bool roundUp)
        {
            if (totalMilliEwu < 0L)
                throw new ArgumentOutOfRangeException(nameof(totalMilliEwu));
            if (outputUnits <= 0L)
                throw new ArgumentOutOfRangeException(nameof(outputUnits));
            long quotient = totalMilliEwu / outputUnits;
            long remainder = totalMilliEwu % outputUnits;
            return roundUp && remainder != 0L ? checked(quotient + 1L) : quotient;
        }

        private static long Divide(
            long totalMilliEwu,
            EwuRational outputUnits,
            bool roundUp)
        {
            if (totalMilliEwu < 0L)
                throw new ArgumentOutOfRangeException(nameof(totalMilliEwu));
            if (outputUnits.Numerator <= 0L)
                throw new ArgumentOutOfRangeException(nameof(outputUnits));
            long scaled = checked(totalMilliEwu * outputUnits.Denominator);
            return Divide(scaled, outputUnits.Numerator, roundUp);
        }
    }

    [BalanceImmutableRecord]
    public readonly struct EwuRational : IEquatable<EwuRational>
    {
        public EwuRational(long numerator, long denominator)
        {
            if (numerator < 0L)
                throw new ArgumentOutOfRangeException(nameof(numerator));
            if (denominator <= 0L)
                throw new ArgumentOutOfRangeException(nameof(denominator));
            long divisor = GreatestCommonDivisor(numerator, denominator);
            Numerator = numerator / divisor;
            Denominator = denominator / divisor;
        }

        public long Numerator { get; }
        public long Denominator { get; }
        public bool IsZero => Numerator == 0L;
        public static EwuRational Zero => new EwuRational(0L, 1L);
        public static EwuRational One => new EwuRational(1L, 1L);

        public static EwuRational FromDecimal(decimal value)
        {
            if (value < 0m)
                throw new ArgumentOutOfRangeException(nameof(value));
            int[] bits = decimal.GetBits(value);
            int scale = (bits[3] >> 16) & 0x7f;
            if ((bits[3] & unchecked((int)0x80000000)) != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (bits[2] != 0 || scale > 18)
                throw new OverflowException("Decimal rational exceeds the Int64 authority range.");
            ulong magnitude = ((ulong)(uint)bits[1] << 32) | (uint)bits[0];
            if (magnitude > long.MaxValue)
                throw new OverflowException("Decimal rational exceeds the Int64 authority range.");
            long denominator = 1L;
            for (int index = 0; index < scale; index++)
                denominator = checked(denominator * 10L);
            return new EwuRational((long)magnitude, denominator);
        }

        public static EwuRational operator +(EwuRational left, EwuRational right)
        {
            long divisor = GreatestCommonDivisor(left.Denominator, right.Denominator);
            long leftScale = right.Denominator / divisor;
            long rightScale = left.Denominator / divisor;
            return new EwuRational(
                checked(left.Numerator * leftScale + right.Numerator * rightScale),
                checked(left.Denominator * leftScale));
        }

        public static EwuRational operator *(EwuRational value, long quantity)
        {
            if (quantity < 0L)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (quantity == 0L || value.IsZero)
                return Zero;
            long divisor = GreatestCommonDivisor(quantity, value.Denominator);
            return new EwuRational(
                checked(value.Numerator * (quantity / divisor)),
                value.Denominator / divisor);
        }

        public decimal ToDecimal() => Numerator / (decimal)Denominator;
        public string ToCanonicalToken() =>
            BalanceCanonicalText.InvariantDecimal(ToDecimal());
        public bool Equals(EwuRational other) =>
            Numerator == other.Numerator && Denominator == other.Denominator;
        public override bool Equals(object obj) => obj is EwuRational other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);
        public static bool operator ==(EwuRational left, EwuRational right) => left.Equals(right);
        public static bool operator !=(EwuRational left, EwuRational right) => !left.Equals(right);

        private static long GreatestCommonDivisor(long left, long right)
        {
            while (right != 0L)
            {
                long remainder = left % right;
                left = right;
                right = remainder;
            }
            return left == 0L ? 1L : left;
        }
    }

    public sealed class CanonicalStringPool
    {
        private readonly Dictionary<string, string> strings =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public string GetOrAdd(string canonical)
        {
            canonical ??= string.Empty;
            if (strings.TryGetValue(canonical, out string existing))
                return existing;
            strings.Add(canonical, canonical);
            return canonical;
        }

        public int Count => strings.Count;
    }

    public static class BalanceCanonicalText
    {
        public static string StableId(string authorityValue, string authorityName)
        {
            if (authorityValue == null)
                throw new InvalidOperationException($"{authorityName} is null.");
            if (authorityValue.Length == 0)
                throw new InvalidOperationException($"{authorityName} is empty.");
            if (!string.Equals(authorityValue, authorityValue.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"NON_CANONICAL_AUTHORITY_ID {authorityName}: surrounding whitespace.");
            string normalized = authorityValue.Normalize(NormalizationForm.FormC);
            if (!string.Equals(authorityValue, normalized, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"NON_CANONICAL_AUTHORITY_ID {authorityName}: not Unicode Form C.");
            for (int index = 0; index < authorityValue.Length; index++)
            {
                char value = authorityValue[index];
                bool allowed = value >= 'a' && value <= 'z'
                    || value >= 'A' && value <= 'Z'
                    || value >= '0' && value <= '9'
                    || value == ':' || value == '/' || value == '.'
                    || value == '_' || value == '-' || value == '|';
                if (!allowed)
                {
                    throw new InvalidOperationException(
                        $"NON_CANONICAL_AUTHORITY_ID {authorityName}: invalid character U+{(int)value:X4}.");
                }
            }
            return authorityValue;
        }

        public static string Display(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return NormalizeLines(value.Normalize(NormalizationForm.FormC)).Trim();
        }

        public static string Detail(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return NormalizeLines(value.Normalize(NormalizationForm.FormC)).Trim();
        }

        public static string ProjectRelativePath(string value)
        {
            string normalized = Display(value).Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.Contains("://", StringComparison.Ordinal)
                || normalized.Length >= 2 && normalized[1] == ':')
            {
                throw new InvalidOperationException(
                    $"Source authority path must be project-relative: {normalized}");
            }
            return normalized;
        }

        public static string InvariantDecimal(decimal value) =>
            value.ToString("0.#############################", CultureInfo.InvariantCulture);

        public static decimal DecimalFromFiniteFloat(float value, string authorityName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException($"{authorityName} is not finite.");
            return decimal.Parse(value.ToString("R", CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string NormalizeLines(string value)
        {
            StringBuilder builder = null;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character != '\r' && character != '\n')
                {
                    builder?.Append(character);
                    continue;
                }

                if (builder == null)
                {
                    builder = new StringBuilder(value.Length);
                    builder.Append(value, 0, index);
                }
                builder.Append(' ');
                if (character == '\r' && index + 1 < value.Length && value[index + 1] == '\n')
                    index++;
            }
            return builder?.ToString() ?? value;
        }
    }

    public sealed class BalanceMetricCaptureRequest
    {
        public string SchemaVersion { get; set; } = "v27.1";
        public string Domain { get; set; }
        public string DefinitionKind { get; set; }
        public string StableId { get; set; }
        public string Metric { get; set; }
        public string Unit { get; set; }
        public string Before { get; set; }
        public string After { get; set; }
        public string AuthoredRoundedValue { get; set; }
        public string PercentDelta { get; set; }
        public string ExactFormula { get; set; }
        public string BeforeBom { get; set; }
        public string AfterBom { get; set; }
        public string BeforeDirectWu { get; set; }
        public string AfterDirectWu { get; set; }
        public string BeforeBomEwu { get; set; }
        public string AfterBomEwu { get; set; }
        public string BeforeLaborDensity { get; set; }
        public string AfterLaborDensity { get; set; }
        public string UpstreamOnlyAfter { get; set; }
        public string InheritedDelta { get; set; }
        public string RawLocalDelta { get; set; }
        public int LocalQuantizationBoundaryCount { get; set; }
        public string DownstreamConsumerCount { get; set; }
        public IEnumerable<string> DependencyIds { get; set; }
        public IEnumerable<string> RootCauseIds { get; set; }
        public string AnomalyDisposition { get; set; }
        public string ReasonCode { get; set; }
        public string ReasonDetail { get; set; }
        public string SourceAuthority { get; set; }
        public string SourcePropertyPath { get; set; }
        public string ExecutionRoute { get; set; }
        public string SaveAuthority { get; set; }
        public string VerificationEvidence { get; set; }
        public string ReviewStatus { get; set; }
        public string ApprovalKey { get; set; }
        public string DependencyFingerprint { get; set; }
        public string LocalFingerprint { get; set; }
        public string SourceDigest { get; set; }
        public string SemanticHash { get; set; }
        public string AssetApplied { get; set; }
        public string BalanceBaselineRecordId { get; set; }
    }

    [BalanceImmutableRecord]
    public sealed class CanonicalBalanceMetricRecord
    {
        internal CanonicalBalanceMetricRecord(BalanceMetricRowData data, BalanceSortRank rank)
        {
            SchemaVersion = data.SchemaVersion;
            Domain = data.Domain;
            DefinitionKind = data.DefinitionKind;
            StableId = data.StableId;
            Metric = data.Metric;
            Unit = data.Unit;
            Before = data.Before;
            After = data.After;
            AuthoredRoundedValue = data.AuthoredRoundedValue;
            PercentDelta = data.PercentDelta;
            ExactFormula = data.ExactFormula;
            BeforeBom = data.BeforeBom;
            AfterBom = data.AfterBom;
            BeforeDirectWu = data.BeforeDirectWu;
            AfterDirectWu = data.AfterDirectWu;
            BeforeBomEwu = data.BeforeBomEwu;
            AfterBomEwu = data.AfterBomEwu;
            BeforeLaborDensity = data.BeforeLaborDensity;
            AfterLaborDensity = data.AfterLaborDensity;
            UpstreamOnlyAfter = data.UpstreamOnlyAfter;
            InheritedDelta = data.InheritedDelta;
            RawLocalDelta = data.RawLocalDelta;
            RoundingEnvelope = data.RoundingEnvelope;
            DownstreamConsumerCount = data.DownstreamConsumerCount;
            DependencyIds = data.DependencyIds;
            RootCauseIds = data.RootCauseIds;
            AnomalyDisposition = data.AnomalyDisposition;
            ReasonCode = data.ReasonCode;
            ReasonDetail = data.ReasonDetail;
            SourceAuthority = data.SourceAuthority;
            SourcePropertyPath = data.SourcePropertyPath;
            ExecutionRoute = data.ExecutionRoute;
            SaveAuthority = data.SaveAuthority;
            VerificationEvidence = data.VerificationEvidence;
            ReviewStatus = data.ReviewStatus;
            ApprovalKey = data.ApprovalKey;
            DependencyFingerprint = data.DependencyFingerprint;
            LocalFingerprint = data.LocalFingerprint;
            SourceDigest = data.SourceDigest;
            SemanticHash = data.SemanticHash;
            AssetApplied = data.AssetApplied;
            BalanceBaselineRecordId = data.BalanceBaselineRecordId;
            SortRank = rank;
        }

        public string SchemaVersion { get; }
        public string Domain { get; }
        public string DefinitionKind { get; }
        public string StableId { get; }
        public string Metric { get; }
        public string Unit { get; }
        public string Before { get; }
        public string After { get; }
        public string AuthoredRoundedValue { get; }
        public string PercentDelta { get; }
        public string ExactFormula { get; }
        public string BeforeBom { get; }
        public string AfterBom { get; }
        public string BeforeDirectWu { get; }
        public string AfterDirectWu { get; }
        public string BeforeBomEwu { get; }
        public string AfterBomEwu { get; }
        public string BeforeLaborDensity { get; }
        public string AfterLaborDensity { get; }
        public string UpstreamOnlyAfter { get; }
        public string InheritedDelta { get; }
        public string RawLocalDelta { get; }
        public string RoundingEnvelope { get; }
        public string DownstreamConsumerCount { get; }
        public IReadOnlyList<string> DependencyIds { get; }
        public IReadOnlyList<string> RootCauseIds { get; }
        public string AnomalyDisposition { get; }
        public string ReasonCode { get; }
        public string ReasonDetail { get; }
        public string SourceAuthority { get; }
        public string SourcePropertyPath { get; }
        public string ExecutionRoute { get; }
        public string SaveAuthority { get; }
        public string VerificationEvidence { get; }
        public string ReviewStatus { get; }
        public string ApprovalKey { get; }
        public string DependencyFingerprint { get; }
        public string LocalFingerprint { get; }
        public string SourceDigest { get; }
        public string SemanticHash { get; }
        public string AssetApplied { get; }
        public string BalanceBaselineRecordId { get; }
        internal BalanceSortRank SortRank { get; }
    }

    internal readonly struct BalanceSortRank
    {
        public BalanceSortRank(int domain, int definitionKind, int stableId, int metric)
        {
            Domain = domain;
            DefinitionKind = definitionKind;
            StableId = stableId;
            Metric = metric;
        }

        public int Domain { get; }
        public int DefinitionKind { get; }
        public int StableId { get; }
        public int Metric { get; }
    }

    internal sealed class BalanceMetricRowData
    {
        public string SchemaVersion;
        public string Domain;
        public string DefinitionKind;
        public string StableId;
        public string Metric;
        public string Unit;
        public string Before;
        public string After;
        public string AuthoredRoundedValue;
        public string PercentDelta;
        public string ExactFormula;
        public string BeforeBom;
        public string AfterBom;
        public string BeforeDirectWu;
        public string AfterDirectWu;
        public string BeforeBomEwu;
        public string AfterBomEwu;
        public string BeforeLaborDensity;
        public string AfterLaborDensity;
        public string UpstreamOnlyAfter;
        public string InheritedDelta;
        public string RawLocalDelta;
        public string RoundingEnvelope;
        public string DownstreamConsumerCount;
        public IReadOnlyList<string> DependencyIds;
        public IReadOnlyList<string> RootCauseIds;
        public string AnomalyDisposition;
        public string ReasonCode;
        public string ReasonDetail;
        public string SourceAuthority;
        public string SourcePropertyPath;
        public string ExecutionRoute;
        public string SaveAuthority;
        public string VerificationEvidence;
        public string ReviewStatus;
        public string ApprovalKey;
        public string DependencyFingerprint;
        public string LocalFingerprint;
        public string SourceDigest;
        public string SemanticHash;
        public string AssetApplied;
        public string BalanceBaselineRecordId;
    }

    [BalanceImmutableRecord]
    public sealed class FrozenBalanceLedger
    {
        internal FrozenBalanceLedger(CanonicalBalanceMetricRecord[] records)
        {
            CanonicalBalanceMetricRecord[] copy = records == null
                ? Array.Empty<CanonicalBalanceMetricRecord>()
                : (CanonicalBalanceMetricRecord[])records.Clone();
            Records = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<CanonicalBalanceMetricRecord> Records { get; }
        public int Count => Records.Count;
    }

    [BalanceImmutableRecord]
    public sealed class BalanceAuthoritySnapshot
    {
        private BalanceAuthoritySnapshot(
            FrozenBalanceLedger ledger,
            string sourceDigest,
            int sourceCount)
        {
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            SourceDigest = sourceDigest ?? throw new ArgumentNullException(nameof(sourceDigest));
            if (SourceDigest.Length != 64)
                throw new ArgumentException("Source digest must be SHA-256 hex.", nameof(sourceDigest));
            if (sourceCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceCount));
            SourceCount = sourceCount;
        }

        public FrozenBalanceLedger Ledger { get; }
        public string SourceDigest { get; }
        public int SourceCount { get; }

        [BalanceCaptureFactory]
        public static BalanceAuthoritySnapshot Capture(
            FrozenBalanceLedger ledger,
            string sourceDigest,
            int sourceCount) => new BalanceAuthoritySnapshot(
                ledger,
                sourceDigest,
                sourceCount);
    }

    [BalanceImmutableRecord]
    public sealed class BalanceArtifactManifest
    {
        private BalanceArtifactManifest(
            string schemaVersion,
            string generatorVersion,
            BalanceAuthoritySnapshot authority,
            int criticalCount,
            int collapsedCriticalCount,
            int approvedCount,
            int sccCount,
            int integrityFailureCount,
            IEnumerable<string> balanceBaselineRecordIds)
        {
            SchemaVersion = schemaVersion ?? throw new ArgumentNullException(nameof(schemaVersion));
            GeneratorVersion = generatorVersion
                ?? throw new ArgumentNullException(nameof(generatorVersion));
            Authority = authority ?? throw new ArgumentNullException(nameof(authority));
            if (criticalCount < 0 || collapsedCriticalCount < 0 || approvedCount < 0
                || sccCount < 0 || integrityFailureCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticalCount),
                    "Manifest counts cannot be negative.");
            }
            CriticalCount = criticalCount;
            CollapsedCriticalCount = collapsedCriticalCount;
            ApprovedCount = approvedCount;
            SccCount = sccCount;
            IntegrityFailureCount = integrityFailureCount;
            string[] baselineIds = (balanceBaselineRecordIds
                    ?? throw new ArgumentNullException(nameof(balanceBaselineRecordIds)))
                .Select(value => value
                    ?? throw new ArgumentException("Baseline record id cannot be null."))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (baselineIds.Length == 0)
                throw new ArgumentException("At least one baseline record id is required.");
            BalanceBaselineRecordIds = Array.AsReadOnly(baselineIds);
        }

        public string SchemaVersion { get; }
        public string GeneratorVersion { get; }
        public BalanceAuthoritySnapshot Authority { get; }
        public int CriticalCount { get; }
        public int CollapsedCriticalCount { get; }
        public int ApprovedCount { get; }
        public int SccCount { get; }
        public int IntegrityFailureCount { get; }
        public IReadOnlyList<string> BalanceBaselineRecordIds { get; }

        [BalanceCaptureFactory]
        public static BalanceArtifactManifest Capture(
            string schemaVersion,
            string generatorVersion,
            BalanceAuthoritySnapshot authority,
            int criticalCount,
            int collapsedCriticalCount,
            int approvedCount,
            int sccCount,
            int integrityFailureCount,
            IEnumerable<string> balanceBaselineRecordIds) => new BalanceArtifactManifest(
                schemaVersion,
                generatorVersion,
                authority,
                criticalCount,
                collapsedCriticalCount,
                approvedCount,
                sccCount,
                integrityFailureCount,
                balanceBaselineRecordIds);
    }

    [BalanceCaptureFactory]
    public static class BalanceLedgerReviewFactory
    {
        public static FrozenBalanceLedger ApplyApprovedKeys(
            FrozenBalanceLedger ledger,
            IReadOnlyCollection<string> approvedKeys)
        {
            if (ledger == null)
                throw new ArgumentNullException(nameof(ledger));
            if (approvedKeys == null || approvedKeys.Count == 0)
                return ledger;

            HashSet<string> approved = new HashSet<string>(approvedKeys, StringComparer.Ordinal);
            CanonicalBalanceMetricRecord[] records = new CanonicalBalanceMetricRecord[ledger.Count];
            for (int index = 0; index < records.Length; index++)
            {
                CanonicalBalanceMetricRecord source = ledger.Records[index];
                if (source.ApprovalKey.Length == 0 || !approved.Contains(source.ApprovalKey))
                {
                    records[index] = source;
                    continue;
                }
                records[index] = new CanonicalBalanceMetricRecord(
                    CopyWithReviewState(source, "approved", "approved"),
                    source.SortRank);
            }
            return new FrozenBalanceLedger(records);
        }

        private static BalanceMetricRowData CopyWithReviewState(
            CanonicalBalanceMetricRecord source,
            string disposition,
            string reviewStatus) => new BalanceMetricRowData
        {
            SchemaVersion = source.SchemaVersion,
            Domain = source.Domain,
            DefinitionKind = source.DefinitionKind,
            StableId = source.StableId,
            Metric = source.Metric,
            Unit = source.Unit,
            Before = source.Before,
            After = source.After,
            AuthoredRoundedValue = source.AuthoredRoundedValue,
            PercentDelta = source.PercentDelta,
            ExactFormula = source.ExactFormula,
            BeforeBom = source.BeforeBom,
            AfterBom = source.AfterBom,
            BeforeDirectWu = source.BeforeDirectWu,
            AfterDirectWu = source.AfterDirectWu,
            BeforeBomEwu = source.BeforeBomEwu,
            AfterBomEwu = source.AfterBomEwu,
            BeforeLaborDensity = source.BeforeLaborDensity,
            AfterLaborDensity = source.AfterLaborDensity,
            UpstreamOnlyAfter = source.UpstreamOnlyAfter,
            InheritedDelta = source.InheritedDelta,
            RawLocalDelta = source.RawLocalDelta,
            RoundingEnvelope = source.RoundingEnvelope,
            DownstreamConsumerCount = source.DownstreamConsumerCount,
            DependencyIds = source.DependencyIds,
            RootCauseIds = source.RootCauseIds,
            AnomalyDisposition = disposition,
            ReasonCode = source.ReasonCode,
            ReasonDetail = source.ReasonDetail,
            SourceAuthority = source.SourceAuthority,
            SourcePropertyPath = source.SourcePropertyPath,
            ExecutionRoute = source.ExecutionRoute,
            SaveAuthority = source.SaveAuthority,
            VerificationEvidence = source.VerificationEvidence,
            ReviewStatus = reviewStatus,
            ApprovalKey = source.ApprovalKey,
            DependencyFingerprint = source.DependencyFingerprint,
            LocalFingerprint = source.LocalFingerprint,
            SourceDigest = source.SourceDigest,
            SemanticHash = source.SemanticHash,
            AssetApplied = source.AssetApplied,
            BalanceBaselineRecordId = source.BalanceBaselineRecordId
        };
    }

    [BalanceCaptureFactory]
    public sealed class BalanceCaptureFactory
    {
        private readonly CanonicalStringPool pool = new CanonicalStringPool();
        private readonly List<BalanceMetricRowData> rows = new List<BalanceMetricRowData>();
        private readonly HashSet<string> uniqueKeys = new HashSet<string>(StringComparer.Ordinal);

        public int Count => rows.Count;

        public void Capture(BalanceMetricCaptureRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string domain = PoolStable(request.Domain, nameof(request.Domain));
            string definitionKind = PoolStable(
                request.DefinitionKind,
                nameof(request.DefinitionKind));
            string stableId = PoolStable(request.StableId, nameof(request.StableId));
            string metric = PoolStable(request.Metric, nameof(request.Metric));
            string key = domain + "\u001f" + definitionKind + "\u001f" + stableId + "\u001f" + metric;
            if (!uniqueKeys.Add(key))
                throw new InvalidOperationException($"Duplicate V27 balance key: {domain}/{definitionKind}/{stableId}/{metric}");

            int boundaryCount = request.LocalQuantizationBoundaryCount;
            if (boundaryCount < 0)
                throw new InvalidOperationException("Local quantization boundary count cannot be negative.");
            int envelope = Math.Min(2, boundaryCount);

            rows.Add(new BalanceMetricRowData
            {
                SchemaVersion = PoolStable(request.SchemaVersion, nameof(request.SchemaVersion)),
                Domain = domain,
                DefinitionKind = definitionKind,
                StableId = stableId,
                Metric = metric,
                Unit = PoolDisplay(request.Unit),
                Before = PoolDisplay(request.Before),
                After = PoolDisplay(request.After),
                AuthoredRoundedValue = PoolDisplay(request.AuthoredRoundedValue),
                PercentDelta = PoolDisplay(request.PercentDelta),
                ExactFormula = PoolDetail(request.ExactFormula),
                BeforeBom = PoolDetail(request.BeforeBom),
                AfterBom = PoolDetail(request.AfterBom),
                BeforeDirectWu = PoolDisplay(request.BeforeDirectWu),
                AfterDirectWu = PoolDisplay(request.AfterDirectWu),
                BeforeBomEwu = PoolDisplay(request.BeforeBomEwu),
                AfterBomEwu = PoolDisplay(request.AfterBomEwu),
                BeforeLaborDensity = PoolDisplay(request.BeforeLaborDensity),
                AfterLaborDensity = PoolDisplay(request.AfterLaborDensity),
                UpstreamOnlyAfter = PoolDisplay(request.UpstreamOnlyAfter),
                InheritedDelta = PoolDisplay(request.InheritedDelta),
                RawLocalDelta = PoolDisplay(request.RawLocalDelta),
                RoundingEnvelope = pool.GetOrAdd(envelope.ToString(CultureInfo.InvariantCulture)),
                DownstreamConsumerCount = PoolDisplay(request.DownstreamConsumerCount),
                DependencyIds = CaptureIds(request.DependencyIds, "dependency"),
                RootCauseIds = CaptureIds(request.RootCauseIds, "root cause"),
                AnomalyDisposition = PoolDisplay(request.AnomalyDisposition),
                ReasonCode = PoolDisplay(request.ReasonCode),
                ReasonDetail = PoolDetail(request.ReasonDetail),
                SourceAuthority = PoolPath(request.SourceAuthority),
                SourcePropertyPath = PoolDetail(request.SourcePropertyPath),
                ExecutionRoute = PoolDetail(request.ExecutionRoute),
                SaveAuthority = PoolDetail(request.SaveAuthority),
                VerificationEvidence = PoolDetail(request.VerificationEvidence),
                ReviewStatus = PoolDisplay(request.ReviewStatus),
                ApprovalKey = PoolDisplay(request.ApprovalKey),
                DependencyFingerprint = PoolDisplay(request.DependencyFingerprint),
                LocalFingerprint = PoolDisplay(request.LocalFingerprint),
                SourceDigest = PoolDisplay(request.SourceDigest),
                SemanticHash = PoolDisplay(request.SemanticHash),
                AssetApplied = PoolDisplay(request.AssetApplied),
                BalanceBaselineRecordId = PoolStable(
                    request.BalanceBaselineRecordId,
                    nameof(request.BalanceBaselineRecordId))
            });
        }

        public FrozenBalanceLedger Freeze()
        {
            Dictionary<string, int> domainRanks = BuildRanks(rows.Select(row => row.Domain));
            Dictionary<string, int> kindRanks = BuildRanks(rows.Select(row => row.DefinitionKind));
            Dictionary<string, int> idRanks = BuildRanks(rows.Select(row => row.StableId));
            Dictionary<string, int> metricRanks = BuildRanks(rows.Select(row => row.Metric));
            CanonicalBalanceMetricRecord[] records = new CanonicalBalanceMetricRecord[rows.Count];
            for (int index = 0; index < rows.Count; index++)
            {
                BalanceMetricRowData row = rows[index];
                records[index] = new CanonicalBalanceMetricRecord(
                    row,
                    new BalanceSortRank(
                        domainRanks[row.Domain],
                        kindRanks[row.DefinitionKind],
                        idRanks[row.StableId],
                        metricRanks[row.Metric]));
            }
            StableRankSorter.Sort(records);
            return new FrozenBalanceLedger(records);
        }

        private static Dictionary<string, int> BuildRanks(IEnumerable<string> values)
        {
            string[] vocabulary = values
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Dictionary<string, int> ranks = new Dictionary<string, int>(
                vocabulary.Length,
                StringComparer.Ordinal);
            for (int index = 0; index < vocabulary.Length; index++)
                ranks.Add(vocabulary[index], index);
            return ranks;
        }

        private string PoolStable(string value, string authorityName) =>
            pool.GetOrAdd(BalanceCanonicalText.StableId(value, authorityName));

        private string PoolDisplay(string value) =>
            pool.GetOrAdd(BalanceCanonicalText.Display(value));

        private string PoolDetail(string value) =>
            pool.GetOrAdd(BalanceCanonicalText.Detail(value));

        private string PoolPath(string value) =>
            pool.GetOrAdd(BalanceCanonicalText.ProjectRelativePath(value));

        private IReadOnlyList<string> CaptureIds(IEnumerable<string> values, string authorityName)
        {
            string[] canonical = (values ?? Array.Empty<string>())
                .Select(value => PoolStable(value, authorityName))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return Array.AsReadOnly(canonical);
        }
    }

    public static class StableRankSorter
    {
        [ThreadStatic] private static CanonicalBalanceMetricRecord[] reusableBuffer;
        [ThreadStatic] private static int[] reusableCounts;
        [ThreadStatic] private static CanonicalBalanceMetricRecord[] rankedInput;
        [ThreadStatic] private static int maximumDomain;
        [ThreadStatic] private static int maximumDefinitionKind;
        [ThreadStatic] private static int maximumStableId;
        [ThreadStatic] private static int maximumMetric;

        public static void Sort(CanonicalBalanceMetricRecord[] records)
        {
            if (records == null || records.Length < 2)
                return;
            EnsureWorkspace(records);
            CountingPassMetric(records, reusableBuffer);
            CountingPassStableId(reusableBuffer, records);
            CountingPassDefinitionKind(records, reusableBuffer);
            CountingPassDomain(reusableBuffer, records);
        }

        private static void CountingPassMetric(
            CanonicalBalanceMetricRecord[] source,
            CanonicalBalanceMetricRecord[] destination)
        {
            int[] counts = reusableCounts;
            Array.Clear(counts, 0, maximumMetric + 1);
            for (int index = 0; index < source.Length; index++)
                counts[source[index].SortRank.Metric]++;
            int sum = 0;
            for (int index = 0; index <= maximumMetric; index++)
            {
                int count = counts[index];
                counts[index] = sum;
                sum += count;
            }
            for (int index = 0; index < source.Length; index++)
            {
                CanonicalBalanceMetricRecord record = source[index];
                destination[counts[record.SortRank.Metric]++] = record;
            }
        }

        private static void CountingPassStableId(
            CanonicalBalanceMetricRecord[] source,
            CanonicalBalanceMetricRecord[] destination)
        {
            int[] counts = reusableCounts;
            Array.Clear(counts, 0, maximumStableId + 1);
            for (int index = 0; index < source.Length; index++)
                counts[source[index].SortRank.StableId]++;
            int sum = 0;
            for (int index = 0; index <= maximumStableId; index++)
            {
                int count = counts[index];
                counts[index] = sum;
                sum += count;
            }
            for (int index = 0; index < source.Length; index++)
            {
                CanonicalBalanceMetricRecord record = source[index];
                destination[counts[record.SortRank.StableId]++] = record;
            }
        }

        private static void CountingPassDefinitionKind(
            CanonicalBalanceMetricRecord[] source,
            CanonicalBalanceMetricRecord[] destination)
        {
            int[] counts = reusableCounts;
            Array.Clear(counts, 0, maximumDefinitionKind + 1);
            for (int index = 0; index < source.Length; index++)
                counts[source[index].SortRank.DefinitionKind]++;
            int sum = 0;
            for (int index = 0; index <= maximumDefinitionKind; index++)
            {
                int count = counts[index];
                counts[index] = sum;
                sum += count;
            }
            for (int index = 0; index < source.Length; index++)
            {
                CanonicalBalanceMetricRecord record = source[index];
                destination[counts[record.SortRank.DefinitionKind]++] = record;
            }
        }

        private static void CountingPassDomain(
            CanonicalBalanceMetricRecord[] source,
            CanonicalBalanceMetricRecord[] destination)
        {
            int[] counts = reusableCounts;
            Array.Clear(counts, 0, maximumDomain + 1);
            for (int index = 0; index < source.Length; index++)
                counts[source[index].SortRank.Domain]++;
            int sum = 0;
            for (int index = 0; index <= maximumDomain; index++)
            {
                int count = counts[index];
                counts[index] = sum;
                sum += count;
            }
            for (int index = 0; index < source.Length; index++)
            {
                CanonicalBalanceMetricRecord record = source[index];
                destination[counts[record.SortRank.Domain]++] = record;
            }
        }

        private static void EnsureWorkspace(CanonicalBalanceMetricRecord[] records)
        {
            if (reusableBuffer == null || reusableBuffer.Length != records.Length)
                reusableBuffer = new CanonicalBalanceMetricRecord[records.Length];
            if (!ReferenceEquals(rankedInput, records))
            {
                rankedInput = records;
                maximumDomain = 0;
                maximumDefinitionKind = 0;
                maximumStableId = 0;
                maximumMetric = 0;
                for (int index = 0; index < records.Length; index++)
                {
                    BalanceSortRank rank = records[index].SortRank;
                    maximumDomain = Math.Max(maximumDomain, rank.Domain);
                    maximumDefinitionKind = Math.Max(maximumDefinitionKind, rank.DefinitionKind);
                    maximumStableId = Math.Max(maximumStableId, rank.StableId);
                    maximumMetric = Math.Max(maximumMetric, rank.Metric);
                }
            }
            int maximum = Math.Max(
                Math.Max(maximumDomain, maximumDefinitionKind),
                Math.Max(maximumStableId, maximumMetric));
            if (reusableCounts == null || reusableCounts.Length <= maximum)
                reusableCounts = new int[maximum + 1];
        }
    }
}
