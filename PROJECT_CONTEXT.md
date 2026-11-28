# THE LAST BELL — Project Context & Architecture Guide

> **Purpose:** Authoritative technical reference for **THE LAST BELL**, an existing Unity horror game targeting Android (ARM64 / IL2CPP) and PC. Future developers and AI agents can rely on this document instead of re-auditing the entire repository from scratch.

---

## 1. Engine, Toolchain & Project Configuration

| Property | Value | Source |
| --- | --- | --- |
| **Unity Editor Version** | `6000.6.7f1 (b22767f04f04)` (Unity 6.6) | `ProjectSettings/ProjectVersion.txt` |
| **Render Pipeline** | Built-in Render Pipeline (`customRenderPipeline` = null) | `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/QualitySettings.asset` |
| **Color Space** | Gamma (`m_ActiveColorSpace: 0`) | `ProjectSettings/ProjectSettings.asset` |
| **Input Handling** | Legacy Input Manager (`activeInputHandler: 0`) | `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/InputManager.asset` |
| **Product / Company** | `THE LAST BELL` / `TheLastBellStudio` | `ProjectSettings/ProjectSettings.asset` |
| **Android Package ID** | `com.thelastbell.game` | `ProjectSettings/ProjectSettings.asset` |
| **Android Scripting Backend** | `IL2CPP` (`Release`, `ARM64` only) | `ProjectSettings/ProjectSettings.asset` |
| **Android SDK Levels** | Min SDK `26` (Android 8.0), Target SDK `Auto` (`0`) | `ProjectSettings/ProjectSettings.asset` |
| **Orientation** | Landscape Left & Right (`defaultScreenOrientation: 3`) | `ProjectSettings/ProjectSettings.asset` |
| **Custom Gradle Templates** | Enabled (`mainTemplate.gradle`, `gradleTemplate.properties`) | `Assets/Plugins/Android/` |
| **Scripting Define Symbols** | `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY` | `ProjectSettings/ProjectSettings.asset` |

### Installed Packages (`Packages/manifest.json` & `Packages/packages-lock.json`)
- `com.unity.ugui` (`2.6.0`) — UI & TextMeshPro (`Unity.TextMeshPro` / `TMPro`)
- `com.unity.timeline` (`6.6.0`)
- `com.unity.cinemachine` (`6.6.0`)
- `com.unity.ai.assistant` (`2.21.0-pre.1`) & `com.unity.ai.inference` (`2.6.1`) (plus transitive `com.unity.dt.app-ui` `2.2.4`)
- `com.coplaydev.unity-mcp` (`https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`, locked hash `81542a7e86292b5c4c21b1782c5af23b567f45ce`)
- `com.unity.ide.rider` (`3.1.0`), `com.unity.ide.visualstudio` (`2.0.26`)
- Standard Unity modules (`animation`, `audio`, `physics`, `ui`, `video`, `androidjni`, etc.)

---

## 2. Current Game Architecture & Flow

### Scene Progression
Enabled scenes in `ProjectSettings/EditorBuildSettings.asset` (in exact build order):
1. **`Assets/Scenes/MainMenu.unity`** (index `0`)
2. **`Assets/Scenes/OpeningCinematic.unity`** (index `1`)
3. **`Assets/Scenes/AbandonedSchool.unity`** (index `2`)

*(Note: `Assets/_Recovery/0.unity` and `Assets/_Recovery/0 (1).unity` are Unity editor recovery snapshots not included in builds.)*

```
[MainMenu.unity]
   ├── "New Game" (Btn_NewGame) -> Sets PlayerPrefs("TLB_HasSavedGame", 1) -> Loads OpeningCinematic.unity
   └── "Continue" (Btn_Continue) -> Enabled if TLB_HasSavedGame == 1 -> Loads AbandonedSchool.unity
           │
           ▼
[OpeningCinematic.unity]
   ├── Plays OpeningCinematicVideo.mp4 (4-second Hold-to-Skip via touch/mouse/Space)
   ├── Fades to black -> Plays THE_LAST_BELL_HorrorJingle.wav -> Fades in TitleScreen_TheLastBell.jpg (5s hold with slow scale)
   └── Fades to black -> Loads AbandonedSchool.unity
           │
           ▼
[AbandonedSchool.unity]
   ├── SeamlessIntroManager holds exterior vantage camera (3.5s) then smoothly flies into Camera_Pivot (2.0s)
   ├── Unlocks FirstPersonController + PlayerInteractor + fades in PlayerHUD
   └── Player explores courtyard, opens entrance doors, enters school corridor & rooms
```

