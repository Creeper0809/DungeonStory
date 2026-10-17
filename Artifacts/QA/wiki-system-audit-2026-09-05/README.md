# 시스템·위키 설명 전수 감사

상태: **2026-09-14 기존 GAP 275건 정적 재검토·문서 수정 완료**. 현재 활성254건(유효34·범위수정220), 제외 이력21건(철회14·병합4·근거미충족3)이다. 현재 판정·공개 범위는 `missing-register.json`과 동일 Markdown이 권위이며 아래 2026-09-08 집계·개별 조사 보고서는 과거 이력이다. Unity 실행 검증이나 공개 위키 게시를 완료했다는 뜻은 아니다.

## 2026-09-14 정정 결과

- 기존275개 ID 전부 현재 F 소스·작성 에셋·공개 위키와 재대조했다.267개 항목에서 본문·근거·공개 범위 등의 필드가 변경됐으며, 이는267개 전부가 독립적인 사실 오류였다는 뜻은 아니다.
- GAP-118의 이미 공개된 의료 설명과 GAP-148의 비활성 사건 자동발동 주장은 철회해 제외 이력으로 옮겼다. GAP-014·109·257은 근거 미충족으로 계속 보류한다.
- 번식11종, 수면 규칙, 축제 실제 진행, 시설별 급유, 수술 시설 제한, 초기29열 등 변경·오해된 설명을 바로잡았다. 병합201/202의 실제 수치는081/082에 복원했다.
- 게임 예외 규칙은 작성 대상으로 복원하고, 내부 코드·미확인 일반화는 제외했다.111·163의 내부 설명 삭제 지시는 공개 원고가 아닌 문서 정정 조치로 분리했다.
- 현재 근거770파일의 경로·행·해시, ID275 합집합·활성/이력 분리, Markdown/JSON·집계를 검증했다. GAP-042의 과거 가격 차이198개도 현재 원본/위키198개 모두 일치한다. 전체1075개 가격 신규 감사는 아니다.

## 2026-09-08 GAP 재검토 결과

- 활성 원장: 유효44건 + 확인 범위로 본문을 수정한212건 = 256건.
- [제외 이력](missing-register-history-2026-09-08.md): 철회12건·중복 병합4건·근거 미충족3건 = 19건.
- 범위 수정 항목은 기존 과장·낡은 설명을 제거하고 현재 확인한 차이와 수정 내용만 본문에 남겼다.
- 활성 원장의 각 항목은 `위키에 작성할 정보`와 `위키에 작성하지 않을 정보`를 분리한다. 목록 간 충돌은 자동 우선순위로 숨기지 않고 근거로 해결해야 한다. 근거 경로·코드 심볼·감사 메타데이터는 공개 원고가 아니다.
- 아래의 2026-09-05 조사 일지에 적힌 과거 GAP 개수·잠정 판정·미완료 표시는 이력이다. 현재 판정 집계로 사용하지 않는다.

## 최종 권위

