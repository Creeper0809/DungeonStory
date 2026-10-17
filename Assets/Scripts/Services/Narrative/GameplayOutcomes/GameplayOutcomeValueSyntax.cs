using System;
using DungeonStory.Narrative.Korean;

/// <summary>Validates frozen outcome text and display-name values without a ledger instance.</summary>
internal static class GameplayOutcomeValueSyntax
{

    internal static bool IsValidDisplayNameSnapshot(in KoreanNameSnapshot value)
    {
        if (!IsValidBoundedUtf16(
                value.DisplayText,
                GameplayOutcomeBufferLimits.MaximumDisplayTextUtf16Length,
                allowEmpty: false)
            || !GameplayOutcomeStableIdSyntax.IsValid(value.DisplaySnapshotRevision)
            || value.Locale == null
            || value.Locale.Length > GameplayOutcomeBufferLimits.MaximumLocaleLength
            || !GameplayOutcomeStableIdSyntax.IsValid(value.Locale)
            || !GameplayOutcomeStableIdSyntax.IsValid(value.PronunciationHint.Revision)
            || !IsValidBoundedUtf16(
                value.PronunciationHint.Value ?? string.Empty,
                GameplayOutcomeBufferLimits.MaximumPronunciationValueUtf16Length,
                allowEmpty: true)
            || (int)value.PronunciationHint.Mode < (int)KoreanPronunciationMode.Unknown
            || (int)value.PronunciationHint.Mode > (int)KoreanPronunciationMode.ExplicitFinalConsonant
            || (int)value.PronunciationHint.ExplicitFinalConsonant < (int)KoreanFinalConsonantKind.Unknown
            || (int)value.PronunciationHint.ExplicitFinalConsonant > (int)KoreanFinalConsonantKind.Rieul)
            return false;

        KoreanPronunciationHint hint = value.PronunciationHint;
        if (hint.Mode == KoreanPronunciationMode.Unknown
            || hint.Mode == KoreanPronunciationMode.AutoHangulDisplay)
        {
            return string.IsNullOrEmpty(hint.Value)
                && hint.ExplicitFinalConsonant == KoreanFinalConsonantKind.Unknown;
        }
        if (hint.Mode == KoreanPronunciationMode.ExplicitFinalConsonant)
        {
            return string.IsNullOrEmpty(hint.Value)
                && hint.ExplicitFinalConsonant != KoreanFinalConsonantKind.Unknown;
        }
        return !string.IsNullOrWhiteSpace(hint.Value)
            && hint.ExplicitFinalConsonant == KoreanFinalConsonantKind.Unknown;
    }

    internal static bool IsValidBoundedUtf16(string value, int maximumLength, bool allowEmpty)
        => DungeonStory.Foundation.Facts.DomainFactValueSyntax.IsText(value, maximumLength, allowEmpty);
}
