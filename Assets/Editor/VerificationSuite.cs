using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TheLastBell.Player;
using TheLastBell.UI;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Automated Functional & Visual Verification Suite (EditMode / BatchMode).
    /// Runs 8-direction analog joystick tests, camera-relative movement at 0/90/180 deg yaw,
    /// simultaneous multi-touch move+look, release stop, resolution dp layout checks (2340x1080),
    /// and captures rendered Gameplay HUD + Flashlight lighting screenshots.
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
                RunFunctionalMovementAndJoystickTests(sb, failures);
                CaptureVisualScreenshots(sb);
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

                // Determine joystick center in screen coordinates (2340x1080 RT camera)
                Vector2 joyCenterScreen = RectTransformUtility.WorldToScreenPoint(cam, joyBgRT.position);
                var eventSys = UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);

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
                    var pdDown = new PointerEventData(eventSys) { pointerId = -1, position = joyCenterScreen, pressEventCamera = cam };
                    mcUI.OnPointerDown(pdDown);

                    var pdDrag = new PointerEventData(eventSys) { pointerId = -1, position = joyCenterScreen + d.dir * dragDist, pressEventCamera = cam };
                    mcUI.OnDrag(pdDrag);

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

                    var pdUp = new PointerEventData(eventSys) { pointerId = -1, position = joyCenterScreen };
                    mcUI.OnPointerUp(pdUp);
                    if (mcUI.MovementInput != Vector2.zero || fpc.MobileMoveInput != Vector2.zero)
                    {
                        failures.Add($"Joystick release did not immediately zero input after '{d.name}'.");
                    }
                }

                // --- Test 2: Deadzone Check ---
                mcUI.OnPointerDown(new PointerEventData(eventSys) { pointerId = -1, position = joyCenterScreen, pressEventCamera = cam });
                mcUI.OnDrag(new PointerEventData(eventSys) { pointerId = -1, position = joyCenterScreen + new Vector2(4f, 4f), pressEventCamera = cam });
                for (int s = 0; s < 10; s++) mcUI.StepMovement(0.02f);
                if (mcUI.MovementInput != Vector2.zero)
                {
                    failures.Add($"Deadzone check failed: expected (0,0), got {mcUI.MovementInput}");
                }
                mcUI.OnPointerUp(new PointerEventData(eventSys) { pointerId = -1, position = joyCenterScreen });

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
                var tlzSimDown = new PointerEventData(eventSys) { pointerId = -2, position = lookStart, pressEventCamera = cam };
                tlzSimDown.pointerCurrentRaycast = new RaycastResult { gameObject = tlz.gameObject };
                mcUI.OnPointerDown(new PointerEventData(eventSys) { pointerId = -1, position = joyCenterScreen, pressEventCamera = cam });
                mcUI.OnDrag(new PointerEventData(eventSys) { pointerId = -1, position = joyCenterScreen + new Vector2(85f, 85f), pressEventCamera = cam });
                tlz.OnPointerDown(tlzSimDown);
                tlz.OnDrag(new PointerEventData(eventSys) { pointerId = -2, position = lookStart + new Vector2(110f, 0f), pressEventCamera = cam });

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
            var flashRig = GameObject.Find("Flashlight_Rig")?.transform;
            var flashCtrl = flashRig != null ? flashRig.GetComponent<FlashlightController>() : null;
            var spot = GameObject.Find("SpotLight")?.GetComponent<Light>();
            if (cam == null || player == null || spot == null) return;

            if (flashCtrl != null) flashCtrl.ApplyRealisticLightSettings();
            spot.enabled = true;

            // 1. Gameplay HUD Screenshot at 2340x1080 (including ScreenSpaceOverlay UI via ScreenSpaceCamera)
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

                if (actionCg != null) actionCg.alpha = 0.92f; // Show full layout including contextual Action button
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
                canvas.gameObject.SetActive(false); // Hide UI for clean 3D lighting shots
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

            if (canvas != null) canvas.gameObject.SetActive(true);
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
