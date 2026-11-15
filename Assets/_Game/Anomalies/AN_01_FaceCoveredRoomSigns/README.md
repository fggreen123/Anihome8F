# AN-01: 이현성의 도배

Not implemented yet.

- 분류: 오브젝트
- 위치: 복도 - 모든 객실 문 앞 호수 표시판
- 현상: 모든 방 인원 표시 종이(<호수> 표시)가 전부 이현성의 얼굴로 바뀌어 있다
- 기획서 트리거: 복도 진입 시 상시 배치(정적)
- 원복 조건: 해당 복도 구간 이탈 시 원복
- 출현 가중치: 5
- 필요한 에셋: 이현성 얼굴 텍스처, 표시판 머티리얼 변형
- 사운드: 없음

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_01.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
