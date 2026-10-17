using System;
using System.Globalization;
using DungeonStory.Foundation.Facts;

namespace DungeonStory.Environment
{
    public enum EnvironmentalFireDamagePhase
    {
        Prepared = 1,
        AwaitingWorldRemoval = 2,
        Delivered = 3,
        Applied = 4
    }

    [Serializable]
    public sealed class EnvironmentalFireDamageRecord
    {
        public string operationId = string.Empty, requestFingerprint = string.Empty, fireId = string.Empty;
        public int targetKind;
        public string targetId = string.Empty, targetDisplayName = string.Empty;
        public int positionX, positionY;
        public float intensity, requestedDamage, appliedDamage;
        public bool targetRemainsCombustible;
        public int absoluteDay;
        public long ownerRevision;
        public EnvironmentalFireDamagePhase phase;
        public string publicationKey = string.Empty, publicationId = string.Empty, publicationFingerprint = string.Empty;
        public EnvironmentalFireDamageRecord Clone() => (EnvironmentalFireDamageRecord)MemberwiseClone();
    }

    /// <summary>Frozen proposal or applied damage; publication references are never interpreted by this model.</summary>
    public sealed class EnvironmentalFireDamageFact
    {
        private readonly EnvironmentalFireDamageRecord data;
        public EnvironmentalFireDamageFact(EnvironmentalFireDamageRecord data)
        {
            ValidateRecord(data, requireBoundReference: false);
            this.data = data.Clone();
            this.data.publicationKey = this.data.publicationId = this.data.publicationFingerprint = string.Empty;
            if (this.data.phase == EnvironmentalFireDamagePhase.Delivered) this.data.phase = EnvironmentalFireDamagePhase.Applied;
        }
        public EnvironmentalFireDamageRecord Snapshot() => data.Clone();

        public static void ValidateRecord(EnvironmentalFireDamageRecord value, bool requireBoundReference = true)
        {
            if (value == null || !DomainFactValueSyntax.IsIdentity(value.operationId)
                || !DomainFactValueSyntax.IsIdentity(value.fireId) || !DomainFactValueSyntax.IsIdentity(value.targetId)
                || !DomainFactValueSyntax.IsText(value.targetDisplayName, 256, false) || value.targetDisplayName.Trim() != value.targetDisplayName
                || (EnvironmentalFireTargetKind)value.targetKind is not (EnvironmentalFireTargetKind.Building or EnvironmentalFireTargetKind.Character)
                || !Finite(value.intensity) || value.intensity <= 0 || value.intensity > 1
                || !Finite(value.requestedDamage) || value.requestedDamage <= 0
                || !Finite(value.appliedDamage) || value.appliedDamage < 0 || value.appliedDamage > value.requestedDamage * 2f + 0.001f
                || value.absoluteDay < 0 || value.ownerRevision != EnvironmentalFireDamageFactIdentity.OwnerRevision(value.operationId)
                || value.requestFingerprint != EnvironmentalFireDamageFactIdentity.RequestFingerprint(value)
                || !Enum.IsDefined(typeof(EnvironmentalFireDamagePhase), value.phase)
                || (value.phase == EnvironmentalFireDamagePhase.Prepared ? value.appliedDamage != 0 : value.appliedDamage <= 0)
                || value.phase == EnvironmentalFireDamagePhase.AwaitingWorldRemoval
                    && ((EnvironmentalFireTargetKind)value.targetKind != EnvironmentalFireTargetKind.Building || value.targetRemainsCombustible)
                || requireBoundReference && !Opaque(value.publicationKey)
                || value.publicationKey == null || value.publicationId == null || value.publicationFingerprint == null
                || requireBoundReference && (value.phase == EnvironmentalFireDamagePhase.Delivered
                    ? !Opaque(value.publicationId) || !Opaque(value.publicationFingerprint)
                    : value.publicationId.Length != 0 || value.publicationFingerprint.Length != 0))
                throw new InvalidOperationException("Environmental fire damage state or publication reference is invalid.");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Opaque(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim() == value;
    }

    public static class EnvironmentalFireDamageFactIdentity
    {
        public static string RequestFingerprint(EnvironmentalFireDamageRecord value)
        {
            var digest = new CanonicalSemanticDigestBuilder();
            digest.Append("environmental-fire-damage-request@1");
            digest.Append(value.operationId); digest.Append(value.fireId); digest.Append(value.targetKind); digest.Append(value.targetId);
            digest.Append(value.positionX); digest.Append(value.positionY);
            digest.Append(value.intensity.ToString("R", CultureInfo.InvariantCulture));
            digest.Append(value.requestedDamage.ToString("R", CultureInfo.InvariantCulture));
            digest.Append(value.targetDisplayName?.Trim() ?? string.Empty); digest.Append(value.absoluteDay);
            return digest.ComputeSha256();
        }
        public static long OwnerRevision(string operationId)
        {
            var digest = new CanonicalSemanticDigestBuilder();
            digest.Append("environmental-fire-damage-owner-revision@1"); digest.Append(operationId?.Trim() ?? string.Empty);
            return Math.Max(1L, long.Parse(digest.ComputeSha256().Substring(0, 15), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
    }
}
