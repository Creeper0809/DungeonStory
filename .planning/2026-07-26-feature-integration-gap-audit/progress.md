# Progress

## 2026-07-26

- Started a code-level integration audit across gameplay domains.
- Defined the audit chain as input/UI -> AI/command -> movement/work -> world
  result -> cross-domain consequence -> save/restore -> player feedback.
- Confirmed duplicate enabled scene runtimes for owner commands and regular
  customers. These are scene composition defects, not intentional fallback
  objects.
- Confirmed exterior incidents have no domain consequence beyond work urgency
  and UI text.
- Confirmed survival weather and AI weather pressure use separate models.
- Confirmed legacy expedition equipment and common combat equipment are both
  registered, saved, shown, and applied instead of using a compatibility
  adapter.
- Confirmed captivity labor itself reaches the normal work selector; it is not
  an orphan.
- Confirmed circus fame/privilege has no late progression consumer.
- Confirmed extracted blood/memory collapse into generic Mana with no thematic
  downstream system.
- Confirmed the AI performance report/settings types are declaration-only.
- Confirmed food is consumed once by daily survival accounting and again by
  actual meal facility stock.
- Confirmed exterior night danger is currently a dashboard/refuel score rather
  than an incident driver.
- Confirmed four offense reward categories are saved/displayed counters with no
  downstream domain effect; recruit-candidate rewards do have a regular-customer
  event bridge.
- Confirmed `BuildingExpeditionSupportAbility` has neither assets nor runtime
  consumers.
- Unity MCP verified the loaded `GameplayScene` has two active/enabled owner
  command controllers and two active/enabled regular-customer runtimes.
- Unity Editor was idle, not compiling, and Console had 0 Error / 0 Warning at
  audit completion.
- One combined planning-file patch missed its context; the correction was
  reapplied as a targeted patch without changing product code.
- Completed the integration audit and ranked the repair order.
