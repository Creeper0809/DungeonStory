using System;

namespace DungeonStory.Narrative.Korean
{
    public enum KoreanJosaKind
    {
        Topic = 0,
        Subject = 1,
        Object = 2,
        Comitative = 3,
        Direction = 4
    }

    /// <summary>
    /// The declared source of the pronunciation used only to choose a Korean
    /// particle. It never changes the display snapshot shown to the player.
    /// </summary>
    public enum KoreanPronunciationMode
    {
        Unknown = 0,
        AutoHangulDisplay = 1,
        SpokenHangulHint = 2,
        WholeNumber = 3,
        LatinInitialism = 4,
        ExplicitFinalConsonant = 5
    }

    public enum KoreanFinalConsonantKind
    {
        Unknown = 0,
        None = 1,
        Consonant = 2,
        Rieul = 3
    }

    public enum KoreanJosaFormatStatus
    {
        Applied = 0,
        UnknownPronunciation = 1,
        InvalidDisplayToken = 2,
        UnsupportedParticle = 3
    }

    public struct KoreanPronunciationHint
    {
        public KoreanPronunciationHint(
            KoreanPronunciationMode mode,
            string value,
            KoreanFinalConsonantKind explicitFinalConsonant,
            string revision)
        {
            Mode = mode;
            Value = value ?? string.Empty;
            ExplicitFinalConsonant = explicitFinalConsonant;
            Revision = revision ?? string.Empty;
        }

        public KoreanPronunciationMode Mode { get; }
        public string Value { get; }
        public KoreanFinalConsonantKind ExplicitFinalConsonant { get; }
        public string Revision { get; }

        public static KoreanPronunciationHint Unknown(string revision = "")
        {
            return new KoreanPronunciationHint(
                KoreanPronunciationMode.Unknown,
                string.Empty,
                KoreanFinalConsonantKind.Unknown,
                revision);
        }

        public static KoreanPronunciationHint AutoHangulDisplay(string revision = "")
        {
            return new KoreanPronunciationHint(
                KoreanPronunciationMode.AutoHangulDisplay,
                string.Empty,
                KoreanFinalConsonantKind.Unknown,
                revision);
        }

        public static KoreanPronunciationHint SpokenHangul(string spokenForm, string revision)
        {
            return new KoreanPronunciationHint(
                KoreanPronunciationMode.SpokenHangulHint,
                spokenForm,
                KoreanFinalConsonantKind.Unknown,
                revision);
        }

        public static KoreanPronunciationHint WholeNumber(string formattedNumber, string revision)
        {
            return new KoreanPronunciationHint(
                KoreanPronunciationMode.WholeNumber,
                formattedNumber,
                KoreanFinalConsonantKind.Unknown,
                revision);
        }

        public static KoreanPronunciationHint LatinInitialism(string initialism, string revision)
        {
            return new KoreanPronunciationHint(
                KoreanPronunciationMode.LatinInitialism,
                initialism,
                KoreanFinalConsonantKind.Unknown,
                revision);
        }

        public static KoreanPronunciationHint ExplicitFinal(
            KoreanFinalConsonantKind finalConsonant,
            string revision)
        {
            return new KoreanPronunciationHint(
                KoreanPronunciationMode.ExplicitFinalConsonant,
                string.Empty,
                finalConsonant,
                revision);
        }
    }

    /// <summary>
    /// Immutable display snapshot supplied by a descriptor or identity service.
    /// Revision fields are required for cache safety; the formatter does not
    /// own character identities or mutate their presentation data.
    /// </summary>
    public struct KoreanNameSnapshot
    {
        public KoreanNameSnapshot(
            string displayText,
            string displaySnapshotRevision,
            KoreanPronunciationHint pronunciationHint,
            string locale)
        {
            DisplayText = displayText ?? string.Empty;
            DisplaySnapshotRevision = displaySnapshotRevision ?? string.Empty;
            PronunciationHint = pronunciationHint;
            Locale = locale ?? string.Empty;
        }

        public string DisplayText { get; }
        public string DisplaySnapshotRevision { get; }
        public KoreanPronunciationHint PronunciationHint { get; }
        public string Locale { get; }
    }

    public struct KoreanJosaRequest
    {
        public KoreanJosaRequest(KoreanNameSnapshot name, KoreanJosaKind particle)
        {
            Name = name;
            Particle = particle;
        }

        public KoreanNameSnapshot Name { get; }
        public KoreanJosaKind Particle { get; }
    }

    public struct KoreanJosaFormatResult
    {
        public KoreanJosaFormatResult(
            KoreanJosaFormatStatus status,
            string text,
            string selectedParticle,
            KoreanFinalConsonantKind finalConsonant)
        {
            Status = status;
            Text = text ?? string.Empty;
            SelectedParticle = selectedParticle ?? string.Empty;
            FinalConsonant = finalConsonant;
        }

        public KoreanJosaFormatStatus Status { get; }
        public string Text { get; }
        public string SelectedParticle { get; }
        public KoreanFinalConsonantKind FinalConsonant { get; }

        /// <summary>
        /// The caller must use its descriptor-defined, particle-free neutral
        /// frame. Do not infer or silently append a guessed particle.
        /// </summary>
        public bool RequiresNeutralFrame
        {
            get { return Status != KoreanJosaFormatStatus.Applied; }
        }
    }

    public interface IKoreanJosaFormatter
    {
        string FormatterVersion { get; }
        KoreanJosaFormatResult Format(KoreanJosaRequest request);
    }
}