### Design & Coding Conventions
- **Namespaces:** `TheLastBell.Cinematics`, `TheLastBell.Interaction`, `TheLastBell.Player`, `TheLastBell.UI`, `TheLastBell.Editor`.
- **Hybrid PC + Mobile Control Pattern:**
  - `MobileControlManager` detects `Application.isMobilePlatform || (Application.isEditor && forceMobileInEditor)` to toggle `Mobile_Controls_Root` and cursor lock state (`CursorLockMode.None` on mobile, `Locked` on PC).
  - `MobileControlsUI` and `TouchLookZone` feed inputs directly into `FirstPersonController` (`MobileMoveInput`, `MobileLookInput`, `MobileSprintInput`, `MobileCrouchInput`).
  - `FirstPersonController` also reads Legacy `Input.GetAxisRaw("Horizontal"/"Vertical")`, `"Mouse X"/"Mouse Y"`, `LeftShift`, `LeftControl`/`C`, and `PlayerInteractor` reads `KeyCode.E`.
- **Modularity:** Gameplay interaction uses the `IInteractable` interface (`DoorInteractable`, `DrawerInteractable`, `ItemPickupInteractable`, `NoteInteractable`) polled via raycast + sphere fallback in `PlayerInteractor`.

---

## 3. Existing Features & Mechanics

1. **Main Menu (`MainMenu.unity`)**
   - Animated looping background video (`MainMenuBackground.mp4` rendered to `MenuBackground_RT.renderTexture` via `MenuVideoBackground` with `AspectRatioFitter` `EnvelopeParent` and dark fallback background).
   - Horror atmosphere effects (`HorrorAtmosphere`): pulsing subtitle alpha and flickering `Atmospheric Dim Light`.
   - Menu button hover/press scaling and color feedback (`MenuButtonEffects`).
   - Functional Settings panel (`TLB_MasterVolume`, `TLB_MusicVolume`, `TLB_SFXVolume`, `TLB_QualityLevel`, `TLB_TargetFPS` [30/60/120], `TLB_Haptics`), Controls guide panel, About/Story panel, Save check (`Continue`), and Exit.
2. **Opening Cinematic (`OpeningCinematic.unity`)**
   - Fullscreen video playback (`OpeningCinematicVideo.mp4` -> `IntroVideo_RT.renderTexture`).
   - 4-second Hold-to-Skip radial progress indicator (`Radial_Ring.png`) supporting touch, mouse click, and `Space`.
   - Synchronized transition to title art (`TitleScreen_TheLastBell.jpg`) + horror jingle audio (`THE_LAST_BELL_HorrorJingle.wav`), then loads `AbandonedSchool`.
