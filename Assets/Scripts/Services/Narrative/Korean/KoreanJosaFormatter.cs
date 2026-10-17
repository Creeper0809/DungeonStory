using DungeonStory.ThirdParty.SmartFormatKorean;

namespace DungeonStory.Narrative.Korean
{
    /// <summary>
    /// Project-facing adapter around the BSD-3-Clause SmartFormat.NET-Korean
    /// particle selector. This class owns presentation-only policy; it is
    /// independent of ledger, receipt, entity, UI, and LLM types.
    /// </summary>
    public sealed class KoreanJosaFormatter : IKoreanJosaFormatter
    {
        public const string CurrentFormatterVersion =
            "ds-korean-josa/1;smartformat-net-korean/cd149be3";

        private readonly KoreanJosaResolutionCache cache;

        public KoreanJosaFormatter(KoreanJosaResolutionCache cache = null)
        {
            this.cache = cache ?? new KoreanJosaResolutionCache();
        }

        public string FormatterVersion
        {
            get { return CurrentFormatterVersion; }
        }

        public KoreanJosaFormatResult Format(KoreanJosaRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name.DisplayText))
            {
                return new KoreanJosaFormatResult(
                    KoreanJosaFormatStatus.InvalidDisplayToken,
                    request.Name.DisplayText,
                    string.Empty,
                    KoreanFinalConsonantKind.Unknown);
            }

            KoreanJosaCacheKey cacheKey = new KoreanJosaCacheKey(
                request.Name.DisplayText,
                request.Name.DisplaySnapshotRevision,
                request.Name.PronunciationHint.Mode,
                request.Name.PronunciationHint.Value,
                request.Name.PronunciationHint.ExplicitFinalConsonant,
                request.Name.PronunciationHint.Revision,
                request.Name.Locale,
                FormatterVersion,
                request.Particle);
            KoreanJosaFormatResult cached;
            if (cache.TryGet(cacheKey, out cached))
            {
                return RebindDisplayText(cached, request.Name.DisplayText);
            }

            SmartFormatKoreanCodaKind upstreamCoda;
            if (!TryResolveCoda(request.Name, out upstreamCoda))
            {
                return Store(cacheKey, new KoreanJosaFormatResult(
                    KoreanJosaFormatStatus.UnknownPronunciation,
                    request.Name.DisplayText,
                    string.Empty,
                    KoreanFinalConsonantKind.Unknown));
            }

            string particle;
            if (!SmartFormatKoreanParticleSelector.TrySelect(
                    ToUpstreamParticle(request.Particle),
                    upstreamCoda,
                    out particle))
            {
                return Store(cacheKey, new KoreanJosaFormatResult(
                    KoreanJosaFormatStatus.UnsupportedParticle,
                    request.Name.DisplayText,
                    string.Empty,
                    ToProjectCoda(upstreamCoda)));
            }

            return Store(cacheKey, new KoreanJosaFormatResult(
                KoreanJosaFormatStatus.Applied,
                request.Name.DisplayText + particle,
                particle,
                ToProjectCoda(upstreamCoda)));
        }

        private KoreanJosaFormatResult Store(
            KoreanJosaCacheKey cacheKey,
            KoreanJosaFormatResult result)
        {
            cache.Store(cacheKey, result);
            return result;
        }

        private static KoreanJosaFormatResult RebindDisplayText(
            KoreanJosaFormatResult cached,
            string displayText)
        {
            return new KoreanJosaFormatResult(
                cached.Status,
                cached.Status == KoreanJosaFormatStatus.Applied
                    ? displayText + cached.SelectedParticle
                    : displayText,
                cached.SelectedParticle,
                cached.FinalConsonant);
        }

        private static bool TryResolveCoda(
            KoreanNameSnapshot name,
            out SmartFormatKoreanCodaKind coda)
        {
            coda = SmartFormatKoreanCodaKind.Unknown;
            KoreanPronunciationHint hint = name.PronunciationHint;
            switch (hint.Mode)
            {
                case KoreanPronunciationMode.AutoHangulDisplay:
                {
                    string displayToken;
                    return KoreanVisibleTokenExtractor.TryExtractLastVisibleToken(
                            name.DisplayText,
                            out displayToken)
                        && SmartFormatKoreanParticleSelector.TryGetCodaFromHangul(
                            displayToken,
                            out coda);
                }

                case KoreanPronunciationMode.SpokenHangulHint:
                {
                    string spokenToken;
                    return KoreanVisibleTokenExtractor.TryExtractLastVisibleToken(
                            hint.Value,
                            out spokenToken)
                        && SmartFormatKoreanParticleSelector.TryGetCodaFromHangul(
                            spokenToken,
                            out coda);
                }

                case KoreanPronunciationMode.WholeNumber:
                    return KoreanNumberPronunciation.TryGetFinalCoda(hint.Value, out coda);

                case KoreanPronunciationMode.LatinInitialism:
                    return KoreanInitialismPronunciation.TryGetFinalCoda(hint.Value, out coda);

                case KoreanPronunciationMode.ExplicitFinalConsonant:
                    coda = ToUpstreamCoda(hint.ExplicitFinalConsonant);
                    return coda != SmartFormatKoreanCodaKind.Unknown;

                default:
                    return false;
            }
        }

        private static SmartFormatKoreanParticleFamily ToUpstreamParticle(KoreanJosaKind particle)
        {
            switch (particle)
            {
                case KoreanJosaKind.Topic:
                    return SmartFormatKoreanParticleFamily.Topic;

                case KoreanJosaKind.Subject:
                    return SmartFormatKoreanParticleFamily.Subject;

                case KoreanJosaKind.Object:
                    return SmartFormatKoreanParticleFamily.Object;

                case KoreanJosaKind.Comitative:
                    return SmartFormatKoreanParticleFamily.Comitative;

                case KoreanJosaKind.Direction:
                    return SmartFormatKoreanParticleFamily.Direction;

                default:
                    return (SmartFormatKoreanParticleFamily)(-1);
            }
        }

        private static SmartFormatKoreanCodaKind ToUpstreamCoda(KoreanFinalConsonantKind coda)
        {
            switch (coda)
            {
                case KoreanFinalConsonantKind.None:
                    return SmartFormatKoreanCodaKind.None;

                case KoreanFinalConsonantKind.Consonant:
                    return SmartFormatKoreanCodaKind.Consonant;

                case KoreanFinalConsonantKind.Rieul:
                    return SmartFormatKoreanCodaKind.Rieul;

                default:
                    return SmartFormatKoreanCodaKind.Unknown;
            }
        }

        private static KoreanFinalConsonantKind ToProjectCoda(SmartFormatKoreanCodaKind coda)
        {
            switch (coda)
            {
                case SmartFormatKoreanCodaKind.None:
                    return KoreanFinalConsonantKind.None;

                case SmartFormatKoreanCodaKind.Consonant:
                    return KoreanFinalConsonantKind.Consonant;

                case SmartFormatKoreanCodaKind.Rieul:
                    return KoreanFinalConsonantKind.Rieul;

                default:
                    return KoreanFinalConsonantKind.Unknown;
            }
        }
    }
}
