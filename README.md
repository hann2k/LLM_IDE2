# LLM IDE2

LLM IDE2는 stateless LLM Provider 위에 stateful Project Runtime을 얹는 API 기반 LLM 개발 보조 IDE다.
IDE가 프로젝트 상태, 기준, 대화 로그, 맥락 요약, 산출물을 보존하고, LLM은 매 요청마다 IDE가 조립한 맥락을 받아 응답한다.

현재 개발 단계는 Phase 9 WPF GUI 적용 단계다.
Phase 1~8.5의 Core/CLI 기반 MVP 기능은 구현되어 있고, WPF에서 같은 Core와 같은 `.llmide` 데이터를 사용하는 GUI 기능을 연결하는 중이다.

[!IMPORTANT]

> ## License / Usage Terms
>
> This project is  **source-available for personal and non-commercial use only** .
>
> 이 프로젝트는 **개인 및 비상업적 용도에 한해 소스 열람과 사용을 허용**합니다.
>
> 허용되는 사용:
>
> * 소스 코드 열람, 복제, 학습
> * 개인, 교육, 연구, 비상업적 목적의 사용
> * 비상업적 목적의 개인 수정
>
> 사전 서면 승인 없이 금지되는 사용:
>
> * 상업적 목적의 사용
> * 판매, 재라이선스, 임대, 유료 호스팅, 유료 서비스 제공
> * 상업 제품, SaaS, 사내 업무 도구, 유료 컨설팅 산출물에 포함
> * 저작권, 라이선스, 출처 표시 제거
>
> 상업적 사용은 저작자의 사전 승인이 필요합니다.
>
> Copyright © 2026 hann2k. All rights reserved except as expressly permitted above.
>
> 상업적 라이선스 문의는 저장소 소유자에게 연락하십시오.

## 현재 상태

완료된 단계:

* Phase 1: 프로젝트 초기화와 프로젝트 등록소 관리
* Phase 2: DeepSeek Provider 기반 대화
* Phase 3: SQLite + JSONL/JSON 대화 로그 저장
* Phase 4: Criteria 관리와 활성 기준 주입
* Phase 5: 프로젝트 상태 관리와 상태 주입
* Phase 6: Rolling Context Compression
* Phase 7: 맥락 빌더 v1
* Phase 8: 산출물 저장과 명시 태그 기반 추출
* Phase 8.5: Agent Loop v1, `web_search`, `fetch_url` 읽기 도구

진행 중인 단계:

* Phase 9: WPF GUI 적용

Phase 9에서 현재 확인되는 GUI 범위:

* 프로젝트 등록소, 생성, 이름 변경, 복제, 삭제
* 채팅 화면과 스트리밍 응답 표시
* 독립 팝업 채팅 입력창
* Provider 선택과 프로젝트 생성 시 API Key 입력
* Criteria 편집
* 대화 목록, 중요도 표시, 개별 대화 삭제
* 수동 대화 추가
* 응답 선택 영역을 산출물로 추출
* 산출물 목록, 편집, Markdown/코드 뷰어, 삭제
* Agent 도구 관리 화면

남은 Phase 9 항목은 Provider/Model 설정 화면, 프로젝트 상태 편집, Rolling Context/맥락 패키지/Request Log 조회 기능의 정리와 보강이다.

## 핵심 원칙

이 프로젝트는 다음 원칙을 따른다.

* IDE가 프로젝트를 기억한다.
* LLM 호출은 요청 단위로 독립 실행한다.
* Core 비즈니스 로직은 UI 계층에 두지 않는다.
* CLI와 WPF는 같은 Core와 같은 `.llmide` 데이터를 사용한다.
* AI는 제안하고, IDE는 기록하며, 최종 변경 권한은 사용자에게 있다.
* 도구 호출은 읽기 전용으로 시작하고, 호출 이력은 대화 메시지와 분리해 기록한다.

## 솔루션 구조

```text
LlmIde.slnx
├─ src/
│  ├─ LlmIde.Core/
│  ├─ LlmIde.Infrastructure/
│  ├─ LlmIde.Cli/
│  └─ LlmIde.Wpf/
├─ tests/
│  └─ LlmIde.Tests/
├─ policies/
└─ Docs/
```

