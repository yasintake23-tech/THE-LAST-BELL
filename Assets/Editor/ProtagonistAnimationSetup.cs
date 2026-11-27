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
    /// THE LAST BELL - Tam 23 Klipli Modüler 7 Katmanlı Karakter Animasyon, 3P Kamera ve Debug Panel Kurulum Aracı.
    /// - Tüm 23 animasyon FBX dosyasını Humanoid + doğru zemin hizalaması (Feet vs Original Y) + Animation Events ile içe aktarır.
    /// - AnimationLibraryRegistry.asset oluşturur/günceller.
    /// - Protagonist_AnimatorController.controller'ı (GUID korunarak) 7 katmanlı mimariyle yeniden kurar.
    /// - AbandonedSchool.unity içinde ProtagonistAnimationController, CameraModeController, FlashlightPhysicalItem ve AnimationDebugPanel'i bağlar.
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
            public AnimationClipCategory category;
            public string targetLayer;
            public string notes;
        }

        public static readonly ClipSpec[] AllClipSpecs = new[]
        {
            // Base Locomotion
            new ClipSpec { id = "Idle_Breathing", fileName = "Idle_Breathing.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "Ayakta nefes alma (Feet grounded, Loop)" },
            new ClipSpec { id = "Walk_Slow_unity", fileName = "Walk_Slow_unity.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "Temkinli yavaş yürüyüş (Feet grounded, Loop)" },
            new ClipSpec { id = "Walk_Forward_unity", fileName = "Walk_Forward_unity.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "İleri yürüyüş (Feet grounded, Loop)" },
            new ClipSpec { id = "Walk_Reverse_unity", fileName = "Walk_Reverse_unity.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "Geri yürüyüş (Feet grounded, Loop)" },
            new ClipSpec { id = "Strafe_Left", fileName = "Strafe_Left.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "Sola yan yürüyüş (Feet grounded, Loop)" },
            new ClipSpec { id = "Strafe_Right", fileName = "Strafe_Right.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "Sağa yan yürüyüş (Feet grounded, Loop)" },
            new ClipSpec { id = "Run_Forward", fileName = "Run_Forward.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "İleri koşu (Feet grounded, Loop)" },
            new ClipSpec { id = "Crouch_Enter", fileName = "Crouch_Enter.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "Çömelmeye giriş (Original Y, One-shot)" },
            new ClipSpec { id = "Flashlight_CrouchWalk", fileName = "Flashlight_CrouchWalk.fbx", category = AnimationClipCategory.Locomotion, targetLayer = ProtagonistAnimationController.LAYER_BASE, notes = "Fenerle çömelerek yürüyüş (Original Y, Loop)" },

            // Upper Body & Head
            new ClipSpec { id = "Flashlight_Fear", fileName = "Flashlight_Fear.fbx", category = AnimationClipCategory.UpperBody, targetLayer = ProtagonistAnimationController.LAYER_UPPER_BODY, notes = "Korku/gerilim üst gövde duruşu (Feet grounded, Loop)" },
            new ClipSpec { id = "Look_Right", fileName = "Look_Right.fbx", category = AnimationClipCategory.UpperBody, targetLayer = ProtagonistAnimationController.LAYER_HEAD_LOOK, notes = "Sağa bakış (Feet grounded, One-shot)" },

            // Right Arm Flashlight
            new ClipSpec { id = "Flashlight_Hold", fileName = "Flashlight_Hold.fbx", category = AnimationClipCategory.Flashlight, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, notes = "Fener tutma duruşu (Loop)" },
            new ClipSpec { id = "Flashlight_Slow", fileName = "Flashlight_Slow.fbx", category = AnimationClipCategory.Flashlight, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, notes = "Fenerle yavaş yürüyüş kol salınımı (Loop)" },
            new ClipSpec { id = "Flashlight_Walk", fileName = "Flashlight_Walk.fbx", category = AnimationClipCategory.Flashlight, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, notes = "Fenerle yürüyüş kol salınımı (Loop)" },
            new ClipSpec { id = "Flashlight_Run", fileName = "Flashlight_Run.fbx", category = AnimationClipCategory.Flashlight, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, notes = "Fenerle koşu kol salınımı (Loop)" },
            new ClipSpec { id = "Flashlight_Raise", fileName = "Flashlight_Raise.fbx", category = AnimationClipCategory.Flashlight, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, notes = "Feneri kaldırma (One-shot)" },
            new ClipSpec { id = "Flashlight_Lower", fileName = "Flashlight_Lower.fbx", category = AnimationClipCategory.Flashlight, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, notes = "Feneri indirme (One-shot)" },
            new ClipSpec { id = "Flashlight_Release", fileName = "Flashlight_Release.fbx", category = AnimationClipCategory.Flashlight, targetLayer = ProtagonistAnimationController.LAYER_RIGHT_ARM, notes = "Feneri bırakma - %55 event (One-shot)" },

            // Left Arm Interaction
            new ClipSpec { id = "Door_Open_Left", fileName = "Door_Open_Left.fbx", category = AnimationClipCategory.Interaction, targetLayer = ProtagonistAnimationController.LAYER_LEFT_ARM, notes = "Sol elle kapı açma - %45 event (One-shot)" },

            // Full Body Actions / Overrides
            new ClipSpec { id = "Flashlight_Pickup", fileName = "Flashlight_Pickup.fbx", category = AnimationClipCategory.FullBodyAction, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, notes = "Yerden fener alma - %55 event (Original Y, One-shot)" },
            new ClipSpec { id = "Stumble_Recover", fileName = "Stumble_Recover.fbx", category = AnimationClipCategory.FullBodyAction, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, notes = "Sendeleme ve toparlanma (Original Y, One-shot)" },
            new ClipSpec { id = "Fall_Forward", fileName = "Fall_Forward.fbx", category = AnimationClipCategory.FullBodyAction, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, notes = "Öne düşme - %70 darbe event (Original Y, One-shot)" },
            new ClipSpec { id = "Get_Up", fileName = "Get_Up.fbx", category = AnimationClipCategory.FullBodyAction, targetLayer = ProtagonistAnimationController.LAYER_FULL_OVERRIDE, notes = "Yerden kalkma (Original Y, One-shot)" }
        };

        [MenuItem("The Last Bell/Animasyon/Modüler 7 Katmanlı Animasyon Sistemini Kur", false, 21)]
        public static void SetupAllMenu()
        {
            SetupAll();
            EditorUtility.DisplayDialog("Animasyon Sistemi Güncellendi",
                "Tüm 23 animasyon klibi, 7 katmanlı Protagonist_AnimatorController, CameraModeController ve AnimationDebugPanel başarıyla yapılandırıldı.",
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

            // Varsayılan ağırlıklar: yalnızca Base Layer (1.0) ve RightArm_Flashlight (1.0) aktif; diğerleri 0.0 (T-pose/boş state çakışması yok)
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
                Debug.Log("[ProtagonistAnimationSetup] Legacy Protagonist_Character.fbx klipleri heightFromFeet=true olarak düzeltildi.");
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
                }

                AnimationClip clip = LoadPrimaryClip(fbxPath);
                if (clip != null)
                {
                    result[spec.id] = clip;
                }
                else
                {
                    Debug.LogError($"[ProtagonistAnimationSetup] AnimationClip yüklenemedi: {fbxPath}");
                }
            }

            Debug.Log($"[ProtagonistAnimationSetup] {result.Count}/{AllClipSpecs.Length} animasyon klibi başarıyla içe aktarıldı.");
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
                    displayName = spec.id.Replace("_unity", "").Replace("_", " "),
                    category = spec.category,
                    targetLayer = spec.targetLayer,
                    defaultLoop = AnimationImportPipeline.ShouldLoop(spec.id, $"{LOCOMOTION_DIR}/{spec.fileName}"),
                    usesRootHeightBakeFromFeet = AnimationImportPipeline.UsesHeightFromFeet(spec.id),
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

        private static AnimationClip GetClip(Dictionary<string, AnimationClip> map, string id, string fallbackId = "Idle_Breathing")
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
            EnsureParam(controller, "Stumble", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "FlashlightRaise", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "FlashlightLower", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "FlashlightRelease", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "FlashlightPickup", AnimatorControllerParameterType.Trigger, false);
            EnsureParam(controller, "LookRight", AnimatorControllerParameterType.Trigger, false);
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

            // 1. 2D Freeform Directional BlendTree (Idle_Breathing merkezde, tüm yönler kendi gerçek klibiyle)
            var locoTree = new BlendTree
            {
                name = "Locomotion_2D_BlendTree",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "Strafe",
                blendParameterY = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(locoTree, controller);

            locoTree.AddChild(GetClip(clips, "Idle_Breathing"), new Vector2(0f, 0f));
            locoTree.AddChild(GetClip(clips, "Walk_Slow_unity"), new Vector2(0f, 0.45f));
            locoTree.AddChild(GetClip(clips, "Walk_Forward_unity"), new Vector2(0f, 1.0f));
            locoTree.AddChild(GetClip(clips, "Run_Forward"), new Vector2(0f, 2.0f));
            locoTree.AddChild(GetClip(clips, "Walk_Reverse_unity"), new Vector2(0f, -1.0f));
            locoTree.AddChild(GetClip(clips, "Strafe_Left"), new Vector2(-1.0f, 0f));
            locoTree.AddChild(GetClip(clips, "Strafe_Right"), new Vector2(1.0f, 0f));

            var locoState = sm.AddState("Locomotion", new Vector3(260f, 80f, 0f));
            locoState.motion = locoTree;
            locoState.iKOnFeet = true;
            locoState.speedParameterActive = true;
            locoState.speedParameter = "LocomotionSpeedMultiplier";
            sm.defaultState = locoState;

            // 2. Çömelme giriş ve çömelme hareketi
            var crouchEnter = sm.AddState("Crouch_Enter", new Vector3(260f, 210f, 0f));
            crouchEnter.motion = GetClip(clips, "Crouch_Enter");
            crouchEnter.iKOnFeet = true;

            var crouchWalk = sm.AddState("Crouch_Locomotion", new Vector3(520f, 210f, 0f));
            crouchWalk.motion = GetClip(clips, "Flashlight_CrouchWalk");
            crouchWalk.iKOnFeet = true;
            crouchWalk.speedParameterActive = true;
            crouchWalk.speedParameter = "LocomotionSpeedMultiplier";

            var toCrouch = locoState.AddTransition(crouchEnter);
            toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");
            toCrouch.hasExitTime = false;
            toCrouch.duration = 0.16f;

            var enterToWalk = crouchEnter.AddTransition(crouchWalk);
            enterToWalk.hasExitTime = true;
            enterToWalk.exitTime = 0.80f;
            enterToWalk.duration = 0.15f;

            var enterToStand = crouchEnter.AddTransition(locoState);
            enterToStand.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            enterToStand.hasExitTime = false;
            enterToStand.duration = 0.18f;

            var crouchToStand = crouchWalk.AddTransition(locoState);
            crouchToStand.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
            crouchToStand.hasExitTime = false;
            crouchToStand.duration = 0.20f;

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
            idle.motion = GetClip(clips, "Idle_Breathing");
            var fear = sm.AddState("Flashlight_Fear", new Vector3(500f, 80f, 0f));
            fear.motion = GetClip(clips, "Flashlight_Fear");
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
            flashTree.AddChild(GetClip(clips, "Flashlight_Hold"), 0.0f);
            flashTree.AddChild(GetClip(clips, "Flashlight_Slow"), 0.45f);
            flashTree.AddChild(GetClip(clips, "Flashlight_Walk"), 1.0f);
            flashTree.AddChild(GetClip(clips, "Flashlight_Run"), 2.0f);

            var hold = sm.AddState("Flashlight_Locomotion", new Vector3(260f, 80f, 0f));
            hold.motion = flashTree;
            sm.defaultState = hold;

            var empty = sm.AddState("Flashlight_Empty", new Vector3(540f, 80f, 0f));
            empty.motion = GetClip(clips, "Idle_Breathing");

            var raise = sm.AddState("Flashlight_Raise", new Vector3(260f, 200f, 0f));
            raise.motion = GetClip(clips, "Flashlight_Raise");

            var lower = sm.AddState("Flashlight_Lower", new Vector3(540f, 200f, 0f));
            lower.motion = GetClip(clips, "Flashlight_Lower");

            var release = sm.AddState("Flashlight_Release", new Vector3(540f, 300f, 0f));
            release.motion = GetClip(clips, "Flashlight_Release");

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
            idle.motion = GetClip(clips, "Idle_Breathing");
            var doorOpen = sm.AddState("Door_Open", new Vector3(500f, 80f, 0f));
            doorOpen.motion = GetClip(clips, "Door_Open_Left");
            sm.defaultState = idle;

            AddTriggerState(sm, idle, doorOpen, "DoorOpen");
            AddTriggerState(sm, idle, doorOpen, "Interact");
        }

        private static void BuildHeadLookStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips)
        {
            var neutral = sm.AddState("Look_Neutral", new Vector3(240f, 80f, 0f));
            neutral.motion = GetClip(clips, "Idle_Breathing");
            var lookRight = sm.AddState("Look_Right", new Vector3(500f, 80f, 0f));
            lookRight.motion = GetClip(clips, "Look_Right");
            sm.defaultState = neutral;

            AddTriggerState(sm, neutral, lookRight, "LookRight");
        }

        private static void BuildAdditiveStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips)
        {
            var breathing = sm.AddState("Breathing_Additive", new Vector3(240f, 80f, 0f));
            breathing.motion = GetClip(clips, "Idle_Breathing");
            var flinch = sm.AddState("Impact_Flinch", new Vector3(500f, 80f, 0f));
            flinch.motion = GetClip(clips, "Stumble_Recover");
            sm.defaultState = breathing;

            AddTriggerState(sm, breathing, flinch, "ImpactFlinch");
        }

        private static void BuildFullOverrideStates(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips)
        {
            var none = sm.AddState("None", new Vector3(240f, 80f, 0f));
            none.motion = GetClip(clips, "Idle_Breathing");
            sm.defaultState = none;

            var fall = sm.AddState("Fall", new Vector3(520f, 40f, 0f));
            fall.motion = GetClip(clips, "Fall_Forward");

            var getUp = sm.AddState("GetUp", new Vector3(520f, 130f, 0f));
            getUp.motion = GetClip(clips, "Get_Up");

            var stumble = sm.AddState("Stumble", new Vector3(520f, 220f, 0f));
            stumble.motion = GetClip(clips, "Stumble_Recover");

            var pickup = sm.AddState("Flashlight_Pickup", new Vector3(520f, 310f, 0f));
            pickup.motion = GetClip(clips, "Flashlight_Pickup");

            var cinematic = sm.AddState("Cinematic_Override", new Vector3(240f, 220f, 0f));
            cinematic.motion = GetClip(clips, "Idle_Breathing");

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
            CameraModeController camModeCtrl = null;
            FirstPersonController fpc = null;
            FlashlightPhysicalItem physItem = null;

            if (player != null)
            {
                fpc = player.GetComponent<FirstPersonController>();
                var charModel = player.transform.Find("Character_Model");
                if (charModel != null)
                {
                    // Karakter modeli kök hizalaması: CharacterController tabanı (y=0) ile birebir hizalı
                    charModel.localPosition = Vector3.zero;
                    charModel.localRotation = Quaternion.identity;
                    charModel.localScale = Vector3.one;
                }

                var anim = player.GetComponentInChildren<Animator>(true);
                var camPivot = player.transform.Find("Camera_Pivot");
                var cam = camPivot != null ? camPivot.Find("Main Camera") : null;
                var flashRig = camPivot != null ? camPivot.Find("Flashlight_Rig") : null;
                var flashCtrl = flashRig != null ? flashRig.GetComponent<FlashlightController>() : null;

                if (anim != null)
                {
                    anim.runtimeAnimatorController = controller;
                    anim.applyRootMotion = false;

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

                camModeCtrl = player.GetComponent<CameraModeController>();
                if (camModeCtrl == null) camModeCtrl = player.AddComponent<CameraModeController>();
                var soCam = new SerializedObject(camModeCtrl);
                if (cam != null)
                {
                    var pMainCam = soCam.FindProperty("mainCamera");
                    if (pMainCam != null) pMainCam.objectReferenceValue = cam.GetComponent<Camera>();
                }
                if (camPivot != null)
                {
                    var pPivot = soCam.FindProperty("cameraPivot");
                    if (pPivot != null) pPivot.objectReferenceValue = camPivot;
                }
                if (charModel != null)
                {
                    var pCharModel = soCam.FindProperty("characterModelRoot");
                    if (pCharModel != null) pCharModel.objectReferenceValue = charModel;
                }
                if (physItem != null)
                {
                    var pFlashPhys = soCam.FindProperty("flashlightPhysical");
                    if (pFlashPhys != null) pFlashPhys.objectReferenceValue = physItem;
                }
                if (fpc != null)
                {
                    var pFpc = soCam.FindProperty("playerController");
                    if (pFpc != null) pFpc.objectReferenceValue = fpc;
                }
                soCam.ApplyModifiedProperties();
            }

            // AnimationDebugPanel'i Gameplay_HUD_Canvas üzerine bağla
            var hudCanvas = GameObject.Find("Gameplay_HUD_Canvas");
            if (hudCanvas != null)
            {
                var debugPanel = hudCanvas.GetComponent<AnimationDebugPanel>();
                if (debugPanel == null) debugPanel = hudCanvas.AddComponent<AnimationDebugPanel>();
                debugPanel.SetRegistry(registry);

                var soPanel = new SerializedObject(debugPanel);
                var pReg = soPanel.FindProperty("libraryRegistry");
                if (pReg != null) pReg.objectReferenceValue = registry;
                var pAc = soPanel.FindProperty("animController");
                if (pAc != null) pAc.objectReferenceValue = animCtrl;
                var pCm = soPanel.FindProperty("cameraModeController");
                if (pCm != null) pCm.objectReferenceValue = camModeCtrl;
                var pFpc = soPanel.FindProperty("playerController");
                if (pFpc != null) pFpc.objectReferenceValue = fpc;
                var pFp = soPanel.FindProperty("flashlightPhysical");
                if (pFp != null) pFp.objectReferenceValue = physItem;
                soPanel.ApplyModifiedProperties();
                EditorUtility.SetDirty(debugPanel);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProtagonistAnimationSetup] AbandonedSchool.unity 23-klip animasyon, 3P kamera ve debug panel bağlantıları kaydedildi.");
        }
    }
}