- [위키 → 구현 누락·부분·의미 차이 원장](wiki-to-implementation-missing-register.md) · [동일 JSON](wiki-to-implementation-missing-register.json): WIM-001~063, 구현 보완60건과 documentation-only3건. WIM-062~063의 구현/위키 처분은 아직 결정 대기다. 기존 구조화 전수 뒤 narrative 의미 소비 후속을 다시 열었다. `missing-register.*`의 반대 방향이다.
- [공개 엔티티 facts·relations 전수 대조](public-entity-claims-runtime-review.md): 공개 엔티티2,905개, facts6,364개, relations3,846개, 총 claim10,210개와 56개 원본 타입별 production 소비자. 가격198·시설요약144·기후 의미5 차이를 WIM-058~060으로 귀속했다.
- [WIM 근거 최신성·중복 전수 감사](wim-evidence-and-dedup-review.md): ID 연속성, DIFF-001~044 1:1 연결, 직접 근거11종, 현재 원본 경로325개·누락0, 중복 root 검토, 저장/UI/Play Mode 증거 경계.
- [문서 검토 원장](wiki-review.md): 가이드30/30, 욕구8, 업무42, 신체 기본12·특수35·그룹10의 본문 완독과 원본 대조 완료. 미검토 모집단0.
- [구현 → 위키 활성 GAP](missing-register.md) · [동일 JSON](missing-register.json): 확인된254건(기존 GAP ID 유지). 이 원장은 WIM과 합산하지 않으며, 과거 개별 조사 근거로 GAP-166~168은 [시설 이용 완료 효과 대조](facility-use-effects-review.md), GAP-169는 [보안 시설·경계 신호 나팔 대조](security-facility-signal-horn-review.md), GAP-170은 [장비 정비 런타임 대조](equipment-maintenance-runtime-review.md), GAP-171은 [골렘 충전 런타임 대조](golem-recharge-runtime-review.md), GAP-172~173은 [서비스 허브·지원시설·의료 물류 대조](service-room-runtime-review.md), GAP-174는 [연료 소비시설 런타임 대조](fuel-consumer-runtime-review.md), GAP-175는 [생산 보조 대체연료 선택 대조](facility-supply-selection-runtime-review.md), GAP-176은 [보존 부품·같은 방 조리 대조](preservation-room-cooking-runtime-review.md), GAP-177은 [재활·룬봉합 수술시설 대조](rehabilitation-surgical-facility-runtime-review.md), GAP-178은 [해부대 수술시설 대조](anatomy-table-surgical-facility-runtime-review.md), GAP-179는 [마취 장치 수술시설 대조](anesthesia-surgical-facility-runtime-review.md), GAP-180은 [세정대 수술시설 대조](sterilization-surgical-facility-runtime-review.md), GAP-181은 [비전 개조대 수술시설 대조](arcane-surgery-facility-runtime-review.md), GAP-182는 [장기 보관함 런타임 대조](organ-storage-runtime-review.md), GAP-183은 [보철 조립대 수술지원·제작품 품질 대조](prosthetic-assembly-facility-runtime-review.md), GAP-184는 [이식 지원시설 수술·재료·거부반응 대조](transplant-support-facility-runtime-review.md), GAP-185는 [수술대 7시설 primary·보정·환자슬롯 대조](surgery-table-facility-runtime-review.md), GAP-186은 [일반 생산 ability 물리 stock 출력 대조](production-ability-stock-output-runtime-review.md), GAP-187은 [조명 설비 환경 조도·시각 clipping 대조](lighting-facility-runtime-review.md), GAP-188은 [외부 휴식처 Rest·기분·원정 스트레스 대조](outdoor-rest-runtime-review.md), GAP-189는 [시설 진화 기여 room profile·환경 소비 대조](facility-evolution-contribution-runtime-review.md)가 소유한다.
- GAP-190은 [대장작업대 장비 제작 주문·품질·저장 대조](equipment-crafting-facility-runtime-review.md)가 소유한다.
- GAP-191은 [서커스 관람석 관객·입장·수익 대조](circus-audience-seating-runtime-review.md)가 소유한다.
- GAP-192는 [야수 우리 수용·돌봄·축산 대조](beast-pen-runtime-review.md)가 소유한다.
- GAP-193은 [감방 구속대 수용·복원 대조](captive-housing-runtime-review.md)가 소유한다.
- GAP-194는 [서커스 매표소 수익 대조](circus-ticket-booth-runtime-review.md)가 소유한다.
- GAP-195는 [서커스 도박 창구 수익·만족도 변동 대조](circus-gambling-booth-runtime-review.md)가 소유한다.
- GAP-196은 [서커스 진행자 단상 만족도·준비 작업 대조](circus-announcer-runtime-review.md)가 소유한다.
- GAP-197은 [공연 위험 장치 사고·만족도 대조](circus-hazard-runtime-review.md)가 소유한다.
- GAP-198은 [공연 치료 구역 사고 피해 대조](circus-treatment-zone-runtime-review.md)가 소유한다.
- GAP-199는 [공개 형벌 장치 조건부 효과 대조](public-punishment-device-runtime-review.md)가 소유한다.
- GAP-200은 [중앙 무대 공연 주문 대조](circus-stage-runtime-review.md)가 소유한다.
- GAP-201은 [공기 교환·덕트 런타임 대조](air-exchange-and-duct-runtime-review.md)가 소유한다.
- GAP-202는 [열 방출기 런타임 대조](thermal-emitter-runtime-review.md)가 소유한다.
- GAP-203은 [청소 시설 런타임 대조](cleaning-facility-runtime-review.md)가 소유한다.
- GAP-204는 [도축 시설 런타임 대조](butcher-facility-runtime-review.md)가 소유한다.
- GAP-205는 [조리 시설 런타임 대조](cooking-facility-runtime-review.md)가 소유한다.
- GAP-206은 [전투 엄폐 시설 런타임 대조](combat-cover-facility-runtime-review.md)가 소유한다.
- GAP-207은 [방어 시설 런타임 대조](defense-facility-runtime-review.md)가 소유한다.
- GAP-208은 [급수 시설 런타임 대조](water-source-facility-runtime-review.md)가 소유한다.
- GAP-209는 [환기 시설 런타임 대조](ventilation-facility-runtime-review.md)가 소유한다.
- 기준: branch `main`, HEAD `c03d2e0a1c68067de48840f3851b388483966a6e`, content source digest `76cce09a76ded556dc74ac400c571e92136f17f86fc3697dfb9eb30dca669871`.

이 아래의 “진행 중/남은 조사” 문구는 절편별 조사 당시의 역사 기록이다. 현재 위키 → 구현 상태는 위 최종 권위와 `final-audit-closure.json`이 소유하며 narrative 후속은 다시 진행 중이다.

## main 병합 후 추가 대조

