# AN-21: 의문의 괴물

Model and rig prepared; behavior is not implemented.

- 분류: 오브젝트
- 위치: 올라가야할 계단
- 현상: 복도 중간쯤 큰 발소리로 뒤에서 남성이 따라온다
- 기획서 트리거: 5호지날때쯤
- 원복 조건: 해당 구간 이탈 시 원복
- 출현 가중치: 기획서 미정 (현재 설정의 기본값은 1, 미구현 상태라 추첨에는 포함되지 않음)
- 필요한 에셋: 남성
- 사운드: 사운드

## Files

- `Scripts`: this anomaly's behavior only.
- `Art`: models, textures, materials, reference images.
- `Audio`: sound used only by this anomaly.
- `Prefabs`: reusable objects for this anomaly.
- `AN_21.asset`: plan number, description, weight, implementation status.

## Adding behavior

Use `FloorAnomaly` from `../_Core`, return the correct `Number`, and implement `OnBeginFloor` / `OnResetFloor`. Attach the component to `GameLoop` or one of its children and assign this folder's definition. Enable `implemented` only after scene references and reset behavior are ready. The common controller selects one implemented anomaly per anomaly floor. Do not add another random roll or write anomaly-specific logic into `FloorLoopManager`.

## Character

Use `Prefabs/Grinner.prefab`. The model has 50,000 triangles and a body rig. Pursuit logic and walking/attack animations are not implemented. Blender sources and backups are under `ArtSource/Anomalies/AN_21_MysteriousMonster` outside Assets to avoid duplicate Blender model imports.

## Unity import verification

- `Prefabs/Grinner.prefab`: 50,000 triangles, valid Humanoid Avatar with 22 mapped human bones, four skin influences per vertex.
- `Art/Models/Grinner.fbx`: approximately 2.1 MiB without duplicated embedded images.
- `Art/Materials/Grinner_Body.mat`: URP/Lit.
- Runtime textures: BaseColor, Normal and MetallicSmoothness (metallic in red, smoothness in alpha), maximum 2048 pixels, compressed with mipmaps. The original 4K files remain in ArtSource.
- Humanoid arm/leg pose changes were tested in Unity, and the material was visually checked with Unity's prefab preview renderer.
- The previous prototype assets and source recording copies are stored outside Assets. The character prefab supplies the model and rig; pursuit behavior and walking/attack clips remain pending.
