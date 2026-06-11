# LLM IDE2 — 파일럿 퀵스타트

글쓰기 작업실(Writer)을 파일럿 테스터에게 배포하고 사용하는 최소 안내서다.

---

## A. 빌더(배포자)용 — 패키지 만들기

요구: Windows + .NET 10 SDK.

```powershell
# 저장소 루트에서 (Windows PowerShell 5.1)
powershell -ExecutionPolicy Bypass -File scripts/publish-writer.ps1 -Zip
```

> PowerShell 7(`pwsh`)이 있으면 `pwsh -File scripts/publish-writer.ps1 -Zip`도 된다. 스크립트는 둘 다 호환.

* 출력: `dist/LlmIde.Writer/` 폴더와 `dist/LlmIde.Writer.zip`.
* self-contained 게시라 테스터 PC에 .NET 설치가 없어도 실행된다(폴더가 ~150MB).
* 공통 정책(`policies/`)이 폴더에 함께 포함된다.
* zip을 테스터에게 전달한다.

> 채팅 IDE(Writer 대신 Chat)를 배포하려면 스크립트의 프로젝트 경로를
> `src/LlmIde.Wpf.Chat/LlmIde.Wpf.Chat.csproj`로 바꿔 동일하게 게시한다.

**비개발자 테스터용 (압축 풀기도 어려운 경우)**: `scripts/install-and-run.bat`을 `LlmIde.Writer.zip`과 **같은 폴더**에 함께 전달한다. 테스터가 bat을 더블클릭하면: ① 실행 폴더에 압축 해제 → ② 데이터 폴더(패키지의 `LlmIde2`)를 `%LOCALAPPDATA%\LlmIde2`로 이동 → ③ 아키텍처 확인(x86/x64/ARM64) → ④ .NET 10 데스크톱 런타임 확인·(winget) 설치 → ⑤ Writer 실행. 프로그램은 데이터를 `%LOCALAPPDATA%\LlmIde2`에서 읽는다. (API 키를 미리 `settings/providers.json`에 박아 두면 키 입력 없이 바로 사용 가능.)

---

## B. 테스터용 — 설치와 첫 사용

### 1. 설치

1. 받은 zip을 **쓰기 가능한 폴더**에 푼다 (예: 바탕화면, 문서). `Program Files`에는 풀지 말 것 — 설정/프로젝트 파일을 그 폴더 안에 쓰기 때문.
2. `LlmIde.Wpf.Writer.exe`를 실행한다.
3. 첫 실행 시 "API 키 필요" 안내가 뜨면 정상이다(아래 3번).

### 2. 프로젝트 만들기

* 메뉴 **프로젝트 > 생성** → 이름 입력 → 생성. 좌측 목록에 추가된다.

### 3. LLM API 키 입력 (필수)

* 메뉴 **편집 > LLM** → 프로바이더 행에 **API 키**를 입력(기본 제공: deepseek) → 저장.
* 키는 이 폴더의 `settings/providers.json`에 **로컬 평문**으로 저장된다(서버 전송 없음). 개인 키를 쓰고, 공유 PC에서는 주의.

### 4. 글쓰기

* **목차**(중앙 좌): 우클릭으로 추가/하위추가/이름변경/삭제, 더블클릭 인라인 편집, 드래그로 순서 변경.
* **본문**(중앙 우): 목차를 선택하면 편집 가능. `Ctrl+S` 또는 다른 목차 클릭 시 저장.
* **대화**(우측): 아래 입력칸에 요청 → Enter 전송. "이 내용으로 본문 고쳐줘" 같은 요청에 LLM이 수정안을 제시하면 **[이전 | 수정]** 창에서 [변경]/[취소]로 확정한다.
* **이미지**: 이미지 파일을 아티팩트 영역(중앙 하단)으로 드래그 → 카드 추가. 카드를 본문으로 드래그하면 이미지 참조가 삽입되고, 본문 우측 **미리보기** 버튼으로 텍스트+이미지를 확인한다.
* **docx 내보내기**: 메뉴 **프로젝트 > docx 내보내기** → 목차(제목)+본문을 Word 문서로 저장. 목차 깊이가 Word 헤딩 레벨이 된다(본문 마크다운 서식·이미지는 평문으로 나감).

---

## C. 데이터 위치

모든 사용자 데이터는 **`%LOCALAPPDATA%\LlmIde2`** 에 저장된다(앱 실행 파일 위치와 무관 — Program Files 같은 읽기 전용 위치에 설치해도 안전). 정책 파일은 앱에 동봉되어 있고, 첫 실행 시 이 폴더에 없으면 자동으로 시드된다(이후 사용자가 편집 가능).

```text
%LOCALAPPDATA%\LlmIde2\
├─ policies/                공통 정책(첫 실행 시 시드, 편집 가능)
├─ settings/providers.json  공통 LLM 프로바이더 + API 키(로컬 평문)
├─ project/projects.json    프로젝트 등록소
└─ <프로젝트폴더>/           목차(outline.json)·본문(<id>.md)·.llmide(대화/아티팩트 등)
```

백업/이전은 이 폴더를 통째로 복사하면 된다.

---

## D. 파일럿 단계 알려진 한계

* 아티팩트 카드 미관은 다듬는 중.
* LLM은 이미지를 읽지 않는다(이미지 생성·인식 미지원). 이미지는 문서 삽입용.
* 공통 정책/키는 로컬 평문 파일이다(로컬 버전 정책, DEC-079). 변조 방지·비공개는 SaaS 전환 시 별도 처리.
* 피드백에서 우선순위가 정해지면 반영한다.
