#!/usr/bin/env python3
"""Author and synchronize Korean item descriptions without touching Unity C#.

The JSON file written by this tool is the reviewable prose authority.  The
Unity ScriptableObject remains the runtime copy consumed by the game, and the
wiki verifies that both copies agree exactly.
"""

from __future__ import annotations

import argparse
import ast
import csv
import hashlib
import json
import re
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = ROOT / "Assets/Resources/SO/InGameNarrativeTextCatalog.asset"
AUTHORITY_PATH = ROOT / "docs/game-design/content/item-in-game-descriptions.ko.json"
ITEM_CSV_PATHS = (
    ROOT / "docs_final/content-db/csv/items/generic-item.csv",
    ROOT / "docs_final/content-db/csv/items/resource-item.csv",
)
BUILDING_CSV_PATH = ROOT / "docs_final/content-db/csv/production-facilities/building.csv"
WEAPON_CSV_PATH = ROOT / "docs_final/content-db/csv/combat-health-world/combat-weapon.csv"
ARMOR_CSV_PATH = ROOT / "docs_final/content-db/csv/combat-health-world/combat-armor.csv"
SHIELD_CSV_PATH = ROOT / "docs_final/content-db/csv/combat-health-world/combat-shield.csv"
WILDLIFE_CSV_PATH = ROOT / "docs_final/content-db/csv/combat-health-world/wildlife-species.csv"

ENTRY_PATTERN = re.compile(
    r"(?ms)^  - kind: (?P<kind>\d+)\n"
    r"    stableId: (?P<stable_id>.*?)\n"
    r"    inGameDescription: (?P<description>.*?)"
    r"(?=\n    worldBranchTag:)"
)

BANNED_FRAGMENTS = (
    "완성된 시설을 원하는 자리에 설치하는 건설 키트",
    "시설을 건설하기 위한 실제 부품과 조립 자재 묶음",
    "시설 개조와 장비 조율에 사용하는 진화 촉매",
    "건설, 제작, 정비와 거래에 쓰는 일반 물자",
    "여러 단계의 생산을 거쳐 만든다",
    "주민이 일상, 작업 또는 의례에 맞춰 착용하는 의복",
    "전투에서 적을 공격하는 무기",
    "전투에서 신체를 보호하는 방어구",
    "공격을 막고 전열을 지키는 방패",
    "원정과 사건에서 얻어 연구, 거래 또는 협약의 증거로 쓰는 유물",
    "주민이 작업할 때 휴대해 속도와 안전을 높이는 도구",
    "물리 인스턴스",
    "작업실의 다음 공정으로 실제 운반되는 물리 중간재",
    "등급·상태 밴드로만 병합되는 V22 원섬유",
    "분기형 생산망의",
    "작업실의 다음 공정으로 운반되는 물리 중간재",
    "Ready/Wet/Contaminated",
)

GENERIC_SOURCE_DESCRIPTIONS = {
    "시설을 건설하기 위한 실제 부품과 조립 자재 묶음.",
    "시설 개조와 장비 조율에 사용하는 진화 촉매.",
    "작업실의 다음 공정으로 실제 운반되는 물리 중간재.",
    "촉매를 분해해 얻은 잔재. 정제하거나 다음 진행 단계로 합칠 수 있다.",
    "도축 시설로 옮기면 식량과 부산물을 얻습니다.",
    "등급·상태 밴드로만 병합되는 V22 원섬유.",
    "수술로 설치하는 고유 보철 부품",
    "기증자와 신선도가 보존되는 고유 수술 장기",
}


@dataclass(frozen=True)
class ItemRow:
    stable_id: str
    title: str
    source_description: str
    source_path: Path


@dataclass(frozen=True)
class LoreLink:
    anchor_id: str
    connection: str
    story_layer: str
    sentence: str


LORE_ANCHOR_TOKENS = {
    "place:harnak": ("하르나크",),
    "place:mnesila": ("므네실라",),
    "place:krik-seventh": ("크릭 제7굴",),
    "place:ailasera": ("아일라세라",),
    "place:fourth-foundry": ("제4주조도시",),
    "place:versadion": ("베르사디온",),
    "place:rakash-crossroads": ("라카쉬 세갈래길",),
    "state:ordena": ("오르데나",),
    "route:milosia": ("밀로세아 가도",),
    "range:norkandra": ("노르칸드라",),
    "sea:sarvenia": ("사르베니아 내해",),
    "practice:orc-cauldron": ("큰솥의 몫",),
    "practice:orc-vigil": ("무기 철야",),
    "practice:vampire-consent": ("동의의 잔",),
    "practice:slime-water": ("맑은물 합류",),
    "practice:myconid-mist": ("공유 안개",),
    "practice:kobold-tool-name": ("첫 도구 이름짓기",),
    "practice:harpy-chorus": ("새벽 합창",),
    "practice:golem-memory": ("기억판 안치",),
    "practice:demon-embers": ("맹세의 잿불",),
    "practice:beastkin-meal": ("무리 한솥",),
    "practice:adventurer-table": ("귀환자의 식탁",),
}

EARLY_REVEAL_TERMS = (
    "첫 협약",
    "귀환 의식",
    "원래 몸",
    "시민 신체",
    "인간형 등록",
    "인간의 몸을 시민권",
)

REPORTING_STYLE_TERMS = ("장부", "기록", "적는다", "책임", "소유권")
MAX_REPORTING_STYLE_TERM_COUNT = 40
MAX_AVERAGE_DESCRIPTION_CHARS = 105
MAX_DESCRIPTION_CHARS = 180


APPAREL_COPY = {
    "apron": "옷자락 앞을 덮어 음식물과 작업 찌꺼기가 안쪽 옷에 닿는 것을 줄인다.",
    "belt": "허리를 조이고 작은 도구나 주머니를 걸 수 있는 튼튼한 띠다.",
    "blouse": "목과 손목에 여유를 둔 가벼운 윗옷으로, 일상복 안팎에 겹쳐 입는다.",
    "boots": "진흙과 거친 바닥을 밟을 때 발목까지 감싸는 장화다.",
    "ceremonial-dress": "공동체의 큰 의식에서 입는 긴 드레스로, 움직임보다 격식을 앞세운 재단이다.",
    "chest-wrap": "가슴을 단단히 감싸되 팔과 어깨의 움직임은 남겨 둔 천옷이다.",
    "cloak": "어깨에서 등까지 넓게 덮어 바람과 먼지를 막는 겉옷이다.",
    "contract-sash": "계약 당사자의 소속과 책임을 드러내도록 어깨에서 허리로 두르는 띠다.",
    "daily-robe": "허리끈 하나로 여미는 넉넉한 로브로, 정착지 안에서 오래 입기 편하다.",
    "envoy-coat": "낯선 세력 앞에서도 소속을 알아볼 수 있게 깃과 소매를 반듯하게 세운 외투다.",
    "farmer-workwear": "흙이 잘 털리고 무릎을 굽히기 편하도록 품을 넉넉히 잡은 농사옷이다.",
    "festival-vest": "잔칫날 평상복 위에 걸쳐 색과 문양을 보태는 짧은 조끼다.",
    "footwraps": "발바닥과 발목을 천으로 감싸 신발 안의 쓸림을 줄이는 기본 의복이다.",
    "formal-coat": "회의와 접견에 입도록 어깨선과 앞섶을 단정하게 잡은 정장 외투다.",
    "gloves": "손가락과 손바닥을 덮어 거친 재료를 다룰 때 피부를 보호한다.",
    "golem-functional-lining": "골렘의 관절 틈과 핵 주변을 감싸도록 잘라 낸 교체용 내피다.",
    "hat": "햇빛과 떨어지는 먼지를 가리도록 머리 위에 얹는 챙 있는 모자다.",
    "heat-work-suit": "불꽃과 뜨거운 설비 가까이에서 입도록 두꺼운 겉감과 닫힌 여밈을 쓴 작업복이다.",
    "hooded-robe": "머리까지 한 벌로 가릴 수 있어 이동 중 비와 시선을 피하기 좋은 로브다.",
    "horn-ring": "뿔 둘레에 끼워 장식하는 고리로, 머리와 귀를 덮지 않는다.",
    "keeper-coat": "사료 주머니와 작은 돌봄 도구를 꺼내기 쉽도록 주머니를 넉넉히 단 외투다.",
    "loincloth-underwear": "허리와 샅을 간단히 감싸며 꼬리나 특수 관절의 움직임을 방해하지 않는 속옷이다.",
    "long-underpants": "허리부터 발목까지 덮어 겉바지 안쪽의 마찰과 냉기를 줄이는 내의다.",
    "lower-underwear": "하체에 밀착해 겉옷 안에서 피부와 옷감이 직접 쓸리는 것을 막는다.",
    "miner-workwear": "좁은 갱도에서 걸리지 않도록 자락을 줄이고 팔꿈치와 무릎을 덧댄 작업복이다.",
    "mourning-clothes": "장례와 추도 기간에 고인을 기억하는 표식을 달아 입는 차분한 옷이다.",
    "raincoat": "빗물이 스며들지 않도록 겉면을 처리하고 목과 소매를 좁게 여민 외투다.",
    "ritual-robe": "의식을 집전할 때 몸짓과 표식이 잘 보이도록 넓은 소매를 단 로브다.",
    "scarf": "목과 입가를 감싸 추위와 먼지를 막는 긴 천이다.",
    "shorts": "무릎 위에서 끝나 더운 작업장과 빠른 이동에 부담이 적은 바지다.",
    "skirt": "허리에서 아래로 넓게 퍼져 다리의 움직임을 막지 않는 일상복이다.",
    "sky-chorus-shawl": "하피의 합창에서 날개 뿌리를 가리지 않도록 어깨에만 걸치는 얇은 숄이다.",
    "sleep-bottom": "침상에서 몸을 조이지 않도록 허리와 밑단을 느슨하게 만든 잠옷 하의다.",
    "sleep-top": "누웠을 때 솔기와 단추가 몸을 누르지 않도록 단순하게 지은 잠옷 상의다.",
    "smith-apron": "불티와 금속 가루를 받아 내도록 가슴부터 무릎까지 질긴 천으로 덮는다.",
    "smoke-protection-hood": "코와 입 앞에 여과층을 두어 연기가 짙은 작업장에서 쓰는 닫힌 두건이다.",
    "socks": "발을 감싸 땀을 흡수하고 신발 안쪽의 쓸림을 줄이는 얇은 의복이다.",
    "spore-garden-cloak": "균사정원에서 포자를 흩뜨리지 않도록 표면을 매끈하게 마감한 망토다.",
    "spore-protection-hood": "얼굴 둘레를 조이고 숨구멍에 여과천을 덧대 포자 먼지를 거르는 두건이다.",
    "sterile-gown": "치료실의 오염을 옮기지 않도록 세척한 뒤 봉해 보관하는 긴 가운이다.",
    "surgical-apron": "수술 중 튀는 피와 세정액을 받아 내며 사용 뒤 바로 씻을 수 있는 앞치마다.",
    "tail-guard": "꼬리의 관절을 따라 감싸 충돌과 쓸림을 줄이되 굽힘은 남겨 둔 보호대다.",
    "tail-ribbon": "꼬리 끝이나 중간에 묶어 소속과 기분을 드러내는 가벼운 장식끈이다.",
    "trousers": "허리에서 발목까지 다리를 따로 감싸는 튼튼한 일상 바지다.",
    "tunic": "머리로 뒤집어쓰고 허리에서 묶는 단순한 윗옷으로, 수선하기 쉽다.",
    "undershirt": "겉옷 아래에서 땀을 받아 내도록 몸에 가깝게 입는 얇은 셔츠다.",
    "vest": "팔을 드러낸 채 몸통만 덮어 다른 옷 위에 겹쳐 입기 좋은 조끼다.",
    "waterproof-work-suit": "젖은 농장과 배관실에서 물이 스며들지 않도록 이음새를 막은 작업복이다.",
    "weapon-vigil-cloak": "오크의 무기 철야 동안 등에 걸치며, 무기를 쥔 팔은 자유롭게 남기는 망토다.",
    "wing-cloak": "접힌 날개를 덮고 펼칠 때 양옆으로 갈라지도록 뒤판을 나눈 망토다.",
    "wing-harness": "날개 뿌리 사이로 끈을 돌려 짐과 장식을 몸통에 고정하는 멜빵이다.",
    "work-shirt": "소매를 걷어 고정할 수 있고 잦은 세탁과 수선을 견디는 작업용 셔츠다.",
}

WEAPON_COPY = {
    "arquebus": "받침 없이 운용할 수 있게 줄인 화승식 장총으로, 탄종을 바꿔 여러 상황에 대응한다.",
    "blacksteel-poleaxe": "흑강 날과 망치면을 한 자루에 단 장병기로, 갑옷 틈과 단단한 표면을 함께 노린다.",
    "composite-bow": "서로 다른 탄성 재료를 겹쳐 짧은 활몸에서도 강한 장력을 내는 활이다.",
    "crossbow": "시위가 걸린 상태로 조준할 수 있어 숙련이 낮아도 일정한 사격을 내는 쇠뇌다.",
    "dagger": "좁은 통로와 몸싸움에서 빠르게 찌를 수 있는 한손 단검이다.",
    "estoc": "두꺼운 갑옷의 틈을 찌르도록 가늘고 단단하게 벼린 장검이다.",
    "falchion": "앞쪽에 무게가 실린 넓은 날로 깊은 베기를 내는 한손 도검이다.",
    "greatsword": "넓은 공간에서 양손으로 휘둘러 전열을 밀어내는 대형 도검이다.",
    "halberd": "도끼날과 갈고리, 창끝을 한 자루에 묶어 전열을 붙잡고 찌르는 장병기다.",
    "handgonne": "짧은 금속 총신에 화약을 다져 넣고 손으로 받쳐 쏘는 초기 화기다.",
    "heavy-matchlock": "무거운 총신과 큰 화약량으로 단단한 표적을 노리는 화승총이다.",
    "javelin": "달려드는 적에게 던지거나 가까이서 찌를 수 있게 가볍게 만든 창이다.",
    "longbow": "긴 활몸의 장력을 온몸으로 당겨 먼 거리까지 화살을 보내는 양손 활이다.",
    "longsword": "베기와 찌르기를 모두 다룰 수 있도록 균형을 잡은 양손 장검이다.",
    "mace": "날 대신 무거운 머리로 충격을 집중해 갑옷 위를 두드리는 한손 무기다.",
    "mana-lance": "마나 도체를 창날까지 이어 찌르는 순간 힘을 모으는 장창이다.",
    "matchlock-long-gun": "긴 총신과 화승 장치로 먼 표적을 겨누는 보병용 화기다.",
    "matchlock-pistol": "좁은 곳에서 한 손으로 꺼내 쏠 수 있게 총신을 줄인 화승총이다.",
    "pollaxe": "도끼날과 망치머리를 번갈아 써 중장갑 상대를 압박하는 양손 무기다.",
    "powered-striking-gauntlet": "관절 구동부가 주먹의 순간 충격을 키우는 근접 전투용 장갑이다.",
    "repeating-crossbow": "탄창과 재장전 장치를 붙여 짧은 간격으로 볼트를 이어 쏘는 쇠뇌다.",
    "rune-blade": "칼몸에 새긴 룬 도체가 마나 흐름을 붙잡는 양손 도검이다.",
    "rune-bow": "활몸의 룬이 당겨진 시위의 힘을 고르게 받쳐 주는 비전 활이다.",
    "shortbow": "좁은 통로에서도 빠르게 당겨 쏠 수 있는 가벼운 활이다.",
    "shotgun": "여러 발의 산탄을 한 번에 퍼뜨려 가까운 적 무리를 겨누는 화기다.",
    "siege-arbalest": "성벽과 대형 표적을 꿰뚫도록 굵은 시위와 받침을 쓴 공성 쇠뇌다.",
    "sniper-arquebus": "긴 총신과 정밀 조준부로 먼 표적 하나를 겨누는 아쿼버스다.",
    "spear": "적과 거리를 둔 채 찌르고 전열을 세우는 기본 장창이다.",
    "throwing-axe": "회전하며 날이 먼저 닿도록 무게를 맞춘 짧은 투척도끼다.",
    "warhammer": "좁은 타격면에 힘을 모아 판금과 뼈에 충격을 전하는 전투망치다.",
    "windlass-crossbow": "권양기로 무거운 시위를 감아 올려 강한 볼트를 쏘는 쇠뇌다.",
}

ARMOR_COPY = {
    "articulated-plate": "겹친 판금을 관절마다 이어 몸통과 팔의 움직임을 함께 살린 중갑이다.",
    "blacksteel-carapace": "흑강 판을 갑각처럼 포개 전신의 빈틈을 줄인 무거운 갑옷이다.",
    "blast-coat": "두꺼운 내피와 겉판으로 폭발 파편과 충격을 받아 내는 외투형 갑옷이다.",
    "breastplate": "가슴과 배의 급소를 한 장의 철판으로 덮는 몸통 갑옷이다.",
    "brigandine": "천이나 가죽 안쪽에 작은 금속판을 촘촘히 박아 유연함을 남긴 갑옷이다.",
    "closed-plate-helm": "얼굴까지 판금으로 닫고 좁은 시야 틈만 남긴 중투구다.",
    "cloth-hood": "머리와 목을 가볍게 감싸는 천 두건으로, 다른 장비와 겹쳐 쓰기 쉽다.",
    "gambeson": "여러 겹의 천을 누벼 충격과 쓸림을 흡수하는 몸통 갑옷이다.",
    "hardened-leather-coat": "단단하게 삶은 가죽 조각을 외투 모양으로 이어 몸통과 팔을 덮는다.",
    "iron-helmet": "머리 꼭대기와 관자놀이를 철판으로 감싼 기본 전투 투구다.",
    "jack-of-plates": "옷감 사이에 작은 판금을 넣어 겉으로 드러나지 않게 방호력을 보탠 갑옷이다.",
    "leather": "두꺼운 가죽을 겹치고 묶어 몸통의 움직임을 해치지 않는 경갑이다.",
    "leather-cap": "정수리와 이마를 가죽으로 덮어 가벼운 충격을 받아 내는 모자다.",
    "mail-coif": "작은 금속 고리를 이어 머리와 목 둘레에 늘어뜨린 사슬 두건이다.",
    "mail-shirt": "수많은 금속 고리를 맞물려 몸통과 어깨를 유연하게 덮는 사슬갑옷이다.",
    "padded-hood": "천과 충전재를 여러 겹 누벼 머리와 목의 충격을 줄이는 두건이다.",
    "powder-cuirass": "화약수 부대의 파편 방호 규격에 맞춰 가슴판과 목깃을 보강한 흉갑이다.",
    "powered-harness": "동력 관절이 무거운 판금을 움직이도록 팔다리와 몸통을 잇는 보조 갑주다.",
    "rune-ward-mail": "사슬 고리마다 수호 룬을 이어 물리 공격과 마나 충격을 함께 받는 갑옷이다.",
    "scale-coat": "작은 비늘판을 아래로 겹쳐 베기와 찌르기를 흘려보내는 외투형 갑옷이다.",
    "smoke-hood": "연기 속에서 얼굴과 호흡기를 가리도록 여과층을 넣은 전투 두건이다.",
}

SHIELD_COPY = {
    "blacksteel": "흑강 판을 겹쳐 강한 충격에도 형태를 잃지 않게 만든 중방패다.",
    "buckler": "주먹 앞에 붙여 공격을 쳐내고 곧바로 반격하기 좋은 작은 방패다.",
    "iron": "나무 심재에 철판을 덧대 무게와 방호의 균형을 잡은 보병 방패다.",
    "mana-buckler": "작은 판 안쪽의 마나 도체로 순간적인 충격을 흘리는 버클러다.",
    "pavise": "땅에 세워 사수와 장전수를 가릴 수 있는 넓고 긴 방패다.",
    "powered": "구동 장치가 방패면을 밀어 주어 큰 충격을 버티게 하는 동력 방패다.",
    "rune": "방패면에 수호 룬을 새겨 마나가 흐르는 공격까지 받아 내는 방패다.",
    "tower": "몸 대부분을 가리고 좁은 통로에 전열을 세우는 대형 방패다.",
    "wood": "여러 장의 판자를 결 방향이 엇갈리게 묶은 가벼운 기본 방패다.",
}

