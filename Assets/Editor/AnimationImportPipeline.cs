using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Yeniden Kullanılabilir Animasyon İçe Aktarma Hattı (Animation Import Pipeline).
    /// Assets/Animations/ altındaki tüm FBX dosyalarını otomatik olarak Humanoid retargeting,
    /// zemin hizalaması (Feet vs Original Y), döngü bayrakları ve Animation Events ile yapılandırır.
    /// </summary>
    public class AnimationImportPipeline : AssetPostprocessor
    {
        private const string ANIMATIONS_ROOT = "Assets/Animations/";

        private static readonly HashSet<string> LoopingClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
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

        // Çömelme, düşme, yerden alma, sendeleme klipleri orijinal Y yüksekliğini korumalıdır (Feet'e zorlanırsa zemin kayması oluşur)
        private static readonly HashSet<string> PreserveOriginalYClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Crouch_Enter",
            "Flashlight_CrouchWalk",
            "Flashlight_Pickup",
            "Stumble_Recover",
            "Fall_Forward",
            "Get_Up"
        };

        private static readonly HashSet<string> FootstepClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Walk_Forward_unity",
            "Walk_Reverse_unity",
            "Walk_Slow_unity",
            "Strafe_Left",
            "Strafe_Right",
            "Run_Forward",
            "Flashlight_Walk",
            "Flashlight_Slow",
            "Flashlight_Run",
            "Flashlight_CrouchWalk"
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

            // Klip adını FBX dosya adıyla eşleştir (Unity Inspector ve DebugPanel'de temiz görünür)
            if (string.IsNullOrEmpty(c.name) || c.name.StartsWith("mixamo", StringComparison.OrdinalIgnoreCase) || c.name.StartsWith("Take ", StringComparison.OrdinalIgnoreCase))
            {
                c.name = fileNameWithoutExt;
            }

            c.loopTime = loop;
            c.loopPose = loop;

            if (!isCinematic)
            {
                // Yatay kök hareketini ve dönüşünü kilitle (CharacterController hareketi yönetir)
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;

                // Zemin hizalaması: Ayakta duran kliplerde Feet (Y=0 zemin), dikey aksiyonlarda Original Y
                c.lockRootHeightY = true;
                c.heightFromFeet = useFeet;
                c.keepOriginalPositionY = !useFeet;
            }

            // Deterministik Animation Events
            var events = new List<AnimationEvent>();
            if (FootstepClips.Contains(fileNameWithoutExt))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_Footstep", time = 0.25f });
                events.Add(new AnimationEvent { functionName = "AnimEvent_Footstep", time = 0.75f });
            }

            if (string.Equals(fileNameWithoutExt, "Flashlight_Release", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_FlashlightRelease", time = 0.55f });
            }
            else if (string.Equals(fileNameWithoutExt, "Flashlight_Pickup", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_FlashlightGrip", time = 0.55f });
                events.Add(new AnimationEvent { functionName = "AnimEvent_Pickup", time = 0.55f });
            }
            else if (string.Equals(fileNameWithoutExt, "Door_Open_Left", StringComparison.OrdinalIgnoreCase))
            {
                events.Add(new AnimationEvent { functionName = "AnimEvent_DoorInteract", time = 0.45f });
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
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }
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
