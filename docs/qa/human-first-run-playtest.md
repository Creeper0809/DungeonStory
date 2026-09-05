# DungeonStory Human First-Run Playtest

## Purpose

This is the final subjective release gate. It evaluates whether a new player can understand and enjoy the already-verified game loop without QA commands or prior instructions.

Use `Builds/HumanPlaytest/DungeonStoryPlaytest.exe`. This build uses product ID `com.dungeonstory.game.playtest` and stores progress separately from the normal DungeonStory profile.

## Rules

- Start from the empty playtest profile.
- Use only visible player-facing controls.
- Normal pause and `x1` through `x5` speed controls are allowed.
- Do not read design documents or QA reports during the run.
- Continue through victory or defeat unless a blocker makes progress impossible.
- Record confusion when it happens; do not reconstruct it afterward.
- Do not use the automation bridge to choose or click for the human player during the scored session. It may observe state and take captures, but the human gate measures unaided comprehension.

## Automation Support

Launch the isolated player with `-automation` when a Codex observer needs standalone state and visual evidence. The bridge binds to loopback, requires the per-launch token, and is rejected by the normal Release product.

```powershell
Builds/HumanPlaytest/DungeonStoryPlaytest.exe -automation -automation-port 48761
```

`tools/player-automation/Invoke-DungeonPlayerAutomation.ps1` can query the current objective, list active controls, send pointer/key input, and request a full-frame PNG. `tools/player-automation/dungeon_player_mcp.py` exposes the same operations as the configured `dungeon-player` MCP server. Automated input is for reproducible diagnosis and objective regression only; any run using it is not a human pass result.

## Milestones

| Milestone | Real time | Clear without help? | Notes |
|---|---:|---|---|
| Owner selected |  |  |  |
| First usable room completed |  |  |  |
| First customer service and revenue |  |  |  |
| First staff or stock problem understood |  |  |  |
| First research completed |  |  |  |
| First invasion resolved |  |  |  |
| First meaningful build branch chosen |  |  |  |
| Final invasion reached |  |  |  |
| Victory or defeat understood |  |  |  |

## Ratings

Score each item from `1` to `5`.

| Question | Score | Evidence |
|---|---:|---|
| I knew what to do next. |  |  |
| Building a room was readable and comfortable. |  |  |
| Waiting time stayed interesting. |  |  |
| Important events were noticeable. |  |  |
| Failures explained their causes. |  |  |
| Rewards felt worth the effort. |  |  |
| The final invasion felt like a climax. |  |  |
| I want to try a different build next run. |  |  |

## Friction Log

Record each moment that causes more than ten seconds of uncertainty or repeated clicking.

| Time | Screen or system | Expected | What happened | Severity 1-3 |
|---:|---|---|---|---:|
|  |  |  |  |  |

## Pass Rule

The human gate passes when all of the following are true:

- No progression blocker or save loss occurs.
- The player completes the first usable room without external instruction.
- The player can state why the first invasion succeeded or failed.
- No required action needs repeated blind clicking.
- `I knew what to do next` and `I want to try a different build next run` score at least `4`.
- No rating is below `3`; any lower score produces a focused tuning task and a retest.
