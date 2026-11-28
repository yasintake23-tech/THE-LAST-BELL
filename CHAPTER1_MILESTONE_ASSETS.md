# Chapter I production asset credits

## 3D models

The imported models and maps below are published under [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/). Poly Haven publishes the original meshes, authors and license. Original source payloads were checked against the MD5 values from the Poly Haven file API.

| Asset | Author | Use and modifications |
| --- | --- | --- |
| [Office Notepads](https://polyhaven.com/a/office_notepads) | Ulan Cabanilla | Classroom 101 assignments on the teacher’s desk. FBX and 1K albedo with the normal map converted to a 512 px PNG. |
| [Desk Lamp Arm 01](https://polyhaven.com/a/desk_lamp_arm_01) | Yann Kervran and Kuutti Siitonen | Unplugged clamp lamp on the Classroom 101 teacher’s desk. FBX and 1K albedo with the normal map converted to a 512 px PNG. |
| [Metal Trash Can](https://polyhaven.com/a/metal_trash_can) | GurJas Studios | Rusted variant and loose lid in Classroom 102. FBX and source albedo with the rusted normal map resized to 512 px. |
| [Television 02](https://polyhaven.com/a/television_02) | Ulan Cabanilla | Existing model re-exported with its 210 curved display faces isolated and remapped to full-frame UVs. The cabinet retains the original UVs and texture maps. |

The window boarding, staircase infill and distant masonry silhouettes are project-original geometry using existing project materials. See the environment asset provenance in `Assets/Environment/ASSET_PROVENANCE.md` for sources of those materials.

## New audio

| Destination in `Assets/Audio/Chapter1` | Source | Treatment |
| --- | --- | --- |
| `Dialogue/Return.wav` | Project owner attachment, `Tekrardan burdayım 5 yıl sonra-Ses.mp3` | Converted to 32 kHz mono PCM with low-rumble filtering and peak limiting. |
| `Dialogue/Entrance.wav` | Project owner attachment, `Burası düşündüğümden daha kötü hâle gelmiş .mp3` | Same conversion. |
| `Dialogue/Inside.wav` | Project owner attachment, `Hadi ama elektrikde yokmuş.. en iyisi panoyu arayayım .mp3` | Same conversion. Subtitle timing uses the separation between its two spoken phrases. |
| `SFX_ObjectiveCue.wav` | Original cue authored for THE LAST BELL | Three softly decaying partials in 32 kHz mono PCM. |

The three voice files are supplied for use in this project. No third-party license is asserted for them. Existing recorded sound-effect and ambience credits remain in `AUDIO_PROVENANCE.json`.
