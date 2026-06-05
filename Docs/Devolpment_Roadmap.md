# Natural-Language IDE MVP Development Roadmap

## 0. 방향 재정의

이 프로젝트의 방향은 WebView2 중심 IDE에서 API 기반 프로젝트 상태 관리 IDE로 전환한다.

핵심 원칙은 다음과 같다.

```text
프로젝트 상태는 IDE가 기억한다.
AI는 매 요청마다 필요한 맥락을 주입받아 실행된다.

즉, 장기세션을 AI 내부 기억에 의존하지 않는다.
장기성은 프로젝트 저장소, 기준 구조, 현재 상태, 산출물, 대화 로그가 만든다.
```

1. 현재 구현 상태

이미 구현된 기능은 다음과 같다.

- WebView2 기반 대화 화면
- JS injection
- DOM 추출
- 대화 로그 저장
- 세션 목록 관리
- 프로젝트 설정 탭
- 초기 기준 저장
- 첫 대화 시작 시 기준 주입

현재 문제는 다음과 같다.

- WebView2는 웹 UI 변경에 취약하다.
- DOM 추출 안정성이 낮다.
- 웹세션 맥락에 의존하면 IDE 구조가 흔들린다.
- API 기반 구조로 전환할 필요가 있다.

WebView2는 폐기하지 않고 Legacy Web Session 모드로 낮춘다.
주 실행 경로는 API 모드로 전환한다.

2. 목표 아키텍처

최상위 단위는 Project다.

Project
 ├─ Criteria
 ├─ Project State
 ├─ Context Notes
 ├─ Conversation Logs
 ├─ Artifacts
 ├─ Files
 ├─ Handoff
 └─ Sessions

AI Provider는 교체 가능해야 한다.

LLM Provider
 ├─ DeepSeek API Provider
 ├─ OpenAI API Provider
 ├─ Gemini API Provider
 └─ Legacy WebView2 Provider

중요한 원칙은 다음과 같다.

IDE Core는 특정 모델이나 특정 공급자에 종속되면 안 된다.
DeepSeek은 Provider 구현체 하나일 뿐이다.
3. 핵심 데이터 구조
3.1 Project

Project는 장기 작업 단위다.

{
  "project_id": "",
  "title": "",
  "description": "",
  "created_at": "",
  "updated_at": "",
  "default_provider": "",
  "default_model": ""
}
3.2 Session

Session은 Project 안의 대화 묶음이다.
API 구조에서 Session은 장기 기억 단위가 아니다.

{
  "session_id": "",
  "project_id": "",
  "provider": "",
  "model": "",
  "title": "",
  "created_at": "",
  "updated_at": ""
}
3.3 Message

Message는 사용자 또는 AI의 단일 발화 기록이다.

{
  "message_id": "",
  "session_id": "",
  "role": "user",
  "content": "",
  "created_at": "",
  "provider": "",
  "model": "",
  "attached_context_ids": [],
  "attached_artifact_ids": [],
  "attached_file_ids": []
}
3.4 Criteria

Criteria는 프로젝트에 저장되는 지속 규칙이다.

{
  "criterion_id": "",
  "project_id": "",
  "title": "",
  "description": "",
  "status": "active",
  "priority": "normal"
}

Status 값은 다음과 같다.

active
inactive
draft
deprecated
3.5 Project State

Project State는 현재 작업 상태다.

{
  "stage": "",
  "current_task": "",
  "completed_items": [],
  "in_progress_items": [],
  "next_actions": [],
  "blockers": [],
  "last_decision": "",
  "updated_at": ""
}
3.6 Context Notes

Context Notes는 장기 맥락이다.
대화 전체를 매번 넣는 대신, 필요한 맥락을 구조화하여 저장한다.

{
  "context_id": "",
  "project_id": "",
  "title": "",
  "content": "",
  "scope": "on_demand",
  "priority": "normal"
}

Scope 값은 다음과 같다.

always
current_task
on_demand
archived
3.7 Artifacts

Artifacts는 AI가 만든 결과물 또는 사용자가 저장한 작업 산출물이다.

{
  "artifact_id": "",
  "project_id": "",
  "title": "",
  "type": "",
  "content": "",
  "status": "",
  "version": "",
  "created_from_message_id": "",
  "updated_at": ""
}

Artifact type 예시는 다음과 같다.

document
code
table
prompt
handoff
note
file_reference
3.8 Files

Files는 외부 파일 자료다.

{
  "file_id": "",
  "project_id": "",
  "file_name": "",
  "file_type": "",
  "local_path": "",
  "extracted_text": "",
  "summary": "",
  "role": "",
  "linked_criteria_ids": [],
  "include_mode": "on_demand"
}

