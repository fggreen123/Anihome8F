# Tripo 캐릭터 리깅

- `Grinner_Original.blend`: 가져온 원본. 고밀도 메시와 텍스처를 보관합니다.
- `Grinner_Rigged.blend`: 새로 가져온 캐릭터를 5만 삼각형으로 줄이고 다시 리깅한 최신 파일입니다. 이 파일에서 작업하세요.
- `Grinner_Rigged.fbx`: 변형용 뼈대와 메시, 텍스처를 포함한 내보내기 파일입니다. Unity용 최적화본은 `Assets/_Game/Anomalies/AN_21_MysteriousMonster`에 별도로 준비했습니다. Humanoid Avatar 설정은 완료했으며 걷기·공격 애니메이션 연결은 후속 작업입니다.
- `Textures`: 원본 색상, 금속, 노멀, 거칠기 텍스처.
- `Backups/BeforeReplacement_20261115_233044`: 이전 리깅본과 교체 전 작업 화면을 보관한 백업입니다. 이전 캐릭터와 뼈대는 현재 작업 화면에서 제거했습니다.

## Blender에서 움직이기

1. `Grinner_Rigged.blend`를 엽니다. 리그가 선택된 포즈 모드로 저장되어 있습니다.
2. 손목·발목의 원형 조작 장치를 선택하고 `G`로 움직입니다.
3. 팔꿈치·무릎 앞뒤의 작은 원은 구부러지는 방향을 정합니다.
4. `Root`는 캐릭터 전체를 이동합니다. `Hips`, `Spine`, `Chest`, `Head`는 회전해서 자세를 잡습니다.
5. 리그 사용자 속성의 `Arm_IK_L/R`, `Leg_IK_L/R`를 0으로 바꾸면 해당 팔·다리는 뼈대 회전 방식(FK)으로 조작합니다. 기본값 1은 손발 위치로 조작하는 방식(IK)입니다.
6. 포즈를 초기화하려면 모든 뼈를 선택하고 `Alt+G`, `Alt+R`, `Alt+S`를 누릅니다.

## 범위와 확인

- 새로 가져온 메시도 실제로는 삼각형 1,961,468개여서 Blender에서 50,000개로 줄였습니다.
- 키 2.13m, 정점 24,988개, 삼각형 50,000개, 변형용 뼈 23개와 전체 이동/손발 조작용 뼈 9개.
- 정점마다 최대 4개의 뼈 영향을 사용하며, 뼈대가 연결되지 않은 정점은 없습니다.
- 팔 굽히기, 무릎 들기, 머리 회전과 칼의 강체 움직임을 확인했습니다.
- 칼과 모자는 각각 손과 머리를 따라갑니다.
- 손가락 개별 조작과 얼굴 표정 리그는 포함하지 않습니다. 원본은 옷·손·칼이 이어진 단일 메시라, 손가락을 펴거나 입을 크게 움직이려면 별도 메시 정리가 필요합니다.
- 걷기·공격 애니메이션은 아직 만들지 않았습니다.

`build_rig.py`는 제작 기록용 스크립트입니다. 이미 리깅된 파일에서 다시 실행하지 마세요.


## Unity delivery

Use `Assets/_Game/Anomalies/AN_21_MysteriousMonster/Prefabs/Grinner.prefab`. Unity-specific FBX omits embedded textures; runtime textures and the URP material are stored in the same numbered folder. This source folder keeps the original 4K textures and packed Blender files.
