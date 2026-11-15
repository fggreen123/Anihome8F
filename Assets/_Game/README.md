# Anihome8F project folders

All folder and file names use English. Korean design titles remain in README content and definition fields.

| Folder | Content |
|---|---|
| `Scenes` | Intro, Game, Main, Sample and associated lighting data |
| `Environment` | Shared dormitory, washroom and prop assets |
| `Player` | Player movement and footsteps |
| `UI` | Intro/menu scripts, images, shaders and audio |
| `Shared` | Shared doors and reusable audio |
| `Scripts` | Floor loop, stairs and floor displays |
| `Anomalies/AN_XX_Name` | Numbered anomaly scripts, art, audio, prefabs and definition |
| `Anomalies/_Core` | Shared selection and reset lifecycle |
| `Design` | Source plan snapshot and anomaly index |
| `Editor` | Environment building tools |
| `Input` | Input System actions |
| `Settings` | Rendering configuration |

Third-party skyboxes and Unity template files are in `Assets/ThirdParty`. TextMesh Pro remains at `Assets/TextMesh Pro` because its resource importer checks that exact location.

## Character sources

`ArtSource/Anomalies/AN_21_MysteriousMonster` contains Blender files, source textures and backups. Runtime FBX, material and compressed textures are in AN-21 under Assets. Keep `.blend`, high-poly originals and backups outside Assets so Unity does not import duplicate models.

## Adding an anomaly

See `Design/AnomalyIndex.md`, then the numbered folder README. Implement a component derived from `FloorAnomaly`, assign that number's definition, attach it under `GameLoop`, and enable the definition's implementation flag only when ready. `FloorLoopManager` needs no anomaly-specific edits. One effect is selected at a time, and state is reset between floors.

AN-11 keeps the existing corridor-midpoint blackout behavior. AN-21 currently supplies the rigged character prefab; pursuit behavior and animations are not included yet.