FOOD_COPY = {
    "boar-stew": "멧돼지 고기와 뿌리채소를 오래 끓여 국물까지 든든하게 만든 고급식이다.",
    "cheese-mushroom": "동굴버섯에 녹인 치즈를 곁들여 향과 포만감을 살린 채식 요리다.",
    "egg-pancake": "알과 곡물 반죽을 넓게 부쳐 빠르게 나눠 먹기 좋은 채식식이다.",
    "expedition-ration-pack": "걷는 중에도 꺼내 먹을 수 있도록 보존식과 작은 식기를 한 끼씩 묶은 원정 식량이다.",
    "fermented-pickle": "채소를 소금물에 삭혀 오래 두고 먹을 수 있게 만든 새콤한 절임이다.",
    "fresh-curd": "갓 굳힌 응유를 부드러운 상태로 담아 내는 신선한 유제품 요리다.",
    "garden-meal": "곡물과 뿌리, 버섯을 함께 조리해 육류 없이도 한 끼를 채우는 정원 요리다.",
    "grain-porridge": "황혼곡을 물에 오래 끓여 재료가 부족한 날에도 속 편하게 먹는 묽은 죽이다.",
    "grape-syrup": "밤포도 즙을 졸여 단맛과 향을 농축한 시럽으로, 음료와 후식에 곁들인다.",
    "jerky": "고기를 얇게 저며 말리고 간을 해 원정 중에도 오래 보관할 수 있게 만든 육포다.",
    "lavish-meat": "고기와 유제품, 과일을 아끼지 않고 차려 손님과 주민을 대접하는 호화식이다.",
    "lavish-vegan": "서로 다른 세 가지 식물 재료를 여러 조리법으로 차려 낸 비건 만찬이다.",
    "malt-porridge": "불린 맥아를 부드럽게 끓여 단맛을 끌어낸 따뜻한 죽이다.",
    "meat-pie": "다진 고기와 육즙을 밀가루 반죽 안에 봉해 구운 든든한 파이다.",
    "mushroom-soup": "동굴버섯을 푹 끓여 진한 향과 단백질을 국물에 우려낸 비건식이다.",
    "night-spirit": "밤포도를 발효한 뒤 다시 증류해 향은 남기고 도수를 높인 술이다.",
    "preserved-ration": "전분과 소금으로 수분을 줄여 먼 원정에도 버티도록 만든 단단한 배급식이다.",
    "preserved-vegetable": "채소를 씻고 절여 수확철이 지나도 꺼내 먹을 수 있게 저장한 채식이다.",
    "roasted-meat": "손질한 고기를 불에 바로 구워 빠르게 내는 단순한 육식 요리다.",
    "root-stew": "잿불뿌리를 큼직하게 썰어 푹 끓인 열량 높은 비건 스튜다.",
    "salted-meat-stew": "염장육의 소금기를 국물에 풀고 채소와 함께 끓인 보존식 스튜다.",
    "stuffed-mushroom": "큰 버섯갓 안에 곡물과 양념한 속재료를 채워 구운 채식 요리다.",
    "twilight-beer": "황혼곡 맥아를 발효해 일과 뒤에 가볍게 나눠 마시는 맥주다.",
    "vegetable-pie": "손질한 채소와 양념 속을 반죽으로 감싸 한 조각씩 나누기 좋은 파이다.",
}

FEED_COPY = {
    "dog-food": "곡물에 동물성 부산물을 섞어 육식과 잡식 동물이 함께 먹도록 만든 사료다.",
    "dog-food-fresh": "생고기와 곡물을 바로 섞어 보존성보다 신선한 냄새와 식감을 살린 사료다.",
    "hay": "풀을 충분히 말려 초식동물이 사계절 먹을 수 있게 쌓아 둔 기본 사료다.",
    "silage": "풀과 잎을 눌러 담아 발효시켜 건초가 부족한 계절에 꺼내는 사료다.",
}

AMMO_COPY = {
    "arrow": "활에 메겨 쏘는 기본 화살로, 발사한 뒤에는 회수할 수 없다.",
    "bolt": "석궁의 짧고 강한 시위에 맞춘 굵고 무거운 기본 볼트다.",
    "armor-piercing-cartridge": "단단한 탄자를 넣어 판금과 두꺼운 방호구를 뚫는 데 맞춘 철갑 탄약이다.",
    "arrow-bone": "뼈를 깎은 가벼운 촉을 달아 값은 낮췄지만 관통력도 낮은 화살이다.",
    "arrow-iron": "철촉의 무게와 날각을 표준에 맞춰 피해와 관통의 균형을 잡은 화살이다.",
    "arrow-rune": "뿔로 만든 촉과 마나 각인을 결합해 비전 표적을 겨누는 화살이다.",
    "arrow-steel": "강철촉을 단단히 고정해 중장갑의 틈을 노리는 고관통 화살이다.",
    "blacksteel-bolt": "흑강 촉의 무게를 굵은 자루가 받쳐 단단한 표적을 꿰뚫는 석궁 볼트다.",
    "blasting-charge": "암반과 구조물을 깨뜨리도록 화약을 한 번의 폭발량으로 밀봉한 장약이다.",
    "bolt-bone": "가벼운 뼈촉을 달아 석궁 장전과 사격을 익힐 때 쓰는 연습용 볼트다.",
    "bolt-iron": "철촉과 짧은 자루의 무게를 맞춘 표준 석궁 볼트다.",
    "bolt-rune": "촉에 새긴 룬이 비전 방호를 흐트러뜨리도록 조율한 석궁 볼트다.",
    "bolt-steel": "강철촉과 굵은 자루로 중장갑을 겨누는 고관통 석궁 볼트다.",
    "incendiary-arrow": "촉 뒤의 소이제를 충돌과 함께 터뜨려 불을 옮기는 화살이다.",
    "incendiary-bolt": "석궁의 힘으로 소이제를 표적 깊숙이 박아 넣는 점화 볼트다.",
    "mana-disruptor-bolt": "각인된 촉이 맞은 자리의 마나 흐름을 흔들도록 만든 특수 볼트다.",
    "paper-cartridge": "화약과 탄자를 종이 한 봉에 담아 화승총의 재장전을 줄이는 탄약통이다.",
    "rune-cartridge": "탄자와 화약 사이에 룬 매개재를 넣어 비전 반응을 일으키는 탄약통이다.",
    "scatter-cartridge": "작은 탄자를 여러 개 넣어 가까운 범위에 퍼뜨리는 산탄 탄약이다.",
    "signal-flare": "밝은 불빛과 연기를 높이 올려 먼 경비대와 원정대에 위치를 알린다.",
    "smoke-cartridge": "맞은 자리에 짙은 연막을 펼쳐 시야와 사선을 끊는 탄약이다.",
    "tranquilizer-dart": "약물을 넣은 가는 침으로 야생동물과 포획 대상을 진정시키는 다트다.",
    "trap-canister": "함정 장치 안에 끼워 접근한 적에게 산탄을 퍼붓는 밀폐 탄통이다.",
}

SPECIAL_ITEM_COPY = {
    "book:seasonal-almanac": "파종과 수확, 우기와 한기의 시작을 해마다 적어 둔 계절력 책자다.",
    "craft:dreamweave-ritual-banner": "몽직물에 의식 문양을 수놓아 멀리서도 행렬과 집전 위치를 알아보게 한 장식 깃발이다.",
    "craft:fermented-vinegar": "발효액을 더 시게 익혀 음식의 간과 저장, 세정에 나눠 쓰는 식초다.",
    "craft:toxic-trap-coating": "함정의 날과 촉에 얇게 발라 상처로 독이 스며들게 하는 도포제다.",
    "item:equipment-module": "무기와 방어구의 기존 결합부에 끼워 성능 조율을 바꾸는 규격화된 개량 부품이다.",
    "item:lineage-seal": "가계와 상속 기록이 어느 계통에서 이어졌는지 확인하도록 문서에 찍는 계보 인장이다.",
    "material:chain-mesh": "작은 금속 고리를 촘촘히 이어 방어구의 유연한 덮개로 쓰는 사슬 망이다.",
    "material:ember-cotton": "잿불 지대에서 거둔 목화를 실로 짜 내열 작업복과 두꺼운 안감에 쓰는 면직물이다.",
    "material:plate-blank": "두께와 넓이를 맞춰 잘라 갑옷판과 기계 외피로 가공하기 전의 판금 소재다.",
    "material:sterile-composite": "세척한 섬유와 결합재를 오염 없이 굳혀 수술 도구와 보철 외피에 쓰는 복합재다.",
    "medical:cross-lineage-medium": "서로 다른 종족의 조직이 맞닿을 때 생기는 거부 반응을 낮추도록 조율한 안정화 배지다.",
    "record:arcane-index": "룬과 주문을 효과, 재료와 위험별로 찾아볼 수 있게 정리한 비전 색인철이다.",
    "record:breeding-ledger": "가축의 짝짓기, 출산과 형질을 세대별로 기록해 다음 번식을 계획하는 장부다.",
    "record:career-ledger": "주민의 업무 경력과 숙련 변화를 적어 배치와 교육에 참고하는 장부다.",
    "textile:quilted-liner": "얇은 천 사이에 충전재를 고르게 넣고 누벼 의복과 방어구 안쪽에 덧대는 내피다.",
}

CATALYST_FAMILY = {
    "arcane": ("비전", "청람 결정", "마나 결을 조율하는"),
    "authority": ("권위", "적동 분말", "명령 인장을 새기는"),
    "defense": ("수호", "층상 광물", "충격을 여러 겹으로 흘리는"),
    "industry": ("산업", "검은 소결제", "열과 압력에 반응하는"),
    "offense": ("공세", "붉은 조율재", "날과 발사 장치를 맞추는"),
    "survival": ("생존", "녹빛 촉매", "습기와 부패에 견디게 하는"),
    "universal": ("범용", "무색 결정", "서로 다른 개조 계통을 잇는"),
}

CATALYST_STAGES = (
    "거친 알갱이만 골라 첫 조율에 맞춘 {material}이다",
    "불순물을 걷어 내고 흐름을 한쪽으로 모은 {material}이다",
    "결정면을 고르게 다듬어 반응의 편차를 줄인 {material}이다",
    "짧은 각인을 받아도 갈라지지 않도록 굳힌 {material}이다",
    "첫 등급에서 쓸 수 있는 밀도에 도달한 {material}이다",
    "두 번째 등급을 위해 입자 사이의 틈을 다시 메운 {material}이다",
    "서로 다른 조각의 결을 한 방향으로 정렬한 {material}이다",
    "열과 마나를 번갈아 가해 내부 응력을 푼 {material}이다",
    "조율 흔적이 표면에서 중심까지 이어진 {material}이다",
    "세 번째 등급의 개조에 맞도록 반응을 안정시킨 {material}이다",
    "긴 각인을 되돌림 없이 버티도록 단단히 굳힌 {material}이다",
    "균열이 생기는 지점을 찾아 새 결로 덧댄 {material}이다",
    "같은 계통의 여러 장치에 쓸 수 있도록 편차를 좁힌 {material}이다",
    "네 번째 등급에 맞춰 중심부까지 정제한 {material}이다",
    "미세한 불순물과 오래된 각인 흔적을 걷어 낸 {material}이다",
    "연속 조율에도 반응이 흐트러지지 않도록 만든 {material}이다",
    "고등 개조에서 요구하는 균일한 결을 갖춘 {material}이다",
    "다섯 번째 등급의 첫 각인을 받을 준비를 마친 {material}이다",
    "결정 전체가 하나의 흐름으로 반응하도록 묶은 {material}이다",
    "마지막 조율 전에도 형태와 반응이 안정된 {material}이다",
    "해당 계통이 요구하는 최종 진행 상태까지 정제한 {material}이다",
)


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def decode_unity_scalar(raw: str) -> str:
    compact = re.sub(r"\n\s+", " ", raw.strip())
    value = ast.literal_eval(compact)
    if not isinstance(value, str):
        raise ValueError("Unity scalar is not a string")
    return re.sub(r"\s+", " ", value).strip()


def load_catalog(path: Path) -> tuple[str, dict[str, str]]:
    source = path.read_text(encoding="utf-8")
    items: dict[str, str] = {}
    for match in ENTRY_PATTERN.finditer(source):
        if match.group("kind") == "0":
            items[match.group("stable_id").strip()] = decode_unity_scalar(match.group("description"))
    return source, items


def load_items() -> dict[str, ItemRow]:
    items: dict[str, ItemRow] = {}
    for csv_path in ITEM_CSV_PATHS:
        for raw in read_csv(csv_path):
            stable_id = raw["stable_id"].strip()
            source_path = ROOT / raw["source_path"].strip()
            if stable_id in items:
                raise ValueError(f"duplicate item ID in source inventory: {stable_id}")
            if not source_path.is_file():
                raise ValueError(f"missing direct item asset: {source_path}")
            direct = source_path.read_text(encoding="utf-8")
            if not re.search(rf"(?m)^  itemId: ['\"]?{re.escape(stable_id)}['\"]?\s*$", direct):
                raise ValueError(f"source inventory does not match direct item asset: {stable_id}")
            items[stable_id] = ItemRow(
                stable_id=stable_id,
                title=raw["display_name"].strip(),
                source_description=raw["description"].strip(),
                source_path=source_path,
            )
    return items


def finish_sentence(text: str) -> str:
    text = re.sub(r"\s+", " ", text.strip()).replace("얻습니다", "얻을 수 있다")
    text = (
        text.replace("할 수 있습니다.", "할 수 있다.")
        .replace("버려야 합니다.", "버려야 한다.")
        .replace("좋습니다.", "좋다.")
        .replace("식량입니다.", "식량이다.")
        .replace("식재료입니다.", "식재료다.")
        .replace("음식입니다.", "음식이다.")
        .replace("부산물입니다.", "부산물이다.")
        .replace("응결물입니다.", "응결물이다.")
        .replace("결박구입니다.", "결박구다.")
    )
    if not text:
        return text
    if text.endswith("다.") or text.endswith("요.") or text.endswith("다"):
        return text if text.endswith(".") else text + "."
    if text.endswith("."):
        return text[:-1] + "이다."
    return text + "이다."


def trim_title_prefix(title: str, description: str) -> str:
    value = description.strip()
    for separator in (". ", ".", " "):
        prefix = title + separator
        if value.startswith(prefix):
            return value[len(prefix):].strip()
    return value


def source_copy(item: ItemRow) -> str:
    description = trim_title_prefix(item.title, item.source_description)
    description = description.replace("V22 ", "").replace("실제 ", "")
    description = description.replace("작성 자산 정의", "")
    return finish_sentence(description)


def extract_stat_sentence(item: ItemRow) -> str:
    source = item.source_path.read_text(encoding="utf-8")
    if "type: {class: FoodItemFeature," in source:
        nutrition = read_number(source, "nutrition")
        mood = read_number(source, "mood")
        if nutrition is not None and mood is not None:
            if mood > 0:
                mood_text = f"기분을 {mood:g} 올린다"
            elif mood < 0:
                mood_text = f"기분을 {abs(mood):g} 낮춘다"
            else:
                mood_text = "기분은 변하지 않는다"
            return f"먹으면 영양을 {nutrition:g}만큼 채우고 {mood_text}."
    if "type: {class: MedicineItemFeature," in source:
        treatment = read_number(source, "treatmentPotency")
        infection = read_number(source, "infectionReduction")
        detox = read_number(source, "detoxReduction")
        pain = read_number(source, "painReduction")
        if None not in (treatment, infection, detox, pain):
            return (
                f"치료 효율 {treatment:g}, 감염 감소 {infection:g}, "
                f"해독 {detox:g}, 통증 완화 {pain:g}이다."
            )
    return ""


def load_buildings() -> dict[str, tuple[str, set[str], str]]:
    result: dict[str, tuple[str, set[str], str]] = {}
    for row in read_csv(BUILDING_CSV_PATH):
        if not re.fullmatch(r"building:\d+", row["stable_id"]):
            continue
        path = ROOT / row["source_path"].strip()
        if not path.is_file():
            continue
        direct = path.read_text(encoding="utf-8")
        numeric_id = row["stable_id"].split(":", 1)[1]
        if not re.search(rf"(?m)^  id: {re.escape(numeric_id)}\s*$", direct):
            raise ValueError(f"building inventory does not match direct asset: {row['stable_id']}")
        abilities = set(re.findall(r"type: \{class: ([A-Za-z0-9_]+),", direct))
        proficiencies = re.findall(r"(?m)^    primaryProficiencyId: (.+?)\s*$", direct)
        operation = proficiencies[-1].strip() if proficiencies else ""
        result[row["stable_id"]] = (row["display_name"].strip(), abilities, operation)
    return result


