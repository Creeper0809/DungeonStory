# Surgery System Implementation

## Goal

Implement the complete surgery, organ transplant, prosthetic, specialized operating-room,
research, AI, UI, and save loop on the current V16 gameplay architecture.

## Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit medical, anatomy, work, building, research, item, UI, and save extension points | Complete |
| 2 | Add anatomy profiles, Medical stat, surgery orders, risk evaluation, and surgical parts | Complete |
| 3 | Add specialized surgery abilities, authored facilities, research, recipes, and unlocks | Complete |
| 4 | Connect humanoid/wildlife subjects, work AI, hauling, policies, and persistence | Complete |
| 5 | Add health, facility, research, and surgery command UI | Complete |
| 6 | Run focused contracts, pointer PlayMode flows, visual capture, and console regression | Complete |

## Fixed Decisions

- Humanoids and wildlife are valid surgery subjects.
- Living restrained subjects and fresh corpses can be organ sources.
- Unwilling living subjects require restraint and anesthesia; direct commands do not bypass this.
- Surgery uses a new Medical stat and a stable `work:surgery` work ID.
- Full major-organ anatomy is modeled with stable string node IDs.
- Cross-species grafts provide strong effects and ongoing rejection risk.
- Emergency automation is policy-based: owner/staff enabled by default; other groups disabled.
- Specialized facilities remain useful in parallel rather than replacing lower-tier facilities.
- Global V16 remains; owned save-section versions migrate independently.
- LLM is not used for surgery rules, effects, or runtime decisions.

## Verification Gate

1. Unity compiles with Console Error 0 / Warning 0.
2. Surgery cannot begin without a valid patient, table, surgeon, materials, restraint, or anesthesia when required.
3. Work, risks, interruption, body effects, graft rejection, and game pause are deterministic.
4. Human and wildlife operations, corpse extraction, unique organ persistence, and spoilage work.
5. Research unlocks every authored surgery facility and procedure without duplicate IDs.
6. Pointer-driven UI performs the full research -> build -> haul -> operate -> recover loop.
7. Save/load restores anatomy, orders, reservations, organs, facilities, policies, and rejection.

## Errors

| Error | Attempt | Resolution |
|---|---|---|
| High-speed work skipped observable clinical states | 1 | Record every crossed clinical threshold on the order, including multi-threshold work ticks. |
| PlayMode outcome was nondeterministic | 2 | Seed only the dedicated surgery outcome stream in the verifier; production probability remains unchanged. |
| Severe legacy surface injuries were masked by healthy anatomy nodes | 1 | Clamp anatomy capacities against the existing head/torso/limb capacity result. |
| Completed research restored progress but not current unlocks | 1 | Reapply building and recipe rewards while restoring completed research projects. |
