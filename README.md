# LLM IDE2

> **A stateful project runtime for stateless LLM providers.**  
> Stateless LLM 위에 Stateful Project Runtime을 얹어, 모델이 바뀌어도 프로젝트의 의도와 맥락이 끊기지 않게 만드는 로컬 LLM 작업 환경.

LLM IDE2는 또 하나의 코딩 에이전트나 코드 편집기가 아니다.

이 프로젝트의 목적은 DeepSeek, Gemini, GPT, Claude 같은 서로 다른 LLM을 언제든 교체하더라도, 프로젝트의 기준, 결정, 상태, 산출물, 대화 맥락이 IDE 안에 남아 계속 이어지도록 하는 것이다.

모델은 바뀔 수 있다.  
Provider는 교체될 수 있다.  
하지만 프로젝트의 기억은 IDE가 보존한다.

---

## Why LLM IDE2 Exists

LLM은 강력하지만 기본적으로 요청 단위로 동작한다.

한 세션에서는 잘 이해하던 모델도 다음 세션에서는 이전 결정을 잊고, 다른 Provider로 바꾸면 말투, 추론 방식, 코드 스타일, 작업 판단이 달라진다. 긴 대화를 계속 붙여넣는 방식은 토큰을 낭비하고, 중요한 결정이 맥락 중간에서 사라지기 쉽다.

현재 많은 도구는 LLM에게 코드를 더 잘 쓰게 만드는 데 집중한다.  
LLM IDE2는 다른 문제를 본다.

**코드를 누가 쓰느냐보다, 프로젝트의 의도와 결정이 어떻게 유지되는가가 더 중요하다.**

LLM IDE2는 다음 질문에서 출발한다.

- LLM이 stateless라면, 프로젝트의 기억은 어디에 있어야 하는가?
- 여러 모델을 바꿔 쓰더라도 같은 프로젝트 맥락을 유지할 수 있는가?
- 대화에서 나온 결정, 기준, 산출물, 다음 작업을 어떻게 잃어버리지 않을 수 있는가?
- 코딩 에이전트에게 작업을 넘기기 전에, 무엇을 해야 하는지 어떻게 정리할 수 있는가?
- 사용자가 최종 결정을 유지하면서도 AI의 도움을 받을 수 있는 구조는 무엇인가?

LLM IDE2의 답은 단순하다.

> **LLM은 계산 엔진이고, 프로젝트의 기억은 IDE가 가진다.**

---

## Core Idea

LLM IDE2는 LLM Provider를 프로젝트 기억의 주체로 보지 않는다.

Provider는 IDE가 조립한 맥락 패키지를 받아 응답을 생성하는 실행 엔진이다. 실제 프로젝트의 기억은 IDE 내부의 `.llmide` 저장소와 `conversation.db`, Criteria, Project State, Artifacts, Rolling Context에 보존된다.

```text
Project Memory
  ├─ Criteria
  ├─ Project State
  ├─ Conversation History
  ├─ Rolling Context
  ├─ Artifacts
  ├─ Request Logs
  └─ Tool Logs
        ↓
Context Builder
        ↓
Provider Request Package
        ↓
DeepSeek / Gemini / GPT / Claude / Other LLM
```

이 구조에서 중요한 것은 특정 모델의 능력이 아니다.

상대 LLM이 누구든, IDE가 같은 프로젝트 기억을 읽고, 현재 요청에 필요한 맥락을 재조립해 전달한다. 따라서 모델을 교체해도 작업 연속성은 LLM의 기억이 아니라 IDE의 상태 보존 능력에 의해 유지된다.

---

## What LLM IDE2 Is

LLM IDE2는 다음에 가깝다.

- LLM 작업을 위한 상태 보존형 프로젝트 런타임
- 프로젝트 의도와 결정의 로컬 메모리
- 여러 LLM Provider 위에서 동작하는 맥락 빌더
- AI와 함께 프로젝트를 굴리기 위한 작업실
- 코딩 에이전트에게 넘길 작업 맥락을 준비하는 상위 레이어

LLM IDE2는 다음을 지향한다.

```text
아이디어
 → 기준 정리
 → 프로젝트 상태 기록
 → 대화
 → 결정 보존
 → 산출물 추출
 → 맥락 압축
 → 다음 작업 정의
 → 외부 LLM 또는 코딩 에이전트에게 전달
 → 결과 검토
 → 다시 프로젝트 기억에 반영
```

---

## What LLM IDE2 Is Not

LLM IDE2는 다음을 목표로 하지 않는다.

