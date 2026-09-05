# 10. 배포·운영·복구

## 배포 원칙

로컬 또는 CI에서 renderer image와 공개 projection을 검증한다. production은 공개 문서를 새로 생성하지 않으며, 검증된 `game-versions/` volume을 요청마다 읽어 HTML로 렌더링한다. 문서·데이터 수정은 image 재배포 없이 같은 volume 안에서 원자 교체한다.

## 환경

| 환경 | 목적 | 공개 범위 |
|---|---|---|
| local development | 템플릿·데이터 작업 | 작업자 로컬 |
| local production preview | renderer, live content reload, 링크·검색 검증 | 작업자 로컬 |
| CI image | 재현 가능한 renderer image와 QA | 저장소 권한 보유자 |
| production | 승인된 public projection을 요청 시 렌더링 | 공개 또는 승인된 접근 범위 |

내부 provenance가 포함된 development build는 production과 다른 artifact다. 동일 artifact를 URL만 숨겨 배포하지 않는다.

## 기본 호스팅: Synology reverse proxy + Node runtime

확인된 대상은 Synology DS916+, DSM 7.1.1-42962 Update 9, RAM 2GB다. 주 runtime은 reverse proxy 뒤의 Node standalone 컨테이너다. 컨테이너는 `127.0.0.1:4321`에만 열고, HTTPS 443 proxy만 public endpoint가 된다. `game-versions/`만 읽기 전용 volume으로 mount하며 게임 원천, `docs_final`, 생성 중간물과 계정 정보는 넣지 않는다. PHP와 MariaDB는 사용하지 않는다.

현재 catch-all portal은 80/443을 사용하고 현재 DDNS hostname의 443 reverse-proxy rule은 다른 서비스로 연결된다. 소유자가 그 서비스 종료를 승인했으므로 V1은 같은 hostname과 표준 HTTPS 443을 인수한다. 먼저 Node 컨테이너와 localhost backend를 LAN/VPN에서 검증하고, 기존 reverse-proxy rule을 export/기록한 뒤 maintenance window에 443 backend를 컨테이너로 교체한다. 현재 hostname 인증서를 그대로 연결하며 별도 외부 port는 열지 않는다.

권장 release 구조는 실제 절대 경로를 문서에 고정하지 않고 다음 논리 구조를 따른다.

```text
$WIKI_DEPLOY_ROOT/
├─ content/
│  └─ game-versions/                  # 컨테이너에 read-only mount하는 공개 문서·데이터
├─ images/
│  ├─ {short-commit}/                 # renderer image release 기록
│  └─ previous/
├─ shared/
│  └─ deploy-metadata/
└─ staging/
```

새 게임 버전은 직전 `content/game-versions/{game-version}` 폴더 전체를 복사해 만든다. 승인된 과거 버전은 수정하지 않으며, 같은 게임 버전의 문서 정정은 별도 deployment release ID와 errata 기록으로 관리한다. live content 파일은 임시 이름으로 올린 뒤 같은 volume 안에서 원자 교체한다. 파일을 한 줄씩 덮어써 반쯤 기록된 문서를 보이게 하지 않는다.

renderer 코드 변경은 새 image를 build·검증한 뒤 container를 교체하고, 이전 image tag를 남겨 rollback한다. 콘텐츠 변경은 container 교체 대상이 아니다.

## NAS 배포 흐름

