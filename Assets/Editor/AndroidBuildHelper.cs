using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Otomatik Android APK Derleyici (Android Build Helper).
    /// Tek tıklamayla Android platformuna geçiş yapar, IL2CPP ve ARM64 mimarisini ayarlar,
    /// C++ Compiler Configuration'ı Release yapar, Custom Gradle şablonlarını doğrular,
    /// paket adını "com.thelastbell.game" olarak belirler ve oyunu APK olarak derler.
    /// Hem yerel Windows Editor hem de Linux/CI batch-mode ortamları ile uyumludur.
    /// </summary>
    public static class AndroidBuildHelper
    {
        public const string MENU_ROOT = "The Last Bell/";
        public const string PACKAGE_NAME = "com.thelastbell.game";
        public const string PRODUCT_NAME = "THE LAST BELL";
        public const string COMPANY_NAME = "TheLastBellStudio";
        public const string APK_FILE_NAME = "TheLastBell.apk";

        private const string LfsPointerPrefix = "version https://git-lfs.github.com/spec/v1";

        public static readonly string[] BUILD_SCENES = new string[]
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/OpeningCinematic.unity",
            "Assets/Scenes/AbandonedSchool.unity"
        };

        [MenuItem("The Last Bell/1. Tüm Değişiklikleri Kaydet ve Ekle", false, 1)]
        public static void ForceSaveAndSyncAll()
        {
            if (!Application.isBatchMode)
            {
                // Açık olan tüm sahneleri diske kaydet
                EditorSceneManager.SaveOpenScenes();

                // Bekleyen tüm asset/prefab değişikliklerini kaydet
                AssetDatabase.SaveAssets();

                // Varlıkları zorla tazeleyip derlemeye hazır hale getir
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            // Build Settings sahne listesini kontrol et ve yalnızca farklıysa güncelle
            ValidateScenes(syncIfChanged: true);

            Debug.Log("<color=green>[Sync] Tüm sahneler, kodlar ve UI varlıkları diske kaydedildi ve Build Settings güncellendi.</color>");
        }

        [MenuItem("The Last Bell/2. Android APK Çıktısı Al", false, 2)]
        public static void BuildAndroidAPKMenu()
        {
            BuildAndroidAPK(true);
        }

        [MenuItem("The Last Bell/4. Projeyi Doğrula (CI Validation)", false, 4)]
        public static void ValidateProjectMenu()
        {
            bool ok = ValidateProject(checkScenesDeep: true, out string report);
            EditorUtility.DisplayDialog(
                ok ? "Proje Doğrulaması Başarılı" : "Proje Doğrulaması Başarısız",
                report,
                "Tamam");
        }

        public static void ValidateProjectBatch()
        {
            Debug.Log("[AndroidBuildHelper] Batchmode proje doğrulaması başlatılıyor...");
            bool ok = ValidateProject(checkScenesDeep: true, out string report);
            WriteReportFile("Logs/ci-validation-report.txt", report);
            Debug.Log("[AndroidBuildHelper] Batchmode proje doğrulama sonucu: " + ok);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void BuildAndroidAPKBatch()
        {
            Debug.Log("[AndroidBuildHelper] Batchmode APK derleme başlatılıyor...");
            bool success = BuildAndroidAPK(false);
            Debug.Log("[AndroidBuildHelper] Batchmode APK derleme sonucu: " + success);
            EditorApplication.Exit(success ? 0 : 1);
        }

        public static bool BuildAndroidAPK(bool isInteractive = false)
        {
            try
            {
                // Derleme öncesi senkronizasyon (Batch mode'da takip edilen dosyaları gereksiz yere kirletme)
                if (!Application.isBatchMode)
                {
                    ForceSaveAndSyncAll();
                }
                else
                {
                    ValidateScenes(syncIfChanged: false);
                }

                if (isInteractive)
                {
                    EditorUtility.DisplayProgressBar("THE LAST BELL - Android APK Derleme", "Android platform ayarları yapılandırılıyor...", 0.2f);
                }

                // 1. Android Platform Ayarlarını Yapılandır
                ConfigureAndroidSettings();

                // .utmp önbelleğini temizle (stale C++ / prefab metadata)
                string utmpDir = Path.Combine(Directory.GetCurrentDirectory(), ".utmp");
                if (Directory.Exists(utmpDir))
                {
                    try { Directory.Delete(utmpDir, true); } catch (Exception ex) { Debug.LogWarning("[AndroidBuildHelper] .utmp temizlenirken: " + ex.Message); }
                }

                // 2. Çıktı Dizinini Hazırla (-customBuildPath varsa onu kullan)
                string projectDir = Directory.GetCurrentDirectory();
                string customBuildPath = GetCommandLineArg("-customBuildPath");
                string apkPath;
                if (!string.IsNullOrEmpty(customBuildPath))
                {
                    apkPath = Path.IsPathRooted(customBuildPath)
                        ? customBuildPath
                        : Path.GetFullPath(Path.Combine(projectDir, customBuildPath));
                    if (!apkPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    {
                        apkPath = Path.Combine(apkPath, APK_FILE_NAME);
                    }
                    string customDir = Path.GetDirectoryName(apkPath);
                    if (!string.IsNullOrEmpty(customDir) && !Directory.Exists(customDir))
                    {
                        Directory.CreateDirectory(customDir);
                    }
                }
                else
                {
                    string outputDir = Path.Combine(projectDir, "Builds", "Android");
                    if (!Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                    apkPath = Path.Combine(outputDir, APK_FILE_NAME);
                }

                // Derleme (BuildPlayer) başlamadan hemen önce eski APK dosyasını sil (Clean Build)
                if (File.Exists(apkPath))
                {
                    try
                    {
                        File.Delete(apkPath);
                        Debug.Log($"[AndroidBuildHelper] Eski APK dosyası temizlendi: {apkPath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[AndroidBuildHelper] Eski APK silinirken uyarı: {ex.Message}");
                    }
                }

                // 3. Sahneleri Doğrula
                if (isInteractive)
                {
                    EditorUtility.DisplayProgressBar("THE LAST BELL - Android APK Derleme", "Sahneler doğrulanıyor...", 0.4f);
                }
                string[] scenesToBuild = ValidateScenes(syncIfChanged: !Application.isBatchMode);

                // 4. BuildPlayerOptions Yapılandırması
                BuildPlayerOptions buildOptions = new BuildPlayerOptions
                {
                    scenes = scenesToBuild,
                    locationPathName = apkPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };

                if (isInteractive)
                {
                    EditorUtility.DisplayProgressBar("THE LAST BELL - Android APK Derleme", "APK derleniyor (IL2CPP + ARM64 + Release). Bu işlem birkaç dakika sürebilir...", 0.6f);
                }
                Debug.Log($"[AndroidBuildHelper] APK derleme başlatıldı. Hedef: {apkPath}");

                // 5. Derleme İşlemini Başlat
                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
                BuildSummary summary = report.summary;

                if (isInteractive)
                {
                    EditorUtility.ClearProgressBar();
                }

                if (summary.result == BuildResult.Succeeded)
                {
                    var fi = new FileInfo(apkPath);
                    float sizeMB = fi.Exists ? fi.Length / (1024f * 1024f) : summary.totalSize / (1024f * 1024f);
                    string lastModified = fi.Exists ? fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    long fileBytes = fi.Exists ? fi.Length : (long)summary.totalSize;

                    string successMsg = $"Android APK başarıyla derlendi!\n\n" +
                                        $"• Dosya Adı: {Path.GetFileName(apkPath)}\n" +
                                        $"• Konum: {apkPath}\n" +
                                        $"• Boyut: {sizeMB:F2} MB ({fileBytes:N0} byte)\n" +
                                        $"• Oluşturulma/Yazılma Tarihi: {lastModified}\n" +
                                        $"• Toplam Derleme Süresi: {summary.totalTime.TotalSeconds:F1} sn";
                    Debug.Log($"[AndroidBuildHelper] BAŞARILI: {successMsg}");

                    if (isInteractive)
                    {
                        EditorUtility.DisplayDialog("APK Derleme Başarılı", successMsg, "Dosyayı Göster");
                        EditorUtility.RevealInFinder(apkPath);
                    }
                    return true;
                }
                else if (summary.result == BuildResult.Cancelled)
                {
                    Debug.LogWarning("[AndroidBuildHelper] Derleme kullanıcı veya sistem tarafından iptal edildi.");
                    return false;
                }
                else
                {
                    string errorMsg = $"Android APK derleme başarısız oldu!\nHata Sayısı: {summary.totalErrors}\nSonuç: {summary.result}";
                    Debug.LogError($"[AndroidBuildHelper] HATA: {errorMsg}");

                    // Detaylı build step hatalarını logla
                    foreach (var step in report.steps)
                    {
                        foreach (var msg in step.messages)
                        {
                            if (msg.type == LogType.Error || msg.type == LogType.Exception)
                            {
                                Debug.LogError($"[BuildStep Error - {step.name}] {msg.content}");
                            }
                        }
                    }

                    if (isInteractive)
                    {
                        EditorUtility.DisplayDialog("APK Derleme Başarısız", errorMsg + "\n\nDetaylı hata mesajları için Unity Console penceresini inceleyin.", "Tamam");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (isInteractive)
                {
                    EditorUtility.ClearProgressBar();
                }
                Debug.LogError($"[AndroidBuildHelper] Beklenmeyen hata: {ex.Message}\n{ex.StackTrace}");
                if (isInteractive)
                {
                    EditorUtility.DisplayDialog("Derleme Hatası", "Beklenmeyen bir hata oluştu:\n" + ex.Message, "Tamam");
                }
                return false;
            }
            finally
            {
                if (isInteractive)
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        [MenuItem("The Last Bell/3. Android Ayarlarını Yapılandır (IL2CPP + ARM64)", false, 3)]
        public static void ConfigureAndroidSettingsMenu()
        {
            ConfigureAndroidSettings();
            EditorUtility.DisplayDialog("Ayarlar Yapılandırıldı",
                $"Android ayarları başarıyla güncellendi:\n\n" +
                $"• Paket Adı: {PACKAGE_NAME}\n" +
                $"• Scripting Backend: IL2CPP\n" +
                $"• C++ Compiler Config: Release\n" +
                $"• Hedef Mimari: ARM64\n" +
                $"• Min SDK: Android 8.0 (API 26)\n" +
                $"• Target SDK: Auto (34+)\n" +
                $"• Çıktı Tipi: APK (.apk)", "Tamam");
        }

        /// <summary>
        /// Platformu Android'e geçirir, IL2CPP, Release Compiler, ARM64, Custom Gradle ve Paket Adı ayarlarını uygular.
        /// </summary>
        public static void ConfigureAndroidSettings()
        {
            // 1. Platformu Android'e Geçir
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[AndroidBuildHelper] Aktif platform Android olarak değiştiriliyor...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            NamedBuildTarget androidTarget = NamedBuildTarget.Android;

            // 2. Paket Adı (Application Identifier) & Ürün Bilgileri
            PlayerSettings.SetApplicationIdentifier(androidTarget, PACKAGE_NAME);
            PlayerSettings.productName = PRODUCT_NAME;
            PlayerSettings.companyName = COMPANY_NAME;

            // 3. Scripting Backend = IL2CPP
            PlayerSettings.SetScriptingBackend(androidTarget, ScriptingImplementation.IL2CPP);

            // 4. C++ Compiler Configuration = Release
            PlayerSettings.SetIl2CppCompilerConfiguration(androidTarget, Il2CppCompilerConfiguration.Release);

            // 5. Hedef Mimari = ARM64
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // 6. Google Play App Bundle (AAB) yerine doğrudan APK (.apk) çıktısı al
            EditorUserBuildSettings.buildAppBundle = false;

            // 7. Minimum & Target API Seviyeleri (Android 8.0+ Oreo ve üzeri)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // 8. Custom Gradle Templates (mainTemplate ve gradleTemplate.properties)
            ConfigureCustomGradleSettings();

            // 9. GRADLE_USER_HOME & JAVA_TOOL_OPTIONS: Yalnızca Windows'ta Türkçe/ASCII dışı kullanıcı yolu workaround'u
            if (Application.platform == RuntimePlatform.WindowsEditor &&
                Directory.Exists(@"D:\") &&
                (Directory.Exists(@"D:\thelast") || HasNonAsciiCharacters(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))))
            {
                try
                {
                    string pureAsciiGradleHome = @"D:\thelast\.gradle";
                    Environment.SetEnvironmentVariable("GRADLE_USER_HOME", pureAsciiGradleHome, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable("GRADLE_USER_HOME", pureAsciiGradleHome, EnvironmentVariableTarget.User);

                    string tmpDir = @"D:\thelast\.tmp";
                    if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
                    Environment.SetEnvironmentVariable("TMP", tmpDir, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable("TEMP", tmpDir, EnvironmentVariableTarget.Process);

                    string javaOptions = "-Dgradle.user.home=D:/thelast/.gradle -Duser.home=D:/thelast -Djava.io.tmpdir=D:/thelast/.tmp -Dfile.encoding=UTF-8";
                    Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", javaOptions, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", javaOptions, EnvironmentVariableTarget.User);

                    Environment.SetEnvironmentVariable("USERPROFILE", @"D:\thelast", EnvironmentVariableTarget.Process);

                    Debug.Log($"[AndroidBuildHelper] Windows ASCII ortam değişkenleri ayarlandı: GRADLE_USER_HOME={pureAsciiGradleHome}, TMP={tmpDir}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AndroidBuildHelper] Ortam değişkenleri ayarlanırken uyarı: {ex.Message}");
                }
            }

            // 10. Oryantasyon: Yatay (Landscape Left & Right)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            Debug.Log($"[AndroidBuildHelper] Ayarlar güncellendi: Paket={PACKAGE_NAME}, Backend=IL2CPP, Compiler=Release, Mimari=ARM64, AppBundle=False");
        }

        /// <summary>
        /// Projenin derleme, sahne, eksik script, Git LFS ve Android ayar bütünlüğünü doğrular.
        /// </summary>
        public static bool ValidateProject(bool checkScenesDeep, out string summaryReport)
        {
            var issues = new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine("=== THE LAST BELL - Project Validation Report ===");
            sb.AppendLine($"Unity Version: {Application.unityVersion}");
            sb.AppendLine($"Platform: {Application.platform} | ActiveBuildTarget: {EditorUserBuildSettings.activeBuildTarget}");

            // 1. Script compilation status
            if (EditorUtility.scriptCompilationFailed)
            {
                issues.Add("Script compilation failed (EditorUtility.scriptCompilationFailed == true).");
            }

            // 2. Unresolved Git LFS pointer check
            List<string> lfsPointers = FindUnresolvedLfsPointers("Assets");
            if (lfsPointers.Count > 0)
            {
                string sample = string.Join(", ", lfsPointers.Take(10));
                issues.Add($"Found {lfsPointers.Count} unresolved Git LFS pointer files in Assets/ (run 'git lfs pull' / push LFS objects to remote). Sample: {sample}");
            }
            sb.AppendLine($"Git LFS unresolved pointers: {lfsPointers.Count}");

            // 3. Required scenes existence and EditorBuildSettings check
            var enabledScenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path.Replace('\\', '/'))
                .ToList();

            foreach (string requiredScene in BUILD_SCENES)
            {
                if (!File.Exists(requiredScene))
                {
                    issues.Add($"Required scene file missing: {requiredScene}");
                }
                else if (!enabledScenes.Contains(requiredScene))
                {
                    issues.Add($"Required scene not enabled in EditorBuildSettings: {requiredScene}");
                }
            }
            sb.AppendLine($"Build scenes configured: {enabledScenes.Count}/{BUILD_SCENES.Length}");

            // 4. Android Player Settings & Gradle template checks
            NamedBuildTarget androidTarget = NamedBuildTarget.Android;
            string appId = PlayerSettings.GetApplicationIdentifier(androidTarget);
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(androidTarget);
            AndroidArchitecture arch = PlayerSettings.Android.targetArchitectures;

            if (appId != PACKAGE_NAME)
            {
                issues.Add($"Android ApplicationIdentifier mismatch: expected '{PACKAGE_NAME}', got '{appId}'.");
            }
            if (backend != ScriptingImplementation.IL2CPP)
            {
                issues.Add($"Android ScriptingBackend mismatch: expected IL2CPP, got {backend}.");
            }
            if ((arch & AndroidArchitecture.ARM64) == 0)
            {
                issues.Add($"Android targetArchitectures does not include ARM64 (current: {arch}).");
            }
            if (!File.Exists("Assets/Plugins/Android/mainTemplate.gradle"))
            {
                issues.Add("Missing Assets/Plugins/Android/mainTemplate.gradle.");
            }
            if (!File.Exists("Assets/Plugins/Android/gradleTemplate.properties"))
            {
                issues.Add("Missing Assets/Plugins/Android/gradleTemplate.properties.");
            }
            sb.AppendLine($"Android Settings -> Package: {appId}, Backend: {backend}, Arch: {arch}, MinSDK: {PlayerSettings.Android.minSdkVersion}");

            // 5. Optional deep scene check for missing scripts
            if (checkScenesDeep)
            {
                int totalMissingScripts = 0;
                foreach (string scenePath in BUILD_SCENES)
                {
                    if (!File.Exists(scenePath)) continue;
                    Scene opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    int sceneMissing = 0;
                    foreach (GameObject root in opened.GetRootGameObjects())
                    {
                        sceneMissing += CountMissingScriptsRecursive(root.transform);
                    }
                    totalMissingScripts += sceneMissing;
                    sb.AppendLine($"Scene check [{scenePath}]: {sceneMissing} missing script(s)");
                    if (sceneMissing > 0)
                    {
                        issues.Add($"Scene '{scenePath}' has {sceneMissing} missing MonoBehaviour script reference(s).");
                    }
                }
            }

            if (issues.Count > 0)
            {
                sb.AppendLine("Validation FAILED:");
                foreach (string issue in issues)
                {
                    sb.AppendLine("  - " + issue);
                    Debug.LogError("[Validation Error] " + issue);
                }
                summaryReport = sb.ToString();
                return false;
            }

            sb.AppendLine("Validation PASSED with 0 issues.");
            summaryReport = sb.ToString();
            Debug.Log(summaryReport);
            return true;
        }

        private static int CountMissingScriptsRecursive(Transform t)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            for (int i = 0; i < t.childCount; i++)
            {
                count += CountMissingScriptsRecursive(t.GetChild(i));
            }
            return count;
        }

        private static List<string> FindUnresolvedLfsPointers(string rootDir)
        {
            var unresolved = new List<string>();
            if (!Directory.Exists(rootDir)) return unresolved;

            byte[] expectedPrefix = Encoding.ASCII.GetBytes(LfsPointerPrefix);
            byte[] buffer = new byte[expectedPrefix.Length];

            foreach (string file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var info = new FileInfo(file);
                    // LFS pointer files are tiny (~130 bytes)
                    if (info.Length < expectedPrefix.Length || info.Length > 512) continue;

                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        int read = fs.Read(buffer, 0, buffer.Length);
                        if (read == expectedPrefix.Length && buffer.SequenceEqual(expectedPrefix))
                        {
                            unresolved.Add(file.Replace('\\', '/'));
                        }
                    }
                }
                catch
                {
                    // Ignore transient unreadable files
                }
            }
            return unresolved;
        }

        private static bool HasNonAsciiCharacters(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
            {
                if (c > 127) return true;
            }
            return false;
        }

        private static string GetCommandLineArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        internal static void WriteReportFile(string relativePath, string content)
        {
            try
            {
                string dir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(relativePath, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidBuildHelper] Rapor yazılamadı ({relativePath}): {ex.Message}");
            }
        }

        /// <summary>
        /// Custom Gradle Template ayarlarını ProjectSettings üzerinde etkinleştirir.
        /// </summary>
        private static void ConfigureCustomGradleSettings()
        {
            try
            {
                var settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (settingsAssets != null && settingsAssets.Length > 0)
                {
                    var so = new SerializedObject(settingsAssets[0]);
                    var propMain = so.FindProperty("useCustomMainGradleTemplate");
                    var propProps = so.FindProperty("useCustomGradlePropertiesTemplate");

                    bool modified = false;
                    if (propMain != null && !propMain.boolValue)
                    {
                        propMain.boolValue = true;
                        modified = true;
                    }
                    if (propProps != null && !propProps.boolValue)
                    {
                        propProps.boolValue = true;
                        modified = true;
                    }

                    if (modified)
                    {
                        so.ApplyModifiedProperties();
                        AssetDatabase.SaveAssets();
                        Debug.Log("[AndroidBuildHelper] Custom Gradle Template bayrakları ProjectSettings'e kaydedildi.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidBuildHelper] Custom Gradle Template yapılandırılırken uyarı: {ex.Message}");
            }
        }

        /// <summary>
        /// Sahnelerin varlığını kontrol eder ve Build Settings sırasına uygun liste döndürür.
        /// </summary>
        private static string[] ValidateScenes(bool syncIfChanged = true)
        {
            var validScenes = new List<string>();
            foreach (var scenePath in BUILD_SCENES)
            {
                if (File.Exists(scenePath))
                {
                    validScenes.Add(scenePath);
                }
                else
                {
                    Debug.LogWarning($"[AndroidBuildHelper] Uyarı: Sahne bulunamadı -> {scenePath}");
                }
            }

            if (syncIfChanged)
            {
                var currentScenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path.Replace('\\', '/'))
                    .ToArray();

                if (!currentScenes.SequenceEqual(validScenes))
                {
                    EditorBuildSettingsScene[] buildScenes = new EditorBuildSettingsScene[validScenes.Count];
                    for (int i = 0; i < validScenes.Count; i++)
                    {
                        buildScenes[i] = new EditorBuildSettingsScene(validScenes[i], true);
                    }
                    EditorBuildSettings.scenes = buildScenes;
                }
            }

            return validScenes.ToArray();
        }
    }

    /// <summary>
    /// Automatically validates project integrity and writes CI build logs/reports during BuildPipeline.BuildPlayer
    /// (works for both local builds and GameCI UnityBuilderAction).
    /// </summary>
    internal sealed class CIBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            Debug.Log("[CIBuildProcessor] Running pre-build validation for Android...");
            bool ok = AndroidBuildHelper.ValidateProject(checkScenesDeep: false, out string reportText);
            AndroidBuildHelper.WriteReportFile("Logs/ci-prebuild-report.txt", reportText);

            if (!ok)
            {
                throw new BuildFailedException("THE LAST BELL pre-build validation failed. See Logs/ci-prebuild-report.txt and console errors.");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== THE LAST BELL - Android Build Report ===");
            sb.AppendLine($"Result: {report.summary.result}");
            sb.AppendLine($"Output Path: {report.summary.outputPath}");
            sb.AppendLine($"Total Size: {report.summary.totalSize} bytes");
            sb.AppendLine($"Total Time: {report.summary.totalTime.TotalSeconds:F1}s");
            sb.AppendLine($"Total Errors: {report.summary.totalErrors}");
            sb.AppendLine($"Total Warnings: {report.summary.totalWarnings}");

            AndroidBuildHelper.WriteReportFile("Logs/ci-build-report.txt", sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
