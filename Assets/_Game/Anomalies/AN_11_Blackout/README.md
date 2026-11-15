# AN-11: 정전

Implemented: existing corridor-midpoint blackout.

- 분류: 공간/구조
- 위치: 복도 천장 형광등
- 현상: 복도 전체가 순간 암전되고 비상등만 켜진다
- 기획서 트리거: 상시(정적)
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 5
- 필요한 에셋: 복도 천장 형광등, 비상구 유도등
- 사운드: 전등 나가는 브금(도어즈에서 가져오는 거 추천)

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_11.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.

## Existing behavior

`CorridorBlackoutAnomaly.cs` preserves the current midpoint trigger and flicker. Light intensity, emission property blocks, ambient light, reflection intensity, audio and player helper light are restored on reset. The old second probability roll has been removed so an anomaly floor cannot silently have no anomaly.
