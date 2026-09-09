# 기숙사 한 층 복도 재현

제공된 사진 6장을 참고해 실행 중인 Blender에서 제작한 3D 모델입니다. EscapeFromAnihome 프로젝트에 FBX, URP 재질, 프리팹, 조명, 충돌체와 1인칭 확인용 장면을 추가했습니다.

## Unity에서 확인

프로젝트: `C:/Users/jiwoo/OneDrive/문서/UnityProjects/EscapeFromAnihome`

장면: `Assets/DormitoryReconstruction/Scenes/DormitoryFloor.unity`

프리팹: `Assets/DormitoryReconstruction/Prefabs/DormitoryFloor.prefab`

Unity의 Project 창에서 **DormitoryFloor** 장면을 열고 Play를 누릅니다.

- WASD 또는 방향키: 이동
- Shift: 빠르게 이동
- 왼쪽 클릭 후 마우스: 시점 회전
- Escape: 마우스 잠금 해제

기존 SampleScene과 프로젝트 렌더링 설정은 덮어쓰지 않았습니다. 장면은 독립적으로 열어 사용하는 구성이며, 빌드 씬 목록에는 자동으로 추가하지 않았습니다.

## 모델 구성

- 반복되는 돌출 벽과 안쪽으로 들어간 방문, 옅은 황록색 문, 구형 손잡이, 도어클로저와 문 안내판
- 회색 비닐 바닥, 갈색 걸레받이, 천장 타일, 패널 조명, 감지기와 스프링클러
- 작은 창 쪽 전자레인지 수납장, 벽걸이 공용 장치, 소화기, 엘리베이터 외관과 점자 타일
- 큰 창 쪽 초록 창틀, 파란 고무 바닥, 정수기와 컵 수거함, 운반 카트
- 양끝의 계단과 붉은 난간, 스테인리스 수직 안전봉
- 곡선 칸막이 독서실 책상, 수납장, 빨간 의자, 회전의자, 스탠드형 에어컨과 라디에이터

방과 엘리베이터는 외관 모델이며 문 개폐·엘리베이터 운행 기능은 포함하지 않습니다. 계단은 층 주변의 두 계단참과 연결 계단을 재현한 범위입니다.

## 원본과 검증 자료

- `DormitoryFloor.blend`: 수정 가능한 Blender 원본. 생성한 텍스처 3개가 파일 안에 포함돼 있습니다.
- `DormitoryFloor.fbx` 및 `Textures/`: Unity에 넣은 모델과 텍스처
- `Blender_*.png`: Blender 렌더 6개
- `Unity_*.png`: Unity 카메라 렌더 6개
- `UnityIntegrationReport.json`: 최종 가져오기·재질·충돌체 검사 결과
- `Dormitory_UnityAssets.zip`: 프로젝트에 추가한 폴더의 백업. `.meta` 파일을 포함합니다.

최종 검증: Unity 장면과 프리팹 저장 성공, 33개 재질 연결 및 누락 없음, 486개 충돌체, 6개 시점의 바닥 확인, 폭 0.44m 캡슐의 복도 중앙 통과 확인, Play 모드의 복도 화면 표시 확인. 실제 방 출입이나 엘리베이터 동작은 구현 범위에 포함하지 않았습니다.

Unity 메뉴 **Tools → Dormitory → Open Walkthrough Scene**으로 확인용 장면을 열 수 있습니다. **Rebuild + Capture**는 이 작업에서 생성한 장면/프리팹을 다시 구성하므로, 직접 편집한 뒤에는 원본을 따로 복사해 두고 사용하세요.

## 추정한 부분

실측 도면이 없으므로 사진의 문과 바닥 비례를 기준으로 복도 길이 약 22m, 좁은 구간 폭 약 1.8m, 천장 높이 약 2.48m로 구성했습니다. 양쪽 방문 6쌍, 실제 방 번호, 보이지 않는 연결부와 독서실의 출입 위치는 확정 정보가 아닙니다. 사진 속 개인 이름은 재현하지 않았으며, 창밖 건물은 간략화했습니다. 정확한 실측 복제 모델이 아닌 사진 기반 재현 모델입니다.
