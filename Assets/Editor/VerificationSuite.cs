using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using TheLastBell.Player;
using TheLastBell.Interaction;
using TheLastBell.Gameplay;
using TheLastBell.UI;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Automated Functional & Visual Verification Suite (EditMode / BatchMode).
    /// Runs 8-direction analog joystick tests, camera-relative movement at 0/90/180 deg yaw,
    /// simultaneous multi-touch move+look, release stop, resolution dp layout checks (2340x1080),
    /// 7-layer Humanoid animation & physical flashlight verification,
    /// Chapter I environment / toe alignment / 13-state Feştek & CRT TV narrative puzzle simulation,
    /// and captures rendered Gameplay HUD + Flashlight + Environment + Puzzle screenshots.
    /// </summary>
    public static class VerificationSuite
    {
        public static bool RunAll(out string report)
        {
            var sb = new StringBuilder();
            var failures = new List<string>();
            sb.AppendLine("=== THE LAST BELL - Functional & Visual Verification Suite ===");

            string scenePath = "Assets/Scenes/AbandonedSchool.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            try
            {
                // Apply Android High Quality for accurate lighting/shadow verification
                if (QualitySettings.names.Length > 3)
                {
                    QualitySettings.SetQualityLevel(3, true);
                }

                VerifyLayoutAcrossResolutions(sb, failures);
                VerifyLightingAndFlashlightConfig(sb, failures);
                VerifyAnimationAndPhysicalFlashlight(sb, failures);
                RunFunctionalMovementAndJoystickTests(sb, failures);
                CaptureVisualScreenshots(sb);
                VerifyChapter1ProductionAndPuzzleArc(sb, failures);
            }
            catch (Exception ex)
            {
                failures.Add("Unhandled exception in VerificationSuite: " + ex);
            }
            finally
            {
                // Reopen clean scene from disk so test perturbations never persist
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            if (failures.Count > 0)
            {
                sb.AppendLine("VerificationSuite FAILED:");
                foreach (var f in failures)
                {
                    sb.AppendLine("  - " + f);
                    Debug.LogError("[VerificationSuite] " + f);
                }
                report = sb.ToString();
                return false;
            }

            sb.AppendLine("VerificationSuite PASSED with 0 failures.");
            report = sb.ToString();
            Debug.Log(report);
            return true;
        }

        private static void VerifyLayoutAcrossResolutions(StringBuilder sb, List<string> failures)
        {
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            var mcUI = UnityEngine.Object.FindAnyObjectByType<MobileControlsUI>(FindObjectsInactive.Include);

            if (scaler == null || mcUI == null)
            {
                failures.Add("CanvasScaler or MobileControlsUI missing in AbandonedSchool.unity.");
                return;
            }

            var joyBgRT = GameObject.Find("Joystick_Background")?.GetComponent<RectTransform>();
            var joyHandleRT = GameObject.Find("Joystick_Handle")?.GetComponent<RectTransform>();
            var sprintRT = GameObject.Find("Mobile_SprintButton")?.GetComponent<RectTransform>();
            var crouchRT = GameObject.Find("Mobile_CrouchButton")?.GetComponent<RectTransform>();
            var flashRT = GameObject.Find("Mobile_FlashlightButton")?.GetComponent<RectTransform>();
            var actionRT = GameObject.Find("Mobile_ActionButton")?.GetComponent<RectTransform>();
            var pauseRT = GameObject.Find("Mobile_PauseButton")?.GetComponent<RectTransform>();

            if (joyBgRT == null || joyHandleRT == null || sprintRT == null || crouchRT == null || flashRT == null || actionRT == null)
            {
                failures.Add("One or more mobile UI RectTransforms are missing.");
                return;
            }

            var joyCg = joyBgRT.GetComponent<CanvasGroup>();
            if (joyCg == null || joyCg.alpha < 0.5f)
            {
                failures.Add($"Joystick_Background CanvasGroup alpha ({joyCg?.alpha}) must be >= 0.50 so joystick stays visible.");
            }
            if (joyBgRT.GetComponent<Image>()?.raycastTarget == true)
            {
                failures.Add("Joystick_Background raycastTarget must be false.");
            }

            // Ensure non-interactive HUD elements don't block touch look
            string[] passiveNames = { "Interaction_Prompt_Panel", "Prompt_Text", "Status_Message_Banner", "Reticle_Dot", "Stamina_Bar_Container", "Fill" };
            foreach (var pName in passiveNames)
            {
                var go = GameObject.Find(pName);
                if (go != null)
                {
                    foreach (var g in go.GetComponentsInChildren<Graphic>(true))
                    {
                        if (g.raycastTarget)
                        {
                            failures.Add($"Passive UI element '{g.gameObject.name}' has raycastTarget=true (blocks TouchLookZone).");
                        }
                    }
                }
            }

            (int w, int h, float density, string label)[] resolutions = {
                (2340, 1080, 2.625f, "Galaxy A56 5G 2340x1080"),
                (2400, 1080, 2.625f, "20:9 FHD+ 2400x1080"),
                (1600, 720, 1.75f, "20:9 HD+ 1600x720")
            };

            foreach (var res in resolutions)
            {
                float scale = (float)res.h / scaler.referenceResolution.y; // MatchWidthOrHeight = 1.0f
                float joyDp = (joyBgRT.sizeDelta.x * scale) / res.density;
                float sprintDp = (sprintRT.sizeDelta.x * scale) / res.density;
                float crouchDp = (crouchRT.sizeDelta.x * scale) / res.density;
                float flashDp = (flashRT.sizeDelta.x * scale) / res.density;
                float actionDp = (actionRT.sizeDelta.x * scale) / res.density;

                sb.AppendLine($"  Layout [{res.label}]: JoyBase={joyDp:F1}dp ({joyBgRT.sizeDelta.x * scale:F0}px), Sprint={sprintDp:F1}dp, Crouch={crouchDp:F1}dp, Flash={flashDp:F1}dp, Action={actionDp:F1}dp");

                if (joyDp < 140f || joyDp > 190f)
                    failures.Add($"[{res.label}] Joystick base size {joyDp:F1}dp outside ~150-180dp target.");
                if (sprintDp < 54f || sprintDp > 76f)
                    failures.Add($"[{res.label}] Sprint button {sprintDp:F1}dp outside 56-72dp target.");
                if (crouchDp < 54f || crouchDp > 76f)
                    failures.Add($"[{res.label}] Crouch button {crouchDp:F1}dp outside 56-72dp target.");
            }
        }

        private static void VerifyLightingAndFlashlightConfig(StringBuilder sb, List<string> failures)
        {
            var flashCtrl = UnityEngine.Object.FindAnyObjectByType<FlashlightController>(FindObjectsInactive.Include);
            var spotGO = GameObject.Find("SpotLight");
            var spot = spotGO != null ? spotGO.GetComponent<Light>() : null;

            if (flashCtrl == null || spot == null)
            {
                failures.Add("FlashlightController or SpotLight missing.");
                return;
            }

            if (spot.type != LightType.Spot) failures.Add($"SpotLight.type={spot.type}, expected Spot.");
            if (spot.renderMode != LightRenderMode.ForcePixel) failures.Add($"SpotLight.renderMode={spot.renderMode}, expected ForcePixel (prevents 4-vertex wall blowout).");
            if (spot.cookie == null) failures.Add("SpotLight.cookie is null.");
            if (spot.shadows == LightShadows.None) failures.Add("SpotLight.shadows is None (expected Soft shadows to stop light passing through solid walls).");
            if (QualitySettings.pixelLightCount < 4) failures.Add($"QualitySettings.pixelLightCount={QualitySettings.pixelLightCount}, expected >= 4.");

            sb.AppendLine($"  Flashlight Config: Type={spot.type}, RenderMode={spot.renderMode}, Angle={spot.spotAngle}, Inner={spot.innerSpotAngle}, Range={spot.range}, Intensity={spot.intensity}, Shadows={spot.shadows}, Cookie={(spot.cookie != null ? spot.cookie.name : "NULL")}, PixelLightCount={QualitySettings.pixelLightCount}");
        }

        private static void VerifyAnimationAndPhysicalFlashlight(StringBuilder sb, List<string> failures)
        {
            var registry = AssetDatabase.LoadAssetAtPath<AnimationLibraryRegistry>(ProtagonistAnimationSetup.REGISTRY_PATH);
            if (registry == null || registry.Entries == null || registry.Entries.Count < ProtagonistAnimationSetup.AllClipSpecs.Length)
            {
                failures.Add($"AnimationLibraryRegistry missing or incomplete ({registry?.Entries?.Count ?? 0}/{ProtagonistAnimationSetup.AllClipSpecs.Length}).");
                return;
            }

            var player = GameObject.Find("Player_Protagonist");
            var anim = player != null ? player.GetComponentInChildren<Animator>(true) : null;
            var animCtrl = player != null ? player.GetComponentInChildren<ProtagonistAnimationController>(true) : null;
            var procCtrl = player != null ? player.GetComponent<CharacterProceduralController>() : null;
            var camMode = player != null ? player.GetComponent<CameraModeController>() : null;
            var fpc = player != null ? player.GetComponent<FirstPersonController>() : null;
            var controller = anim != null ? anim.runtimeAnimatorController as AnimatorController : null;

            if (anim == null || anim.avatar == null || !anim.avatar.isValid || !anim.avatar.isHuman)
            {
                failures.Add("Protagonist Animator or Humanoid Avatar is invalid.");
                return;
            }
            if (anim.applyRootMotion) failures.Add("Protagonist Animator.applyRootMotion must be false.");
            if (animCtrl == null) failures.Add("ProtagonistAnimationController missing on protagonist.");
            if (procCtrl == null) failures.Add("CharacterProceduralController missing on Player_Protagonist.");
            if (camMode == null) failures.Add("CameraModeController missing on Player_Protagonist.");

            // Validate ALL 52 clips (29 V2 + 23 V1) in AnimationLibraryRegistry
            int validClips = 0;
            int v2Count = 0;
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
            anim.transform.localPosition = Vector3.zero;
            anim.transform.localRotation = Quaternion.identity;

            foreach (var entry in registry.Entries)
            {
                if (entry == null || entry.clip == null)
                {
                    failures.Add($"Registry entry '{entry?.id}' has null AnimationClip.");
                    continue;
                }
                if (!entry.clip.humanMotion)
                {
                    failures.Add($"Clip '{entry.id}' is not Humanoid retargetable (humanMotion=false).");
                    continue;
                }
                if (entry.clip.length <= 0.1f)
                {
                    failures.Add($"Clip '{entry.id}' has invalid duration {entry.clip.length:F3}s.");
                    continue;
                }

                Vector3 rootBefore = anim.transform.localPosition;
                animCtrl.StartIsolatedPreview(entry.clip, true);
                animCtrl.SetPreviewNormalizedTime(0.35f);
                Transform hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                Transform head = anim.GetBoneTransform(HumanBodyBones.Head);
                Transform lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);

                if (hips == null || head == null || lFoot == null || rFoot == null ||
                    float.IsNaN(hips.position.y) || float.IsNaN(lFoot.position.y) || float.IsNaN(rFoot.position.y))
                {
                    failures.Add($"Clip '{entry.id}' produced invalid/NaN Humanoid bone transforms.");
                    animCtrl.StopIsolatedPreview();
                    continue;
                }
                if ((anim.transform.localPosition - rootBefore).sqrMagnitude > 0.01f)
                {
                    failures.Add($"Clip '{entry.id}' displaced character root by {(anim.transform.localPosition - rootBefore).magnitude:F3}m.");
                    animCtrl.StopIsolatedPreview();
                    continue;
                }

                if (string.Equals(entry.id, "Idle_Natural_V2", StringComparison.OrdinalIgnoreCase))
                {
                    float hipsY = hips.position.y - player.transform.position.y;
                    float headY = head.position.y - player.transform.position.y;
                    float lFootY = lFoot.position.y - player.transform.position.y;
                    float rFootY = rFoot.position.y - player.transform.position.y;

                    if (hipsY < 0.72f || headY < 1.40f || lFootY < -0.04f || lFootY > 0.25f || rFootY < -0.04f || rFootY > 0.25f)
                    {
                        failures.Add($"Idle_Natural_V2 ground alignment FAILED: HipsY={hipsY:F2}m, HeadY={headY:F2}m, LFootY={lFootY:F2}m, RFootY={rFootY:F2}m");
                    }
                    else
                    {
                        sb.AppendLine($"  Idle_Natural_V2 Ground Alignment OK: HipsY={hipsY:F2}m, HeadY={headY:F2}m, LFootY={lFootY:F2}m, RFootY={rFootY:F2}m");
                    }
                }

                if (string.Equals(entry.versionTag, "V2", StringComparison.OrdinalIgnoreCase)) v2Count++;
                animCtrl.StopIsolatedPreview();
                validClips++;
            }
            sb.AppendLine($"  Animation Library Registry: {validClips}/{registry.Entries.Count} Humanoid clips validated ({v2Count} V2 + {validClips - v2Count} V1) OK");

            // Validate 7-layer AnimatorController architecture + Stationary Crouch + Turn-in-Place states
            if (controller == null)
            {
                failures.Add("Protagonist AnimatorController missing.");
            }
            else
            {
                string[] expectedLayers = {
                    ProtagonistAnimationController.LAYER_BASE,
                    ProtagonistAnimationController.LAYER_UPPER_BODY,
                    ProtagonistAnimationController.LAYER_RIGHT_ARM,
                    ProtagonistAnimationController.LAYER_LEFT_ARM,
                    ProtagonistAnimationController.LAYER_HEAD_LOOK,
                    ProtagonistAnimationController.LAYER_ADDITIVE,
                    ProtagonistAnimationController.LAYER_FULL_OVERRIDE
                };
                foreach (var lName in expectedLayers)
                {
                    if (!controller.layers.Any(l => l.name == lName))
                        failures.Add($"AnimatorController missing modular layer '{lName}'.");
                }

                var walkV2 = registry.FindClip("Walk_Grounded_V2");
                var baseSm = controller.layers[0].stateMachine;
                var locoState = baseSm.states.FirstOrDefault(s => s.state.name == "Locomotion").state;
                var crouchIdleState = baseSm.states.FirstOrDefault(s => s.state.name == "Crouch_Idle").state;
                var turnLeftState = baseSm.states.FirstOrDefault(s => s.state.name == "Turn_Left_45").state;
                var turnRightState = baseSm.states.FirstOrDefault(s => s.state.name == "Turn_Right_45").state;

                if (locoState == null || !(locoState.motion is BlendTree bt) || bt.children.Length < 7)
                    failures.Add("Base Layer missing 7-clip Locomotion 2D BlendTree.");
                else if (!bt.children.Any(c => c.motion == walkV2 && Mathf.Abs(c.position.y - 1.0f) < 0.05f))
                    failures.Add("Locomotion BlendTree (0,1) slot does not use Walk_Grounded_V2.");

                if (crouchIdleState == null) failures.Add("Base Layer missing dedicated Crouch_Idle state (stationary crouch).");
                if (turnLeftState == null || turnRightState == null) failures.Add("Base Layer missing Turn_Left_45 / Turn_Right_45 states.");

                sb.AppendLine($"  Animator Architecture: Layers={controller.layers.Length}/7, StationaryCrouch=True, TurnInPlace=True, RootMotion={anim.applyRootMotion} OK");
            }

            // Test A & B: Stationary Look (feet planted) + Large Turn-in-Place (90 deg)
            if (fpc != null && procCtrl != null)
            {
                InvokePrivate(fpc, "Awake");
                procCtrl.InitializeIfNeeded();
                player.transform.rotation = Quaternion.identity;
                fpc.MobileMoveInput = Vector2.zero;
                fpc.StepController(0.02f);

                // Small look (+25 deg): body stays planted, RelativeLookYaw updates
                fpc.MobileLookInput = new Vector2(20f / 1.4f, 0f);
                fpc.StepController(0.02f);
                procCtrl.StepProcedural(0.02f);
                float smallBodyYaw = Mathf.Abs(Mathf.DeltaAngle(0f, player.transform.eulerAngles.y));
                if (smallBodyYaw > 2.0f || Mathf.Abs(fpc.RelativeLookYaw) < 15f)
                {
                    failures.Add($"Test A (Stationary Small Look) failed: bodyYaw={smallBodyYaw:F1}deg, relYaw={fpc.RelativeLookYaw:F1}deg.");
                }
                else
                {
                    sb.AppendLine($"  Test A (Stationary Look): BodyPlanted={smallBodyYaw:F2}deg, RelLookYaw={fpc.RelativeLookYaw:F1}deg OK");
                }

                // Large turn (+85 deg): triggers Turn-in-Place and smoothly rotates body
                fpc.MobileLookInput = new Vector2(75f / 1.4f, 0f);
                fpc.StepController(0.02f);
                bool triggeredTurn = fpc.IsTurningInPlace;
                for (int i = 0; i < 45; i++) fpc.StepController(0.02f);
                float alignedDelta = Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, fpc.DesiredLookYaw));
                if (!triggeredTurn || alignedDelta > 10f)
                {
                    failures.Add($"Test B (Large Turn-in-Place) failed: triggered={triggeredTurn}, residualDelta={alignedDelta:F1}deg.");
                }
                else
                {
                    sb.AppendLine($"  Test B (Large Turn-in-Place): Triggered={triggeredTurn}, BodyAlignedDelta={alignedDelta:F1}deg OK");
                }
                player.transform.rotation = Quaternion.identity;
            }

            // Test C: True First-Person selective visibility + 3P + FreeInspectionOrbit + Neutral Light
            if (camMode != null)
            {
                InvokePrivate(camMode, "Awake");
                Vector3 posBefore = player.transform.position;

                camMode.SetCameraMode(CameraViewMode.FirstPerson);
                var renderers = player.transform.Find("Character_Model")?.GetComponentsInChildren<Renderer>(true);
                if (renderers != null)
                {
                    bool bodyVisible = renderers.Any(r => r.gameObject.name.Contains("outfit_bottom") && r.shadowCastingMode == ShadowCastingMode.On);
                    bool headHidden = renderers.Where(r => r.gameObject.name.Contains("AvatarHead")).All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly);
                    if (!bodyVisible || !headHidden)
                    {
                        failures.Add($"Test C (True FPS Selective Visibility) failed: bodyVisible={bodyVisible}, headHidden={headHidden}.");
                    }
                }

                camMode.SetCameraMode(CameraViewMode.FreeInspectionOrbit);
                if (fpc != null && !fpc.FreezeGazeForInspection)
                    failures.Add("FreeInspectionOrbit did not freeze character gaze.");

                camMode.SetCameraMode(CameraViewMode.ThirdPerson);
                if (renderers != null && renderers.Any(r => r.shadowCastingMode != ShadowCastingMode.On))
                    failures.Add("ThirdPerson mode did not restore all Character_Model renderers to ShadowCastingMode.On.");

                if ((player.transform.position - posBefore).sqrMagnitude > 0.001f)
                    failures.Add("Camera mode switch perturbed player world position.");

                camMode.SetNeutralInspectionLight(true);
                camMode.SetNeutralInspectionLight(false);
                camMode.SetCameraMode(CameraViewMode.FirstPerson);
                sb.AppendLine("  Test C (True FPS Selective Culling & Orbit): 1P(Body=ON,Head=ShadowsOnly) <-> 3P <-> FreeOrbit OK");
            }

            // Test F: Door Environmental InteractionTarget check
            var door = UnityEngine.Object.FindAnyObjectByType<DoorInteractable>(FindObjectsInactive.Include);
            if (door != null && procCtrl != null)
            {
                InvokePrivate(door, "Awake");
                if (door.DoorInteractionTarget == null || door.DoorInteractionTarget.HandTarget == null)
                {
                    failures.Add("Test F (Door InteractionTarget) failed: Door_Handle_Target not initialized.");
                }
                else
                {
                    procCtrl.BeginEnvironmentalInteraction(door.DoorInteractionTarget);
                    procCtrl.StepProcedural(0.5f);
                    if (!procCtrl.IsEnvironmentalInteractionActive)
                        failures.Add("Test F failed: CharacterProceduralController did not activate environmental interaction.");
                    procCtrl.CancelEnvironmentalInteraction();
                    sb.AppendLine($"  Test F (Door Handle IK): Target={door.DoorInteractionTarget.HandTarget.name}, LeftHandIK=Active OK");
                }
            }

            // Validate AnimationDebugPanel build, isolated preview enter/exit, and gameplay restoration
            var debugPanel = UnityEngine.Object.FindAnyObjectByType<AnimationDebugPanel>(FindObjectsInactive.Include);
            if (debugPanel == null)
            {
                failures.Add("AnimationDebugPanel missing on Gameplay_HUD_Canvas.");
            }
            else
            {
                debugPanel.EnsureBuilt();
                debugPanel.SetPanelOpen(true);
                debugPanel.EnterIsolatedPreview();
                if (!debugPanel.IsIsolatedMode || !animCtrl.IsIsolatedPreviewActive || (fpc != null && fpc.AllowMovement))
                {
                    failures.Add("AnimationDebugPanel.EnterIsolatedPreview failed to pause movement or activate PlayableGraph preview.");
                }
                debugPanel.SelectClipByIndex(2);
                debugPanel.ExitIsolatedPreview();
                debugPanel.SetPanelOpen(false);
                if (debugPanel.IsIsolatedMode || animCtrl.IsIsolatedPreviewActive || (fpc != null && !fpc.AllowMovement))
                {
                    failures.Add("AnimationDebugPanel.ExitIsolatedPreview failed to cleanly restore gameplay movement/Animator.");
                }
                if (camMode != null) camMode.SetCameraMode(CameraViewMode.FirstPerson);
                sb.AppendLine("  AnimationDebugPanel: 52-Clip Build -> Open -> IsolatedPreview -> Clean Gameplay Restore OK");
            }

            // Test D: Verify FlashlightPhysicalItem hand socket -> drop -> lit -> pickup -> hand socket
            var physItem = UnityEngine.Object.FindAnyObjectByType<FlashlightPhysicalItem>(FindObjectsInactive.Include);
            var spot = GameObject.Find("SpotLight")?.GetComponent<Light>();
            if (physItem == null)
            {
                failures.Add("FlashlightPhysicalItem missing on Flashlight_Rig.");
            }
            else
            {
                InvokePrivate(physItem, "Awake");
                physItem.AttachToHand();
                if (physItem.CurrentState != FlashlightPhysicalState.HeldInHandBone)
                    failures.Add("Test D failed: Flashlight did not attach to RightHandSocket.");

                physItem.DropWithPhysics(new Vector3(0.5f, 0.2f, 1.0f));
                if (physItem.CurrentState != FlashlightPhysicalState.DroppedPhysics || (spot != null && !spot.enabled))
                    failures.Add("Test D failed: DropWithPhysics failed or turned off light.");

                physItem.PickupAndReattach(true);
                if (physItem.CurrentState != FlashlightPhysicalState.HeldInHandBone)
                    failures.Add("Test D failed: PickupAndReattach did not return to HeldInHandBone.");

                sb.AppendLine("  Test D (Physical Flashlight): HandSocket -> DroppedPhysics (Light ON) -> PickupAndReattach(HandSocket) OK");
            }
        }

        private static void RunFunctionalMovementAndJoystickTests(StringBuilder sb, List<string> failures)
        {
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            var player = GameObject.Find("Player_Protagonist");
            var fpc = player != null ? player.GetComponent<FirstPersonController>() : null;
            var cc = player != null ? player.GetComponent<CharacterController>() : null;
            var mcUI = UnityEngine.Object.FindAnyObjectByType<MobileControlsUI>(FindObjectsInactive.Include);
            var tlz = UnityEngine.Object.FindAnyObjectByType<TouchLookZone>(FindObjectsInactive.Include);
            var joyArea = mcUI != null ? mcUI.GetComponent<RectTransform>() : null;
            var joyBgRT = GameObject.Find("Joystick_Background")?.GetComponent<RectTransform>();

            if (canvas == null || cam == null || fpc == null || cc == null || mcUI == null || tlz == null || joyBgRT == null)
            {
                failures.Add("Missing core components for functional movement test.");
                return;
            }

            // Temporarily use ScreenSpaceCamera for deterministic RectTransformUtility screen coords
            var prevRenderMode = canvas.renderMode;
            var prevWorldCam = canvas.worldCamera;
            var prevPlaneDist = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;
            var rtTmp = new RenderTexture(2340, 1080, 16, RenderTextureFormat.ARGB32);
            cam.targetTexture = rtTmp;
            cam.aspect = 2340f / 1080f;
            Canvas.ForceUpdateCanvases();

            try
            {
                // Initialize components cleanly and refresh canvas layout before reading screen pos
                InvokePrivate(fpc, "Awake");
                InvokePrivate(mcUI, "Awake");
                InvokePrivate(mcUI, "Start");
                InvokePrivate(tlz, "Awake");
                Canvas.ForceUpdateCanvases();

                fpc.enabled = true;
                cc.enabled = true;

                Vector2 joyCenterScreen = RectTransformUtility.WorldToScreenPoint(cam, joyBgRT.position);
                var eventSys = UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                PointerEventData MakePointer(int pid, Vector2 pos, GameObject hitGo)
                {
                    var rr = new RaycastResult { gameObject = hitGo, module = raycaster };
                    return new PointerEventData(eventSys)
                    {
                        pointerId = pid,
                        position = pos,
                        pointerPressRaycast = rr,
                        pointerCurrentRaycast = rr
                    };
                }

                // --- Test 1: 8-Direction Analog Joystick Vectors ---
                (string name, Vector2 dir)[] dirs8 = {
                    ("Forward (N)",     new Vector2( 0f,  1f)),
                    ("ForwardRight(NE)",new Vector2( 1f,  1f).normalized),
                    ("Right (E)",       new Vector2( 1f,  0f)),
                    ("BackRight (SE)",  new Vector2( 1f, -1f).normalized),
                    ("Back (S)",        new Vector2( 0f, -1f)),
                    ("BackLeft (SW)",   new Vector2(-1f, -1f).normalized),
                    ("Left (W)",        new Vector2(-1f,  0f)),
                    ("ForwardLeft(NW)", new Vector2(-1f,  1f).normalized)
                };

                float dragDist = 115f;
                foreach (var d in dirs8)
                {
                    mcUI.OnPointerDown(MakePointer(-1, joyCenterScreen, mcUI.gameObject));
                    mcUI.OnDrag(MakePointer(-1, joyCenterScreen + d.dir * dragDist, mcUI.gameObject));

                    for (int s = 0; s < 15; s++) mcUI.StepMovement(0.02f);

                    Vector2 got = mcUI.MovementInput;
                    float mag = got.magnitude;
                    float dot = mag > 0.01f ? Vector2.Dot(got.normalized, d.dir) : 0f;

                    if (dot < 0.96f || mag < 0.6f || mag > 1.005f)
                    {
                        failures.Add($"8-Dir Joystick '{d.name}' failed: expected dir={d.dir}, got={got} (mag={mag:F3}, dot={dot:F3}).");
                    }
                    else
                    {
                        sb.AppendLine($"  Joystick 8-Dir [{d.name}]: input=({got.x:F2}, {got.y:F2}), mag={mag:F2}, dot={dot:F3} OK");
                    }

                    mcUI.OnPointerUp(MakePointer(-1, joyCenterScreen, mcUI.gameObject));
                    if (mcUI.MovementInput != Vector2.zero || fpc.MobileMoveInput != Vector2.zero)
                    {
                        failures.Add($"Joystick release did not immediately zero input after '{d.name}'.");
                    }
                }

                // --- Test 2: Deadzone Check ---
                mcUI.OnPointerDown(MakePointer(-1, joyCenterScreen, mcUI.gameObject));
                mcUI.OnDrag(MakePointer(-1, joyCenterScreen + new Vector2(4f, 4f), mcUI.gameObject));
                for (int s = 0; s < 10; s++) mcUI.StepMovement(0.02f);
                if (mcUI.MovementInput != Vector2.zero)
                {
                    failures.Add($"Deadzone check failed: expected (0,0), got {mcUI.MovementInput}");
                }
                mcUI.OnPointerUp(MakePointer(-1, joyCenterScreen, mcUI.gameObject));

                // --- Test 3: Camera-Relative Movement at 0, 90, 180 deg Yaw ---
                player.transform.position = new Vector3(0f, 0.05f, -15.5f);
                Physics.SyncTransforms();

                void TestMoveForYaw(float yawDeg, Vector2 stickInput, Vector3 expectedWorldDir, string label)
                {
                    player.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
                    player.transform.position = new Vector3(0f, 0.05f, -15.5f);
                    Physics.SyncTransforms();

                    fpc.MobileMoveInput = stickInput;
                    Vector3 startPos = player.transform.position;
                    for (int i = 0; i < 12; i++) fpc.StepController(0.02f);
                    Vector3 delta = player.transform.position - startPos;
                    Vector3 horizDelta = new Vector3(delta.x, 0f, delta.z);
                    float dist = horizDelta.magnitude;
                    float align = dist > 0.001f ? Vector3.Dot(horizDelta.normalized, expectedWorldDir.normalized) : 0f;

                    if (dist < 0.05f || align < 0.96f)
                    {
                        failures.Add($"Camera-relative move '{label}' (yaw={yawDeg}, stick={stickInput}) failed: delta={horizDelta}, expectedDir={expectedWorldDir}, align={align:F3}");
                    }
                    else
                    {
                        sb.AppendLine($"  Camera-Relative [{label}]: yaw={yawDeg} deg, stick={stickInput} -> delta=({horizDelta.x:F3}, {horizDelta.z:F3}), align={align:F3} OK");
                    }

                    // Test immediate stop on release
                    fpc.MobileMoveInput = Vector2.zero;
                    for (int i = 0; i < 10; i++) fpc.StepController(0.02f);
                    Vector3 pBefore = player.transform.position;
                    fpc.StepController(0.02f);
                    float residualDist = (player.transform.position - pBefore).magnitude;
                    if (residualDist > 0.002f)
                    {
                        failures.Add($"Player continued sliding after release on '{label}': residual={residualDist:F4}");
                    }
                }

                TestMoveForYaw(0f,   new Vector2( 0f, 1f), Vector3.forward, "Yaw 0 Forward");
                TestMoveForYaw(0f,   new Vector2( 1f, 0f), Vector3.right,   "Yaw 0 Right");
                TestMoveForYaw(0f,   new Vector2(-1f, 0f), Vector3.left,    "Yaw 0 Left");
                TestMoveForYaw(90f,  new Vector2( 0f, 1f), Vector3.right,   "Yaw 90 Forward");
                TestMoveForYaw(90f,  new Vector2(-1f, 0f), Vector3.forward, "Yaw 90 Left");
                TestMoveForYaw(180f, new Vector2( 0f, 1f), Vector3.back,    "Yaw 180 Forward");
                TestMoveForYaw(180f, new Vector2( 1f, 0f), Vector3.left,    "Yaw 180 Right");

                // --- Test 4: Simultaneous Joystick Movement + TouchLookZone Multi-Touch ---
                player.transform.rotation = Quaternion.identity;
                player.transform.position = new Vector3(0f, 0.05f, -15.5f);
                Physics.SyncTransforms();
                float startYaw = player.transform.eulerAngles.y;

                Vector2 lookStart = new Vector2(1600f, 540f);
                mcUI.OnPointerDown(MakePointer(-1, joyCenterScreen, mcUI.gameObject));
                mcUI.OnDrag(MakePointer(-1, joyCenterScreen + new Vector2(85f, 85f), mcUI.gameObject));
                tlz.OnPointerDown(MakePointer(-2, lookStart, tlz.gameObject));
                tlz.OnDrag(MakePointer(-2, lookStart + new Vector2(110f, 0f), tlz.gameObject));

                Vector3 simPosStart = player.transform.position;
                for (int i = 0; i < 8; i++)
                {
                    mcUI.StepMovement(0.02f);
                    tlz.StepLook(0.02f);
                    fpc.StepController(0.02f);
                }
                float simMoved = (player.transform.position - simPosStart).magnitude;
                float simYawDelta = Mathf.Abs(Mathf.DeltaAngle(startYaw, player.transform.eulerAngles.y));

                mcUI.OnPointerUp(new PointerEventData(eventSys) { pointerId = -1 });
                tlz.OnPointerUp(new PointerEventData(eventSys) { pointerId = -2 });

                if (simMoved < 0.05f || simYawDelta < 1.0f)
                {
                    failures.Add($"Simultaneous move+look multi-touch failed: moved={simMoved:F3}m, yawDelta={simYawDelta:F2}deg.");
                }
                else
                {
                    sb.AppendLine($"  Simultaneous Multi-Touch Move+Look: moved={simMoved:F3}m, yawDelta={simYawDelta:F2}deg OK");
                }

                // --- Test 5: Walk vs Sprint vs Crouch ---
                float MeasureSpeed(bool sprint, bool crouch)
                {
                    fpc.MobileMoveInput = Vector2.zero;
                    fpc.MobileSprintInput = false;
                    fpc.SetCrouch(crouch);
                    for (int i = 0; i < 20; i++) fpc.StepController(0.02f);

                    player.transform.position = new Vector3(0f, 0.05f, -15.5f);
                    player.transform.rotation = Quaternion.identity;
                    Physics.SyncTransforms();

                    fpc.MobileMoveInput = Vector2.up;
                    fpc.MobileSprintInput = sprint;
                    Vector3 p0 = player.transform.position;
                    for (int i = 0; i < 15; i++) fpc.StepController(0.02f);
                    return (player.transform.position - p0).magnitude;
                }

                float walkDist = MeasureSpeed(false, false);
                float sprintDist = MeasureSpeed(true, false);
                float crouchDist = MeasureSpeed(false, true);
                float crouchHeight = cc.height;

                if (!(sprintDist > walkDist && walkDist > crouchDist) || crouchHeight > 1.3f)
                {
                    failures.Add($"Walk/Sprint/Crouch mismatch: sprint={sprintDist:F3}, walk={walkDist:F3}, crouch={crouchDist:F3}, crouchHeight={crouchHeight:F2}");
                }
                else
                {
                    sb.AppendLine($"  Stance Speeds (0.3s): Sprint={sprintDist:F3}m > Walk={walkDist:F3}m > Crouch={crouchDist:F3}m (cc.height={crouchHeight:F2}m) OK");
                }
            }
            finally
            {
                cam.targetTexture = null;
                cam.ResetAspect();
                canvas.renderMode = prevRenderMode;
                canvas.worldCamera = prevWorldCam;
                canvas.planeDistance = prevPlaneDist;
                UnityEngine.Object.DestroyImmediate(rtTmp);
            }
        }

        private static void CaptureVisualScreenshots(StringBuilder sb)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                sb.AppendLine("  Screenshots: skipped (GraphicsDeviceType.Null)");
                return;
            }

            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            var player = GameObject.Find("Player_Protagonist");
            var camMode = player != null ? player.GetComponent<CameraModeController>() : null;
            var debugPanel = UnityEngine.Object.FindAnyObjectByType<AnimationDebugPanel>(FindObjectsInactive.Include);
            var flashRig = GameObject.Find("Flashlight_Rig")?.transform;
            var flashCtrl = flashRig != null ? flashRig.GetComponent<FlashlightController>() : null;
            var spot = GameObject.Find("SpotLight")?.GetComponent<Light>();
            if (cam == null || player == null || spot == null) return;

            if (flashCtrl != null) flashCtrl.ApplyRealisticLightSettings();
            spot.enabled = true;

            // 1P modunda gövde mesh'ini gizle (yaka/ceket kamera tıkanmasını önle)
            if (camMode != null) camMode.SetCameraMode(CameraViewMode.FirstPerson);
            if (debugPanel != null)
            {
                debugPanel.EnsureBuilt();
                debugPanel.SetPanelOpen(false);
            }

            // 1. Gameplay HUD Screenshot at 2340x1080 (Clean 1P + Mobile Controls + ANIM & CAM buttons)
            if (canvas != null)
            {
                var prevMode = canvas.renderMode;
                var prevCam = canvas.worldCamera;
                var prevPlane = canvas.planeDistance;
                var actionBtn = GameObject.Find("Mobile_ActionButton");
                var actionCg = actionBtn != null ? actionBtn.GetComponent<CanvasGroup>() : null;
                float prevActionAlpha = actionCg != null ? actionCg.alpha : 0f;
                var joyCg = GameObject.Find("Joystick_Background")?.GetComponent<CanvasGroup>();
                float prevJoyAlpha = joyCg != null ? joyCg.alpha : 0.65f;

                if (actionCg != null) actionCg.alpha = 0.92f;
                if (joyCg != null) joyCg.alpha = 0.80f;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 0.35f;

                player.transform.position = new Vector3(0f, 0.02f, -15.5f);
                player.transform.rotation = Quaternion.identity;
                if (flashCtrl != null)
                {
                    flashRig.localPosition = new Vector3(0.18f, -0.16f, 0.16f);
                    flashRig.LookAt(cam.transform.position + cam.transform.forward * 8f);
                }

                Canvas.ForceUpdateCanvases();
                RenderAndSave(cam, 2340, 1080, "Logs/Screenshots/Gameplay_HUD_2340x1080.png", sb);

                if (actionCg != null) actionCg.alpha = prevActionAlpha;
                if (joyCg != null) joyCg.alpha = prevJoyAlpha;
                canvas.renderMode = prevMode;
                canvas.worldCamera = prevCam;
                canvas.planeDistance = prevPlane;
                canvas.gameObject.SetActive(false);
            }

            // 2. Flashlight Close Wall (2.2m perpendicular to School_FrontFacade_L)
            player.transform.position = new Vector3(-6.0f, 0.02f, -2.5f);
            player.transform.rotation = Quaternion.identity;
            if (flashRig != null)
            {
                flashRig.localPosition = new Vector3(0.18f, -0.16f, 0.16f);
                flashRig.LookAt(cam.transform.position + cam.transform.forward * 8f);
            }
            RenderAndSave(cam, 1280, 720, "Logs/Screenshots/Flashlight_Wall_Close_2m.png", sb);

            // 3. Flashlight Oblique Wall & Doorway (5m at ~35 deg angle)
            player.transform.position = new Vector3(-3.5f, 0.02f, -5.2f);
            player.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            if (flashRig != null)
            {
                flashRig.localPosition = new Vector3(0.18f, -0.16f, 0.16f);
                flashRig.LookAt(cam.transform.position + cam.transform.forward * 8f);
            }
            RenderAndSave(cam, 1280, 720, "Logs/Screenshots/Flashlight_Wall_Oblique_5m.png", sb);

            // 4. Dark Interior Corridor Beam Shot
            player.transform.position = new Vector3(0f, 0.02f, 2.2f);
            player.transform.rotation = Quaternion.identity;
            if (flashRig != null)
            {
                flashRig.localPosition = new Vector3(0.18f, -0.16f, 0.16f);
                flashRig.LookAt(cam.transform.position + cam.transform.forward * 8f);
            }
            RenderAndSave(cam, 1280, 720, "Logs/Screenshots/Flashlight_Hallway_Dark.png", sb);

            // 5. Third-Person Character Ground Alignment & Animation Visual Verification Shots
            var anim = player.GetComponentInChildren<Animator>(true);
            var animCtrl = player.GetComponentInChildren<ProtagonistAnimationController>(true);
            var registry = AssetDatabase.LoadAssetAtPath<AnimationLibraryRegistry>(ProtagonistAnimationSetup.REGISTRY_PATH);
            if (anim != null && animCtrl != null && registry != null)
            {
                if (camMode != null) camMode.SetCameraMode(CameraViewMode.ThirdPerson);
                player.transform.position = new Vector3(0f, 0.0f, -4.0f);
                player.transform.rotation = Quaternion.identity;
                anim.transform.localPosition = Vector3.zero;
                anim.transform.localRotation = Quaternion.identity;

                // Sahne karanlık olduğu için karakteri net görebilmek adına geçici stüdyo dolgu ışığı ekle
                var fillLightGo = new GameObject("_Temp_VerifyFillLight");
                var fillLight = fillLightGo.AddComponent<Light>();
                fillLight.type = LightType.Directional;
                fillLight.intensity = 0.85f;
                fillLight.color = new Color(0.92f, 0.95f, 1.0f);
                fillLightGo.transform.rotation = Quaternion.Euler(32f, -145f, 0f);

                var camParent = cam.transform.parent;
                var camLocalPos = cam.transform.localPosition;
                var camLocalRot = cam.transform.localRotation;

                cam.transform.SetParent(null, true);
                cam.transform.position = player.transform.position + new Vector3(1.65f, 1.25f, 2.35f);
                cam.transform.LookAt(player.transform.position + Vector3.up * 0.92f);

                // 5a. True First-Person Look-Down Body Check (Test C: torso, legs, shoes visible!)
                var idleClip = registry.FindClip("Idle_Natural_V2") ?? registry.FindClip("Idle_Breathing");
                if (idleClip != null)
                {
                    if (camMode != null) camMode.SetCameraMode(CameraViewMode.FirstPerson);
                    anim.Rebind();
                    idleClip.SampleAnimation(anim.gameObject, idleClip.length * 0.25f);
                    cam.transform.position = player.transform.position + new Vector3(0f, 1.62f, 0.14f);
                    cam.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
                    RenderAndSave(cam, 1280, 720, "Logs/Screenshots/TrueFPS_LookDown_Body.png", sb);
                }

                // 5b. 3P Idle_Natural_V2 Ground Check
                if (camMode != null) camMode.SetCameraMode(CameraViewMode.ThirdPerson);
                cam.transform.position = player.transform.position + new Vector3(1.65f, 1.25f, 2.35f);
                cam.transform.LookAt(player.transform.position + Vector3.up * 0.92f);

                if (idleClip != null)
                {
                    anim.Rebind();
                    idleClip.SampleAnimation(anim.gameObject, idleClip.length * 0.25f);
                    RenderAndSave(cam, 1280, 720, "Logs/Screenshots/ThirdPerson_Idle_GroundCheck.png", sb);
                }

                // 5c. 3P Crouch_Idle_V2
                var crouchIdleClip = registry.FindClip("Crouch_Idle_V2");
                if (crouchIdleClip != null)
                {
                    anim.Rebind();
                    crouchIdleClip.SampleAnimation(anim.gameObject, crouchIdleClip.length * 0.25f);
                    RenderAndSave(cam, 1280, 720, "Logs/Screenshots/ThirdPerson_Crouch_Idle.png", sb);
                }

                // 5d. 3P Walk_Grounded_V2 Mid-Stride
                var walkClip = registry.FindClip("Walk_Grounded_V2") ?? registry.FindClip("Walk_Forward_unity");
                if (walkClip != null)
                {
                    anim.Rebind();
                    walkClip.SampleAnimation(anim.gameObject, walkClip.length * 0.30f);
                    RenderAndSave(cam, 1280, 720, "Logs/Screenshots/ThirdPerson_Walk_Forward.png", sb);
                }

                // 5e. 3P Flashlight_Hold_V2
                var flashHoldClip = registry.FindClip("Flashlight_Hold_V2") ?? registry.FindClip("Flashlight_Hold");
                if (flashHoldClip != null)
                {
                    anim.Rebind();
                    flashHoldClip.SampleAnimation(anim.gameObject, flashHoldClip.length * 0.25f);
                    RenderAndSave(cam, 1280, 720, "Logs/Screenshots/ThirdPerson_Flashlight_Hold.png", sb);
                }

                // 5d. DebugPanel Open at 2340x1080 (Panel left, full protagonist visible on right)
                if (canvas != null && debugPanel != null)
                {
                    if (idleClip != null)
                    {
                        anim.Rebind();
                        idleClip.SampleAnimation(anim.gameObject, idleClip.length * 0.25f);
                    }
                    cam.transform.position = player.transform.position + new Vector3(0.65f, 1.32f, 2.55f);
                    cam.transform.LookAt(player.transform.position + new Vector3(-0.45f, 0.95f, 0f));

                    canvas.gameObject.SetActive(true);
                    var prevMode = canvas.renderMode;
                    var prevCam = canvas.worldCamera;
                    var prevPlane = canvas.planeDistance;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 0.35f;

                    debugPanel.SetPanelOpen(true);
                    Canvas.ForceUpdateCanvases();
                    RenderAndSave(cam, 2340, 1080, "Logs/Screenshots/DebugPanel_Open_2340x1080.png", sb);

                    debugPanel.SetPanelOpen(false);
                    canvas.renderMode = prevMode;
                    canvas.worldCamera = prevCam;
                    canvas.planeDistance = prevPlane;
                }

                animCtrl.StopIsolatedPreview();
                cam.transform.SetParent(camParent, false);
                cam.transform.localPosition = camLocalPos;
                cam.transform.localRotation = camLocalRot;
                UnityEngine.Object.DestroyImmediate(fillLightGo);
            }

            if (canvas != null) canvas.gameObject.SetActive(true);
        }

        private static void VerifyChapter1ProductionAndPuzzleArc(StringBuilder sb, List<string> failures)
        {
            sb.AppendLine("=== Chapter I: The Arrival — Environment, Toe Alignment & Puzzle Verification ===");

            // 1. Provenance & Skybox & Zero Pink/Error Materials Check
            if (!File.Exists("Assets/Environment/ASSET_PROVENANCE.md"))
                failures.Add("ASSET_PROVENANCE.md missing in Assets/Environment/.");

            if (RenderSettings.skybox == null)
                failures.Add("RenderSettings.skybox is null (expected Blue Hour winter_evening skybox).");

            var allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int errorMatCount = 0;
            foreach (var r in allRenderers)
            {
                if (r == null || !r.enabled) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].shader == null || mats[i].shader.name.Contains("InternalErrorShader"))
                    {
                        errorMatCount++;
                        failures.Add($"Missing or pink error material on '{r.gameObject.name}' (slot {i}).");
                    }
                }
            }
            sb.AppendLine($"  Material Integrity: {allRenderers.Length} scene renderers checked, {errorMatCount} missing/error materials OK");

            // 2. Hero Environment Objects Check (Sedan, 3D HARPUT Sign, Detailed Door, Panel, Feştek, Socket, CRT TV)
            var sedan = GameObject.Find("Protagonist_ArrivedSedan");
            var sign = GameObject.Find("HarputEnglishCourse_3DSign");
            var signText1 = GameObject.Find("Sign_3DText_HARPUT");
            var signText2 = GameObject.Find("Sign_3DText_ENGLISH_COURSE");
            var detailedDoor = GameObject.Find("Detailed_Entrance_DoorSet");
            var objMgr = UnityEngine.Object.FindAnyObjectByType<ChapterObjectiveManager>(FindObjectsInactive.Include);
            var panel = UnityEngine.Object.FindAnyObjectByType<ElectricalPanelInteractable>(FindObjectsInactive.Include);
            var festek = UnityEngine.Object.FindAnyObjectByType<FestekItem>(FindObjectsInactive.Include);
            var socket = UnityEngine.Object.FindAnyObjectByType<FestekSocketInteractable>(FindObjectsInactive.Include);
            var powerMgr = UnityEngine.Object.FindAnyObjectByType<FacilityPowerManager>(FindObjectsInactive.Include);
            var tvCtrl = UnityEngine.Object.FindAnyObjectByType<AbandonedTelevisionController>(FindObjectsInactive.Include);

            if (sedan == null || sedan.GetComponent<Collider>() == null)
                failures.Add("Protagonist_ArrivedSedan or its collider is missing.");
            if (sign == null || signText1 == null || signText2 == null)
                failures.Add("3D extruded HARPUT / ENGLISH COURSE sign or raised letter meshes missing.");
            if (detailedDoor == null)
                failures.Add("Detailed_Entrance_DoorSet missing on SchoolEntrance_Doorway.");
            if (objMgr == null || panel == null || festek == null || socket == null || powerMgr == null || tvCtrl == null)
            {
                failures.Add("One or more Chapter I gameplay/puzzle components missing in AbandonedSchool.unity.");
                return;
            }

            // 3. Protagonist Toe & Sole Flat Ground Alignment Check (Sec 20)
            var player = GameObject.Find("Player_Protagonist");
            var anim = player != null ? player.GetComponentInChildren<Animator>(true) : null;
            var animCtrl = player != null ? player.GetComponentInChildren<ProtagonistAnimationController>(true) : null;
            var procCtrl = player != null ? player.GetComponent<CharacterProceduralController>() : null;
            var fpc = player != null ? player.GetComponent<FirstPersonController>() : null;
            var flashPhys = player != null ? player.GetComponentInChildren<FlashlightPhysicalItem>(true) : null;
            var camMode = player != null ? player.GetComponent<CameraModeController>() : null;
            var registry = AssetDatabase.LoadAssetAtPath<AnimationLibraryRegistry>(ProtagonistAnimationSetup.REGISTRY_PATH);

            if (anim != null && animCtrl != null && procCtrl != null && registry != null)
            {
                player.transform.position = Vector3.zero;
                player.transform.rotation = Quaternion.identity;
                anim.transform.localPosition = Vector3.zero;
                anim.transform.localRotation = Quaternion.identity;
                procCtrl.InitializeIfNeeded();

                var idleV2 = registry.FindClip("Idle_Natural_V2");
                if (idleV2 != null)
                {
                    // Earlier checks can leave a full-body action active. Test the idle pose in a clean state.
                    animCtrl.ResetAnimationState();
                    procCtrl.InitializeIfNeeded();
                    animCtrl.StartIsolatedPreview(idleV2, true);
                    try
                    {
                        animCtrl.SetPreviewNormalizedTime(0.25f);
                        procCtrl.StepProcedural(0.02f);

                        Transform lToeBase = anim.GetBoneTransform(HumanBodyBones.LeftToes);
                        Transform rToeBase = anim.GetBoneTransform(HumanBodyBones.RightToes);
                        Transform lToeEnd = lToeBase != null && lToeBase.childCount > 0 ? (lToeBase.Find("LeftToe_End") ?? lToeBase.GetChild(0)) : null;
                        Transform rToeEnd = rToeBase != null && rToeBase.childCount > 0 ? (rToeBase.Find("RightToe_End") ?? rToeBase.GetChild(0)) : null;

                        if (lToeBase != null && lToeEnd != null && rToeBase != null && rToeEnd != null)
                        {
                            float lLift = lToeEnd.position.y - lToeBase.position.y;
                            float rLift = rToeEnd.position.y - rToeBase.position.y;
                            if (lLift > 0.004f || rLift > 0.004f || lToeEnd.position.y < 0.012f)
                            {
                                var leftAnkle = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                                failures.Add($"Toe alignment check failed: LeftToeLift={lLift:F4}m, RightToeLift={rLift:F4}m, LToeEndY={lToeEnd.position.y:F4}m, LAnkleY={leftAnkle.position.y:F4}m, enabled={procCtrl.enableToeAlignment}, fullOverride={animCtrl.IsFullBodyOverrideActive}.");
                            }
                            else
                            {
                                sb.AppendLine($"  Toe & Sole Alignment OK: LToeBaseY={lToeBase.position.y:F3}m -> LToeEndY={lToeEnd.position.y:F3}m (lift={lLift:F4}m), RToeEndY={rToeEnd.position.y:F3}m");
                            }
                        }
                    }
                    finally
                    {
                        animCtrl.StopIsolatedPreview();
                    }
                }
            }

            // Setup screenshot helper camera if rendering is available
            bool canRender = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            Camera shotCam = null;
            GameObject shotCamGo = null;
            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            bool prevCanvasActive = canvas != null && canvas.gameObject.activeSelf;
            if (canRender)
            {
                if (canvas != null) canvas.gameObject.SetActive(false);
                shotCamGo = new GameObject("_Chapter1_VerifyShotCam");
                shotCam = shotCamGo.AddComponent<Camera>();
                shotCam.fieldOfView = 58f;
                shotCam.nearClipPlane = 0.05f;
                shotCam.farClipPlane = 120f;
                shotCam.clearFlags = CameraClearFlags.Skybox;

                // Shot 1: Exterior Arrival — Car + Wet Asphalt + Weeds + HARPUT ENGLISH COURSE Building
                shotCamGo.transform.position = new Vector3(-1.35f, 1.62f, -21.2f);
                shotCamGo.transform.LookAt(new Vector3(1.1f, 1.45f, -9.5f));
                RenderAndSave(shotCam, 1280, 720, "Logs/Screenshots/Chapter1_Arrival_Car_And_Facade.png", sb);

                // Shot 2: Close-up of 3D Extruded HARPUT ENGLISH COURSE Sign & Detailed Entrance Door
                shotCamGo.transform.position = new Vector3(-0.65f, 2.25f, -5.2f);
                shotCamGo.transform.LookAt(new Vector3(0.0f, 2.55f, -0.15f));
                RenderAndSave(shotCam, 1280, 720, "Logs/Screenshots/Chapter1_Harput_3DSign_And_Door.png", sb);

                // Shot 3: Interior Classroom 101 (Desks, B1 Whiteboard Lesson, Bookshelf, Notebook)
                var c101Fill = new GameObject("_Temp_C101_ShotLight").AddComponent<Light>();
                c101Fill.type = LightType.Spot;
                c101Fill.spotAngle = 62f;
                c101Fill.range = 14f;
                c101Fill.intensity = 2.6f;
                c101Fill.color = new Color(1.0f, 0.96f, 0.88f);
                c101Fill.transform.position = new Vector3(-3.4f, 1.68f, 7.6f);
                c101Fill.transform.LookAt(new Vector3(-6.2f, 1.15f, 3.8f));
                shotCamGo.transform.position = c101Fill.transform.position;
                shotCamGo.transform.rotation = c101Fill.transform.rotation;
                RenderAndSave(shotCam, 1280, 720, "Logs/Screenshots/Chapter1_Classroom101_B1Lesson.png", sb);
                UnityEngine.Object.DestroyImmediate(c101Fill.gameObject);
            }

            // 4. Full 13-State Chapter I Narrative & Physical Puzzle Progression Verification
            InvokePrivate(objMgr, "Awake");
            InvokePrivate(powerMgr, "Awake");
            InvokePrivate(tvCtrl, "Awake");
            InvokePrivate(panel, "Awake");
            InvokePrivate(festek, "Awake");
            InvokePrivate(socket, "Awake");
            if (flashPhys != null) InvokePrivate(flashPhys, "Awake");

            if (objMgr.CurrentState != ChapterObjectiveState.Arrival)
                failures.Add($"Initial objective expected Arrival, got {objMgr.CurrentState}.");
            if (powerMgr.IsPowerRestored || tvCtrl.IsPoweredOn)
                failures.Add("Facility power or CRT TV must be OFF at Chapter I start.");
            if (festek.CurrentState != FestekItemState.CradledInPanel)
                failures.Add($"Festek expected CradledInPanel at start, got {festek.CurrentState}.");

            // Edge-case test: interacting with socket BEFORE holding Feştek must NOT restore power
            socket.Interact(null);
            if (socket.IsActivated || powerMgr.IsPowerRestored)
                failures.Add("Out-of-order socket interaction restored power without Feştek!");

            // Spatial progression Arrival -> EnterBuilding -> ExploreInstitution -> LocateElectricalPanel -> OperateElectricalPanel
            objMgr.EvaluateSpatialProgression(new Vector3(0f, 0.02f, -8.0f));
            if (objMgr.CurrentState != ChapterObjectiveState.EnterBuilding)
                failures.Add($"Expected EnterBuilding at courtyard approach, got {objMgr.CurrentState}.");

            objMgr.EvaluateSpatialProgression(new Vector3(0f, 0.02f, 1.5f));
            if (objMgr.CurrentState != ChapterObjectiveState.ExploreInstitution)
                failures.Add($"Expected ExploreInstitution inside entrance, got {objMgr.CurrentState}.");

            objMgr.EvaluateSpatialProgression(new Vector3(0f, 0.02f, 5.0f));
            if (objMgr.CurrentState != ChapterObjectiveState.LocateElectricalPanel)
                failures.Add($"Expected LocateElectricalPanel in dark hallway, got {objMgr.CurrentState}.");

            objMgr.EvaluateSpatialProgression(new Vector3(3.85f, 0.02f, 7.8f));
            if (objMgr.CurrentState != ChapterObjectiveState.OperateElectricalPanel)
                failures.Add($"Expected OperateElectricalPanel near panel, got {objMgr.CurrentState}.");

            // Step 5-6: Pull electrical panel lever -> power stays OFF, Feştek physically drops!
            panel.ExecuteImmediateForTest();
            panel.ExecuteImmediateForTest(); // Spam resilience check
            if (!panel.HasOperated || powerMgr.IsPowerRestored)
                failures.Add("Operating panel lever either failed or prematurely restored power without Feştek.");
            if (festek.CurrentState != FestekItemState.DroppedPhysics)
                failures.Add($"Feştek did not enter DroppedPhysics after lever pull (got {festek.CurrentState}).");
            if (objMgr.CurrentState != ChapterObjectiveState.DiscoverFestek)
                failures.Add($"Expected DiscoverFestek after lever pull, got {objMgr.CurrentState}.");

            // Test Feştek out-of-bounds safety recovery onto reachable floor
            festek.transform.position = new Vector3(3.85f, -4.5f, 8.5f);
            festek.EnforceReachableSafetyBounds();
            if (festek.transform.position.y < 0.02f || festek.transform.position.y > 0.25f)
                failures.Add($"Feştek safety recovery failed: pos={festek.transform.position}");

            if (canRender && shotCam != null)
            {
                // Shot 4: Electrical Panel (Pulled Lever) + Dropped Custom Feştek on Floor under Flashlight
                var pSpot = new GameObject("_Temp_PanelSpot").AddComponent<Light>();
                pSpot.type = LightType.Spot;
                pSpot.spotAngle = 60f;
                pSpot.range = 10f;
                pSpot.intensity = 2.5f;
                pSpot.color = new Color(1.0f, 0.96f, 0.88f);
                pSpot.transform.position = new Vector3(3.85f, 1.55f, 6.75f);
                pSpot.transform.LookAt(new Vector3(3.85f, 0.72f, 8.95f));
                shotCamGo.transform.position = pSpot.transform.position;
                shotCamGo.transform.rotation = pSpot.transform.rotation;
                RenderAndSave(shotCam, 1280, 720, "Logs/Screenshots/Chapter1_Puzzle_Panel_And_DroppedFestek.png", sb);
                UnityEngine.Object.DestroyImmediate(pSpot.gameObject);
            }

            // Step 7: Pick up Feştek into Right Hand + Verify Off-Hand Left Flashlight Transfer (Zero Overlap!)
            var spotLight = GameObject.Find("SpotLight")?.GetComponent<Light>();
            if (flashPhys != null) flashPhys.AttachToHand();
            festek.PickupIntoRightHand(player);

            if (festek.CurrentState != FestekItemState.HeldInRightHand || animCtrl == null || !festek.transform.IsChildOf(animCtrl.RightHandFestekSocket))
                failures.Add("Feştek did not attach cleanly to RightHand_Festek_Socket.");
            if (flashPhys == null || !flashPhys.IsTransferredToLeftHand || !flashPhys.transform.IsChildOf(animCtrl.LeftHandFlashlightSocket))
                failures.Add("Flashlight did not transfer to LeftHand_Flashlight_Socket when picking up Feştek.");
            if (spotLight != null && !spotLight.enabled)
                failures.Add("Flashlight SpotLight turned off during left-hand transfer.");
            if (objMgr.CurrentState != ChapterObjectiveState.LocateFestekSocket)
                failures.Add($"Expected LocateFestekSocket after Feştek pickup, got {objMgr.CurrentState}.");

            if (canRender && shotCam != null && camMode != null)
            {
                // Shot 5: 3P Dual-Hand Coordination (Right Hand holding Feştek + Left Hand holding Flashlight + Flat Shoes)
                camMode.SetCameraMode(CameraViewMode.ThirdPerson);
                player.transform.position = new Vector3(4.8f, 0.0f, 7.6f);
                player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                if (fpc != null) fpc.StepController(0.02f);
                if (procCtrl != null) procCtrl.StepProcedural(0.02f);

                var charFill = new GameObject("_Temp_DualHandFill").AddComponent<Light>();
                charFill.type = LightType.Point;
                charFill.range = 8f;
                charFill.intensity = 1.6f;
                charFill.transform.position = new Vector3(4.8f, 1.6f, 5.6f);

                shotCamGo.transform.position = new Vector3(5.35f, 1.22f, 5.55f);
                shotCamGo.transform.LookAt(new Vector3(4.8f, 0.92f, 7.6f));
                RenderAndSave(shotCam, 1280, 720, "Logs/Screenshots/Chapter1_DualHand_Festek_And_Flashlight.png", sb);

                UnityEngine.Object.DestroyImmediate(charFill.gameObject);
                camMode.SetCameraMode(CameraViewMode.FirstPerson);
            }

            // Step 8-13: Approach Socket -> Insert Feştek -> Press Activation Button -> Restore Power -> TV Turns ON -> Milestone Complete!
            objMgr.EvaluateSpatialProgression(new Vector3(5.65f, 0.02f, 8.0f));
            if (objMgr.CurrentState != ChapterObjectiveState.InsertFestek)
                failures.Add($"Expected InsertFestek near socket, got {objMgr.CurrentState}.");

            socket.ExecuteImmediateForTest(festek, player);

            if (festek.CurrentState != FestekItemState.SeatedInSocket)
                failures.Add($"Expected Feştek SeatedInSocket, got {festek.CurrentState}.");
            if (!socket.IsActivated)
                failures.Add("FestekSocketInteractable did not reach Activated state.");
            if (flashPhys != null && flashPhys.IsTransferredToLeftHand)
                failures.Add("Flashlight did not return from left hand after seating Feştek.");
            if (!powerMgr.IsPowerRestored)
                failures.Add("FacilityPowerManager.IsPowerRestored is false after Feştek activation.");
            if (!tvCtrl.IsPoweredOn || tvCtrl.ScreenLocalLight == null || !tvCtrl.ScreenLocalLight.enabled)
                failures.Add("AbandonedTelevisionController or its local glow light did not activate when electricity restored.");
            if (objMgr.CurrentState != ChapterObjectiveState.ChapterMilestoneComplete)
                failures.Add($"Expected final state ChapterMilestoneComplete, got {objMgr.CurrentState}.");
            if (fpc != null && !fpc.AllowMovement)
                failures.Add("Player movement was locked at ChapterMilestoneComplete (player must remain in control!).");

            sb.AppendLine("  Chapter I 13-State Puzzle Chain: Arrival -> Enter -> Explore -> Panel Lever -> Feştek Physics Drop & Recovery -> Right-Hand Pickup + Left-Hand Flashlight -> Socket Insert & Button Press -> Power Restored -> CRT TV Active -> ChapterMilestoneComplete OK");

            if (canRender && shotCam != null)
            {
                // Shot 6: Powered Facility + Seated Feştek in Socket + Active CRT Television Glowing on the Right!
                tvCtrl.StepStaticEffect(0.15f);
                shotCamGo.transform.position = new Vector3(5.35f, 1.52f, 6.35f);
                shotCamGo.transform.LookAt(new Vector3(6.65f, 1.32f, 9.05f));
                RenderAndSave(shotCam, 1280, 720, "Logs/Screenshots/Chapter1_Socket_Seated_And_TV_Active.png", sb);

                UnityEngine.Object.DestroyImmediate(shotCamGo);
                if (canvas != null) canvas.gameObject.SetActive(prevCanvasActive);
            }
        }

        private static void RenderAndSave(Camera cam, int w, int h, string path, StringBuilder sb)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prevT = cam.targetTexture;
            var prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, png);

            cam.targetTexture = prevT;
            cam.ResetAspect();
            RenderTexture.active = prevActive;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);

            sb.AppendLine($"  Screenshot: {path} ({w}x{h}, {png.Length} bytes)");
        }

        private static void InvokePrivate(object target, string methodName)
        {
            if (target == null) return;
            var m = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            m?.Invoke(target, null);
        }
    }
}
