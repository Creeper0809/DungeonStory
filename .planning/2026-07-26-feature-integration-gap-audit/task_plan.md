# Feature Integration Gap Audit

## Goal

Find gameplay features that exist in isolation instead of participating in a
complete player-visible loop from command and AI through world effects, UI,
save/restore, and downstream consequences.

## Phases

- [x] Inventory domain entry points, consumers, events, UI, and save sections.
- [x] Find registered or modeled features with weak or missing consumers.
- [x] Separate intentional future hooks from product-facing broken links.
- [x] Rank findings by player impact and recommend the next integration order.

## Constraints

- This pass is an audit only; do not change gameplay behavior.
- Ignore unrelated dirty worktree changes.
- Prefer concrete file and line references over speculative architecture notes.
