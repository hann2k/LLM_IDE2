# LLM IDE2

LLM IDE2는 API 기반 LLM 개발 보조 IDE를 목표로 하는 .NET 프로젝트다.
현재 단계에서는 GUI보다 CLI 기반 핵심 기능을 먼저 구현한다.

이 프로젝트는 DeepSeek API 사용을 기준으로 만들어졌다.
DeepSeek API Key는 저장소에 포함되지 않으며, 사용자가 별도로 발급받아 로컬 설정 파일에 입력해야 한다.

## 프로젝트 개요

현재 구현 범위:

* 프로젝트 이름 기반 초기화 및 목록 관리
* 프로젝트별 `.llmide` 메타데이터 폴더 생성
* DeepSeek Provider 기반 CLI 대화
* 프로젝트 단위 SQLite 대화 저장소
* JSONL/JSON 보조 대화 로그
* 최근 대화 맥락 자동 주입
* CLI chat debug 출력

프로젝트는 이름을 키로 관리한다.
프로젝트 이름 저장소는 실행 프로그램 하부의 고정 `project/projects.json`에 저장되고, 실제 프로젝트 데이터는 각 프로젝트 폴더의 `.llmide` 아래에 저장된다.

대화 기록은 프로젝트별로 다음 위치에 저장된다.

```text
<ProjectRoot>/
 └─ .llmide/
     ├─ conversations/
     │   ├─ conversation.db
     │   ├─ messages.jsonl
     │   ├─ requests.jsonl
     │   └─ context-packages/
     └─ settings/
         └─ providers.json
```

## 개발 환경

필요 환경:

* .NET 10 SDK
* C#
* Microsoft.Data.Sqlite 10.0.8

## 프로젝트 빌드 방법

루트 폴더에서 실행한다.

```bash
dotnet build LlmIde.slnx -c Debug -v:minimal
```

빌드 결과물은 다음 위치에 생성된다.

```text
bin/Debug/net10.0/
```

실행 파일은 빌드 출력 폴더의 `llmide`다.

```bash
cd bin/Debug/net10.0
./llmide
```

테스트 실행 명령:

```bash
dotnet run --project tests/LlmIde.Tests/LlmIde.Tests.csproj -c Debug
```

## 프로젝트 사용법

### 도움말 출력

옵션 없이 실행하면 사용법을 출력한다.

```bash
./llmide
```

### 프로젝트 생성

이름 없이 생성하면 `DefaultProject`가 사용된다.

```bash
./llmide init
```

이름을 지정해서 생성한다.

```bash
./llmide init --name MyProject
```

경로를 지정해서 생성한다.

```bash
./llmide init --name MyProject --path /Users/me/Projects/MyProject
```

### 프로젝트 목록과 관리

```bash
./llmide projects list
./llmide projects open MyProject
./llmide projects rename MyProject NewProjectName
./llmide projects move MyProject /Users/me/Projects/NewPath
```

`remove`는 프로젝트 목록에서만 제거하고 실제 프로젝트 폴더는 삭제하지 않는다.

```bash
./llmide projects remove MyProject
```

`delete`는 실제 프로젝트 폴더까지 삭제한다.
삭제는 위험하므로 프로젝트 이름 재확인이 필요하다.

```bash
./llmide projects delete MyProject --confirm MyProject
```

프로젝트에 API Key가 있으면 추가 확인 옵션이 필요하다.

```bash
./llmide projects delete MyProject --confirm MyProject --confirm-api-key-delete
```

### DeepSeek API Key 설정

프로젝트 생성 후 다음 파일이 생성된다.

```text
<ProjectRoot>/.llmide/settings/providers.json
```

처음 생성되는 API Key 값은 빈 문자열이다.

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

DeepSeek API Key를 별도로 발급받은 뒤 `api_key`에 입력해야 대화 기능을 사용할 수 있다.
API Key가 들어간 파일은 개인 로컬 데이터이므로 커밋하지 않는다.

### 대화 실행

```bash
./llmide chat MyProject "안녕"
```

같은 프로젝트에서 대화를 이어가면 최근 대화 맥락이 다음 요청에 자동으로 포함된다.

```bash
./llmide chat MyProject "세계에서 가장 높은 산은?"
./llmide chat MyProject "두 번째로 높은 산은?"
./llmide chat MyProject "그 산의 높이는?"
```

### Debug 출력

실제로 Provider API에 전달되는 데이터를 확인하려면 `--debug` 옵션을 사용한다.

```bash
./llmide chat MyProject "그 산의 높이는?" --debug
```

debug 출력에는 `request_id`, provider, model, messages가 표시된다.
현재 JSON 출력에서 한국어는 유니코드 이스케이프 형태로 보일 수 있다.

## 주의사항

* 이 프로젝트는 현재 DeepSeek API 기준으로 작성되어 있다.
* DeepSeek API Key는 사용자가 별도로 발급받아야 한다.
* API Key와 빌드 결과물은 저장소에 커밋하지 않는다.
* `bin/`, `obj/`, `backup/` 폴더는 커밋 대상에서 제외된다.
