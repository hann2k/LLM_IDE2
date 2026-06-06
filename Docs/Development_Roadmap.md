# LLM IDE Project Folder Structure v0.5

## 0. 개발환경

개발언어 및 버전:

```text
C# / .NET 10
Target Framework: net10.0
```

필요 라이브러리:

```text
Microsoft.Data.Sqlite 10.0.8
```

라이브러리 용도:

* `Microsoft.Data.Sqlite`: 프로젝트 단위 SQLite 대화 저장소 구현

---

## 1. 핵심 방향

Project는 이름 기반으로 관리한다.
프로젝트 이름은 IDE가 프로젝트를 찾고 선택하는 기본 키다.

프로젝트 이름 저장소는 IDE 실행 프로그램 하부의 고정 폴더에 둔다.

```text
<IdeProgramRoot>/
 ├─ llmide
 ├─ LlmIde.Core.dll
 ├─ LlmIde.Infrastructure.dll
 ├─ project/
 │   └─ projects.json
 └─ <ProjectName>/
```

`<IdeProgramRoot>/project` 위치는 항상 고정이다.
이 폴더는 프로젝트 정보만 관리하는 IDE 전용 저장소다.
실제 프로젝트 데이터는 이 저장소 안에 두지 않는다.

프로젝트 데이터는 실제 프로젝트 폴더 아래 `.llmide`에 저장한다.

```text
<ProjectRoot>/
 ├─ .llmide/
 └─ 사용자 작업 파일들
```

`.llmide` 폴더는 IDE가 프로젝트 상태, 기준, 정책, 대화 로그, 산출물, 파일 인덱스를 저장하는 메타데이터 영역이다.

프로젝트 생성 규칙은 다음과 같다.

* 프로젝트 이름은 필수 항목이다.
* 빈 이름으로 생성 요청이 들어오면 프로젝트 이름은 `DefaultProject`로 해석한다.
* `DefaultProject`가 이미 있는 상태에서 빈 이름으로 생성 요청이 들어오면, "DefaultProject가 이미 존재한다"는 오류를 표시하고 중단한다.
* 이 중복 오류 상황에서는 파일 저장과 폴더 생성을 금지한다.
* 경로가 비어 있으면 `<IdeProgramRoot>/<ProjectName>` 폴더를 생성하고 그 아래에 프로젝트 데이터를 저장한다.
* 이름과 경로가 모두 지정되면 이름은 `<IdeProgramRoot>/project/projects.json`에 저장하고, 실제 프로젝트 폴더는 사용자가 지정한 경로에 생성한다.
* 프로젝트 저장소 내부에는 프로젝트 이름, 경로, 생성일자를 저장한다.
* 프로젝트 이름과 경로는 수정 가능해야 한다.
* 프로젝트 이름은 프로젝트 목록, 열기, 제거, 선택의 기본 키로 사용한다.
* 같은 이름의 프로젝트가 이미 있으면 기존 프로젝트를 열거나 명시적 갱신 명령을 사용한다.
* 권한 오류가 발생하면 오류를 표시하고 즉시 중단한다.
* 프로젝트 폴더를 생성한 뒤 오류가 발생한 경우, 생성된 프로젝트 폴더 안에 초기화 진행 단계를 기록한다.

이 구조에서는 다음 개념을 사용하지 않는다.

* Session
* Handoff

API 기반 구조이므로 장기 세션을 유지하지 않는다.
새 요청은 매번 IDE가 필요한 맥락을 조립하여 Provider API에 전달한다.

대화 원본 저장과 API에 주입하는 맥락은 분리한다.
모든 원본 대화는 장기 기록으로 저장하지만, 매 API 요청마다 과거 전체 원문을 전송하지 않는다.
기본 맥락 관리 전략은 `Summary + Window`로 한다.

과거 대화는 `Rolling Context Summary`로 압축하여 전달하고, 최근 대화는 제한된 원문 `Recent Window`로 전달한다.
현재 사용자 요청은 항상 원문 그대로 전달한다.
맥락 압축은 IDE 내부 알고리즘이 아니라 LLM Provider API를 이용해 수행한다.

맥락 압축 원칙:

```text
원본은 저장한다.
과거는 압축해서 넣는다.
최근은 원문으로 유지한다.
압축은 LLM에게 맡긴다.
```

### Agent Boundary Principle

LLM IDE는 에이전트 기능을 수용할 수 있는 구조로 설계한다.
그러나 MVP에서는 자율 실행 에이전트를 구현하지 않는다.

AI는 다음을 할 수 있다.

* 응답 생성
* 요약
* 상태 변경 후보 제안
* 산출물 초안 생성
* 파일/맥락 기반 분석

AI는 사용자 승인 없이 다음을 할 수 없다.

* Project State 변경
* Criteria 변경
* Artifact 확정 저장
* 파일 수정
* 프로젝트 삭제
* 외부 명령 실행
* 다음 작업 자동 실행

이 원칙은 LLM IDE의 정체성이다.
LLM IDE는 AI가 작업을 제안하고 사용자가 승인하는 구조를 기본으로 한다.

또한 MVP 단계에서는 GUI보다 핵심 기능 구현을 우선한다.

```text
MVP = CLI 기반 구현
차기 단계 = WPF GUI 적용
```

모든 핵심 기능은 CLI 환경에서 먼저 구현하며, 이후 WPF GUI는 동일한 Core 라이브러리를 사용하는 방식으로 추가한다.

---

## 2. Context Management Strategy

LLM IDE의 기본 맥락 관리 전략은 다음 구조다.

