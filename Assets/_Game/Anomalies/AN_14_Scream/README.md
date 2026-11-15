# AN-14: 비명 소리

Not implemented yet.

- 분류: 사운드
- 위치: 전체
- 현상: 여자의 비명소리가 시작하자 마자 난다
- 기획서 트리거: 복도 진입 시
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 2
- 필요한 에셋: 
- 사운드: 비명소리

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_14.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
