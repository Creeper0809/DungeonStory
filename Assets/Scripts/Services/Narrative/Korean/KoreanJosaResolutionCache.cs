using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonStory.Narrative.Korean
{
    /// <summary>
    /// Cache key contract for a rendered name token. Revisions make normal
    /// invalidation cheap; the display/pronunciation fingerprint prevents two
    /// independently-owned identities with coincident revision strings from
    /// sharing an incorrect particle.
    /// </summary>
    public struct KoreanJosaCacheKey : IEquatable<KoreanJosaCacheKey>
    {
        public KoreanJosaCacheKey(
            string displaySnapshotRevision,
            string pronunciationRevision,
            string locale,
            string formatterVersion,
            KoreanJosaKind particle)
            : this(
                string.Empty,
                displaySnapshotRevision,
                KoreanPronunciationMode.Unknown,
                string.Empty,
                KoreanFinalConsonantKind.Unknown,
                pronunciationRevision,
                locale,
                formatterVersion,
                particle)
        {
        }

        public KoreanJosaCacheKey(
            string displayTextFingerprint,
            string displaySnapshotRevision,
            KoreanPronunciationMode pronunciationMode,
            string pronunciationValueFingerprint,
            KoreanFinalConsonantKind explicitFinalConsonant,
            string pronunciationRevision,
            string locale,
            string formatterVersion,
            KoreanJosaKind particle)
        {
            DisplayTextFingerprint = NormalizeFingerprint(displayTextFingerprint);
            DisplaySnapshotRevision = displaySnapshotRevision ?? string.Empty;
            PronunciationMode = pronunciationMode;
            PronunciationValueFingerprint = NormalizeFingerprint(pronunciationValueFingerprint);
            ExplicitFinalConsonant = explicitFinalConsonant;
            PronunciationRevision = pronunciationRevision ?? string.Empty;
            Locale = locale ?? string.Empty;
            FormatterVersion = formatterVersion ?? string.Empty;
            Particle = particle;
        }

        public string DisplayTextFingerprint { get; }
        public string DisplaySnapshotRevision { get; }
        public KoreanPronunciationMode PronunciationMode { get; }
        public string PronunciationValueFingerprint { get; }
        public KoreanFinalConsonantKind ExplicitFinalConsonant { get; }
        public string PronunciationRevision { get; }
        public string Locale { get; }
        public string FormatterVersion { get; }
        public KoreanJosaKind Particle { get; }

        public bool IsCacheable
        {
            get
            {
                return !string.IsNullOrEmpty(DisplayTextFingerprint)
                    && !string.IsNullOrEmpty(DisplaySnapshotRevision)
                    && !string.IsNullOrEmpty(PronunciationRevision)
                    && !string.IsNullOrEmpty(Locale)
                    && !string.IsNullOrEmpty(FormatterVersion);
            }
        }

        public bool Equals(KoreanJosaCacheKey other)
        {
            return Particle == other.Particle
                && PronunciationMode == other.PronunciationMode
                && ExplicitFinalConsonant == other.ExplicitFinalConsonant
                && string.Equals(DisplayTextFingerprint, other.DisplayTextFingerprint, StringComparison.Ordinal)
                && string.Equals(DisplaySnapshotRevision, other.DisplaySnapshotRevision, StringComparison.Ordinal)
                && string.Equals(PronunciationValueFingerprint, other.PronunciationValueFingerprint, StringComparison.Ordinal)
                && string.Equals(PronunciationRevision, other.PronunciationRevision, StringComparison.Ordinal)
                && string.Equals(Locale, other.Locale, StringComparison.Ordinal)
                && string.Equals(FormatterVersion, other.FormatterVersion, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is KoreanJosaCacheKey && Equals((KoreanJosaCacheKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Particle;
                hash = (hash * 397) ^ (int)PronunciationMode;
                hash = (hash * 397) ^ (int)ExplicitFinalConsonant;
                hash = (hash * 397) ^ GetOrdinalHashCode(DisplayTextFingerprint);
                hash = (hash * 397) ^ GetOrdinalHashCode(DisplaySnapshotRevision);
                hash = (hash * 397) ^ GetOrdinalHashCode(PronunciationValueFingerprint);
                hash = (hash * 397) ^ GetOrdinalHashCode(PronunciationRevision);
                hash = (hash * 397) ^ GetOrdinalHashCode(Locale);
                hash = (hash * 397) ^ GetOrdinalHashCode(FormatterVersion);
                return hash;
            }
        }

        private static int GetOrdinalHashCode(string value)
        {
            return value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        private static string NormalizeFingerprint(string value)
        {
            if (string.IsNullOrEmpty(value) || !IsWellFormedUtf16(value))
            {
                return value ?? string.Empty;
            }

            return value.Normalize(NormalizationForm.FormC);
        }

        private static bool IsWellFormedUtf16(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        return false;
                    }

                    index++;
                }
                else if (char.IsLowSurrogate(current))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class KoreanJosaResolutionCache
    {
        public const int DefaultCapacity = 4096;

        private readonly Dictionary<KoreanJosaCacheKey, KoreanJosaFormatResult> entries =
            new Dictionary<KoreanJosaCacheKey, KoreanJosaFormatResult>();
        private readonly Queue<KoreanJosaCacheKey> insertionOrder =
            new Queue<KoreanJosaCacheKey>();

        public KoreanJosaResolutionCache(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int Capacity { get; }

        public int Count
        {
            get { return entries.Count; }
        }

        public bool TryGet(KoreanJosaCacheKey key, out KoreanJosaFormatResult result)
        {
            if (!key.IsCacheable)
            {
                result = default(KoreanJosaFormatResult);
                return false;
            }

            return entries.TryGetValue(key, out result);
        }

        public void Store(KoreanJosaCacheKey key, KoreanJosaFormatResult result)
        {
            if (!key.IsCacheable)
                return;
            if (entries.ContainsKey(key))
            {
                entries[key] = result;
                return;
            }
            while (entries.Count >= Capacity && insertionOrder.Count > 0)
                entries.Remove(insertionOrder.Dequeue());
            entries.Add(key, result);
            insertionOrder.Enqueue(key);
        }

        public void Clear()
        {
            entries.Clear();
            insertionOrder.Clear();
        }
    }
}
