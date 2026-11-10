# 복도 프리팹으로 이상 현상 추가하기

`Game` 씬의 `GameLoop > FloorLoopManager`가 복도를 선택합니다.
플레이어와 GameLoop는 복도 바깥에 유지되고, CorridorRoot 아래의 복도만 교체됩니다.

## 기본 구성

- 정상 복도: `Prefabs/Corridors/Corridor_Normal.prefab`
- `Normal Corridor Prefab`: 정상 복도 프리팹
- `Anomaly Corridor Prefabs`: 이상 현상별 복도 프리팹 목록
- `Anomaly Chance`: 이상 층이 나올 확률. 기본 0.5입니다.
- 각 복도 루트의 `CorridorLayout > Spawn Point`: 그 복도 안의 PlayerSpawn

## 새 이상 현상 추가

1. Project 창에서 Corridor_Normal 프리팹의 Prefab Variant를 만듭니다.
2. 변형 프리팹을 열어 모델, 배치, 머티리얼, 조명 등을 수정합니다.
3. 필요한 경우 변형 프리팹 안의 PlayerSpawn 위치와 방향도 조정합니다.
4. Game 씬의 GameLoop에서 Anomaly Corridor Prefabs 목록에 변형 프리팹을 추가하고 씬을 저장합니다.

PlayerSpawn, 양쪽 StairGate, 문과 번호판, NPC 경로 등은 복도 프리팹 안에 유지합니다.
플레이어, 플레이어 카메라와 FloorLoopManager는 복도 프리팹에 넣지 않습니다.
머티리얼이나 외부 모델 원본을 수정하면 다른 프리팹에도 반영되므로,
특정 이상 현상에만 적용할 재질과 원본은 별도 에셋으로 복제해 사용합니다.

## 실행 흐름

시작 또는 층 전환 → 이상 여부 추첨 → 정상 복도 또는 등록된 이상 복도 중 하나 선택
→ 새 복도 생성 → 이전 복도 비활성화 및 제거 → 번호판 갱신 → 새 PlayerSpawn에 배치 → NPC 재시작.

이상 복도 목록의 유효한 항목은 같은 확률로 선택됩니다. 연속 재등장 제한은 없습니다.
이상 복도가 하나도 없으면 정상 복도를 사용하고 이상 상태도 false로 유지합니다.
빈 항목, 시작점이 없는 항목, 플레이어나 관리자를 포함한 항목은 추첨에서 제외합니다.
각 층에서 프리팹을 새로 생성하므로 문 등은 프리팹에 저장된 초기 상태로 돌아갑니다.
1층에 도착하면 정상 복도를 생성하고 NPC를 숨깁니다.

예전 구조의 씬에 변환이 필요한 경우 Game 씬에서
Tools > Dormitory > Setup Corridor Prefabs를 한 번 실행합니다.
이미 정상 복도가 연결되어 있으면 기존 프리팹을 다시 생성하거나 덮어쓰지 않습니다.