Include mode 값은 다음과 같다.

always
on_demand
summary_only
chunked
archived
4. Context Builder

Context Builder는 이 IDE의 핵심 엔진이다.

역할은 다음과 같다.

프로젝트 기준, 상태, 맥락, 선택된 메시지, 산출물, 파일을 조합하여
API 요청용 입력 패키지를 만든다.
4.1 매 요청 기본 구성

매 API 요청은 다음 요소를 조합해 만든다.

[System Rule]
AI 역할, 말투, 권한 구조

[Active Criteria]
현재 활성 기준

[Project State]
현재 단계, 현재 작업, 완료 항목, 다음 작업

[Context Notes]
현재 요청에 필요한 맥락

[Attached Messages]
사용자가 선택한 이전 대화

[Attached Artifacts]
사용자가 선택한 산출물

[Attached Files]
사용자가 선택한 파일 요약 또는 일부

[Recent Turns]
최근 N개 대화

[User Request]
이번 사용자 입력
4.2 전체 로그를 매번 넣지 않는다

전체 대화 로그는 저장만 한다.
매 요청에 전체 로그를 모두 넣지 않는다.

전체 로그 = 보존 / 검색 / 추적용
요청 맥락 = 현재 요청에 필요한 것만
5. MVP 개발 단계
Phase 1. API Provider 기반 만들기
목표

WebView2 대신 API로 대화가 가능하게 만든다.

작업
- LLM Provider 인터페이스 작성
- DeepSeek API Provider 구현
- API Key 저장
- 요청 생성기 작성
- 응답 표시
- 요청/응답 로그 저장
- API 세션을 기존 세션 목록에 저장
완료 기준
- IDE 안에서 DeepSeek API로 메시지를 보낼 수 있다.
- 응답을 화면에 표시한다.
- user/assistant 메시지가 세션 로그에 저장된다.
- WebView2 모드는 legacy로 유지된다.
Phase 2. Project State 저장 및 편집
목표

대화 로그와 별도로 프로젝트 현재 상태를 저장한다.

작업
- 프로젝트 설정 탭에 Project State 편집 UI 추가
- stage 입력
- current_task 입력
- completed_items 관리
- in_progress_items 관리
- next_actions 관리
- blockers 관리
- last_decision 관리
- 저장/불러오기
- API 요청 시 Project State 주입
완료 기준
- 사용자가 Project State를 직접 편집할 수 있다.
- 앱 재시작 후에도 Project State가 유지된다.
- API 요청에 Project State가 포함된다.
Phase 3. Context Notes 저장
목표

장기 맥락을 따로 저장한다.

작업
- Context Notes 탭 추가
- short context / working context / deep context 저장
- scope 설정: always, current_task, on_demand, archived
- 다음 요청에 포함할 Context 선택 기능
완료 기준
- 사용자가 맥락 메모를 저장할 수 있다.
- always 맥락은 자동 포함할 수 있다.
- on_demand 맥락은 사용자가 선택해서 포함할 수 있다.
Phase 4. 명시적 맥락 첨부
목표

IDE가 사용자 입력을 자동으로 완벽히 해석하려 하지 않는다.
대신 사용자가 이전 메시지나 산출물을 명시적으로 첨부할 수 있게 한다.

작업
- 메시지마다 "다음 요청에 첨부" 버튼 추가
- assistant 응답에 "Artifact로 저장" 버튼 추가
- 첨부된 맥락 미리보기 패널 추가
- 첨부 제거 기능
- 전송 시 첨부된 메시지를 API 요청에 포함
완료 기준
- 사용자가 이전 메시지를 선택해 다음 요청에 붙일 수 있다.
- 전송 전 어떤 맥락이 포함되는지 확인할 수 있다.
- 응답 로그에 어떤 맥락이 첨부되었는지 기록된다.
Phase 5. Artifact Store
목표

중요한 AI 응답을 재사용 가능한 산출물로 저장한다.

작업
- Artifact 목록 UI 추가
- 선택한 assistant 응답을 Artifact로 저장
- title, type, status, version 편집
- Artifact 내용 직접 수정
- Artifact를 다음 요청에 첨부
- 원본 메시지 추적
완료 기준
- AI 응답을 Artifact로 저장할 수 있다.
- Artifact를 수정할 수 있다.
- Artifact를 다음 API 요청에 첨부할 수 있다.
- Artifact가 어떤 메시지에서 왔는지 추적할 수 있다.
Phase 6. File Store와 파일 주입
목표

외부 파일을 프로젝트 자료로 저장하고, 필요 시 API 요청에 주입한다.

