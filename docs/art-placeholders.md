# P1 플레이스홀더 이미지

P1 구현에서는 최종 아트를 만들지 않고, 검증용 플레이스홀더 이미지를 사용한다.

기준:

- 흰 배경 + 검은 한글 라벨
- 기능 구분이 목적이며, 미술 품질 검증용이 아니다.
- Unity import 설정은 `Sprite`, `Single`, `Point`, `PPU 16`, 압축 없음으로 둔다.
- P1 시설, 종족, 상태 피드백을 구현할 때 아래 이미지를 우선 연결한다.
- 최종 아트가 들어오면 같은 의미의 Sprite 참조만 교체한다.

## 위치

```text
Assets/Images/Placeholders
```

## 시설

```text
Assets/Images/Placeholders/Facilities/facility_food_basic.png
Assets/Images/Placeholders/Facilities/facility_meat_restaurant.png
Assets/Images/Placeholders/Facilities/facility_general_store.png
Assets/Images/Placeholders/Facilities/facility_weapon_shop.png
Assets/Images/Placeholders/Facilities/facility_rest_room.png
Assets/Images/Placeholders/Facilities/facility_training_room.png
Assets/Images/Placeholders/Facilities/facility_research_lab.png
Assets/Images/Placeholders/Facilities/facility_mana_storage.png
Assets/Images/Placeholders/Facilities/facility_warehouse.png
Assets/Images/Placeholders/Facilities/facility_battle_dining.png
Assets/Images/Placeholders/Facilities/facility_premium_meat_restaurant.png
Assets/Images/Placeholders/Facilities/facility_battlefield_dining.png
Assets/Images/Placeholders/Facilities/facility_noble_dining.png
```

## 방어 시설

```text
Assets/Images/Placeholders/Defense/defense_spike.png
Assets/Images/Placeholders/Defense/defense_poison.png
Assets/Images/Placeholders/Defense/defense_fire.png
Assets/Images/Placeholders/Defense/defense_lightning.png
Assets/Images/Placeholders/Defense/defense_ice.png
Assets/Images/Placeholders/Defense/defense_guard_room.png
Assets/Images/Placeholders/Defense/defense_venom_spike.png
Assets/Images/Placeholders/Defense/defense_alarm_coil.png
Assets/Images/Placeholders/Defense/defense_barracks.png
Assets/Images/Placeholders/Defense/defense_corrosion_freezer.png
Assets/Images/Placeholders/Defense/defense_storm_fire.png
Assets/Images/Placeholders/Defense/defense_war_barracks.png
```

## 재고 아이템

```text
Assets/Images/Placeholders/Items/item_food.png
Assets/Images/Placeholders/Items/item_general.png
Assets/Images/Placeholders/Items/item_weapon.png
Assets/Images/Placeholders/Items/item_mana.png
```

## 캐릭터

```text
Assets/Images/Placeholders/Characters/character_slime.png
Assets/Images/Placeholders/Characters/character_orc.png
Assets/Images/Placeholders/Characters/character_vampire.png
Assets/Images/Placeholders/Characters/character_owner.png
Assets/Images/Placeholders/Characters/character_intruder.png
```

## UI 상태

```text
Assets/Images/Placeholders/UI/ui_happy.png
Assets/Images/Placeholders/UI/ui_angry.png
Assets/Images/Placeholders/UI/ui_confused.png
Assets/Images/Placeholders/UI/ui_tired.png
Assets/Images/Placeholders/UI/ui_stock_empty.png
Assets/Images/Placeholders/UI/ui_work.png
```

## 사용 원칙

- `BuildingSO.sprite`와 `BuildingSO.icon`에는 같은 플레이스홀더를 먼저 연결한다.
- 합성 결과 시설은 원본 시설 이미지를 그대로 재사용하지 않고, 결과 시설 이름이 보이는 별도 플레이스홀더를 우선 연결한다.
- `SaleItem.itemSprite`는 상품별 실제 그림이 아니라 재고 카테고리 placeholder를 먼저 연결한다.
- 캐릭터 SO에는 종족별 플레이스홀더를 먼저 연결한다.
- UI 이모티콘 풍선은 `UI` 폴더의 플레이스홀더로 시작한다.
- 새 P1 에셋이 필요하면 같은 규칙으로 파일명만 추가한다.