계층 책임:

* `LlmIde.Core`: 프로젝트, Criteria, 상태, 맥락 빌더, 대화 흐름, 산출물, Agent Loop 모델과 인터페이스
* `LlmIde.Infrastructure`: 파일/JSON/JSONL/SQLite 저장소, DeepSeek Provider, Agent 도구 구현
* `LlmIde.Cli`: CLI 어댑터와 검증용 실행 환경
* `LlmIde.Wpf`: WPF GUI 어댑터
* `LlmIde.Tests`: Core/Infrastructure 중심 회귀 테스트

## 저장 구조

프로젝트 등록소는 실행 프로그램 루트 아래에 저장된다.

```text
<IdeProgramRoot>/project/projects.json
```

등록소는 `pID`, 표시 이름 `name`, 프로젝트 경로를 분리해서 관리한다.
CLI에서 프로젝트를 지정할 때는 `pID`를 사용한다.

실제 프로젝트 데이터는 각 프로젝트 루트의 `.llmide` 아래에 저장된다.

```text
<ProjectRoot>/
└─ .llmide/
   ├─ project.json
   ├─ init-progress.json
   ├─ project-state.json
   ├─ criteria.json
   ├─ policies/
   ├─ conversations/
   │  ├─ conversation.db
   │  ├─ messages.jsonl
   │  ├─ requests.jsonl
   │  ├─ tool-calls.jsonl
   │  ├─ context-packages/
   │  └─ rolling-context/
   │     ├─ current.md
   │     ├─ history/
   │     └─ index.jsonl
   ├─ artifacts/
   │  └─ artifacts.index.json
   └─ settings/
      └─ providers.json
```

`conversation.db`가 주 저장소이고, JSONL/JSON은 디버그, 감사, 요청 재현용 보조 기록이다.

## 개발 환경

필요 환경:

* Windows
* .NET 10 SDK
* C#
* WPF

주요 패키지:

* `Microsoft.Data.Sqlite 10.0.8`
* `Markdig.Wpf 0.5.0.1`

## 빌드와 테스트

루트 폴더에서 실행한다.

```bash
dotnet build LlmIde.slnx -c Debug -v:minimal
```

테스트 실행:

```bash
dotnet run --project tests/LlmIde.Tests/LlmIde.Tests.csproj -c Debug
```

CLI 출력은 기본적으로 다음 위치에 생성된다.

```text
bin/Debug/net10.0/
```

WPF 출력은 다음 위치에 생성된다.

```text
bin/Debug/net10.0-windows/
```

WPF 프로젝트는 CLI 런타임을 같은 출력 폴더로 복사해서 GUI와 CLI가 같은 program root와 프로젝트 등록소를 공유할 수 있게 한다.

## 실행

CLI:

```bash
dotnet run --project src/LlmIde.Cli/LlmIde.Cli.csproj -- <command>
```

빌드 결과물에서 직접 실행:

```bash
cd bin/Debug/net10.0
./llmide
```

WPF:

```bash
dotnet run --project src/LlmIde.Wpf/LlmIde.Wpf.csproj -c Debug
```

또는 빌드 후 `bin/Debug/net10.0-windows/` 아래의 WPF 실행 파일을 실행한다.

## CLI 사용 예시

프로젝트 생성:

```bash
./llmide init
./llmide init --pid MyProject --name "내 프로젝트"
./llmide init --pid MyProject --name "내 프로젝트" --path C:\Work\MyProject
```

프로젝트 관리:

```bash
./llmide projects list
./llmide projects open MyProject
./llmide projects rename MyProject "새 표시 이름"
./llmide projects repid MyProject NewProjectId
./llmide projects move MyProject C:\Work\NewPath
./llmide projects remove MyProject
./llmide projects delete MyProject --confirm MyProject
```

프로젝트에 API Key가 있으면 삭제 시 추가 확인이 필요하다.

```bash
./llmide projects delete MyProject --confirm MyProject --confirm-api-key-delete
```

