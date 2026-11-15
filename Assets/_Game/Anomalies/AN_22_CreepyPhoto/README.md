# AN-22: 기괴한 사진

Not implemented yet.

- 분류: 오브젝트
- 위치: 방 종이
- 현상: 방 종이에 호수명 대신 기괴한 귀신 사진이 붙어 있음 
- 기획서 트리거: 상시
- 원복 조건: 해당구간 이탈 시 원복 
- 출현 가중치: 3
- 필요한 에셋: 귀신사진
- 사운드: 없음

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_22.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
