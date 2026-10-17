using System;
using System.Collections.Generic;
using DungeonStory.ThirdParty.SmartFormatKorean;

namespace DungeonStory.Narrative.Korean
{
    /// <summary>
    /// Handles tokens explicitly declared as Latin initialisms. It never runs
    /// for ordinary English display names, which must instead have a Korean
    /// spoken-form hint or report UnknownPronunciation.
    /// </summary>
    internal static class KoreanInitialismPronunciation
    {
        private static readonly Dictionary<char, SmartFormatKoreanCodaKind> LastLetterCoda =
            new Dictionary<char, SmartFormatKoreanCodaKind>
            {
                { 'A', SmartFormatKoreanCodaKind.None },       // 에이
                { 'B', SmartFormatKoreanCodaKind.None },       // 비
                { 'C', SmartFormatKoreanCodaKind.None },       // 씨
                { 'D', SmartFormatKoreanCodaKind.None },       // 디
                { 'E', SmartFormatKoreanCodaKind.None },       // 이
                { 'F', SmartFormatKoreanCodaKind.Consonant },  // 에프
                { 'G', SmartFormatKoreanCodaKind.None },       // 지
                { 'H', SmartFormatKoreanCodaKind.Consonant },  // 에이치
                { 'I', SmartFormatKoreanCodaKind.None },       // 아이
                { 'J', SmartFormatKoreanCodaKind.None },       // 제이
                { 'K', SmartFormatKoreanCodaKind.None },       // 케이
                { 'L', SmartFormatKoreanCodaKind.Consonant },  // 엘
                { 'M', SmartFormatKoreanCodaKind.Consonant },  // 엠
                { 'N', SmartFormatKoreanCodaKind.Consonant },  // 엔
                { 'O', SmartFormatKoreanCodaKind.None },       // 오
                { 'P', SmartFormatKoreanCodaKind.None },       // 피
                { 'Q', SmartFormatKoreanCodaKind.None },       // 큐
                { 'R', SmartFormatKoreanCodaKind.Rieul },      // 알
                { 'S', SmartFormatKoreanCodaKind.Consonant },  // 에스
                { 'T', SmartFormatKoreanCodaKind.None },       // 티
                { 'U', SmartFormatKoreanCodaKind.None },       // 유
                { 'V', SmartFormatKoreanCodaKind.None },       // 브이
                { 'W', SmartFormatKoreanCodaKind.None },       // 더블유
                { 'X', SmartFormatKoreanCodaKind.Consonant },  // 엑스
                { 'Y', SmartFormatKoreanCodaKind.None },       // 와이
                { 'Z', SmartFormatKoreanCodaKind.Consonant }   // 제트
            };

        public static bool TryGetFinalCoda(string initialism, out SmartFormatKoreanCodaKind coda)
        {
            coda = SmartFormatKoreanCodaKind.Unknown;
            if (string.IsNullOrWhiteSpace(initialism))
            {
                return false;
            }

            int index = initialism.Length - 1;
            while (index >= 0 && char.IsWhiteSpace(initialism[index]))
            {
                index--;
            }

            if (index < 0)
            {
                return false;
            }

            char terminal = initialism[index];
            if (terminal < 'A' || terminal > 'Z')
            {
                return false;
            }

            for (int position = 0; position <= index; position++)
            {
                char current = initialism[position];
                if (!char.IsWhiteSpace(current) && (current < 'A' || current > 'Z'))
                {
                    return false;
                }
            }

            return LastLetterCoda.TryGetValue(terminal, out coda);
        }
    }
}