```text
Raw Conversation Log
 + Rolling Context Summary
 + Recent Window
```

### Raw Conversation Log

Raw Conversation Log는 사용자와 AI의 모든 대화 원문을 저장한다.

용도:

* 감사
* 재현
* 검색
* 디버깅
* 추적

저장 위치:

* SQLite `conversation.db`
* JSONL `messages.jsonl`

Raw Conversation Log는 장기 기록의 원본이지만, 매 API 요청에 전체 원문으로 포함하지 않는다.

### Rolling Context Summary

Rolling Context Summary는 다음 요청에 필요한 과거 맥락을 압축한 요약이다.
이 요약은 LLM Provider API를 사용해 생성한다.
Rolling Context Summary는 원본 대화 로그를 대체하지 않는다.

압축 실패 시 기존 Summary를 유지한다.
사용자 응답이 정상적으로 생성된 경우, 압축 실패는 chat 실패로 처리하지 않는다.

### Recent Window

Recent Window는 최근 N개의 user/assistant 발화를 원문 그대로 포함한다.

목적:

* 직전 흐름 유지
* 지시어 참조 유지
* 말투와 형식 유지
* 짧은 문맥 연속성 보장

Recent Window는 Rolling Context Summary를 보완한다.

### 기본 조립 알고리즘

```text
[과거 맥락] Rolling Context Summary
+
[최근 대화] Recent Window
+
[현재 요청] User Request
```

Sliding Window는 Recent Window 구현에만 사용한다.
RAG는 MVP에서 제외하고 File Store 구현 이후 다시 검토한다.

적용 알고리즘:

| 알고리즘 | 적용 여부 | LLM IDE에서의 역할 |
| --- | --- | --- |
| Sliding Window | 부분 채택 | Recent Window 구현에 사용 |
| Conversation Summary Buffer | 채택 | Rolling Context Summary 생성에 사용 |
| Summary + Window | 기본 전략 | 장기 대화 관리 기본 구조 |
| Session-based RAG | 보류 | File Store 이후 후속 검토 |

---

## 3. 수정된 프로젝트 구성

IDE 프로그램 하부의 프로젝트 이름 저장소:

```text
<IdeProgramRoot>/
 └─ project/
     └─ projects.json
```

`projects.json`은 프로젝트 이름을 키로 사용한다.
프로젝트 저장소는 프로젝트 정보만 저장하며 실제 프로젝트 데이터는 저장하지 않는다.

```json
{
  "projects": [
    {
      "name": "DefaultProject",
      "path": "<ProjectRoot>",
      "created_at": ""
    }
  ]
}
```

프로젝트 이름과 경로는 이후 수정할 수 있어야 한다.

실제 프로젝트 폴더:

```text
<ProjectRoot>/
 ├─ .llmide/
 │   ├─ project.json
 │   ├─ init-progress.json
 │   ├─ project-state.json
 │   ├─ criteria.json
 │   ├─ policies/
 │   │   ├─ system-rule.md
 │   │   ├─ compression-rule.md
 │   │   ├─ context-policy.json
 │   │   └─ provider-policy.json
 │   ├─ context/
 │   │   └─ context-notes.json
 │   ├─ conversations/
 │   │   ├─ conversation.db
 │   │   ├─ messages.jsonl
 │   │   ├─ requests.jsonl
 │   │   ├─ context-packages/
 │   │   │   └─ <request_id>.json
 │   │   └─ rolling-context/
 │   │       ├─ current.md
 │   │       ├─ history/
 │   │       │   └─ rctx_000001.md
 │   │       └─ index.jsonl
 │   ├─ artifacts/
 │   │   ├─ artifacts.index.json
 │   │   └─ <artifact_id>.md
 │   ├─ files/
 │   │   ├─ files.index.json
 │   │   ├─ extracted/
 │   │   │   └─ <file_id>.txt
 │   │   └─ summaries/
 │   │       └─ <file_id>.md
 │   ├─ state-candidates/
 │   │   └─ candidates.jsonl
 │   ├─ settings/
 │   │   ├─ providers.json
 │   │   └─ ui.json
 │   ├─ cache/
 │   └─ lock
 │
 └─ 사용자 실제 작업 파일들
```

---

## 4. 애플리케이션 구조

초기 구현은 CLI 기반으로 진행한다.

```text
Solution
 ├─ LlmIde.Core
 ├─ LlmIde.Infrastructure
 ├─ LlmIde.Cli
 ├─ LlmIde.Server (후속 단계)
 └─ LlmIde.Wpf (후속 단계)
```

### LlmIde.Core

비즈니스 로직 계층

포함 기능:

* Project 관리
* 프로젝트 이름 저장소 관리
* Criteria 관리
* Project State 관리
* Context Builder
* Artifact 관리
* State Candidate 관리
* Provider 추상화

### LlmIde.Infrastructure

외부 시스템 연동 계층

포함 기능:

* 파일 저장소
* JSON/JSONL 저장
* Provider 구현
* Credential 저장소
* 파일 추출기

### LlmIde.Cli

MVP 실행 환경

포함 기능:

* 프로젝트 초기화
* 프로젝트 열기
* 프로젝트 목록 관리
* 대화
* Criteria 관리
* State 관리
* Artifact 관리

### LlmIde.Server

후속 로컬 서버 계층

목표:

```text
CLI와 WPF 외부에서도 LLM IDE 기능을 호출할 수 있게 한다.
```

포함 기능:

* 로컬 TCP 서버
* 로컬 UDP 서버
* 로컬 HTTP API 검토
* OpenAI/DeepSeek 호환 형식의 요청 중계 검토
* Project 관리 API
* Provider 모델 조회 API
* Conversation 요청 API
* Criteria 관리 API
* Artifact 관리 API

원칙:

```text
Server는 Core를 호출하는 어댑터 계층이다.
비즈니스 로직은 Core에만 존재한다.
```

서버 계층은 로컬 프로그램에서 LLM 웹서비스처럼 호출하거나, 에이전트 도구 런타임처럼 호출하는 양쪽 가능성을 열어둔다.

### LlmIde.Wpf

후속 GUI 계층

원칙:

```text
WPF는 Core를 호출하는 UI 계층이다.
비즈니스 로직은 Core에만 존재한다.
```

---

## 5. 제거된 구성

아래 항목은 프로젝트 구조에서 제거한다.

```text
.llmide/handoff/
.llmide/handoff/handoff.index.json
.llmide/handoff/<handoff_id>.md
```

또한 MVP 개발 범위에서도 Handoff Generator를 제외한다.

---

## 6. 수정된 핵심 모델

```text
Project Registry
 ├─ Project Name
 ├─ Project Path
 └─ Created At

Project
 ├─ Project Info
 ├─ Criteria
 ├─ Policies
 ├─ Project State
 ├─ Context Notes
 ├─ Conversation Log
 ├─ Request Log
 ├─ Context Packages
 ├─ Artifacts
 ├─ Files
 └─ State Update Candidates
```

추가로 애플리케이션 수준에서 프로젝트 이름 저장소를 관리한다.

```text
Application
 └─ Project Registry
```

프로젝트 이름 저장소는 사용자 환경(AppData)이 아니라 IDE 실행 프로그램 하부의 고정 경로 `<IdeProgramRoot>/project`에 저장한다.

---

## 7. 요청 실행 구조

API 호출의 실행 단위는 Request다.

```text
User Input
 → Context Builder
 → Context Package 생성
 → Provider API Request
 → Assistant Response
 → Raw Conversation Log 저장
 → Request Log 저장
 → Compression Request 실행
 → Rolling Context Summary 갱신
```

Compression Request는 사용자 응답 이후 별도의 내부 LLM 요청으로 실행한다.
Compression Request 실패는 chat 실패가 아니다.

CLI와 WPF는 동일한 실행 흐름을 사용한다.

```text
CLI
  └─ Core
      └─ Provider

WPF
  └─ Core
      └─ Provider
```

---

## 8. Context Package의 역할

Context Package는 특정 API 요청에 실제로 들어간 맥락 스냅샷이다.

```json
{
  "request_id": "req_001",
  "system_rule": "",
  "active_criteria": [],
  "project_state": {},
  "rolling_context_summary": "",
  "context_notes": [],
  "attached_messages": [],
  "attached_artifacts": [],
  "attached_files": [],
  "recent_turns": [],
  "user_request": ""
}
```

이 파일은 API 기반 구조에서 요청 재현성과 추적성을 보장하는 핵심 데이터다.

API 요청에 포함되는 맥락 구조:

```text
[System Rule]
[Active Criteria]
[Project State]
[Rolling Context Summary]
[Recent Window]
[Manual Attachments]
[Artifacts]
[User Request]
```

규칙:

* 과거 전체 대화 원문은 포함하지 않는다.
* 과거 대화는 Rolling Context Summary로 표현한다.
* 최근 대화만 제한된 개수의 원문으로 포함한다.
* 현재 사용자 요청은 항상 원문으로 포함한다.
* Manual Attachments는 사용자가 명시적으로 선택한 경우에만 포함한다.

우선순위:

1. System Rule
2. Active Criteria
3. Project State
4. User Request
5. Manual Attachments
6. Artifacts
7. Rolling Context Summary
8. Recent Window

---

## 9. 정책 파일

### compression-rule.md

초기 Compression Rule은 다음 내용으로 생성한다.

```markdown
# Compression Rule

너는 대화 맥락 압축기다.

목표:
- 다음 요청에 필요한 과거 맥락만 보존한다.
- 기존 Rolling Context Summary와 현재 user/assistant 대화를 병합한다.
- 중복은 제거한다.
- 원문에 없는 내용을 추가하지 않는다.
- 추정하지 않는다.
- 불확실한 내용은 확정 사실처럼 쓰지 않는다.

반드시 보존할 항목:
- 프로젝트 정체성
- 현재 진행 상태
- 확정 결정
- 활성 제약
- 중요한 기술 세부사항
- 미해결 질문
- 사용자가 명시한 선호
- 앞으로 착각하면 안 되는 내용

제거할 항목:
- 단순 인사
- 반복 설명
- 해결된 사소한 논의
- 장기 맥락에 불필요한 문장

출력 형식은 Markdown으로 한다.
```

### Rolling Context Summary 출력 형식

```markdown
# Rolling Context Summary

## Project Identity

## Current Progress

## Confirmed Decisions

## Active Constraints

## Important Technical Details

## Open Questions

## Recent Context To Preserve

## Do Not Assume
```

### rolling-context/index.jsonl

```json
{
  "rolling_context_id": "rctx_000001",
  "source_request_id": "req_000001",
  "previous_rolling_context_id": null,
  "content_path": "conversations/rolling-context/history/rctx_000001.md",
  "is_current": true,
  "created_at": "",
  "provider": "deepseek",
  "model": "deepseek-chat",
  "status": "completed",
  "error": null
}
```

`status` 값:

* `completed`
* `failed`
* `superseded`

