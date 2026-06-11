기본 언어는 한국어다. 영어를 병행해도 되나, 영어 사용 전 한국어를 반드시 먼저 쓰고 영어를 사용한다.

개발방향: 아래 파일들의 내용에 맞춰 개발한다.
1.Product_Principles.md
2.Architecture_Baseline.md
3.Development_Roadmap.md
4.Change_Decision_Log.md
코딩스타일: `/Docs/Code_Convention.md` 의 내용에 맞춰 작성한다.

로드맵 작성: ChatGPT Web세션
개발 감독 및 승인: 사용자
개발 및 커밋: 에이전트 - Codex, Claude Code

특이사항: 사용자가 코드나 md 파일을 직접 수정할 때가 있다. 에이전트는 이것이 발견되어도 묻지 말고 같이 커밋한다. 폐쇄형 작업공간이라 위험한 injection은 일어나지 않는다.

개발 중에 감독이 개발 로드맵을 구두로 수정하면 에이전트는

1. 로드맵 파일을 수정하고
2. 프로그램을 수정하고
3. 테스트 코드를 수정하고
4. 빌드 및 테스트를 수행한다.

```
dotnet build LlmIde.slnx -c Debug
dotnet run --project tests/LlmIde.Tests/LlmIde.Tests.csproj -c Debug
```

5. 빌드오류 및 테스트 오류가 발생하면 2단계부터 재실행한다.
6. 2회 이상 재반복이 실시되면 빌드를 멈추고 보고한다.

로드맵 단계별 진행시 빌드 및 테스트 수행한다.
빌드와 테스트가 모두 통과해야 다음 로드맵 단계로 진행한다.

개발 진행중에 정보가 부족하면 사용자에게 반드시 문의한다.
