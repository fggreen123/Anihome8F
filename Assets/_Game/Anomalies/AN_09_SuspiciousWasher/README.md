# AN-09: 수상한 세탁기

Not implemented yet.

- 분류: 공간/구조
- 위치: 화장실 세탁기
- 현상: 세탁되고 있는 세탁기 중, 세탁물이 빨갛게 변해🩸  돌아가고 있는 세탁기가 있다.
- 기획서 트리거: 상시(정적)
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 4
- 필요한 에셋: 세탁기 오브젝트, 붉은 실내 공간(또는 붉은 벽)
- 사운드: 없음

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_09.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