- [위키 주장과 구현의 차이](wiki-implementation-differences.md): DIFF-001~047의 정적 근거. WIM-001~044는 1:1 연결되고 DIFF-045는 WIM-045, DIFF-046은 WIM-061, DIFF-047은 WIM-062에 통합했다. 실제 Unity 재현 완료 건수와 혼동하지 않는다.
- [재고 정책·자동 판매·소매 운영 후속](economy-stock-policy-retail-review.md): 품목별10/20/40 정책·비상비축·초과처리5종과 상점 exact lot·보충·계산대·대기·절도를 GAP-139~140으로 기록했다. [직접 근거16개](economy-stock-policy-retail-evidence.json). 가격/품질 수치의 정상 일치와 broad `BuildingCategory.Shop`을 실제 retail ability로 오인하지 않도록 구분했다.
- [세력 계약·교역 경로 후속](faction-contract-route-review.md): authored 계약18개와 공개18개, 별도 세력 route6개·화물36행을 대조했다. 계약 gameplay 진입점 부재, 전역 스택 직접 소비, 교역 유료/보급 무상 정책 차이를 GAP-133~136·DIFF-032~034로 구분했다. [직접 근거52개](faction-contract-route-evidence.json). 실제 UI 클릭·저장 왕복은 미실행이다.
- [경제·고용·자동 조달 후속](economy-employment-procurement-review.md): 중앙 금화/금고 설명 불일치, 직원·용병 급여와 체불, 자동구매 보호금·실행 순서, 별도 지역 공급 계약을 GAP-127~130으로 기록했다. [25개 원본 해시·독립 산술9건](economy-employment-procurement-evidence.json). 작성 유료시설0개를 실제 콘텐츠로 세지 않았으며 게임 실행 검증은 미실행이다.
- [의료 두 경로·약품 전수 교차 감사](general-medical-cross-path-review.md): 구조/생활시설 치료의 대상·WU·소비·효과 차이, 이송/안정화 순서 및 해독 효과 연결 미확인. [22약품·10시설140필드](general-medical-authoring-review.json)·[현재 근거와 카탈로그22연결](general-medical-cross-path-evidence.json). 기존 GAP-123의 6종 표 밖 균사 배양 팩까지 포함하면 구조 치료 후보는7종이다. 전역 미사용으로 보류했던 workSeconds는 생활시설 치료 업무에서 소비됨을 확인했다.
- [의료·수술·전투 후속](medical-combat-followup-review.md): 수술47개 작업/효과개수94값·소재12개60배율은 일치. 수혈·긴급봉합·일부 이식명칭의 실제 효과 차이, 질량계산 경로 차이 및 수술조건 설명 누락을 구분했다. [47개 전수 필드](surgery-projection-followup-review.json)·[소재/종족별 절차](combat-material-and-species-procedure-review.json)·[근거 검증](medical-combat-followup-evidence.json).
- [농업·축산·원정 환경 연계](ecology-travel-followup-review.md): 작물 12개·기본 36값의 위키 일치, 실내 성장조건·축산 계절·원정 날씨·야생 종별 이동 차이 4건 추가. 정상 연결과 미확인을 분리했다.
- [이번 원본 근거 48개](ecology-travel-followup-evidence.json): main c03d2e0a의 파일 해시. 해시는 완독/실행 증거를 뜻하지 않는다.
- [수술 절차·위험·실패 후속 감사](surgery-mechanics-review.md): 절차47개 조건/효과 분포, 실제 위험·환경·실패 상태와 도감 문장 오류를 분리했다.
- [수술 절차 전수 투영](surgery-projection-followup-review.json): 작성 자산47개와 공개 도감47개의 작업량·효과 개수·조건 원본을 보존한다.
- [일반 구조·안정화·치료 주문 후속](general-medical-order-review.md): 다운 환자 의료 주문의 순서·WU·중단/반복, 부상 약품6종·의료 ability10곳과 추출혈 대체를 대조했다.
- [전투 장비·개량 모듈 도감 후속](combat-equipment-entry-review.md): 장비61종·모듈20종의 원본/공개 모집단과 도감 facts, 모듈 획득·호환·등급 배율 소비를 대조했다.
- [야생동물 18종·축산 프로필 도감 후속](wildlife-species-entry-review.md): 종18개·관계69행의 대응과 전부 빈 facts, 계절·축산 필드의 실제 소비/미연결을 구분했다.
- [작물 유전체·종자 계승 후속](crop-genome-review.md): 유전체32종의 6 locus·tradeoff·공개 빈 facts를 전수 대조하고 phenotype, 돌연변이, 물리 종자 lot, cultivar 상한과 수확 재시도 권위를 구분했다.
- [주민 식사 소비·효과 후속](meal-consumption-review.md): 실제 식사22종의 공개 필드, 식단·품질·문화 선택, 배달·물리 commit·효과·ledger/save를 대조했다. 사체/placeholder25개와 음료2개를 식사로 오분류하지 않았고, 기본 Fine 상한 때문에 호화식2종이 자동 경로에서 막히는 차이를 DIFF-035로 분리했다.
- [음료·기호물질·중독 후속](substance-consumption-review.md): 작성/공개 물질9종과 정책·AI 복용·술음료장 배송·내성/중독/금단·작업/전투 배율·save를 대조해 GAP-141~142로 기록했다. 구체적 피로/연구·비전/통증 문구와 범용 효과의 차이는 DIFF-036으로 묶고 질병 대응 GAP-020과 중복하지 않았다.
- [고급 금고·장비 강화·원정 자금 후속](advanced-treasury-expedition-economy-review.md): 오버클럭·정밀 재단조·금고 연동 방어·원정 현장 자금·외부 영향 결제의 실제 UI/효과/저장을 GAP-143~147로 기록했다. [직접 근거25개](advanced-treasury-expedition-economy-evidence.json). 호출자 없는 촉매 경제 API와 작성 에셋에 저장되지 않은 BribeOffer는 현재 기능으로 과장하지 않았다.
- [사회 사건·손님 요청·서비스 사고 후속](society-event-scheduler-review.md): 작성/공개 사건54개를 대조하고 개별 기한·빈도·요구·선택·효과와 실제 일일 scheduler를 GAP-148~149로 기록했다. 위키의 운반 작업·발생 간격·동시 상한·경보 UI/요약과 현재 구현의 차이는 DIFF-037~039로 분리했다. [정적 근거와 모집단 digest](society-event-scheduler-evidence.json). 실제 사건 발생 Play Mode는 미실행이다.
- [축제16개·사회 사건 의미 후속](society-events-festivals-review.md): V20 축제16개 중 공개12개와 중복 제외4개, 날짜·시설·준비물·참가자·등급·효과 실행을 GAP-150~151로 기록했다. 사건의 구체 원인/도메인 operation 공백은 DIFF-040, 문화별 참가·단식 연결 공백은 DIFF-041로 분리했다.
- [시작 파티·난이도·생존 압박](starting-party-runtime-review.md): 3인 구성·같은 종족 후보6·원정 금지·숙련/연령 수치를 확인하고 GAP-002의 수동 선택 해석을 자동 확정 계약으로 정정했다. 준비 화면의 건강·식단·수면·기후 정보 부재는 DIFF-042로 분리했다.
- [31개 업무와 사건 작업 지연 매핑](work-delay-mapping-review.md): 런타임 업무31개와 공개 정성 업무42개를 구분하고 WorkDelay83행을 전수 대조했다. GAP-093을 정확한 범위로 보강하고 범람의 Haul-only 범위와 은퇴 요청 -2 no-op을 DIFF-043~044로 분리했다.
- [방 환경·역할·문화 선호·문 권한 후속](room-and-door-runtime-review.md): 방4점수와5개 업무 속도·기분, 역할13종·수용력, 문7집단·5프리셋·저장, 문화10종 방 선호와 시설 점수, 방 condition과 특성201/222/245 반응을 GAP-152~156으로 기록했다. 마나실의 마나두창 노출은 기존 GAP-022를 보강했고, “닫힌 문”의 물리 차단 설명과 정책 기반 통과 구현의 차이는 DIFF-045로 분리했다.
- [특성100종 효과·규칙 투영 후속](trait-projection-review.md): 생산 카탈로그100개와 공개100개를 exact 대조하고 effect167·조건부50·identity rule108의 실제 payload가 개수로만 축약된 공백을 GAP-157로 기록했다. 디스크의 retired13개는 공개 누락으로 오계수하지 않았다.
- [극단 특성 실행·저장 후속](extreme-traits-runtime-review.md): 사선 각성·기적의 집도·황금 수확·한계 돌파·마력 과충전의 실제 명령, 결정론 판정, 물리 비용/commit, 상태 전이와 저장을 GAP-158~162로 기록했다. 특성 공통 payload GAP-157과 기존 trait300/302 소유자는 중복 계수하지 않았다.
- [내부 런타임 시설 공개 투영 후속](runtime-facility-publication-review.md): 음수 ID·unlocked=false인 외부 zone40개와 월드 작업 target2개가 일반 facility page42개로 노출되는 문제를 GAP-163으로 기록했다. layer별 복제와 placeholder18WU/BOM을 플레이어 건설 시설로 세지 않았다.
- [외부 활동 구역·사건 런타임 후속](exterior-activity-runtime-review.md): 자동 생성 외부 구역8종의20초 마모·5업무·원정 출입과180초 외부 사건6종의 확률·실물 결과·UI/save를 GAP-164~165로 기록했다.
- [시설 이용 완료 효과 후속](facility-use-effects-review.md): active 욕구 회복19종·훈련4종·원정 회복4종의 작성 payload, 실제 완료 경로와 캐릭터 저장을 GAP-166~168로 기록했다.
- [보안 시설·경계 신호 나팔 후속](security-facility-signal-horn-review.md): 보안 시설6개·물리 나팔의 침입 집결+6초/내구도1과 producer-only alarm charge를 대조하고 GAP-169, DIFF-046/WIM-061로 분리했다.
- [정비 부품함 장비 수리 후속](equipment-maintenance-runtime-review.md): 방어구·방패 수리 정책3종, 물리 장비/키트 운반, 작업·재료 공식, 침공 대기, 원자 완료·저장·UI를 GAP-170으로 기록했다. authored 동시2슬롯은 production consumer0인 구현 고아라 효과로 설명하지 않는다.
- [골렘 충전 후속](golem-recharge-runtime-review.md): M02의 charge35 이하·마나결정1·100WU·charge+50, 25미만 기분과0 방전 피해, 예약·저장 경계를 GAP-171로 기록했다. deprecated P1은 migration 호환 자산이라 공개 누락으로 중복하지 않는다.
- [서비스 허브·지원시설·의료 물류 후속](service-room-runtime-review.md): 허브8·지원20·프로세스5의 세 모드/연구/stage/가격·만족/결제/저장/UI와 같은 방 링크·효과를 GAP-172~173으로 기록했다. 의료 창고 stack의 현장 Sink와 공개 배송 문장 차이는 DIFF-047/WIM-062로 분리했다.
- [종족 선호·P1 상점 노출 후속](species-affinity-runtime-review.md): 종족선호 작성14개가 모두 deprecated P1임을 확인했다. 초기 배치는 modular migration되지만 root/runtime catalog와 daily offer가 deprecated를 거르지 않아 star<=2인13개를 구매·unlock·facility-kit화할 수 있는 문제를 WIM-063으로 기록했고, public page 누락/GAP으로 오계수하지 않았다.
- 새 KB query `wiki system` implementation/documents는 stale 2429, 반환0행. 아래의 과거 stale4 기록은 당시 결과이며 현재 결과가 아니다. 새 digest는 양방향 보고서에 기록한다.

