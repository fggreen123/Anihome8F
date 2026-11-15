# AN-13: 핏물과 괴물

Not implemented yet.

- 분류: 공간/구조/UI
- 위치: 복도 천장
- 현상: 복도 천장 위에서 구멍이생기고 핏물이 똑똑 떨어지고 그 바닥에 물이 흥건하며 접근하면 천장에서 무서운게 떨어진다
- 기획서 트리거: 트리거 진입
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 4
- 필요한 에셋: 핏물 에셋 괴물에셋
- 사운드: 물방울소리 괴물소리 떨어지는 소리

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_13.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
