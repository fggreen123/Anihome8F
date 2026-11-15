# The Grinner — 의문의 남성

제작 기준: 2026-11-11. Anihome8F 기획서 아이디어 21에 사용할 캐릭터의 첫 3D 모델.
사용자가 제공한 캐릭터 도안을 참고해 Blender 4.1에서 직접 메시를 구성했습니다.

## 파일

- `TheGrinner.blend`: 편집용 원본. 절차적 재질, 기본 뼈대, 스튜디오 조명과 카메라를 포함합니다. 제공된 도안은 이미지 데이터로 내장되어 있습니다.
- `TheGrinner_Baked.blend`: 이미지 텍스처로 변환한 재질을 적용한 버전입니다.
- `TheGrinner.glb`: 재질과 뼈대가 포함된 휴대용 모델입니다.
- `TheGrinner_FullBody.png`: 전신 렌더입니다.
- 프로젝트의 `Assets/Characters/TheGrinner/TheGrinner.fbx`: Unity용 모델입니다.
- 프로젝트의 `Assets/Characters/TheGrinner/Textures/`: 몸체 4K Base Color·Normal, 2K Roughness / 무기 1K 텍스처입니다.

## 모델 구성

- 약 2.13m 크기, Blender Z축 위 / -Y 방향 정면. FBX는 Y축 위로 변환해 내보냅니다.
- `TheGrinner_Body`: 머리, 모자, 의상, 손, 신발을 포함한 스킨 메시.
- `TheGrinner_Cleaver`: 별도 메시. 오른손 뼈대의 웨이트로 움직입니다.
- `TheGrinner_Rig`: 22개 뼈. 골반·척추·목·머리·양팔·양다리·발을 포함합니다.
- 팔꿈치와 무릎, 몸통에 혼합 웨이트가 있습니다. 손가락은 손 뼈와 함께 움직이며 개별 손가락 뼈는 없습니다.
- 조명·카메라·바닥은 프리뷰 전용이며 FBX와 GLB에는 포함하지 않습니다.

## Unity에서 사용

1. `TheGrinner.fbx`를 씬으로 드래그합니다. 1 Unity unit = 1m 기준으로 내보냈습니다.
2. 재질은 URP/Lit를 사용하고 `BaseColor`를 Base Map에 연결합니다.
3. `Normal` 텍스처의 Texture Type을 Normal map으로 설정하고 Normal Map에 연결합니다.
4. Roughness는 Blender/glTF용 원본입니다. URP Smoothness에는 그대로 연결하지 않습니다. Smoothness = 1 − Roughness로 변환하거나 몸체 Smoothness를 약 0.15로 설정합니다.
5. 현재 모델은 제작용 중립 자세입니다. 외부 Humanoid 애니메이션을 적용하려면 Rig의 Configure에서 뼈 매핑과 T-pose를 확인해야 합니다. Generic 리그로도 사용할 수 있습니다.

## 현재 범위

도안의 큰 체격, 너덜너덜한 모자, 낡은 외투, 흐린 눈, 과장된 치열과 녹슨 칼을 반영한 첫 모델입니다. 도안의 실사 수준 피부·천 디테일을 완전히 재현한 최종 조각 모델은 아닙니다.

걷기·추적 애니메이션, 얼굴 표정 리그, 게임 내 등장 트리거와 AI, 콜라이더, LOD는 포함하지 않습니다. 실제 애니메이션을 붙인 후 관절·외투의 변형을 추가 보정해야 합니다.

## 제작 스크립트

`build_grinner.py` → `detail_grinner.py` → `finish_grinner.py`를 같은 Python 전역 공간에서 실행하면 새 캐릭터 스튜디오를 생성합니다. `export_grinner.py`는 UV 생성·텍스처 베이크·내보내기를 수행합니다. Blender MCP 작업에 사용한 재현용 원본이며 Unity 런타임 코드는 아닙니다.