요청: 모든 시스템과 모든 위키 문서를 대조하여 설명되지 않은 시스템을 전부 목록화한다. 수치, 조건, 예외, 상태 전이와 도감 필드·관계의 누락도 포함한다.

기존 감사의 한계: `validate_source_coverage.py`는 목적지 문서 존재와 절 매핑 수만 검증하므로, PASS를 설명 완전성의 근거로 사용하지 않는다.

검토 기록은 `.planning/2026-09-05-system-wiki-audit/`에서 이어 간다. 최종 판정 전에는 미검토를 명시적으로 유지한다.

## 현재 결과

- [누락·오류 목록](missing-register.md): 구현 → 위키 방향 원장173행. GAP-007은 판정을 철회한 이력으로 보존하고 GAP-166~168은 시설 이용 완료 효과, GAP-169는 보안 신호소·나팔 효과, GAP-170은 정비 부품함 장비 수리, GAP-171은 골렘 충전 상태기, GAP-172~173은 서비스 허브/지원시설이다. WIM 구현 보완60건과 합산하지 않는다.
- [기계 판독 원장](missing-register.json): 고유 ID와 소유 문서, 분류, 원본 근거, 미완료 교차 검토 상태.
- [문서 검토 원장](wiki-review.md): 가이드 30/30, 욕구8개·업무42개·신체 기본12개·특수35개 및 체형 그룹10개 본문 완독. 완독과 원본 대조 완료를 구분한다.
- [모집단 스냅샷](census.json): 현재 확인한 경로의 파일 수와 digest. 이 파일에 집계됐다는 사실은 검토 완료를 뜻하지 않는다.
- [도감 노출 집계](entity-projection-census.json): 공개 엔티티 2,905개 전부를 구조적으로 열거했다. facts 없는 356개는 추가 대조 후보이며 자동으로 설명 누락으로 세지 않는다.
- [질병 도감 대조](disease-projection-review.json): 16개 작성 자산과 공개 도감의 수치·관계·hash 전수 대조.
- [시설 계보 진화 대조](facility-evolution-projection-review.json): 6개 작성 진화식의 조건과 공개 도감 대조.
- [신체 구조 대조](anatomy-projection-review.json): 작성 프로필12개·노드147개와 도감12개, 특수 문서35개 대조. 표의 명시 수치 오류0과 본문의 기능 전담 오표기5곳을 구분한다.
- [아이템 가격 오류표](item-basic-facts-review.md) · [기계 판독 상세](item-basic-facts-review.json): 아이템1075개·기본 필드3225개 비교. 가격198개 불일치, 무게·적재량은 모두 일치.198개 원본/위키 경로와 양쪽 값을 기록했다. 효과·관계·장비 성능 전체 검증은 별도다.
- [연구·청사진 요구 조건표](research-projection-review.md) · [기계 판독 상세](research-projection-review.json): 연구180개/시설8개/청사진7개·공개14문서 대조. 작업량180개 일치, 시설요구·청사진유형 누락, 즉시해금4종·조합식3종 설명오류를 구별한다. 연구실16의 공개대응과 전체 효과·저장 검토는 미완료다.
- [연구 효과·계승 강화 전수표](research-meta-mechanics-review.md) · [기계 판독 상세](research-meta-mechanics-review.json): 연구180개 직접효과 배열은 모두 비어 있으며 별도 특성·관리기술·계승 효과와 구분한다. 금단의 도약, 승인WU 공식, 계승강화9종과 런재화 산식을 GAP-048~051에 기록했다. 주인특성후보 증가의 실제 소비처1건과 UI/설계 불일치 후보는 별도다.
- [자동화·정비 대조](automation-mechanics-review.md) · [기계 판독 상세](automation-mechanics-review.json): 카탈로그 연결 시설27개·도감27개·필드189개 대조. 모드·속도·정비·수리 규칙과 품질 상한의 미확인 소비처를 GAP-052~056으로 기록했다.
- [구조 내구도·수리·돌파 대조](structural-mechanics-review.md) · [기계 판독 상세](structural-mechanics-review.json): 자산419개를 구조 조건으로 선별해10개 작성 모듈·4개 기본 생성을 구분했다. 시설13개의 수치52개 누락과 공통 피해·균열·수리·격노·경로 규칙을 GAP-057~060으로 기록했다. 복도1개의 설명 필요 여부와 수리/HUD 불일치는 별도 확인 대상이다.
- [전력 시설·공급·축전 대조](power-mechanics-review.md) · [기계 판독 상세](power-mechanics-review.json): 시설73개·작성 필드220개를 대조했다. 실제 적용191개 누락, 자동화 모드대체27개, 미사용 연료2개를 구분하고 공급 우선순위·연료·축전·과부하·복구·저장 설명을 GAP-061~065로 기록했다.
- [급수·폐수·배관 대조](fluid-mechanics-review.md) · [기계 판독 상세](fluid-mechanics-review.json): 시설7개·작성34필드와 공통 규칙을 GAP-066~070에 기록했다. FluidNetworkRuntime의 남은 구간도 대조했다. 실제 유체 실행·저장 왕복은 미검증이다.
- [생활·공정 급배수와 조합식 대조](fluid-use-mechanics-review.md) · [기계 판독 상세](fluid-use-mechanics-review.json): 생활4·공정18·보조28개 시설의244필드, 조합식355개의1420필드를 대조했다. 조합식24개의 물 수치는 모두 맞으며 폐수량·수동허용·시설 합산 규칙 등을 GAP-071~075에 추가했다.
- [컨베이어 운송·필터·배출·저장 대조](conveyor-mechanics-review.md) · [기계 판독 상세](conveyor-mechanics-review.json): 시설40개·43모듈·작성284필드를 대조해 GAP-076~080을 추가했다. 빈 목적지29개·덮인 포트 수용량2개를 구분한다. 목적지 지정 UI, 배출구 설정과 저장 타이머의 구현 확인 사항은 문서 누락과 별도로 기록했다.
- [구현 미확인 후보](implementation-uncertainties.md): 직접 질병 효과, 연구·영입 예외, 구조 수리와 전력망의 축전·차단·UI 한계를 설명 누락과 분리한다.
- [환경장·노출·작업 안전 대조](environment-mechanics-review.md) · [기계 판독 상세](environment-mechanics-review.json): 시설11개·14모듈·50필드와 확산·노출·파생 효과·작업 중단·음식 부패 배율을 GAP-081~086에 추가했다. 조명 전력/연료, 문 개방, 예측 시간, 보호함 정원 등8개 구현 확인 사항은 별도다.
- [기후·날씨·계절 사건 대조](climate-mechanics-review.md) · [기계 판독 상세](climate-mechanics-review.json): 기후5개·날씨6개·계절사건28개와 공개39도감을 대조해 GAP-087~093을 추가했다. 기존131수치와 효과관계7개는 일치하며 진폭의 연교차 오표기5개, 공식·예보·사건 수명주기·효과·작업지연 설명을 구분했다. Threat11개 등 구현 확인 사항8개는 별도다.
- [의복 유지관리 대조](apparel-work-maintenance-review.md) · [기계 판독 상세](apparel-work-maintenance-review.json): 세탁·건조·수선·기존 구멍 여닫기와 주문 실패/복원 규칙을 GAP-100~103으로 기록했다. 시설5개·공개5개·수선 재료 도감2개와 정적 UI 호출5변형을 확인했으며, 실제 노동 회계·일반 개조 UI 등 구현 확인 사항7개는 별도다.
- [의복 제작·품질·회수 대조](apparel-crafting-review.md) · [기계 판독 상세](apparel-crafting-review.json): 의복56종·원단12종·재단시설1개·특성1개의 제작 비용, 품질 투영, 영감, 반복 한도와 불합격품 처리를 GAP-104~109로 추가했다. 시설 건설 WU249/306 불일치와 반복 종료·저장 번호 등 구현 확인 사항5개를 분리했다.
- [시설 건설 비용 전수표](facility-construction-review.md) · [기계 판독 상세](facility-construction-review.json): BuildingSO 419개·공개 398개와 나머지 시설 도감 27개를 구분했다. WU 103개·재료 수량 42개 오류를 GAP-109의 144시설로 확장하고, 91시설의 재료 142행 생략과 랜드마크 9개의 비용·설명 누락을 GAP-110/111로 추가했다. 내부 작업 대상 42개의 공개 정책은 별도 확인 대상이다.
- [의복 상태·환복과 운반 멜빵](apparel-lifecycle-review.md) · [기계 판독 상세](apparel-lifecycle-review.json): 멜빵의 자동 착용·반납 조건과 내구도120/회당1 소모를 GAP-112로 추가했다. 의복 오염·마모 생산자, 일반 환복, 내구도0의120 복귀, 별도 내구도 상태와 저장/반납 등 확인 사항6건은 문서 공백과 분리했다. 기존 APPAREL-U04 후속은 중복 계수하지 않는다.
- [근거 검증](evidence-check.json): GAP-112까지의 원장112건에 대한 JSON/Markdown 일치·원본 경로·고유ID 검사와 추적 근거3991개·감사 산출물32개를 기록한다. GAP-113~132는 후속에서 별도로 JSON/Markdown ID 일치·원본 경로 존재를 검사하며, evidence-check 전체 재생성은 아직 하지 않았다. 기존 의복 상태·멜빵 독립 산술20건과 원장 제목·본문336개 검사 오류0이다. 이전 시설 건설4480검사·의복336필드/산술27건/5454조합 검증과 원본 변경2개의 판정 재검토 기록은 유지한다. 해시 추적은 완독 수가 아니며 전체 목표 완료나 Unity 실행 검증을 뜻하지 않는다.

