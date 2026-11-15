# AN-07: 피

Not implemented yet.

- 분류: 오브젝트
- 위치: 화장실 문 1개
- 현상: 화장실 문 중 하나가 잠겨 있고, 플레이어가 근처에 가면 문 밑으로 빨간 액체가 흘러나온다
- 기획서 트리거: 플레이어가 해당 문 근접(반경 약 2m) 시 발동
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 4
- 필요한 에셋: 액체 흐름 VFX/데칼, 잠긴 문 오브젝트
- 사운드: 물 떨어지는 소리, 문 안쪽 두드림(선택)

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_07.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