- Cursor, Claude Code, Codex, Devin 같은 코딩 에이전트의 대체재
- 코드 자동완성 IDE
- 대형 조직용 Jira/Linear/Confluence 클론
- 모든 파일과 코드를 통째로 읽어주는 repo 분석기
- LLM 응답을 자동으로 신뢰하고 적용하는 자동 개발 시스템

LLM IDE2는 코딩 경쟁을 하지 않는다.

코드는 다른 도구와 에이전트가 더 잘할 수 있다.  
LLM IDE2는 그 위에서 **프로젝트의 기억, 기준, 결정, 맥락, 작업 전달**을 관리한다.

---

## Target User

LLM IDE2의 첫 번째 사용자는 대형 프로젝트 방법론에 익숙한 PM이나 아키텍트가 아니다.

오히려 다음과 같은 사람을 상정한다.

- 코드는 어느 정도 작성할 수 있지만, 무엇을 먼저 해야 할지 자주 흔들리는 1인 개발자
- 하청 개발 경험은 있지만, 자기 제품의 방향과 우선순위를 잡는 데 익숙하지 않은 개발자
- ChatGPT, Claude, Codex, Cursor 등을 오가며 작업하다가 맥락이 흩어지는 사람
- 대화에서 나온 결정을 다시 찾느라 시간을 쓰는 사람
- LLM에게 일을 시키기 전, 작업을 어떻게 정의해야 할지 막히는 사람
- 프로젝트의 기준과 변경 이력을 로컬에 남기고 싶은 사람

LLM IDE2는 경험 많은 PM처럼 정답을 대신 내려주는 도구가 아니다.  
대신 사용자가 프로젝트를 잃어버리지 않도록, 현재 상태와 다음 작업을 계속 구조화하는 도구다.

---

## Design Principles

### 1. IDE Owns the Memory

프로젝트의 장기 기억은 LLM Provider가 아니라 IDE가 가진다.

Criteria, Project State, 대화 로그, 요청 로그, 산출물, 맥락 요약은 프로젝트별 `.llmide` 저장소에 남는다. LLM은 매 요청마다 IDE가 조립한 맥락을 받아 응답한다.

### 2. Provider Is Replaceable

Provider는 교체 가능한 계산 엔진이다.

현재 구현의 기본 Provider는 DeepSeek이지만, Core는 특정 Provider에 종속되지 않도록 설계한다. Gemini, GPT, Claude 등 다른 Provider를 연결하더라도 동일한 프로젝트 기억과 맥락 빌더를 사용할 수 있어야 한다.

### 3. Context Is Built, Not Remembered

LLM이 기억한다고 가정하지 않는다.

매 요청마다 Context Builder가 프로젝트 상태, 활성 Criteria, 최근 대화, Rolling Context, 관련 산출물, 사용자 요청을 조합해 Request Context Package를 만든다.

### 4. Preserve Semantic Commitments

긴 대화에서 중요한 것은 모든 문장을 보존하는 것이 아니다.

중요한 것은 프로젝트가 지키기로 한 의미론적 약속이다.

- 무엇을 만들기로 했는가
- 무엇을 하지 않기로 했는가
- 어떤 기준을 따르기로 했는가
- 어떤 결정이 이미 내려졌는가
- 어떤 제약을 계속 유지해야 하는가
- 다음 작업은 무엇인가

Rolling Context Compression은 대화가 길어져도 이러한 핵심 약속이 사라지지 않도록 보조한다.

### 5. Human in the Loop

AI는 제안하고, IDE는 기록하며, 최종 판단은 사용자가 한다.

LLM IDE2는 자동 변경보다 검토 가능한 기록을 우선한다. 중요한 상태 변경, 기준 변경, 산출물 반영은 사용자가 확인할 수 있어야 한다.

### 6. Tool Isolation

Agent 도구 호출은 Core와 분리된다.

도구는 읽기 전용에서 시작하고, 호출 이력은 대화 메시지와 별도로 기록한다. 도구 실행은 제한 가능한 allowlist와 감사 가능한 로그를 전제로 한다.

### 7. Core Sharing

CLI와 WPF는 같은 Core와 같은 `.llmide` 데이터를 사용한다.

UI는 어댑터일 뿐이고, 프로젝트 기억과 핵심 흐름은 Core에 있다.

---

## Project Memory Model

LLM IDE2의 중심 객체는 코드 파일이 아니라 프로젝트 기억이다.

### Criteria

프로젝트가 계속 지켜야 할 기준이다.

예시:

- 항상 한국어로 답한다.
- 코딩 기능은 직접 경쟁하지 않는다.
- Core 비즈니스 로직은 UI 계층에 두지 않는다.
- API Key와 개인 프로젝트 데이터는 저장소에 커밋하지 않는다.
- 상태 변경은 사용자 승인 없이 자동 적용하지 않는다.

