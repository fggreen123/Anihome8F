# AN-03: 9와 4분의 3층 엘리베이터

Not implemented yet.

- 분류: UI/텍스트
- 위치: 엘리베이터 층수 표시기
- 현상: 층수 표시가 비정상적인 층으로 표시되어 있다
- 기획서 트리거: 상시(정적)
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 5
- 필요한 에셋: 세그먼트 디스플레이 머티리얼, 숫자 폰트 에셋
- 사운드: 전자음 지지직(선택)

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_03.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