def facility_copy(item: ItemRow, buildings: dict[str, tuple[str, set[str], str]]) -> str:
    number = item.stable_id.split(":", 1)[1]
    landmark = {
        "9201": "사라진 기록과 서로 모순되는 증거를 대조하는 진실 관측소의 설치 부품이다.",
        "9202": "여러 공동체가 한자리에 모여 새 협약을 논의할 대협약 회당의 설치 부품이다.",
        "9203": "지상 패권을 선포하고 군세의 이동을 통제하는 거대한 관문의 설치 부품이다.",
        "9204": "던전의 주권과 영토를 지키는 성채를 세울 때 쓰는 핵심 설치 부품이다.",
        "9205": "외부와 단절된 생태계를 오래 보존하는 봉인 생태정원의 설치 부품이다.",
        "9206": "가계와 계승의 기록을 영구히 보관하는 계보전의 설치 부품이다.",
        "9207": "시간의 흐름을 붙잡는 의식을 치르는 성소의 설치 부품이다.",
        "9208": "비전 연구의 마지막 단계에서 마나를 끌어올리는 승천탑의 설치 부품이다.",
        "9209": "강철로 만든 신격상을 세우고 기계 신앙을 공표할 때 쓰는 설치 부품이다.",
    }
    if number in landmark:
        return landmark[number]
    building = buildings.get(f"building:{number}")
    name = item.title.removesuffix(" 설치 키트")
    obj = with_object(name)
    topic = with_topic(name)
    instrument = with_instrument(name)
    if not building:
        return f"{obj} 현장에 세우기 위해 운반 단위로 묶어 둔 설치 부품이다."
    _, abilities, proficiency = building
    title_roles = (
        (("식당", "음식점"), f"{name}에서 주민과 손님에게 음식을 내고 값을 받을 수 있게 식탁과 배식 설비를 묶은 설치 부품이다."),
        (("식탁",), f"{name}에서 여러 주민과 손님이 둘러앉아 식사할 수 있게 만든 설치 부품이다."),
        (("상점", "판매", "시장", "거래"), f"{name}에서 상품을 보여 주고 손님과 값을 치를 수 있게 꾸린 설치 부품이다."),
        (("숙박", "객실", "여관"), f"{name}에서 손님이 짐을 두고 안전하게 쉴 수 있게 꾸린 설치 부품이다."),
        (("병동", "치료", "의료", "수술", "진단", "마취", "혈청", "백신", "약품", "격리"), f"{name}에서 환자를 살피고 필요한 처치를 이어 갈 수 있게 꾸린 설치 부품이다."),
        (("화덕", "조리", "주방", "배식", "제분", "훈연", "가마솥", "절임", "고기그릴"), f"{name}에서 식재료를 손질하고 조리해 끼니를 내는 데 필요한 설치 부품이다."),
        (("발전기",), f"{name}에서 시설망에 공급할 동력을 만들어 내는 설치 부품이다."),
        (("축전지",), f"{name}에 남는 동력을 모아 두었다가 부족할 때 내보내는 설치 부품이다."),
    )
    for tokens, text in title_roles:
        if any(token in name for token in tokens):
            return text
    role_rules = (
        ("BuildingButcherAbility", f"{name}에서 사체를 손질해 식량과 부산물을 나눌 때 쓰는 설치 부품이다."),
        ("BuildingCookingAbility", f"{name}에서 식재료를 익혀 끼니를 마련하는 데 필요한 설치 부품이다."),
        ("BuildingArcaneSurgeryAbility", f"{name}에서 비전 수술을 진행하도록 수술대와 연결 장치를 갖춘 설치 부품이다."),
        ("BuildingAnesthesiaAbility", f"{name}에서 마취를 유지하고 수술을 준비하는 데 필요한 설치 부품이다."),
        ("BuildingOrganStorageAbility", f"{name}에 적출 장기를 오염 없이 보관하도록 만든 설치 부품이다."),
        ("BuildingMedicalAbility", f"{name}에서 환자를 진단하고 치료할 수 있게 꾸린 설치 부품이다."),
        ("BuildingSterilizationAbility", f"{name}에서 의료 도구와 작업 공간을 소독하도록 만든 설치 부품이다."),
        ("BuildingResearchArchiveAbility", f"{name}에 연구 기록과 표본을 분류해 보관하도록 만든 설치 부품이다."),
        ("BuildingResearchCapacityAbility", f"{name}에서 자료와 표본을 대조해 연구를 진행하는 데 필요한 설치 부품이다."),
        ("BuildingCropPlotAbility", f"{name}에 씨앗을 심고 작물의 성장 상태를 돌보는 데 필요한 설치 부품이다."),
        ("BuildingBeastPenAbility", f"{name}에 가축을 들이고 먹이와 안전을 관리하도록 만든 설치 부품이다."),
        ("BuildingWaterProducerAbility", f"{name}에서 생활과 생산에 쓸 물을 확보하도록 만든 설치 부품이다."),
        ("BuildingWaterStorageAbility", f"{name}에 물을 모아 두고 필요한 곳으로 내보내는 설비의 설치 부품이다."),
        ("BuildingWastewaterProcessorAbility", f"{name}에서 오수를 걸러 다시 쓰거나 안전하게 배출하도록 만든 설치 부품이다."),
        ("BuildingVentilationAbility", f"{name}로 연기와 탁한 공기를 빼내는 환기 설비의 설치 부품이다."),
        ("BuildingAirDuctAbility", f"{obj} 이어 방 사이로 공기가 흐르게 하는 덕트 설치 부품이다."),
        ("BuildingPowerProducerAbility", f"{name}에서 시설망에 공급할 동력을 만들어 내는 데 필요한 설치 부품이다."),
        ("BuildingPowerStorageAbility", f"{name}에 남는 동력을 저장했다가 부족할 때 내보내도록 만든 설치 부품이다."),
        ("BuildingCircuitBreakerAbility", f"{name}에서 과부하가 번지기 전에 전력 구간을 끊도록 만든 설치 부품이다."),
        ("BuildingConveyorSegmentAbility", f"{obj} 따라 재료와 완성품을 다음 설비로 보내는 운반 장치의 설치 부품이다."),
        ("BuildingConveyorOverflowAbility", f"{name}에서 막힌 생산품을 다른 보관 경로로 빼내도록 만든 설치 부품이다."),
        ("BuildingStorageAbility", f"{name}에 재고를 모아 두고 반출 순서를 관리하도록 만든 설치 부품이다."),
        ("BuildingInternalStockAbility", f"{name} 안에 다음 작업에 쓸 재료를 잠시 보관하도록 만든 설치 부품이다."),
        ("BuildingDefenseAbility", f"{name}로 침입 경로를 막고 방어선을 유지하는 데 필요한 설치 부품이다."),
        ("BuildingSecurityAbility", f"{name}에서 출입과 위험 징후를 감시하도록 만든 설치 부품이다."),
        ("BuildingCoverAbility", f"{name} 뒤에 몸을 숨겨 날아오는 공격을 피하도록 세우는 설치 부품이다."),
        ("BuildingCaptiveHousingAbility", f"{name}에 포로를 수용하고 이동을 제한하도록 만든 설치 부품이다."),
        ("BuildingRehabilitationAbility", f"{name}에서 포로의 재사회화 절차를 진행하도록 꾸린 설치 부품이다."),
        ("BuildingMercenaryHiringAbility", f"{name}에서 용병의 조건을 확인하고 고용 계약을 맺도록 꾸린 설치 부품이다."),
        ("BuildingEquipmentCraftingAbility", f"{name}에서 무기와 방어구를 조립하고 마감하는 데 필요한 설치 부품이다."),
        ("BuildingEquipmentMaintenanceAbility", f"{name}에서 손상된 전투 장비를 점검하고 수리하도록 만든 설치 부품이다."),
        ("BuildingProstheticAssemblyAbility", f"{name}에서 신체 부위에 맞는 보철을 조립하도록 만든 설치 부품이다."),
        ("BuildingGolemRechargeAbility", f"{name}에서 골렘의 핵과 동력 장치를 충전하도록 만든 설치 부품이다."),
        ("BuildingRetailAbility", f"{name}에 상품을 진열하고 손님과 거래하도록 꾸린 설치 부품이다."),
        ("BuildingServiceSupportAbility", f"{name}에서 손님의 주문과 서비스 절차를 처리하도록 꾸린 설치 부품이다."),
        ("BuildingServiceAbility", f"{name}에서 손님에게 약속된 서비스를 제공하도록 꾸린 설치 부품이다."),
        ("BuildingCircusStageAbility", f"{name}에서 공연자가 관객 앞에 설 수 있도록 무대와 동선을 갖춘 설치 부품이다."),
        ("BuildingCircusTicketBoothAbility", f"{name}에서 관람료를 받고 입장 인원을 관리하도록 만든 설치 부품이다."),
        ("BuildingCircusGamblingAbility", f"{name}에서 내기와 판돈을 관리하도록 만든 설치 부품이다."),
        ("BuildingTrainingAbility", f"{name}에서 주민이 전투 동작과 장비 사용을 연습하도록 만든 설치 부품이다."),
        ("BuildingPreservationAbility", f"{name}에서 식량과 재료가 상하는 속도를 늦추도록 만든 설치 부품이다."),
        ("BuildingCleaningAbility", f"{name}로 오염과 쓰레기를 처리해 생활 공간을 깨끗이 유지하는 데 쓰는 설치 부품이다."),
        ("BuildingLightingAbility", f"{name}에서 빛을 내 통로와 작업 공간을 밝히도록 만든 설치 부품이다."),
        ("BuildingTemperatureAbility", f"{name}로 방 안의 온도를 조절하도록 만든 설치 부품이다."),
        ("BuildingThermalEmitterAbility", f"{name}에서 열을 내어 주변 공간을 덥히도록 만든 설치 부품이다."),
        ("BuildingNeedRecoveryAbility", f"{name}에서 주민이 쉬거나 기본 욕구를 해결하도록 꾸린 설치 부품이다."),
        ("BuildingSeatingAbility", f"{name}에 주민과 손님이 앉아 쉬거나 식사할 수 있도록 만든 설치 부품이다."),
        ("BuildingTableAbility", f"{name}에서 식사와 간단한 작업을 함께 처리하도록 만든 설치 부품이다."),
    )
    for ability, text in role_rules:
        if ability in abilities:
            return text
    if "BuildingProductionWorkstationAbility" in abilities:
        work = {
            "proficiency:food-production": "식재료를 가공하고 조리하는",
            "proficiency:medicine": "약품과 의료 재료를 만드는",
            "proficiency:crafting": "재료를 다듬어 부품과 완성품을 만드는",
            "proficiency:scholarship": "표본과 기록을 조사하는",
            "proficiency:fieldwork": "채집물과 현장 표본을 정리하는",
            "proficiency:melee-combat": "근접 전투 장비를 다루는",
            "proficiency:ranged-combat": "원거리 장비와 탄약을 다루는",
        }.get(proficiency, "생산 주문을 처리하는")
        return f"{name}에서 {work} 공정을 맡기 위해 준비한 설치 부품이다."
    structural = (
        ("복도", "주민과 운반물이 지나는 길을 놓을 때 쓰는 복도 설치 부품이다."),
        (" 문", f"방의 경계를 열고 닫아 출입을 조절하는 {name}의 설치 부품이다."),
        ("벽", f"{instrument} 방의 경계와 지지 구조를 세울 때 쓰는 설치 부품이다."),
        ("바닥", f"{obj} 깔아 통행과 시설 배치를 받칠 때 쓰는 설치 부품이다."),
        ("계단", f"{instrument} 높이가 다른 구역을 잇는 데 쓰는 설치 부품이다."),
        ("다리", f"{name}로 끊긴 지형을 건널 통로를 놓을 때 쓰는 설치 부품이다."),
    )
    for token, text in structural:
        if token in name or (token == " 문" and name == "문"):
            return text
    return f"{obj} 현장에 세워 공간의 용도와 작업 동선을 갖추는 데 쓰는 설치 부품이다."


def read_number(source: str, field: str) -> float | None:
    match = re.search(rf"(?m)^\s+{re.escape(field)}: (-?[\d.]+)\s*$", source)
    return float(match.group(1)) if match else None


def display_number(value: float | None) -> str:
    return "?" if value is None else f"{value:g}"


def display_average(value: float) -> str:
    return f"{value:.1f}".rstrip("0").rstrip(".")


def load_combat_sources() -> dict[str, str]:
    result: dict[str, str] = {}
    for path in (WEAPON_CSV_PATH, ARMOR_CSV_PATH, SHIELD_CSV_PATH):
        for row in read_csv(path):
            source_path = ROOT / row["source_path"].strip()
            direct = source_path.read_text(encoding="utf-8")
            if row["stable_id"] not in direct:
                raise ValueError(f"combat inventory does not match direct asset: {row['stable_id']}")
            result[row["stable_id"]] = direct
    return result


def equipment_copy(item: ItemRow, combat_sources: dict[str, str]) -> str:
    parts = item.stable_id.split(":")
    equipment_kind, slug = parts[1], parts[2]
    if equipment_kind == "weapon":
        return WEAPON_COPY[slug]
    if equipment_kind == "armor":
        return ARMOR_COPY[slug]
    return SHIELD_COPY[slug]


def apparel_copy(item: ItemRow) -> str:
    slug = item.stable_id.split(":", 1)[1]
    return APPAREL_COPY[slug]


