using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonStory.Narrative.Korean
{
    /// <summary>
    /// Locates the final visible character for pronunciation analysis without
    /// changing the original display string. It understands common TMP rich
    /// text tags and only removes recognised tags from the analysis stream.
    /// </summary>
    internal static class KoreanVisibleTokenExtractor
    {
        private static readonly HashSet<string> RichTextTags = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "a", "align", "alpha", "b", "br", "color", "cspace", "font", "gradient",
            "i", "indent", "line-height", "line-indent", "link", "lowercase", "margin",
            "mark", "material", "mspace", "nobr", "page", "pos", "rotate", "s", "size",
            "smallcaps", "space", "sprite", "style", "sub", "sup", "u", "uppercase",
            "voffset", "width"
        };

        public static bool TryExtractLastVisibleToken(string text, out string token)
        {
            token = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized;
            if (!TryNormalizeWellFormedUtf16(text, out normalized))
            {
                return false;
            }
            int index = normalized.Length - 1;
            SkipTrailingMarkupAndPunctuation(normalized, ref index);
            if (index < 0)
            {
                return false;
            }

            int end = index;
            while (index >= 0 && !char.IsWhiteSpace(normalized[index]))
            {
                if (normalized[index] == '>' && TrySkipRichTextTag(normalized, ref index))
                {
                    continue;
                }

                index--;
            }

            int start = index + 1;
            if (start > end)
            {
                return false;
            }

            string candidate = normalized.Substring(start, end - start + 1);
            int candidateLast = candidate.Length - 1;
            SkipTrailingMarkupAndPunctuation(candidate, ref candidateLast);
            if (candidateLast < 0)
            {
                return false;
            }

            token = candidate.Substring(0, candidateLast + 1);
            return token.Length > 0;
        }

        private static void SkipTrailingMarkupAndPunctuation(string text, ref int index)
        {
            while (index >= 0)
            {
                if (text[index] == '>' && TrySkipRichTextTag(text, ref index))
                {
                    continue;
                }

                if (!char.IsWhiteSpace(text[index]) && !IsTrailingWrapper(text[index]))
                {
                    return;
                }

                index--;
            }
        }

        private static bool TrySkipRichTextTag(string text, ref int index)
        {
            int tagEnd = index;
            int tagStart = text.LastIndexOf('<', tagEnd);
            if (tagStart < 0 || !IsRecognizedRichTextTag(text, tagStart, tagEnd))
            {
                return false;
            }

            index = tagStart - 1;
            return true;
        }

        private static bool IsRecognizedRichTextTag(string text, int tagStart, int tagEnd)
        {
            int index = tagStart + 1;
            while (index < tagEnd && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index < tagEnd && text[index] == '/')
            {
                index++;
            }

            while (index < tagEnd && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            int nameStart = index;
            while (index < tagEnd && IsTagNameCharacter(text[index]))
            {
                index++;
            }

            if (index == nameStart)
            {
                return false;
            }

            string tagName = text.Substring(nameStart, index - nameStart);
            return RichTextTags.Contains(tagName);
        }

        private static bool IsTagNameCharacter(char value)
        {
            return (value >= 'a' && value <= 'z')
                || (value >= 'A' && value <= 'Z')
                || value == '-';
        }

        private static bool IsTrailingWrapper(char value)
        {
            switch (value)
            {
                case '!':
                case '"':
                case '\'':
                case ',':
                case '.':
                case ':':
                case ';':
                case '?':
                case '~':
                case ')':
                case ']':
                case '}':
                case '>':
                case '\u2019':
                case '\u201D':
                case '\u3009':
                case '\u300B':
                case '\u300D':
                case '\u300F':
                    return true;

                default:
                    return false;
            }
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