작업
- 파일 추가 기능
- txt, md, docx, pdf 텍스트 추출
- 파일 역할 저장
- 파일 요약 저장
- 기준과 파일 연결
- include_mode에 따라 주입 방식 결정
완료 기준
- 파일을 프로젝트에 추가할 수 있다.
- 파일 역할을 지정할 수 있다.
- 파일 전문, 요약, 일부를 API 요청에 포함할 수 있다.
- 요청 로그에 포함된 파일 ID가 기록된다.
Phase 7. Handoff Generator
목표

프로젝트 상태를 새 세션으로 넘길 수 있는 handoff prompt를 생성한다.

작업
- short handoff 생성
- full handoff 생성
- 기준, 상태, 맥락, 산출물, 다음 작업 포함
- 클립보드 복사
- 생성된 handoff를 Artifact로 저장
완료 기준
- 저장된 프로젝트 자료로 handoff를 생성할 수 있다.
- 새 세션 첫 입력으로 사용할 수 있다.
- handoff가 산출물로 저장된다.
Phase 8. State Update Candidate
목표

AI가 상태 변경 후보를 만들고, 사용자가 승인해야 저장되게 한다.

작업
- "현재 대화에서 상태 업데이트 후보 생성" 버튼 추가
- 최근 대화 로그를 API에 보냄
- AI가 아래 후보 추출:
  - 새 결정
  - 새 기준
  - 완료 항목
  - 다음 작업
  - 생성 산출물
  - 막힌 점
- 사용자가 후보를 검토
- 승인한 항목만 Project State / Criteria / Artifacts에 반영
완료 기준
- AI가 상태 업데이트 후보를 생성할 수 있다.
- 상태 업데이트 후보는 자동 저장되지 않는다.
- 사용자가 승인, 수정, 거부할 수 있다.
6. UI 구조
왼쪽 패널
- 프로젝트 목록
- 세션 목록
- 프로젝트 설정 탭
중앙 패널
- 대화 화면
- 사용자 입력창
- 전송 버튼
- Provider 선택
- Model 선택
프로젝트 설정 탭
- Initial Criteria
- Active Criteria
- Project State
- Context Notes
- Artifacts
- Files
- Handoff
- Provider Settings

현재 구조에서는 프로젝트 설정 탭 안에 대부분을 넣는 것이 적절하다.

7. 개발 우선순위

바로 할 작업은 다음과 같다.

1. API Provider 인터페이스
2. DeepSeek API Provider
3. API 요청/응답 로그 저장
4. Project State 편집/저장
5. Criteria + Project State를 API 요청에 주입
6. 메시지 수동 첨부 기능
7. Artifact 저장 기능

나중으로 미룰 작업은 다음과 같다.

- 사용자 입력 자동 분석
- "방금 표" 같은 참조 자동 해석
- 자동 파일 검색
- 벡터 검색
- 멀티에이전트
- 자동 권한 실행
- 외부 시스템 실행
8. 리스크 관리
리스크 1. WebView2 불안정

대응:

WebView2는 legacy mode로 격하한다.
API mode를 주 실행 경로로 사용한다.
리스크 2. 맥락 폭발

대응:

전체 로그는 저장만 한다.
요청에는 선택된 맥락만 넣는다.
Project State와 Context Notes를 사용한다.
리스크 3. AI가 상태를 잘못 요약

대응:

AI는 상태 업데이트 후보만 만든다.
저장은 사용자 승인 후 한다.
리스크 4. DeepSeek 종속

대응:

Provider Adapter 구조로 분리한다.
Provider별 구현은 Core 밖에 둔다.
리스크 5. 모델 말투/태도 문제

대응:

System Rule과 Active Criteria를 매 요청에 주입한다.
사용자가 말투 기준을 편집할 수 있게 한다.
9. MVP 완료 기준

MVP는 아래가 되면 완료로 본다.

1. 프로젝트 생성 가능
2. 기준 저장 가능
3. 프로젝트 상태 저장 가능
4. 대화 로그 저장 가능
5. DeepSeek API로 대화 가능
6. 요청마다 기준, 상태, 선택 맥락, 최근 대화를 조합 가능
7. 이전 메시지를 다음 요청에 수동 첨부 가능
8. 중요한 응답을 Artifact로 저장 가능
9. Artifact를 다음 요청에 첨부 가능
10. 프로젝트 상태로 handoff prompt 생성 가능
10. 최종 원칙

이 IDE는 AI가 모든 것을 기억하게 만드는 도구가 아니다.

IDE가 프로젝트를 기억한다.
AI는 요청마다 필요한 맥락을 주입받는다.
사용자가 기준과 상태 변경의 최종 권한자다.

따라서 자동화보다 먼저 필요한 것은 다음이다.

저장
선택
첨부
주입
승인
추적