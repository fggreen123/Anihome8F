# AN-04: 시체 절단

Not implemented yet.

- 분류: 오브젝트
- 위치: 복도 벽면 살충기(벌레 잡는 기계)
- 현상: 벽에 붙어있는 벌레 잡는 기계 안에 절단된 사람의 팔이 들어있다
- 기획서 트리거: 상시(정적)
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 3
- 필요한 에셋: 절단된 팔 모델, 혈흔 데칼, 살충기 오브젝트
- 사운드: 살충기 지직 소리, 파리 날갯소리

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_04.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