새 Summary는 `history/`에 저장하고 `current.md`를 갱신한다.
이전 Summary는 삭제하지 않는다.

### context-policy.json 확장

```json
{
  "context_management_strategy": "summary_plus_window",
  "include_rolling_context_summary": true,
  "rolling_context_path": "conversations/rolling-context/current.md",
  "recent_turn_count": 3,
  "max_recent_turn_chars": 12000,
  "max_rolling_context_chars": 24000,
  "compression_enabled": true,
  "compression_provider": "deepseek",
  "compression_model": "deepseek-chat",
  "compression_after_chat": true,
  "compression_failure_strategy": "keep_previous",
  "rag_enabled": false
}
```

`context_management_strategy` 허용 값:

* `window_only`
* `summary_only`
* `summary_plus_window`
* `rag`

MVP 기본값은 `summary_plus_window`다.

`compression_failure_strategy` 허용 값:

* `keep_previous`
* `clear_current`
* `fail_chat`

MVP 기본값은 `keep_previous`다.

---

## 10. MVP 최소 파일

IDE 프로그램 하부 고정 저장소:

```text
<IdeProgramRoot>/
 └─ project/
     └─ projects.json
```

초기 MVP에서 필요한 최소 구조는 다음이다.

```text
<ProjectRoot>/
 └─ .llmide/
     ├─ project.json
     ├─ init-progress.json
     ├─ project-state.json
     ├─ criteria.json
     ├─ policies/
     │   ├─ system-rule.md
     │   ├─ compression-rule.md
     │   ├─ context-policy.json
     │   └─ provider-policy.json
     ├─ conversations/
     │   ├─ conversation.db
     │   ├─ messages.jsonl
     │   ├─ requests.jsonl
     │   ├─ context-packages/
     │   └─ rolling-context/
     │       ├─ current.md
     │       ├─ history/
     │       └─ index.jsonl
     ├─ settings/
     │   └─ providers.json
     └─ artifacts/
         └─ artifacts.index.json
```

후순위로 추가할 항목:

```text
context/context-notes.json
files/files.index.json
state-candidates/candidates.jsonl
settings/ui.json
cache/
lock
```

---

## 11. Request Log와 SQLite 확장

Chat Request 기록 필드:

```text
request_type: chat
used_rolling_context_id
rolling_context_path
recent_turn_count
compression_request_id
compression_status
compression_error
```

Compression Request는 별도 Request로 저장한다.

```text
request_type: compression
source_chat_request_id
provider
model
status
error
```

`request_type` 허용 값:

* `chat`
* `compression`
* `model_list`
* `state_candidate`

SQLite에는 `rolling_context_summaries` 테이블을 추가한다.

```text
rolling_context_id
source_request_id
previous_rolling_context_id
content
content_path
created_at
provider
model
status
error
is_current
```

후속 검토 테이블:

```text
context_package_items
 ├─ context_package_id
 ├─ request_id
 ├─ item_type
 ├─ item_id
 ├─ source_path
 ├─ included_chars
 ├─ priority
 └─ created_at
```

MVP 구현 우선순위:

1. `rolling-context/current.md`
2. `rolling-context/history/<rolling_context_id>.md`
3. `rolling-context/index.jsonl`
4. SQLite `rolling_context_summaries`
5. `context_package_items`

---

## 12. Compression Request 구조

Compression Request 입력:

```text
[Compression Rule]

[Previous Rolling Context Summary]
기존 current.md 내용

[Current User Message]
이번 user 원문

[Current Assistant Response]
이번 assistant 원문

[Task]
위 내용을 병합하여 다음 요청에 사용할 Rolling Context Summary를 갱신하라.
```

규칙:

* 과거 전체 원문 대화를 포함하지 않는다.
* 이전 Summary와 현재 user/assistant 대화만 포함한다.
* 사용자에게 출력하지 않는다.
* 결과는 `rolling-context/` 아래에 저장한다.

Compression 실패 정책:

* 사용자 응답이 성공했고 raw log 저장이 끝난 뒤 compression이 실패하면 사용자 응답은 정상으로 유지한다.
* raw user/assistant message는 유지한다.
* 기존 `current.md`는 유지한다.
* `requests.jsonl`에 `compression_failed`를 기록한다.
* 다음 chat은 이전 Summary를 사용한다.

금지:

* Compression 실패 때문에 사용자 응답을 폐기하지 않는다.
* Raw Conversation Log를 롤백하지 않는다.
* 빈 Summary로 덮어쓰지 않는다.

---

## 13. MVP 개발 절차

### Phase 1. 프로젝트 초기화 및 프로젝트 목록 관리 (CLI)

목표:

* 프로젝트를 이름 기반으로 초기화한다.
* IDE 프로그램 하부의 고정 `project` 저장소에서 프로젝트 목록을 관리한다.
* 프로젝트 이름과 실제 프로젝트 경로를 매핑한다.

구현 항목:

