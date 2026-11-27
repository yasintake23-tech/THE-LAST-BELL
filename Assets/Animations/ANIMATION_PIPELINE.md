# THE LAST BELL — Character Animation Pipeline & Architecture

## 1. Folder Organization (`Assets/Animations/`)

| Folder | Purpose | Auto-Import Rule (`AnimationImportPipeline.cs`) |
| --- | --- | --- |
| `Locomotion/` | Walk, run, sprint, crouch, strafe, backward loops | Humanoid, `loopTime = true`, `loopPose = true`, root motion baked into pose |
| `UpperBody/` | Breathing, alert/fear idle, upper torso turns | Humanoid, `loopTime = true`, `loopPose = true`, root motion baked |
| `Hands/` | Flashlight grip, aim, release, empty hand poses | Humanoid, `loopTime = false`, root motion baked |
| `Interactions/` | Door open, drawer pull, item pickup, switch press | Humanoid, `loopTime = false`, root motion baked |
| `Reactions/` | Impact flinch, fall, get-up, horror startle | Humanoid, `loopTime = false`, root motion baked |
| `Cinematics/` | Scripted narrative / Timeline cutscenes | Humanoid, `loopTime = false`, root motion preserved |
| `Additive/` | Trembling, heavy breathing, subtle idle sway | Humanoid, `loopTime = true`, `loopPose = true`, root motion baked |
| `Controllers/` | Future override / NPC / cutscene AnimatorControllers | — |
| `Masks/` | `UpperBody_Mask`, `RightArm_Mask`, `LeftArm_Mask`, `HeadNeck_Mask` | — |

## 2. Multi-Layer Animator Architecture (`Protagonist_AnimatorController`)

Managed at runtime by `ProtagonistAnimationController` (`Assets/Scripts/Player/ProtagonistAnimationController.cs`):

0. **`Base Layer`** (Weight `1.0`, IKPass `true`):
   - 2D Freeform Directional `Locomotion` BlendTree driven by `Strafe` (`[-1..1]`) and `Speed` (`[-1..3]`), with `LocomotionSpeedMultiplier` matching playback speed to actual `CharacterController` velocity.
   - Dedicated directional slots: `(0, 1)` uses `Walk_Forward_unity.fbx`; `(0, -1)` backward, `(-1, 0)` strafe left, `(1, 0)` strafe right, `(0, 2)` run, `(0, 3)` sprint remain separate slots.
1. **`UpperBody`** (`UpperBody_Mask`, Override): Breathing & fear/alertness (`FearLevel`).
2. **`RightArm_Flashlight`** (`RightArm_Mask`, Override, IKPass `true`): Flashlight hold/aim/empty states + subtle aim IK.
3. **`LeftArm_Interaction`** (`LeftArm_Mask`, Override, IKPass `true`): `Door_Open`, `Item_PickUp`, `Env_Interact` triggers with left-hand IK reach.
4. **`Head_Look`** (`HeadNeck_Mask`, Override, IKPass `true`): Camera/target `LookAt` IK (`SetLookTarget(Transform)`).
5. **`Additive_Reactions`** (`UpperBody_Mask`, Additive): Breathing & `ImpactFlinch` reactions.
6. **`FullBody_Override`** (Full Body, Override): `Fall`, `GetUp`, and `Cinematic_Override` (Timeline/PlayableDirector); automatically suppresses upper-body/arm layers when active.

## 3. Adding Future FBX Animations (Zero Refactor Workflow)

1. Drop the new `.fbx` file (Mixamo, Universal Animation Library, AI mocap, or procedural export) into the appropriate `Assets/Animations/<Category>/` folder.
2. `AnimationImportPipeline` (`AssetPostprocessor`) automatically configures **Humanoid retargeting**, disables duplicate material imports, and applies the folder's loop & root-motion baking rules.
3. Assign the imported `AnimationClip` to its target state or BlendTree slot in `Protagonist_AnimatorController` (or drive it via a `PlayableDirector` Timeline track using `SetCinematicOverride(true, director)`).
4. For physical hand/world item transitions, attach `FlashlightPhysicalItem` (or extend `IInteractable`) and hook Animation Events: `AnimEvent_FlashlightGrip`, `AnimEvent_FlashlightRelease`, `AnimEvent_DoorInteract`, `AnimEvent_Pickup`, `AnimEvent_FallImpact`.
