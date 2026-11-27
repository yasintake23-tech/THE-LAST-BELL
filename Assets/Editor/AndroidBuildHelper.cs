using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Otomatik Android APK Derleyici (Android Build Helper).
    /// Tek tıklamayla Android platformuna geçiş yapar, IL2CPP ve ARM64 mimarisini ayarlar,
    /// C++ Compiler Configuration'ı Release yapar, Custom Gradle şablonlarını doğrular,
    /// paket adını "com.thelastbell.game" olarak belirler ve oyunu APK olarak derler.
    /// </summary>
    public static class AndroidBuildHelper
    {
        public const string MENU_ROOT = "The Last Bell/";
        public const string PACKAGE_NAME = "com.thelastbell.game";
        public const string PRODUCT_NAME = "THE LAST BELL";
        public const string COMPANY_NAME = "TheLastBellStudio";
        public const string APK_FILE_NAME = "TheLastBell.apk";

        public static readonly string[] BUILD_SCENES = new string[]
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/OpeningCinematic.unity",
            "Assets/Scenes/AbandonedSchool.unity"
        };

        [MenuItem("The Last Bell/1. Tüm Değişiklikleri Kaydet ve Ekle", false, 1)]
        public static void ForceSaveAndSyncAll()
        {
            // Açık olan tüm sahneleri diske kaydet
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            // Bekleyen tüm asset/prefab değişikliklerini kaydet
            AssetDatabase.SaveAssets();

            // Varlıkları zorla tazeleyip derlemeye hazır hale getir
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // Build Settings sahne listesini kontrol et ve güncelle
            ValidateScenes();

            Debug.Log("<color=green>[Sync] Tüm sahneler, kodlar ve UI varlıkları diske kaydedildi ve Build Settings güncellendi.</color>");
        }

        [MenuItem("The Last Bell/2. Android APK Çıktısı Al", false, 2)]
        public static void BuildAndroidAPKMenu()
        {
            BuildAndroidAPK(true);
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
                // Derleme öncesi zorunlu senkronizasyon (Tüm açık sahneler, assetler ve build settings)
                ForceSaveAndSyncAll();

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

                // 2. Çıktı Dizinini Hazırla
                string projectDir = Directory.GetCurrentDirectory();
                string outputDir = Path.Combine(projectDir, "Builds", "Android");
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                string apkPath = Path.Combine(outputDir, APK_FILE_NAME);

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
                string[] scenesToBuild = ValidateScenes();

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
                                        $"• Dosya Adı: {APK_FILE_NAME}\n" +
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

            // 9. GRADLE_USER_HOME & JAVA_TOOL_OPTIONS: Pure ASCII path to prevent CXX1429 and Turkish character issues
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

                Debug.Log($"[AndroidBuildHelper] Ortam değişkenleri ayarlandı: GRADLE_USER_HOME={pureAsciiGradleHome}, TMP={tmpDir}, JAVA_TOOL_OPTIONS={javaOptions}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidBuildHelper] Ortam değişkenleri ayarlanırken uyarı: {ex.Message}");
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
        private static string[] ValidateScenes()
        {
            var validScenes = new System.Collections.Generic.List<string>();
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

            // Build Settings senkronizasyonu
            EditorBuildSettingsScene[] buildScenes = new EditorBuildSettingsScene[validScenes.Count];
            for (int i = 0; i < validScenes.Count; i++)
            {
                buildScenes[i] = new EditorBuildSettingsScene(validScenes[i], true);
            }
            EditorBuildSettings.scenes = buildScenes;

            return validScenes.ToArray();
        }
    }
}