* IDE 프로그램 루트 확인
* `<IdeProgramRoot>/project` 폴더 생성
* `<IdeProgramRoot>/project/projects.json` 생성
* 프로젝트 이름 필수 처리
* 빈 이름 입력 시 기본값 처리: `DefaultProject`
* 기존 `DefaultProject` 중복 생성 차단
* 중복 오류 시 파일 저장 및 폴더 생성 금지
* 프로젝트 이름 중복 확인
* 경로 미지정 시 `<IdeProgramRoot>/<ProjectName>` 생성
* 경로 지정 시 지정 경로에 프로젝트 폴더 생성
* 프로젝트 이름, 경로, 생성일자 저장
* 프로젝트 이름 수정
* 프로젝트 경로 수정
* `.llmide` 폴더 생성
* init-progress.json 생성 및 단계별 진행 상태 기록
* 기본 설정 파일 생성
* project.json 생성
* project-state.json 생성
* criteria.json 생성
* 프로젝트 목록 조회
* 프로젝트 이름으로 열기
* 프로젝트 이름으로 목록에서 제거
* 프로젝트 이름으로 실제 프로젝트 폴더 삭제
* 실제 프로젝트 폴더 삭제 시 프로젝트 이름 재확인 강제
* API Key가 있는 프로젝트 삭제 시 추가 확인 강제
* 권한 오류 표시 및 중단
* 실행 파일을 옵션 없이 실행하면 사용법 출력
* 사용법에는 가능한 명령 조합 전체 출력
* CLI 명령 구현

예시:

```bash
llmide
llmide init
llmide init --name MyProject
llmide init --path /Users/me/Projects/DefaultProject
llmide init --name MyProject --path /Users/me/Projects/MyProject
llmide projects list
llmide projects open MyProject
llmide projects remove MyProject
llmide projects delete MyProject --confirm MyProject
llmide projects delete MyProject --confirm MyProject --confirm-api-key-delete
llmide projects rename MyProject NewProjectName
llmide projects move MyProject /Users/me/Projects/NewPath
```

완료 기준:

* 이름 없이 초기화하면 `DefaultProject`로 해석된다.
* 기존 `DefaultProject`가 없는 경우 `DefaultProject`가 생성된다.
* 기존 `DefaultProject`가 있는 상태에서 이름 없이 초기화하면 오류를 표시하고 중단한다.
* 기존 `DefaultProject` 중복 오류에서는 파일 저장과 폴더 생성이 발생하지 않는다.
* 경로 없이 초기화하면 `<IdeProgramRoot>/<ProjectName>` 아래에 프로젝트 데이터가 생성된다.
* 이름과 경로를 모두 지정하면 이름은 고정 프로젝트 저장소에 등록되고 데이터는 지정 경로에 생성된다.
* 프로젝트 저장소에는 이름, 경로, 생성일자가 저장된다.
* 프로젝트 이름과 경로를 수정할 수 있다.
* 프로젝트 목록은 이름 기준으로 조회된다.
* 기존 프로젝트를 이름으로 열 수 있다.
* 프로젝트를 이름으로 목록에서 제거할 수 있다.
* `remove`는 프로젝트 저장소 목록에서만 제거하고 실제 프로젝트 폴더는 삭제하지 않는다.
* `delete`는 실제 프로젝트 폴더를 삭제하고 프로젝트 저장소 목록에서도 제거한다.
* `delete`는 `--confirm <project-name>` 없이는 실행되지 않는다.
* 프로젝트에 API Key가 있으면 `delete`는 `--confirm-api-key-delete` 없이는 실행되지 않는다.
* 권한 오류가 발생하면 오류를 표시하고 초기화를 중단한다.
* 프로젝트 폴더 생성 후 실패한 경우 init-progress.json에서 실패 단계를 확인할 수 있다.
* 옵션 없이 실행하면 사용법이 출력된다.
* 사용법에는 init, list, open, remove, delete, rename, move의 가능한 조합이 출력된다.
* IDE 재실행 후에도 프로젝트 이름 목록이 유지된다.

---

### Phase 2. Provider 기반 대화 (CLI)

목표:

* DeepSeek API를 이용한 기본 대화를 구현한다.

구현 항목:

* Provider 인터페이스 정의
* DeepSeek Provider 구현
* API Key 관리
* API Key 기본값은 빈 문자열로 생성
* Provider 모델 목록 조회
* 프로젝트 기본 모델 교체
* 요청/응답 처리
* 대용량 응답 스트리밍 출력
* Markdown 표와 코드 블록 원문 출력
* CLI 채팅 명령 구현

예시:

```bash
llmide chat MyProject "안녕"
llmide models list MyProject
llmide models set MyProject deepseek-reasoner
```

완료 기준:

* 프로젝트 생성 시 `.llmide/settings/providers.json`에 DeepSeek 설정이 생성된다.
* DeepSeek API Key 값은 빈 문자열로 생성된다.
* DeepSeek API에서 사용 가능한 모델 목록을 조회할 수 있다.
* 프로젝트 기본 모델을 CLI에서 교체할 수 있다.
* 사용자 메시지를 전송할 수 있다.
* AI 응답을 받을 수 있다.
* 긴 응답은 도착하는 대로 CLI에 출력된다.
* 표와 코드 블록은 Markdown 원문 형식으로 출력된다.

---

### Phase 3. 대화 로그 저장

목표:

* 모든 대화를 프로젝트에 저장한다.
* SQLite를 프로젝트 단위 주 저장소로 사용한다.
* JSONL/JSON 파일은 보조 기록, 디버깅, 요청 재현용으로 유지한다.
* 다음 대화 요청에는 최근 대화 맥락을 자동으로 포함한다.

구현 항목:

* `.llmide/conversations/conversation.db` 생성
* SQLite `conversation_turns` 테이블 저장
* SQLite `conversation_messages` 테이블 저장
* SQLite `context_packages` 테이블 저장
* messages.jsonl 저장
* requests.jsonl 저장
* 요청별 Context Package 저장
* chat 명령 실행 시 사용자 메시지 저장
* chat 명령 응답 수신 시 assistant 메시지 저장
* 다음 chat 명령 실행 시 SQLite에 저장된 최근 user/assistant 메시지 주입
* 요청 재현을 위한 request_id 연결
* SQLite 기록과 JSONL/JSON 보조 기록이 같은 request_id를 공유하도록 처리