3. **Gameplay Scene (`AbandonedSchool.unity`)**
   - **Seamless In-Engine Intro (`SeamlessIntroManager`)**: External camera vantage (`Cinematic_Start_Vantage`) -> smooth S-curve flight into `Camera_Pivot` (skippable via `Space` or left click).
   - **True First-Person Controller (`FirstPersonController`)**:
     - `CharacterController`-based walk (`2.8 m/s`), sprint (`5.2 m/s` in scene / `5.6 m/s` default), crouch (`1.4 m/s`, height `1.8m -> 1.1m` / camera `1.7m -> 1.0m` with ceiling `SphereCast` check).
     - Stamina pool (`100`) with drain, regeneration delay, exhaustion threshold (`25`), dynamic sprint FOV (`60 -> 64.5`), and breathing audio hookup.
     - Surface-aware footstep system (`Concrete`, `Wood`, `Metal` via downward raycast) and Mecanim `Animator` parameter updates (`Speed`, `Strafe`, `IsCrouching`, `IsGrounded`, `IsExhausted`, triggers `Interact`, `PickUp`, `Hit`, `Die`).
     - Head-mesh shadow-only culling helper (`ApplyTrueFPSHeadMeshCulling`) for True FPS mode.
   - **Flashlight (`FlashlightController`)**:
     - Child `SpotLight` (`range=25`, `spotAngle=55`, `intensity=3.5`, soft shadows).
     - Supports toggle (`F` key / mobile `FENER` button), optional battery drain + low-battery flicker + battery reload (`R` key) via `PlayerInventory`.
   - **Interaction & Inventory (`PlayerInteractor`, `PlayerInventory`, `DoorInteractable`)**:
     - Center raycast (`2.6m` in scene) + overlap sphere fallback.
     - Double-leaf school entrance door (`SchoolEntrance_Doorway`) animated open/close (`92°` over `1.1s`), with lock/key support via `PlayerInventory`.
   - **HUD & Mobile Touch Controls (`PlayerHUD`, `MobileControlsUI`, `TouchLookZone`, `MobileControlManager`, `MobileHoldButton`)**:
     - Floating left-half virtual joystick (`140x140` bg, `60x60` handle, `0.1` deadzone).
     - Right-half touch look zone (`TouchLookZone`) with `EventSystem` UI overlap filtering so dragging off buttons/joystick does not spin the camera.
     - Right-bottom thumb-arc circular buttons (`75x75`): `KOŞ` (hold-to-sprint), `ÇÖMEL` (crouch toggle), `FENER` (flashlight toggle), and context-sensitive `ETKİLEŞİM` (appears when looking at an `IInteractable`).
     - Center reticle dot, interaction prompt panel, stamina bar, and note inspection modal hierarchy.

---

## 4. Scene Structure Summary

### `Assets/Scenes/MainMenu.unity` (131 GameObjects, 0 missing scripts)
- `Main Camera` (`Camera`, `AudioListener`)
- `MainMenuCanvas` (`Canvas`, `CanvasScaler`, `GraphicRaycaster`, `MainMenuManager`, `HorrorAtmosphere`)
  - `FallbackDarkBackground`, `VideoBackgroundRawImage` (`RawImage`, `AspectRatioFitter`, `CanvasGroup`), `VideoDarkTint`
  - `LeftScrimOverlay` (`LeftScrimGradient.png`), `TitleContainer` (`Title`, `SubtitleText`)
  - `MainButtonsContainer` (`Btn_NewGame`, `Btn_Continue`, `Btn_Settings`, `Btn_Controls`, `Btn_About`, `Btn_Exit` — each with `MenuButtonEffects`)
  - `SettingsPanel`, `ControlsPanel`, `AboutPanel` (inactive by default)
  - `FooterLeftVersion`, `FooterRightAudioTip`
- `EventSystem` (`EventSystem`, `StandaloneInputModule`)
- `Atmospheric Dim Light` (`Light`)
- `MenuVideoPlayer` (`VideoPlayer` -> `MainMenuBackground.mp4` -> `MenuBackground_RT.renderTexture`, `MenuVideoBackground`)

### `Assets/Scenes/OpeningCinematic.unity` (14 GameObjects, 0 missing scripts)
- `OpeningCinematic_Controller` (`OpeningCinematicManager`, `VideoPlayer` -> `OpeningCinematicVideo.mp4` -> `IntroVideo_RT.renderTexture`, `AudioSource`)
  - `HorrorJingle_AudioSource` (`AudioSource`)
- `Cinematic_Canvas`
  - `Background_SolidBlack`, `Video_Container/Video_RawImage`, `Title_Container/Title_Image` (`TitleScreen_TheLastBell.jpg`), `BlackFade_Overlay`, `HoldToSkip_Panel` (`Skip_Circle_Track`, `Skip_Radial_Fill`, `Skip_Prompt_Text`)
- `Main Camera` (`Camera`, `AudioListener`)

