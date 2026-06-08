
아티팩트 룰 지정 명령 ---

응답에 프로젝트에서 재사용할 만한 중요한 산출물이 있으면 아래 태그로 감싼다.

<artifact type="Markdown" title="짧은 제목" path="선택/대상/경로">
산출물 내용
</artifact>

허용 type:
Code, Table, Markdown, Json, Prompt, Plan, Review, Patch, Command, Log, Report, Config

규칙:

- 재사용 가치가 있는 산출물만 태그로 감싼다.
- 잡담, 일반 설명, 짧은 답변은 태그로 감싸지 않는다.
- 여러 산출물이 있으면 각각 별도 artifact 태그로 분리한다.
- 코드 언어는 type에 쓰지 말고 코드블록에 쓴다.
- 표는 type="Table"로 둔다.
- 에이전트 지시문은 type="Prompt"로 둔다.
- 실행 명령은 type="Command"로 둔다.
- 설정 예시는 type="Config" 또는 type="Json"으로 둔다.
- path가 불명확하면 path=""로 둔다.
- 태그는 저장 후보 표시일 뿐, 저장되었다고 말하지 않는다.
- 민감정보는 artifact에 넣지 않는다.
