# LLM IDE Project Folder Structure v0.4

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

또한 MVP 단계에서는 GUI보다 핵심 기능 구현을 우선한다.

```text
MVP = CLI 기반 구현
차기 단계 = WPF GUI 적용
```

모든 핵심 기능은 CLI 환경에서 먼저 구현하며, 이후 WPF GUI는 동일한 Core 라이브러리를 사용하는 방식으로 추가한다.

---

## 2. 수정된 프로젝트 구성

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
 │   │   ├─ context-policy.json
 │   │   └─ provider-policy.json
 │   ├─ context/
 │   │   └─ context-notes.json
 │   ├─ conversations/
 │   │   ├─ messages.jsonl
 │   │   ├─ requests.jsonl
 │   │   └─ context-packages/
 │   │       └─ <request_id>.json
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

## 3. 애플리케이션 구조

초기 구현은 CLI 기반으로 진행한다.

```text
Solution
 ├─ LlmIde.Core
 ├─ LlmIde.Infrastructure
 ├─ LlmIde.Cli
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

### LlmIde.Wpf

후속 GUI 계층

원칙:

```text
WPF는 Core를 호출하는 UI 계층이다.
비즈니스 로직은 Core에만 존재한다.
```

---

## 4. 제거된 구성

아래 항목은 프로젝트 구조에서 제거한다.

```text
.llmide/handoff/
.llmide/handoff/handoff.index.json
.llmide/handoff/<handoff_id>.md
```

또한 MVP 개발 범위에서도 Handoff Generator를 제외한다.

---

## 5. 수정된 핵심 모델

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

## 6. 요청 실행 구조

API 호출의 실행 단위는 Request다.

```text
User Input
 → Context Builder
 → Context Package
 → Provider API Request
 → Assistant Response
 → Message Log 저장
 → Request Log 저장
```

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

## 7. Context Package의 역할

Context Package는 특정 API 요청에 실제로 들어간 맥락 스냅샷이다.

```json
{
  "request_id": "req_001",
  "system_rule": "",
  "active_criteria": [],
  "project_state": {},
  "context_notes": [],
  "attached_messages": [],
  "attached_artifacts": [],
  "attached_files": [],
  "recent_turns": [],
  "user_request": ""
}
```

이 파일은 API 기반 구조에서 요청 재현성과 추적성을 보장하는 핵심 데이터다.

---

## 8. MVP 최소 파일

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
     │   ├─ context-policy.json
     │   └─ provider-policy.json
     ├─ conversations/
     │   ├─ messages.jsonl
     │   ├─ requests.jsonl
     │   └─ context-packages/
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

## 9. MVP 개발 절차

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
* 요청/응답 처리
* CLI 채팅 명령 구현

예시:

```bash
llmide chat MyProject "안녕"
```

완료 기준:

* 프로젝트 생성 시 `.llmide/settings/providers.json`에 DeepSeek 설정이 생성된다.
* DeepSeek API Key 값은 빈 문자열로 생성된다.
* 사용자 메시지를 전송할 수 있다.
* AI 응답을 받을 수 있다.

---

### Phase 3. 대화 로그 저장

목표:

* 모든 대화를 프로젝트에 저장한다.

구현 항목:

* messages.jsonl 저장
* requests.jsonl 저장
* 요청별 Context Package 저장

완료 기준:

* 사용자 메시지와 AI 응답이 저장된다.
* 어떤 맥락으로 응답이 생성되었는지 추적 가능하다.

---

### Phase 4. Criteria 관리

목표:

* 프로젝트 기준을 저장하고 요청에 주입한다.

구현 항목:

* Criteria 생성
* Criteria 수정
* Criteria 삭제
* Active Criteria 주입
* CLI 관리 명령 구현

예시:

```bash
llmide criteria add
llmide criteria list
```

완료 기준:

* 활성 기준이 API 요청에 포함된다.
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

### Phase 6. Context Builder

목표:

* 요청에 필요한 맥락을 조립한다.

구현 항목:

* System Rule 주입
* Active Criteria 주입
* Project State 주입
* 최근 대화 포함
* Context Package 생성

완료 기준:

* 요청마다 Context Package가 생성된다.
* 동일한 요청을 재현할 수 있다.

---

### Phase 7. Artifact 저장

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

### Phase 8. Context Notes

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

### Phase 9. File Store

목표:

* 프로젝트 파일을 AI 맥락으로 활용한다.

구현 항목:

* 파일 등록
* 텍스트 추출
* 파일 요약
* 요청 첨부

완료 기준:

* 파일 내용을 요청에 포함할 수 있다.
* 포함 이력을 추적할 수 있다.

---

### Phase 10. State Update Candidate

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

### Phase 11. WPF GUI 적용

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

## 10. 현재 확정 결정

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
* AI의 장기 기억에 의존하지 않는다.
* IDE가 프로젝트 상태, 기준, 정책, 맥락, 산출물을 저장한다.
* MVP는 Project → Provider → Conversation → Criteria → State → Context → Artifact 순서로 구현한다.
* GUI보다 Core와 CLI 기능 완성을 우선한다.