1. 개발 PC 또는 CI의 깨끗한 checkout에서 pinned Node/npm 설치
2. knowledge-base freshness와 기존 content/KB 검증
3. 게임 업데이트라면 직전 게임 버전 폴더를 복사해 candidate의 normalized data 재생성
4. candidate와 published 게임 버전 폴더 digest, 결정론·공개 누출 검사
5. `npm run check`, Astro server build, current/historical 링크·게임 버전 선택기·접근성·검색·관계 graph slice를 검증
6. 게임 버전, source snapshot digest, commit, renderer source digest를 포함한 release manifest 생성
7. 전용 SSH key로 `content.zip`, renderer build context, manifest와 checksum을 NAS의 release별 incoming 경로에 업로드
8. root 소유의 고정 범위 배포 helper가 checksum과 ZIP 경로를 검사하고, 같은 volume의 불변 release 경로에 압축을 푼다.
9. canonical HTTPS origin을 build argument로 넣어 renderer image를 만들고, versioned content를 read-only mount한 새 container를 `127.0.0.1:4321`에 띄운다.
10. 현재·이전 게임 버전 URL을 localhost에서 smoke test한 뒤에만 release를 현재판으로 기록한다. 실패하면 새 container를 중지하고 기존 container를 복구한다.
11. 첫 Node 전환에서는 smoke test가 끝난 backend로 Synology reverse proxy를 갱신한다. 이전 Web Station tree, reverse-proxy 설정 사본, content와 image release는 rollback용으로 보존한다.

현재 구현에서 release 준비는 `Tools/Wiki/package_live_release.ps1`, 작업자 실행은 `Tools/Wiki/deploy_nas.ps1`, NAS의 권한 상승 경계는 `/usr/local/sbin/dungeonstory-wiki-deploy`가 맡는다. 공개 projection 생성·검증·Astro 검사·server build가 모두 통과한 파일만 NAS content volume과 renderer image에 들어간다. Pagefind와 정적 HTML artifact는 live runtime에 사용하지 않는다.

- `game-versions/`: container mount 대상 공개 projection
- `content.zip`: read-only `game-versions/` volume용 공개 projection
- `renderer-context.zip`: 고정 Dockerfile과 Astro renderer source를 담은 NAS image build context
- `SHA256SUMS`: 두 ZIP의 전송 무결성 검사
- `release-manifest.json`: 게임 버전, content/source digest, renderer source digest, repository HEAD(있을 때), payload 파일 수·크기·SHA-256, 통과한 검증을 기록

이 release bundle은 NAS의 `staging`에만 먼저 올린다. `game-versions/` 이외의 파일을 content volume에 복사하지 않으며, NAS가 checksum과 smoke-test를 통과하기 전에는 content directory 또는 443 backend를 바꾸지 않는다.

첫 구현은 같은 LAN/VPN에서 작업자가 승인 후 실행하는 `Tools/Wiki/deploy_nas.ps1` 형태를 기본으로 한다. SSH client alias가 hostname, custom port, account와 전용 private key를 저장소 밖에서 관리한다. DSM 관리자 비밀번호는 최초 key·helper 등록 때만 대화형으로 쓰며 저장하지 않는다. 이후 배포는 key 인증과 정확히 한 root-owned helper만 허용하는 NOPASSWD 규칙을 사용한다. 자동 배포가 필요해질 때만 CI 연결을 추가한다.

## 연결·자동화 선택

| 방식 | 사용 시점 | 판단 |
|---|---|---|
| 개발 PC → NAS SFTP | 초기 운영, 같은 LAN/VPN | 기본안; 전용 비관리자 계정과 staging share ACL을 실제 검증한 뒤 사용 |
| 개발 PC → NAS SMB | 수동 복구, 같은 LAN | fallback; 자동 배포와 WAN 사용 금지 |
| NAS가 CI artifact를 outbound HTTPS로 pull | 무인 배포가 필요할 때 | 권장 자동화 후보; NAS inbound 포트를 CI에 열지 않음 |
| GitHub-hosted runner가 NAS로 push | 고정 VPN/tunnel과 제한 계정이 있을 때 | 조건부 |
| NAS self-hosted runner | 격리된 runner와 운영 역량이 있을 때 | 기본 비권장; 저장소 코드가 NAS에서 실행됨 |
| NAS에서 전체 위키 build | 별도 build container와 자원이 충분할 때 | 기본 비권장; production host와 toolchain 결합 |

## 공개 범위