완료 기준:

* 프로젝트 초기화 시 `.llmide/conversations/conversation.db`가 생성된다.
* 사용자 메시지와 AI 응답이 저장된다.
* 사용자 메시지와 AI 응답이 SQLite의 같은 conversation turn에 연결된다.
* 요청별 Context Package가 SQLite와 JSON 파일 양쪽에 저장된다.
* 두 번째 이후 대화는 이전 user/assistant 메시지를 포함해 Provider API에 요청한다.
* 어떤 맥락으로 응답이 생성되었는지 추적 가능하다.
* conversation_turns, conversation_messages, context_packages가 request_id로 연결된다.
* messages.jsonl, requests.jsonl, context-packages/<request_id>.json이 서로 같은 request_id로 연결된다.

---

### Phase 4. Criteria 관리

목표:

* 프로젝트 기준을 저장하고 요청에 주입한다.

구현 항목:

* Criteria 생성
* Criteria 수정
* Criteria 삭제
* Criteria 활성화
* Criteria 비활성화
* criteria.json 저장
* Active Criteria 주입
* 요청별 Context Package에 Active Criteria 기록
* Active Criteria가 없으면 chat 요청 중단
* CLI 관리 명령 구현

예시:

```bash
llmide criteria add MyProject --title "Korean" --description "항상 한국어로 답한다" --priority high
llmide criteria list MyProject
llmide criteria update MyProject <criterion-id> --description "한국어를 기본으로 사용한다"
llmide criteria deactivate MyProject <criterion-id>
llmide criteria activate MyProject <criterion-id>
llmide criteria remove MyProject <criterion-id>
```

완료 기준:

* 활성 기준이 API 요청에 포함된다.
* 활성 기준은 system 메시지로 Provider 요청에 포함된다.
* 비활성 기준은 Provider 요청에 포함되지 않는다.
* 활성 기준이 하나도 없으면 Provider API로 전송하지 않고 중단한다.
* Criteria를 생성, 조회, 수정, 삭제할 수 있다.
* Criteria를 활성화, 비활성화할 수 있다.
* 요청별 Context Package에서 사용된 활성 기준을 확인할 수 있다.
* 요청 로그에서 사용된 기준을 확인할 수 있다.

---

### Phase 5. Project State 관리

목표:

* 프로젝트 상태를 별도로 저장한다.

구현 항목:

* Stage 관리
* Current Task 관리
* Completed Items 관리
* Next Actions 관리
* Blockers 관리
* CLI 상태 관리 명령 구현

완료 기준:

* 프로젝트 상태를 저장하고 수정할 수 있다.
* API 요청 시 Project State가 포함된다.

---

### Phase 6. Rolling Context Compression

목표:

* 장기 대화 맥락을 Rolling Context Summary로 관리한다.
* 압축은 LLM Provider API를 이용한다.
* 원본 대화 로그와 압축 맥락을 분리해 저장한다.

구현 항목:

* `conversations/rolling-context/` 폴더 생성
* `rolling-context/current.md` 생성
* `rolling-context/history/` 생성
* `rolling-context/index.jsonl` 생성
* `policies/compression-rule.md` 생성
* `context-policy.json` 압축 정책 확장
* SQLite `rolling_context_summaries` 테이블 추가
* chat 응답 이후 Compression Request 실행
* 이전 Summary와 현재 user/assistant 대화를 병합
* 새 Summary를 `history/`에 저장
* `current.md` 갱신
* 기존 Summary를 `superseded` 상태로 기록
* Compression Request를 Request Log에 별도로 기록
* Compression 실패 시 기존 Summary 유지

완료 기준:

* chat 이후 Rolling Context Summary가 생성된다.
* 다음 chat 요청에 `current.md`가 포함된다.
* 원본 대화 로그는 삭제되거나 덮어써지지 않는다.
* Compression Request가 일반 chat request와 구분되어 기록된다.
* Compression 실패 시 사용자 응답은 정상 유지된다.
* Compression 실패 시 기존 Summary가 유지된다.

---

### Phase 7. Context Builder v1

목표:

* 요청에 필요한 맥락을 조립한다.
* Summary + Window 전략을 적용한다.

구현 항목:

* System Rule 주입
* Active Criteria 주입
* Project State 주입
* Rolling Context Summary 주입
* Recent Window 주입
* Manual Attachments 주입
* Artifact 주입
* Context Package 생성
* 요청별 사용된 `rolling_context_id` 기록
* 최근 대화 포함 개수와 문자 수 제한 적용

완료 기준:

* 요청마다 Context Package가 생성된다.
* 동일한 요청을 재현할 수 있다.
* 과거 전체 대화 원문은 요청에 포함되지 않는다.
* Rolling Context Summary와 Recent Window가 함께 주입된다.
* Context Package에서 어떤 Summary와 최근 대화가 사용되었는지 확인할 수 있다.

---

### Phase 8. Artifact 저장

목표:

* 중요한 응답을 재사용 가능한 산출물로 저장한다.

구현 항목:

* Artifact 생성
* Artifact 수정
* Artifact 목록 조회
* Artifact 첨부
* CLI 관리 명령 구현

완료 기준:

* 응답을 Artifact로 저장할 수 있다.
* Artifact를 이후 요청에 첨부할 수 있다.

---

### Phase 9. Context Notes

목표:

* 장기 맥락을 별도 저장한다.

