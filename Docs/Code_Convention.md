람다식은 꼭 필요한 경우만 사용하고, 최대한 자제할 것. 
- 단 한줄로 표현가능한 것은 람다식 사용
- 속성 표현, 메서드 내부 1줄인 경우

## 주석 작성
모든 클래스와, 메서드, 속성, 멤버변수에 xml 주석 달아줄 것.
메서드 내부 로직 진행에서 중요한 부분은 주석 달아줄 것.

## 디버그 로깅 (실행 추적)
새로 만드는 모든 메서드/생성자 본문 첫 줄에 `Log.Ins.Debug("시작");` 를 고정으로 넣는다.
(`Log` = `Framework.Common.Logger.Log`. 단축형이 멤버명과 충돌하는 클래스에서는 정규화형 `Framework.Common.Logger.Log.Ins.Debug("시작");` 사용.)

예외 — 다음에는 넣지 않는다.
- 테스트 메서드.
- 연속 발생 마우스 이동 이벤트 핸들러: `MouseMove`, `PreviewMouseMove`, `DragOver`, `PreviewDragOver`, `GiveFeedback`, `QueryContinueDrag` 등. (이동마다 로그가 과도하게 쌓이고 실익이 없다.)

그 외 사용자 액션 핸들러(클릭, 드롭/드래그 시작·완료 등 이산 이벤트)에는 필수로 넣는다.
주의: `MoveXxx`(데이터 이동 메서드), `ProjectRegistryService.Move`(프로젝트 경로 이동)처럼 "이동"이 이름에 들어가도 마우스 이동 이벤트가 아니면 넣는다.