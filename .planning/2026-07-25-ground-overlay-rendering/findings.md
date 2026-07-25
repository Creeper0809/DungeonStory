# Findings

- The reported red surface is aligned to grid-cell boundaries and extends over
  multiple exterior cells. The initial overlay hypothesis was only partly
  correct: a recent scene-save pass serialized transient tile data into both
  `Wall` and the authored `Ground` Tilemap.
- `GridUIManager.DrawGrid()` currently writes overlay tiles for every physical
  world cell. Non-installable cells receive a red tint, and the overlay
  TilemapRenderer is forced to sorting order 100, above the authored ground.
- Since the physical grid was expanded beyond the dungeon, this legacy
  full-grid fill now paints the large exterior lower band shown in the report.
- Unity MCP reproduced the defect while the Editor was stopped. The Scene View
  contains the same opaque red grid-aligned band and a large white rectangle,
  proving stale authoring-time overlay/ghost visuals are serialized and visible
  independently of gameplay mode.
- The serialized placement object is named `WhiteBox`, is active, contains 88
  opaque pink tile entries, and relies on a Tilemap global alpha of zero.
- The ordinary `Wall` Tilemap contained the same 88 embedded pink overlay tiles
  while its global alpha was one. This was one contaminated renderer, although
  removing it exposed a second corruption in the `Ground` Tilemap.
- The embedded source tile is a 4x4 solid pink sprite and Tile asset. Placement
  preview data has therefore contaminated the serialized wall tilemap content.
- `HEAD` confirms the intended `Wall` Tilemap had `m_Tiles: {}` while
  `WhiteBox` alone owned the 88 overlay tiles. The current scene save copied
  those entries into `Wall`; this is a regression introduced by the recent
  hierarchy-save pass, not authored terrain.
- `GridTexture.DrawWall()` only removes obsolete tiles equal to its configured
  wall/floor assets. Pink overlay tiles are neither, so they survive runtime
  redraw unless the wall Tilemap is explicitly sanitized.
- A live PlayMode capture still shows the full red band after the starter
  dungeon is generated. Runtime wall redraw therefore confirms the unknown
  tile persistence path rather than repairing it.
- Initial building placement is recreated from runtime grid state before
  `DrawWall()` completes, so clearing unexpected wall-tile entries is safe;
  legitimate wall/floor cells are then redrawn from their configured assets.
- The placement ghost's child sprite is null, so the red band is not emitted by
  `GridGhostObject`.
- After sanitizing `Wall` to zero tiles and restoring the external placement
  tile reference, the red band remained. A complete live renderer inventory
  identified `Ground` as a 275-cell (55x5) opaque Tilemap covering the same
  bounds. The scene save has also replaced/corrupted the authored ground tile
  data, so wall cleanup alone is insufficient.
- The pre-organizer Ground map used two external tiles:
  `TILESET SUMMER DAY_1` (55 cells) and `TILESET SUMMER DAY_9` (220 cells),
  with white per-cell color. The current save replaced both tile/sprite
  references with scene-local generated objects and two opaque red tints while
  preserving the same 275 cell positions.
- Live grouping confirms the intended mapping is structural and simple:
  the top ground row (`y=-1`) has 55 `TILESET SUMMER DAY_1` cells, while the
  four rows below (`y=-5..-2`) have 220 `TILESET SUMMER DAY_9` cells.
- Restoring those external tile references and resetting per-cell colors to
  white removes the red band both in EditMode and PlayMode.
- Five QA fixture objects with missing script references were also serialized
  into `GameplayScene`. They were unrelated to the terrain but caused clean
  PlayMode validation to fail, so only those named invalid fixtures were
  removed.