## 확인 범위

시설419개의 건설 WU·BOM과 공개 여부를 전수 대조했다. 의복 상태 변경·후보 조회와 멜빵의 착용·반납은 정적 대조를 마쳤으며, 생산자 미확인과 실행·저장 경계는 별도 확인 사항으로 남겼다. 다른 시설 기능, 내부 작업대상42개의 공개 정책, 날씨의 작물·축산·원정 효과와 나머지 전체 시스템·도감은 후속 조사 대상이다. 이번 실제 UI 입력·실행·저장 왕복은 미실행이다.

추가 대조: [종족 환경·의복·원단](apparel-environment-review.md) · [전수 데이터](apparel-environment-review.json). 종족10종, 작업복4종, 의복56종, 원단12종과 침구2종의 설명을 GAP-094~099에 기록했다. 소재 성능 투영·착용 UI 등 구현 확인 사항6건은 확정된 효과 설명과 구분한다.

| 대상 | 집계 | 의미상 검토 상태 |
| --- | ---: | --- |
| 위키 가이드 | 30 | 30개 완독, 전체 원본 대조 미완료 |
| 욕구 참고 문서 | 8 | 8개 완독, 상태기 전체 대조 미완료 |
| 업무·신체 참고 항목 | 업무42·기본12·특수35·그룹10 | 본문 완독, 실제 원본·소비처 전체 대조는 진행 중 |
| 공개 도감 엔티티 | 2,905 | 전수 구조 집계, 번식10개·질병16개·시설 계보 진화6개·신체12개 및 일부 대표 항목 직접 대조 |
| C# | 2,505 | 모집단 digest 확보, 시작·생존·숙련·번식·멘토링 일부 원본 조사 |
| 긴급 동원·근무 정책 | 경보·예비 노동·중단/복귀·수면 응답 | 관련 실제 함수·작성 수치 대조, GAP-031~035 추가. 31개 업무 전체 실행기 검토 완료를 뜻하지 않음 |
| 공동 작업·노동·일반 영입 | 정원·기여·노동회계·수용·단골·후발보정 | 실제 실행/조회 경로 대조, GAP-036~041에 색인철 효과까지 추가. 다른 모집/연구 흐름은 미완료 |
| 아이템 기본 수치 | 아이템1075개·필드3225개 | 가격198개 불일치, 무게/적재일치. 고유효과·관계·별도장비 정의는 제외한 부분 검사 |
| 연구·청사진·기억 잔재 | 연구180·능력시설8·청사진7/공개14문서 | 대기열·시설요구·청사진·분석/정찰을 GAP-043~047로 기록. 연구 전효과·저장·실제 UI 검증은 미완료 |
| 연구 효과·계승 강화 | 직접효과 배열180·도약302·관리보정·계승9종 | 직접연구효과바인딩0개, 다른 시스템의 연구보정은 별도.9종 구매UI/가격·레벨·효과를 대조했고 정적소비8종/소비미확인1종을 구분. 런재화 산식까지 GAP-048~051 기록, 실제 UI·저장왕복 미실행 |
| 자동화·정비 | 작성27·도감27·설정189개 | 모드·작업 권한·상태 공식·수리WU 대조. 설정189개 도감 누락, 품질 상한 소비 미확인. 전체 공정 실행·전력망 등은 미완료 |
| 구조 내구도·돌파 | 자산419 선별·도감14 확인 | 작성10·기본3개에서52필드 누락, 복도1개 보류. 실제 돌파·저장왕복은 미실행 |
| 시설 건설 비용 | 자산419·공개398·다른 유형27 | WU103시설/재료42시설 오류,91시설 재료142행 생략,랜드마크9개 WU/재료39행 누락. 내부42개/호환21개를 분리했고 다른 시설기능 검토는 미완료 |
| 전력망 | 시설73·전력필드220 | 실제 적용191개 미노출. 모드대체27·미사용2를 구분. 공통 계산·UI 호출·저장 대조, 모든 시설 실제 실행은 미검증 |
| 급수·폐수·배관 | 생산/저장/처리/물통7시설34필드 + 생활/공정/보조50시설244필드 | 공통·수동·합산 규칙과355조합식1420필드 대조. 실제 시설 이용·실패/저장 왕복은 미검증 |
| 컨베이어 | 시설40·모듈43·작성284필드 | 반입/경로/필터/배출/저장과 도감 대조.284개 독립 효과를 의미하지 않음. 실제 목적지 설정 경로와 UI/저장왕복은 미검증 |
| 환경장·노출·작업 안전 | 시설11·모듈14·선택50필드 | 칸별 확산·온도조절·노출4종·회복·작업/전투 보정·대피·부패 온도배율 대조. 종족·의복·계절·식품 전체와 실제 UI/저장왕복은 미검증 |
| Resources 작성 자산 | 3,533 | 모집단 digest 확보, 번식10개·질병16개·시설 계보 진화6개·신체12개·생존 설정 및 일부 항목 조사 |
| docs 핸드북 파일 | 11 | 본문 9장은 docs_final 사본과 hash 동일, 내용 전수 대조는 미완료 |
| 아키텍처 문서 파일 | 32 | 시스템 README와 08장 완독, 나머지 미검토 |
| game-design 파일 | 117 | 파일별 hash 확보, 내용 전수 대조 미완료 |
| 위키 페이지·공용 템플릿 등 소스 | 57 | EntryContent의 실제 노출 항목과 모델 fact 투영 일부 확인 |