- 소유자 결정: 첫 production release부터 인터넷에 완전 공개한다. LAN/VPN RC는 공개를 대신하는 운영 모드가 아니라 필수 사전 검증 단계다.
- 비공개 preview: 전용 Web Station 내부 portal을 LAN 또는 VPN에서만 접근하고 WAN port forwarding을 추가하지 않는다.
- 제한 공개: reverse proxy 앞 인증 계층을 사용하고 위키와 NAS 관리 UI를 서로 다른 host/port 정책으로 분리한다.
- 완전 공개: 현재 DDNS hostname의 HTTPS 443을 위키 canonical endpoint로 사용하고, 일치하는 기존 인증서, 보안 헤더, rate limit, NAS/라우터 업데이트, 외부 포트 감사와 로그 보존 정책을 승인한다. 별도 custom domain은 후속 선택 사항이다.

어떤 경우에도 NAS 관리 UI, SMB, NFS를 위키 공개를 위해 인터넷에 노출하지 않는다.

## 남은 Synology 승인 게이트

- router에서 현재 WAN 443 mapping의 실제 NAS target과 불필요한 동시 노출 확인
- Web Station 전용 document root와 내부 전용 port-based portal 생성 경로의 구현 spike
- SFTP 비관리자 계정, staging share ACL, 상위 경로 차단의 실제 permission test
- 같은 volume에서 atomic rename, Web Station root 전환 또는 blue/green slot 중 지원되는 release switch 실증
- DSM 방화벽·자동 IP 차단 또는 앞단 gateway의 동등 보호 정책
- 기존 phpMyAdmin alias가 위키 공개 hostname/port에서 격리됨을 외부에서 확인
- 기존 reverse-proxy rule export, 443 handover, smoke test, 이전 rule 복원의 리허설
- 현재 DDNS hostname 인증서의 자동 갱신과 443 적용 시험
- 정적 artifact, Web Station 설정, 인증서·router 설정의 백업/복구 책임과 UPS 상태 확인

DSM의 암호화된 시스템 설정 자동 백업은 최근 성공 상태였지만, 감사 시 Hyper Backup과 Snapshot Replication은 설치돼 있지 않았다. `dist`는 재빌드할 수 있어도 Web Station 설정, 인증서, 배포 metadata와 운영 문서는 별도 복구 경로가 필요하다. 특정 패키지 설치를 전제하지 말고 실제 복원 시험을 통과한 방법을 선택한다.

저장 공간과 기본 정적 호스팅 능력은 충분한 것으로 확인됐다. 위 항목이 완료되기 전에는 local/LAN production artifact까지만 승인하고 외부 port를 추측해 열지 않는다.

## 관리형 fallback 또는 mirror

NAS 점검·회선 장애 때 지속적인 공개가 반드시 필요하다면 동일한 정적 artifact를 GitHub Pages나 Cloudflare Pages에 임시 mirror로 배포할 수 있다. DNS failover를 자동화하기 전에는 두 사이트의 revision 불일치와 스포일러 정책을 함께 검증한다.

관리형 fallback은 같은 Astro Node renderer와 공개 content volume 계약을 재현한다. Workers와 외부 데이터베이스는 추가하지 않는다.

## 도메인과 URL

- V1 canonical은 현재 Synology DDNS hostname의 HTTPS 443으로 둔다.
- LAN/VPN RC에서는 내부 portal URL을 쓰되 production build의 canonical, sitemap, Open Graph, robots는 공개 hostname으로 검증한다.
- HTTPS를 강제하고 현재 인증서의 이름 일치와 자동 갱신을 확인한다.
- 향후 custom domain을 도입하면 DNS 변경 전에 새 canonical과 리디렉션, sitemap, 검색 엔진 이전 절차를 별도 ADR로 승인한다.

## 배포 트리거

- PR: 생성·검증·build만 수행하고 artifact 제공
- 기본 branch 또는 명시적 release tag: NAS production candidate 생성
- NAS production: 소유자 승인 뒤 게임 버전 기반 release 전환
- 예약 자동 배포: V1 없음

게임 업데이트 후보에는 유일하고 증가한 게임 버전과 해당 update record가 반드시 있어야 한다. 같은 게임 버전의 위키 코드 재빌드나 문서 errata 배포는 게임 버전을 올리지 않고 commit/artifact digest가 포함된 deployment release ID만 달라진다.