### Project State

프로젝트의 현재 상태다.

예시:

- 현재 단계
- 완료된 작업
- 진행 중인 작업
- 다음 액션
- blocker
- 최근 결정

### Conversation History

LLM과의 대화 기록이다.

단순 채팅 로그가 아니라, 프로젝트 판단의 원천 데이터다.

### Rolling Context

긴 대화에서 핵심 맥락을 압축한 상태다.

이전 대화 전체를 매번 주입하지 않고, 프로젝트에 필요한 요약된 맥락을 유지한다.

### Artifacts

대화에서 추출된 산출물이다.

예시:

- README 초안
- 설계 문서
- 결정 로그
- 작업 지시서
- 코드 블록
- 분석 결과

### Request Logs

LLM에게 어떤 맥락이 전달되었는지 재현하기 위한 기록이다.

### Tool Logs

Agent 도구가 무엇을 호출했고 어떤 결과를 반환했는지 남기는 감사 기록이다.

---

## Working Loop

LLM IDE2의 기본 작업 루프는 기능 목록이 아니라 다음 흐름이다.

```text
1. 현재 생각이나 문제를 IDE에 입력한다.
2. IDE가 프로젝트 기억과 Criteria를 함께 LLM에게 전달한다.
3. LLM은 현재 맥락에 맞춰 응답한다.
4. 사용자는 응답에서 결정, 기준, 산출물, 다음 작업을 추출한다.
5. IDE는 이를 프로젝트 기억에 저장한다.
6. 다음 요청에서는 저장된 기억이 다시 Context Builder에 들어간다.
```

이 루프의 목적은 대화를 많이 하는 것이 아니다.

목적은 대화에서 생긴 의미 있는 판단을 프로젝트 기억으로 승격시키고, 다음 작업으로 이어지게 만드는 것이다.

---

## Example Use Cases

### 1. 프로젝트 방향이 흔들릴 때

사용자는 현재 고민을 입력한다.

```text
코딩 기능을 직접 만들지 말고, LLM 작업 지휘 도구로 가는 게 맞을까?
```

IDE는 기존 Criteria, Project State, Decision Log, 이전 대화 요약을 함께 주입한다.  
LLM은 이전 결정과 충돌하는지, 지금 어떤 결정을 내려야 하는지 제안한다.

### 2. 다른 LLM으로 교체할 때

DeepSeek으로 작업하다가 GPT나 Claude로 바꿔도, 프로젝트 기억은 Provider 내부가 아니라 `.llmide`에 남아 있다.

새 Provider는 IDE가 조립한 Context Package를 받아 현재 프로젝트의 기준과 상태를 이어받는다.

### 3. 외부 코딩 에이전트에게 작업을 넘길 때

LLM IDE2는 코드를 직접 작성하기보다, Codex, Claude Code, Cursor 같은 외부 코딩 에이전트에게 전달할 작업 맥락을 정리하는 상위 레이어가 될 수 있다.

예시 Handoff:

```text
배경:
현재 프로젝트는 LLM Provider 독립적인 Stateful Runtime을 목표로 한다.

목표:
Provider 설정 화면을 WPF에 추가한다.

제약:
Core 비즈니스 로직은 WPF에 두지 않는다.
기존 .llmide/settings/providers.json 구조를 유지한다.
API Key는 로그에 남기지 않는다.

완료 기준:
빌드와 테스트가 통과해야 한다.
변경된 구조는 Docs에 반영해야 한다.
```

### 4. 결과를 다시 프로젝트 기억에 반영할 때

외부 에이전트나 사용자가 작업을 마친 뒤, 결과를 붙여넣으면 IDE는 다음을 정리할 수 있다.

- 실제로 완료된 것
- 실패한 것
- 새로 생긴 결정
- 업데이트해야 할 Criteria
- 다음 작업
- 산출물로 저장할 내용

---

## Architecture

LLM IDE2는 UI 중심 앱이 아니라 Core 중심 런타임이다.

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

### LlmIde.Core

프로젝트 기억과 핵심 흐름을 담당한다.

- Project
- Criteria
- Project State
- Context Builder
- Conversation Flow
- Rolling Context
- Artifacts
- Agent Loop 모델과 인터페이스

### LlmIde.Infrastructure

외부 시스템과 저장소를 담당한다.

- SQLite 저장소
- JSON/JSONL 저장소
- 파일 시스템 저장소
- Provider 구현
- Agent 도구 구현

### LlmIde.Cli

Core 검증과 자동화에 적합한 CLI 어댑터다.

