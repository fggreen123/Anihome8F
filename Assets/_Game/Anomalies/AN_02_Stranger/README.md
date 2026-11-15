# AN-02: 낯선 사람

Not implemented yet.

- 분류: NPC
- 위치: 맵 중간 지점 객실 문 1개
- 현상: 문 하나가 끼이익 하고 살짝 열리고, 안을 자세히 보면 귀신이 서서 플레이어를 지켜본다 (입장 불가)
- 기획서 트리거: 플레이어가 복도 중간 트리거 볼륨 진입
- 원복 조건: 플레이어가 구간 이탈 시 문이 닫히고 사라짐
- 출현 가중치: 4
- 필요한 에셋: 귀신 모델, 대기(Idle) 애니메이션, 문 개방 애니메이션, 실내 어둠 세팅
- 사운드: 문 삐걱임, 저주파 앰비언스

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_02.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.