구현 항목:

* Context Note 생성
* Context Note 수정
* Scope 관리
* 요청 주입

완료 기준:

* 항상 포함할 맥락과 선택 포함 맥락을 구분할 수 있다.

---

### Phase 10. File Store

목표:

* 프로젝트 파일을 AI 맥락으로 활용한다.
* MVP에서는 파일 등록, 추출, 요약, 명시적 첨부까지만 구현한다.
* RAG 검색은 MVP에서 제외하고 File Store 안정화 이후 검토한다.

구현 항목:

* 파일 등록
* 텍스트 추출
* 파일 요약
* 요청 첨부

완료 기준:

* 파일 내용을 요청에 포함할 수 있다.
* 포함 이력을 추적할 수 있다.

후속 검토 항목:

* `file_chunks`
* embedding index
* semantic retrieval
* session/project RAG
* query-based file context selection

---

### Phase 11. State Update Candidate

목표:

* AI가 상태 변경 후보를 제안하고 사용자가 승인한다.

구현 항목:

* 상태 변경 후보 생성
* 승인
* 거부
* 적용

완료 기준:

* AI는 후보만 생성한다.
* 사용자가 승인한 내용만 실제 상태에 반영된다.

---

### Phase 12. WPF GUI 적용

목표:

* CLI에서 구현된 기능을 GUI로 제공한다.

구현 항목:

* Project Explorer
* Project Registry
* 프로젝트 완전 삭제 확인 UI
* Conversation View
* Criteria Editor
* Project State Editor
* Artifact Manager
* Context Manager
* Provider Settings

완료 기준:

* Core 로직 수정 없이 GUI를 적용할 수 있다.
* CLI와 WPF가 동일한 프로젝트 데이터를 사용한다.
* GUI에서 프로젝트 완전 삭제는 사용자 확인 후 실행한다.
* 모든 핵심 기능이 GUI에서도 동작한다.

---

### Phase 13. Local Server 적용

목표:

* CLI와 GUI 외부의 로컬 프로그램이 LLM IDE 기능을 호출할 수 있게 한다.
* LLM IDE를 로컬 LLM 웹서비스 또는 에이전트 도구 런타임처럼 사용할 수 있게 한다.

구현 항목:

* `LlmIde.Server` 프로젝트 추가
* 로컬 TCP 서버 구현
* 로컬 UDP 서버 구현
* 로컬 HTTP API 적용 여부 검토
* 서버 시작/중지 명령
* 서버 포트 설정
* Project 목록/열기 API
* Provider 모델 목록 조회 API
* Provider 기본 모델 교체 API
* Chat 요청 API
* Streaming Chat 응답 API
* Criteria 생성/수정/삭제/활성화/비활성화 API
* Artifact 조회/저장 API
* 로컬 접근 제한 및 보안 정책

완료 기준:

* Core 로직 수정 없이 Server 계층을 적용할 수 있다.
* CLI, WPF, Server가 동일한 Core 서비스를 사용한다.
* 로컬 프로그램이 TCP 또는 UDP로 프로젝트 기능을 호출할 수 있다.
* Chat 요청은 기존 Conversation Log와 Context Package에 기록된다.
* 활성 Criteria, Project State, Context가 서버 요청에도 동일하게 주입된다.
* Streaming 응답을 서버 프로토콜에서 처리할 수 있다.

---

## 14. MVP 완료 기준

MVP는 다음 조건을 만족해야 한다.

* 긴 대화에서도 과거 전체 원문을 매 요청에 포함하지 않는다.
* 과거 맥락은 Rolling Context Summary로 전달한다.
* 최근 대화는 Recent Window로 전달한다.
* Rolling Context Summary는 LLM Provider API를 통해 생성한다.
* Raw Conversation Log와 압축 맥락은 별도로 보존한다.
* Compression 실패 시 raw conversation과 사용자 응답은 유지한다.
* 각 요청에서 어떤 압축 맥락이 사용되었는지 추적할 수 있다.
* `Summary + Window`를 기본 맥락 관리 전략으로 사용한다.

---

## 15. 현재 확정 결정