### `Assets/Scenes/AbandonedSchool.unity` (187 GameObjects, 0 missing scripts)
- `Gameplay_HUD_Canvas` (`PlayerHUD`, `MobileControlManager`)
  - `Interaction_Prompt_Panel`, `Mobile_Controls_Root` (`Touch_Look_Zone`, `Joystick_Touch_Zone`, `ActionButtonsPanel` [`Mobile_SprintButton`, `Mobile_CrouchButton`, `Mobile_FlashlightButton`, `Mobile_ActionButton`], `Joystick_Background/Joystick_Handle`), `Reticle_Dot`, `Status_Message_Banner`, `Stamina_Bar_Container`, `Note_Inspection_Modal` (inactive)
- `--- ENVIRONMENT ---` (Blockout geometry with `Mat_*` materials & `BoxCollider`s):
  - `Corridor_Main`, `Classroom_101`, `Classroom_102`, `Classroom_103_Quarantine`, `Teachers_Lounge`, `Staircase_Area`, `Elevator_EndHallway`
- `School_Courtyard` (exterior asphalt, facade, walls, `Courtyard_ArrivedCar`, `Light_Courtyard_EntranceLamp`, cobble path, `SchoolEntrance_Doorway` with `DoorInteractable` + `Pivot_DoorLeaf_L`/`R`)
- `SeamlessIntroManager` + `Cinematic_Start_Vantage`
- `Player_Protagonist` (`CharacterController`, `PlayerInteractor`, `PlayerInventory`, `FirstPersonController`)
  - `Camera_Pivot` -> `Main Camera` (`Camera`, `AudioListener`) + `Flashlight_Rig` (`FlashlightController`, `AudioSource`, `Flashlight_Mesh`, `SpotLight`)
  - `Character_Model` (PrefabInstance of `Assets/Characters/Protagonist/exported-model.fbx` with 8 `M_Protagonist_*` materials)
- `--- LIGHTING ---` (`Moonlight_Directional` + 5 interior point/spot lights)

---

## 5. Important Scripts & Responsibilities

### Runtime Scripts (`Assets/Scripts/`)
| Area | Script | Responsibility |
| --- | --- | --- |
| **Cinematics** | `OpeningCinematicManager.cs` | Plays intro video, 4s hold-to-skip, title reveal + horror jingle, loads `AbandonedSchool`. |
| **Cinematics** | `SeamlessIntroManager.cs` | Exterior vantage hold + smooth camera flight into `Camera_Pivot`, then enables FPS & HUD. |
| **Player** | `FirstPersonController.cs` | True-FPS movement, crouch, stamina, breathing/footstep audio, head-bob/FOV, Animator sync. |
| **Player** | `FlashlightController.cs` | SpotLight toggle, battery consumption/flicker/reload, click SFX, shoulder mount offset. |
| **Player** | `PlayerInventory.cs` | Stores collected key IDs (`HasKey`/`AddKey`) and battery count. |
| **Player** | `TPSController.cs`, `TPSCameraFollow.cs` | Legacy/alternative third-person controller & collision-aware orbit camera (currently unused in scene). |
| **Interaction** | `IInteractable.cs` | Contract (`GetInteractionPrompt`, `CanInteract`, `Interact`) for all world interactables. |
| **Interaction** | `PlayerInteractor.cs` | Camera raycast + sphere fallback interaction detector, triggers player animations & HUD events. |
| **Interaction** | `DoorInteractable.cs` | Modular single/double door swing animation with key/lock support. |
| **Interaction** | `DoorInteraction.cs` | Legacy proximity-based entrance door script (unused; replaced by `DoorInteractable`). |
| **Interaction** | `DrawerInteractable.cs` | Sliding drawer/cabinet interactable with SFX (ready to place in scene). |
| **Interaction** | `ItemPickupInteractable.cs` | Key / battery / quest item pickup interactable (ready to place in scene). |
| **Interaction** | `NoteInteractable.cs` | Readable document interactable opening `PlayerHUD.ShowNoteUI` (ready to place in scene). |
| **UI** | `MainMenuManager.cs` | Main menu navigation, PlayerPrefs settings persistence, New Game / Continue / Exit. |
| **UI** | `MenuVideoBackground.cs` | Looping muted menu background video with `AspectRatioFitter` and mobile pause/resume handling. |
| **UI** | `MenuButtonEffects.cs` | Hover/press scale and crimson highlight animations for menu buttons. |
| **UI** | `HorrorAtmosphere.cs` | Subtitle breathing alpha pulse and background light flicker on MainMenu. |
| **UI** | `MobileControlManager.cs` | Platform switcher toggling `Mobile_Controls_Root` vs PC cursor lock. |
| **UI** | `MobileControlsUI.cs` | Floating virtual joystick, thumb-arc action buttons, scale punch & active tint states. |
| **UI** | `MobileHoldButton.cs` | PointerDown/Up hold handler for mobile Sprint button. |
| **UI** | `TouchLookZone.cs` | Right-side touch camera look with `EventSystem` UI overlap rejection. |
| **UI** | `PlayerHUD.cs` | Reticle, interaction prompt, status banner, stamina bar, battery bar, note inspection modal. |