최초 production cutover는 위키 구현과 모든 release gate가 완료된 직후 수행한다. 별도 날짜를 기다리지 않되, old rule 복원 권한을 가진 운영자가 15분 동안 중단 없이 대응할 수 있는 시점이어야 한다.

게임 코드 커밋마다 사이트를 자동 공개하지 않는다. 원천이 바뀌면 CI는 영향과 stale 상태를 보고하고, 승인된 위키 릴리스가 공개를 수행한다.

## 관측과 상태

서버가 없으므로 운영 관측은 최소화한다.

- 게임 버전, 배포 commit, source snapshot digests, generator/schema version, artifact digest, 페이지·관계·graph node/edge/slice·검색 문서 수를 release manifest로 공개 또는 내부 기록
- CI build time, artifact size, Pagefind index size, broken link count 추세
- 게임 버전별 문서·정규화 데이터·archive 크기와 직전 게임 버전 대비 증가량
- NAS reverse proxy의 최소 접근·오류 로그는 보안과 장애 진단 목적에 한해 보존 기간을 정함
- TLS 만료, DSM·Web Station 패키지 업데이트, 디스크 상태와 backup 성공 여부를 운영 체크에 포함
- 오류 수집 SDK와 세션 리플레이 없음
- 사이트 footer에서 현재 게임 버전, 해당 `/updates/{game-version}/`, source snapshot과 상태 페이지/이슈 경로 제공

## 캐시

- fingerprinted CSS/JS/image: 장기 immutable cache
- `/game-versions/{published-game-version}/` HTML과 게임 버전별 Pagefind/graph slice: 게임 버전 artifact 불변을 전제로 장기 cache
- HTML, sitemap, manifest: 짧은 cache 또는 revalidate
- Pagefind index: 빌드 fingerprint에 맞춘 원자적 배포
- 새 HTML과 옛 검색 index가 섞이지 않도록 artifact 단위 배포

## 롤백

1. 마지막 정상 NAS release의 commit과 source digests 확인
2. `current` pointer 또는 blue/green slot을 이전 deployment release로 원자적 전환
3. 잘못된 artifact의 공개 범위와 current 검색 캐시 확인; immutable historical route는 유지
4. 비공개 누출이면 일반 결함보다 우선해 즉시 사이트 비공개/이전 배포 복구
5. 원인과 재발 방지 검사를 기록

새 게임 버전이 공개 후 철회되면 기록을 삭제하거나 번호를 재사용하지 않는다. 해당 update record를 `withdrawn`으로 표시하고 실제 서비스 중인 이전 게임 버전을 footer와 current manifest에 복원한다.

443 최초 인계 후 외부 HTTPS, 홈, 대표 엔터티, 검색, 정적 자산, 404 중 하나라도 정상 기준을 만족하지 못하면 실패로 판정한다. 진단 때문에 시간을 연장하지 않고 전환 시작 후 15분 안에 저장해 둔 기존 reverse-proxy rule과 backend를 복원한다. 복구 뒤 외부 HTTPS 확인이 끝날 때까지 maintenance 상태를 유지한다.

생성된 `dist`나 과거 archive를 손으로 고쳐 긴급 패치하지 않는다. 게임 사실이 바뀌면 새 게임 버전 폴더를 복사해 그 폴더의 content/data/manifest에서 수행하고 전체 파이프라인으로 재배포한다. 게임 버전 변화 없는 문서 정정도 source와 errata 기록을 수정한 뒤 전체 파이프라인으로 재배포한다.

## 운영 주기

- 게임 데이터 릴리스 전: freshness, 공개 diff, 스포일러, 미디어, 검색 회귀 검토
- 위키 릴리스 후: 주요 URL, 검색, sitemap, 404 smoke test
- 월별 또는 큰 의존성 변경 시: dependency, Lighthouse, 접근성, 링크 전체 검사
- NAS OS·reverse proxy·호스팅·프레임워크 major 변경 시: 별도 ADR과 rollback rehearsal
- 공개 운영 중 인증서 만료 전: 갱신 성공과 새 인증서 적용을 외부에서 확인