def catalyst_copy(item: ItemRow) -> str:
    parts = item.stable_id.split(":")
    family = parts[2]
    stage = int(parts[3])
    family_name, material, purpose = CATALYST_FAMILY[family]
    stage_text = CATALYST_STAGES[stage - 1].format(material=material)
    tier = min(5, 1 + max(0, stage - 1) // 4) if stage > 5 else 1
    if stage >= 18:
        tier = 5
    elif stage >= 14:
        tier = 4
    elif stage >= 10:
        tier = 3
    elif stage >= 6:
        tier = 2
    return f"{stage_text}. {purpose} {family_name} 계통의 {tier}등급 개조에 쓴다."


def residue_copy(item: ItemRow) -> str:
    stage = int(item.stable_id.rsplit(":", 1)[1])
    states = (
        "거친 가루", "미세한 결정 부스러기", "빛이 엷게 남은 분말", "각인 자국이 남은 조각",
        "서로 달라붙기 시작한 입자", "두 겹으로 굳은 파편", "결이 한쪽으로 선 가루",
        "열을 머금은 잔조각", "마나 흔적이 이어진 분말", "단단히 뭉친 결정편",
        "각인선이 교차한 파편", "중심이 비어 있는 결정껍질", "반응이 고른 정제 가루",
        "짙은 빛을 띠는 결정편", "불순물이 거의 없는 분말", "표면이 매끈한 잔조각",
        "여러 결이 포개진 파편", "고등 각인의 흔적이 남은 가루", "중심까지 굳은 결정편",
        "반응이 오래 남는 잔조각", "고순도 분말",
    )
    return f"{stage}단계 촉매를 분해해 얻은 {states[stage - 1]}이다. 정제하거나 같은 진행대의 재료와 합칠 수 있다."


def has_final_consonant(text: str) -> bool:
    if not text:
        return False
    code = ord(text[-1])
    return 0xAC00 <= code <= 0xD7A3 and (code - 0xAC00) % 28 != 0


def with_topic(text: str) -> str:
    return text + ("은" if has_final_consonant(text) else "는")


def with_subject(text: str) -> str:
    return text + ("이" if has_final_consonant(text) else "가")


def with_instrument(text: str) -> str:
    if not text:
        return text
    code = ord(text[-1])
    final = (code - 0xAC00) % 28 if 0xAC00 <= code <= 0xD7A3 else 0
    return text + ("으로" if final not in (0, 8) else "로")


def with_object(text: str) -> str:
    return text + ("을" if has_final_consonant(text) else "를")


def stable_choice(stable_id: str, values: tuple[str, ...]) -> str:
    index = int(hashlib.sha256(stable_id.encode("utf-8")).hexdigest()[:8], 16) % len(values)
    return values[index]


def exact_lore_link(
    item: ItemRow,
    anchor_id: str,
    connection: str,
    sentence: str,
    story_layer: str = "everyday",
) -> LoreLink:
    if anchor_id not in LORE_ANCHOR_TOKENS:
        raise ValueError(f"unknown lore anchor: {anchor_id}")
    if not any(token in sentence for token in LORE_ANCHOR_TOKENS[anchor_id]):
        raise ValueError(f"lore sentence does not name its anchor: {item.stable_id} -> {anchor_id}")
    return LoreLink(anchor_id, connection, story_layer, sentence)


def make_lore_link(
    item: ItemRow,
    anchor_id: str,
    connection: str,
    story_layer: str = "everyday",
) -> LoreLink:
    name = item.title
    topic = with_topic(name)
    obj = with_object(name)
    instrument = with_instrument(name)

    if anchor_id == "place:harnak":
        variants = (
            f"하르나크 장터는 {obj} 여러 체형의 주민이 함께 쓰는 공용 재고로 관리한다.",
            f"오르데나의 봉쇄가 길어지면 하르나크는 {name} 재고부터 세어 버틸 날을 계산한다.",
            f"{topic} 하르나크 정착지가 여섯 도시와 물자를 나눌 때 쓰는 공용 규격을 따른다.",
        )
    elif anchor_id == "place:mnesila":
        if connection == "facility":
            variants = (
                f"므네실라의 약방과 공동 정원은 {name} 사용 기록을 한 장부에 남겨 습도와 오염을 함께 추적한다.",
                f"므네실라에서 들여온 {name} 규격은 포자와 약재가 섞이지 않도록 물길과 세척 순서를 먼저 잡는다.",
            )
        elif connection == "care":
            variants = (
                f"므네실라의 치료사는 {name} 포장에 만든 날과 배양 정원을 적어 감염 경로를 거슬러 찾는다.",
                f"하르나크의 약재상은 {obj} 므네실라에서 받을 때 습도와 채취 철을 함께 확인한다.",
            )
        else:
            variants = (
                f"므네실라에서는 {obj} 거둔 자리와 다음 포자철을 함께 기록한다.",
                f"{topic} 므네실라의 습림과 발효실을 오가며 약재와 끼니 양쪽에 쓰인다.",
            )
    elif anchor_id == "place:krik-seventh":
        if connection == "facility":
            variants = (
                f"크릭 제7굴의 코볼트는 {name} 조립판에 만든 이와 마지막으로 수리한 이의 표식을 나란히 남긴다.",
                f"{topic} 크릭 제7굴의 좁은 갱도에서도 부품을 갈아 끼울 수 있는 규격을 따른다.",
            )
        elif connection in {"ammunition", "military"}:
            variants = (
                f"크릭 제7굴은 {obj} 갱도 폭과 탄약함 규격에 맞춰 묶어 낸다.",
                f"{topic} 크릭 제7굴 경비대가 교대할 때 수량과 불발 기록을 함께 넘긴다.",
            )
        else:
            variants = (
                f"크릭 제7굴의 첫 도구 이름짓기 관습에 따라 {name}에도 제작자와 수리자의 표식이 남는다.",
                f"크릭 제7굴에서는 {obj} 씨족 작업대의 재산으로 세고 고친 횟수까지 새긴다.",
            )
    elif anchor_id == "place:ailasera":
        if connection == "facility":
            variants = (
                f"아일라세라의 하피는 {name} 배치를 정할 때 바람길과 날개가 펼쳐질 폭부터 잰다.",
                f"{topic} 아일라세라의 높은 통로와 발착장에서 쓰도록 가볍게 나누어 조립한다.",
            )
        elif connection in {"ranged", "ammunition"}:
            variants = (
                f"아일라세라의 사수들은 {obj} 바람 절벽의 횡풍에 맞춰 손본다.",
                f"{topic} 아일라세라 경비대가 하늘길 교대마다 무게와 비행 흔들림을 확인한다.",
            )
        else:
            variants = (
                f"아일라세라의 운반대는 {obj} 하늘길 한 구간에 맞는 무게로 다시 묶는다.",
                f"아일라세라의 새벽 합창이 울리면 {name} 수량도 다음 운반대에 노래로 전해진다.",
            )
    elif anchor_id == "place:fourth-foundry":
        if connection == "facility":
            variants = (
                f"제4주조도시는 {name} 틀마다 주조 순서와 정비 횟수를 새겨 다음 교대에 넘긴다.",
                f"{topic} 제4주조도시의 골렘이 열과 진동을 읽을 수 있도록 점검판을 함께 단다.",
            )
        else:
            variants = (
                f"제4주조도시는 {name} 표면에 주조 순서와 수리 이력을 새긴다.",
                f"제4주조도시의 골렘은 {obj} 무게보다 오래 버틸 수리 횟수로 평가한다.",
            )
    elif anchor_id == "place:versadion":
        if connection == "facility":
            variants = (
                f"베르사디온은 {name} 설치 계약에 사용 기한과 마나 사고의 책임자를 함께 적는다.",
                f"{topic} 베르사디온의 세 번째 종 뒤에 봉인 상태를 다시 확인하는 절차를 따른다.",
            )
        else:
            variants = (
                f"베르사디온에서는 {obj} 넘길 때 수량과 함께 마나 사고의 책임까지 계약서에 적는다.",
                f"{topic} 베르사디온의 계약정에서 소유자, 사용 기한과 파기 조건을 한 묶음으로 다룬다.",
            )
    elif anchor_id == "place:rakash-crossroads":
        if connection == "facility":
            variants = (
                f"라카쉬 세갈래길은 {name} 주변에 짐수레와 피난 행렬이 함께 지날 폭을 남긴다.",
                f"{topic} 라카쉬 세갈래길의 상인과 난민이 함께 쓰도록 표식과 출입 순서를 단순하게 맞춘다.",
            )
        elif connection in {"animal", "food"}:
            variants = (
                f"라카쉬 세갈래길의 무리는 {obj} 어린 짐승과 부상자 몫부터 떼어 둔다.",
                f"{topic} 라카쉬 세갈래길의 긴 운송과 무리 한솥에 맞춰 나누어진다.",
            )
        else:
            variants = (
                f"라카쉬 세갈래길의 상단은 {name} 포장에 원산지보다 마지막으로 안전하게 건넌 길을 표시한다.",
                f"{topic} 라카쉬 세갈래길에서 피난 행렬과 교역 수레가 같은 재고를 나눌 때 쓰인다.",
            )
    elif anchor_id == "state:ordena":
        if connection == "record":
            variants = (
                f"오르데나 등기원은 {name} 표지와 봉인 상태로 열람 자격을 가른다.",
                f"{topic} 오르데나의 다섯 기관이 서로 다른 인장을 덧찍어 소유권을 다투던 문서다.",
            )
        else:
            variants = (
                f"오르데나 수호원은 {name} 포장에 군영 번호를 찍어 하르나크로 향하는 보급대에 넘긴다.",
                f"오르데나 검문소는 {name} 소유자와 이동 허가가 맞지 않으면 압수품 장부에 올린다.",
            )
    elif anchor_id == "route:milosia":
        variants = (
            f"밀로세아 가도의 짐꾼들은 {obj} 우르단 관문을 넘길 수 있는 크기로 다시 묶는다.",
            f"{topic} 밀로세아 가도의 검문과 비를 견디도록 봉인한 장거리 화물로 거래된다.",
            f"하르나크 상단은 {name} 묶음마다 밀로세아 가도에서 무사히 지난 검문소 수를 표시한다.",
        )
    elif anchor_id == "range:norkandra":
        variants = (
            f"노르칸드라 산맥에서는 {name} 결이나 광맥 흔적으로 다음 채굴지를 잡는다.",
            f"{topic} 노르칸드라의 급한 비탈과 긴 겨울을 지나 내려오는 산지 물자다.",
        )
    elif anchor_id == "sea:sarvenia":
        variants = (
            f"사르베니아 내해의 상인은 {obj} 밀로세아 가도에 오르는 장거리 화물로 포장한다.",
            f"{topic} 사르베니아 내해 연안의 농장과 하르나크 장터를 잇는 서부 교역품이다.",
        )
    elif anchor_id == "practice:orc-cauldron":
        variants = (
            f"하르나크의 오크는 큰솥의 몫 관습에 따라 {obj} 가장 약한 주민에게 먼저 내어 준다.",
            f"큰솥의 몫을 나누는 날에는 {name} 냄비가 비기 전까지 하르나크의 서열도 잠시 멈춘다.",
        )
    elif anchor_id == "practice:orc-vigil":
        variants = (
            f"하르나크의 오크는 무기 철야 때 {obj} 손질하며 주인의 용기와 실수를 함께 말한다.",
            f"무기 철야가 끝나면 {name} 손잡이에 하르나크 동료들의 정비 표식이 한 줄 늘어난다.",
        )
    elif anchor_id == "practice:vampire-consent":
        variants = (
            f"하르나크의 밤궁정은 동의의 잔을 나누기 전에 {name} 양과 목적을 제공자와 함께 확인한다.",
            f"동의의 잔 장부에는 {name} 제공자와 수령자, 약속한 용도가 하르나크식으로 함께 적힌다.",
        )
    elif anchor_id == "practice:slime-water":
        variants = (
            f"하르나크의 합류수 공동체는 맑은물 합류 전에 {topic} 핵과 기억을 흐리지 않는지 확인한다.",
            f"맑은물 합류가 열리는 날에는 {name} 오염 검사가 하르나크 수조의 첫 준비다.",
        )
    elif anchor_id == "practice:myconid-mist":
        variants = (
            f"므네실라의 공유 안개 시간에는 {name} 향과 습도가 공동 정원의 기억 신호가 된다.",
            f"공유 안개를 준비하는 므네실라 주민은 {name} 상태를 포자 리듬과 함께 기록한다.",
        )
    elif anchor_id == "practice:kobold-tool-name":
        variants = (
            f"크릭 제7굴의 첫 도구 이름짓기 뒤에는 {name} 손잡이에 도제의 이름과 날짜가 남는다.",
            f"첫 도구 이름짓기를 치른 코볼트는 {obj} 크릭 제7굴 씨족 장부에도 올린다.",
        )
    elif anchor_id == "practice:harpy-chorus":
        variants = (
            f"아일라세라의 새벽 합창은 {name} 수량과 다음 하늘길의 위험도 함께 전한다.",
            f"새벽 합창이 끝나면 아일라세라 운반대는 {obj} 노래에 적힌 목적지로 나눈다.",
        )
    elif anchor_id == "practice:golem-memory":
        variants = (
            f"제4주조도시의 기억판 안치 기록에는 {name} 제작자와 마지막 정비일이 함께 새겨진다.",
            f"기억판 안치를 맡은 골렘은 {obj} 제4주조도시의 수리 계보에도 남긴다.",
        )
    elif anchor_id == "practice:demon-embers":
        variants = (
            f"베르사디온의 맹세의 잿불 의식에서는 {name} 소유 조건을 쓴 사본 하나만 태운다.",
            f"맹세의 잿불이 꺼질 때까지 베르사디온은 {name} 인도와 대가를 한 계약으로 묶는다.",
        )
    elif anchor_id == "practice:beastkin-meal":
        variants = (
            f"라카쉬 세갈래길의 무리 한솥에서는 {obj} 홀로 온 이와 어린 짐승에게 먼저 나눈다.",
            f"무리 한솥이 열리면 라카쉬 세갈래길의 {name} 몫은 가구보다 돌봄 순서로 정해진다.",
        )
    elif anchor_id == "practice:adventurer-table":
        variants = (
            f"귀환자의 식탁에서는 {obj} 나누며 밀로세아 가도에서 돌아오지 못한 동료를 보고한다.",
            f"하르나크 원정대는 귀환자의 식탁에 {obj} 올린 뒤 잃은 장비와 사람을 함께 센다.",
        )
    else:
        raise ValueError(f"unknown lore anchor: {anchor_id}")

    sentence = stable_choice(item.stable_id, variants)
    if not any(token in sentence for token in LORE_ANCHOR_TOKENS[anchor_id]):
        raise ValueError(f"lore sentence does not name its anchor: {item.stable_id} -> {anchor_id}")
    return LoreLink(anchor_id, connection, story_layer, sentence)


def facility_lore_anchor(
    item: ItemRow,
    buildings: dict[str, tuple[str, set[str], str]],
) -> tuple[str, str, str]:
    number = item.stable_id.split(":", 1)[1]
    landmark = {
        "9201": ("state:ordena", "record", "clue"),
        "9202": ("place:harnak", "facility", "clue"),
        "9203": ("state:ordena", "military", "clue"),
        "9204": ("place:harnak", "facility", "clue"),
        "9205": ("place:mnesila", "facility", "clue"),
        "9206": ("state:ordena", "record", "clue"),
        "9207": ("place:versadion", "facility", "clue"),
        "9208": ("place:versadion", "facility", "clue"),
        "9209": ("place:fourth-foundry", "facility", "clue"),
    }
    if number in landmark:
        return landmark[number]
    building = buildings.get(f"building:{number}")
    abilities = building[1] if building else set()
    operation = building[2] if building else ""
    title = item.title.removesuffix(" 설치 키트")
    if "보철" in title or "골렘" in title:
        return "place:fourth-foundry", "facility", "everyday"
    if any(token in title for token in ("의료", "치료", "수술", "격리", "감염", "백신", "혈청", "약품", "장기", "마취", "소독", "위생", "시신", "동면")):
        return "place:mnesila", "facility", "everyday"
    if any(token in title for token in ("식당", "음식점", "식탁", "주방", "화덕", "조리", "배식", "고기", "제분", "훈연", "가마솥", "절임", "냉장 준비", "식재료")):
        return "practice:orc-cauldron", "facility", "everyday"
    if any(token in title for token in ("공연", "무대", "천문", "기후", "바람", "비행", "신호", "환기", "탄도")):
        return "place:ailasera", "facility", "everyday"
    if any(token in title for token in ("사육", "축사", "용병", "피난", "운송")):
        return "place:rakash-crossroads", "facility", "everyday"
    if any(token in title for token in ("동력", "전력", "발전", "축전", "흑강", "주조")):
        return "place:fourth-foundry", "facility", "everyday"
    if any(token in title for token in ("비전", "마나", "룬", "공명", "시간 고정", "연금술")):
        return "place:versadion", "facility", "everyday"
    if any(token in title for token in ("상점", "판매", "거래", "시장", "숙박", "여관", "객실", "접객")):
        return "place:rakash-crossroads", "facility", "everyday"
    if "BuildingCookingAbility" in abilities or "BuildingButcherAbility" in abilities:
        return "practice:orc-cauldron", "facility", "everyday"
    if "BuildingProductionWorkstationAbility" in abilities:
        operation_anchor = {
            "proficiency:food-production": "practice:orc-cauldron",
            "proficiency:medicine": "place:mnesila",
            "proficiency:scholarship": "place:versadion",
            "proficiency:fieldwork": "place:rakash-crossroads",
            "proficiency:ranged-combat": "place:ailasera",
            "proficiency:melee-combat": "place:krik-seventh",
            "proficiency:crafting": "place:krik-seventh",
        }.get(operation)
        if operation_anchor:
            return operation_anchor, "facility", "everyday"
    groups = (
        ({"BuildingMedicalAbility", "BuildingAnesthesiaAbility", "BuildingOrganStorageAbility", "BuildingSterilizationAbility", "BuildingCropPlotAbility", "BuildingPreservationAbility", "BuildingCleaningAbility", "BuildingWastewaterProcessorAbility"}, "place:mnesila"),
        ({"BuildingPowerProducerAbility", "BuildingPowerStorageAbility", "BuildingCircuitBreakerAbility", "BuildingGolemRechargeAbility", "BuildingEquipmentCraftingAbility", "BuildingEquipmentMaintenanceAbility", "BuildingProstheticAssemblyAbility", "BuildingConveyorSegmentAbility", "BuildingConveyorOverflowAbility"}, "place:fourth-foundry"),
        ({"BuildingRetailAbility", "BuildingServiceAbility", "BuildingServiceSupportAbility", "BuildingBeastPenAbility", "BuildingMercenaryHiringAbility", "BuildingSeatingAbility", "BuildingTableAbility"}, "place:rakash-crossroads"),
        ({"BuildingCircusStageAbility", "BuildingCircusTicketBoothAbility", "BuildingCircusGamblingAbility", "BuildingTrainingAbility", "BuildingVentilationAbility", "BuildingAirDuctAbility"}, "place:ailasera"),
        ({"BuildingArcaneSurgeryAbility", "BuildingResearchArchiveAbility", "BuildingResearchCapacityAbility", "BuildingRehabilitationAbility"}, "place:versadion"),
        ({"BuildingProductionWorkstationAbility", "BuildingStorageAbility", "BuildingInternalStockAbility", "BuildingSecurityAbility", "BuildingDefenseAbility", "BuildingCoverAbility"}, "place:krik-seventh"),
    )
    for expected, anchor in groups:
        if abilities.intersection(expected):
            return anchor, "facility", "everyday"
    return "place:harnak", "facility", "everyday"


def facility_lore_link(
    item: ItemRow,
    buildings: dict[str, tuple[str, set[str], str]],
) -> LoreLink:
    anchor, connection, layer = facility_lore_anchor(item, buildings)
    number = item.stable_id.split(":", 1)[1]
    name = item.title.removesuffix(" 설치 키트")
    obj = with_object(name)
    topic = with_topic(name)
    landmark_sentences = {
        "9201": f"오르데나 등기원이 폐기한 수송 명부와 하르나크의 귀환 기록을 {name}에서 나란히 대조한다.",
        "9202": f"하르나크의 여섯 도시는 {name}에 서로 다른 체형의 대표가 같은 높이에서 발언할 자리를 둔다.",
        "9203": f"오르데나 수호원은 {name}의 문폭과 깃발 높이까지 군단 규격으로 통일한다.",
        "9204": f"하르나크 주민은 {name}의 성벽마다 어느 도시가 맡아 지키고 고칠지를 새긴다.",
        "9205": f"므네실라는 {name} 안의 흙과 물을 외부 생태계와 섞이지 않는 하나의 순환으로 묶는다.",
        "9206": f"오르데나 등기원에서 지운 이름도 {name}의 계보 기록에는 혈연과 양육 관계를 따라 남는다.",
        "9207": f"베르사디온은 {obj} 가동할 때마다 시간을 빌린 대가와 책임자를 계약서에 적는다.",
        "9208": f"베르사디온의 계약술사들은 {name}에 공급한 마나와 실패했을 때의 배상 조건을 함께 봉인한다.",
        "9209": f"제4주조도시의 골렘은 {name}에 참여한 제작자의 기억판을 완성 순서대로 안치한다.",
    }
    if number in landmark_sentences:
        return exact_lore_link(item, anchor, connection, landmark_sentences[number], layer)

    building = buildings.get(f"building:{number}")
    abilities = building[1] if building else set()
    operation = building[2] if building else ""

    if anchor == "practice:orc-cauldron":
        if "BuildingButcherAbility" in abilities:
            sentence = f"하르나크의 큰솥의 몫이 시작되면 {name}에서 나온 가장 부드러운 고기는 부상자와 어린 주민에게 먼저 간다."
        else:
            sentence = stable_choice(item.stable_id, (
                f"하르나크식 {name}에는 큰솥의 몫을 받을 어린 주민과 부상자를 위한 낮은 자리가 비어 있다.",
                f"하르나크의 {name}에서는 큰솥의 몫이 끝날 때까지 조리사가 자기 그릇을 들지 않는다.",
                f"하르나크의 큰솥의 몫은 {name}의 불을 올리기 전 밤 경비와 어린 주민의 그릇부터 늘어놓는다.",
                f"하르나크식 {name}의 솥가에는 큰솥의 몫을 뜻하는 여섯 개의 얕은 국자 자국이 있다.",
            ))
        return exact_lore_link(item, anchor, connection, sentence, layer)

    if anchor == "place:mnesila":
        if abilities.intersection({"BuildingMedicalAbility", "BuildingAnesthesiaAbility", "BuildingOrganStorageAbility", "BuildingSterilizationAbility"}):
            sentence = f"므네실라식 {name}의 바닥 홈은 환자, 도구와 세척수가 서로 다른 색으로 흘러 섞인 자리를 드러낸다."
        elif "BuildingCropPlotAbility" in abilities:
            sentence = f"므네실라의 {name}에는 수확한 자리보다 쉬게 둔 흙에서 더 밝은 포자등이 켜진다."
        elif abilities.intersection({"BuildingCleaningAbility", "BuildingWastewaterProcessorAbility"}):
            sentence = f"므네실라의 {name}에서 거른 물은 깨끗하면 초록 포자막이 피고, 오염이 남으면 검게 가라앉는다."
        elif "BuildingPreservationAbility" in abilities:
            sentence = f"므네실라의 {name} 안에서는 습도가 오르면 벽의 포자점이 번져 약재와 음식의 자리를 바꾸라고 알린다."
        else:
            sentence = stable_choice(item.stable_id, (
                f"므네실라식 {name}에는 약방의 흰 포자점과 공동 정원의 초록 포자점이 함께 박혀 있다.",
                f"므네실라의 {topic} 씻은 물이 공동 정원으로 돌아갈 수 있을 때만 초록등이 켜진다.",
                f"므네실라에서는 {name} 틈에 포자가 검게 피면 사용을 멈추고 물길부터 닫는다.",
            ))
        return exact_lore_link(item, anchor, connection, sentence, layer)

    if anchor == "place:fourth-foundry":
        if abilities.intersection({"BuildingPowerProducerAbility", "BuildingPowerStorageAbility", "BuildingCircuitBreakerAbility", "BuildingGolemRechargeAbility"}):
            sentence = f"제4주조도시식 {name}의 네모 홈은 출력이 흔들릴수록 깊어져 골렘이 손끝으로 고장 지점을 찾게 한다."
        elif abilities.intersection({"BuildingEquipmentCraftingAbility", "BuildingEquipmentMaintenanceAbility", "BuildingProstheticAssemblyAbility"}):
            sentence = f"제4주조도시의 {name}에서 나온 물건은 마지막 수리자가 누른 네모 손자국을 하나씩 달고 나온다."
        elif abilities.intersection({"BuildingConveyorSegmentAbility", "BuildingConveyorOverflowAbility"}):
            sentence = f"제4주조도시의 {topic} 멈춘 부품도 바닥으로 버리지 않고 원래 작업대의 색홈에 걸어 둔다."
        else:
            sentence = stable_choice(item.stable_id, (
                f"제4주조도시제 {name}의 이음부에는 몸과 설비를 같은 공구로 고칠 수 있는 네모 눈금이 있다.",
                f"제4주조도시의 {topic} 정비할 때 기억판을 놓아둘 작은 홈부터 드러난다.",
                f"제4주조도시산 {name}의 그을린 네모 자국은 마지막으로 열을 댄 골렘의 손 모양이다.",
            ))
        return exact_lore_link(item, anchor, connection, sentence, layer)

    if anchor == "place:krik-seventh":
        if abilities.intersection({"BuildingDefenseAbility", "BuildingSecurityAbility", "BuildingCoverAbility"}):
            sentence = stable_choice(item.stable_id, (
                f"크릭 제7굴식 {name}의 바닥에는 막는 사선과 주민이 빠질 퇴로가 서로 다른 톱니 모양으로 파여 있다.",
                f"크릭 제7굴의 {name}에서 보이지 않는 사각은 실제 갱도 벽과 같은 붉은색으로 칠해진다.",
                f"크릭 제7굴산 {name}에는 방어 방향보다 퇴로를 가리키는 일곱 번째 톱니가 더 크게 새겨져 있다.",
            ))
        elif abilities.intersection({"BuildingStorageAbility", "BuildingInternalStockAbility"}):
            sentence = f"크릭 제7굴식 {name}의 선반 칸은 씨족마다 톱니 모양이 달라 빌린 공구가 돌아갈 자리를 손으로도 찾는다."
        elif "BuildingProductionWorkstationAbility" in abilities:
            work = {
                "proficiency:food-production": "식재료",
                "proficiency:medicine": "약재",
                "proficiency:crafting": "부품",
                "proficiency:scholarship": "기록",
                "proficiency:fieldwork": "현장 표본",
                "proficiency:melee-combat": "근접 장비",
                "proficiency:ranged-combat": "원거리 장비",
            }.get(operation, "재료")
            sentence = stable_choice(item.stable_id, (
                f"크릭 제7굴의 {name}에서 처음 나온 {work}에는 대장보다 도제의 이름이 먼저 붙는다.",
                f"크릭 제7굴식 {name}의 공구걸이는 씨족마다 톱니 모양이 달라 엉뚱한 자리에 걸린 공구가 바로 눈에 띈다.",
                f"크릭 제7굴의 {name}에서 치수를 통과한 {work}에는 도제가 낸 일곱 번째 톱니 자국이 있다.",
                f"크릭 제7굴에서는 {name}의 첫 고장을 고친 날부터 그 작업대를 도제의 별명으로 부른다.",
                f"크릭 제7굴산 {name}의 작업면에는 {work}를 처음 완성한 도제의 손높이에 맞춘 긁힘이 남아 있다.",
                f"크릭 제7굴의 {topic} 교대가 끝나면 공구 손잡이를 일곱 톱니 방향으로 돌려 두어 빠진 도구를 드러낸다.",
                f"크릭 제7굴식 {name}에는 완성된 {work}보다 실패한 첫 시제품을 걸어 두는 자리가 더 눈에 띈다.",
            ))
        else:
            sentence = f"크릭 제7굴산 {name}의 조립판에는 만든 이와 마지막으로 손본 이의 톱니 자국이 나란히 찍혀 있다."
        return exact_lore_link(item, anchor, connection, sentence, layer)

    if anchor == "place:rakash-crossroads":
        if abilities.intersection({"BuildingRetailAbility", "BuildingServiceAbility", "BuildingServiceSupportAbility"}):
            sentence = stable_choice(item.stable_id, (
                f"라카쉬 세갈래길의 {name} 가격판에는 동전 옆에 운반과 길 안내로 값을 치르는 빈칸이 있다.",
                f"라카쉬 세갈래길식 {topic} 화폐가 없는 피난민도 수레를 밀거나 길을 알려 값을 치를 수 있다.",
                f"라카쉬 세갈래길의 {name}에서는 물과 응급품만큼은 상인과 피난민에게 같은 값을 받는다.",
                f"라카쉬 세갈래길의 {name} 출구에는 물건값보다 다음 안전한 길을 크게 그린 지도가 걸려 있다.",
            ))
        elif "BuildingBeastPenAbility" in abilities:
            sentence = f"라카쉬 세갈래길의 {name}에는 주인 이름표가 없고, 먹이 순서와 무리에서 떨어진 날을 뜻하는 매듭만 달려 있다."
        elif "BuildingMercenaryHiringAbility" in abilities:
            sentence = f"라카쉬 세갈래길의 {name} 벽에는 임금표 옆에 고향으로 돌아갈 길과 부상자를 데려올 길이 함께 그려져 있다."
        elif abilities.intersection({"BuildingSeatingAbility", "BuildingTableAbility"}):
            sentence = f"라카쉬 세갈래길의 {name}에는 상인, 난민과 정찰대가 같은 식탁을 나눌 수 있도록 빈자리를 둔다."
        else:
            sentence = f"라카쉬 세갈래길식 {name} 주변은 짐수레 두 대와 피난 행렬이 엇갈릴 만큼 넓게 비워 둔다."
        return exact_lore_link(item, anchor, connection, sentence, layer)

    if anchor == "place:ailasera":
        if abilities.intersection({"BuildingCircusStageAbility", "BuildingCircusTicketBoothAbility", "BuildingCircusGamblingAbility"}):
            sentence = f"아일라세라의 {name}에서 난 웃음과 사고는 다음 날 새벽 합창의 서로 다른 음으로 도시 전체에 퍼진다."
        elif abilities.intersection({"BuildingVentilationAbility", "BuildingAirDuctAbility"}):
            sentence = f"아일라세라의 하피는 {obj} 놓기 전 연기 흐름과 날개가 펴질 폭을 바람 절벽에서 시험한다."
        elif "BuildingTrainingAbility" in abilities:
            sentence = f"아일라세라식 {name}의 바닥에는 걷는 전사와 나는 전사가 같은 목표에 닿는 두 훈련선이 그려져 있다."
        else:
            sentence = f"아일라세라산 {name}의 부품 상자는 하늘길 한 구간을 날아 나를 수 있는 무게를 넘지 않는다."
        return exact_lore_link(item, anchor, connection, sentence, layer)

    if anchor == "place:versadion":
        if abilities.intersection({"BuildingResearchArchiveAbility", "BuildingResearchCapacityAbility"}):
            sentence = f"베르사디온의 {name}에서 나온 발견은 세 번째 종이 울리기 전까지 발명자의 그림자 아래 봉해 둔다."
        elif "BuildingArcaneSurgeryAbility" in abilities:
            sentence = f"베르사디온식 {topic} 환자, 집도의와 마나 제공자의 세 봉인이 모두 있어야 불이 들어온다."
        elif "BuildingRehabilitationAbility" in abilities:
            sentence = f"베르사디온의 {name}에는 어제의 서명을 지울 수 있는 작은 화로가 있다. 마음이 바뀌면 다시 쓴다."
        else:
            sentence = stable_choice(item.stable_id, (
                f"베르사디온식 {name}에는 사용 기한이 지나면 스스로 풀리는 세 번째 봉인실이 있다.",
                f"베르사디온의 {topic} 계약자가 손을 떼면 마나빛도 천천히 사라진다.",
                f"베르사디온산 {name}의 쌍인장은 마나를 댄 사람과 쓰는 사람의 손 모양이다.",
            ))
        return exact_lore_link(item, anchor, connection, sentence, layer)

    if anchor == "state:ordena":
        if connection == "record":
            sentence = f"오르데나 등기원은 {name}에 남은 봉인과 필체로 열람자와 기록의 소유권을 가른다."
        else:
            sentence = f"오르데나 수호원은 {name}의 규격과 통행 허가가 맞지 않으면 군용 시설로 압류한다."
        return exact_lore_link(item, anchor, connection, sentence, layer)

    if abilities.intersection({"BuildingWaterProducerAbility", "BuildingWaterStorageAbility"}):
        sentence = f"하르나크식 {name}에는 식수, 슬라임 생활수와 공방 용수가 섞이지 않도록 서로 다른 모양의 관이 달린다."
    elif abilities.intersection({"BuildingLightingAbility", "BuildingTemperatureAbility", "BuildingThermalEmitterAbility"}):
        sentence = f"하르나크의 {topic} 밤에 일하는 뱀파이어 쪽은 어둡고 균사 주민 쪽은 촉촉하게 맞춰져 있다."
    elif "BuildingNeedRecoveryAbility" in abilities:
        sentence = f"하르나크의 {with_topic(name)} 뿔, 날개와 꼬리가 눌리지 않도록 주민이 실제로 쉬는 자세를 보고 치수를 고친다."
    elif any(token in name for token in ("복도", "문", "계단", "다리", "통로")):
        sentence = f"하르나크식 {topic} 오크와 골렘이 마주 지나고 하피가 날개를 접어 돌 수 있는 폭으로 만든다."
    elif any(token in name for token in ("벽", "바닥", "기둥", "지붕")):
        sentence = f"하르나크 주민은 {name}의 수리 구역을 여섯 도시가 나눠 맡아 한 도시가 봉쇄돼도 길이 끊기지 않게 한다."
    else:
        sentence = stable_choice(item.stable_id, (
            f"하르나크의 건축대는 {obj} 여러 체형의 주민이 함께 쓸 수 있는 높이와 통로 폭으로 고친다.",
            f"하르나크는 {name}의 사용 순서를 종족 구분 없이 실제 작업과 생활 시간에 맞춰 정한다.",
            f"하르나크의 {name} 도면에는 뿔, 날개, 꼬리와 골렘의 무게가 부딪히는 지점을 따로 표시한다.",
            f"하르나크 주민은 {obj} 어느 한 도시의 소유로 두지 않고 정비 교대를 나눠 맡는다.",
        ))
    return exact_lore_link(item, anchor, connection, sentence, layer)


def evolution_lore_link(item: ItemRow) -> LoreLink:
    if item.stable_id.startswith("evolution:catalyst:"):
        family = item.stable_id.split(":")[2]
        instrument = with_instrument(item.title)
        obj = with_object(item.title)
        topic = with_topic(item.title)
        anchor = {
            "arcane": "place:versadion", "authority": "state:ordena",
            "defense": "place:fourth-foundry", "industry": "place:krik-seventh",
            "offense": "place:ailasera", "survival": "place:mnesila",
            "universal": "place:harnak",
        }[family]
        variants = {
            "arcane": (
                f"베르사디온산 {item.title}에는 조율자와 마나 제공자가 누른 두 봉인이 서로 마주 본다.",
                f"베르사디온의 {topic} 빌린 마나가 줄어들수록 세 번째 봉인실이 옅어진다.",
                f"베르사디온의 세 번째 종이 울리면 {item.title} 표면의 남은 마나가 푸른 이슬처럼 맺힌다.",
            ),
            "authority": (
                f"오르데나제 {item.title}에는 오원회의 다섯 색 인장 중 허가한 기관의 색만 남는다.",
                f"오르데나 등기원은 진행 번호가 빈 {obj} 완성품으로 인정하지 않는다.",
                f"오르데나 통상원의 붉은 봉랍이 붙은 {topic} 군영과 허가 공방에서만 풀 수 있다.",
            ),
            "defense": (
                f"제4주조도시는 {instrument} 강화한 구조물에 시험 망치 자국을 지우지 않고 남겨 둔다.",
                f"제4주조도시의 골렘은 {obj} 쓴 장갑판의 움푹 팬 자리를 손끝으로 읽는다.",
                f"제4주조도시산 {topic} 단계가 오를 때마다 같은 방어구를 다시 눌러 생긴 네모 자국이 늘어난다.",
            ),
            "industry": (
                f"크릭 제7굴산 {topic} 진행 단계마다 톱니 하나가 더 새겨져 다른 부품과 뒤섞이지 않는다.",
                f"크릭 제7굴의 도제는 {obj} 처음 쓴 설비의 별명을 포장끈에 매단다.",
                f"크릭 제7굴의 {item.title} 한 묶음에는 참여한 작업대 수만큼 작은 톱니 자국이 찍힌다.",
            ),
            "offense": (
                f"아일라세라의 푸른 깃매듭은 {instrument} 바뀐 사거리와 반동을 바람 절벽에서 시험했다는 표시다.",
                f"아일라세라 사수는 {obj} 댄 무기가 빗나간 방향만큼 깃털 끝을 짧게 자른다.",
                f"아일라세라산 {item.title}에는 지상 시험의 흰 깃과 비행 시험의 푸른 깃이 따로 달린다.",
            ),
            "survival": (
                f"므네실라산 {topic} 물과 흙이 회복되면 표면의 검은 포자점이 다시 초록빛으로 돌아온다.",
                f"므네실라의 공동 정원은 {obj} 쓴 뒤 살아남은 균사에서 다음 단계의 포자를 채취한다.",
                f"므네실라의 {topic} 서로 다른 몸에 닿을 때마다 다른 색의 포자 무늬를 피운다.",
            ),
            "universal": (
                f"하르나크의 {topic} 여섯 도시 어느 공방에서도 맞도록 가장자리에 여섯 모양의 홈이 있다.",
                f"하르나크 장터의 {item.title}에는 만든 도시의 표식 옆을 다음 개조 도시를 위해 비워 둔다.",
                f"하르나크산 {topic} 한 도시가 독점하지 못하도록 여섯 공방의 봉인을 번갈아 받는다.",
            ),
        }[family]
        sentence = stable_choice(item.stable_id, variants)
        return exact_lore_link(item, anchor, "craft", sentence)

    variants = (
        f"하르나크의 수리공은 {item.title}에 남은 빛과 냄새만으로 어느 도시의 촉매에서 나온 찌꺼기인지 가린다.",
        f"하르나크 장터의 {item.title} 통에는 같은 진행대에서 나온 가루끼리만 모여 다시 정제된다.",
        f"하르나크의 공동 창고는 {with_object(item.title)} 원래 장비와 다른 선반에 둔다. 남은 반응이 엉키는 일을 막기 위해서다.",
    )
    return exact_lore_link(item, "place:harnak", "craft", stable_choice(item.stable_id, variants))


def food_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1]
    name = item.title
    obj = with_object(name)
    if slug in {"salted-meat-stew", "boar-stew", "roasted-meat"}:
        sentence = {
            "salted-meat-stew": "하르나크의 큰솥의 몫에서는 염장육 스튜의 첫 그릇을 밤 경비를 마친 주민에게 건넨다.",
            "boar-stew": "하르나크의 큰솥의 몫에서는 멧돼지 스튜의 살코기와 국물을 어린 주민과 부상자에게 먼저 나눈다.",
            "roasted-meat": "하르나크의 큰솥의 몫을 치르는 날에는 고기구이를 굽던 불을 꺼뜨리기 전까지 누구도 혼자 먹지 않는다.",
        }[slug]
        return exact_lore_link(item, "practice:orc-cauldron", "food", sentence)
    if slug in {"lavish-meat", "night-spirit"}:
        sentence = (
            f"하르나크 밤궁정의 {name}에는 피를 내준 이와 초대한 손님의 동의의 잔이 나란히 놓인다."
            if slug == "lavish-meat"
            else "베르사디온의 밤 증류주는 동의의 잔을 맞댄 뒤에야 피와 섞인다."
        )
        return exact_lore_link(item, "practice:vampire-consent", "food", sentence)
    if slug in {"mushroom-soup", "cheese-mushroom", "stuffed-mushroom", "garden-meal", "lavish-vegan", "fermented-pickle", "root-stew"}:
        detail = {
            "mushroom-soup": "동굴버섯국을 끓인 물",
            "cheese-mushroom": "치즈버섯찜에서 떼어 낸 균사 밑동",
            "stuffed-mushroom": "속 채운 버섯을 다듬고 남은 자투리",
            "garden-meal": "정원 요리의 씨앗과 줄기",
            "lavish-vegan": "월야 비건 만찬에 오른 작물의 채취 자리",
            "fermented-pickle": "발효 절임의 소금물과 발효 날짜",
            "root-stew": "잿불뿌리 스튜의 껍질과 식힌 재",
        }[slug]
        sentence = f"므네실라에서는 {detail}까지 공동 정원으로 돌아가 다음 포자철의 거름이 된다."
        return exact_lore_link(item, "place:mnesila", "food", sentence)
    if slug in {"expedition-ration-pack", "jerky", "preserved-ration"}:
        detail = {
            "expedition-ration-pack": "원정 배급 꾸러미의 빈 포장",
            "jerky": "육포 묶음",
            "preserved-ration": "보존 배급식의 봉인",
        }[slug]
        sentence = f"하르나크의 귀환자의 식탁에는 {detail}도 놓인다. 빈 포장 하나가 돌아오지 못한 한 끼를 뜻한다."
        return exact_lore_link(item, "practice:adventurer-table", "food", sentence)
    if slug in {"grain-porridge", "malt-porridge", "twilight-beer", "egg-pancake", "meat-pie", "vegetable-pie", "fresh-curd", "grape-syrup"}:
        detail = {
            "grain-porridge": "황혼곡죽은 수확일 아침에 밭일꾼과 가도 짐꾼이 함께 먹는 첫 끼다",
            "malt-porridge": "맥아죽은 싹이 너무 길게 난 곡물도 버리지 않으려 만든 농가 음식이다",
            "twilight-beer": "황혼 맥주는 내해의 곡물값과 가도 사정을 전하는 장터 술이다",
            "egg-pancake": "달걀전은 내해 농가가 그날 거둔 알을 장날 전에 나눠 먹는 음식이다",
            "meat-pie": "고기 파이는 사르베니아 내해의 곡물과 하르나크에서 온 염장육이 만나는 가도 음식이다",
            "vegetable-pie": "채소 파이는 내해 농가가 장거리 운송에 상처 난 작물을 남기지 않으려 굽는다",
            "fresh-curd": "신선 응유식은 내해 목장의 아침 우유가 밀로세아 가도에 오르기 전 먹는 음식이다",
            "grape-syrup": "포도 시럽은 사르베니아 내해의 밤포도를 장거리 운송할 수 있게 졸인 저장식이다",
        }[slug]
        return exact_lore_link(item, "sea:sarvenia", "food", f"사르베니아 내해에서 {detail}.")
    sentence = f"라카쉬 세갈래길의 무리 한솥에서는 {obj} 어린 주민과 먼 길을 온 손님에게 먼저 건넨다."
    return exact_lore_link(item, "practice:beastkin-meal", "food", sentence)


