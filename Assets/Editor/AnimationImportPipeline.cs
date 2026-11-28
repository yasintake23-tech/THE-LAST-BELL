using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Yeniden Kullanılabilir 52 Kliplik Animasyon İçe Aktarma Hattı (V1 + Motion Pack 04 V2).
    /// Tüm FBX dosyalarını otomatik olarak Humanoid retargeting, doğru zemin hizalaması (Feet vs Original Y),
    /// manifest.json döngü bayrakları ve deterministik Animation Events ile yapılandırır.
    /// </summary>
    public class AnimationImportPipeline : AssetPostprocessor
    {
        private const string ANIMATIONS_ROOT = "Assets/Animations/";

        private static readonly HashSet<string> LoopingClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // V2 Looping (manifest.json)
            "Idle_Natural_V2",
            "Walk_Grounded_V2",
            "Walk_Backward_V2",
            "Crouch_Idle_V2",
            "Crouch_Walk_V2",
            "Fear_Idle_V2",
            "Flashlight_Hold_V2",
            "Flashlight_Walk_V2",
            "Flashlight_Slow_V2",
            "Flashlight_Run_V2",
            "Flashlight_CrouchIdle_V2",
            "Flashlight_CrouchWalk_V2",
            // V1 Looping
            "Idle_Breathing",
            "Walk_Forward_unity",
            "Walk_Reverse_unity",
            "Walk_Slow_unity",
            "Strafe_Left",
            "Strafe_Right",
            "Run_Forward",
            "Flashlight_Hold",
            "Flashlight_Fear",
            "Flashlight_Walk",
            "Flashlight_Slow",
            "Flashlight_Run",
            "Flashlight_CrouchWalk"
        };

        // Çömelme, düşme, yerden alma, sendeleme klipleri orijinal Y yüksekliğini korumalıdır
        private static readonly HashSet<string> PreserveOriginalYClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Crouch_Enter", "Crouch_Enter_V2", "Crouch_Exit_V2",
            "Crouch_Idle_V2", "Crouch_Walk_V2",
            "Flashlight_CrouchIdle_V2", "Flashlight_CrouchWalk", "Flashlight_CrouchWalk_V2",
            "Flashlight_Pickup", "Flashlight_Pickup_V2",
            "Stumble_Recover",
            "Fall_Forward", "Fall_Forward_V2",
            "Get_Up", "Get_Up_V2"
        };

        private static readonly HashSet<string> FootstepClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Walk_Grounded_V2", "Walk_Backward_V2", "Crouch_Walk_V2",
            "Flashlight_Walk_V2", "Flashlight_Slow_V2", "Flashlight_Run_V2", "Flashlight_CrouchWalk_V2",
            "Walk_Forward_unity", "Walk_Reverse_unity", "Walk_Slow_unity",
            "Strafe_Left", "Strafe_Right", "Run_Forward",
            "Flashlight_Walk", "Flashlight_Slow", "Flashlight_Run", "Flashlight_CrouchWalk"
        };

        public static bool ShouldLoop(string fileNameWithoutExt, string normalizedPath)
        {
            if (LoopingClips.Contains(fileNameWithoutExt)) return true;
            if (normalizedPath.Contains("/Additive/")) return true;
            return false;
        }

        public static bool UsesHeightFromFeet(string fileNameWithoutExt)
        {
            return !PreserveOriginalYClips.Contains(fileNameWithoutExt);
        }

        public static ModelImporterClipAnimation ConfigureClipSettings(string fileNameWithoutExt, string normalizedPath, ModelImporterClipAnimation c)
        {
            bool isCinematic = normalizedPath.Contains("/Cinematics/");
            bool loop = ShouldLoop(fileNameWithoutExt, normalizedPath);
            bool useFeet = UsesHeightFromFeet(fileNameWithoutExt);

            if (string.IsNullOrEmpty(c.name) || c.name.StartsWith("mixamo", StringComparison.OrdinalIgnoreCase) || c.name.StartsWith("Take ", StringComparison.OrdinalIgnoreCase))
            {
                c.name = fileNameWithoutExt;
            }

            c.loopTime = loop;
            c.loopPose = loop;

            if (!isCinematic)
            {
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;

                c.lockRootHeightY = true;
                c.heightFromFeet = useFeet;
                c.keepOriginalPositionY = !useFeet;
            }

            var events = new List<AnimationEvent>();
            if (FootstepClips.Contains(fileNameWithoutExt))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_Footstep", time = 0.25f });
                events.Add(new AnimationEvent { functionName = "AnimEvent_Footstep", time = 0.75f });
            }

            // Motion Pack 04 V2 + V1 deterministik Animation Events (manifest.json sürelerinden normalize edilmiştir)
            if (string.Equals(fileNameWithoutExt, "Flashlight_Release_V2", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_FlashlightRelease", time = 0.60f }); // 0.48s / 0.8s
            }
            else if (string.Equals(fileNameWithoutExt, "Flashlight_Release", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_FlashlightRelease", time = 0.55f });
            }
            else if (string.Equals(fileNameWithoutExt, "Flashlight_Pickup_V2", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_FlashlightGrip", time = 0.54f }); // 1.35s / 2.5s
                events.Add(new AnimationEvent { functionName = "AnimEvent_Pickup", time = 0.54f });
            }
            else if (string.Equals(fileNameWithoutExt, "Flashlight_Pickup", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_FlashlightGrip", time = 0.55f });
                events.Add(new AnimationEvent { functionName = "AnimEvent_Pickup", time = 0.55f });
            }
            else if (string.Equals(fileNameWithoutExt, "Door_Open_Left_V2", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_DoorInteract", time = 0.38f }); // 0.76s / 2.0s
                events.Add(new AnimationEvent { functionName = "AnimEvent_DoorRelease", time = 0.65f });  // 1.30s / 2.0s
            }
            else if (string.Equals(fileNameWithoutExt, "Door_Open_Left", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_DoorInteract", time = 0.45f });
            }
            else if (string.Equals(fileNameWithoutExt, "Fall_Forward_V2", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_FlashlightRelease", time = 0.265f }); // 0.45s / 1.7s
                events.Add(new AnimationEvent { functionName = "AnimEvent_FallImpact", time = 0.588f });        // 1.00s / 1.7s
            }
            else if (string.Equals(fileNameWithoutExt, "Fall_Forward", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_FallImpact", time = 0.70f });
            }

            c.events = events.ToArray();
            return c;
        }

        private void OnPreprocessModel()
        {
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith(ANIMATIONS_ROOT)) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        private void OnPreprocessAnimation()
        {
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith(ANIMATIONS_ROOT)) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(normalized);

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = ConfigureClipSettings(fileNameWithoutExt, normalized, clips[i]);
            }

            importer.clipAnimations = clips;
        }

        [MenuItem("The Last Bell/Animasyon/Tüm Animasyon FBX'lerini Yeniden Yapılandır", false, 20)]
        public static void ReimportAllAnimationFBXs()
        {
            if (!Directory.Exists("Assets/Animations")) return;

            string[] fbxs = Directory.GetFiles("Assets/Animations", "*.fbx", SearchOption.AllDirectories);
            foreach (string rawPath in fbxs)
            {
                string path = rawPath.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[AnimationImportPipeline] {fbxs.Length} animasyon FBX dosyası Humanoid ve zemin hizalamasıyla yeniden içe aktarıldı.");
        }
    }
}
