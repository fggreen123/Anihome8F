# AN-16: 사라진 손잡이

Not implemented yet.

- 분류: 오브젝트
- 위치: 문 중 하나의 손잡이
- 현상: 문 중 하나의 손잡이가 사라져 있다.
- 기획서 트리거: 상시(정적)
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 3
- 필요한 에셋: 없음
- 사운드: 없음

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_16.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