def feed_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1]
    if slug in {"dog-food", "dog-food-fresh"}:
        sentence = (
            "라카쉬 세갈래길의 운송대는 남은 개밥으로 다음 야영지까지 갈 수 있는 거리를 가늠한다."
            if slug == "dog-food"
            else "라카쉬 세갈래길의 신선 개밥은 다친 사냥개와 새끼에게 먼저 돌아가며, 해가 지기 전에 모두 먹인다."
        )
        return exact_lore_link(item, "place:rakash-crossroads", "animal", sentence)
    sentence = (
        "사르베니아 내해의 목장은 비가 적은 철에 벤 풀을 건초 사료로 말려 밀로세아 가도의 짐승까지 먹인다."
        if slug == "hay"
        else "사르베니아 내해의 목장은 장마철 풀을 사일리지로 눌러 담아 겨울과 가도 봉쇄에 대비한다."
    )
    return exact_lore_link(item, "sea:sarvenia", "animal", sentence)


def material_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1].lower()
    name = item.title
    topic = with_topic(name)
    obj = with_object(name)

    if "water" in slug:
        sentence = f"하르나크의 맑은물 합류가 열리는 날에는 {name} 수조 가장자리에 서로 다른 슬라임의 색이 둥글게 남는다."
        return exact_lore_link(item, "practice:slime-water", "ecology", sentence)
    if slug.endswith("-ore") or slug == "coal" or any(token in slug for token in ("saltstone", "stone")):
        sentence = stable_choice(item.stable_id, (
            f"노르칸드라 산맥에서 캔 {name}에는 귀환 갱도를 뜻하는 흰 쐐기 자국이 남아 있다.",
            f"노르칸드라 채굴대는 {obj} 실은 자루에 광맥보다 먼저 돌아갈 갱도의 방향을 그린다.",
            f"노르칸드라산 {name}의 붉은 줄은 붕괴 위험 경고다. 좋은 광맥 표시로 읽으면 돌아오지 못한다.",
        ))
        return exact_lore_link(item, "range:norkandra", "origin", sentence)
    if any(token in slug for token in ("blacksteel", "mana-alloy", "lead-ingot", "gold-ingot", "plate-blank")):
        detail = "마나가 흐른 온도" if any(token in slug for token in ("blacksteel", "mana")) else "주조 순서와 식힌 시간"
        sentence = f"제4주조도시산 {name}의 옆면에는 {detail}가 얕은 홈으로 새겨져 있다."
        return exact_lore_link(item, "place:fourth-foundry", "craft", sentence)
    if any(token in slug for token in ("iron-ingot", "steel-ingot", "spring-steel", "barrel-steel", "chain-mesh")):
        sentence = f"크릭 제7굴산 {name}에는 일곱 톱니 자국과 되녹인 횟수가 찍혀 있다."
        return exact_lore_link(item, "place:krik-seventh", "craft", sentence)
    if any(token in slug for token in ("powder", "niter", "sulfur", "cartridge", "lead-shot", "charcoal")):
        sentence = f"오르데나의 붉은 봉랍이 없는 {name} 묶음은 밀로세아 검문소를 통과하지 못한다."
        return exact_lore_link(item, "state:ordena", "military", sentence)
    if any(token in slug for token in ("mushroom", "spore", "mycel", "compost", "manure", "dreamleaf", "bloodleaf", "moonflower", "resin", "vinegar", "soap", "rot-toxin")):
        sentence = f"므네실라에서는 {name} 찌꺼기까지 버리지 않는다. 물기를 빼면 공동 정원의 다음 흙이 된다."
        return exact_lore_link(item, "place:mnesila", "ecology", sentence)
    if any(token in slug for token in ("blood", "fang", "ritual", "night", "alchemical", "alcohol", "young-wine", "grape-juice", "syrup")):
        sentence = f"베르사디온의 {name} 병목에는 동의의 잔에서 떼어 낸 검은 실이 감겨 있다."
        return exact_lore_link(item, "practice:vampire-consent", "trade", sentence)
    if any(token in slug for token in ("feather", "bowstring", "cave-silk", "silk")):
        sentence = f"아일라세라산 {name}에는 바람 절벽의 세 횡풍을 견뎠다는 푸른 깃매듭이 달려 있다."
        return exact_lore_link(item, "place:ailasera", "craft", sentence)
    if any(token in slug for token in ("grain", "flour", "malt", "dough", "starch", "cheese", "curd", "vegetable", "ration", "filling")):
        sentence = stable_choice(item.stable_id, (
            f"사르베니아 내해에서 온 {name} 포대에는 소금바람 냄새와 밀로세아 가도의 붉은 먼지가 함께 밴다.",
            f"사르베니아 내해산 {topic} 밀로세아 가도를 오래 지나도 상하지 않도록 작은 포대로 나뉜다.",
            f"하르나크 장터의 {name} 값은 사르베니아 내해의 수위와 밀로세아 가도의 통행세에 따라 먼저 움직인다.",
        ))
        return exact_lore_link(item, "sea:sarvenia", "trade", sentence)
    if any(token in slug for token in ("frost", "wool", "linen", "cloth", "canvas", "cotton", "hemp", "fiber", "yarn", "textile")):
        sentence = stable_choice(item.stable_id, (
            f"노르칸드라 산기슭의 직조가는 {obj} 여러 체형의 옷으로 고쳐 쓰도록 폭과 늘어나는 방향을 가장자리에 표시한다.",
            f"노르칸드라의 겨울 직조장은 {name} 한 필마다 짠 날의 습도와 첫서리를 견딘 횟수를 적는다.",
            f"노르칸드라 상단은 {obj} 하르나크 재단소에 넘길 때 뿔, 날개와 꼬리용 재단 여유를 따로 표시한다.",
        ))
        return exact_lore_link(item, "range:norkandra", "craft", sentence)
    if any(token in slug for token in ("bone", "horn", "leather", "hide", "fat", "meat", "tallow")):
        sentence = f"라카쉬 세갈래길에서 손질한 {name}에는 먹을 몫과 만들 몫을 가르는 두 줄 칼집이 있다."
        return exact_lore_link(item, "place:rakash-crossroads", "animal", sentence)
    if any(token in slug for token in ("lumber", "wood", "log", "rope")):
        sentence = f"밀로세아 가도를 건넌 {name} 묶음은 우르단 관문의 폭에 맞춰 한 번 더 짧게 잘려 있다."
        return exact_lore_link(item, "route:milosia", "trade", sentence)
    if any(token in slug for token in ("rune", "dreamweave", "ember")):
        sentence = f"베르사디온산 {name}의 봉인에는 마나가 새면 가장 먼저 빛을 잃는 세 번째 매듭이 있다."
        return exact_lore_link(item, "place:versadion", "arcane", sentence)
    sentence = f"밀로세아 가도를 지난 {name} 묶음에는 마지막 검문소의 색실이 하나씩 더해진다."
    return exact_lore_link(item, "route:milosia", "trade", sentence)