* 개발 환경은 .NET 10이다.
* 주 개발 언어는 C#이다.
* GUI는 WPF로 구현한다.
* MVP는 CLI 기반으로 구현한다.
* WPF는 후속 단계에서 적용한다.
* 비즈니스 로직은 Core 라이브러리에 구현한다.
* CLI와 WPF는 동일한 Core를 사용한다.
* Project는 이름 기반으로 관리한다.
* 프로젝트 이름은 프로젝트 목록, 열기, 제거, 선택의 기본 키다.
* 프로젝트 이름은 필수 항목이다.
* 빈 이름은 `DefaultProject`로 해석한다.
* 기존 `DefaultProject`가 있는 상태에서 빈 이름으로 생성하면 오류를 표시하고 중단한다.
* 이 중복 오류 상황에서는 파일 저장과 폴더 생성을 금지한다.
* 프로젝트 이름 저장소는 IDE 실행 프로그램 하부 `<IdeProgramRoot>/project`에 둔다.
* `<IdeProgramRoot>/project` 위치는 항상 고정이다.
* `<IdeProgramRoot>/project`는 프로젝트 정보만 관리하고 실제 프로젝트 데이터는 저장하지 않는다.
* 프로젝트 저장소에는 프로젝트 이름, 경로, 생성일자를 저장한다.
* 프로젝트 이름과 경로는 수정 가능해야 한다.
* 경로가 지정되지 않으면 `<IdeProgramRoot>/<ProjectName>` 아래에 프로젝트 데이터를 저장한다.
* 이름과 경로가 모두 지정되면 이름은 고정 프로젝트 저장소에 저장하고, 실제 프로젝트 폴더는 지정 경로에 생성한다.
* IDE 메타데이터는 Project Root 아래 `.llmide` 폴더에 저장한다.
* 권한 오류가 발생하면 오류를 표시하고 중단한다.
* 프로젝트 폴더 생성 후 실패하면 초기화 진행 단계를 프로젝트 폴더에 기록한다.
* 프로젝트 이름 목록 관리 기능을 제공한다.
* API 기반 구조이므로 Session 개념은 사용하지 않는다.
* API 기반 구조이므로 Handoff 기능도 사용하지 않는다.
* Conversation Log는 Project에 직접 귀속된다.
* Request Log는 API 호출 단위로 저장한다.
* Context Package는 각 요청에 실제 주입된 맥락 스냅샷이다.
* 대화 원본 저장과 API 요청 맥락 주입은 분리한다.
* 모든 원본 대화는 SQLite와 JSONL에 보존한다.
* 과거 장기 맥락은 전체 원문이 아니라 Rolling Context Summary로 전달한다.
* 최근 대화는 제한된 Recent Window로 원문 전달한다.
* 기본 맥락 관리 전략은 `Summary + Window`다.
* Sliding Window는 Recent Window 구현에만 사용한다.
* Conversation Summary Buffer 방식은 Rolling Context Summary 생성에 사용한다.
* RAG는 MVP에서 제외하고 File Store 이후 다시 검토한다.
* Rolling Context Summary는 LLM Provider API의 Compression Request로 생성한다.
* chat 응답 이후 이전 Summary와 현재 user/assistant 대화를 압축해 Summary를 갱신한다.
* 다음 chat은 갱신된 Rolling Context Summary를 포함한다.
* Compression 실패 시 기존 Summary를 유지한다.
* AI의 장기 기억에 의존하지 않는다.
* IDE가 프로젝트 상태, 기준, 정책, 맥락, 산출물을 저장한다.
* LLM IDE는 에이전트 기능을 수용할 수 있는 구조로 설계한다.
* MVP에서는 자율 실행 에이전트를 구현하지 않는다.
* AI는 응답 생성, 요약, 상태 변경 후보 제안, 산출물 초안 생성, 파일/맥락 기반 분석을 수행할 수 있다.
* AI는 사용자 승인 없이 Project State, Criteria, Artifact, 파일, 프로젝트를 변경할 수 없다.
* AI는 사용자 승인 없이 외부 명령 실행이나 다음 작업 자동 실행을 할 수 없다.
* MVP는 Project → Provider → Conversation → Criteria → State → Rolling Context Compression → Context Builder → Artifact 순서로 구현한다.
* GUI보다 Core와 CLI 기능 완성을 우선한다.
* TCP/UDP 서버는 후속 `LlmIde.Server` 어댑터 계층으로 추가한다.
* Server는 로컬 LLM 웹서비스 또는 에이전트 도구 런타임 형태를 모두 고려한다.
* Server, CLI, WPF는 동일한 Core 서비스를 사용한다.

---

## 16. 현재 진행 상태

* Phase 1 완료: 프로젝트 초기화 및 프로젝트 목록 관리
* Phase 2 완료: Provider 기반 대화
* Phase 3 완료: 대화 로그 저장
* Phase 4 완료: Criteria 관리
* 다음 작업: Phase 5 Project State 관리
* 이후 작업: Phase 6 Rolling Context Compression

---

## 17. Codex 작업 목록

### Codex Task 005. Project State 관리 구현

* `project-state.json` 저장소 구현
* stage, current task, completed items, next actions, blockers 관리
* CLI state 명령 구현
* chat 요청에 Project State 포함

### Codex Task 006. Rolling Context 저장소 구조 생성

* `conversations/rolling-context/current.md` 생성
* `conversations/rolling-context/history/` 생성
* `conversations/rolling-context/index.jsonl` 생성
* SQLite `rolling_context_summaries` 테이블 추가

### Codex Task 007. Compression Rule 및 context-policy 확장

* `policies/compression-rule.md` 생성
* `context-policy.json`에 Summary + Window 정책 추가
* compression failure strategy 적용

### Codex Task 008. Compression Request 생성기 구현

* 이전 Rolling Context Summary 로드
* 현재 user/assistant 메시지 수집
* Compression Rule 기반 내부 요청 생성
* Provider API로 Summary 생성

### Codex Task 009. chat 후 Rolling Context Summary 갱신 구현

* chat 응답 저장 후 Compression Request 실행
* 새 Summary를 history에 저장
* `current.md` 갱신
* 실패 시 기존 Summary 유지

### Codex Task 010. Context Builder v1에서 Rolling Summary + Recent Window 주입

* Rolling Context Summary 포함
* Recent Window 포함
* 전체 과거 원문 제외
* Context Package에 사용된 맥락 기록

### Codex Task 011. Request Log에 used_rolling_context_id 및 compression_status 기록

* chat request와 compression request 구분
* `used_rolling_context_id` 기록
* `compression_status` 기록
* `compression_error` 기록

---

## 18. v0.5 요약

v0.5에서는 LLM IDE의 장기 대화 관리 전략을 `Summary + Window`로 확정한다.
원본 대화는 모두 저장하고, 과거 맥락은 압축하며, 최근 대화는 제한된 원문 창으로 유지한다.
압축은 LLM Provider API를 통해 수행하고, RAG는 MVP에서 제외한다.
