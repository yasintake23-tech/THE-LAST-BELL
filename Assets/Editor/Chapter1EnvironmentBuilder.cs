using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using TheLastBell.Player;
using TheLastBell.Interaction;
using TheLastBell.Gameplay;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Bölüm I: Varış (Chapter I — The Arrival) Üretim Kurucusu.
    /// Mevcut bina yerleşimini, karakter kontrolcüsünü, 52 kliplik animasyon mimarisini ve mobil HUD'ı
    /// %100 koruyarak AbandonedSchool.unity sahnesini gerçekçi bir psikolojik korku deneyimine dönüştürür:
    /// - Mobil ASTC doku sıkıştırma & NormalMap doğrulaması
    /// - 26 doğrulanmış CC0 (Poly Haven + ambientCG) ve 7 özgün Blender 4.0.2 3B varlığının PBR materyal kurulumu
    /// - Alacakaranlık (Blue Hour) winter_evening 2K HDRI gökyüzü, sis, ReflectionProbe ve LightProbeGroup
    /// - Avlu arabası (ArrivedSedan_Car), yabani otlar, 3B kabartma "HARPUT / ENGLISH COURSE" tabelası ve detaylı çift kanatlı giriş kapısı
    /// - Derslikler, koridor, öğretmenler/personel odası (elektrik panosu -> fiziksel Feştek düşüşü -> Feştek yuvası -> sağ taraftaki CRT televizyon)
    /// - 7 yeni Bölüm I karakter etkileşim animasyonu (.anim, Humanoid uyumlu) ve 15 uzamsal 3B ses klibi
    /// </summary>
    public static class Chapter1EnvironmentBuilder
    {
        public const string MAT_DIR = "Assets/Environment/Materials";
        public const string CH1_ANIM_DIR = "Assets/Animations/Chapter1";
        public const string SCENE_PATH = "Assets/Scenes/AbandonedSchool.unity";
        public const string CHAPTER1_ROOT_NAME = "--- CHAPTER 1 PRODUCTION ---";

        [MenuItem("The Last Bell/Bölüm 1/Chapter I - The Arrival Kurulumunu Uygula", false, 30)]
        public static void SetupChapter1Menu()
        {
            SetupChapter1();
            EditorUtility.DisplayDialog("Chapter I — The Arrival Hazır",
                "Çevre, PBR materyaller, 3B HARPUT ENGLISH COURSE tabelası, Feştek bulmaca zinciri, CRT televizyon, uzamsal sesler ve yeni animasyonlar kuruldu.",
                "Tamam");
        }

        public static void SetupChapter1()
        {
            Debug.Log("[Chapter1EnvironmentBuilder] Chapter I — The Arrival kurulumu başlatılıyor...");

            Directory.CreateDirectory(MAT_DIR);
            Directory.CreateDirectory(CH1_ANIM_DIR);

            // 1. Doku, HDRI, Ses ve Model Importer Ayarları (Mobil ASTC + NormalMap + Cubemap)
            ConfigureEnvironmentAssetImporters();

            // 2. Bölüm I Yeni Karakter Etkileşim Animasyonlarını (.anim) Üret ve Kayıt Defterine Ekle
            CreateAndRegisterChapter1Animations();

            // 3. PBR Materyal Kütüphanesini Oluştur / Güncelle
            Dictionary<string, Material> mats = BuildMaterialLibrary();

            // 4. AbandonedSchool.unity Sahnesini Yıkımsız (In-Place) Güncelle
            UpgradeAbandonedSchoolScene(mats);

            AssetDatabase.SaveAssets();
            Debug.Log("[Chapter1EnvironmentBuilder] Chapter I — The Arrival kurulumu başarıyla tamamlandı.");
        }

        #region 1. Importer Optimization (Mobile ASTC, NormalMaps, Skybox Cubemap, 3D Audio)
        private static void ConfigureEnvironmentAssetImporters()
        {
            string[] searchRoots = { "Assets/Environment", "Assets/Audio/Chapter1" };
            string[] guids = AssetDatabase.FindAssets("", searchRoots);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || Directory.Exists(path)) continue;

                string low = path.ToLowerInvariant();

                if (low.EndsWith(".hdr"))
                {
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti != null)
                    {
                        bool dirty = false;
                        if (ti.textureShape != TextureImporterShape.TextureCube) { ti.textureShape = TextureImporterShape.TextureCube; dirty = true; }
                        if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; dirty = true; }
                        var androidSettings = ti.GetPlatformTextureSettings("Android");
                        if (!androidSettings.overridden || androidSettings.format != TextureImporterFormat.ASTC_6x6)
                        {
                            androidSettings.overridden = true;
                            androidSettings.maxTextureSize = 2048;
                            androidSettings.format = TextureImporterFormat.ASTC_6x6;
                            ti.SetPlatformTextureSettings(androidSettings);
                            dirty = true;
                        }
                        if (dirty) ti.SaveAndReimport();
                    }
                }
                else if (low.EndsWith(".jpg") || low.EndsWith(".png"))
                {
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti != null)
                    {
                        bool isNormal = low.Contains("_nor_gl") || low.Contains("normalgl") || low.Contains("_normal");
                        bool isLinearMask = low.Contains("_metalsmooth") || low.Contains("_ao") || low.Contains("ambientocclusion") || low.Contains("_arm_") || low.Contains("roughness") || low.Contains("metalness");
                        bool isHero2K = low.Contains("plaster007") || low.Contains("television_02") || low.Contains("festek_albedo") || low.Contains("harputsign");

                        bool dirty = false;
                        var expectedType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                        if (ti.textureType != expectedType) { ti.textureType = expectedType; dirty = true; }
                        if (!isNormal && ti.sRGBTexture == isLinearMask) { ti.sRGBTexture = !isLinearMask; dirty = true; }

                        int targetMax = isHero2K ? 2048 : 1024;
                        if (ti.maxTextureSize != targetMax) { ti.maxTextureSize = targetMax; dirty = true; }

                        var android = ti.GetPlatformTextureSettings("Android");
                        var targetFmt = (isHero2K || isNormal) ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_8x8;
                        if (!android.overridden || android.format != targetFmt || android.maxTextureSize != targetMax)
                        {
                            android.overridden = true;
                            android.maxTextureSize = targetMax;
                            android.format = targetFmt;
                            ti.SetPlatformTextureSettings(android);
                            dirty = true;
                        }
                        if (dirty) ti.SaveAndReimport();
                    }
                }
                else if (low.EndsWith(".fbx"))
                {
                    var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (mi != null)
                    {
                        bool dirty = false;
                        if (mi.animationType != ModelImporterAnimationType.None)
                        {
                            mi.animationType = ModelImporterAnimationType.None;
                            mi.importAnimation = false;
                            dirty = true;
                        }
                        if (mi.meshCompression != ModelImporterMeshCompression.Low)
                        {
                            mi.meshCompression = ModelImporterMeshCompression.Low;
                            dirty = true;
                        }
                        if (dirty) mi.SaveAndReimport();
                    }
                }
                else if (low.EndsWith(".wav"))
                {
                    var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (ai != null)
                    {
                        bool dirty = false;
                        if (!ai.forceToMono) { ai.forceToMono = true; dirty = true; }
                        var def = ai.defaultSampleSettings;
                        bool isAmbLoop = low.Contains("amb_") || low.Contains("loop");
                        var expectedLoad = isAmbLoop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                        if (def.loadType != expectedLoad || def.compressionFormat != AudioCompressionFormat.Vorbis)
                        {
                            def.loadType = expectedLoad;
                            def.compressionFormat = AudioCompressionFormat.Vorbis;
                            def.quality = 0.7f;
                            ai.defaultSampleSettings = def;
                            dirty = true;
                        }
                        if (dirty) ai.SaveAndReimport();
                    }
                }
            }
        }
        #endregion

        #region 2. Chapter I Custom Humanoid Interaction Animations (.anim)
        private static void CreateAndRegisterChapter1Animations()
        {
            var registry = AssetDatabase.LoadAssetAtPath<AnimationLibraryRegistry>(ProtagonistAnimationSetup.REGISTRY_PATH);
            if (registry == null) return;

            AnimationClip baseIdle = registry.FindClip("Idle_Natural_V2");
            AnimationClip baseWalk = registry.FindClip("Walk_Grounded_V2");
            if (baseIdle == null || baseWalk == null) return;

            var specs = new (string id, AnimationClip seed, float dur, bool loop, AnimationClipCategory cat, string layer, Action<AnimationClip, float> sculpt, string notes)[]
            {
                ("Panel_Lever_Pull_Ch1", baseIdle, 1.10f, false, AnimationClipCategory.Interaction, ProtagonistAnimationController.LAYER_RIGHT_ARM,
                    (clip, d) => {
                        SetMuscleCurve(clip, "Right Arm Down-Up", (0f, -0.15f), (0.35f, 0.58f), (0.80f, -0.35f), (d, -0.15f));
                        SetMuscleCurve(clip, "Right Arm Front-Back", (0f, 0.10f), (0.35f, 0.65f), (0.80f, 0.35f), (d, 0.10f));
                        SetMuscleCurve(clip, "Right Forearm Stretch", (0f, -0.25f), (0.35f, 0.15f), (0.80f, -0.55f), (d, -0.25f));
                        SetMuscleCurve(clip, "Spine Front-Back", (0f, 0.0f), (0.45f, -0.14f), (0.85f, -0.22f), (d, 0.0f));
                    }, "Chapter I electrical panel lever reach & downward pull"),

                ("Festek_Pickup_Ch1", baseIdle, 1.05f, false, AnimationClipCategory.Interaction, ProtagonistAnimationController.LAYER_RIGHT_ARM,
                    (clip, d) => {
                        SetMuscleCurve(clip, "Spine Front-Back", (0f, 0f), (0.45f, -0.45f), (d, 0f));
                        SetMuscleCurve(clip, "Right Arm Down-Up", (0f, -0.15f), (0.45f, -0.55f), (d, 0.05f));
                        SetMuscleCurve(clip, "Right Arm Front-Back", (0f, 0.10f), (0.45f, 0.58f), (d, 0.32f));
                        SetMuscleCurve(clip, "Right Forearm Stretch", (0f, -0.20f), (0.45f, 0.25f), (d, -0.42f));
                    }, "Chapter I physical Feştek floor pickup reach"),

                ("Festek_Hold_Idle_Ch1", baseIdle, 2.00f, true, AnimationClipCategory.Interaction, ProtagonistAnimationController.LAYER_RIGHT_ARM,
                    (clip, d) => {
                        SetMuscleCurve(clip, "Right Arm Down-Up", (0f, 0.02f), (d * 0.5f, 0.05f), (d, 0.02f));
                        SetMuscleCurve(clip, "Right Arm Front-Back", (0f, 0.36f), (d * 0.5f, 0.38f), (d, 0.36f));
                        SetMuscleCurve(clip, "Right Forearm Stretch", (0f, -0.45f), (d * 0.5f, -0.42f), (d, -0.45f));
                        SetMuscleCurve(clip, "Left Arm Down-Up", (0f, 0.06f), (d * 0.5f, 0.08f), (d, 0.06f));
                        SetMuscleCurve(clip, "Left Arm Front-Back", (0f, 0.40f), (d * 0.5f, 0.42f), (d, 0.40f));
                        SetMuscleCurve(clip, "Left Forearm Stretch", (0f, -0.38f), (d * 0.5f, -0.36f), (d, -0.38f));
                    }, "Chapter I dual-hand idle: Feştek in right hand + flashlight in left hand"),

                ("Festek_Hold_Walk_Ch1", baseWalk, baseWalk.length, true, AnimationClipCategory.Locomotion, ProtagonistAnimationController.LAYER_BASE,
                    (clip, d) => {
                        SetMuscleCurve(clip, "Right Arm Down-Up", (0f, 0.02f), (d * 0.5f, 0.06f), (d, 0.02f));
                        SetMuscleCurve(clip, "Right Arm Front-Back", (0f, 0.35f), (d * 0.5f, 0.39f), (d, 0.35f));
                        SetMuscleCurve(clip, "Right Forearm Stretch", (0f, -0.44f), (d * 0.5f, -0.40f), (d, -0.44f));
                        SetMuscleCurve(clip, "Left Arm Front-Back", (0f, 0.38f), (d * 0.5f, 0.42f), (d, 0.38f));
                    }, "Chapter I grounded walk holding Feştek in right hand + off-hand flashlight"),

                ("Festek_Insert_Socket_Ch1", baseIdle, 0.95f, false, AnimationClipCategory.Interaction, ProtagonistAnimationController.LAYER_RIGHT_ARM,
                    (clip, d) => {
                        SetMuscleCurve(clip, "Right Arm Down-Up", (0f, 0.04f), (0.35f, 0.28f), (0.75f, 0.30f), (d, 0.12f));
                        SetMuscleCurve(clip, "Right Arm Front-Back", (0f, 0.36f), (0.35f, 0.52f), (0.75f, 0.74f), (d, 0.42f));
                        SetMuscleCurve(clip, "Right Forearm Stretch", (0f, -0.45f), (0.35f, -0.22f), (0.75f, 0.18f), (d, -0.25f));
                    }, "Chapter I Feştek alignment & firm seating into receiving socket"),

                ("Festek_Press_Button_Ch1", baseIdle, 0.65f, false, AnimationClipCategory.Interaction, ProtagonistAnimationController.LAYER_RIGHT_ARM,
                    (clip, d) => {
                        SetMuscleCurve(clip, "Right Arm Down-Up", (0f, 0.15f), (0.30f, 0.22f), (d, 0.02f));
                        SetMuscleCurve(clip, "Right Arm Front-Back", (0f, 0.45f), (0.30f, 0.68f), (d, 0.28f));
                        SetMuscleCurve(clip, "Right Forearm Stretch", (0f, -0.25f), (0.30f, 0.10f), (d, -0.35f));
                        SetMuscleCurve(clip, "Right Index 1 Stretched", (0f, 0.15f), (0.30f, 0.65f), (d, 0.10f));
                    }, "Chapter I finger button press activating seated Feştek"),

                ("Flashlight_OffHand_Transfer_Ch1", baseIdle, 0.70f, false, AnimationClipCategory.Flashlight, ProtagonistAnimationController.LAYER_UPPER_BODY,
                    (clip, d) => {
                        SetMuscleCurve(clip, "Right Arm Front-Back", (0f, 0.42f), (0.35f, 0.52f), (d, 0.34f));
                        SetMuscleCurve(clip, "Right Arm In-Out", (0f, 0.0f), (0.35f, -0.38f), (d, 0.05f));
                        SetMuscleCurve(clip, "Left Arm Front-Back", (0f, 0.05f), (0.35f, 0.50f), (d, 0.40f));
                        SetMuscleCurve(clip, "Left Arm In-Out", (0f, 0.0f), (0.35f, -0.35f), (d, 0.05f));
                    }, "Chapter I clean right-to-left hand flashlight transfer")
            };

            var entries = new List<RegisteredAnimationEntry>(registry.Entries);
            foreach (var sp in specs)
            {
                string animPath = $"{CH1_ANIM_DIR}/{sp.id}.anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
                bool isNew = (clip == null);
                if (isNew) clip = new AnimationClip { name = sp.id };
                else clip.ClearCurves();

                // Grounded Root & lower-body Humanoid eğrilerini doğrulanmış V2 klibinden kopyala (humanMotion=true garantisi!)
                foreach (var bind in AnimationUtility.GetCurveBindings(sp.seed))
                {
                    AnimationCurve srcCurve = AnimationUtility.GetEditorCurve(sp.seed, bind);
                    if (srcCurve == null) continue;
                    var trimmed = TrimOrScaleCurve(srcCurve, sp.dur);
                    AnimationUtility.SetEditorCurve(clip, bind, trimmed);
                }

                sp.sculpt(clip, sp.dur);

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = sp.loop;
                settings.loopBlend = sp.loop;
                settings.keepOriginalPositionY = false;
                settings.heightFromFeet = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendOrientation = true;
                settings.keepOriginalOrientation = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                if (isNew) AssetDatabase.CreateAsset(clip, animPath);
                else EditorUtility.SetDirty(clip);

                entries.RemoveAll(e => e != null && string.Equals(e.id, sp.id, StringComparison.OrdinalIgnoreCase));
                entries.Add(new RegisteredAnimationEntry
                {
                    id = sp.id,
                    displayName = sp.id.Replace("_", " "),
                    clip = clip,
                    versionTag = "Ch1",
                    category = sp.cat,
                    approvalStatus = AnimationApprovalStatus.ApprovedWithProceduralCorrection,
                    targetLayer = sp.layer,
                    defaultLoop = sp.loop,
                    usesRootHeightBakeFromFeet = true,
                    recommendedSpeedMps = sp.id.Contains("Walk") ? 0.69f : 0f,
                    notes = sp.notes
                });
            }

            registry.SetEntries(entries);
            EditorUtility.SetDirty(registry);
        }

        private static AnimationCurve TrimOrScaleCurve(AnimationCurve src, float targetDur)
        {
            if (src.length == 0) return new AnimationCurve();
            float srcEnd = Mathf.Max(0.01f, src.keys[src.length - 1].time);
            float scale = targetDur / srcEnd;
            var keys = new Keyframe[src.length];
            for (int i = 0; i < src.length; i++)
            {
                var k = src.keys[i];
                keys[i] = new Keyframe(k.time * scale, k.value, k.inTangent / scale, k.outTangent / scale);
            }
            return new AnimationCurve(keys);
        }

        private static void SetMuscleCurve(AnimationClip clip, string muscleProp, params (float t, float v)[] pts)
        {
            var keys = new Keyframe[pts.Length];
            for (int i = 0; i < pts.Length; i++) keys[i] = new Keyframe(pts[i].t, pts[i].v);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscleProp), curve);
        }
        #endregion

        #region 3. Shared PBR Material Library
        private static Dictionary<string, Material> BuildMaterialLibrary()
        {
            var dict = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            // Skybox Cubemap Material
            var hdrCube = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Environment/External/HDRI/winter_evening_2k.hdr");
            Shader skyShader = Shader.Find("Skybox/Cubemap");
            if (skyShader != null && hdrCube != null)
            {
                string skyPath = $"{MAT_DIR}/Mat_Skybox_WinterEvening_BlueHour.mat";
                Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                if (skyMat == null) { skyMat = new Material(skyShader); AssetDatabase.CreateAsset(skyMat, skyPath); }
                skyMat.shader = skyShader;
                skyMat.SetTexture("_Tex", hdrCube);
                skyMat.SetColor("_Tint", new Color(0.36f, 0.43f, 0.56f, 1f));
                skyMat.SetFloat("_Exposure", 0.62f);
                skyMat.SetFloat("_Rotation", 145f);
                EditorUtility.SetDirty(skyMat);
                dict["Skybox"] = skyMat;
            }

            // 9 ambientCG Architectural Surface Materials
            dict["Asphalt"] = CreateAcgMaterial("Road012B", new Vector2(8f, 7f), new Color(0.62f, 0.64f, 0.68f), 0.35f);
            dict["Cobble"] = CreateAcgMaterial("PavingStones141", new Vector2(1.8f, 1.8f), new Color(0.72f, 0.72f, 0.74f), 0.30f);
            dict["Plaster"] = CreateAcgMaterial("Plaster007", new Vector2(3.2f, 1.8f), new Color(0.80f, 0.79f, 0.75f), 0.22f);
            dict["FacadePlaster"] = CreateAcgMaterial("Plaster007", new Vector2(5.0f, 2.2f), new Color(0.68f, 0.69f, 0.67f), 0.25f, "Mat_Acg_FacadePlaster");
            dict["Concrete"] = CreateAcgMaterial("Concrete034", new Vector2(3.5f, 3.5f), new Color(0.66f, 0.67f, 0.68f), 0.24f);
            dict["Brick"] = CreateAcgMaterial("Bricks097", new Vector2(6f, 1.8f), new Color(0.68f, 0.64f, 0.62f), 0.20f);
            dict["FloorTiles"] = CreateAcgMaterial("Tiles141", new Vector2(4.5f, 6.0f), new Color(0.74f, 0.74f, 0.72f), 0.38f);
            dict["PaintedWood"] = CreateAcgMaterial("PaintedWood009C", new Vector2(1.5f, 1.5f), new Color(0.76f, 0.74f, 0.70f), 0.28f);
            dict["DarkWood"] = CreateAcgMaterial("Wood035", new Vector2(1.5f, 1.5f), new Color(0.66f, 0.60f, 0.54f), 0.30f);
            dict["RustedMetal"] = CreateAcgMaterial("Metal064", new Vector2(1.6f, 1.6f), new Color(0.74f, 0.70f, 0.68f), 0.42f);

            // 16 Poly Haven Prop Materials
            string[] phIds = {
                "SchoolDesk_01", "SchoolChair_01", "WoodenTable_01", "metal_office_desk",
                "painted_wooden_cabinet", "wooden_bookshelf_worn", "Sofa_01", "wall_clock",
                "mounted_fluorescent_lights", "industrial_wall_lamp", "television_02",
                "binder_notebook", "clipboard", "cardboard_box_01", "fern_02", "weed_plant_02"
            };
            foreach (string pid in phIds)
            {
                bool cutout = pid.Contains("fern") || pid.Contains("weed");
                dict[pid] = CreatePolyHavenMaterial(pid, cutout);
            }

            // Glass / Emissive Tube Material for Lamps & Fluorescents
            dict["FluorescentTubeGlass"] = CreateSimpleStandardMat("Mat_Fluorescent_TubeGlass", new Color(0.55f, 0.58f, 0.58f, 1f), 0.1f, 0.75f);
            dict["ExteriorLampGlass"] = CreateSimpleStandardMat("Mat_ExteriorLamp_WarmGlass", new Color(0.85f, 0.72f, 0.48f, 1f), 0.05f, 0.80f, new Color(0.72f, 0.52f, 0.25f) * 0.85f);

            // Original Asset Materials (Feştek, Car, Sign, Door, Panel, Socket, Whiteboard, Poster, CRT Screen)
            dict["Festek"] = CreateCustomPbrMat("Mat_Original_Festek",
                "Assets/Environment/Original/Festek/Festek_Albedo.png",
                "Assets/Environment/Original/Festek/Festek_MetalSmooth.png", null, Color.white, 0.65f);
            dict["FestekButton"] = CreateSimpleStandardMat("Mat_Original_FestekButton", new Color(0.56f, 0.15f, 0.13f, 1f), 0.25f, 0.52f);

            dict["SignBoard"] = CreateCustomPbrMat("Mat_Original_HarputSignBoard",
                "Assets/Environment/Original/Signage/HarputSign_Backing_Albedo.jpg",
                "Assets/Environment/Original/Signage/HarputSign_Backing_MetalSmooth.png", null, Color.white, 0.42f);
            dict["SignLetters"] = CreateSimpleStandardMat("Mat_Original_HarputSignLetters", new Color(0.14f, 0.19f, 0.28f, 1f), 0.68f, 0.54f);

            dict["CarPaint"] = CreateSimpleStandardMat("Mat_Car_SlatePaint", new Color(0.15f, 0.19f, 0.25f, 1f), 0.68f, 0.68f);
            dict["CarTrim"] = CreateSimpleStandardMat("Mat_Car_TrimRubber", new Color(0.09f, 0.09f, 0.10f, 1f), 0.05f, 0.28f);
            dict["CarGlass"] = CreateTransparentStandardMat("Mat_Car_TintedGlass", new Color(0.06f, 0.09f, 0.12f, 0.76f), 0.15f, 0.90f);
            dict["CarInterior"] = CreateSimpleStandardMat("Mat_Car_InteriorDark", new Color(0.12f, 0.12f, 0.14f, 1f), 0.0f, 0.20f);
            dict["CarRim"] = CreateSimpleStandardMat("Mat_Car_AlloyRim", new Color(0.58f, 0.60f, 0.64f, 1f), 0.82f, 0.68f);
            dict["CarHeadlight"] = CreateSimpleStandardMat("Mat_Car_HeadlightWarm", new Color(0.88f, 0.86f, 0.78f, 1f), 0.20f, 0.85f, new Color(0.42f, 0.36f, 0.22f) * 0.45f);
            dict["CarTaillight"] = CreateSimpleStandardMat("Mat_Car_TaillightRed", new Color(0.55f, 0.08f, 0.08f, 1f), 0.15f, 0.82f, new Color(0.38f, 0.04f, 0.04f) * 0.40f);
            dict["CarPlate"] = CreateSimpleStandardMat("Mat_Car_WeatheredPlate", new Color(0.72f, 0.73f, 0.70f, 1f), 0.25f, 0.42f);

            dict["PanelSteel"] = CreateSimpleStandardMat("Mat_Panel_IndustrialSteel", new Color(0.30f, 0.33f, 0.32f, 1f), 0.72f, 0.44f);
            dict["PanelDark"] = CreateSimpleStandardMat("Mat_Panel_BreakerDark", new Color(0.11f, 0.12f, 0.13f, 1f), 0.25f, 0.32f);
            dict["PanelCopper"] = CreateSimpleStandardMat("Mat_Panel_CopperBus", new Color(0.70f, 0.40f, 0.22f, 1f), 0.88f, 0.62f);
            dict["PanelRed"] = CreateSimpleStandardMat("Mat_Panel_InsulatedRed", new Color(0.62f, 0.14f, 0.12f, 1f), 0.15f, 0.52f);
            dict["PanelHazard"] = CreateCustomPbrMat("Mat_Panel_HazardDecal",
                "Assets/Environment/Original/Electrical/Panel_HazardDecal.jpg", null, null, Color.white, 0.40f);

            dict["WhiteboardLesson"] = CreateCustomPbrMat("Mat_Whiteboard_B1Lesson",
                "Assets/Environment/Original/Props/Whiteboard_Lesson_Albedo.jpg", null, null, Color.white, 0.65f);
            dict["CoursePoster"] = CreateCustomPbrMat("Mat_Poster_IrregularVerbs",
                "Assets/Environment/Original/Props/CoursePoster_Verbs_Albedo.jpg", null, null, Color.white, 0.25f);

            // CRT Television Off & Powered Static Materials
            dict["TVScreenOff"] = CreateSimpleStandardMat("Mat_TV_Screen_Unpowered", new Color(0.04f, 0.05f, 0.055f, 1f), 0.08f, 0.82f);
            Material tvOn = CreateCustomPbrMat("Mat_TV_Screen_PoweredStatic",
                "Assets/Environment/Original/Props/CRT_Static_Scanlines.png", null, null, new Color(0.75f, 0.84f, 0.92f, 1f), 0.78f);
            tvOn.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Environment/Original/Props/CRT_Static_Scanlines.png"));
            tvOn.SetColor("_EmissionColor", new Color(0.58f, 0.68f, 0.78f) * 1.15f);
            tvOn.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(tvOn);
            dict["TVScreenOn"] = tvOn;

            return dict;
        }

        private static Material CreateAcgMaterial(string matId, Vector2 tiling, Color tint, float smoothnessFallback, string customName = null)
        {
            string dir = $"Assets/Environment/External/Textures/{matId}";
            string mName = customName ?? $"Mat_Acg_{matId}";
            string mPath = $"{MAT_DIR}/{mName}.mat";
            Material mat = LoadOrCreateStandardMat(mPath);

            Texture2D col = FindFirstTex(dir, "_Color");
            Texture2D nrm = FindFirstTex(dir, "_NormalGL");
            Texture2D ao = FindFirstTex(dir, "_AmbientOcclusion");
            Texture2D ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{matId}_MetalSmooth.png");

            mat.color = tint;
            if (col != null) mat.SetTexture("_MainTex", col);
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_GlossMapScale", 1.0f);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                mat.SetFloat("_Glossiness", smoothnessFallback);
            }
            if (nrm != null)
            {
                mat.SetTexture("_BumpMap", nrm);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 0.85f);
            }
            mat.mainTextureScale = tiling;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreatePolyHavenMaterial(string modelId, bool cutout)
        {
            string dir = $"Assets/Environment/External/Models/{modelId}";
            string mPath = $"{MAT_DIR}/Mat_PH_{modelId}.mat";
            Material mat = LoadOrCreateStandardMat(mPath);

            Texture2D diff = FindFirstTex(dir, "_diff_");
            Texture2D nrm = FindFirstTex(dir, "_nor_gl_");
            Texture2D ms = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{modelId}_MetalSmooth.png");
            Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{modelId}_AO.jpg");

            mat.color = Color.white;
            if (diff != null) mat.SetTexture("_MainTex", diff);
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_GlossMapScale", 1.0f);
                mat.EnableKeyword("_METALLICGLOSSMAP");
            }
            if (nrm != null)
            {
                mat.SetTexture("_BumpMap", nrm);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 0.85f);
            }
            if (cutout)
            {
                mat.SetFloat("_Mode", 1f); // Cutout
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 2450;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateCustomPbrMat(string mName, string albedoPath, string metalSmoothPath, string normalPath, Color tint, float fallbackSmooth)
        {
            Material mat = LoadOrCreateStandardMat($"{MAT_DIR}/{mName}.mat");
            mat.color = tint;
            if (!string.IsNullOrEmpty(albedoPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                if (tex != null) mat.SetTexture("_MainTex", tex);
            }
            if (!string.IsNullOrEmpty(metalSmoothPath))
            {
                var ms = AssetDatabase.LoadAssetAtPath<Texture2D>(metalSmoothPath);
                if (ms != null)
                {
                    mat.SetTexture("_MetallicGlossMap", ms);
                    mat.SetFloat("_GlossMapScale", 1.0f);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                }
            }
            else
            {
                mat.SetFloat("_Glossiness", fallbackSmooth);
            }
            if (!string.IsNullOrEmpty(normalPath))
            {
                var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (nrm != null)
                {
                    mat.SetTexture("_BumpMap", nrm);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateSimpleStandardMat(string mName, Color albedo, float metallic, float smoothness, Color emission = default)
        {
            Material mat = LoadOrCreateStandardMat($"{MAT_DIR}/{mName}.mat");
            mat.color = albedo;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
            if (emission.maxColorComponent > 0.005f)
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateTransparentStandardMat(string mName, Color albedo, float metallic, float smoothness)
        {
            Material mat = LoadOrCreateStandardMat($"{MAT_DIR}/{mName}.mat");
            mat.color = albedo;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.SetInt("_SrcBlend", (int)BlendMode.One);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateStandardMat(string path)
        {
            Shader std = Shader.Find("Standard");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(std);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (std != null)
            {
                m.shader = std;
            }
            return m;
        }

        private static Texture2D FindFirstTex(string folder, string substring)
        {
            if (!Directory.Exists(folder)) return null;
            foreach (string f in Directory.GetFiles(folder))
            {
                if (f.EndsWith(".meta")) continue;
                if (Path.GetFileName(f).IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(f.Replace("\\", "/"));
                }
            }
            return null;
        }
        #endregion

        #region 4. Scene Upgrade & Narrative Puzzle Wiring (AbandonedSchool.unity)
        private static void UpgradeAbandonedSchoolScene(Dictionary<string, Material> mats)
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            // Idempotent temizlik: daha önce oluşturulmuş üretim kökünü yenile
            GameObject oldProd = GameObject.Find(CHAPTER1_ROOT_NAME);
            if (oldProd != null) UnityEngine.Object.DestroyImmediate(oldProd);

            GameObject prodRoot = new GameObject(CHAPTER1_ROOT_NAME);
            GameObject extGroup = CreateGroup("Exterior_Courtyard_Props", prodRoot.transform);
            GameObject intGroup = CreateGroup("Interior_Course_Props", prodRoot.transform);
            GameObject puzzleGroup = CreateGroup("Puzzle_Electrical_Festek_TV", prodRoot.transform);
            GameObject audioGroup = CreateGroup("Spatial_Audio_Zones", prodRoot.transform);
            GameObject sysGroup = CreateGroup("Chapter1_Gameplay_Systems", prodRoot.transform);

            // 4A. Gökyüzü, Sis, Işıklandırma & Problar (Alacakaranlık Mavi Saat)
            ConfigureBlueHourAtmosphere(mats, prodRoot.transform);

            // 4B. Mevcut Mimari Yüzeylerin PBR Materyal Yükseltmesi (Yerleşim %100 korunur!)
            ApplyArchitecturalSurfaceMaterials(mats);

            // 4C. Dış Avlu: Gerçekçi Araba + Yabani Otlar + 3B HARPUT ENGLISH COURSE Tabelası
            BuildCourtyardAndFacadeSign(mats, extGroup.transform);

            // 4D. Ana Giriş Kapısı: Detaylı Çift Kanatlı Kapı, Menteşeler, Kollar ve Sol El IK + Ses
            DoorInteractable mainEntranceDoor = UpgradeMainEntranceDoor(mats, extGroup.transform);

            // 4E. İç Mekan Derslikler & Koridor Donanımı + Tavan Floresanları (Başlangıçta elektriksiz)
            var poweredFixtures = new List<FacilityLightFixture>();
            PopulateClassroomsAndCorridor(mats, intGroup.transform, poweredFixtures);

            // 4F. Personel & Arşiv Odası (Teachers_Lounge): Pano -> Feştek Fiziksel Düşüş -> Yuva -> Sağdaki CRT TV
            BuildPuzzleRoomAndPowerChain(mats, puzzleGroup.transform, sysGroup.transform, mainEntranceDoor, poweredFixtures);

            // 4G. Dış Rüzgar ve İç Mekan Oda Sesi (Spatial 3D Ambience)
            BuildSpatialAudioZones(audioGroup.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureBlueHourAtmosphere(Dictionary<string, Material> mats, Transform root)
        {
            if (mats.TryGetValue("Skybox", out Material skyMat) && skyMat != null)
            {
                RenderSettings.skybox = skyMat;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.15f, 0.19f, 0.29f);
            RenderSettings.ambientEquatorColor = new Color(0.09f, 0.11f, 0.17f);
            RenderSettings.ambientGroundColor = new Color(0.045f, 0.05f, 0.065f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.065f, 0.085f, 0.135f);
            RenderSettings.fogDensity = 0.017f;

            // Alacakaranlık ay/ufuk yönlü ışığı (yumuşak gölgeli tek dış ana ışık)
            GameObject moonGo = GameObject.Find("Moonlight_Directional");
            if (moonGo != null)
            {
                moonGo.transform.rotation = Quaternion.Euler(28f, -34f, 0f);
                var dirLight = moonGo.GetComponent<Light>();
                if (dirLight != null)
                {
                    dirLight.color = new Color(0.48f, 0.58f, 0.78f);
                    dirLight.intensity = 0.52f;
                    dirLight.shadows = LightShadows.Soft;
                    dirLight.shadowStrength = 0.78f;
                }
            }

            // Giriş tabelasını ve kapıyı hafifçe okutan dış cephe lambası
            GameObject extLampGo = GameObject.Find("Light_Courtyard_EntranceLamp");
            if (extLampGo != null)
            {
                extLampGo.transform.position = new Vector3(0f, 3.95f, -1.35f);
                var l = extLampGo.GetComponent<Light>();
                if (l != null)
                {
                    l.color = new Color(0.92f, 0.72f, 0.46f);
                    l.intensity = 0.85f;
                    l.range = 6.8f;
                    l.shadows = LightShadows.None;
                    l.enabled = true;
                }
            }

            // Doğaüstü / aşırı renkli prototip ışıklarını kapat, iç mekan ışıklarını elektrik gelene kadar kapalı tut
            DisableOrMuteLight("Light_C103_OccultGlow", false, 0f);
            DisableOrMuteLight("Light_Elevator_EerieRed", true, 0.18f); // Sadece acil durum zayıf pil ledi
            DisableOrMuteLight("Light_Entrance_Hallway", false, 0f);
            DisableOrMuteLight("Light_Mid_Corridor", false, 0f);
            DisableOrMuteLight("Light_Staircase_Dim", true, 0.16f); // Merdiven penceresinden sızan loş mavi alacakaranlık

            var stairL = GameObject.Find("Light_Staircase_Dim")?.GetComponent<Light>();
            if (stairL != null) stairL.color = new Color(0.38f, 0.48f, 0.68f);

            // Reflection Probes & LightProbeGroup
            CreateReflectionProbe("RefProbe_Courtyard", root, new Vector3(0f, 2.2f, -10f), new Vector3(32f, 10f, 26f), 0.55f);
            CreateReflectionProbe("RefProbe_Interior", root, new Vector3(2f, 2.0f, 8f), new Vector3(24f, 6f, 28f), 0.35f);

            var lpgGo = new GameObject("LightProbeGroup_Chapter1");
            lpgGo.transform.SetParent(root, false);
            var lpg = lpgGo.AddComponent<LightProbeGroup>();
            lpg.probePositions = new Vector3[]
            {
                new Vector3(0f, 1.5f, -15f), new Vector3(2.5f, 1.5f, -16f),
                new Vector3(0f, 1.5f, -6f),  new Vector3(0f, 1.5f, -1.2f),
                new Vector3(0f, 1.5f, 3.0f), new Vector3(0f, 1.5f, 7.0f),
                new Vector3(4.2f, 1.5f, 7.8f), new Vector3(6.8f, 1.5f, 7.8f),
                new Vector3(-5.5f, 1.5f, 6.0f), new Vector3(0f, 1.5f, 15.0f)
            };
        }

        private static void ApplyArchitecturalSurfaceMaterials(Dictionary<string, Material> mats)
        {
            var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            foreach (var mr in allRenderers)
            {
                if (mr == null || mr.transform.IsChildOf(GameObject.Find("Player_Protagonist")?.transform ?? mr.transform))
                    continue;
                if (mr.gameObject.name.Contains("Player")) continue;

                string n = mr.gameObject.name;
                if (n == "Courtyard_AsphaltGround") mr.sharedMaterial = mats["Asphalt"];
                else if (n.StartsWith("Path_Cobble")) mr.sharedMaterial = mats["Cobble"];
                else if (n.StartsWith("Courtyard_Wall")) mr.sharedMaterial = mats["Brick"];
                else if (n.StartsWith("School_FrontFacade")) mr.sharedMaterial = mats["FacadePlaster"];
                else if (n.EndsWith("_Floor") || n == "Corridor_Floor") mr.sharedMaterial = mats["FloorTiles"];
                else if (n.EndsWith("_Ceiling") || n.Contains("Pillar") || n.Contains("Stair")) mr.sharedMaterial = mats["Concrete"];
                else if (n.Contains("Wall") || n.Contains("Lintel")) mr.sharedMaterial = mats["Plaster"];
                else if (n.Contains("Door") && !n.StartsWith("Door_Visual_")) mr.sharedMaterial = mats["PaintedWood"];
                else if (n.Contains("Barricade") || n.Contains("Podium")) mr.sharedMaterial = mats["DarkWood"];
                else if (n.StartsWith("Elevator_Door") || n.Contains("IronGate") || n.Contains("Locker")) mr.sharedMaterial = mats["RustedMetal"];
            }

            // C103 doğaüstü sembol çıkartmasını gizle (Bölüm I tamamen gerçekçi psikolojik ton)
            var occultDecal = GameObject.Find("C103_CenterSymbol_Decal");
            if (occultDecal != null) occultDecal.SetActive(false);
        }

        private static void BuildCourtyardAndFacadeSign(Dictionary<string, Material> mats, Transform parent)
        {
            // 1. Eski küp araba görselini gizle ve yerine gerçekçi ArrivedSedan_Car.fbx yerleştir
            GameObject oldCar = GameObject.Find("Courtyard_ArrivedCar");
            Vector3 carPos = new Vector3(2.85f, 0.0f, -17.6f);
            if (oldCar != null)
            {
                var oldMr = oldCar.GetComponent<MeshRenderer>();
                if (oldMr != null) oldMr.enabled = false;
                var oldCol = oldCar.GetComponent<Collider>();
                if (oldCol != null) oldCol.enabled = false;
            }

            GameObject carInst = InstantiateFbx("Assets/Environment/Original/Vehicle/ArrivedSedan_Car.fbx",
                "Protagonist_ArrivedSedan", parent, carPos, Quaternion.Euler(0f, -18f, 0f), Vector3.one, isStatic: true);
            if (carInst != null)
            {
                foreach (var r in carInst.GetComponentsInChildren<Renderer>(true))
                {
                    string rn = r.gameObject.name;
                    if (rn.Contains("Body") || rn.Contains("MirrorCap")) r.sharedMaterial = mats["CarPaint"];
                    else if (rn.Contains("Glass") || rn.Contains("Window") || rn.Contains("Windshield")) r.sharedMaterial = mats["CarGlass"];
                    else if (rn.Contains("Interior") || rn.Contains("Dashboard") || rn.Contains("Seat") || rn.Contains("Bench")) r.sharedMaterial = mats["CarInterior"];
                    else if (rn.Contains("Rim") || rn.Contains("Spoke") || rn.Contains("Exhaust")) r.sharedMaterial = mats["CarRim"];
                    else if (rn.Contains("Headlight")) r.sharedMaterial = mats["CarHeadlight"];
                    else if (rn.Contains("Taillight")) r.sharedMaterial = mats["CarTaillight"];
                    else if (rn.Contains("Plate")) r.sharedMaterial = mats["CarPlate"];
                    else r.sharedMaterial = mats["CarTrim"];
                }

                var carBox = carInst.AddComponent<BoxCollider>();
                carBox.center = new Vector3(0f, 0.72f, 0f);
                carBox.size = new Vector3(1.82f, 1.42f, 4.46f);

                // Yeni park edilmiş arabanın hafif gösterge / park lambası ışıması (gölgesiz, mobil dostu)
                var dashGlow = new GameObject("Car_SubtleParkingGlow");
                dashGlow.transform.SetParent(carInst.transform, false);
                dashGlow.transform.localPosition = new Vector3(0f, 0.72f, 2.35f);
                var cl = dashGlow.AddComponent<Light>();
                cl.type = LightType.Point;
                cl.color = new Color(0.95f, 0.78f, 0.52f);
                cl.intensity = 0.45f;
                cl.range = 3.2f;
                cl.shadows = LightShadows.None;
            }

            // 2. Bina Ana Girişi Üzerine 3B Kabartma "HARPUT / ENGLISH COURSE" Metal Tabela
            GameObject signInst = InstantiateFbx("Assets/Environment/Original/Signage/HarputCourse_Sign.fbx",
                "HarputEnglishCourse_3DSign", parent, new Vector3(0f, 3.68f, -0.28f), Quaternion.Euler(0f, 180f, -0.5f), Vector3.one, isStatic: true);
            if (signInst != null)
            {
                foreach (var r in signInst.GetComponentsInChildren<Renderer>(true))
                {
                    string rn = r.gameObject.name;
                    if (rn.Contains("BackingPlate")) r.sharedMaterial = mats["SignBoard"];
                    else if (rn.Contains("3DText")) r.sharedMaterial = mats["SignLetters"];
                    else r.sharedMaterial = mats["RustedMetal"];
                }
            }

            // Girişin iki yanına endüstriyel duvar aplikleri
            foreach (float lx in new[] { -1.45f, 1.45f })
            {
                var wlamp = InstantiateFbx("Assets/Environment/External/Models/industrial_wall_lamp/industrial_wall_lamp.fbx",
                    $"Exterior_WallLamp_{(lx < 0 ? "L" : "R")}", parent, new Vector3(lx, 2.55f, -0.16f), Quaternion.Euler(0f, 180f, 0f), Vector3.one, isStatic: true);
                AssignPolyHavenMats(wlamp, mats["industrial_wall_lamp"], mats["ExteriorLampGlass"]);
            }

            // 3. Avlu çatlakları ve duvar diplerinde yabani otlar & eğrelti otları (bakımsızlık hissi)
            (Vector3 pos, float yaw, float sc, bool isFern)[] weeds = {
                (new Vector3(-1.35f, 0.0f, -0.45f), 25f, 0.85f, false),
                (new Vector3( 1.55f, 0.0f, -0.42f), -40f, 0.90f, true),
                (new Vector3(-1.15f, 0.0f, -5.8f),  72f, 0.80f, false),
                (new Vector3( 1.20f, 0.0f, -9.2f),  145f, 0.75f, false),
                (new Vector3(-3.8f,  0.0f, -14.5f), 12f, 1.05f, true),
                (new Vector3( 4.6f,  0.0f, -12.8f), -85f, 0.95f, true),
                (new Vector3(-6.2f,  0.0f, -1.1f),  35f, 1.10f, true),
                (new Vector3( 5.8f,  0.0f, -1.0f),  160f, 1.00f, false)
            };
            for (int i = 0; i < weeds.Length; i++)
            {
                var w = weeds[i];
                string fbx = w.isFern
                    ? "Assets/Environment/External/Models/fern_02/fern_02.fbx"
                    : "Assets/Environment/External/Models/weed_plant_02/weed_plant_02.fbx";
                var wgo = InstantiateFbx(fbx, $"Courtyard_Overgrowth_{i}", parent, w.pos, Quaternion.Euler(0f, w.yaw, 0f), Vector3.one * w.sc, isStatic: true);
                AssignPolyHavenMats(wgo, w.isFern ? mats["fern_02"] : mats["weed_plant_02"], null);
            }
        }

        private static DoorInteractable UpgradeMainEntranceDoor(Dictionary<string, Material> mats, Transform fallbackParent)
        {
            GameObject doorway = GameObject.Find("SchoolEntrance_Doorway");
            if (doorway == null) return null;

            // Eski küp kapı görsellerini gizle
            foreach (string oldVis in new[] { "Door_Visual_L", "Door_Visual_R" })
            {
                var visGo = FindDeep(doorway.transform, oldVis);
                if (visGo != null)
                {
                    var mr = visGo.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                }
            }

            Transform existingDetailed = doorway.transform.Find("Detailed_Entrance_DoorSet");
            if (existingDetailed != null) UnityEngine.Object.DestroyImmediate(existingDetailed.gameObject);

            // Mevcut 1.20m x 2.50m duvar açıklığına milimetrik uyacak ölçek (0.53 x 0.90 x 1.0); kanatlar içeri (+Z) açılır!
            GameObject doorModel = InstantiateFbx("Assets/Environment/Original/Doors/SchoolEntrance_DetailedDoor.fbx",
                "Detailed_Entrance_DoorSet", doorway.transform, Vector3.zero, Quaternion.identity, new Vector3(0.53f, 0.90f, 1.0f), isStatic: false);

            Transform leafL = null;
            Transform leafR = null;
            if (doorModel != null)
            {
                foreach (var r in doorModel.GetComponentsInChildren<Renderer>(true))
                {
                    string rn = r.gameObject.name;
                    if (rn.Contains("Glass")) r.sharedMaterial = mats["CarGlass"];
                    else if (rn.Contains("Hinge") || rn.Contains("Handle") || rn.Contains("Lock") || rn.Contains("Kickplate") || rn.Contains("Threshold") || rn.Contains("Astragal"))
                        r.sharedMaterial = mats["RustedMetal"];
                    else if (rn.Contains("Jamb") || rn.Contains("Header") || rn.Contains("Transom"))
                        r.sharedMaterial = mats["DarkWood"];
                    else
                        r.sharedMaterial = mats["PaintedWood"];
                }

                leafL = FindDeep(doorModel.transform, "DoorLeaf_L");
                leafR = FindDeep(doorModel.transform, "DoorLeaf_R");
                if (leafL != null)
                {
                    Transform hTarget = FindDeep(leafL, "Door_Handle_Target");
                    if (hTarget != null)
                    {
                        Vector3 lp = hTarget.localPosition;
                        hTarget.localPosition = new Vector3(lp.x, lp.y, -Mathf.Abs(lp.z));
                    }
                }
            }

            var doorInteract = doorway.GetComponent<DoorInteractable>();
            if (doorInteract == null) doorInteract = doorway.AddComponent<DoorInteractable>();

            Collider blocker = doorway.GetComponent<BoxCollider>();
            if (leafL != null && leafR != null)
            {
                doorInteract.Configure(leafL, leafR, blocker);
            }

            AudioSource doorAudio = doorway.GetComponent<AudioSource>();
            if (doorAudio == null) doorAudio = doorway.AddComponent<AudioSource>();
            ConfigureSpatialAudioSource(doorAudio, 1.0f, 14f);

            var latchClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Door_HandleLatch.wav");
            var openClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Door_HeavyHingeOpen.wav");
            var closeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Door_HeavyHingeClose.wav");
            doorInteract.ConfigureAudio(doorAudio, latchClip, openClip, closeClip);

            return doorInteract;
        }

        private static void PopulateClassroomsAndCorridor(
            Dictionary<string, Material> mats,
            Transform parent,
            List<FacilityLightFixture> poweredFixtures)
        {
            // --- A. ANA KORİDOR (Corridor_Main: x in [-2, 2], z in [0, 26]) ---
            float[] corridorLightZ = { 3.5f, 8.8f, 15.2f, 21.8f };
            for (int i = 0; i < corridorLightZ.Length; i++)
            {
                bool broken = (i == 2); // 3. koridor floresanı arızalı/loş kalarak korku kontrastını korur!
                AddCeilingFluorescent($"Corridor_Fluorescent_{i}", parent, new Vector3(0f, 3.46f, corridorLightZ[i]), 0f, mats, poweredFixtures, broken, 0.90f);
            }

            // Koridor radyatörleri, ders posterleri ve durmuş duvar saati
            PlaceRadiator("Corridor_Radiator_1", parent, new Vector3(-1.82f, 0f, 3.8f), 90f, mats["RustedMetal"]);
            PlaceRadiator("Corridor_Radiator_2", parent, new Vector3( 1.82f, 0f, 18.2f), -90f, mats["RustedMetal"]);
            PlaceWallPoster("Corridor_Poster_1", parent, new Vector3(1.83f, 1.62f, 4.4f), -90f, mats["CoursePoster"]);
            PlaceWallPoster("Corridor_Poster_2", parent, new Vector3(-1.83f, 1.62f, 10.0f), 90f, mats["CoursePoster"]);

            var corrClock = InstantiateFbx("Assets/Environment/External/Models/wall_clock/wall_clock.fbx",
                "Corridor_WallClock", parent, new Vector3(0f, 2.85f, 25.82f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 1.15f, isStatic: true);
            AssignPolyHavenMats(corrClock, mats["wall_clock"], mats["CarGlass"]);

            // Girişten personel odasına (z=6.8) uzanan görünür tavan elektrik boru hattı (oyuncuyu doğal olarak yönlendirir!)
            CreateConduitSegment("Conduit_Corridor_To_Lounge_1", parent, new Vector3(1.55f, 3.35f, 3.4f), new Vector3(0.04f, 0.04f, 6.8f), mats["RustedMetal"]);
            CreateConduitSegment("Conduit_Corridor_To_Lounge_Turn", parent, new Vector3(2.85f, 3.35f, 6.8f), new Vector3(2.6f, 0.04f, 0.04f), mats["RustedMetal"]);

            // --- B. DERSLİK 101 (Classroom_101: Akşam B1 İngilizce Sınıfı) ---
            HidePrimitiveRenderers("C101_Blackboard", "C101_Desk_1", "C101_Desk_1_Chair", "C101_Desk_2", "C101_Desk_2_Chair", "C101_Desk_3", "C101_Desk_3_Chair", "C101_TeacherDesk");

            var wb101 = InstantiateFbx("Assets/Environment/Original/Props/Whiteboard_Classroom.fbx",
                "C101_Whiteboard_B1", parent, new Vector3(-6.0f, 1.68f, 2.70f), Quaternion.identity, Vector3.one, isStatic: true);
            AssignWhiteboardMats(wb101, mats["WhiteboardLesson"], mats["CarRim"]);

            var tDesk101 = InstantiateFbx("Assets/Environment/External/Models/metal_office_desk/metal_office_desk.fbx",
                "C101_TeacherOfficeDesk", parent, new Vector3(-6.0f, 0f, 3.75f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * 0.92f, isStatic: true);
            AssignPolyHavenMats(tDesk101, mats["metal_office_desk"], null);

            var shelf101 = InstantiateFbx("Assets/Environment/External/Models/wooden_bookshelf_worn/wooden_bookshelf_worn.fbx",
                "C101_Bookshelf", parent, new Vector3(-8.8f, 0f, 2.95f), Quaternion.identity, Vector3.one * 0.95f, isStatic: true);
            AssignPolyHavenMats(shelf101, mats["wooden_bookshelf_worn"], null);

            Vector3[] c101Desks = {
                new Vector3(-4.5f, 0f, 5.5f), new Vector3(-7.5f, 0f, 5.5f),
                new Vector3(-4.5f, 0f, 7.5f), new Vector3(-7.5f, 0f, 7.5f)
            };
            for (int i = 0; i < c101Desks.Length; i++)
            {
                Vector3 dp = c101Desks[i];
                var d = InstantiateFbx("Assets/Environment/External/Models/SchoolDesk_01/SchoolDesk_01.fbx",
                    $"C101_RealDesk_{i}", parent, dp, Quaternion.Euler(0f, 180f, 0f), Vector3.one, isStatic: true);
                AssignPolyHavenMats(d, mats["SchoolDesk_01"], null);

                float chairYaw = (i == 1) ? 162f : 180f;
                Vector3 chairOff = (i == 1) ? new Vector3(0.14f, 0f, 0.58f) : new Vector3(0f, 0f, 0.48f);
                var ch = InstantiateFbx("Assets/Environment/External/Models/SchoolChair_01/SchoolChair_01.fbx",
                    $"C101_RealChair_{i}", parent, dp + chairOff, Quaternion.Euler(0f, chairYaw, 0f), Vector3.one, isStatic: true);
                AssignPolyHavenMats(ch, mats["SchoolChair_01"], null);
            }

            // Masa üstü defter & yoklama panosu (yaşanmışlık detayı)
            var binder1 = InstantiateFbx("Assets/Environment/External/Models/binder_notebook/binder_notebook.fbx",
                "C101_StudentNotebook", parent, new Vector3(-4.45f, 0.76f, 5.48f), Quaternion.Euler(0f, 24f, 0f), Vector3.one, isStatic: true);
            AssignPolyHavenMats(binder1, mats["binder_notebook"], null);

            var clip1 = InstantiateFbx("Assets/Environment/External/Models/clipboard/clipboard.fbx",
                "C101_AttendanceClipboard", parent, new Vector3(-5.75f, 0.79f, 3.72f), Quaternion.Euler(0f, -32f, 0f), Vector3.one, isStatic: true);
            AssignPolyHavenMats(clip1, mats["clipboard"], null);

            PlaceRadiator("C101_WindowRadiator", parent, new Vector3(-9.72f, 0f, 6.2f), 90f, mats["RustedMetal"]);
            AddCeilingFluorescent("C101_Fluorescent", parent, new Vector3(-6.0f, 3.46f, 6.0f), 90f, mats, poweredFixtures, false, 0.85f);

            // C101 penceresinden sızan çok hafif alacakaranlık dolgu ışığı (elektrik yokken bile silüet okunurluğu)
            CreateSubtleWindowFill("C101_TwilightWindowFill", parent, new Vector3(-9.2f, 2.1f, 6.0f), 6.5f, 0.26f);

            // --- C. DERSLİK 102 & 103 ---
            HidePrimitiveRenderers("C102_Blackboard", "C102_Desk_A", "C102_Desk_A_Chair", "C102_Desk_B", "C102_Desk_B_Chair", "C103_Blackboard");
            var wb102 = InstantiateFbx("Assets/Environment/Original/Props/Whiteboard_Classroom.fbx",
                "C102_Whiteboard", parent, new Vector3(-6.0f, 1.68f, 10.70f), Quaternion.identity, Vector3.one, isStatic: true);
            AssignWhiteboardMats(wb102, mats["WhiteboardLesson"], mats["CarRim"]);

            foreach (var pos in new[] { new Vector3(-4.8f, 0f, 13.5f), new Vector3(-7.4f, 0f, 14.2f) })
            {
                var d = InstantiateFbx("Assets/Environment/External/Models/SchoolDesk_01/SchoolDesk_01.fbx",
                    "C102_RealDesk", parent, pos, Quaternion.Euler(0f, 172f, 0f), Vector3.one, isStatic: true);
                AssignPolyHavenMats(d, mats["SchoolDesk_01"], null);
            }
            var box103 = InstantiateFbx("Assets/Environment/External/Models/cardboard_box_01/cardboard_box_01.fbx",
                "C103_ArchiveBox", parent, new Vector3(5.4f, 0f, 13.2f), Quaternion.Euler(0f, 38f, 0f), Vector3.one * 1.15f, isStatic: true);
            AssignPolyHavenMats(box103, mats["cardboard_box_01"], null);
        }

        private static void BuildPuzzleRoomAndPowerChain(
            Dictionary<string, Material> mats,
            Transform puzzleParent,
            Transform sysParent,
            DoorInteractable mainEntranceDoor,
            List<FacilityLightFixture> poweredFixtures)
        {
            // 1. Teachers_Lounge (x in [2, 10], z in [2.5, 9.5]) eski küp mobilyalarını gerçek CC0 modellerle değiştir
            // Kuzey duvarı önünde (x in [2.8, 8.0], z in [7.15, 9.25]) geniş, engelsiz bulmaca koridoru bırak!
            DisableObjectCompletely("Lounge_Sofa");
            DisableObjectCompletely("Lounge_Chair_2");
            HidePrimitiveRenderers("Lounge_MeetingTable", "Lounge_Chair_1", "Lounge_FilingCabinet1", "Lounge_FilingCabinet2");

            // Toplantı masasını hafif güneye (z=5.4) çek ki kuzey pano-soket-TV alanında 2.8m ferah boşluk kalsın
            var oldTable = GameObject.Find("Lounge_MeetingTable");
            if (oldTable != null) oldTable.transform.position = new Vector3(6.0f, 0.38f, 5.2f);
            var oldChair1 = GameObject.Find("Lounge_Chair_1");
            if (oldChair1 != null) oldChair1.transform.position = new Vector3(6.0f, 0.35f, 4.2f);

            var realTable = InstantiateFbx("Assets/Environment/External/Models/WoodenTable_01/WoodenTable_01.fbx",
                "Lounge_RealWoodenTable", puzzleParent, new Vector3(6.0f, 0f, 5.2f), Quaternion.identity, new Vector3(1.45f, 1.35f, 1.45f), isStatic: true);
            AssignPolyHavenMats(realTable, mats["WoodenTable_01"], null);

            var realSofa = InstantiateFbx("Assets/Environment/External/Models/Sofa_01/Sofa_01.fbx",
                "Lounge_RealSofa", puzzleParent, new Vector3(4.2f, 0f, 3.15f), Quaternion.identity, Vector3.one * 1.05f, isStatic: true);
            AssignPolyHavenMats(realSofa, mats["Sofa_01"], null);

            var realChair = InstantiateFbx("Assets/Environment/External/Models/SchoolChair_01/SchoolChair_01.fbx",
                "Lounge_RealChair", puzzleParent, new Vector3(6.0f, 0f, 4.35f), Quaternion.identity, Vector3.one, isStatic: true);
            AssignPolyHavenMats(realChair, mats["SchoolChair_01"], null);

            var bookshelf = InstantiateFbx("Assets/Environment/External/Models/wooden_bookshelf_worn/wooden_bookshelf_worn.fbx",
                "Lounge_ArchiveBookshelf", puzzleParent, new Vector3(9.45f, 0f, 4.6f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 0.95f, isStatic: true);
            AssignPolyHavenMats(bookshelf, mats["wooden_bookshelf_worn"], null);

            var boxLounge = InstantiateFbx("Assets/Environment/External/Models/cardboard_box_01/cardboard_box_01.fbx",
                "Lounge_StorageBox", puzzleParent, new Vector3(8.55f, 0f, 3.25f), Quaternion.Euler(0f, -24f, 0f), Vector3.one, isStatic: true);
            AssignPolyHavenMats(boxLounge, mats["cardboard_box_01"], null);

            var clipTable = InstantiateFbx("Assets/Environment/External/Models/clipboard/clipboard.fbx",
                "Lounge_MaintenanceClipboard", puzzleParent, new Vector3(5.55f, 0.75f, 5.35f), Quaternion.Euler(0f, 18f, 0f), Vector3.one, isStatic: true);
            AssignPolyHavenMats(clipTable, mats["clipboard"], null);

            PlaceRadiator("Lounge_WindowRadiator", puzzleParent, new Vector3(9.72f, 0f, 6.8f), -90f, mats["RustedMetal"]);
            CreateSubtleWindowFill("Lounge_TwilightWindowFill", puzzleParent, new Vector3(9.1f, 2.1f, 6.8f), 6.2f, 0.25f);

            // Oda tavan floresanları (elektrik gelince titreşerek yanacak)
            AddCeilingFluorescent("Lounge_Fluorescent_West", puzzleParent, new Vector3(4.5f, 3.46f, 6.4f), 0f, mats, poweredFixtures, false, 1.05f);
            AddCeilingFluorescent("Lounge_Fluorescent_East", puzzleParent, new Vector3(7.6f, 3.46f, 6.4f), 0f, mats, poweredFixtures, true, 0.75f);

            // =========================================================================
            // 2. ANA ELEKTRİK PANOSU (Sol-Kuzey: x=3.85, y=1.48, z=9.24, -Z yönüne bakar)
            // =========================================================================
            Vector3 panelPos = new Vector3(3.85f, 1.48f, 9.24f);
            Vector3 safeDropFloorPos = new Vector3(3.85f, 0.05f, 8.52f);

            GameObject panelGo = InstantiateFbx("Assets/Environment/Original/Electrical/ElectricalPanel_Unit.fbx",
                "ElectricalPanel_Station", puzzleParent, panelPos, Quaternion.Euler(0f, 180f, 0f), Vector3.one, isStatic: false);
            foreach (var r in panelGo.GetComponentsInChildren<Renderer>(true))
            {
                string rn = r.gameObject.name;
                if (rn.Contains("Hazard")) r.sharedMaterial = mats["PanelHazard"];
                else if (rn.Contains("Copper") || rn.Contains("Busbar") || rn.Contains("DinRail") || rn.Contains("Blade")) r.sharedMaterial = mats["PanelCopper"];
                else if (rn.Contains("GripBar") || rn.Contains("_0")) r.sharedMaterial = mats["PanelRed"];
                else if (rn.Contains("Breaker") || rn.Contains("BaseBlock") || rn.Contains("Latch")) r.sharedMaterial = mats["PanelDark"];
                else r.sharedMaterial = mats["PanelSteel"];
            }
            var panelCol = panelGo.AddComponent<BoxCollider>();
            panelCol.center = new Vector3(0f, 0f, 0.02f);
            panelCol.size = new Vector3(0.72f, 0.94f, 0.26f);

            AudioSource panelAudio = panelGo.AddComponent<AudioSource>();
            ConfigureSpatialAudioSource(panelAudio, 1.0f, 12f);

            // =========================================================================
            // 3. ÖZEL FEŞTEK CİHAZI (Başlangıçta panonun iç alt yuvasında durur!)
            // =========================================================================
            Transform festekSpawnAnchor = FindDeep(panelGo.transform, "Festek_Spawn_Anchor") ?? panelGo.transform;
            GameObject festekGo = InstantiateFbx("Assets/Environment/Original/Festek/Festek_Device.fbx",
                "Festek_CustomDevice", festekSpawnAnchor, Vector3.zero, Quaternion.identity, Vector3.one, isStatic: false);
            if (festekSpawnAnchor == panelGo.transform)
            {
                festekGo.transform.localPosition = new Vector3(-0.11f, -0.25f, 0.02f);
            }

            Renderer lod0Rend = null;
            Renderer lod1Rend = null;
            foreach (var r in festekGo.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.name.Contains("Button")) r.sharedMaterial = mats["FestekButton"];
                else r.sharedMaterial = mats["Festek"];

                if (r.gameObject.name.Contains("LOD0")) lod0Rend = r;
                else if (r.gameObject.name.Contains("LOD1")) lod1Rend = r;
            }
            if (lod0Rend != null && lod1Rend != null)
            {
                var lodGrp = festekGo.GetComponentInChildren<LODGroup>(true) ?? festekGo.AddComponent<LODGroup>();
                if (lodGrp != null)
                {
                    Renderer btnRend = FindDeep(festekGo.transform, "Festek_Button_Cap")?.GetComponent<Renderer>();
                    var lod0Arr = btnRend != null ? new[] { lod0Rend, btnRend } : new[] { lod0Rend };
                    lodGrp.SetLODs(new[]
                    {
                        new LOD(0.18f, lod0Arr),
                        new LOD(0.015f, new[] { lod1Rend })
                    });
                    lodGrp.RecalculateBounds();
                }
            }

            AudioSource festekAudio = festekGo.AddComponent<AudioSource>();
            ConfigureSpatialAudioSource(festekAudio, 0.8f, 12f);

            var festekComp = festekGo.AddComponent<FestekItem>();
            festekComp.Configure(
                festekAudio,
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Festek_MetalDropImpact.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Festek_Pickup.wav"),
                safeDropFloorPos);

            var panelInteract = panelGo.AddComponent<ElectricalPanelInteractable>();
            panelInteract.Configure(
                FindDeep(panelGo.transform, "Main_Lever_Pivot"),
                FindDeep(panelGo.transform, "Lever_Hand_Target"),
                festekComp,
                safeDropFloorPos,
                panelAudio,
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Panel_LeverPull.wav"));

            // =========================================================================
            // 4. FEŞTEK BAĞLANTI YUVASI & DÜĞMESİ (Orta-Kuzey: x=5.65, y=1.34, z=9.24)
            // =========================================================================
            GameObject socketGo = InstantiateFbx("Assets/Environment/Original/Electrical/FestekSocket_Unit.fbx",
                "FestekSocket_Station", puzzleParent, new Vector3(5.65f, 1.34f, 9.24f), Quaternion.Euler(0f, 180f, 0f), Vector3.one, isStatic: false);
            foreach (var r in socketGo.GetComponentsInChildren<Renderer>(true))
            {
                string rn = r.gameObject.name;
                if (rn.Contains("Collar") || rn.Contains("Bolt") || rn.Contains("Bezel") || rn.Contains("Guard"))
                    r.sharedMaterial = mats["PanelCopper"];
                else if (rn.Contains("Activation_Button"))
                    r.sharedMaterial = mats["PanelRed"];
                else if (rn.Contains("StatusLens"))
                    r.sharedMaterial = mats["FluorescentTubeGlass"];
                else if (rn.Contains("Bore"))
                    r.sharedMaterial = mats["PanelDark"];
                else
                    r.sharedMaterial = mats["PanelSteel"];
            }
            var socketCol = socketGo.AddComponent<BoxCollider>();
            socketCol.center = new Vector3(0f, 0.02f, 0.04f);
            socketCol.size = new Vector3(0.44f, 0.54f, 0.26f);

            AudioSource socketAudio = socketGo.AddComponent<AudioSource>();
            ConfigureSpatialAudioSource(socketAudio, 0.9f, 12f);

            // =========================================================================
            // 5. BULMACA ALANININ SAĞ TARAFINDAKİ TERK EDİLMİŞ CRT TELEVİZYON (x=7.55, z=8.78)
            // =========================================================================
            var tvCabinet = InstantiateFbx("Assets/Environment/External/Models/painted_wooden_cabinet/painted_wooden_cabinet.fbx",
                "PuzzleRight_TVCabinet", puzzleParent, new Vector3(7.55f, 0f, 8.78f), Quaternion.Euler(0f, -152f, 0f), Vector3.one, isStatic: true);
            AssignPolyHavenMats(tvCabinet, mats["painted_wooden_cabinet"], null);
            var cabCol = tvCabinet.AddComponent<BoxCollider>();
            cabCol.center = new Vector3(0f, 0.59f, 0f);
            cabCol.size = new Vector3(1.18f, 1.18f, 0.62f);

            GameObject tvGo = InstantiateFbx("Assets/Environment/External/Models/television_02/television_02.fbx",
                "Abandoned_CRT_Television", puzzleParent, new Vector3(7.55f, 1.18f, 8.78f), Quaternion.Euler(0f, -152f, 0f), Vector3.one * 1.18f, isStatic: false);
            AssignPolyHavenMats(tvGo, mats["television_02"], null);

            // CRT ön tüp camına tam oturan parazit ekran yüzeyi
            GameObject screenQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenQuad.name = "TV_CRT_ScreenQuad";
            UnityEngine.Object.DestroyImmediate(screenQuad.GetComponent<Collider>());
            screenQuad.transform.SetParent(tvGo.transform, false);
            screenQuad.transform.localPosition = new Vector3(-0.032f, 0.215f, 0.1235f);
            screenQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            screenQuad.transform.localScale = new Vector3(0.245f, 0.195f, 1f);
            var screenRend = screenQuad.GetComponent<MeshRenderer>();
            screenRend.shadowCastingMode = ShadowCastingMode.Off;
            screenRend.sharedMaterial = mats["TVScreenOff"];

            // TV ekranından odaya yayılan kontrollü soğuk fosfor ışıması (başlangıçta kapalı!)
            GameObject tvLightGo = new GameObject("TV_Screen_LocalGlow");
            tvLightGo.transform.SetParent(tvGo.transform, false);
            tvLightGo.transform.localPosition = new Vector3(-0.03f, 0.22f, 0.22f);
            tvLightGo.transform.localRotation = Quaternion.identity;
            Light tvGlow = tvLightGo.AddComponent<Light>();
            tvGlow.type = LightType.Point;
            tvGlow.color = new Color(0.66f, 0.78f, 0.92f);
            tvGlow.range = 4.5f;
            tvGlow.intensity = 0f;
            tvGlow.shadows = LightShadows.None;
            tvGlow.enabled = false;

            AudioSource tvAudio = tvGo.AddComponent<AudioSource>();
            ConfigureSpatialAudioSource(tvAudio, 1.1f, 14f);

            var tvCtrl = tvGo.AddComponent<AbandonedTelevisionController>();
            tvCtrl.Configure(
                screenRend, tvGlow,
                mats["TVScreenOff"], mats["TVScreenOn"],
                tvAudio,
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_TV_CRTStartup.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_TV_StaticLoop.wav"));

            // Görsel Hikaye Anlatımı: Panodan -> Feştek Yuvasına -> Sağdaki Televizyona uzanan çelik kablo boruları!
            CreateConduitSegment("Conduit_Panel_To_Socket", puzzleParent, new Vector3(4.75f, 2.22f, 9.31f), new Vector3(1.85f, 0.038f, 0.038f), mats["RustedMetal"]);
            CreateConduitSegment("Conduit_Socket_Drop", puzzleParent, new Vector3(5.71f, 1.95f, 9.31f), new Vector3(0.038f, 0.58f, 0.038f), mats["RustedMetal"]);
            CreateConduitSegment("Conduit_Socket_To_TV_Horiz", puzzleParent, new Vector3(6.62f, 1.42f, 9.31f), new Vector3(1.90f, 0.035f, 0.035f), mats["RustedMetal"]);
            CreateConduitSegment("Conduit_Socket_To_TV_Drop", puzzleParent, new Vector3(7.55f, 1.32f, 9.12f), new Vector3(0.032f, 0.032f, 0.42f), mats["RustedMetal"]);

            // =========================================================================
            // 6. MERKEZİ GÜÇ YÖNETİCİSİ & BÖLÜM HEDEF YÖNETİCİSİ
            // =========================================================================
            GameObject powerMgrGo = new GameObject("FacilityPowerManager");
            powerMgrGo.transform.SetParent(sysParent, false);
            powerMgrGo.transform.position = socketGo.transform.position;
            AudioSource relayAudio = powerMgrGo.AddComponent<AudioSource>();
            ConfigureSpatialAudioSource(relayAudio, 1.5f, 20f);

            var powerMgr = powerMgrGo.AddComponent<FacilityPowerManager>();
            powerMgr.Configure(
                poweredFixtures,
                tvCtrl,
                relayAudio,
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Power_RelayThunk.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Fluorescent_FlickerStart.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Fluorescent_HumLoop.wav"));

            var socketInteract = socketGo.AddComponent<FestekSocketInteractable>();
            Renderer lensRend = FindDeep(socketGo.transform, "Socket_StatusLens")?.GetComponent<Renderer>();
            socketInteract.Configure(
                FindDeep(socketGo.transform, "Insertion_Approach_Point"),
                FindDeep(socketGo.transform, "Insertion_Transform"),
                FindDeep(socketGo.transform, "Activation_Button"),
                FindDeep(socketGo.transform, "Button_Finger_Target"),
                lensRend,
                powerMgr,
                socketAudio,
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Festek_SocketInsert.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/SFX_Festek_ButtonClick.wav"));

            GameObject objMgrGo = new GameObject("ChapterObjectiveManager");
            objMgrGo.transform.SetParent(sysParent, false);
            var objMgr = objMgrGo.AddComponent<ChapterObjectiveManager>();
            Transform playerT = GameObject.Find("Player_Protagonist")?.transform;
            objMgr.Configure(playerT, mainEntranceDoor, panelGo.transform, socketGo.transform);
        }

        private static void BuildSpatialAudioZones(Transform audioParent)
        {
            var windClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/Amb_BlueHour_Wind.wav");
            if (windClip != null)
            {
                var extWindGo = new GameObject("Ambience_Courtyard_BlueHourWind");
                extWindGo.transform.SetParent(audioParent, false);
                extWindGo.transform.position = new Vector3(0f, 2.0f, -11.0f);
                var src = extWindGo.AddComponent<AudioSource>();
                ConfigureSpatialAudioSource(src, 4.0f, 24f);
                src.clip = windClip;
                src.loop = true;
                src.playOnAwake = true;
                src.volume = 0.36f;
            }

            var roomClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Chapter1/Amb_Interior_RoomTone.wav");
            if (roomClip != null)
            {
                var intToneGo = new GameObject("Ambience_Interior_DeadRoomTone");
                intToneGo.transform.SetParent(audioParent, false);
                intToneGo.transform.position = new Vector3(2.0f, 1.8f, 8.5f);
                var src = intToneGo.AddComponent<AudioSource>();
                ConfigureSpatialAudioSource(src, 3.0f, 22f);
                src.clip = roomClip;
                src.loop = true;
                src.playOnAwake = true;
                src.volume = 0.32f;
            }
        }
        #endregion

        #region Helper Builders
        private static GameObject CreateGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject InstantiateFbx(string assetPath, string instanceName, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, bool isStatic)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                Debug.LogError($"[Chapter1EnvironmentBuilder] FBX yüklenemedi: {assetPath}");
                return null;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = instanceName;
            inst.transform.SetParent(parent, false);
            inst.transform.localPosition = pos;
            inst.transform.localRotation = rot;
            inst.transform.localScale = scale;

            if (isStatic)
            {
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
                }
            }
            return inst;
        }

        private static void AssignPolyHavenMats(GameObject root, Material mainMat, Material glassMat)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    bool isGlassSlot = (shared[i] != null && shared[i].name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0) || (i == 1 && glassMat != null);
                    shared[i] = (isGlassSlot && glassMat != null) ? glassMat : mainMat;
                }
                r.sharedMaterials = shared;
            }
        }

        private static void AssignWhiteboardMats(GameObject root, Material surfaceMat, Material frameMat)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = r.gameObject.name.Contains("Surface") ? surfaceMat : frameMat;
            }
        }

        private static void AddCeilingFluorescent(
            string name, Transform parent, Vector3 pos, float yaw,
            Dictionary<string, Material> mats, List<FacilityLightFixture> list,
            bool brokenDim, float intensity)
        {
            var fixGo = InstantiateFbx("Assets/Environment/External/Models/mounted_fluorescent_lights/mounted_fluorescent_lights.fbx",
                name, parent, pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one, isStatic: true);
            if (fixGo == null) return;

            AssignPolyHavenMats(fixGo, mats["mounted_fluorescent_lights"], mats["FluorescentTubeGlass"]);
            Renderer rend = fixGo.GetComponentInChildren<Renderer>();

            var lightGo = new GameObject(name + "_FillLight");
            lightGo.transform.SetParent(fixGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            var pt = lightGo.AddComponent<Light>();
            pt.type = LightType.Point;
            pt.color = new Color(0.84f, 0.91f, 0.94f);
            pt.range = 8.2f;
            pt.intensity = 0f;
            pt.shadows = LightShadows.None; // Mobil GPU dostu (gölge el fenerinde!)
            pt.enabled = false;

            var humSrc = fixGo.AddComponent<AudioSource>();
            ConfigureSpatialAudioSource(humSrc, 1.2f, 9.5f);

            list.Add(new FacilityLightFixture
            {
                fixtureRenderer = rend,
                emissiveMaterialIndex = 1,
                pointOrSpotLight = pt,
                targetIntensity = intensity,
                emissionColor = new Color(0.80f, 0.90f, 0.94f) * 1.35f,
                brokenStaysDim = brokenDim,
                ballastAudioSource = humSrc
            });
        }

        private static void PlaceRadiator(string name, Transform parent, Vector3 pos, float yaw, Material ironMat)
        {
            var rad = InstantiateFbx("Assets/Environment/Original/Props/Radiator_CastIron.fbx",
                name, parent, pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one, isStatic: true);
            if (rad == null) return;
            foreach (var r in rad.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = ironMat;
        }

        private static void PlaceWallPoster(string name, Transform parent, Vector3 pos, float yaw, Material posterMat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = Quaternion.Euler(0f, yaw + 180f, 0f);
            q.transform.localScale = new Vector3(0.62f, 0.88f, 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = posterMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(q, StaticEditorFlags.BatchingStatic);
        }

        private static void CreateConduitSegment(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static void CreateSubtleWindowFill(string name, Transform parent, Vector3 pos, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.36f, 0.46f, 0.68f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        private static void CreateReflectionProbe(string name, Transform parent, Vector3 pos, Vector3 size, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var rp = go.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            rp.resolution = 128;
            rp.size = size;
            rp.intensity = intensity;
        }

        private static void ConfigureSpatialAudioSource(AudioSource src, float minDist, float maxDist)
        {
            if (src == null) return;
            src.playOnAwake = false;
            src.spatialBlend = 1.0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = minDist;
            src.maxDistance = maxDist;
            src.dopplerLevel = 0f;
        }

        private static void DisableOrMuteLight(string objName, bool enabled, float intensity)
        {
            var go = GameObject.Find(objName);
            if (go == null) return;
            var l = go.GetComponent<Light>();
            if (l != null)
            {
                l.enabled = enabled;
                l.intensity = intensity;
                l.shadows = LightShadows.None;
            }
        }

        private static void HidePrimitiveRenderers(params string[] names)
        {
            foreach (string n in names)
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }
        }

        private static void DisableObjectCompletely(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) go.SetActive(false);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase)) return c;
                var sub = FindDeep(c, name);
                if (sub != null) return sub;
            }
            return null;
        }
        #endregion
    }
}
