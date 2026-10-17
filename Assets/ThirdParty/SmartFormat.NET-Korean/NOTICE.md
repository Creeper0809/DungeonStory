# SmartFormat.NET-Korean notice

DungeonStory includes a narrow Unity-compatible port of the Korean particle
selection algorithm from SmartFormat.NET-Korean.

- Upstream: https://github.com/what-studio/SmartFormat.NET-Korean
- Pinned upstream commit: `cd149be3a683a2ef49fbef44e31308d39c3ca375`
- Copyright: Copyright (c) 2016, What! Studio
- License: BSD-3-Clause; the complete upstream license is retained in
  [LICENSE](LICENSE).

The upstream `KoreanFormatter` is a SmartFormat.NET 1.6.1 extension targeting
net40. DungeonStory does not currently ship that dependency, and its package
manifests were already dirty when this slice was introduced. To avoid creating
a second formatter runtime or changing package resolution, only the isolated
Hangul final-consonant and particle-selection portion is ported under
`Runtime/SmartFormatKoreanParticleSelector.cs`.

The DungeonStory adapter adds display/pronunciation separation, Unicode and
TMP-markup handling, typed unknown-pronunciation results, complete-number and
initialism context handling, and cache-key plumbing. Those additions are local
adapter code; they are not represented as upstream SmartFormat.NET behaviour.
