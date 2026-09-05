# CombatArmorSO

방어 부위와 피해 저항을 장비 선택에 연결한다

총 21개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/combat-health-world/combat-armor.csv)
- [중첩 작성 필드 CSV](../../../fields/combat-health-world/combat-armor.csv)
- [정방향 관계 CSV](../../../relations/combat-health-world/combat-armor.csv)
- [역방향 관계 CSV](../../../incoming/combat-health-world/combat-armor.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `equipment-item:armor:articulated-plate` | 관절식 판금갑 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=256; requiredResearchId=research:equipment:articulated-plate | catalog-registered-static-consumer | active-authored | 0 | [A12_ArticulatedPlate.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A12_ArticulatedPlate.asset) |
| `equipment-item:armor:blacksteel-carapace` | 흑강 갑각 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=328; requiredResearchId=research:industry:dark-foundry | catalog-registered-static-consumer | active-authored | 0 | [A17_BlacksteelCarapace.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A17_BlacksteelCarapace.asset) |
| `equipment-item:armor:blast-coat` | 방폭 외투 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=224; requiredResearchId=research:equipment:pressure-barrels | catalog-registered-static-consumer | active-authored | 0 | [A13_BlastCoat.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A13_BlastCoat.asset) |
| `equipment-item:armor:breastplate` | 철 흉갑 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=184; requiredResearchId=research:equipment:articulated-plate | catalog-registered-static-consumer | active-authored | 0 | [A08_Breastplate.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A08_Breastplate.asset) |
| `equipment-item:armor:brigandine` | 브리간딘 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=168; requiredResearchId=research:equipment:armor-tailoring | catalog-registered-static-consumer | active-authored | 0 | [A09_Brigandine.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A09_Brigandine.asset) |
| `equipment-item:armor:closed-plate-helm` | 폐쇄형 판금 투구 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=84; requiredResearchId=research:equipment:articulated-plate | catalog-registered-static-consumer | active-authored | 0 | [A11_ClosedPlateHelm.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A11_ClosedPlateHelm.asset) |
| `equipment-item:armor:cloth-hood` | 천 후드 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=52 | catalog-registered-static-consumer | active-authored | 0 | [A01_ClothHood.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A01_ClothHood.asset) |
| `equipment-item:armor:gambeson` | 누비옷 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=108; requiredResearchId=research:textile:tailoring | catalog-registered-static-consumer | active-authored | 0 | [A02_Gambeson.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A02_Gambeson.asset) |
| `equipment-item:armor:hardened-leather-coat` | 경화 가죽 외투 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=148; requiredResearchId=research:textile:tanning | catalog-registered-static-consumer | active-authored | 0 | [A21_HardenedLeatherCoat.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A21_HardenedLeatherCoat.asset) |
| `equipment-item:armor:iron-helmet` | 철 투구 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=84; requiredResearchId=research:equipment:articulated-plate | catalog-registered-static-consumer | active-authored | 0 | [A07_IronHelmet.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A07_IronHelmet.asset) |
| `equipment-item:armor:jack-of-plates` | 잭 오브 플레이트 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=168; requiredResearchId=research:equipment:armor-tailoring | catalog-registered-static-consumer | active-authored | 0 | [A18_JackOfPlates.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A18_JackOfPlates.asset) |
| `equipment-item:armor:leather` | 가죽 갑옷 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=124; requiredResearchId=research:textile:tanning | catalog-registered-static-consumer | active-authored | 0 | [A04_LeatherArmor.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A04_LeatherArmor.asset) |
| `equipment-item:armor:leather-cap` | 가죽 모자 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=52 | catalog-registered-static-consumer | active-authored | 0 | [A03_LeatherCap.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A03_LeatherCap.asset) |
| `equipment-item:armor:mail-coif` | 사슬 두건 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=88; requiredResearchId=research:equipment:mail-weaving | catalog-registered-static-consumer | active-authored | 0 | [A05_MailCoif.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A05_MailCoif.asset) |
| `equipment-item:armor:mail-shirt` | 사슬 갑옷 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=152; requiredResearchId=research:equipment:mail-weaving | catalog-registered-static-consumer | active-authored | 0 | [A06_MailShirt.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A06_MailShirt.asset) |
| `equipment-item:armor:padded-hood` | 누비 두건 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=80; requiredResearchId=research:textile:tailoring | catalog-registered-static-consumer | active-authored | 0 | [A20_PaddedHood.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A20_PaddedHood.asset) |
| `equipment-item:armor:powder-cuirass` | 화약수 흉갑 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=256; requiredResearchId=research:equipment:pressure-barrels | catalog-registered-static-consumer | active-authored | 0 | [A19_PowderCuirass.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A19_PowderCuirass.asset) |
| `equipment-item:armor:powered-harness` | 동력 보조 갑주 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=392; requiredResearchId=research:equipment:powered-armor | catalog-registered-static-consumer | active-authored | 0 | [A15_PoweredHarness.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A15_PoweredHarness.asset) |
| `equipment-item:armor:rune-ward-mail` | 룬 수호 사슬갑옷 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=344; requiredResearchId=research:equipment:rune-module-tuning | catalog-registered-static-consumer | active-authored | 0 | [A16_RuneWardMail.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A16_RuneWardMail.asset) |
| `equipment-item:armor:scale-coat` | 비늘 외투 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=208; requiredResearchId=research:equipment:mail-weaving | catalog-registered-static-consumer | active-authored | 0 | [A10_ScaleCoat.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A10_ScaleCoat.asset) |
| `equipment-item:armor:smoke-hood` | 연기 두건 | 방어 부위와 피해 저항을 장비 선택에 연결한다 | requiredCraftWork=140; requiredResearchId=research:equipment:pressure-barrels | catalog-registered-static-consumer | active-authored | 0 | [A14_SmokeHood.asset](../../../../../Assets/Resources/SO/Combat/Equipment/A14_SmokeHood.asset) |
