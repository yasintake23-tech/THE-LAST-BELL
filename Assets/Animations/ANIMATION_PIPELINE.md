# THE LAST BELL — Modular Character Animation Pipeline & Debug System

## 1. Architecture Overview (7 Modular Animator Layers)

`Protagonist_AnimatorController.controller` (Humanoid, `applyRootMotion = false`, Foot IK enabled on Base Layer) uses 7 clean, independent layers managed by `ProtagonistAnimationController.cs`:

| # | Layer Name | Avatar Mask | Blend Mode | Default Weight | Purpose |
|---|---|---|---|---|---|
| 0 | `Base Layer` | Full Body | Override | `1.0` | 2D Freeform Directional Locomotion (`Idle_Breathing`, `Walk_Slow`, `Walk_Forward`, `Run_Forward`, `Walk_Reverse`, `Strafe_Left`, `Strafe_Right`) + Crouch (`Crouch_Enter`, `Flashlight_CrouchWalk`) + Foot IK |
| 1 | `UpperBody` | `UpperBody_Mask` | Override | `0.0` (auto when Fear > 0.05) | Fear/tension posture (`Flashlight_Fear`) |
| 2 | `RightArm_Flashlight` | `RightArm_Mask` | Override | `1.0` | Flashlight hold/slow/walk/run 1D BlendTree + `Raise`, `Lower`, `Release` |
| 3 | `LeftArm_Interaction` | `LeftArm_Mask` | Override | `0.0` (auto on trigger) | Left-hand environment interactions (`Door_Open_Left`) |
| 4 | `Head_Look` | `HeadNeck_Mask` | Override | `0.0` (auto on trigger/target) | Head look-at & `Look_Right` |
| 5 | `Additive_Reactions` | `UpperBody_Mask` | Additive | `0.0` (auto on fear/flinch) | Additive breathing & impact flinch |
| 6 | `FullBody_Override` | Full Body | Override | `0.0` (auto on trigger/cinematic) | `Fall_Forward`, `Get_Up`, `Stumble_Recover`, `Flashlight_Pickup`, Timeline cutscenes |

## 2. Ground Alignment & Root Motion Policy

Configured automatically by `Assets/Editor/AnimationImportPipeline.cs`:
- **Horizontal XZ & Rotation:** Always baked into pose (`lockRootRotation = true`, `lockRootPositionXZ = true`). `CharacterController` drives all world movement.
- **Standing Locomotion & Upper-Body Clips:** `lockRootHeightY = true`, `heightFromFeet = true`, `keepOriginalPositionY = false`. Guarantees feet rest cleanly at `Y = 0` with zero ground penetration.
- **Vertical Action Clips (`Crouch_Enter`, `Flashlight_CrouchWalk`, `Flashlight_Pickup`, `Stumble_Recover`, `Fall_Forward`, `Get_Up`):** `lockRootHeightY = true`, `heightFromFeet = false`, `keepOriginalPositionY = true`. Preserves intentional downward pelvis/body travel toward the floor.
- **Foot IK:** `ProtagonistAnimationController.OnAnimatorIK(0)` raycasts downward from each foot when grounded and gently lifts feet onto uneven terrain without pulling raised swing feet down.

## 3. Integrated 23-Clip Library (`AnimationLibraryRegistry.asset`)

| Clip ID | Category | Layer | Loop | Ground Y Mode | Events |
|---|---|---|---|---|---|
| `Idle_Breathing` | Locomotion | Base Layer | Yes | Feet | — |
| `Walk_Slow_unity` | Locomotion | Base Layer | Yes | Feet | Footstep (25%, 75%) |
| `Walk_Forward_unity` | Locomotion | Base Layer | Yes | Feet | Footstep (25%, 75%) |
| `Walk_Reverse_unity` | Locomotion | Base Layer | Yes | Feet | Footstep (25%, 75%) |
| `Strafe_Left` | Locomotion | Base Layer | Yes | Feet | Footstep (25%, 75%) |
| `Strafe_Right` | Locomotion | Base Layer | Yes | Feet | Footstep (25%, 75%) |
| `Run_Forward` | Locomotion | Base Layer | Yes | Feet | Footstep (25%, 75%) |
| `Crouch_Enter` | Locomotion | Base Layer | No | Original Y | — |
| `Flashlight_CrouchWalk` | Locomotion | Base Layer | Yes | Original Y | Footstep (25%, 75%) |
| `Flashlight_Fear` | UpperBody | UpperBody | Yes | Feet | — |
| `Look_Right` | UpperBody | Head_Look | No | Feet | — |
| `Flashlight_Hold` | Flashlight | RightArm_Flashlight | Yes | Feet | — |
| `Flashlight_Slow` | Flashlight | RightArm_Flashlight | Yes | Feet | Footstep (25%, 75%) |
| `Flashlight_Walk` | Flashlight | RightArm_Flashlight | Yes | Feet | Footstep (25%, 75%) |
| `Flashlight_Run` | Flashlight | RightArm_Flashlight | Yes | Feet | Footstep (25%, 75%) |
| `Flashlight_Raise` | Flashlight | RightArm_Flashlight | No | Feet | — |
| `Flashlight_Lower` | Flashlight | RightArm_Flashlight | No | Feet | — |
| `Flashlight_Release` | Flashlight | RightArm_Flashlight | No | Feet | FlashlightRelease (55%) |
| `Door_Open_Left` | Interaction | LeftArm_Interaction | No | Feet | DoorInteract (45%) |
| `Flashlight_Pickup` | FullBodyAction | FullBody_Override | No | Original Y | FlashlightGrip + Pickup (55%) |
| `Stumble_Recover` | FullBodyAction | FullBody_Override | No | Original Y | — |
| `Fall_Forward` | FullBodyAction | FullBody_Override | No | Original Y | FallImpact (70%) |
| `Get_Up` | FullBodyAction | FullBody_Override | No | Original Y | — |

## 4. Third-Person Camera & Animation Debug Panel

- **1P / 3P Toggle:** Tap `CAM: 1P` / `CAM: 3P` (top-right, left of Pause) or press `V` on keyboard. Switches seamlessly without reloading the scene or resetting position/animation, avoids walls via `SphereCast`, toggles character mesh `ShadowCastingMode` (`ShadowsOnly` in 1P, `On` in 3P), and automatically moves the flashlight between camera rig and `Flashlight_Hand_Socket`.
- **Animation Debug Panel:** Tap `ANIM` (top-left, below Stamina bar) or press `F8`. Supports **Live Gameplay** and **Isolated Preview** (`PlayableGraph` with Play/Pause, Restart, Loop, Speed `0.25x–2.0x`, Timeline scrubber, Prev/Next, scrollable 23-clip list, 7-layer weight overrides, action triggers, and real-time `HipsY / LFootY / RFootY` ground telemetry).
- **Production Builds:** Uncheck `enableDebugTools` on `AnimationDebugPanel` in `Gameplay_HUD_Canvas` to cleanly disable debug UI for release builds.

## 5. Adding Future Animations

1. Place the new Humanoid FBX under `Assets/Animations/Locomotion/` (or a subfolder of `Assets/Animations/`).
2. Add its `ClipSpec` entry to `ProtagonistAnimationSetup.AllClipSpecs` and loop/ground rules (if needed) to `AnimationImportPipeline`.
3. Run menu **`The Last Bell > Animasyon > Modüler 7 Katmanlı Animasyon Sistemini Kur`** (or CI build). It automatically imports the clip, updates `AnimationLibraryRegistry.asset`, and registers it in the Debug Panel.
