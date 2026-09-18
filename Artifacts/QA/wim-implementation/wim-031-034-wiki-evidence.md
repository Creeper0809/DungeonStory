# WIM-031 / WIM-034 wiki correction evidence

Date: 2026-09-06. Gameplay code and authored numerical values unchanged.

Authored guide changes:

- `wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md`: central account instead of physical gold carrying/payment access; merchandise and valuables still physical.
- `wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md`: paid trade versus alliance-benefit supply; cooldown EWU formula labeled authoring audit, not replacement for payment.

Direct current source:

- `GameRuntimeServices.cs`: GameMoneyAccount.Balance/TrySpend/TrySpendOnce/TryGetMoney read/write GameSessionState.holdingMoney and ledger receipts. No vault path or carry admission in payment.
- `FactionRouteEconomicPolicy.cs`: PaidMarketPurchase quote requests payment; AllianceBenefit quote payment zero. Quote uses exact item unit price times quantity.
- `FactionRuntime.cs`: route creation calls money.TrySpendOnce before publishing trade; publication failure resolves refund. Supply uses allianceBenefitBudget and domain reservation; CreateAllianceBenefitSettlementReceipt validates positive debit and exact budget balances, paymentGold zero. CompleteRoute calls TryDeliverCargo.
- `FactionAllianceBenefitBudgetReviewAuthority.cs`: supply debit derives from current acquisition ledger and exact cargo quantities.

The guide is authored content, not a generated entity file. No generated index manually edited. Existing content-collections guidance and wiki instructions apply.

Validation PASS: validate_wiki_model.py (2905 entities), validate_document_authority.py, node --test wiki/tests/guide-markdown.test.mjs (5 passed, 0 failed). Both documentation checkpoints closed. No PlayMode required for prose-only corrections.