def equipment_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.lower()
    name = item.title
    obj = with_object(name)
    topic = with_topic(name)
    subject = with_subject(name)
    if any(token in slug for token in ("arquebus", "matchlock", "handgonne", "shotgun", "powder", "blast", "smoke", "cartridge")):
        sentence = stable_choice(item.stable_id, (
            f"오르데나식 {name}의 군번은 개머리판 깊숙이 찍혀 있어 칼로 긁어도 흔적이 남는다.",
            f"오르데나 군영에서 풀린 {name}에는 붉은 봉랍과 지워진 지급 번호가 나란히 남아 있다.",
            f"오르데나 수호원은 {name}의 방아쇠울 안쪽에도 군영 번호를 새긴다. 탈영병이 가장 먼저 긁어내는 자리다.",
        ))
        return exact_lore_link(item, "state:ordena", "military", sentence)
    if any(token in slug for token in ("rune", "mana")):
        sentence = stable_choice(item.stable_id, (
            f"베르사디온산 {name}에는 마나가 새면 먼저 끊어지는 세 번째 봉인실이 감겨 있다.",
            f"베르사디온의 {name} 손잡이에는 사용자와 마나 제공자가 하나씩 누른 쌍인장이 남는다.",
            f"베르사디온에서는 {name}의 마나가 마를 때까지 계약자의 그림자가 날에서 떨어지지 않는다고 말한다.",
        ))
        return exact_lore_link(item, "place:versadion", "arcane", sentence)
    if any(token in slug for token in ("bow", "javelin", "throwing", "arrow")) and "crossbow" not in slug:
        sentence = stable_choice(item.stable_id, (
            f"아일라세라의 푸른 깃매듭은 {subject} 바람 절벽의 세 횡풍을 버텼다는 표시다.",
            f"아일라세라 사수는 {obj} 쏘기 전 바람의 방향을 짧게 노래한다. 마지막 음이 발사 신호다.",
            f"아일라세라제 {name}에는 횡풍 쪽으로 한 치 비껴 잡는 손자국이 닳아 있다.",
        ))
        return exact_lore_link(item, "place:ailasera", "ranged", sentence)
    if any(token in slug for token in ("crossbow", "arbalest", "windlass", "bolt")):
        sentence = stable_choice(item.stable_id, (
            f"크릭 제7굴식 {topic} 좁은 갱도 벽에 걸리지 않도록 활몸 양끝을 짧게 깎는다.",
            f"크릭 제7굴의 일곱 톱니 자국은 {name}의 장력이 갱도 시험을 버텼다는 표시다.",
            f"크릭 제7굴 경비는 {obj} 벽에 기대지 않고도 다시 감을 수 있어야 광산용으로 인정한다.",
        ))
        return exact_lore_link(item, "place:krik-seventh", "ranged", sentence)
    if any(token in slug for token in ("blacksteel", "powered", "plate", "mail", "iron", "warhammer", "mace", "pollaxe", "halberd")):
        sentence = stable_choice(item.stable_id, (
            f"제4주조도시산 {name}에는 충격 시험 자국을 일부러 남긴다. 흠집이 없는 물건은 아직 검사를 마치지 못한 것이다.",
            f"제4주조도시의 골렘은 {obj} 수리할 때 겉판보다 안쪽 기억판을 먼저 떼어 낸다.",
            f"제4주조도시제 {name}의 작은 홈은 몸을 바꿀 때 맞추는 수리 눈금이다.",
        ))
        return exact_lore_link(item, "place:fourth-foundry", "craft", sentence)
    if any(token in slug for token in ("leather", "cloth", "padded", "wood", "buckler")):
        sentence = stable_choice(item.stable_id, (
            f"라카쉬 세갈래길에서 고친 {name} 안쪽에는 이전 주인의 표식과 다음 행선지가 겹쳐 있다.",
            f"라카쉬 세갈래길의 수선공은 {obj} 새 주인에게 맞추되 오래된 이름은 안감 아래 남겨 둔다.",
            f"라카쉬 세갈래길의 {topic} 서로 다른 체형을 거치며 끈 구멍이 한 줄씩 늘어난다.",
        ))
        return exact_lore_link(item, "place:rakash-crossroads", "trade", sentence)
    sentence = stable_choice(item.stable_id, (
        f"하르나크의 무기 철야를 지낸 {name}에는 살아 돌아온 싸움마다 얕은 흠집이 하나씩 더해진다.",
        f"하르나크의 오크는 무기 철야에 {obj} 버리기 전 흠집을 손끝으로 짚으며 마지막 주인의 싸움을 들려준다.",
        f"하르나크 무기 철야에서 이름을 얻은 {topic} 새 주인이 들어도 옛 흠집을 갈아내지 않는다.",
    ))
    return exact_lore_link(item, "practice:orc-vigil", "military", sentence)


def apparel_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1]
    name = item.title
    obj = with_object(name)
    topic = with_topic(name)
    if any(token in slug for token in ("wing", "sky-chorus")):
        sentence = f"아일라세라제 {name}의 등솔기는 날개가 완전히 펴질 때만 팽팽해지며, 첫 착용자의 이름은 새벽 합창에 오른다."
        return exact_lore_link(item, "place:ailasera", "dress", sentence)
    if any(token in slug for token in ("spore", "sterile", "surgical")):
        sentence = f"므네실라의 {topic} 삶으면 포자 무늬가 사라지고, 오염이 남으면 자줏빛 점이 다시 피어난다."
        return exact_lore_link(item, "place:mnesila", "dress", sentence)
    if any(token in slug for token in ("golem", "smith", "heat-work")):
        sentence = f"제4주조도시의 {topic} 그을음을 완전히 씻지 않는다. 골렘들은 그 자국을 몸이 바뀌어도 남는 근무 흔적으로 여긴다."
        return exact_lore_link(item, "place:fourth-foundry", "dress", sentence)
    if any(token in slug for token in ("tail", "keeper", "loincloth", "horn")):
        sentence = f"라카쉬 세갈래길의 수선 천막은 {obj} 뿔과 꼬리, 서로 다른 보행 방식에 맞춰 그 자리에서 고쳐 준다."
        return exact_lore_link(item, "place:rakash-crossroads", "dress", sentence)
    if "weapon-vigil" in slug:
        sentence = f"하르나크의 무기 철야에서 {obj} 두른 사람은 자기 무기의 첫 주인과 마지막 수리자를 차례로 부른다."
        return exact_lore_link(item, "practice:orc-vigil", "dress", sentence)
    if any(token in slug for token in ("contract", "ritual", "ceremonial", "formal", "envoy", "mourning")):
        sentence = f"베르사디온의 {topic} 매듭 수로 계약 당사자를, 안감 색으로 증인과 애도 기간을 드러낸다."
        return exact_lore_link(item, "place:versadion", "dress", sentence)
    if any(token in slug for token in ("miner", "belt", "gloves", "work-shirt", "apron")):
        sentence = f"크릭 제7굴산 {name}의 솔기에는 갱도 번호가 박혀 있다. 같은 자리만 찢어지면 그 갱도부터 닫는다."
        return exact_lore_link(item, "place:krik-seventh", "dress", sentence)
    sentence = stable_choice(item.stable_id, (
        f"하르나크제 {name}의 솔기 안에는 뿔, 날개와 꼬리를 위한 여유 천이 접혀 있다.",
        f"하르나크의 {topic} 골렘 관절에 쓸리지 않도록 안쪽만 두껍고, 슬라임이 입는 치수에는 물빠짐 구멍이 난다.",
        f"하르나크 장터의 중고 {name} 안쪽에는 이전 주인의 체형과 다음 수선 자리가 작은 바늘땀으로 남아 있다.",
        f"하르나크 세탁장은 {obj} 피, 포자, 기름과 점액 냄새에 따라 서로 다른 물길로 보낸다.",
    ))
    return exact_lore_link(item, "place:harnak", "dress", sentence)


def medical_lore_link(item: ItemRow) -> LoreLink:
    stable_id = item.stable_id
    slug = stable_id.split(":", 1)[1].lower()
    name = item.title
    obj = with_object(name)
    topic = with_topic(name)
    if any(token in slug for token in ("blood", "fang")):
        sentence = f"하르나크 밤궁정의 {name}에는 동의의 잔에서 떼어 낸 검은 실과 환자의 흰 실이 함께 묶인다."
        return exact_lore_link(item, "practice:vampire-consent", "care", sentence)
    if any(token in slug for token in ("slime", "coagulation", "pseudopod", "sensory-gel")):
        sentence = f"하르나크의 맑은물 합류 치료사는 {obj} 맑은 물에 한 방울 풀어 색이 핵 쪽으로 번지는지 먼저 본다."
        return exact_lore_link(item, "practice:slime-water", "care", sentence)
    if any(token in slug for token in ("core", "golem", "prosthetic", "power-bypass")):
        sentence = f"제4주조도시의 기억판 안치에서는 {obj} 손보기 전에 기억판부터 떼어 낸다. 몸보다 사람이 먼저라는 오래된 순서다."
        return exact_lore_link(item, "practice:golem-memory", "care", sentence)
    if any(token in slug for token in ("rune", "mana")):
        sentence = f"베르사디온의 {name}에는 환자, 집도의와 마나 제공자가 누른 세 봉인이 모두 있어야 빛이 돈다."
        return exact_lore_link(item, "place:versadion", "care", sentence)
    if "wing" in slug:
        sentence = f"아일라세라의 치료사는 {obj} 댄 뒤 걷는 자세와 비행 때의 장력을 따로 검사한다."
        return exact_lore_link(item, "place:ailasera", "care", sentence)
    if stable_id.startswith("sample:antigen:"):
        disease = name.removesuffix(" 항원 표본")
        sentence = f"므네실라의 {name} 봉인은 {disease} 환자가 머문 방의 색과 같아 잘못 든 병을 한눈에 가린다."
    elif "vaccine" in slug:
        disease = name.removesuffix(" 백신")
        sentence = f"므네실라산 {name} 병에는 {disease}가 유행한 계절만큼 포자 고리가 둘러져 있다."
    elif stable_id.startswith("surgery:organ:"):
        sentence = f"므네실라에서 보존한 {name} 용기에는 기증자의 이름이 포자 먹물로 떠 있다. 시간이 지나면 글자부터 흐려진다."
    elif stable_id.startswith("surgery:prosthetic:"):
        sentence = f"므네실라와 제4주조도시가 함께 만든 {name}에는 살이 닿는 쪽에 초록 점, 기계가 닿는 쪽에 네모 홈이 있다."
    elif "mycel" in slug:
        sentence = f"므네실라의 {topic} 병을 닫은 뒤에도 천천히 자란다. 공동 정원은 절반 이상 채취하지 않는다."
    elif any(token in slug for token in ("anesthetic", "analgesic", "poultice", "trauma")):
        sentence = f"므네실라 치료사는 {obj} 쓴 뒤 통증이 가라앉아도 환자가 스스로 걷기 전에는 회복됐다고 말하지 않는다."
    elif any(token in slug for token in ("antidote", "toxin", "detox")):
        sentence = f"므네실라산 {topic} 독의 계통과 맞지 않으면 약병의 포자점부터 검게 변한다."
    elif any(token in slug for token in ("fertility", "rejuvenation", "regenerative")):
        sentence = f"므네실라에서는 {obj} 건네기 전 환자가 직접 초록 매듭을 묶는다. 치료를 멈춰 달라는 뜻이면 풀어 둔다."
    elif any(token in slug for token in ("emergency", "field")):
        sentence = f"므네실라의 {name} 포장끈은 상처 종류마다 매듭이 달라 어두운 원정길에서도 손끝으로 고를 수 있다."
    elif stable_id == "medicine:antiseptic":
        sentence = "므네실라의 외용 소독제는 피부에 닿으면 옅은 초록빛이 돈다. 빛이 검어지면 곧바로 씻어 낸다."
    elif stable_id == "medicine:disinfectant":
        sentence = "므네실라의 기구 소독제는 마르면 흰 포자막을 남긴다. 닦이지 않은 틈을 눈으로 찾기 위한 흔적이다."
    elif any(token in slug for token in ("isolation", "disinfect", "antiseptic", "sterile", "contaminated")):
        sentence = f"므네실라의 격리실에서 쓰는 {name}에는 방마다 다른 포자색이 배어 있어 밖으로 섞이면 곧 드러난다."
    else:
        sentence = stable_choice(item.stable_id, (
            f"므네실라의 {name} 병에는 사람의 종족보다 몸이 보인 반응을 먼저 표시하는 색점이 찍혀 있다.",
            f"므네실라 치료사는 {obj} 쓴 뒤 환자가 다시 먹고 걷고 일할 때까지 병을 버리지 않는다.",
            f"므네실라산 {name}에는 재료를 기른 정원의 흙이 작은 유리칸에 함께 봉해져 있다.",
        ))
    return exact_lore_link(item, "place:mnesila", "care", sentence)


def relic_lore_link(item: ItemRow) -> LoreLink:
    _, _, faction, index = item.stable_id.split(":")
    anchor = {
        "myconid": "place:mnesila", "kobold": "place:krik-seventh",
        "harpy": "place:ailasera", "golem": "place:fourth-foundry",
        "demon": "place:versadion", "beastkin": "place:rakash-crossroads",
    }[faction]
    facts = {
        ("myconid", "1"): "므네실라의 첫포자 기억병에는 인간군이 불태운 정원과 그곳에서 피신시킨 사람들의 냄새 기억이 남아 있다.",
        ("myconid", "2"): "므네실라의 균맥 절단칼에는 병든 균사만 끊었다는 치료사의 표식과 사람을 해쳤다는 오르데나 압수표가 함께 붙어 있다.",
        ("myconid", "3"): "므네실라의 새정원 배양편은 피난민이 가져온 흙에서도 공동 기억이 다시 자랄 수 있음을 보여 준다.",
        ("kobold", "1"): "크릭 제7굴의 깊은톱니 대가도장은 공방 소유권이 씨족에서 실제 제작자에게 넘어간 시절을 증명한다.",
        ("kobold", "2"): "크릭 제7굴의 무결점 태엽에는 오르데나 군수 규격과 지워진 코볼트 제작자 이름이 같은 깊이로 새겨져 있다.",
        ("kobold", "3"): "크릭 제7굴의 도제명판 원본은 첫 도구 이름짓기 뒤 사람이 공방의 권리자가 되었음을 기록한다.",
        ("harpy", "1"): "아일라세라의 첫바람 깃인장은 하늘길을 연 사람의 이름을 노래와 문서 양쪽에 남긴다.",
        ("harpy", "2"): "아일라세라의 폭풍음 쇳조각에는 오르데나 봉쇄선의 교대 시각이 새벽 합창의 음높이로 새겨져 있다.",
        ("harpy", "3"): "아일라세라의 귀환노래 방울은 돌아온 사람보다 돌아오지 못한 사람의 이름을 먼저 울린다.",
        ("golem", "1"): "제4주조도시의 자기각인 핵편은 골렘이 제작 번호 대신 스스로 고른 이름을 처음 새긴 조각이다.",
        ("golem", "2"): "제4주조도시의 돌맥 조율쇠는 한 골렘의 기억이 여러 번 수리된 몸을 이어 같은 사람으로 남겼음을 보여 준다.",
        ("golem", "3"): "제4주조도시의 첫자유 관절핀에는 소유자의 인장 대신 관절을 선택한 골렘의 손자국이 찍혀 있다.",
        ("demon", "1"): "베르사디온의 삼중계약 인장은 약속한 사람, 대가를 낸 사람과 책임질 사람을 따로 기록한다.",
        ("demon", "2"): "베르사디온의 재판관의 불씨는 강요된 계약을 태워 없앤 판결 뒤에도 꺼지지 않았다고 전해진다.",
        ("demon", "3"): "베르사디온의 무효조항 두루마리는 당사자가 마음을 바꿀 권리를 계약보다 앞에 둔다.",
        ("beastkin", "1"): "라카쉬 세갈래길의 첫발톱 인장은 혈통이 달라도 같은 길을 지킨 사람을 한 무리로 받아들인 기록이다.",
        ("beastkin", "2"): "라카쉬 세갈래길의 무리뼈 호각은 피난 행렬에서 가장 느린 사람의 속도에 맞춰 울렸다.",
        ("beastkin", "3"): "라카쉬 세갈래길의 붉은사냥 망토핀에는 사냥한 짐승보다 함께 돌아온 사람의 수가 먼저 새겨져 있다.",
    }
    return exact_lore_link(item, anchor, "heritage", facts[(faction, index)], "clue")


def component_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1]
    name = item.title
    obj = with_object(name)
    topic = with_topic(name)
    if any(token in slug for token in ("golem", "powered", "alloy", "plate", "joint", "frame", "shaft", "manifold", "machine-parts")):
        sentence = stable_choice(item.stable_id, (
            f"제4주조도시산 {name}에는 마지막 정비자의 손가락 폭만 한 네모 홈이 파여 있다.",
            f"제4주조도시의 골렘은 {obj} 끼우기 전 자기 기억판과 맞닿는 면을 직접 닦는다.",
            f"제4주조도시제 {topic} 몸과 기계를 가리지 않고 맞도록 양쪽에 같은 수리 눈금이 있다.",
        ))
        return exact_lore_link(item, "place:fourth-foundry", "craft", sentence)
    if any(token in slug for token in ("rune", "mana", "dreamweave", "temporal")):
        sentence = stable_choice(item.stable_id, (
            f"베르사디온산 {topic} 세 번째 봉인실이 끊어지면 마나 흐름도 함께 멎는다.",
            f"베르사디온의 {name}에는 마나를 댄 손 두 개의 인장이 서로 마주 보고 있다.",
            f"베르사디온에서는 {name}의 빛이 계약자의 그림자보다 먼저 꺼져야 안전한 부품으로 친다.",
        ))
        return exact_lore_link(item, "place:versadion", "arcane", sentence)
    if any(token in slug for token in ("optics", "weather", "signal")):
        sentence = f"아일라세라산 {name}에는 바람 절벽에서 빛과 소리가 닿은 거리를 나타내는 푸른 깃금이 있다."
        return exact_lore_link(item, "place:ailasera", "craft", sentence)
    if any(token in slug for token in ("strap", "package", "storage", "price-board", "room-partition")):
        sentence = f"라카쉬 세갈래길식 {topic} 수레와 천막 어디에나 맞도록 구멍 간격이 한 뼘으로 통일돼 있다."
        return exact_lore_link(item, "place:rakash-crossroads", "trade", sentence)
    sentence = stable_choice(item.stable_id, (
        f"크릭 제7굴의 첫 도구 이름짓기를 거친 {name}에는 대장보다 도제의 이름이 먼저 새겨진다.",
        f"크릭 제7굴의 첫 도구 이름짓기에서 {name}의 일곱 번째 톱니 자국은 처음 손댄 도제가 직접 낸다.",
        f"크릭 제7굴의 첫 도구 이름짓기는 {with_subject(name)} 처음 움직이는 날 제작 도제의 별명을 붙여 부른다.",
    ))
    return exact_lore_link(item, "practice:kobold-tool-name", "craft", sentence)


def tool_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1]
    name = item.title
    obj = with_object(name)
    if slug == "administrative-seal":
        sentence = "오르데나 등기원의 행정 인장은 사람, 화물과 토지의 이동을 허가하며 찍은 관리의 이름은 공개하지 않는다."
        return exact_lore_link(item, "state:ordena", "record", sentence)
    if any(token in slug for token in ("prisoner", "restraint")):
        sentence = f"오르데나제 {name}에는 포로 이름보다 감시자 번호가 더 깊게 찍혀 있다."
        return exact_lore_link(item, "state:ordena", "military", sentence)
    if any(token in slug for token in ("deep-shaft", "gauge", "prospecting", "maintenance")):
        sentence = f"크릭 제7굴의 첫 도구 이름짓기를 마친 {name}에는 첫 측정값과 도제의 별명이 나란히 새겨진다."
        return exact_lore_link(item, "practice:kobold-tool-name", "craft", sentence)
    if any(token in slug for token in ("alloy", "powered")):
        sentence = f"제4주조도시의 골렘은 {obj} 잡으면 열과 진동만으로 이전 몸에서 쓰던 공구인지 알아본다."
        return exact_lore_link(item, "place:fourth-foundry", "craft", sentence)
    if any(token in slug for token in ("mana", "rune")):
        sentence = f"베르사디온산 {with_topic(name)} 계약자의 맨손이 닿아야 룬이 끝까지 밝아진다."
        return exact_lore_link(item, "place:versadion", "arcane", sentence)
    if any(token in slug for token in ("weather", "signal")):
        sentence = f"아일라세라의 새벽 합창은 {with_instrument(name)} 확인한 날씨와 경보를 문자보다 먼저 하늘길에 전한다."
        return exact_lore_link(item, "practice:harpy-chorus", "record", sentence)
    sentence = f"밀로세아 가도의 공동 {name}에는 검문소를 지날 때마다 다른 색실이 손잡이에 묶인다."
    return exact_lore_link(item, "route:milosia", "trade", sentence)


def seed_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1]
    crop = item.title.removesuffix(" 종자 로트")
    if any(token in slug for token in ("frost", "flax")):
        sentence = f"노르칸드라산 {crop} 종자 포대에는 첫서리를 견딘 밭의 높이만큼 흰 실이 감겨 있다."
        return exact_lore_link(item, "range:norkandra", "ecology", sentence)
    if any(token in slug for token in ("spore", "cave", "mire", "dream", "shade", "bloodleaf", "moonflower")):
        sentence = f"므네실라의 {crop} 종자는 주인 이름 없이 채취한 정원의 향과 다음 파종자의 색점만 남긴다."
        return exact_lore_link(item, "place:mnesila", "ecology", sentence)
    if "night-grape" in slug:
        sentence = f"베르사디온의 {crop} 종자 봉투에는 동의의 잔을 뜻하는 두 입술 모양 봉인이 찍혀 있다."
        return exact_lore_link(item, "practice:vampire-consent", "ecology", sentence)
    sentence = f"사르베니아 내해산 {crop} 종자 포대에서는 마른 뒤에도 옅은 소금바람 냄새가 난다."
    return exact_lore_link(item, "sea:sarvenia", "ecology", sentence)