### Editor Utilities (`Assets/Editor/`)
- `AndroidBuildHelper.cs`:
  - Menu items under `The Last Bell/` for saving/syncing scenes, configuring Android (`IL2CPP`, `ARM64`, `Release`, `com.thelastbell.game`, MinSDK 26), validating project integrity, and building `TheLastBell.apk`.
  - Contains `ValidateProjectBatch()`, `BuildAndroidAPKBatch()`, and `CIBuildProcessor` (`IPreprocessBuildWithReport` / `IPostprocessBuildWithReport`) which validate compilation, LFS pointers, scenes, and Android settings and output reports to `Logs/`.
- `MobileSetupHelper.cs`:
  - Menu item `The Last Bell/Mobil Düzeltmeleri ve Düzeni Uygula` — programmatically rebuilds/wires `AbandonedSchool.unity` mobile HUD hierarchy, `SpotLight`, `CanvasScaler`, and sprite references.
- `UISpriteGenerator.cs`:
  - Menu item `The Last Bell/UI/Generate Mobile Circle Sprites` — procedurally generates anti-aliased `circle_button.png`, `circle_joystick_bg.png`, and `circle_joystick_handle.png`.

---

## 6. Available Assets

- **Total Asset GUIDs:** 339 (0 missing `.meta`, 0 orphan `.meta`, 0 duplicate GUIDs).
- **Audio (`Assets/Audio/`):**
  - Ambience: `School_Eerie_Ambience_Loop.wav`
  - SFX (17 clips): `Document_Paper_Rustle.wav`, `Door_Creak_Open.wav`, `Door_Locked_Rattle.wav`, `Door_Slam_Close.wav`, `Drawer_Slide_Open.wav`, `Flashlight_Off.wav`, `Flashlight_On.wav`, `Footstep_Concrete_01..03.wav`, `Footstep_Metal_01..02.wav`, `Footstep_Wood_01..02.wav`, `Item_Pickup_Key.wav`, `Player_Exhausted_Breathing.wav`, `THE_LAST_BELL_HorrorJingle.wav`
- **Character (`Assets/Characters/Protagonist/`):**
  - FBX models: `exported-model.fbx` (used in `AbandonedSchool.unity`), `Protagonist_Character.fbx` (178 animations), `Avatar_Mesh.fbx`
  - Animator: `Protagonist_AnimatorController.controller` (Locomotion blend tree + Crouch, Crawl, Climb, Exhausted, Interact, PickUp, Hit, Death states)
  - 8 Materials (`M_Protagonist_Body`, `Bottom`, `Eyelashes`, `Eyes`, `Head`, `Shoes`, `Teeth`, `Top`) + 1K/512 PBR textures
- **Blockout Materials (`Assets/Materials/Blockout/`):** 19 Built-in `Standard` shader materials (walls, tiles, concrete, slate, decayed wood, metal, glass, elevator red light, exterior/car materials).
- **UI & Video (`Assets/Textures/UI/`, `Assets/UI/Textures/`, `Assets/Videos/`):**
  - Mobile sprites: `circle_button.png`, `circle_joystick_bg.png`, `circle_joystick_handle.png`
  - UI overlays/art: `LeftScrimGradient.png`, `Radial_Ring.png`, `TitleScreen_TheLastBell.jpg`
  - Videos & RenderTextures: `MainMenuBackground.mp4` (`MenuBackground_RT`), `OpeningCinematicVideo.mp4` (`IntroVideo_RT`), `gemini_generated_video_ed38eef1.mp4`