모델 관리:

```bash
./llmide models list MyProject
./llmide models set MyProject deepseek-reasoner
```

Criteria 관리:

```bash
./llmide criteria add MyProject --title "Korean" --description "항상 한국어로 답한다" --priority high
./llmide criteria list MyProject
./llmide criteria update MyProject <criterion-id> --description "한국어를 기본으로 사용한다"
./llmide criteria deactivate MyProject <criterion-id>
./llmide criteria activate MyProject <criterion-id>
./llmide criteria remove MyProject <criterion-id>
```

프로젝트 상태 관리:

```bash
./llmide state show MyProject
./llmide state set MyProject --stage phase9 --current-task "WPF GUI 적용"
./llmide state set MyProject --last-decision "상태 변경은 사용자가 승인한다"
./llmide state add MyProject completed "Phase 8.5 Agent Loop 완료"
./llmide state add MyProject next-action "Phase 9 GUI 보강"
./llmide state add MyProject blocker "없음"
./llmide state remove MyProject blocker "없음"
```

채팅:

```bash
./llmide chat MyProject "안녕"
./llmide chat MyProject "표와 코드 예제를 보여줘" --no-stream
./llmide chat MyProject "전송 맥락을 확인해줘" --debug
./llmide chat MyProject "이 산출물을 요약해줘" --artifact art_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

Agent Loop 실행:

```bash
./llmide agent-run MyProject "공식 문서를 찾아 요약해줘"
```

산출물 관리:

```bash
./llmide artifacts extract MyProject --content "<artifact type=\"markdown\" title=\"README 초안\"># README</artifact>"
./llmide artifacts add MyProject --title "README 초안" --type markdown --content "# README"
./llmide artifacts list MyProject
./llmide artifacts show MyProject art_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
./llmide artifacts update MyProject art_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx --content "# Updated"
./llmide artifacts remove MyProject art_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

## Provider 설정

신규 프로젝트 생성 후 다음 파일이 생성된다.

```text
<ProjectRoot>/.llmide/settings/providers.json
```

예시:

```json
{
  "default_provider": "deepseek",
  "providers": [
    {
      "name": "deepseek",
      "api_key": "",
      "endpoint": "https://api.deepseek.com/chat/completions",
      "model": "deepseek-chat"
    }
  ]
}
```

DeepSeek API Key는 저장소에 포함하지 않는다.
사용자가 별도로 발급받아 프로젝트 생성 화면이나 로컬 `providers.json`에 입력한다.

## Agent 도구

Phase 8.5에서 읽기 전용 Agent 도구가 추가되었다.

* `web_search`: DuckDuckGo HTML 기반 검색
* `fetch_url`: HTTP/HTTPS URL 본문 조회

도구는 모델이 `list_tools`로 사용 가능 목록을 요청한 뒤 `tool_request`로 호출한다.
도구 호출 이력은 다음 위치에 남는다.

```text
<ProjectRoot>/.llmide/conversations/tool-calls.jsonl
conversation.db의 agent_tool_calls 테이블
```

전역 allowlist는 다음 파일로 제한할 수 있다.

```text
<IdeProgramRoot>/settings/agent-tools.json
```

빈 allowlist는 전체 허용을 의미한다.

## 주요 문서

개발 기준 문서:

* `Docs/1.Product_Principles.md`
* `Docs/2.Architecture_Baseline.md`
* `Docs/3.Development_Roadmap.md`
* `Docs/4.Change_Decision_Log.md`
* `Docs/Code_Convention.md`

개발 중 로드맵이나 구조가 바뀌면 기준 문서를 먼저 갱신하고, 프로그램과 테스트를 함께 수정한 뒤 빌드와 테스트를 통과시킨다.

## 주의사항

* API Key와 개인 프로젝트 데이터는 커밋하지 않는다.
* `bin/`, `obj/`, `backup/` 폴더는 커밋 대상에서 제외한다.
* `.llmide`는 프로젝트별 로컬 메타데이터 저장소다.
* 현재 기준 Provider는 DeepSeek이며, Core는 Provider 교체 가능한 구조를 유지한다.