핸드북 appendix-source-map.md는 docs와 docs_final의 내용이 다르다. 본문 9장의 동일성으로 부록의 최신성까지 주장하지 않는다. 위키 버전은 registry 기준 0.0.1v 하나이며 현재판·보관판 경로 검증은 별도 남아 있다.

## 지식베이스와 근거 권위

- query: `system`, areas: `code`, `authority`, `documents`, limit 12.
- 후속 query: `ProjectWorkforceRuntime SettlementLaborAccountingRuntime`, areas: `code`, `authority`, limit12. 당시 결과도 stale/469실패/반환0행이었다.
- 최신 query: `BuildingWorkAmountAbility CalculateConstruction constructionMaterials`, areas=`code/content/authority`, limit8. session87390은 exit1, stale4실패/반환0행으로 종료했다. 생성물을 재생성하지 않고 직접 원문으로 대조했다.
- 결과: stale, 조회 반환 행0, freshness failures4.
- 최신 content source digest: `139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6`. 원본 해시 대조는 생성 인덱스와 별도로 수행했다.
- knowledge-base source digest: `ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd`.
- 생성 인덱스를 현재 구현의 증거로 사용하거나 재생성하지 않았다. 직접 연 원본은 각 누락 항목과 evidence-check.json에 기록한다.
- 원본·공개 위키·게임 수치 변경 없음. 밸런스 영향 없음. 정적 조사이며 Unity 컴파일·Play Mode·실제 UI 입력은 실행하지 않았다.

## 남은 조사

1. 업무·신체 JSON의 남은 소비처·기능 보정 대조, 전체 템플릿·현재판/보관판의 실제 노출 구조. 가이드30개와 욕구·업무·신체 본문은 완독했으나 위임한 도감과 원본 전체 대조는 남아 있다.
2. 핸드북·설계·현재 런타임의 모든 시스템과 수치·조건·예외. 현재 조사한 가족·숙련도 아직 전체 완료가 아니다.
3. 도감 2,905개의 유형별 작성 필드·효과·관계, 공개 제외·누락 참조, 질병 등 개수 차이.
4. 각 누락의 다른 위키 문서 존재 여부를 의미 단위로 교차 검토하고 중복 제거.
5. 조사 중 바뀐 원본의 hash 재확인과 미검토 0 여부 검증. 현재는 전체 완료로 판정할 수 없다.
