# Project structure and anomaly workflow

## Main folders

모든 에셋·작업용 폴더와 파일 이름을 영어로 통일했습니다. 기획서의 한글 제목과 설명은 문서 내용과 Inspector 데이터에 남아 있습니다.

| Location | Content |
|---|---|
| `Assets/_Game/Scenes` | 게임, 인트로, 메뉴, 테스트 씬 |
| `Assets/_Game/Environment` | 기숙사, 화장실, 공통 소품 |
| `Assets/_Game/Player` | 플레이어 이동 코드와 발소리 |
| `Assets/_Game/UI` | 인트로·메뉴 코드, 이미지, 셰이더, 소리 |
| `Assets/_Game/Shared` | 공통 문 상호작용과 효과음 |
| `Assets/_Game/Scripts` | 층 반복·계단·층 표시 |
| `Assets/_Game/Anomalies` | 기획서 번호별 이상현상 |
| `Assets/_Game/Design` | 기획서 스냅샷과 번호 목록 |
| `Assets/_Game/Editor` | 환경 제작 도구 |
| `Assets/ThirdParty` | 외부 하늘 에셋과 Unity 템플릿 |
| `Assets/TextMesh Pro` | 패키지가 고정 경로로 확인하는 글꼴 리소스 |
| `ArtSource/Anomalies` | 같은 번호별 Blender 원본·원본 텍스처·이전 버전 |

Blender 파일과 백업은 Assets 밖에 보관합니다. Unity는 게임에 사용할 FBX와 압축 텍스처만 불러오며, 무거운 원본을 중복으로 가져오지 않습니다.

## AN-11 Blackout

실제 코드: `Assets/_Game/Anomalies/AN_11_Blackout/Scripts/CorridorBlackoutAnomaly.cs`.

기존 복도 중간 진입 → 깜빡임·소등 동작은 유지하고, 층 반복 관리자와의 직접 연결을 공통 선택·원복 구조로 분리했습니다. 이제 이상현상 층에서는 준비가 끝난 현상 하나를 선택합니다. 기존의 두 번째 50% 추첨을 없애 실제 현상 없이 이상현상 층으로 판정되는 경우를 막았습니다.

층 전환 시 조명 밝기·활성화 상태, 발광 재질 PropertyBlock, 환경광·반사 강도, 효과음, 플레이어 보조 조명을 원복합니다. 공통 문·표지판·위치 초기화 뒤에 새 이상현상을 준비하므로 다른 이상현상이 만든 상태를 공통 초기화가 덮어쓰지 않습니다.

## AN-21 Mysterious Monster

사용할 프리팹: `Assets/_Game/Anomalies/AN_21_MysteriousMonster/Prefabs/Grinner.prefab`.

5만 삼각형, Unity Humanoid Avatar, URP 재질, 2K 압축 텍스처, 간단한 캡슐 충돌체까지 준비했습니다. 원본 4K 텍스처와 Blender 리그는 `ArtSource/Anomalies/AN_21_MysteriousMonster`에 있습니다. 추격 행동과 걷기·공격 애니메이션은 아직 구현하지 않았습니다.

## Adding another anomaly

1. `Assets/_Game/Design/AnomalyIndex.md`에서 기획서 번호를 찾습니다.
2. 해당 `AN_XX_EnglishName` 폴더의 `Scripts`, `Art`, `Audio`, `Prefabs`에 관련 파일을 넣습니다. Blender 원본은 같은 번호의 ArtSource 폴더에 보관합니다.
3. `_Core/AnomalyTemplate.cs.txt`를 복사해 실제 `.cs` 파일과 클래스 이름을 정하고 `Number`를 맞춥니다.
4. `OnBeginFloor`에서 원래 상태를 저장하고 현상을 준비합니다. `Update`에서는 `CanTrigger`가 true일 때만 발동을 확인합니다. `OnResetFloor`는 코루틴·소리를 멈추고 바꾼 상태를 전부 복원하며 여러 번 호출해도 안전해야 합니다.
5. `GameLoop` 또는 그 자식에 컴포넌트를 붙이고 해당 번호의 `AN_XX.asset`을 연결합니다. 씬 연결과 원복 검증이 끝났을 때만 `implemented`를 켭니다.

`FloorLoopManager`에 개별 이상현상 분기를 추가할 필요가 없습니다. 현재 실행 가능한 행동은 AN-11이며, 나머지 번호 폴더는 준비된 작업 공간입니다. 기획서가 비어 있는 AN-23/24는 Reserved로 남겼습니다. AN-21/23/24의 기획서 가중치는 미정이며 설정상의 기본값 1은 추첨에 사용되지 않습니다.

## Verification

- 플레이어 발소리 참조도 다시 연결하고 걷기 재생·정지 및 공중 무음 검사를 통과했습니다.
- 실제 플레이: 정전 발동, 31개 조명 소등, 환경광·반사 변화, 8층 → 7층 정답 계단 이동, 조명 원복, 다음 현상 재선택을 확인했습니다.
- 20회 선택에서 두 번째 확률 탈락 없이 하나의 현상이 준비됐습니다.
- 정상 층, 비활성 현상, 필수 연결 누락 상태에서 빈 이상현상 층이 생기지 않는 것을 확인했습니다.
- 4개 씬과 2개 프리팹의 스크립트 및 모델 연결을 검사했습니다. 화장실 모델 루트의 메시 없는 빈 Renderer 슬롯은 실제 누락 재질에서 제외했습니다.
- 원래 파일 GUID는 유지하거나 사용하지 않는 원본과 함께 보관했으며, 중복 GUID와 예기치 않은 원본 파일 유실은 없습니다.
- 캐릭터는 Unity에서 사람형 팔·다리 자세를 적용해 메시 변형을 확인하고 프리팹 미리보기로 재질을 확인했습니다.
- 검증 결과는 `Docs/Validation`에, 캐릭터 미리보기는 `Docs/Previews`에 있습니다.

이 작업은 폴더 구성과 캐릭터 가져오기를 정리한 것입니다. 기존 맵 전체의 프레임 성능을 목표 PC에서 측정한 결과는 포함하지 않습니다.
