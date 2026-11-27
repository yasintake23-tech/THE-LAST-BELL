using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using TheLastBell.Player;
using TheLastBell.Interaction;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Modüler 7 Katmanlı Karakter Animasyon & Fiziksel Fener Kurulum Aracı.
    /// Walk_Forward_unity.fbx klibini Humanoid olarak içe aktarır, AvatarMask'ları üretir,
    /// Protagonist_AnimatorController.controller'ı (GUID korunarak) 7 katmanlı mimariye yükseltir
    /// ve AbandonedSchool.unity içindeki karakter ile fener referanslarını bağlar.
    /// </summary>
    public static class ProtagonistAnimationSetup
    {
        public const string WALK_FBX_PATH = "Assets/Animations/Locomotion/Walk_Forward_unity.fbx";
        public const string CONTROLLER_PATH = "Assets/Characters/Protagonist/Protagonist_AnimatorController.controller";
        public const string MASKS_DIR = "Assets/Animations/Masks";

        [MenuItem("The Last Bell/Animasyon/Modüler 7 Katmanlı Animasyon Sistemini Kur", false, 21)]
        public static void SetupAllMenu()
        {
            SetupAll();
            EditorUtility.DisplayDialog("Animasyon Sistemi Güncellendi",
                "Walk_Forward_unity.fbx, 4 AvatarMask, 7 katmanlı Protagonist_AnimatorController ve FlashlightPhysicalItem başarıyla yapılandırıldı.",
                "Tamam");
        }

        public static void SetupAll()
        {
            EnsureWalkForwardImportSettings();

            AnimationClip walkForwardClip = LoadPrimaryClip(WALK_FBX_PATH);
            if (walkForwardClip == null)
            {
                Debug.LogError($"[ProtagonistAnimationSetup] Walk_Forward_unity AnimationClip yüklenemedi: {WALK_FBX_PATH}");
            }
            else
            {
                Debug.Log($"[ProtagonistAnimationSetup] Walk_Forward_unity yüklendi: {walkForwardClip.name}, len={walkForwardClip.length:F2}s, loop={walkForwardClip.isLooping}, humanMotion={walkForwardClip.humanMotion}");
            }

            if (!Directory.Exists(MASKS_DIR)) Directory.CreateDirectory(MASKS_DIR);

            AvatarMask upperMask = CreateOrUpdateMask($"{MASKS_DIR}/UpperBody_Mask.mask", mask =>
            {
                SetAllBodyParts(mask, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            });

            AvatarMask rightArmMask = CreateOrUpdateMask($"{MASKS_DIR}/RightArm_Mask.mask", mask =>
            {
                SetAllBodyParts(mask, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            });

            AvatarMask leftArmMask = CreateOrUpdateMask($"{MASKS_DIR}/LeftArm_Mask.mask", mask =>
            {
                SetAllBodyParts(mask, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            });

            AvatarMask headMask = CreateOrUpdateMask($"{MASKS_DIR}/HeadNeck_Mask.mask", mask =>
            {
                SetAllBodyParts(mask, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            });

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
            if (controller == null)
            {
                Debug.LogError($"[ProtagonistAnimationSetup] AnimatorController bulunamadı: {CONTROLLER_PATH}");
                return;
            }

            EnsureParameters(controller);
            UpgradeBaseLocomotionLayer(controller, walkForwardClip);
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_UPPER_BODY, upperMask, AnimatorLayerBlendingMode.Override, 0.25f, false, BuildUpperBodyStates);
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_RIGHT_ARM, rightArmMask, AnimatorLayerBlendingMode.Override, 1.0f, true, BuildRightArmStates);
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_LEFT_ARM, leftArmMask, AnimatorLayerBlendingMode.Override, 0.0f, true, BuildLeftArmStates);
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_HEAD_LOOK, headMask, AnimatorLayerBlendingMode.Override, 1.0f, true, BuildHeadLookStates);
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_ADDITIVE, upperMask, AnimatorLayerBlendingMode.Additive, 0.35f, false, BuildAdditiveStates);
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_FULL_OVERRIDE, null, AnimatorLayerBlendingMode.Override, 0.0f, true, BuildFullOverrideStates);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            WireSceneComponents(controller);
        }

        private static void EnsureWalkForwardImportSettings()
        {
            if (!File.Exists(WALK_FBX_PATH))
            {
                Debug.LogError($"[ProtagonistAnimationSetup] FBX bulunamadı: {WALK_FBX_PATH}");
                return;
            }

            var importer = AssetImporter.GetAtPath(WALK_FBX_PATH) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(WALK_FBX_PATH, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(WALK_FBX_PATH) as ModelImporter;
            }
            if (importer == null) return;

            bool needsReimport = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                needsReimport = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                needsReimport = true;
            }

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];
                    if (!c.loopTime || !c.loopPose || !c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ)
                    {
                        c.loopTime = true;
                        c.loopPose = true;
                        c.lockRootRotation = true;
                        c.keepOriginalOrientation = true;
                        c.lockRootHeightY = true;
                        c.keepOriginalPositionY = true;
                        c.lockRootPositionXZ = true;
                        c.keepOriginalPositionXZ = true;
                        clips[i] = c;
                        needsReimport = true;
                    }
                }
                importer.clipAnimations = clips;
            }

            if (needsReimport)
            {
                importer.SaveAndReimport();
                Debug.Log("[ProtagonistAnimationSetup] Walk_Forward_unity.fbx Humanoid + Loop + RootBake olarak içe aktarıldı.");
            }
        }

        private static AnimationClip LoadPrimaryClip(string fbxPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null) return null;
            return assets
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
        }

        private static void SetAllBodyParts(AvatarMask mask, bool active)
        {
            foreach (AvatarMaskBodyPart part in Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart) continue;
                mask.SetHumanoidBodyPartActive(part, active);
            }
        }

        private static AvatarMask CreateOrUpdateMask(string path, Action<AvatarMask> configure)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            bool isNew = (mask == null);
            if (isNew) mask = new AvatarMask();
            configure(mask);
            if (isNew) AssetDatabase.CreateAsset(mask, path);
            else EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void EnsureParameters(AnimatorController controller)
        {
            EnsureParam(controller, "Speed", AnimatorControllerParameterType.Float, 0f);
            EnsureParam(controller, "Strafe", AnimatorControllerParameterType.Float, 0f);
            EnsureParam(controller, "MoveMagnitude", AnimatorControllerParameterType.Float, 0f);
            EnsureParam(controller, "LocomotionSpeedMultiplier", AnimatorControllerParameterType.Float, 1f);
            EnsureParam(controller, "FearLevel", AnimatorControllerParameterType.Float, 0f);

            EnsureParam(controller, "IsCrouching", AnimatorControllerParameterType.Bool, false);
            EnsureParam(controller, "IsGrounded", AnimatorControllerParameterType.Bool, true);
            EnsureParam(controller, "IsExhausted", AnimatorControllerParameterType.Bool, false);
            EnsureParam(controller, "IsClimbing", AnimatorControllerParameterType.Bool, false);
            EnsureParam(controller, "IsCrawling", AnimatorControllerParameterType.Bool, false);
            EnsureParam(controller, "FlashlightHeld", AnimatorControllerParameterType.Bool, true);
            EnsureParam(controller, "FlashlightAim", AnimatorControllerParameterType.Bool, false);
            EnsureParam(controller, "CinematicActive", AnimatorControllerParameterType.Bool, false);

            EnsureParam(controller, "Interact", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "PickUp", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "DoorOpen", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "ImpactFlinch", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "Hit", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "Die", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "LayToIdle", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "Fall", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "GetUp", AnimatorControllerParameterType.Trigger, false);
        }

        private static void EnsureParam(AnimatorController controller, string name, AnimatorControllerParameterType type, object defaultVal)
        {
            if (controller.parameters.Any(p => p.name == name)) return;
            var p = new AnimatorControllerParameter { name = name, type = type };
            if (type == AnimatorControllerParameterType.Float) p.defaultFloat = Convert.ToSingle(defaultVal);
            else if (type == AnimatorControllerParameterType.Bool) p.defaultBool = Convert.ToBoolean(defaultVal);
            controller.AddParameter(p);
        }

        private static void UpgradeBaseLocomotionLayer(AnimatorController controller, AnimationClip walkForwardClip)
        {
            var layers = controller.layers;
            if (layers == null || layers.Length == 0) return;

            layers[0].iKPass = true;
            var sm = layers[0].stateMachine;
            if (sm != null)
            {
                foreach (var childState in sm.states)
                {
                    var st = childState.state;
                    if (st.name == "Locomotion")
                    {
                        st.speedParameterActive = true;
                        st.speedParameter = "LocomotionSpeedMultiplier";

                        if (st.motion is BlendTree bt && walkForwardClip != null)
                        {
                            var children = bt.children;
                            bool replaced = false;
                            for (int i = 0; i < children.Length; i++)
                            {
                                // Sadece İleri Yürüme yuvasını (x≈0, y≈1) Walk_Forward_unity ile değiştir
                                if (Mathf.Abs(children[i].position.x) < 0.05f &&
                                    Mathf.Abs(children[i].position.y - 1.0f) < 0.05f)
                                {
                                    children[i].motion = walkForwardClip;
                                    children[i].timeScale = 1.0f;
                                    replaced = true;
                                }
                            }
                            if (!replaced)
                            {
                                bt.AddChild(walkForwardClip, new Vector2(0f, 1f));
                            }
                            else
                            {
                                bt.children = children;
                            }
                            EditorUtility.SetDirty(bt);
                        }
                    }
                }
            }
            controller.layers = layers;
        }

        private static void EnsureModularLayer(
            AnimatorController controller,
            string layerName,
            AvatarMask mask,
            AnimatorLayerBlendingMode blendingMode,
            float defaultWeight,
            bool ikPass,
            Action<AnimatorStateMachine> populateStates)
        {
            var layers = controller.layers.ToList();
            int existingIdx = layers.FindIndex(l => l.name == layerName);

            AnimatorControllerLayer layer;
            if (existingIdx >= 0)
            {
                layer = layers[existingIdx];
                layer.avatarMask = mask;
                layer.blendingMode = blendingMode;
                layer.defaultWeight = defaultWeight;
                layer.iKPass = ikPass;
                layers[existingIdx] = layer;
            }
            else
            {
                var sm = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(sm, controller);

                layer = new AnimatorControllerLayer
                {
                    name = layerName,
                    stateMachine = sm,
                    avatarMask = mask,
                    blendingMode = blendingMode,
                    defaultWeight = defaultWeight,
                    iKPass = ikPass
                };
                layers.Add(layer);
            }

            if (layer.stateMachine.states.Length == 0)
            {
                populateStates(layer.stateMachine);
            }

            controller.layers = layers.ToArray();
        }

        private static void BuildUpperBodyStates(AnimatorStateMachine sm)
        {
            var idle = sm.AddState("UpperBody_Idle");
            var alert = sm.AddState("UpperBody_Alert");
            sm.defaultState = idle;

            var toAlert = idle.AddTransition(alert);
            toAlert.AddCondition(AnimatorConditionMode.Greater, 0.5f, "FearLevel");
            toAlert.duration = 0.25f;
            toAlert.hasExitTime = false;

            var toIdle = alert.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.4f, "FearLevel");
            toIdle.duration = 0.25f;
            toIdle.hasExitTime = false;
        }

        private static void BuildRightArmStates(AnimatorStateMachine sm)
        {
            var hold = sm.AddState("Flashlight_Hold");
            var aim = sm.AddState("Flashlight_Aim");
            var empty = sm.AddState("Flashlight_Empty");
            sm.defaultState = hold;

            var holdToEmpty = hold.AddTransition(empty);
            holdToEmpty.AddCondition(AnimatorConditionMode.IfNot, 0f, "FlashlightHeld");
            holdToEmpty.duration = 0.18f;
            holdToEmpty.hasExitTime = false;

            var emptyToHold = empty.AddTransition(hold);
            emptyToHold.AddCondition(AnimatorConditionMode.If, 0f, "FlashlightHeld");
            emptyToHold.duration = 0.18f;
            emptyToHold.hasExitTime = false;

            var holdToAim = hold.AddTransition(aim);
            holdToAim.AddCondition(AnimatorConditionMode.If, 0f, "FlashlightAim");
            holdToAim.duration = 0.15f;
            holdToAim.hasExitTime = false;

            var aimToHold = aim.AddTransition(hold);
            aimToHold.AddCondition(AnimatorConditionMode.IfNot, 0f, "FlashlightAim");
            aimToHold.duration = 0.15f;
            aimToHold.hasExitTime = false;
        }

        private static void BuildLeftArmStates(AnimatorStateMachine sm)
        {
            var empty = sm.AddState("LeftArm_Empty");
            var doorOpen = sm.AddState("Door_Open");
            var pickUp = sm.AddState("Item_PickUp");
            var envInteract = sm.AddState("Env_Interact");
            sm.defaultState = empty;

            AddTriggerState(sm, empty, doorOpen, "DoorOpen");
            AddTriggerState(sm, empty, pickUp, "PickUp");
            AddTriggerState(sm, empty, envInteract, "Interact");
        }

        private static void BuildHeadLookStates(AnimatorStateMachine sm)
        {
            var neutral = sm.AddState("Look_Neutral");
            sm.defaultState = neutral;
        }

        private static void BuildAdditiveStates(AnimatorStateMachine sm)
        {
            var breath = sm.AddState("Breathing_Idle");
            var flinch = sm.AddState("Impact_Flinch");
            sm.defaultState = breath;
            AddTriggerState(sm, breath, flinch, "ImpactFlinch");
        }

        private static void BuildFullOverrideStates(AnimatorStateMachine sm)
        {
            var none = sm.AddState("None");
            var fall = sm.AddState("Fall");
            var getUp = sm.AddState("GetUp");
            var cinematic = sm.AddState("Cinematic_Override");
            sm.defaultState = none;

            var anyToFall = sm.AddAnyStateTransition(fall);
            anyToFall.AddCondition(AnimatorConditionMode.If, 0f, "Fall");
            anyToFall.duration = 0.12f;

            var fallToGetUp = fall.AddTransition(getUp);
            fallToGetUp.AddCondition(AnimatorConditionMode.If, 0f, "GetUp");
            fallToGetUp.duration = 0.25f;
            fallToGetUp.hasExitTime = false;

            var getUpToNone = getUp.AddTransition(none);
            getUpToNone.hasExitTime = true;
            getUpToNone.exitTime = 0.95f;
            getUpToNone.duration = 0.2f;

            var toCine = none.AddTransition(cinematic);
            toCine.AddCondition(AnimatorConditionMode.If, 0f, "CinematicActive");
            toCine.duration = 0.2f;
            toCine.hasExitTime = false;

            var fromCine = cinematic.AddTransition(none);
            fromCine.AddCondition(AnimatorConditionMode.IfNot, 0f, "CinematicActive");
            fromCine.duration = 0.2f;
            fromCine.hasExitTime = false;
        }

        private static void AddTriggerState(AnimatorStateMachine sm, AnimatorState idleState, AnimatorState actionState, string triggerName)
        {
            var toAct = sm.AddAnyStateTransition(actionState);
            toAct.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            toAct.duration = 0.12f;

            var back = actionState.AddTransition(idleState);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.18f;
        }

        private static void WireSceneComponents(AnimatorController controller)
        {
            string scenePath = "Assets/Scenes/AbandonedSchool.unity";
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var player = GameObject.Find("Player_Protagonist");
            if (player != null)
            {
                var fpc = player.GetComponent<FirstPersonController>();
                var anim = player.GetComponentInChildren<Animator>(true);
                var cam = player.transform.Find("Camera_Pivot/Main Camera");
                var flashRig = player.transform.Find("Camera_Pivot/Flashlight_Rig");
                var flashCtrl = flashRig != null ? flashRig.GetComponent<FlashlightController>() : null;

                if (anim != null)
                {
                    anim.runtimeAnimatorController = controller;
                    anim.applyRootMotion = false;

                    var animCtrl = anim.GetComponent<ProtagonistAnimationController>();
                    if (animCtrl == null) animCtrl = anim.gameObject.AddComponent<ProtagonistAnimationController>();

                    var soAnim = new SerializedObject(animCtrl);
                    var pAnim = soAnim.FindProperty("animator");
                    if (pAnim != null) pAnim.objectReferenceValue = anim;
                    var pCam = soAnim.FindProperty("cameraTransform");
                    if (pCam != null && cam != null) pCam.objectReferenceValue = cam;
                    soAnim.ApplyModifiedProperties();

                    animCtrl.InitializeIfNeeded();

                    if (fpc != null)
                    {
                        var soFpc = new SerializedObject(fpc);
                        var pCharAnim = soFpc.FindProperty("characterAnimator");
                        if (pCharAnim != null) pCharAnim.objectReferenceValue = anim;
                        var pAnimCtrl = soFpc.FindProperty("animController");
                        if (pAnimCtrl != null) pAnimCtrl.objectReferenceValue = animCtrl;
                        soFpc.ApplyModifiedProperties();
                    }

                    if (flashRig != null)
                    {
                        var physItem = flashRig.GetComponent<FlashlightPhysicalItem>();
                        if (physItem == null) physItem = flashRig.gameObject.AddComponent<FlashlightPhysicalItem>();

                        var soPhys = new SerializedObject(physItem);
                        var pRig = soPhys.FindProperty("cameraRigAttachPoint");
                        if (pRig != null) pRig.objectReferenceValue = flashRig.parent;
                        var pHand = soPhys.FindProperty("handAttachPoint");
                        if (pHand != null && animCtrl.RightHandSocket != null) pHand.objectReferenceValue = animCtrl.RightHandSocket;
                        var pFc = soPhys.FindProperty("flashlightController");
                        if (pFc != null) pFc.objectReferenceValue = flashCtrl;
                        var pAc = soPhys.FindProperty("animController");
                        if (pAc != null) pAc.objectReferenceValue = animCtrl;
                        soPhys.ApplyModifiedProperties();
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProtagonistAnimationSetup] AbandonedSchool.unity animasyon & fiziksel fener bağlantıları kaydedildi.");
        }
    }
}