### LlmIde.Wpf

사용자가 직접 프로젝트 기억을 보고 다루기 위한 GUI 어댑터다.

### LlmIde.Tests

Core와 Infrastructure 중심의 회귀 테스트를 담당한다.

---

## Local Storage

프로젝트 등록소는 실행 프로그램 루트 아래에 저장된다.

```text
<IdeProgramRoot>/project/projects.json
```

실제 프로젝트 데이터는 각 프로젝트 루트의 `.llmide` 아래에 저장된다.

```text
<ProjectRoot>/
└─ .llmide/
   ├─ project.json
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

---

## Provider Strategy

현재 기준 Provider는 DeepSeek이다.

하지만 LLM IDE2의 핵심은 특정 Provider가 아니다. Core는 Provider를 교체 가능한 인터페이스로 다루며, Provider는 IDE가 만든 요청 패키지를 받아 응답을 반환하는 역할만 한다.

```text
LlmIde.Core
  → IChatProvider
      → DeepSeek
      → Gemini
      → GPT
      → Claude
      → Other
```

Provider별 성능과 성향은 다를 수 있다.  
LLM IDE2가 보장하려는 것은 모든 모델의 동일한 답변이 아니라, **모델 교체로 인한 프로젝트 맥락 손실을 줄이는 것**이다.

---

## Development Status

LLM IDE2는 현재 Core/CLI 기반 MVP 기능을 바탕으로 WPF GUI 통합을 진행 중이다.

이미 구현된 핵심 축은 다음과 같다.

- 프로젝트 등록소
- Provider 기반 대화
- SQLite + JSONL/JSON 저장
- Criteria 관리
- Project State 관리
- Rolling Context Compression
- Context Builder v1
- Artifacts 저장과 추출
- 읽기 전용 Agent Loop
- CLI와 WPF의 Core/데이터 공유

진행 상태는 기능 목록 자체보다, 다음 방향을 기준으로 관리한다.

> Core는 프로젝트 기억을 안정적으로 유지하고,  
> UI는 그 기억을 사용자가 보고 수정할 수 있게 하며,  
> Provider는 언제든 교체 가능한 실행 엔진으로 남긴다.

---

## Development Rules

LLM IDE2 개발은 다음 규칙을 따른다.

- API Key와 개인 프로젝트 데이터는 커밋하지 않는다.
- `.llmide`, `providers.json`, `bin`, `obj`, `backup`은 저장소에 포함하지 않는다.
- 구조나 로드맵이 바뀌면 Docs를 먼저 갱신한다.
- Core 비즈니스 로직을 UI 계층에 넣지 않는다.
- CLI와 WPF는 같은 Core와 같은 데이터 구조를 사용해야 한다.
- 새로운 기능은 빌드와 테스트를 통과해야 한다.
- LLM 결과는 자동 적용보다 검토 가능한 기록을 우선한다.

주요 문서:

```text
Docs/1.Product_Principles.md
Docs/2.Architecture_Baseline.md
Docs/3.Development_Roadmap.md
Docs/4.Change_Decision_Log.md
Docs/Code_Convention.md
```

---

## Build

필요 환경:

- Windows
- .NET 10 SDK
- C#
- WPF

빌드:

```bash
dotnet build LlmIde.slnx -c Debug -v:minimal
```

테스트:

```bash
dotnet run --project tests/LlmIde.Tests/LlmIde.Tests.csproj -c Debug
```

WPF 실행:

```bash
dotnet run --project src/LlmIde.Wpf/LlmIde.Wpf.csproj -c Debug
```

CLI 실행:

```bash
dotnet run --project src/LlmIde.Cli/LlmIde.Cli.csproj -- <command>
```

---

## License / Usage Terms

This project is **source-available for personal and non-commercial use only**.

이 프로젝트는 **개인 및 비상업적 용도에 한해 소스 열람과 사용을 허용**합니다.

허용되는 사용:

- 소스 코드 열람, 복제, 학습
- 개인, 교육, 연구, 비상업적 목적의 사용
- 비상업적 목적의 개인 수정

사전 서면 승인 없이 금지되는 사용:

- 상업적 목적의 사용
- 판매, 재라이선스, 임대, 유료 호스팅, 유료 서비스 제공
- 상업 제품, SaaS, 사내 업무 도구, 유료 컨설팅 산출물에 포함
- 저작권, 라이선스, 출처 표시 제거

상업적 사용은 저작자의 사전 승인이 필요합니다.

Copyright © 2026 hann2k. All rights reserved except as expressly permitted above.

상업적 라이선스 문의는 저장소 소유자에게 연락하십시오.
