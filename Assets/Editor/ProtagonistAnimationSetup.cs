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
using TheLastBell.UI;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Birleşik 52 Kliplik (29 V2 + 23 V1) Karakter Animasyon, Prosedürel Kontrol,
    /// True First-Person, 3P/Orbit Kamera ve Gelişmiş Debug Panel Kurulum Aracı.
    /// </summary>
    public static class ProtagonistAnimationSetup
    {
        public const string LOCOMOTION_DIR = "Assets/Animations/Locomotion";
        public const string CONTROLLER_PATH = "Assets/Characters/Protagonist/Protagonist_AnimatorController.controller";
        public const string REGISTRY_PATH = "Assets/Resources/AnimationLibraryRegistry.asset";
        public const string MASKS_DIR = "Assets/Animations/Masks";
        public const string PROTAGONIST_LEGACY_FBX = "Assets/Characters/Protagonist/Protagonist_Character.fbx";

        public struct ClipSpec
        {
            public string id;
            public string fileName;
            public string versionTag;
            public AnimationClipCategory category;
            public AnimationApprovalStatus status;
            public string targetLayer;
            public float recommendedSpeedMps;
            public string notes;
        }

        public static readonly ClipSpec[] AllClipSpecs = new[]
        {
            // ===================== MOTION PACK 04 (29 V2 CLIPS) =====================
            // Locomotion & Posture V2
            new ClipSpec { id = "Idle_Natural_V2", fileName = "Idle_Natural_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0f, notes = "Standing, fixed feet, mild weight shift" },
            new ClipSpec { id = "Walk_Grounded_V2", fileName = "Walk_Grounded_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.69f, notes = "Grounded forward walk gait" },
            new ClipSpec { id = "Walk_Backward_V2", fileName = "Walk_Backward_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.48f, notes = "Backward stepping gait" },
            new ClipSpec { id = "Crouch_Idle_V2", fileName = "Crouch_Idle_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0f, notes = "Stationary crouch, planted feet" },
            new ClipSpec { id = "Crouch_Walk_V2", fileName = "Crouch_Walk_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.37f, notes = "Low crouched walking gait" },
            new ClipSpec { id = "Crouch_Enter_V2", fileName = "Crouch_Enter_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0f, notes = "Lower pelvis with planted feet" },
            new ClipSpec { id = "Crouch_Exit_V2", fileName = "Crouch_Exit_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0f, notes = "Controlled stand-up transition" },

            // Flashlight V2
            new ClipSpec { id = "Flashlight_Hold_V2", fileName = "Flashlight_Hold_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "Standing flashlight hold + dynamic hand IK" },
            new ClipSpec { id = "Flashlight_Walk_V2", fileName = "Flashlight_Walk_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0.69f, notes = "Walking flashlight gait with steady wrist" },
            new ClipSpec { id = "Flashlight_Slow_V2", fileName = "Flashlight_Slow_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0.41f, notes = "Slow cautious flashlight gait" },
            new ClipSpec { id = "Flashlight_Run_V2", fileName = "Flashlight_Run_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 1.31f, notes = "Preview only per manifest (no true flight phase)" },
            new ClipSpec { id = "Flashlight_CrouchIdle_V2", fileName = "Flashlight_CrouchIdle_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "Stationary crouch holding flashlight" },
            new ClipSpec { id = "Flashlight_CrouchWalk_V2", fileName = "Flashlight_CrouchWalk_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0.37f, notes = "Crouched flashlight gait" },
            new ClipSpec { id = "Flashlight_Raise_V2", fileName = "Flashlight_Raise_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "Calibrated flashlight raise" },
            new ClipSpec { id = "Flashlight_Lower_V2", fileName = "Flashlight_Lower_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "Calibrated flashlight lower" },
            new ClipSpec { id = "Flashlight_Release_V2", fileName = "Flashlight_Release_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "Release event at 0.48s (60%)" },
            new ClipSpec { id = "Flashlight_Pickup_V2", fileName = "Flashlight_Pickup_V2.fbx", versionTag = "V2", category = AnimationClipCategory.FullBodyAction, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, recommendedSpeedMps = 0f, notes = "Preview/target-IK prototype, attach at 1.35s (54%)" },

            // Head & Body Orientation V2
            new ClipSpec { id = "Look_Down_V2", fileName = "Look_Down_V2.fbx", versionTag = "V2", category = AnimationClipCategory.UpperBody, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_HEAD_LOOK, recommendedSpeedMps = 0f, notes = "Scripted/reference look down pose" },
            new ClipSpec { id = "Look_Up_V2", fileName = "Look_Up_V2.fbx", versionTag = "V2", category = AnimationClipCategory.UpperBody, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_HEAD_LOOK, recommendedSpeedMps = 0f, notes = "Scripted/reference look up pose" },
            new ClipSpec { id = "Look_Left_V2", fileName = "Look_Left_V2.fbx", versionTag = "V2", category = AnimationClipCategory.UpperBody, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_HEAD_LOOK, recommendedSpeedMps = 0f, notes = "Scripted/reference look left pose" },
            new ClipSpec { id = "Look_Right_V2", fileName = "Look_Right_V2.fbx", versionTag = "V2", category = AnimationClipCategory.UpperBody, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_HEAD_LOOK, recommendedSpeedMps = 0f, notes = "Scripted/reference look right pose" },
            new ClipSpec { id = "Turn_Left_45_V2", fileName = "Turn_Left_45_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0f, notes = "Two-step turn-in-place left coordinated with controller yaw" },
            new ClipSpec { id = "Turn_Right_45_V2", fileName = "Turn_Right_45_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0f, notes = "Two-step turn-in-place right coordinated with controller yaw" },

            // Reactions & Interactions V2
            new ClipSpec { id = "Fear_Idle_V2", fileName = "Fear_Idle_V2.fbx", versionTag = "V2", category = AnimationClipCategory.UpperBody, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_UPPER_BODY, recommendedSpeedMps = 0f, notes = "Guarded flashlight fear posture" },
            new ClipSpec { id = "Startle_Reaction_V2", fileName = "Startle_Reaction_V2.fbx", versionTag = "V2", category = AnimationClipCategory.FullBodyAction, status = AnimationApprovalStatus.ApprovedForGameplay, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, recommendedSpeedMps = 0f, notes = "Fast recoil & protective left arm" },
            new ClipSpec { id = "Wall_Support_V2", fileName = "Wall_Support_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Interaction, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_LEFT_ARM, recommendedSpeedMps = 0f, notes = "Left-arm wall brace + surface IK" },
            new ClipSpec { id = "Door_Open_Left_V2", fileName = "Door_Open_Left_V2.fbx", versionTag = "V2", category = AnimationClipCategory.Interaction, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_LEFT_ARM, recommendedSpeedMps = 0f, notes = "Left-hand door reach/pull + handle IK (0.76s/1.30s)" },
            new ClipSpec { id = "Fall_Forward_V2", fileName = "Fall_Forward_V2.fbx", versionTag = "V2", category = AnimationClipCategory.FullBodyAction, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, recommendedSpeedMps = 0f, notes = "Preview only per manifest (release 0.45s, impact 1.0s)" },
            new ClipSpec { id = "Get_Up_V2", fileName = "Get_Up_V2.fbx", versionTag = "V2", category = AnimationClipCategory.FullBodyAction, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, recommendedSpeedMps = 0f, notes = "Preview only per manifest (push-up to crouch to stand)" },

            // ===================== PREVIOUS LIBRARY (23 V1 CLIPS PRESERVED) =====================
            new ClipSpec { id = "Idle_Breathing", fileName = "Idle_Breathing.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0f, notes = "V1 idle alternative (superseded by Idle_Natural_V2)" },
            new ClipSpec { id = "Walk_Slow_unity", fileName = "Walk_Slow_unity.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.45f, notes = "V1 slow walk alternative" },
            new ClipSpec { id = "Walk_Forward_unity", fileName = "Walk_Forward_unity.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.70f, notes = "V1 forward walk (superseded by Walk_Grounded_V2)" },
            new ClipSpec { id = "Walk_Reverse_unity", fileName = "Walk_Reverse_unity.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.50f, notes = "V1 reverse walk (superseded by Walk_Backward_V2)" },
            new ClipSpec { id = "Strafe_Left", fileName = "Strafe_Left.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.65f, notes = "Active left strafe clip (no V2 replacement)" },
            new ClipSpec { id = "Strafe_Right", fileName = "Strafe_Right.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.65f, notes = "Active right strafe clip (no V2 replacement)" },
            new ClipSpec { id = "Run_Forward", fileName = "Run_Forward.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 1.60f, notes = "Active sprint clip (Flashlight_Run_V2 is preview-only)" },
            new ClipSpec { id = "Crouch_Enter", fileName = "Crouch_Enter.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0f, notes = "V1 crouch enter (superseded by Crouch_Enter_V2)" },
            new ClipSpec { id = "Flashlight_CrouchWalk", fileName = "Flashlight_CrouchWalk.fbx", versionTag = "V1", category = AnimationClipCategory.Locomotion, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_BASE, recommendedSpeedMps = 0.37f, notes = "V1 crouch walk (superseded by V2)" },
            new ClipSpec { id = "Flashlight_Fear", fileName = "Flashlight_Fear.fbx", versionTag = "V1", category = AnimationClipCategory.UpperBody, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_UPPER_BODY, recommendedSpeedMps = 0f, notes = "V1 fear pose (superseded by Fear_Idle_V2)" },
            new ClipSpec { id = "Look_Right", fileName = "Look_Right.fbx", versionTag = "V1", category = AnimationClipCategory.UpperBody, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_HEAD_LOOK, recommendedSpeedMps = 0f, notes = "V1 look right (superseded by Look_Right_V2)" },
            new ClipSpec { id = "Flashlight_Hold", fileName = "Flashlight_Hold.fbx", versionTag = "V1", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "V1 flashlight hold (superseded by Flashlight_Hold_V2)" },
            new ClipSpec { id = "Flashlight_Slow", fileName = "Flashlight_Slow.fbx", versionTag = "V1", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0.41f, notes = "V1 flashlight slow (superseded by Flashlight_Slow_V2)" },
            new ClipSpec { id = "Flashlight_Walk", fileName = "Flashlight_Walk.fbx", versionTag = "V1", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0.69f, notes = "V1 flashlight walk (superseded by Flashlight_Walk_V2)" },
            new ClipSpec { id = "Flashlight_Run", fileName = "Flashlight_Run.fbx", versionTag = "V1", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 1.60f, notes = "Active right-arm sprint clip matched with Run_Forward" },
            new ClipSpec { id = "Flashlight_Raise", fileName = "Flashlight_Raise.fbx", versionTag = "V1", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "V1 flashlight raise (superseded by V2)" },
            new ClipSpec { id = "Flashlight_Lower", fileName = "Flashlight_Lower.fbx", versionTag = "V1", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "V1 flashlight lower (superseded by V2)" },
            new ClipSpec { id = "Flashlight_Release", fileName = "Flashlight_Release.fbx", versionTag = "V1", category = AnimationClipCategory.Flashlight, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, recommendedSpeedMps = 0f, notes = "V1 flashlight release (superseded by V2)" },
            new ClipSpec { id = "Door_Open_Left", fileName = "Door_Open_Left.fbx", versionTag = "V1", category = AnimationClipCategory.Interaction, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_LEFT_ARM, recommendedSpeedMps = 0f, notes = "V1 door open (superseded by Door_Open_Left_V2)" },
            new ClipSpec { id = "Flashlight_Pickup", fileName = "Flashlight_Pickup.fbx", versionTag = "V1", category = AnimationClipCategory.FullBodyAction, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, recommendedSpeedMps = 0f, notes = "V1 flashlight pickup" },
            new ClipSpec { id = "Stumble_Recover", fileName = "Stumble_Recover.fbx", versionTag = "V1", category = AnimationClipCategory.FullBodyAction, status = AnimationApprovalStatus.ApprovedWithProceduralCorrection, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, recommendedSpeedMps = 0f, notes = "Active stumble recovery clip" },
            new ClipSpec { id = "Fall_Forward", fileName = "Fall_Forward.fbx", versionTag = "V1", category = AnimationClipCategory.FullBodyAction, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, recommendedSpeedMps = 0f, notes = "V1 fall prototype" },
            new ClipSpec { id = "Get_Up", fileName = "Get_Up.fbx", versionTag = "V1", category = AnimationClipCategory.FullBodyAction, status = AnimationApprovalStatus.DebugPreviewOnly, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, recommendedSpeedMps = 0f, notes = "V1 get-up prototype" }
        };

        [MenuItem("The Last Bell/Animasyon/Modüler 7 Katmanlı Animasyon Sistemini Kur", false, 21)]
        public static void SetupAllMenu()
        {
            SetupAll();
            EditorUtility.DisplayDialog("Animasyon Sistemi Güncellendi",
                "52 animasyon klibi (29 V2 + 23 V1), 7 katmanlı Animator, CharacterProceduralController, True FPS ve Debug Panel yapılandırıldı.",
                "Tamam");
        }

        public static void SetupAll()
        {
            FixLegacyProtagonistFbxGrounding();
            Dictionary<string, AnimationClip> clipMap = ImportAndLoadAllClips();
            AnimationLibraryRegistry registry = CreateOrUpdateRegistry(clipMap);

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
            RebuildBaseLocomotionLayer(controller, clipMap);

            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_UPPER_BODY, upperMask, AnimatorLayerBlendingMode.Override, 0.0f, false, sm => BuildUpperBodyStates(sm, clipMap));
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_RIGHT_ARM, rightArmMask, AnimatorLayerBlendingMode.Override, 1.0f, true, sm => BuildRightArmStates(sm, clipMap, controller));
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_LEFT_ARM, leftArmMask, AnimatorLayerBlendingMode.Override, 0.0f, true, sm => BuildLeftArmStates(sm, clipMap));
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_HEAD_LOOK, headMask, AnimatorLayerBlendingMode.Override, 0.0f, true, sm => BuildHeadLookStates(sm, clipMap));
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_ADDITIVE, upperMask, AnimatorLayerBlendingMode.Additive, 0.0f, false, sm => BuildAdditiveStates(sm, clipMap));
            EnsureModularLayer(controller, ProtagonistAnimationController.LAYER_FULL_OVERRIDE, null, AnimatorLayerBlendingMode.Override, 0.0f, true, sm => BuildFullOverrideStates(sm, clipMap));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            WireSceneComponents(controller, registry);
        }

        private static void FixLegacyProtagonistFbxGrounding()
        {
            if (!File.Exists(PROTAGONIST_LEGACY_FBX)) return;
            var importer = AssetImporter.GetAtPath(PROTAGONIST_LEGACY_FBX) as ModelImporter;
            if (importer == null) return;

            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                if (!c.heightFromFeet || !c.lockRootHeightY)
                {
                    c.lockRootHeightY = true;
                    c.heightFromFeet = true;
                    c.keepOriginalPositionY = false;
                    clips[i] = c;
                    changed = true;
                }
            }
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static Dictionary<string, AnimationClip> ImportAndLoadAllClips()
        {
            var result = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

            foreach (var spec in AllClipSpecs)
            {
                string fbxPath = $"{LOCOMOTION_DIR}/{spec.fileName}";
                if (!File.Exists(fbxPath))
                {
                    Debug.LogError($"[ProtagonistAnimationSetup] Eksik animasyon FBX: {fbxPath}");
                    continue;
                }

                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                }

                if (importer != null)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = true;
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.autoGenerateAvatarMappingIfUnspecified = true;
                    importer.animationCompression = ModelImporterAnimationCompression.Optimal;

                    var clips = importer.clipAnimations;
                    if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
                    if (clips != null && clips.Length > 0)
                    {
                        for (int i = 0; i < clips.Length; i++)
                        {
                            clips[i] = AnimationImportPipeline.ConfigureClipSettings(spec.id, fbxPath, clips[i]);
                        }
                        importer.clipAnimations = clips;
                    }
                    importer.SaveAndReimport();

                    // Yalnızca geçerli Humanoid kemik eşlemesi (human >= 15 kemik) zaten mevcutsa bacak T-Pose kalibrasyonu uygula;
                    // aksi halde boş human=[] yazarak Avatar'ı geçersiz kılma!
                    HumanDescription hd = importer.humanDescription;
                    if (hd.human != null && hd.human.Length >= 15 && hd.skeleton != null && hd.skeleton.Length > 0)
                    {
                        bool poseFixed = false;
                        for (int b = 0; b < hd.skeleton.Length; b++)
                        {
                            string bn = hd.skeleton[b].name;
                            if (bn == "LeftUpperLeg" || bn == "RightUpperLeg")
                            {
                                hd.skeleton[b].rotation = Quaternion.Euler(180f, 0f, 0f);
                                poseFixed = true;
                            }
                            else if (bn == "LeftLowerLeg" || bn == "RightLowerLeg")
                            {
                                hd.skeleton[b].rotation = Quaternion.identity;
                                poseFixed = true;
                            }
                            else if (bn == "LeftFoot" || bn == "RightFoot")
                            {
                                hd.skeleton[b].rotation = Quaternion.Euler(-90f, 0f, 0f);
                                poseFixed = true;
                            }
                        }
                        if (poseFixed)
                        {
                            importer.humanDescription = hd;
                            importer.SaveAndReimport();
                        }
                    }
                }

                AnimationClip clip = LoadPrimaryClip(fbxPath);
                if (clip != null) result[spec.id] = clip;
                else Debug.LogError($"[ProtagonistAnimationSetup] AnimationClip yüklenemedi: {fbxPath}");
            }

            Debug.Log($"[ProtagonistAnimationSetup] {result.Count}/{AllClipSpecs.Length} animasyon klibi (V2+V1) içe aktarıldı.");
            return result;
        }

        private static AnimationLibraryRegistry CreateOrUpdateRegistry(Dictionary<string, AnimationClip> clipMap)
        {
            string dir = Path.GetDirectoryName(REGISTRY_PATH);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var registry = AssetDatabase.LoadAssetAtPath<AnimationLibraryRegistry>(REGISTRY_PATH);
            bool isNew = (registry == null);
            if (isNew) registry = ScriptableObject.CreateInstance<AnimationLibraryRegistry>();

            var entries = new List<RegisteredAnimationEntry>();
            foreach (var spec in AllClipSpecs)
            {
                clipMap.TryGetValue(spec.id, out AnimationClip clip);
                entries.Add(new RegisteredAnimationEntry
                {
                    id = spec.id,
                    displayName = spec.id.Replace("_unity", "").Replace("_V2", "").Replace("_", " "),
                    versionTag = spec.versionTag,
                    category = spec.category,
                    approvalStatus = spec.status,
                    targetLayer = spec.targetLayer,
                    defaultLoop = AnimationImportPipeline.ShouldLoop(spec.id, $"{LOCOMOTION_DIR}/{spec.fileName}"),
                    usesRootHeightBakeFromFeet = AnimationImportPipeline.UsesHeightFromFeet(spec.id),
                    recommendedSpeedMps = spec.recommendedSpeedMps,
                    notes = spec.notes,
                    clip = clip
                });
            }

            registry.SetEntries(entries);
            if (isNew) AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
            else EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<AnimationLibraryRegistry>(REGISTRY_PATH);
        }

        private static AnimationClip LoadPrimaryClip(string fbxPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            if (assets == null) return null;
            return assets
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
        }

        private static AnimationClip GetClip(Dictionary<string, AnimationClip> map, string id, string fallbackId = "Idle_Natural_V2")
        {
            if (map.TryGetValue(id, out var c) && c != null) return c;
            if (!string.IsNullOrEmpty(fallbackId) && map.TryGetValue(fallbackId, out var fb)) return fb;
            return null;
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

            EnsureParam(controller, "IsMoving", AnimatorControllerParameterType.Bool, false);
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
            EnsureParam(controller, "WallSupport", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "ImpactFlinch", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "Startle", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "TurnLeft", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "TurnRight", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "Hit", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "Die", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "LayToIdle", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "Fall", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "GetUp", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "Stumble", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "FlashlightRaise", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "FlashlightLower", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "FlashlightRelease", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "FlashlightPickup", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "LookRight", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "LookLeft", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "LookUp", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "LookDown", AnimatorControllerParameterType.Trigger, false);
        }

        private static void EnsureParam(AnimatorController controller, string name, AnimatorControllerParameterType type, object defaultVal)
        {
            if (controller.parameters.Any(p => p.name == name)) return;
            var p = new AnimatorControllerParameter { name = name, type = type };
            if (type == AnimatorControllerParameterType.Float) p.defaultFloat = Convert.ToSingle(defaultVal);
            else if (type == AnimatorControllerParameterType.Bool) p.defaultBool = Convert.ToBoolean(defaultVal);
            controller.AddParameter(p);
        }

        private static void ClearStateMachine(AnimatorStateMachine sm)
        {
            foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
            foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
            foreach (var sub in sm.stateMachines.ToArray()) sm.RemoveStateMachine(sub.stateMachine);
        }

        private static void RebuildBaseLocomotionLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            var layers = controller.layers;
            if (layers == null || layers.Length == 0) return;

            layers[0].iKPass = true;
            var sm = layers[0].stateMachine;
            ClearStateMachine(sm);

            // 1. 2D Freeform Directional Standing Locomotion (V2 Idle/Walk/Back/Slow + V1 Strafe/Run)
            var locoTree = new BlendTree
            {
                name = "Locomotion_2D_BlendTree",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "Strafe",
                blendParameterY = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(locoTree, controller);

            locoTree.AddChild(GetClip(clips, "Idle_Natural_V2"), new Vector2(0f, 0f));
            locoTree.AddChild(GetClip(clips, "Flashlight_Slow_V2"), new Vector2(0f, 0.45f));
            locoTree.AddChild(GetClip(clips, "Walk_Grounded_V2"), new Vector2(0f, 1.0f));
            locoTree.AddChild(GetClip(clips, "Run_Forward"), new Vector2(0f, 2.0f));
            locoTree.AddChild(GetClip(clips, "Walk_Backward_V2"), new Vector2(0f, -1.0f));
            locoTree.AddChild(GetClip(clips, "Strafe_Left"), new Vector2(-1.0f, 0f));
            locoTree.AddChild(GetClip(clips, "Strafe_Right"), new Vector2(1.0f, 0f));

            var locoState = sm.AddState("Locomotion", new Vector3(260f, 80f, 0f));
            locoState.motion = locoTree;
            locoState.iKOnFeet = true;
            locoState.speedParameterActive = true;
            locoState.speedParameter = "LocomotionSpeedMultiplier";
            sm.defaultState = locoState;

            // 2. Doğru Sabit Çömelme (Crouch_Idle_V2) vs Çömelerek Yürüme (Crouch_Walk_V2) + Giriş/Çıkış
            var crouchEnter = sm.AddState("Crouch_Enter", new Vector3(260f, 210f, 0f));
            crouchEnter.motion = GetClip(clips, "Crouch_Enter_V2");
            crouchEnter.iKOnFeet = true;

            var crouchIdle = sm.AddState("Crouch_Idle", new Vector3(500f, 210f, 0f));
            crouchIdle.motion = GetClip(clips, "Crouch_Idle_V2");
            crouchIdle.iKOnFeet = true;

            var crouchWalk = sm.AddState("Crouch_Locomotion", new Vector3(740f, 210f, 0f));
            crouchWalk.motion = GetClip(clips, "Crouch_Walk_V2");
            crouchWalk.iKOnFeet = true;
            crouchWalk.speedParameterActive = true;
            crouchWalk.speedParameter = "LocomotionSpeedMultiplier";

            var crouchExit = sm.AddState("Crouch_Exit", new Vector3(500f, 80f, 0f));
            crouchExit.motion = GetClip(clips, "Crouch_Exit_V2");
            crouchExit.iKOnFeet = true;

            var toCrouch = locoState.AddTransition(crouchEnter);
            toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");
            toCrouch.hasExitTime = false;
            toCrouch.duration = 0.14f;

            var enterToIdle = crouchEnter.AddTransition(crouchIdle);
            enterToIdle.hasExitTime = true;
            enterToIdle.exitTime = 0.85f;
            enterToIdle.duration = 0.14f;

            var enterToStand = crouchEnter.AddTransition(locoState);
            enterToStand.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            enterToStand.hasExitTime = false;
            enterToStand.duration = 0.16f;

            var crouchIdleToWalk = crouchIdle.AddTransition(crouchWalk);
            crouchIdleToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            crouchIdleToWalk.hasExitTime = false;
            crouchIdleToWalk.duration = 0.16f;

            var crouchWalkToIdle = crouchWalk.AddTransition(crouchIdle);
            crouchWalkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            crouchWalkToIdle.hasExitTime = false;
            crouchWalkToIdle.duration = 0.16f;

            var crouchIdleToExit = crouchIdle.AddTransition(crouchExit);
            crouchIdleToExit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            crouchIdleToExit.hasExitTime = false;
            crouchIdleToExit.duration = 0.14f;

            var crouchWalkToExit = crouchWalk.AddTransition(crouchExit);
            crouchWalkToExit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            crouchWalkToExit.hasExitTime = false;
            crouchWalkToExit.duration = 0.14f;

            var exitToStand = crouchExit.AddTransition(locoState);
            exitToStand.hasExitTime = true;
            exitToStand.exitTime = 0.85f;
            exitToStand.duration = 0.16f;

            // 3. Yerinde Dönüş (Turn-in-Place) Durumları
            var turnLeft = sm.AddState("Turn_Left_45", new Vector3(80f, -60f, 0f));
            turnLeft.motion = GetClip(clips, "Turn_Left_45_V2");
            turnLeft.iKOnFeet = true;

            var turnRight = sm.AddState("Turn_Right_45", new Vector3(440f, -60f, 0f));
            turnRight.motion = GetClip(clips, "Turn_Right_45_V2");
            turnRight.iKOnFeet = true;

            var toTurnL = locoState.AddTransition(turnLeft);
            toTurnL.AddCondition(AnimatorConditionMode.If, 0f, "TurnLeft");
            toTurnL.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            toTurnL.hasExitTime = false;
            toTurnL.duration = 0.14f;

            var turnLBack = turnLeft.AddTransition(locoState);
            turnLBack.hasExitTime = true;
            turnLBack.exitTime = 0.90f;
            turnLBack.duration = 0.18f;

            var turnLInterrupt = turnLeft.AddTransition(locoState);
            turnLInterrupt.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            turnLInterrupt.hasExitTime = false;
            turnLInterrupt.duration = 0.12f;

            var toTurnR = locoState.AddTransition(turnRight);
            toTurnR.AddCondition(AnimatorConditionMode.If, 0f, "TurnRight");
            toTurnR.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            toTurnR.hasExitTime = false;
            toTurnR.duration = 0.14f;

            var turnRBack = turnRight.AddTransition(locoState);
            turnRBack.hasExitTime = true;
            turnRBack.exitTime = 0.90f;
            turnRBack.duration = 0.18f;

            var turnRInterrupt = turnRight.AddTransition(locoState);
            turnRInterrupt.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            turnRInterrupt.hasExitTime = false;
            turnRInterrupt.duration = 0.12f;

            controller.layers = layers;
        }

        private static void EnsureModularLayer(
            AnimatorController controller,
            string layerName,
            AvatarMask mask,
            AnimatorLayerBlendingMode blendingMode,
            float defaultWeight,
            bool ikPass,
            Action<AnimatorStateMachine> buildStates)
        {
            var layers = controller.layers.ToList();
            int idx = layers.FindIndex(l => l.name == layerName);

            AnimatorControllerLayer layer;
            if (idx < 0)
            {
                var sm = new AnimatorStateMachine { name = $"{layerName}_SM", hideFlags = HideFlags.HideInHierarchy };
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
            else
            {
                layer = layers[idx];
                layer.avatarMask = mask;
                layer.blendingMode = blendingMode;
                layer.defaultWeight = defaultWeight;
                layer.iKPass = ikPass;
                layers[idx] = layer;
            }

            ClearStateMachine(layer.stateMachine);
            buildStates?.Invoke(layer.stateMachine);
            controller.layers = layers.ToArray();
        }

        private static void BuildUpperBodyStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips)
        {
            var idle = sm.AddState("Idle_Upper", new Vector3(240f, 80f, 0f));
            idle.motion = GetClip(clips, "Idle_Natural_V2");
            var fear = sm.AddState("Fear_Idle", new Vector3(500f, 80f, 0f));
            fear.motion = GetClip(clips, "Fear_Idle_V2");
            sm.defaultState = idle;

            var toFear = idle.AddTransition(fear);
            toFear.AddCondition(AnimatorConditionMode.Greater, 0.35f, "FearLevel");
            toFear.hasExitTime = false;
            toFear.duration = 0.25f;

            var toIdle = fear.AddTransition(idle);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.25f, "FearLevel");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.25f;
        }

        private static void BuildRightArmStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips, AnimatorController controller)
        {
            var flashTree = new BlendTree
            {
                name = "Flashlight_RightArm_BlendTree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(flashTree, controller);
            flashTree.AddChild(GetClip(clips, "Flashlight_Hold_V2"), 0.0f);
            flashTree.AddChild(GetClip(clips, "Flashlight_Slow_V2"), 0.45f);
            flashTree.AddChild(GetClip(clips, "Flashlight_Walk_V2"), 1.0f);
            flashTree.AddChild(GetClip(clips, "Flashlight_Run"), 2.0f);

            var hold = sm.AddState("Flashlight_Locomotion", new Vector3(260f, 80f, 0f));
            hold.motion = flashTree;
            sm.defaultState = hold;

            var crouchHoldIdle = sm.AddState("Flashlight_CrouchIdle", new Vector3(260f, -40f, 0f));
            crouchHoldIdle.motion = GetClip(clips, "Flashlight_CrouchIdle_V2");

            var crouchHoldWalk = sm.AddState("Flashlight_CrouchWalk", new Vector3(540f, -40f, 0f));
            crouchHoldWalk.motion = GetClip(clips, "Flashlight_CrouchWalk_V2");

            var empty = sm.AddState("Flashlight_Empty", new Vector3(540f, 80f, 0f));
            empty.motion = GetClip(clips, "Idle_Natural_V2");

            var raise = sm.AddState("Flashlight_Raise", new Vector3(260f, 200f, 0f));
            raise.motion = GetClip(clips, "Flashlight_Raise_V2");

            var lower = sm.AddState("Flashlight_Lower", new Vector3(540f, 200f, 0f));
            lower.motion = GetClip(clips, "Flashlight_Lower_V2");

            var release = sm.AddState("Flashlight_Release", new Vector3(540f, 300f, 0f));
            release.motion = GetClip(clips, "Flashlight_Release_V2");

            // Standing <-> Crouched Flashlight states
            var toCrouchIdle = hold.AddTransition(crouchHoldIdle);
            toCrouchIdle.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");
            toCrouchIdle.hasExitTime = false;
            toCrouchIdle.duration = 0.16f;

            var cIdleToWalk = crouchHoldIdle.AddTransition(crouchHoldWalk);
            cIdleToWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            cIdleToWalk.hasExitTime = false;
            cIdleToWalk.duration = 0.16f;

            var cWalkToIdle = crouchHoldWalk.AddTransition(crouchHoldIdle);
            cWalkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");
            cWalkToIdle.hasExitTime = false;
            cWalkToIdle.duration = 0.16f;

            var cIdleToStand = crouchHoldIdle.AddTransition(hold);
            cIdleToStand.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            cIdleToStand.hasExitTime = false;
            cIdleToStand.duration = 0.16f;

            var cWalkToStand = crouchHoldWalk.AddTransition(hold);
            cWalkToStand.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            cWalkToStand.hasExitTime = false;
            cWalkToStand.duration = 0.16f;

            var toEmpty = hold.AddTransition(empty);
            toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0f, "FlashlightHeld");
            toEmpty.hasExitTime = false;
            toEmpty.duration = 0.18f;

            var toHold = empty.AddTransition(hold);
            toHold.AddCondition(AnimatorConditionMode.If, 0f, "FlashlightHeld");
            toHold.hasExitTime = false;
            toHold.duration = 0.18f;

            AddTriggerState(sm, hold, raise, "FlashlightRaise");
            AddTriggerState(sm, empty, lower, "FlashlightLower");
            AddTriggerState(sm, empty, release, "FlashlightRelease");
        }

        private static void BuildLeftArmStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips)
        {
            var idle = sm.AddState("LeftArm_Idle", new Vector3(240f, 80f, 0f));
            idle.motion = GetClip(clips, "Idle_Natural_V2");
            var doorOpen = sm.AddState("Door_Open", new Vector3(500f, 60f, 0f));
            doorOpen.motion = GetClip(clips, "Door_Open_Left_V2");
            var wallSupport = sm.AddState("Wall_Support", new Vector3(500f, 160f, 0f));
            wallSupport.motion = GetClip(clips, "Wall_Support_V2");
            sm.defaultState = idle;

            AddTriggerState(sm, idle, doorOpen, "DoorOpen");
            AddTriggerState(sm, idle, doorOpen, "Interact");
            AddTriggerState(sm, idle, wallSupport, "WallSupport");
        }

        private static void BuildHeadLookStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips)
        {
            var neutral = sm.AddState("Look_Neutral", new Vector3(240f, 80f, 0f));
            neutral.motion = GetClip(clips, "Idle_Natural_V2");
            var lookRight = sm.AddState("Look_Right", new Vector3(500f, 20f, 0f));
            lookRight.motion = GetClip(clips, "Look_Right_V2");
            var lookLeft = sm.AddState("Look_Left", new Vector3(500f, 90f, 0f));
            lookLeft.motion = GetClip(clips, "Look_Left_V2");
            var lookUp = sm.AddState("Look_Up", new Vector3(500f, 160f, 0f));
            lookUp.motion = GetClip(clips, "Look_Up_V2");
            var lookDown = sm.AddState("Look_Down", new Vector3(500f, 230f, 0f));
            lookDown.motion = GetClip(clips, "Look_Down_V2");
            sm.defaultState = neutral;

            AddTriggerState(sm, neutral, lookRight, "LookRight");
            AddTriggerState(sm, neutral, lookLeft, "LookLeft");
            AddTriggerState(sm, neutral, lookUp, "LookUp");
            AddTriggerState(sm, neutral, lookDown, "LookDown");
        }

        private static void BuildAdditiveStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips)
        {
            var breathing = sm.AddState("Breathing_Additive", new Vector3(240f, 80f, 0f));
            breathing.motion = GetClip(clips, "Idle_Natural_V2");
            var flinch = sm.AddState("Impact_Flinch", new Vector3(500f, 80f, 0f));
            flinch.motion = GetClip(clips, "Startle_Reaction_V2");
            sm.defaultState = breathing;

            AddTriggerState(sm, breathing, flinch, "ImpactFlinch");
        }

        private static void BuildFullOverrideStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips)
        {
            var none = sm.AddState("None", new Vector3(240f, 80f, 0f));
            none.motion = GetClip(clips, "Idle_Natural_V2");
            sm.defaultState = none;

            var startle = sm.AddState("Startle", new Vector3(520f, -40f, 0f));
            startle.motion = GetClip(clips, "Startle_Reaction_V2");

            var fall = sm.AddState("Fall", new Vector3(520f, 40f, 0f));
            fall.motion = GetClip(clips, "Fall_Forward_V2");

            var getUp = sm.AddState("GetUp", new Vector3(520f, 130f, 0f));
            getUp.motion = GetClip(clips, "Get_Up_V2");

            var stumble = sm.AddState("Stumble", new Vector3(520f, 220f, 0f));
            stumble.motion = GetClip(clips, "Stumble_Recover");

            var pickup = sm.AddState("Flashlight_Pickup", new Vector3(520f, 310f, 0f));
            pickup.motion = GetClip(clips, "Flashlight_Pickup_V2");

            var cinematic = sm.AddState("Cinematic_Override", new Vector3(240f, 220f, 0f));
            cinematic.motion = GetClip(clips, "Idle_Natural_V2");

            AddTriggerState(sm, none, startle, "Startle");
            AddTriggerState(sm, none, fall, "Fall");
            AddTriggerState(sm, none, getUp, "GetUp");
            AddTriggerState(sm, none, stumble, "Stumble");
            AddTriggerState(sm, none, pickup, "FlashlightPickup");

            var fallToGetUp = fall.AddTransition(getUp);
            fallToGetUp.AddCondition(AnimatorConditionMode.If, 0f, "GetUp");
            fallToGetUp.duration = 0.18f;
            fallToGetUp.hasExitTime = false;

            var toCine = none.AddTransition(cinematic);
            toCine.AddCondition(AnimatorConditionMode.If, 0f, "CinematicActive");
            toCine.duration = 0.2f;
            toCine.hasExitTime = false;

            var fromCine = cinematic.AddTransition(none);
            fromCine.AddCondition(AnimatorConditionMode.IfNot, 0f, "CinematicActive");
            fromCine.duration = 0.2f;
            fromCine.hasExitTime = false;
        }

        private static void AddTriggerState(AnimatorStateMachine sm, AnimatorState returnState, AnimatorState actionState, string triggerName)
        {
            var toAct = sm.AddAnyStateTransition(actionState);
            toAct.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            toAct.duration = 0.12f;

            var back = actionState.AddTransition(returnState);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.16f;
        }

        private static void WireSceneComponents(AnimatorController controller, AnimationLibraryRegistry registry)
        {
            string scenePath = "Assets/Scenes/AbandonedSchool.unity";
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var player = GameObject.Find("Player_Protagonist");
            ProtagonistAnimationController animCtrl = null;
            CharacterProceduralController procCtrl = null;
            CameraModeController camModeCtrl = null;
            FirstPersonController fpc = null;
            FlashlightPhysicalItem physItem = null;

            // Bölüm 14: Dengeli, okunabilir korku ortam ışığı (çok karanlık sahneyi okunabilir hale getirir)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.22f);
            RenderSettings.ambientIntensity = 1.0f;

            if (player != null)
            {
                fpc = player.GetComponent<FirstPersonController>();
                var charModel = player.transform.Find("Character_Model");
                if (charModel != null)
                {
                    charModel.localPosition = Vector3.zero;
                    charModel.localRotation = Quaternion.identity;
                    charModel.localScale = Vector3.one;

                    foreach (var smr in charModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        smr.updateWhenOffscreen = true;
                    }
                }

                var anim = player.GetComponentInChildren<Animator>(true);
                var camPivot = player.transform.Find("Camera_Pivot");
                var cam = camPivot != null ? camPivot.Find("Main Camera") : null;
                var flashRig = camPivot != null ? camPivot.Find("Flashlight_Rig") : null;
                if (flashRig == null) flashRig = player.GetComponentInChildren<FlashlightController>(true)?.transform;
                var flashCtrl = flashRig != null ? flashRig.GetComponent<FlashlightController>() : null;

                if (anim != null)
                {
                    anim.runtimeAnimatorController = controller;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                    animCtrl = anim.GetComponent<ProtagonistAnimationController>();
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
                        physItem = flashRig.GetComponent<FlashlightPhysicalItem>();
                        if (physItem == null) physItem = flashRig.gameObject.AddComponent<FlashlightPhysicalItem>();

                        var soPhys = new SerializedObject(physItem);
                        var pRig = soPhys.FindProperty("cameraRigAttachPoint");
                        if (pRig != null && camPivot != null) pRig.objectReferenceValue = camPivot;
                        var pHand = soPhys.FindProperty("handAttachPoint");
                        if (pHand != null && animCtrl.RightHandSocket != null) pHand.objectReferenceValue = animCtrl.RightHandSocket;
                        var pFc = soPhys.FindProperty("flashlightController");
                        if (pFc != null) pFc.objectReferenceValue = flashCtrl;
                        var pAc = soPhys.FindProperty("animController");
                        if (pAc != null) pAc.objectReferenceValue = animCtrl;
                        soPhys.ApplyModifiedProperties();
                    }
                }

                // Birleşik Prosedürel Karakter Kontrolcüsü
                procCtrl = player.GetComponent<CharacterProceduralController>();
                if (procCtrl == null) procCtrl = player.AddComponent<CharacterProceduralController>();
                var soProc = new SerializedObject(procCtrl);
                if (fpc != null) soProc.FindProperty("playerController").objectReferenceValue = fpc;
                if (animCtrl != null) soProc.FindProperty("animController").objectReferenceValue = animCtrl;
                if (anim != null) soProc.FindProperty("animator").objectReferenceValue = anim;
                if (physItem != null) soProc.FindProperty("flashlightPhysical").objectReferenceValue = physItem;
                soProc.ApplyModifiedProperties();
                EditorUtility.SetDirty(procCtrl);

                // Kamera Modu Kontrolcüsü
                camModeCtrl = player.GetComponent<CameraModeController>();
                if (camModeCtrl == null) camModeCtrl = player.AddComponent<CameraModeController>();
                var soCam = new SerializedObject(camModeCtrl);
                if (cam != null) soCam.FindProperty("mainCamera").objectReferenceValue = cam.GetComponent<Camera>();
                if (camPivot != null) soCam.FindProperty("cameraPivot").objectReferenceValue = camPivot;
                if (charModel != null) soCam.FindProperty("characterModelRoot").objectReferenceValue = charModel;
                if (physItem != null) soCam.FindProperty("flashlightPhysical").objectReferenceValue = physItem;
                if (fpc != null) soCam.FindProperty("playerController").objectReferenceValue = fpc;
                if (procCtrl != null) soCam.FindProperty("proceduralController").objectReferenceValue = procCtrl;
                if (animCtrl != null) soCam.FindProperty("animController").objectReferenceValue = animCtrl;
                soCam.ApplyModifiedProperties();
                EditorUtility.SetDirty(camModeCtrl);
            }

            // AnimationDebugPanel bağlantısı
            var hudCanvas = GameObject.Find("Gameplay_HUD_Canvas");
            if (hudCanvas != null)
            {
                var debugPanel = hudCanvas.GetComponent<AnimationDebugPanel>();
                if (debugPanel == null) debugPanel = hudCanvas.AddComponent<AnimationDebugPanel>();
                debugPanel.SetRegistry(registry);

                var soPanel = new SerializedObject(debugPanel);
                soPanel.FindProperty("libraryRegistry").objectReferenceValue = registry;
                soPanel.FindProperty("animController").objectReferenceValue = animCtrl;
                soPanel.FindProperty("proceduralController").objectReferenceValue = procCtrl;
                soPanel.FindProperty("cameraModeController").objectReferenceValue = camModeCtrl;
                soPanel.FindProperty("playerController").objectReferenceValue = fpc;
                soPanel.FindProperty("flashlightPhysical").objectReferenceValue = physItem;
                soPanel.ApplyModifiedProperties();
                EditorUtility.SetDirty(debugPanel);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProtagonistAnimationSetup] AbandonedSchool.unity 52-klip animasyon, prosedürel kontrol, True FPS ve debug panel bağlantıları kaydedildi.");
        }
    }
}
