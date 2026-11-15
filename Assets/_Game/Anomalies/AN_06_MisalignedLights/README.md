# AN-06: 엇갈린 형광등

Not implemented yet.

- 분류: 공간/구조
- 위치: 복도 천장 형광등 / 현관문 배열
- 현상: 복도 현관문(또는 형광등) 배열이 불규칙적으로 배열되어 있다
- 기획서 트리거: 상시(정적)
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 4
- 필요한 에셋: 문 프리팹 배치 변형, 형광등 프리팹 배치 변형
- 사운드: 형광등 깜빡임 노이즈(선택)

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_06.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
