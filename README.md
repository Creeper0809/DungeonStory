# DungeonStory

Unity 게임 개발 저장소다. Windows x64와 Unity **6000.3.8f1**을 기준으로 한다.

## 개발 시작

Git과 Git LFS를 설치한 뒤 다음 순서로 받는다. 최신 파일만 받으려면 얕은 복제를 사용한다.

```powershell
git lfs install
git clone --depth 1 https://github.com/Creeper0809/DungeonStory.git
cd DungeonStory
git lfs pull
```

Unity Hub에서 이 폴더를 추가하고 6000.3.8f1로 연다. 첫 실행에서는 패키지 복원과 에셋 가져오기가 끝날 때까지 기다린다. 시작 씬은 `Assets/Scenes/TitleScene.unity`다.

Git LFS는 모델, 플러그인, 폰트와 이미지의 실제 파일을 받는 데 필요하다. ZIP 대신 위 복제 방법을 사용한다. `Library`, IDE 솔루션과 사용자 설정은 Unity와 편집기가 다시 만든다.

Python 3.10 이상이 있으면 편집기를 열기 전에 필수 파일을 검사할 수 있다.

```powershell
python tools/Repository/verify_checkout.py
```

이 검사는 필수 파일, 씬 GUID, 로컬 패키지, LFS 복원과 모델 실행 파일의 해시를 확인한다. Unity 컴파일이나 플레이 검증을 대신하지 않는다.

## 포함된 개발 파일

- `Assets/`와 `.meta`: 게임 코드, 씬, 프리팹, SO와 플러그인
- `Packages/`, `ProjectSettings/`: 고정 패키지와 Unity 프로젝트 설정
- `tools/`, `.github/`: 개발 도구와 자동 검사
- `docs/`, `docs_final/`, `wiki/`: 설계 문서, 생성 인덱스와 위키 소스
- `Assets/StreamingAssets/DungeonStoryLlm/`: 현재 main의 Windows 로컬 모델과 실행 파일

별도 모델 학습 저장소나 개인 컴퓨터의 소스 복사본은 프로젝트를 여는 데 필요하지 않다. 배포된 모델의 종류와 상태는 해당 폴더의 `manifest.json`을 따른다. 진행 중인 다른 브랜치의 모델이나 게임 변경이 자동으로 포함되지는 않는다.

## 저장소 보관 기준

실행 로그, 생성된 내보내기, 임시 빌드와 개인 작업 기록은 기본적으로 Git에서 제외한다. `HANDOFF.md`, `task_plan.md`, `findings.md`, `progress.md`는 작업 중 필요할 때 로컬에 만든다.

`Artifacts/`에는 기존 자동 검사와 고정 회귀 검사가 실제로 읽는 입력만 예외로 남긴다. [필수 입력 목록](tools/Repository/required-fixtures.json)에 용도를 명시한다. 이 목록의 실행 기록은 검사 원문과 해시를 검증하는 고정 입력이므로 일반 실행 로그와 함께 제거하지 않는다. 새 실행을 공유할 때는 사용처를 확인해 목록을 갱신하고 필요한 파일만 명시적으로 추가한다.

과거 산출물은 이전 커밋에서 확인할 수 있다. Git 이력이 필요하면 `git fetch --unshallow`로 받는다. 개발 문서 진입점은 [DOCUMENTATION.md](DOCUMENTATION.md)다.
