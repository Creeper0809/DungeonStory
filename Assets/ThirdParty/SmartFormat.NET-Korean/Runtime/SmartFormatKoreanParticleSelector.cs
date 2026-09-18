using System;
using System.Text;

// Copyright (c) 2016, What! Studio.
// Derived from SmartFormat.NET-Korean at commit
// cd149be3a683a2ef49fbef44e31308d39c3ca375. See ../LICENSE and
// ../PROVENANCE.json. This is a deliberately narrow port of the upstream
// Hangul final-consonant and particle-selection algorithm; it has no
// SmartFormat.NET or DungeonStory runtime dependency.

namespace DungeonStory.ThirdParty.SmartFormatKorean
{
    public enum SmartFormatKoreanCodaKind
    {
        Unknown = 0,
        None = 1,
        Consonant = 2,
        Rieul = 3
    }

    public enum SmartFormatKoreanParticleFamily
    {
        Topic = 0,
        Subject = 1,
        Object = 2,
        Comitative = 3,
        Direction = 4
    }

    /// <summary>
    /// Minimal, Unity-compatible particle selector derived from the upstream
    /// SmartFormat.NET-Korean KoreanFormatter and Hangul utilities.
    /// </summary>
    public static class SmartFormatKoreanParticleSelector
    {
        private const char FirstHangulSyllable = '\uAC00';
        private const char LastHangulSyllable = '\uD7A3';
        private const int JongsungCount = 28;
        private const int RieulJongsungIndex = 8;

        public static bool TryGetCodaFromHangul(string pronunciation, out SmartFormatKoreanCodaKind coda)
        {
            coda = SmartFormatKoreanCodaKind.Unknown;
            if (string.IsNullOrWhiteSpace(pronunciation))
            {
                return false;
            }

            string normalized;
            if (!TryNormalizeWellFormedUtf16(pronunciation, out normalized))
            {
                return false;
            }
            int index = normalized.Length - 1;
            while (index >= 0 && char.IsWhiteSpace(normalized[index]))
            {
                index--;
            }

            if (index < 0)
            {
                return false;
            }

            char syllable = normalized[index];
            if (syllable < FirstHangulSyllable || syllable > LastHangulSyllable)
            {
                return false;
            }

            int jongsungIndex = (syllable - FirstHangulSyllable) % JongsungCount;
            if (jongsungIndex == 0)
            {
                coda = SmartFormatKoreanCodaKind.None;
            }
            else if (jongsungIndex == RieulJongsungIndex)
            {
                coda = SmartFormatKoreanCodaKind.Rieul;
            }
            else
            {
                coda = SmartFormatKoreanCodaKind.Consonant;
            }

            return true;
        }

        public static bool TrySelect(
            SmartFormatKoreanParticleFamily family,
            SmartFormatKoreanCodaKind coda,
            out string particle)
        {
            particle = string.Empty;
            if (coda == SmartFormatKoreanCodaKind.Unknown)
            {
                return false;
            }

            switch (family)
            {
                case SmartFormatKoreanParticleFamily.Topic:
                    particle = HasCoda(coda) ? "은" : "는";
                    return true;

                case SmartFormatKoreanParticleFamily.Subject:
                    particle = HasCoda(coda) ? "이" : "가";
                    return true;

                case SmartFormatKoreanParticleFamily.Object:
                    particle = HasCoda(coda) ? "을" : "를";
                    return true;

                case SmartFormatKoreanParticleFamily.Comitative:
                    particle = HasCoda(coda) ? "과" : "와";
                    return true;

                case SmartFormatKoreanParticleFamily.Direction:
                    particle = coda == SmartFormatKoreanCodaKind.None
                        || coda == SmartFormatKoreanCodaKind.Rieul
                        ? "로"
                        : "으로";
                    return true;

                default:
                    return false;
            }
        }

        private static bool HasCoda(SmartFormatKoreanCodaKind coda)
        {
            return coda == SmartFormatKoreanCodaKind.Consonant
                || coda == SmartFormatKoreanCodaKind.Rieul;
        }

        private static bool TryNormalizeWellFormedUtf16(string input, out string normalized)
        {
            normalized = string.Empty;
            for (int index = 0; index < input.Length; index++)
            {
                char current = input[index];
                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 >= input.Length || !char.IsLowSurrogate(input[index + 1]))
                    {
                        return false;
                    }

                    index++;
                    continue;
                }

                if (char.IsLowSurrogate(current))
                {
                    return false;
                }
            }

            normalized = input.Normalize(NormalizationForm.FormC);
            return true;
        }
    }
}
