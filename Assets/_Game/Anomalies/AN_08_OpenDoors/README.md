# AN-08: 열린 문들

Not implemented yet.

- 분류: 공간/구조
- 위치: 복도 전체 객실 문
- 현상: 모든 방의 문이 열려 있다 (들어가기 불가)
- 기획서 트리거: 상시(정적)
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 4
- 필요한 에셋: 문 개방 상태 프리팹, 실내 더미 공간(또는 검은 벽)
- 사운드: 없음

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_08.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
