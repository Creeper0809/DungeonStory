using DungeonStory.ThirdParty.SmartFormatKorean;

namespace DungeonStory.Narrative.Korean
{
    /// <summary>
    /// Determines the final coda of a complete, non-decimal Sino-Korean number
    /// reading. It intentionally accepts only a typed WholeNumber request;
    /// units, decimals, counters, and identifier digits must provide their own
    /// pronunciation context rather than being guessed from the last glyph.
    /// </summary>
    internal static class KoreanNumberPronunciation
    {
        private static readonly SmartFormatKoreanCodaKind[] DigitCoda =
        {
            SmartFormatKoreanCodaKind.Consonant, // 영
            SmartFormatKoreanCodaKind.Consonant, // 일
            SmartFormatKoreanCodaKind.None,      // 이
            SmartFormatKoreanCodaKind.Consonant, // 삼
            SmartFormatKoreanCodaKind.None,      // 사
            SmartFormatKoreanCodaKind.None,      // 오
            SmartFormatKoreanCodaKind.Consonant, // 육
            SmartFormatKoreanCodaKind.Rieul,     // 칠
            SmartFormatKoreanCodaKind.Rieul,     // 팔
            SmartFormatKoreanCodaKind.None       // 구
        };

        // The final syllable of each four-digit group. This preserves correct
        // final pronunciation for values such as 10, 100, 1000, 10000 and
        // larger whole numbers without reducing them to their final digit.
        private static readonly SmartFormatKoreanCodaKind[] GroupUnitCoda =
        {
            SmartFormatKoreanCodaKind.Unknown,   // no group unit
            SmartFormatKoreanCodaKind.Consonant, // 만
            SmartFormatKoreanCodaKind.Consonant, // 억
            SmartFormatKoreanCodaKind.None,      // 조
            SmartFormatKoreanCodaKind.Consonant, // 경
            SmartFormatKoreanCodaKind.None,      // 해
            SmartFormatKoreanCodaKind.None,      // 자
            SmartFormatKoreanCodaKind.Consonant, // 양
            SmartFormatKoreanCodaKind.None,      // 구
            SmartFormatKoreanCodaKind.Consonant, // 간
            SmartFormatKoreanCodaKind.Consonant, // 정
            SmartFormatKoreanCodaKind.None,      // 재
            SmartFormatKoreanCodaKind.Consonant, // 극
            SmartFormatKoreanCodaKind.None,      // 항하사
            SmartFormatKoreanCodaKind.None,      // 아승기
            SmartFormatKoreanCodaKind.None,      // 나유타
            SmartFormatKoreanCodaKind.None,      // 불가사의
            SmartFormatKoreanCodaKind.None,      // 무량대수
            SmartFormatKoreanCodaKind.Consonant, // 겁
            SmartFormatKoreanCodaKind.Consonant  // 업
        };

        public static bool TryGetFinalCoda(
            string formattedNumber,
            out SmartFormatKoreanCodaKind coda)
        {
            coda = SmartFormatKoreanCodaKind.Unknown;
            if (string.IsNullOrWhiteSpace(formattedNumber))
            {
                return false;
            }

            int firstDigit = -1;
            int lastNonZeroDigit = -1;
            int digitCount = 0;
            for (int index = 0; index < formattedNumber.Length; index++)
            {
                char value = formattedNumber[index];
                if (value >= '0' && value <= '9')
                {
                    if (firstDigit < 0)
                    {
                        firstDigit = index;
                    }

                    if (value != '0')
                    {
                        lastNonZeroDigit = digitCount;
                    }

                    digitCount++;
                    continue;
                }

                if (value == ',' || value == '_' || char.IsWhiteSpace(value))
                {
                    continue;
                }

                if ((value == '+' || value == '-') && digitCount == 0 && firstDigit < 0)
                {
                    continue;
                }

                // Decimal points, counters, units, and identifier notation
                // require an explicit spoken-form hint from their formatter.
                return false;
            }

            if (digitCount == 0)
            {
                return false;
            }

            if (lastNonZeroDigit < 0)
            {
                coda = DigitCoda[0];
                return true;
            }

            int fromRight = digitCount - lastNonZeroDigit - 1;
            if (fromRight == 0)
            {
                char finalDigit = GetDigitAtOrdinal(formattedNumber, lastNonZeroDigit);
                coda = DigitCoda[finalDigit - '0'];
                return true;
            }

            int groupIndex = fromRight / 4;
            if (groupIndex == 0)
            {
                // 십, 백 and 천 each have a final consonant.
                coda = SmartFormatKoreanCodaKind.Consonant;
                return true;
            }

            // The last non-zero digit means every lower digit is zero. For a
            // higher four-digit group, its group unit is therefore the last
            // spoken syllable: 100000 = 십만, 10000000000000 = 십조.
            if (groupIndex >= GroupUnitCoda.Length)
            {
                return false;
            }

            coda = GroupUnitCoda[groupIndex];
            return coda != SmartFormatKoreanCodaKind.Unknown;
        }

        private static char GetDigitAtOrdinal(string input, int ordinal)
        {
            int seen = 0;
            for (int index = 0; index < input.Length; index++)
            {
                char value = input[index];
                if (value < '0' || value > '9')
                {
                    continue;
                }

                if (seen == ordinal)
                {
                    return value;
                }

                seen++;
            }

            return '0';
        }
    }
}