- **Fonts/Shaders:** `Assets/TextMesh Pro/` (Essential Resources + Examples & Extras, `LiberationSans SDF.asset`).

---

## 7. Known Problems & Current Blockers

### Critical CI / Repository Blockers
1. **Missing Git LFS Binary Objects on GitHub (`0 / 86` available on remote):**
   - `.gitattributes` tracks 86 binary assets (~79.92 MB total: all `.fbx`, `.wav`, `.mp4`, `.jpg`, `.png`, `.ttf`, `.psd` files) via Git LFS.
   - Commit `ce536b7` committed only the ~130-byte LFS pointer text files; the binary blobs were never pushed to GitHub LFS (`404 Object does not exist on the server`).
   - Until the original author pushes the LFS objects (`git lfs push --all origin main` from the source workspace), `actions/checkout` with `lfs: true` will fail with 404.
2. **Unity License GitHub Secrets Not Yet Configured:**
   - The repository currently has no GitHub Actions secrets (`UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_LICENSE`, `UNITY_SERIAL`, or `UNITY_LICENSING_SERVER`), so Unity batch-mode activation cannot run yet.

### In-Project / Scene Wiring Gaps (Existing Gameplay State)
3. **`AbandonedSchool.unity` Incomplete Component Bindings (Preserved As-Is):**
   - `PlayerHUD`: `playerController`, `statusContainer`, `batteryCanvasGroup`, `batteryFillImage`, and `notePanelRoot` are `null` (`{fileID: 0}`) in the scene even though `Player_Protagonist`, `Status_Message_Banner`, and `Note_Inspection_Modal` exist (`Stamina` bar and `ShowNoteUI` / `ShowStatusMessage` will partially no-op until wired).
   - `FirstPersonController`: `footstepAudioSource`, `breathingAudioSource`, `concreteSteps`, `woodSteps`, `metalSteps`, and `exhaustedBreathingClip` are unassigned in the scene.
   - `FlashlightController`: `audioSource`, `switchOnClick`, `switchOffClick` are unassigned; `useBattery` is `false`.
   - `SeamlessIntroManager`: `introAudioSource` and `windWhooshClip` are unassigned.
   - `Character_Model` (`exported-model.fbx` `Animator`): `Protagonist_AnimatorController.controller` is not yet assigned to the `Animator` in `AbandonedSchool.unity` (though `FirstPersonController` sets Animator parameters).
   - `DoorInteractable` (`SchoolEntrance_Doorway`): `blockerCollider` is `{fileID: 0}` (visual door leaves have `BoxCollider`s on children).
   - `Audio` & `Interactables`: None of the `Assets/Audio/` ambience/footstep/door clips, `DrawerInteractable`, `ItemPickupInteractable`, or `NoteInteractable` components are placed in `AbandonedSchool.unity` yet (only `SchoolEntrance_Doorway` has `DoorInteractable`).
4. **Lost Turkish Characters in Some Script Comments/Prompts:**
   - Several files under `Assets/Scripts/Interaction/`, `PlayerHUD.cs`, `PlayerInventory.cs`, and `AbandonedSchool.unity` strings (`Kapnn kilidi ald!`, `[E] - Kapy A`) have stripped diacritics from an earlier encoding conversion on the source machine.

### Resolved in Initialization Pass
- **Windows-Only `D:/thelast` Hardcoding Fixed:** `Assets/Plugins/Android/mainTemplate.gradle`, `Assets/Plugins/Android/gradleTemplate.properties`, and `Assets/Editor/AndroidBuildHelper.cs` previously forced `D:/thelast/.gradle` and `D:/thelast/.tmp` unconditionally, which breaks Linux/CI Gradle builds. These are now cross-platform (Windows-guarded where needed).

---

## 8. Android Build Instructions (Cloud & Local)

