using System.IO;
using UnityEngine;
using UnityEditor;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Yeniden Kullanılabilir Animasyon İçe Aktarma Hattı (Animation Import Pipeline).
    /// Assets/Animations/ altındaki tüm FBX dosyalarını otomatik olarak Humanoid retargeting,
    /// kök hareketi sabitleme (root motion baking) ve klasör bazlı döngü ayarlarıyla yapılandırır.
    /// </summary>
    public class AnimationImportPipeline : AssetPostprocessor
    {
        private const string ANIMATIONS_ROOT = "Assets/Animations/";

        private void OnPreprocessModel()
        {
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith(ANIMATIONS_ROOT)) return;

            var importer = assetImporter as ModelImporter;
            if (importer == null) return;

            // Animasyon FBX'leri gereksiz materyal/mesh kopyası üretmesin
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

            bool isLocomotionOrAdditive =
                normalized.Contains("/Locomotion/") ||
                normalized.Contains("/Additive/") ||
                normalized.Contains("/UpperBody/");
            bool isCinematic = normalized.Contains("/Cinematics/");

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }
            if (clips == null || clips.Length == 0) return;

            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                c.loopTime = isLocomotionOrAdditive;
                c.loopPose = isLocomotionOrAdditive;

                if (!isCinematic)
                {
                    // Kök hareketini (Root Motion) sabitleyerek ayak kayması ve istenmeyen yer değiştirmeyi önle
                    c.lockRootRotation = true;
                    c.keepOriginalOrientation = true;
                    c.lockRootHeightY = true;
                    c.keepOriginalPositionY = true;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalPositionXZ = true;
                }
                clips[i] = c;
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
            Debug.Log($"[AnimationImportPipeline] {fbxs.Length} animasyon FBX dosyası Humanoid olarak yeniden içe aktarıldı.");
        }
    }
}