def wild_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1]
    name = item.title
    obj = with_object(name)
    if any(token in slug for token in ("mire", "spore", "fung", "rot")):
        sentence = f"므네실라의 정원지기는 {name}에서 건진 포자를 죽이지 않고 검은 흙독에 따로 재운다."
        return exact_lore_link(item, "place:mnesila", "ecology", sentence)
    if any(token in slug for token in ("frost", "cave", "deep", "crystal", "tunnel")):
        sentence = f"노르칸드라 사냥꾼은 {obj} 묶은 끈에 발견한 눈길과 무너진 갱도의 방향을 칼집으로 낸다."
        return exact_lore_link(item, "range:norkandra", "animal", sentence)
    if any(token in slug for token in ("moth", "drake", "wisp")):
        sentence = f"아일라세라의 하피는 {obj} 하늘길의 바람과 마나 흐름이 바뀌었다는 징조로 읽는다."
        return exact_lore_link(item, "place:ailasera", "animal", sentence)
    if any(token in slug for token in ("ash", "ember")):
        sentence = f"베르사디온의 화구 사냥꾼이 거둔 {name}에는 계약 몫을 뜻하는 검은 밀랍이 한 방울 떨어져 있다."
        return exact_lore_link(item, "place:versadion", "animal", sentence)
    sentence = f"라카쉬 세갈래길의 무리는 {name}에서 얻은 고기와 가죽을 사냥한 사람보다 피난 행렬의 필요에 따라 나눈다."
    return exact_lore_link(item, "place:rakash-crossroads", "animal", sentence)


def supply_lore_link(item: ItemRow) -> LoreLink:
    slug = item.stable_id.split(":", 1)[1]
    name = item.title
    obj = with_object(name)
    if any(token in slug for token in ("seed", "pesticide", "fungicide", "greenhouse", "mushroom", "log", "fertilizer", "lure")):
        sentence = f"므네실라의 {name} 포장에는 빈 밭과 죽은 벌레 대신 다음 철에 돌아올 포자 무늬가 그려져 있다."
        return exact_lore_link(item, "place:mnesila", "ecology", sentence)
    if "ammo" in slug:
        sentence = f"오르데나제 {name} 상자는 탄약보다 먼저 붉은 봉랍과 군영 번호로 잠긴다."
        return exact_lore_link(item, "state:ordena", "military", sentence)
    if "performance" in slug:
        sentence = f"아일라세라 공연단은 {name} 하나가 사라져도 새벽 합창에서 마지막 사용 장면과 배우를 알린다."
        return exact_lore_link(item, "place:ailasera", "record", sentence)
    if "funeral" in slug:
        sentence = f"제4주조도시의 기억판 안치는 {obj} 몸의 재료와 상관없이 한 사람의 생을 남기는 데 쓴다."
        return exact_lore_link(item, "practice:golem-memory", "heritage", sentence)
    sentence = f"하르나크의 여섯 도시는 {name}의 표식 순서를 맞춰 서로 다른 경비대도 같은 구조 신호를 알아보게 한다."
    return exact_lore_link(item, "place:harnak", "settlement", sentence)


def item_lore_link(
    item: ItemRow,
    buildings: dict[str, tuple[str, set[str], str]],
) -> LoreLink:
    stable_id = item.stable_id
    prefix = stable_id.split(":", 1)[0]
    slug = stable_id.split(":", 1)[1].lower()

    if prefix == "facility-kit":
        return facility_lore_link(item, buildings)
    if prefix == "food":
        return food_lore_link(item)
    if prefix == "feed":
        return feed_lore_link(item)
    if prefix in {"material", "resource", "craft", "fiber", "yarn", "textile"}:
        return material_lore_link(item)
    if stable_id.startswith("equipment-item:") or prefix == "ammo":
        return equipment_lore_link(item)
    if prefix == "apparel":
        return apparel_lore_link(item)
    if prefix in {"medical", "medicine", "drug", "sample", "surgery", "container"}:
        return medical_lore_link(item)
    if prefix == "component":
        return component_lore_link(item)
    if prefix == "tool":
        return tool_lore_link(item)
    if prefix == "seed-lot":
        return seed_lore_link(item)
    if prefix == "wild":
        return wild_lore_link(item)
    if prefix == "supply":
        return supply_lore_link(item)
    if stable_id.startswith("relic:faction:"):
        return relic_lore_link(item)
    if prefix == "record":
        sentence = {
            "arcane-index": "오르데나의 비전 색인철에는 같은 주문을 수호원은 무기로, 등기원은 압수물로, 통상원은 과세품으로 분류한 흔적이 남아 있다.",
            "breeding-ledger": "오르데나의 번식 장부는 가족을 혈통 칸에 맞추며 다른 종족이 함께 기른 아이의 양육 기록을 여백으로 밀어냈다.",
            "career-ledger": "오르데나의 경력 장부는 하르나크에서 익힌 숙련을 인정하지 않아 돌아온 기술자를 무경력자로 기록한다.",
        }[slug]
        return exact_lore_link(item, "state:ordena", "record", sentence, "clue")
    if prefix == "dark":
        sentence = {
            "bone": "오르데나 토벌문은 하르나크 묘역에서 나온 인간형 뼈를 식인의 증거로 싣지만 매장 시각과 이름은 적지 않는다.",
            "humanoid_corpse": "오르데나 수호원은 인간형 사체의 군복과 상처를 조사하기 전에 하르나크가 훼손한 전사자로 등록한다.",
            "humanoid_meat": "오르데나의 선전물은 금기의 고기 한 점만으로 하르나크의 모든 부엌을 식인 시설이라 부른다.",
        }[slug]
        return exact_lore_link(item, "state:ordena", "military", sentence, "clue")
    if prefix == "evolution":
        return evolution_lore_link(item)
    if prefix == "husbandry":
        return exact_lore_link(item, "place:rakash-crossroads", "animal", "라카쉬 세갈래길의 짐승지기는 깔짚을 갈아 낸 날과 짐승이 무리에서 떨어져 잠든 날을 같은 축사 장부에 적는다.")
    if prefix == "research-blueprint":
        anchor, sentence = {
            "6101": ("place:rakash-crossroads", "라카쉬 세갈래길의 상업 확장 설계도는 고정 상점보다 피난 행렬과 이동 장터가 먼저 지나갈 자리를 남긴다."),
            "6102": ("state:ordena", "오르데나의 요새화 설계도는 방어선 안쪽의 주민보다 우르단 관문에서 들어오는 적의 동선을 먼저 그린다."),
            "6103": ("place:harnak", "하르나크의 생활 지원 설계도는 서로 다른 몸이 같은 물과 침상을 쓰지 못할 때의 우회 설비를 함께 그린다."),
            "6104": ("place:versadion", "베르사디온의 비전 연구 설계도에는 발견의 소유권과 마나 사고의 배상 책임을 적는 빈칸이 있다."),
            "6191": ("place:rakash-crossroads", "라카쉬 세갈래길의 상권 통합 설계도는 상인 조합과 난민 배급소가 같은 창고를 쓰는 시간을 나눈다."),
            "6192": ("state:ordena", "오르데나의 전술 지휘 설계도는 수호원, 개척원과 용병대가 서로 다른 명령을 내릴 때의 우선권을 표시한다."),
            "6193": ("place:versadion", "베르사디온의 비전 공명 설계도는 여러 계약자의 마나가 한 시설에서 충돌할 때 끊을 회로를 따로 둔다."),
        }[slug]
        return exact_lore_link(item, anchor, "record", sentence, "clue")
    if prefix == "captivity":
        if slug == "extracted-blood":
            return exact_lore_link(item, "practice:vampire-consent", "record", "동의의 잔 장부가 없는 추출 혈액은 하르나크 밤궁정에서도 압수하고 제공자를 먼저 찾는다.", "clue")
        sentence = (
            "오르데나 등기원은 기억 잔재를 소유권 없는 압수물로 분류해 당사자의 진술과 열람을 막는다."
            if slug == "memory-residue"
            else "오르데나 수호원의 구속구에는 죄목보다 수용 번호가 크게 새겨져 사람을 사건 기록과 떼어 놓는다."
        )
        return exact_lore_link(item, "state:ordena", "record", sentence, "clue")
    if prefix == "offense":
        sentence = (
            "라카쉬 세갈래길의 감정사는 감정된 귀중품에 원래 주인, 발견한 길과 돌려줄 가능성을 함께 적는다."
            if slug == "appraised-valuables"
            else "라카쉬 세갈래길에서는 미감정 전리품을 승자의 몫으로 나누기 전에 피난민과 실종자의 표식을 먼저 찾는다."
        )
        return exact_lore_link(item, "place:rakash-crossroads", "trade", sentence, "clue")
    if prefix in {"survival", "equipment", "item"}:
        exact = {
            "equipment:cold-work-suit": ("range:norkandra", "노르칸드라 광부는 방한 작업복의 소매에 교대 시간과 손끝 감각이 돌아온 때를 표시한다."),
            "equipment:rune-cold-suit": ("place:versadion", "베르사디온은 룬 방한복이 버틴 냉기와 사용한 마나를 계약자의 작업 시간과 함께 계산한다."),
            "equipment:slime-warming-pad": ("practice:slime-water", "하르나크의 맑은물 합류 치료사는 보온 점액 패드가 슬라임의 핵 주위 온도와 기억 흐름을 바꾸지 않는지 살핀다."),
            "item:equipment-module": ("place:fourth-foundry", "제4주조도시는 개량 부품을 바꾼 뒤에도 원래 장비의 제작자와 수리 계보를 기억판에 이어 적는다."),
            "item:lineage-seal": ("state:ordena", "오르데나 등기원의 계보 인장은 혈연, 양육과 상속 가운데 법이 인정할 관계를 한 칸으로 고정한다."),
            "survival:cooked_meal": ("place:harnak", "하르나크 공동 부엌의 조리 식량은 종족 이름보다 먹을 수 없는 재료와 필요한 배식 시간을 먼저 표시한다."),
            "survival:preserved_food": ("place:harnak", "하르나크의 여섯 도시는 보존 식량의 봉인 색을 맞춰 어느 도시의 창고에서도 유통기한을 알아보게 한다."),
            "survival:raw_food": ("place:harnak", "하르나크 장터는 날 식재료의 종족별 소유 구분을 버리고 조리 가능 시간과 오염 위험에 따라 나눈다."),
            "survival:tainted_food": ("place:harnak", "하르나크 위생대는 오염된 음식을 버린 자리와 함께 먹은 주민을 표시해 다음 환자를 찾는다."),
        }
        anchor, sentence = exact[stable_id]
        return exact_lore_link(item, anchor, "settlement", sentence)
    if prefix in {"waste", "industrial"}:
        sentence = {
            "industrial:sludge": "므네실라는 오수 슬러지의 금속, 독성과 유기물을 나눠 공동 정원으로 돌려보낼 몫만 따로 숙성한다.",
            "waste:animal-rot": "므네실라는 동물성 부패물에서 뼈와 지방을 먼저 걷어 사료, 연료와 퇴비의 흐름을 분리한다.",
            "waste:forbidden-rot": "므네실라는 금기 부패물을 별도 균사밭에 묻고 어디서 나온 재료인지 지우지 않은 채 분해 과정을 지켜본다.",
            "waste:mixed-rot": "므네실라의 정원지기는 혼합 부패물을 바로 퇴비로 쓰지 않고 독성과 포자 반응을 작은 흙밭에서 먼저 시험한다.",
            "waste:plant-rot": "므네실라는 식물성 부패물을 거둔 밭으로 되돌려 보내 다음 수확까지 흙이 회복되는 시간을 잰다.",
        }[stable_id]
        return exact_lore_link(item, "place:mnesila", "ecology", sentence)
    if prefix == "book":
        return exact_lore_link(item, "practice:harpy-chorus", "record", "아일라세라의 새벽 합창은 계절력 책자의 날짜가 빗나갈 때마다 실제 바람과 이동 시기를 노래로 바로잡는다.")
    return make_lore_link(item, "route:milosia", "trade")


def component_copy(item: ItemRow) -> str:
    name = item.title
    slug = item.stable_id.split(":", 1)[1]
    topic = with_topic(name)
    instrument = with_instrument(name)
    rules = (
        (("drawing", "plan"), f"{name}에 부품 치수와 조립 순서를 적어 제작자들이 같은 규격을 맞추게 한다."),
        (("lining", "padding"), f"{with_object(name)} 장비 안쪽에 덧대 충격과 마찰이 착용자에게 바로 닿지 않게 한다."),
        (("wiring", "conductor", "coupler"), f"{instrument} 동력과 마나가 끊기지 않도록 장치 사이의 연결부를 잇는다."),
        (("plate", "shield"), f"{with_object(name)} 장비와 설비의 바깥쪽에 덧대 충격과 마나 간섭을 받아 낸다."),
        (("gauge", "coupon"), f"{instrument} 완성품의 치수와 재료 상태가 허용 범위에 드는지 확인한다."),
        (("optics",), f"{topic} 먼 표적과 미세한 흔적을 확대해 조준과 검사를 돕는다."),
        (("counterweight",), f"{instrument} 무거운 장치의 하중을 맞춰 승강과 발사를 안정시킨다."),
        (("filter", "purification"), f"{topic} 물과 마나 흐름에 섞인 오염을 걸러 다음 공정으로 보낸다."),
        (("seal", "sealed"), f"{instrument} 용기와 장치의 경계를 닫아 내용물과 조율 상태를 보존한다."),
        (("joint", "frame", "shaft"), f"{topic} 움직이는 부품의 하중을 받아 동력과 힘을 다음 관절로 전달한다."),
        (("panel", "sensor"), f"{name}에서 장치의 상태를 읽고 필요한 조작을 한곳에서 처리한다."),
        (("kit", "package"), f"{name}에는 현장 조립과 보강에 필요한 작은 부품을 빠짐없이 모아 두었다."),
        (("parts", "manifold", "detonator"), f"{topic} 복잡한 설비의 작동 순서와 흐름을 맡는 핵심 조립품이다."),
        (("strap",), f"{instrument} 장비의 움직이는 부분을 몸과 골격에 단단히 고정한다."),
        (("hardener", "paste"), f"{with_object(name)} 바르거나 섞어 천과 종이 재료가 다음 제작 공정을 견디게 한다."),
    )
    for tokens, text in rules:
        if any(token in slug for token in tokens):
            return text
    return f"{topic} 완제품 안에서 한 가지 기능을 맡도록 규격을 맞춘 조립 부품이다."


def tool_copy(item: ItemRow) -> str:
    slug = item.stable_id.split(":", 1)[1]
    copies = {
        "administrative-seal": "행정 인장을 문서와 화물표에 찍어 승인한 사람과 처리 순서를 남긴다.",
        "alloy-crucible": "합금 도가니는 서로 다른 금속을 높은 열에서 녹이고 섞을 때 쓰는 내열 용기다.",
        "banquet-cart": "연회 운반 수레는 여러 접시와 술병을 한꺼번에 객석과 식탁으로 나른다.",
        "deep-shaft-hoist": "심부 승강기는 깊은 갱도에서 광석과 작업자를 줄과 권양기로 끌어올린다.",
        "field-repair-kit": "야전 수리 키트에는 정착지 밖에서 장비의 느슨한 결합과 파손을 응급 수리할 도구가 들어 있다.",
        "hauling-harness": "운반 멜빵은 짐의 무게를 어깨와 허리에 나눠 싣도록 끈과 고리를 연결한다.",
        "inspection-gauge": "검사 게이지로 생산품의 두께와 간격이 작업 규격에 맞는지 빠르게 가린다.",
        "maintenance-kit": "정비 키트에는 시설의 마모 부위를 조이고 닦고 교체하는 기본 공구가 모여 있다.",
        "mana-probe": "마나 탐침은 재료와 장치에 흐르는 마나의 방향과 불안정한 지점을 찾는다.",
        "powered-tool-head": "동력 공구날은 구동축에 갈아 끼워 절단과 굴착 작업을 맡기는 교체 부품이다.",
        "precision-gauge": "정밀 게이지는 눈으로 가리기 어려운 작은 오차까지 재어 고급 제작품을 검사한다.",
        "prisoner-work-kit": "포로 작업 도구는 허용된 작업에 필요한 공구만 추려 수량과 반납을 관리하는 꾸러미다.",
        "prospecting-kit": "탐광 키트에는 암석층을 깨고 표본을 담아 광맥의 방향을 찾는 도구가 들어 있다.",
        "reinforced-restraint": "강화 구속구는 손발의 움직임을 제한하고 잠금 상태를 점검할 수 있게 만든 장구다.",
        "rune-identification-lens": "룬 식별 렌즈는 닳거나 겹쳐진 각인을 확대해 계통과 제작 흔적을 읽는다.",
        "sewing-kit": "재봉 도구에는 의복을 만들고 찢어진 솔기를 고칠 바늘과 실, 가위가 들어 있다.",
        "watch-signal-horn": "경계 신호 나팔은 침입과 화재 같은 위험을 멀리 있는 주민에게 알린다.",
        "weather-observation-kit": "기상 관측 도구함으로 바람과 비, 기온의 변화를 기록해 농사와 원정을 준비한다.",
    }
    return copies[slug]


def body_part_name(slug: str) -> str:
    mapping = {
        "arm:left": "왼팔", "arm:right": "오른팔", "brain": "뇌", "core": "핵",
        "eye:left": "왼쪽 눈", "eye:right": "오른쪽 눈", "heart": "심장",
        "kidney:left": "왼쪽 신장", "kidney:right": "오른쪽 신장", "leg:left": "왼다리",
        "leg:right": "오른다리", "liver": "간", "lung:left": "왼쪽 폐",
        "lung:right": "오른쪽 폐", "pseudopods": "위족", "sensory-gel": "감각 젤", "stomach": "위",
    }
    return mapping[slug]


def surgery_copy(item: ItemRow) -> str:
    if item.stable_id == "surgery:contaminated-tissue":
        return "병변이나 감염 부위에서 떼어 낸 오염 조직이다. 밀봉해 폐기하거나 검사 표본으로 넘겨야 한다."
    if item.stable_id.startswith("surgery:organ:"):
        part = body_part_name(item.stable_id.removeprefix("surgery:organ:"))
        return f"수술로 적출한 {part}이다. 기증자 정보와 신선도가 남아 있어 이식 전까지 보존해야 한다."
    part = body_part_name(item.stable_id.removeprefix("surgery:prosthetic:"))
    return f"손상되거나 잃은 {part}의 기능을 대신하도록 신체에 연결하는 보철 부품이다."


def wildlife_copy(item: ItemRow, wildlife: dict[str, str]) -> str:
    if item.stable_id == "wild:rot":
        return "먹거나 가공할 수 없을 만큼 썩은 유기물이다. 방치하면 위생과 질병 관리에 부담이 된다."
    species_id = item.stable_id.removeprefix("wild:carcass:")
    name = item.title.removesuffix(" 사체")
    context = wildlife.get(species_id)
    if context:
        context = finish_sentence(context)
        return f"{context} 죽은 {name}의 사체는 도축 시설로 옮겨 식량과 부산물로 나눈다."
    return f"야생에서 회수한 {name}의 사체다. 도축 시설로 옮기면 식량과 부산물을 얻을 수 있다."