### Automated GitHub Actions Build (`.github/workflows/android-build.yml`)
- **Runner & Image:** `ubuntu-latest` with `game-ci/unity-builder@v6` and `unityci/editor:ubuntu-6000.6.7f1-android-4` (Ubuntu 24.04 + Unity `6000.6.7f1` + Android SDK/NDK/OpenJDK 17 + `/dev/shm` `1025m` for Unity 6.6).
- **Required Repository Secrets** (`Settings -> Secrets and variables -> Actions`):
  - **Option A — Unity Personal (Unity 6+, no desktop PC needed):**
    - `UNITY_EMAIL`: Unity ID email (standard email/password account, **2FA disabled**, not Google/Apple OAuth)
    - `UNITY_PASSWORD`: Unity ID password (avoid special characters if possible per GameCI recommendations)
  - **Option B — Unity Pro / Plus:**
    - `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`
  - **Option C — License Server / Enterprise `.ulf`:**
    - `UNITY_LICENSING_SERVER` or `UNITY_LICENSE`
  - **Optional Release Signing Secrets** (if omitted, Unity builds a debug-signed APK installable via sideloading/`adb`):
    - `ANDROID_KEYSTORE_NAME`, `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`
- **How to Trigger:**
  - Automatically on `push` / `pull_request` to `main` touching `Assets/**`, `Packages/**`, `ProjectSettings/**`, or `.github/workflows/android-build.yml`.
  - Manually via GitHub **Actions -> Android APK Build & Validation -> Run workflow**, choosing:
    - `build_mode`: `build` (full APK) or `validate-only` (fast compile + scene + asset validation)
    - `clean_library_cache`: `true` to force clean `Library` re-import
    - `android_symbol_type`: `none` | `public` | `debugging`
- **Artifacts Produced:**
  - `TheLastBell-Android-APK` (`build/Android/TheLastBell.apk`, 30-day retention)
  - `TheLastBell-Build-Logs` (`Logs/ci-prebuild-report.txt`, `Logs/ci-build-report.txt`, `Logs/ci-validation-report.txt`, 14-day retention)

### Local Unity Editor Build (If Opened Locally)
- Open with Unity **`6000.6.7f1`**.
- Use menu `The Last Bell -> 4. Projeyi Doğrula (CI Validation)` to validate, then `The Last Bell -> 2. Android APK Çıktısı Al` (outputs `Builds/Android/TheLastBell.apk`).

---

## 9. Recommended Development Priorities (Next Phases)

1. **Unblock CI Execution (Immediate User Action):**
   - Push the 86 missing Git LFS binary assets to GitHub (`git lfs push --all origin main` from the source machine/backup), and add Unity licensing credentials to GitHub Secrets.
   - Run `workflow_dispatch` (`validate-only`, then `build`) to verify a green cloud APK build and test installing the generated APK on an Android device.
2. **Wire Existing Assets & Scene References in `AbandonedSchool.unity` (Zero New Code Needed First):**
   - Assign `Protagonist_AnimatorController.controller` to `Character_Model`'s `Animator`.
   - Connect missing `PlayerHUD` references (`playerController`, `statusContainer`, `notePanelRoot`, battery UI).
   - Assign the 18 existing `Assets/Audio/` clips (ambience loop, footsteps, flashlight clicks, door creak/locked/slam) to `FirstPersonController`, `FlashlightController`, and doors.
   - Fix Turkish UTF-8 strings in `DoorInteractable.cs`, `DrawerInteractable.cs`, `ItemPickupInteractable.cs`, `NoteInteractable.cs`, `PlayerInteractor.cs`, `PlayerHUD.cs`, and `AbandonedSchool.unity`.
3. **Gameplay Loop & School Progression:**
   - Place `NoteInteractable`, `ItemPickupInteractable` (keys/batteries), and `DrawerInteractable` instances across `Classroom_101`, `Classroom_102`, `Teachers_Lounge`, and lock `Classroom_103_Quarantine` / `Elevator_EndHallway` with key progression.
   - Add ambient/horror audio controller honoring `MainMenuManager`'s `TLB_MusicVolume` and `TLB_SFXVolume` PlayerPrefs, plus in-game pause/settings menu for mobile.
4. **Visual & Performance Polish for Android:**
   - Replace blockout primitives progressively with textured/baked lighting (`Mobile` URP or optimized Built-in lightmaps + occlusion culling) while profiling frame rate on target Android devices.
   - Configure app icons (`m_BuildTargetPlatformIcons`) and release keystore signing when approaching distribution.
