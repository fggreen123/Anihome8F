# AN-12: 창문 밖 이상

Not implemented yet.

- 분류: 공간/구조
- 위치: 창문 밖
- 현상: 복도 창문 밖 풍경이 점프스케어/다른 장소로 바뀌어 있다
- 기획서 트리거: 플레이어가 층을 내려와서 뒤를 뒤돌아 보거나 창문에 근접할때(반경 약 2m)시 발동
- 원복 조건: 플레이어가 구간 이탈 시 이상현상이 사라짐
- 출현 가중치: 3
- 필요한 에셋: 점프스케어 에셋, 점프스케어 브금
- 사운드: 점프스케어 브금

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_12.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
