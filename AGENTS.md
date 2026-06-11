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
빌드
dotnet build LlmIde.slnx -c Debug

테스트
dotnet run --project tests/LlmIde.Tests/LlmIde.Tests.csproj -c Debug

커밋
git add -A
git status --short
git commit -q -F - <<'EOF'
chore(diag): 디버그 로깅 컨벤션 확정 + 연속 이동 이벤트 로그 제외

- Code_Convention.md: 새 메서드/생성자 첫 줄 Log.Ins.Debug("시작") 고정 규칙
  추가. 예외 = 테스트 메서드, 연속 마우스 이동 이벤트 핸들러.
- Writer: 이동/드래그오버 핸들러 5개(OutlineTree_MouseMove, OutlineTree_DragOver,
  ArtifactArea_DragOver, ArtifactCard_MouseMove, BodyEditor_PreviewDragOver)의
  시작 로그 주석 처리(이동마다 과도 축적 방지). [사용자 수정]
- tests: DefaultProject 초기화의 project.json 존재 단언 임시 주석. [사용자 수정]

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
git log --oneline -1

푸시
git push origin main 2>&1 | tail -5

```

5. 빌드오류 및 테스트 오류가 발생하면 2단계부터 재실행한다.
6. 2회 이상 재반복이 실시되면 빌드를 멈추고 보고한다.

로드맵 단계별 진행시 빌드 및 테스트 수행한다.
빌드와 테스트가 모두 통과해야 다음 로드맵 단계로 진행한다.

개발 진행중에 정보가 부족하면 사용자에게 반드시 문의한다.
