# AN-18: 깜짝놀랐지

Not implemented yet.

- 분류: NPC
- 위치: 복도 중간 약 5호쯤?
- 현상: 귀신이 확 튀어나온다
- 기획서 트리거: 5호 지날 때쯤
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 2
- 필요한 에셋: 
- 사운드: 

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_18.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
