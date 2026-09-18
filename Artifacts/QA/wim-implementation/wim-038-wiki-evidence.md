# WIM-038 — closed (wiki correction)

Date: 2026-09-06. Gameplay source unchanged by this correction.

Changed authored source: `wiki/game-versions/0.0.1v/content/guides/events-and-choices.md`.
The generator `Tools/Wiki/generate_wiki_model.py:878` writes versioned data and spoiler output, not this authored guide. No generated index was manually edited.

Direct implementation evidence: `Assets/Scripts/Services/Run/V20CampaignRuntime.cs`:

- EvaluateSociety checks lastEvaluationAbsoluteDay, records the day, and expires events only when deadlineAbsoluteDay < day.
- Ordinary cap is 1 through day30 and2 thereafter, plus one emergency slot.
- occupied participant set is shared between active and newly selected events; automatic count stops at6.
- MarkEventRecurrence uses life-definition cooldown or30 for requests/incidents, and category delay3.
- EvaluateSeasonal waits until its current event terminates before selecting a next event.

Corrected the guide's guaranteed2–5/1–2-day intervals, blanket2-event cap and unimplemented5/2 alert caps/daily automatic summary. Kept cost/expected-value bands explicitly labeled as balance targets. Other WIM event implementation descriptions remain under their own work items.

Validation:

- `python -X utf8 Tools/Wiki/validate_wiki_model.py --repo-root . --game-version 0.0.1v`: valid,2905 entities.
- `python -X utf8 Tools/Wiki/validate_document_authority.py --repo-root . --game-version 0.0.1v`: valid.
- `node --test wiki/tests/guide-markdown.test.mjs`:5 passed,0 failed (all versioned guides included).
- First prose validation rejected the marker 아니다; wording corrected and validation rerun successfully. No rule disabled.

Balance impact: none; actual event scheduling and all authored numbers unchanged. This is a documentation checkpoint, not gameplay or full source freshness certification. Astro content guidance consulted per wiki/AGENTS.md: https://docs.astro.build/en/guides/content-collections/ . No Astro schema or routing change.