def generic_named_copy(item: ItemRow) -> str:
    name = item.title
    prefix = item.stable_id.split(":", 1)[0]
    slug = item.stable_id.split(":", 1)[1]
    topic = with_topic(name)
    instrument = with_instrument(name)
    if prefix == "fiber":
        material = name.removesuffix(" 원섬유")
        return f"{material}에서 뽑아 세척한 섬유다. 실로 잣기 전까지 엉킴과 오염을 피해서 보관한다."
    if prefix == "yarn":
        material = name.removesuffix(" 원사")
        return f"{material} 섬유를 꼬아 굵기를 맞춘 실이다. 직물과 의복을 짜는 다음 공정에 넘긴다."
    if prefix == "sample" and ":antigen:" in item.stable_id:
        disease = name.removesuffix(" 항원 표본")
        return f"{disease}의 특징을 확인할 수 있도록 밀봉한 항원 표본이다. 진단과 백신 연구에서 교차 오염을 막아야 한다."
    if prefix == "supply":
        supply_rules = {
            "alliance-signal-kit": "서로 다른 세력의 표식을 함께 올려 지원 요청과 집결 지점을 알리는 신호 도구다.",
            "botanical-pesticide": "재배 작물에 붙은 해충을 줄이도록 식물 성분을 우려 만든 살충제다.",
            "certified-seed-kit": "발아와 품종 기록을 확인한 종자를 파종 단위별로 나눠 담은 꾸러미다.",
            "defense-mixed-ammo-box": "방어 시설마다 다른 탄약을 즉시 꺼내 쓰도록 종류별로 칸을 나눈 상자다.",
            "funeral-preparation-kit": "여러 종족의 장례 절차에 필요한 천, 표식과 세정 도구를 함께 챙긴 준비품이다.",
            "fungicide": "포자와 곰팡이가 작물과 저장고에 번지는 것을 막는 살균제다.",
            "greenhouse-nutrient": "온실 작물이 흙이 부족한 환경에서도 자라도록 물에 타서 공급하는 영양액이다.",
            "inoculated-log": "먹을 수 있는 균사를 원목 속에 접종해 재배 준비를 마친 통나무다.",
            "mushroom-substrate": "버섯 균사가 뿌리내릴 수 있도록 유기물과 수분을 맞춘 재배 배지다.",
            "nitrate-fertilizer": "질소가 부족한 밭에 나눠 뿌려 작물의 성장을 돕는 비료다.",
            "performance-prop-box": "공연의 장면 전환과 연기에 쓰는 소도구를 순서대로 담은 상자다.",
            "pest-lure": "해충이 좋아하는 냄새로 작물에서 멀어진 덫 쪽으로 끌어들이는 유인제다.",
        }
        return supply_rules[slug]
    if prefix == "component":
        return component_copy(item)
    if prefix == "tool":
        return tool_copy(item)
    if prefix in {"medical", "medicine", "drug"}:
        medical_rules = (
            (("vaccine", "antigen"), f"{topic} 특정 감염에 대비해 면역 반응을 준비시키는 의료 물자다."),
            (("bandage", "patch", "splint"), f"{instrument} 상처와 손상 부위를 덮거나 고정해 치료를 돕는다."),
            (("disinfectant", "antiseptic", "sterile"), f"{topic} 치료 도구와 상처 주변의 오염을 줄이는 데 쓴다."),
            (("anesthetic",), f"{topic} 수술과 중상 치료 중 통증과 움직임을 낮추는 약품이다."),
            (("antidote",), f"{topic} 몸속 독성 물질을 중화하고 해독을 돕는 약품이다."),
            (("blood",), f"{topic} 출혈로 잃은 혈액을 보충하거나 혈관 손상을 처치할 때 쓴다."),
            (("organ", "regenerative", "whole-body"), f"{topic} 심한 조직 손상과 장기 회복을 지원하는 고급 의료 재료다."),
            (("isolation", "trauma", "emergency"), f"{name}에는 해당 상황에서 환자를 안정시키고 치료를 시작할 물자가 들어 있다."),
            (("trait-analysis",), f"{instrument} 환자의 신체 계통과 이식 적합성을 검사한다."),
            (("fertility", "rejuvenation"), f"{topic} 생식 기능이나 노화로 약해진 신체 상태를 치료하는 데 쓴다."),
            (("mana-core", "rune", "slime"), f"{topic} 일반 붕대가 맞지 않는 특수 신체 구조를 안정시키는 처치 도구다."),
        )
        for tokens, text in medical_rules:
            if any(token in slug for token in tokens):
                return text
        return source_copy(item) or f"{with_topic(name)} 진단과 치료 과정에서 사용하는 의료 물자다."
    if prefix in {"material", "resource", "craft", "textile"}:
        material_rules = (
            (("powder", "niter", "sulfur", "charcoal"), f"{topic} 화약과 연금 공정에서 반응을 일으키도록 건조해 둔 재료다."),
            (("steel", "iron", "gold", "alloy", "lead"), f"{topic} 무기, 방어구와 정밀 부품의 형태를 잡는 금속 재료다."),
            (("lumber", "wood", "log"), f"{topic} 구조물과 가구, 손잡이를 만들 수 있도록 길이와 결을 살려 둔 목재다."),
            (("cloth", "wool", "linen", "canvas", "silk", "hemp", "fiber", "cotton"), f"{topic} 의복과 안감, 생활용 천을 재단하는 직물 재료다."),
            (("leather", "hide"), f"{topic} 의복, 끈과 가벼운 방어구에 맞게 손질한 가죽 재료다."),
            (("paper", "paste"), f"{topic} 기록, 탄약과 도면 제작에 필요한 얇은 가공 재료다."),
            (("rope", "string", "thread"), f"{topic} 묶음과 장력, 재봉이 필요한 제작 공정에 쓰는 긴 섬유 재료다."),
            (("flour", "dough", "starch", "malt", "curd", "cheese"), f"{topic} 식재료를 한 차례 가공해 조리와 발효에 바로 쓸 수 있게 만든 재료다."),
            (("brined", "salted", "washed", "filling", "ration"), f"{topic} 오래 보관하거나 다음 조리를 빠르게 시작하도록 미리 손질한 식재료다."),
            (("alcohol", "liquor", "wine", "juice", "syrup"), f"{topic} 음료, 조리와 약품 제조에 나눠 쓰는 액체 재료다."),
            (("stone",), f"{topic} 벽과 바닥, 내열 설비의 무게를 받치는 석재다."),
            (("blood", "bone", "fang", "horn", "feather", "fat"), f"{topic} 사냥과 도축에서 얻어 음식, 의식과 제작에 나눠 쓰는 생물 재료다."),
            (("water",), f"{topic} 음용과 조리, 세척 및 여러 생산 공정에 공급한다."),
            (("manure", "compost"), f"{topic} 밭과 재배 배지에 섞어 토양의 양분을 보충하는 유기 재료다."),
            (("ore",), f"{topic} 제련을 거쳐 금속 재료로 바꾸기 전의 광석이다."),
        )
        for tokens, text in material_rules:
            if any(token in slug for token in tokens):
                return text
    return source_copy(item) or f"{with_topic(name)} 정착지의 생산과 생활에 사용하는 물자다."


def source_needs_replacement(description: str) -> bool:
    return description in GENERIC_SOURCE_DESCRIPTIONS or any(
        fragment in description
        for fragment in (
            "분기형 생산망의",
            "작업실의 다음 공정",
            "물리 원단",
            "물리 물품",
            "물리 종자 로트",
            "실제 부품과 조립 자재",
            "수술로 설치하는 고유 보철",
            "기증자와 신선도가 보존되는 고유 수술 장기",
        )
    )


def build_descriptions(
    items: dict[str, ItemRow],
    current: dict[str, str],
) -> tuple[dict[str, str], dict[str, LoreLink]]:
    if set(items) != set(current):
        raise ValueError(f"item/catalogue key mismatch: source={len(items)}, catalogue={len(current)}")
    buildings = load_buildings()
    combat_sources = load_combat_sources()
    wildlife = {row["stable_id"]: row["description"].strip() for row in read_csv(WILDLIFE_CSV_PATH)}
    result: dict[str, str] = {}
    lore_links: dict[str, LoreLink] = {}
    for stable_id in sorted(items):
        item = items[stable_id]
        if stable_id in SPECIAL_ITEM_COPY:
            description = SPECIAL_ITEM_COPY[stable_id]
        elif stable_id.startswith("facility-kit:"):
            description = facility_copy(item, buildings)
        elif stable_id.startswith("equipment-item:"):
            description = equipment_copy(item, combat_sources)
        elif stable_id.startswith("apparel:"):
            description = apparel_copy(item)
        elif stable_id.startswith("evolution:catalyst:"):
            description = catalyst_copy(item)
        elif stable_id.startswith("evolution:residue:"):
            description = residue_copy(item)
        elif stable_id.startswith("surgery:"):
            description = surgery_copy(item)
        elif stable_id.startswith("wild:"):
            description = wildlife_copy(item, wildlife)
        elif stable_id.startswith("food:"):
            description = FOOD_COPY[stable_id.split(":", 1)[1]]
        elif stable_id.startswith("feed:"):
            description = FEED_COPY[stable_id.split(":", 1)[1]]
        elif stable_id.startswith("ammo:"):
            description = AMMO_COPY[stable_id.split(":", 1)[1]]
        elif stable_id.startswith("component:"):
            description = component_copy(item)
        elif stable_id.startswith("tool:"):
            description = tool_copy(item)
        elif stable_id.startswith("seed-lot:"):
            crop = item.title.removesuffix(" 종자 로트")
            description = f"{crop}의 품종과 품질, 병원체 검사 기록을 함께 보존한 파종용 종자 묶음이다."
        elif stable_id in {"material:mending-scrap", "material:sewing-thread"}:
            if stable_id.endswith("mending-scrap"):
                description = "의복의 작은 구멍과 닳은 가장자리를 덧대기 좋게 크기를 맞춘 수선용 천 조각이다."
            else:
                description = "의복을 새로 짓거나 터진 솔기를 꿰맬 때 쓰도록 굵기와 꼬임을 맞춘 재봉실이다."
        else:
            source = source_copy(item)
            if source_needs_replacement(item.source_description) or len(source) < 16:
                source = generic_named_copy(item)
            description = source
        lore_link = item_lore_link(item, buildings)
        lore_links[stable_id] = lore_link
        result[stable_id] = re.sub(r"\s+", " ", f"{description} {lore_link.sentence}").strip()
    return result, lore_links


def normalized_opening(item: ItemRow, description: str) -> str:
    first = description.split(".", 1)[0]
    first = first.replace(item.title, "<항목>")
    first = re.sub(r"\d+(?:\.\d+)?", "<수치>", first)
    return re.sub(r"\s+", " ", first).strip()


def normalized_lore_frame(item: ItemRow, sentence: str) -> str:
    result = sentence
    names = {item.title, item.title.removesuffix(" 설치 키트")}
    for name in sorted((value for value in names if value), key=len, reverse=True):
        result = result.replace(name, "<항목>")
    result = re.sub(r"<항목>[은는이가을를]", "<항목><조사>", result)
    result = re.sub(r"\d+(?:\.\d+)?", "<수치>", result)
    return re.sub(r"\s+", " ", result).strip()


def invalid_name_particles(item: ItemRow, sentence: str) -> list[str]:
    errors: list[str] = []
    names = {item.title, item.title.removesuffix(" 설치 키트")}
    for name in (value for value in names if value):
        code = ord(name[-1])
        final = (code - 0xAC00) % 28 if 0xAC00 <= code <= 0xD7A3 else 0
        for particle in re.findall(re.escape(name) + r"(으로|은|는|이|가|을|를|로)", sentence):
            invalid = (
                (particle == "은" and final == 0)
                or (particle == "는" and final != 0)
                or (particle == "이" and final == 0)
                or (particle == "가" and final != 0)
                or (particle == "을" and final == 0)
                or (particle == "를" and final != 0)
                or (particle == "으로" and final in (0, 8))
                or (particle == "로" and final not in (0, 8))
            )
            if invalid:
                errors.append(name + particle)
    return errors


def validate_descriptions(
    items: dict[str, ItemRow],
    descriptions: dict[str, str],
    lore_links: dict[str, LoreLink],
) -> dict[str, object]:
    errors: list[str] = []
    if set(items) != set(descriptions):
        errors.append("authority keys do not match item keys")
    if set(items) != set(lore_links):
        errors.append("lore links do not match item keys")
    duplicates = [text for text, count in Counter(descriptions.values()).items() if count > 1]
    if duplicates:
        errors.append(f"duplicate descriptions: {duplicates[:3]}")
    for stable_id, description in descriptions.items():
        if not description or len(description) < 16:
            errors.append(f"description is too short: {stable_id}")
        if any(fragment in description for fragment in BANNED_FRAGMENTS):
            errors.append(f"banned template fragment: {stable_id}")
        if "—" in description or "–" in description:
            errors.append(f"forbidden dash character: {stable_id}")
        lore = lore_links.get(stable_id)
        if lore is None:
            continue
        if lore.anchor_id not in LORE_ANCHOR_TOKENS:
            errors.append(f"unknown lore anchor: {stable_id} -> {lore.anchor_id}")
            continue
        if lore.story_layer not in {"everyday", "clue"}:
            errors.append(f"invalid story layer: {stable_id} -> {lore.story_layer}")
        if not lore.connection:
            errors.append(f"missing lore connection: {stable_id}")
        if not lore.sentence or not description.endswith(lore.sentence):
            errors.append(f"description does not carry its reviewed lore sentence: {stable_id}")
        if not any(token in lore.sentence for token in LORE_ANCHOR_TOKENS[lore.anchor_id]):
            errors.append(f"lore sentence does not name its anchor: {stable_id}")
        if lore.story_layer == "everyday" and any(term in description for term in EARLY_REVEAL_TERMS):
            errors.append(f"early item text leaks the central reveal: {stable_id}")
        bad_particles = invalid_name_particles(items[stable_id], lore.sentence)
        if bad_particles:
            errors.append(f"invalid Korean particle after item name: {stable_id} -> {bad_particles}")
    frames = Counter(normalized_opening(items[stable_id], text) for stable_id, text in descriptions.items())
    anchors = Counter(link.anchor_id for link in lore_links.values())
    connections = Counter(link.connection for link in lore_links.values())
    story_layers = Counter(link.story_layer for link in lore_links.values())
    lore_sentences = Counter(link.sentence for link in lore_links.values())
    lore_frames = Counter(
        normalized_lore_frame(items[stable_id], link.sentence)
        for stable_id, link in lore_links.items()
    )
    reporting_style_terms = {
        term: sum(term in link.sentence for link in lore_links.values())
        for term in REPORTING_STYLE_TERMS
    }
    description_lengths = [len(text) for text in descriptions.values()]
    if len(lore_frames) < 250:
        errors.append(f"lore prose has too few distinct semantic frames: {len(lore_frames)}")
    if lore_frames and lore_frames.most_common(1)[0][1] > 20:
        errors.append(f"lore prose repeats one semantic frame too often: {lore_frames.most_common(1)[0]}")
    overused_reporting_terms = {
        term: count
        for term, count in reporting_style_terms.items()
        if count > MAX_REPORTING_STYLE_TERM_COUNT
    }
    if overused_reporting_terms:
        errors.append(f"lore prose overuses report-like vocabulary: {overused_reporting_terms}")
    average_description_chars = sum(description_lengths) / len(description_lengths)
    if average_description_chars > MAX_AVERAGE_DESCRIPTION_CHARS:
        errors.append(f"item prose is too long on average: {average_description_chars:.1f}")
    if max(description_lengths) > MAX_DESCRIPTION_CHARS:
        errors.append(f"an item description is too long: {max(description_lengths)}")
    return {
        "errors": errors,
        "item_count": len(descriptions),
        "unique_description_count": len(set(descriptions.values())),
        "distinct_opening_frame_count": len(frames),
        "largest_opening_frames": [
            {"count": count, "frame": frame} for frame, count in frames.most_common(10)
        ],
        "banned_fragment_count": sum(
            1 for text in descriptions.values() if any(fragment in text for fragment in BANNED_FRAGMENTS)
        ),
        "world_grounded_count": sum(
            1
            for stable_id, link in lore_links.items()
            if stable_id in descriptions
            and descriptions[stable_id].endswith(link.sentence)
            and any(token in link.sentence for token in LORE_ANCHOR_TOKENS.get(link.anchor_id, ()))
        ),
        "missing_lore_count": len(set(items).difference(lore_links)),
        "distinct_lore_sentence_count": len(lore_sentences),
        "distinct_lore_frame_count": len(lore_frames),
        "largest_lore_frames": [
            {"count": count, "frame": frame} for frame, count in lore_frames.most_common(10)
        ],
        "reporting_style_term_counts": reporting_style_terms,
        "average_description_chars": round(average_description_chars, 1),
        "max_description_chars": max(description_lengths),
        "lore_anchor_counts": dict(sorted(anchors.items())),
        "lore_connection_counts": dict(sorted(connections.items())),
        "story_layer_counts": dict(sorted(story_layers.items())),
    }


def write_authority(
    items: dict[str, ItemRow],
    descriptions: dict[str, str],
    lore_links: dict[str, LoreLink],
    report: dict[str, object],
) -> None:
    payload = {
        "schema_version": 2,
        "language": "ko-KR",
        "description_authority": "player-facing immutable item prose",
        "lore_contract": "Every item names one reviewed place, polity, route, culture or practice. Everyday goods stay outside the central reveal.",
        "item_count": len(descriptions),
        "quality": {key: value for key, value in report.items() if key != "errors"},
        "items": [
            {
                "stable_id": stable_id,
                "display_name": items[stable_id].title,
                "description": descriptions[stable_id],
                "lore_anchor": lore_links[stable_id].anchor_id,
                "lore_connection": lore_links[stable_id].connection,
                "story_layer": lore_links[stable_id].story_layer,
                "lore_sentence": lore_links[stable_id].sentence,
            }
            for stable_id in sorted(descriptions)
        ],
    }
    AUTHORITY_PATH.parent.mkdir(parents=True, exist_ok=True)
    AUTHORITY_PATH.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def load_authority() -> tuple[dict[str, str], dict[str, str], dict[str, LoreLink]]:
    payload = json.loads(AUTHORITY_PATH.read_text(encoding="utf-8"))
    if payload.get("schema_version") != 2 or payload.get("language") != "ko-KR":
        raise ValueError("item narrative authority schema is invalid")
    descriptions: dict[str, str] = {}
    titles: dict[str, str] = {}
    lore_links: dict[str, LoreLink] = {}
    for row in payload.get("items", []):
        stable_id = row["stable_id"]
        if stable_id in descriptions:
            raise ValueError(f"duplicate authority item: {stable_id}")
        descriptions[stable_id] = row["description"].strip()
        titles[stable_id] = row["display_name"].strip()
        lore_links[stable_id] = LoreLink(
            anchor_id=row.get("lore_anchor", "").strip(),
            connection=row.get("lore_connection", "").strip(),
            story_layer=row.get("story_layer", "").strip(),
            sentence=row.get("lore_sentence", "").strip(),
        )
    if payload.get("item_count") != len(descriptions):
        raise ValueError("item narrative authority count is invalid")
    return descriptions, titles, lore_links


def sync_catalog(source: str, descriptions: dict[str, str]) -> str:
    seen: set[str] = set()

    def replace(match: re.Match[str]) -> str:
        if match.group("kind") != "0":
            return match.group(0)
        stable_id = match.group("stable_id").strip()
        if stable_id not in descriptions:
            raise ValueError(f"catalogue item is absent from prose authority: {stable_id}")
        seen.add(stable_id)
        encoded = json.dumps(descriptions[stable_id], ensure_ascii=True)
        return (
            f"  - kind: 0\n"
            f"    stableId: {stable_id}\n"
            f"    inGameDescription: {encoded}"
        )

    updated = ENTRY_PATTERN.sub(replace, source)
    if seen != set(descriptions):
        raise ValueError("not every authority item was synchronized to the catalogue")
    return updated


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--refresh-authority", action="store_true", help="rebuild the reviewable JSON prose authority")
    parser.add_argument("--sync", action="store_true", help="copy the JSON authority into the Unity catalogue asset")
    parser.add_argument("--check", action="store_true", help="validate authority quality and exact runtime synchronization")
    args = parser.parse_args()
    if not (args.refresh_authority or args.sync or args.check):
        parser.error("select at least one operation")

    items = load_items()
    catalogue_source, current = load_catalog(CATALOG_PATH)
    if args.refresh_authority:
        descriptions, lore_links = build_descriptions(items, current)
        report = validate_descriptions(items, descriptions, lore_links)
        if report["errors"]:
            raise ValueError("; ".join(report["errors"][:10]))
        write_authority(items, descriptions, lore_links, report)

    authority, titles, lore_links = load_authority()
    if any(titles.get(stable_id) != item.title for stable_id, item in items.items()):
        raise ValueError("item titles in prose authority are stale")
    report = validate_descriptions(items, authority, lore_links)
    if report["errors"]:
        raise ValueError("; ".join(report["errors"][:10]))

    if args.sync:
        catalogue_source = sync_catalog(catalogue_source, authority)
        CATALOG_PATH.write_text(catalogue_source, encoding="utf-8")
        _, current = load_catalog(CATALOG_PATH)

    if args.check and current != authority:
        missing = sorted(set(authority).difference(current))
        stale = sorted(stable_id for stable_id in set(authority).intersection(current) if authority[stable_id] != current[stable_id])
        raise ValueError(f"runtime catalogue differs from prose authority: missing={missing[:3]}, stale={stale[:3]}")

    digest = hashlib.sha256(
        "\n".join(f"{stable_id}\t{authority[stable_id]}" for stable_id in sorted(authority)).encode("utf-8")
    ).hexdigest()
    print(json.dumps({**report, "digest": digest}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, json.JSONDecodeError) as error:
        print(f"item narrative synchronization failed: {error}", file=sys.stderr)
        raise SystemExit(1)
