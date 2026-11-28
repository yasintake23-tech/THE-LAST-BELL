# Chapter I finale and Chapter II opening assets

| Asset | Provenance | Changes and destination |
|---|---|---|
| CRT video | User-provided `gemini_generated_video_27727e4a.mp4` in `sesler ve görüntüler.zip`, supplied for this project | Byte-identical copy in `Assets/StreamingAssets/Chapter1_Television.mp4`. H.264/AAC, 1280 × 720, 20.032 s. |
| Four Turkish voice recordings | User-provided MP3s in `sesler ve görüntüler.zip`, supplied for this project | Decoded to PCM WAV using FFmpeg with original timing and level. `Assets/Audio/Chapter1/Dialogue/{TV_Reaction,Corridor_Return,Elevator_Approach,Elevator_Inside}.wav`. |
| Elevator cab and doors | Original Blender model authored for THE LAST BELL | Exported to `Assets/Environment/Original/Elevator/Elevator_{Cab,Leaf}.fbx`. Existing game material library reused. |
| Eyelids, CRT effects, vent gas texture and shader | Original effects authored for THE LAST BELL | `Assets/Environment/Original/Elevator`. |
| Awakening ceiling fixtures, finishes and props | Reused from the existing project. The Poly Haven `mounted_fluorescent_lights` model and ambientCG concrete, plaster and floor materials are CC0. | See [Chapter I asset provenance](CHAPTER1_MILESTONE_ASSETS.md). |
| Collapse and get-up motion | Existing project assets `Fall_Forward_V2.fbx` and `Get_Up_V2.fbx` | Reused through the existing Humanoid avatar during the final sequence. |
| `Original_Descent_Score.wav` | Original score composed for THE LAST BELL, no samples from external music | `Assets/Audio/Chapter1/Finale`. |

New Foley edits use the project's previously documented CC0 recordings. The original sources, authors and licenses are in [AUDIO_PROVENANCE.json](AUDIO_PROVENANCE.json).

| Edit in `Assets/Audio/Chapter1/Finale` | Inputs and processing |
|---|---|
| `Elevator_Slam.wav` | Heavy door close, handle latch and Feştek impact. Varispeed, decay envelopes and layered transients. |
| `Shaft_Descent.wav` | Heavy hinge opening, distant exterior and relay thunks. Varispeed, band limiting, modulation and timed transients. |
| `Vent_Hiss.wav` | Evening wind and original filtered noise. Built as a short vent envelope. |
| `Distressed_Breath_Foley.wav` | Evening-wind recording and original filtered noise shaped into breath pulses. |
| `Dragging_Floor.wav` | Soil footsteps, frame creaks and wind. Varispeed, filters and intermittent friction envelopes. |
| `Collapse_Impact.wav` | Concrete footstep and frame creak, slowed and combined. |
