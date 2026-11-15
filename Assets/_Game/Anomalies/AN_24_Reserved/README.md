# AN-24: 미정

Reserved number: no design content is listed yet.

- 분류: 
- 위치: 
- 현상: 
- 기획서 트리거: 
- 원복 조건: 
- 출현 가중치: 기획서 미정 (현재 설정의 기본값은 1, 미구현 상태라 추첨에는 포함되지 않음)
- 필요한 에셋: 
- 사운드: 

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_24.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
