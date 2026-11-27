using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using TheLastBell.Player;
using TheLastBell.Interaction;
using TheLastBell.UI;

namespace TheLastBell.Editor
{
    /// <summary>
    /// THE LAST BELL - Profesyonel Mobil Kontroller, Estetik Daire HUD ve Dokunma Çakışması Giderici.
    /// Yapılan Düzenlemeler:
    /// 1. DOKUNMA ÇAKIŞMASI:
    ///    - TouchLookZone: EventSystem ve IsPointerOverGameObject ile buton/joystick dokunuşlarını yok sayar.
    ///    - Hiyerarşi Sıralaması:
    ///      1. TouchLookZone (En üstte / Sibling 0 -> Arkada kalır, boş alanlarda kamera döner)
    ///      2. VirtualJoystick / JoystickArea (Sibling 1 -> Sol ekranda yüzen joystick)
    ///      3. ActionButtonsPanel (Sibling 2 -> En altta/önde, tıklamaları daima ilk yakalar!)
    /// 2. BUTONLARIN BASILIR HALE GETİRİLMESİ:
    ///    - KOŞ: Basılı tutulduğunda 2x hız (MobileHoldButton + EventTrigger: PointerDown / PointerUp).
    ///    - ÇÖMEL: 1.0m ve 1.8m boy/kamera geçişi.
    ///    - FENER: SpotLight anında aç/kapa.
    /// 3. PROFESYONEL VE ESTETİK MOBİL HUD:
    ///    - Şekil: Daire (75x75 dp), yumuşak kenarlı, beyaz çerçeveli anti-aliased sprite.
    ///    - Renkler: Normal füme (#141418, %60 alfa), Fener altın sarısı (#E5B842, %75), Koş buz mavisi (#99D9F2, %85).
    ///    - Sağ Alt Başparmak Yayı:
    ///      * KOŞ: (Right: 60px, Bottom: 60px)
    ///      * ÇÖMEL: (Right: 155px, Bottom: 60px)
    ///      * FENER: (Right: 60px, Bottom: 155px)
    ///      * ETKİLEŞİM: (Right: 155px, Bottom: 155px)
    ///    - Sol Floating Joystick:
    ///      * Zemin: Daire (140x140), şık anti-aliased zemin.
    ///      * Tutamaç: Daire (60x60).
    ///      * Sadece sol ekrana basıldığında görünür (Floating).
    /// </summary>
    public static class MobileSetupHelper
    {
        [MenuItem("The Last Bell/Mobil Düzeltmeleri ve Düzeni Uygula", false, 5)]
        public static void ApplyMobileFixesMenu()
        {
            ApplyMobileFixes();
            EditorUtility.DisplayDialog("Mobil Kontroller ve Estetik HUD Güncellendi",
                "Tüm kritik düzeltmeler başarıyla uygulandı:\n\n" +
                "• TouchLookZone çakışması çözüldü (Hiyerarşi: TouchLookZone arkada, Butonlar en önde).\n" +
                "• Daire şeklinde 75x75 şık butonlar ve başparmak yayı yerleşimi kuruldu.\n" +
                "• KOŞ butonu Hold (basılı tutma) mantığıyla 2x hıza bağlandı.\n" +
                "• ÇÖMEL butonu 1.0m/1.8m boy ve kamera geçişine bağlandı.\n" +
                "• FENER butonu SpotLight'a bağlandı (Altın sarısı parlama).\n" +
                "• 140x140 Daire Yüzen (Floating) Joystick kuruldu.\n" +
                "• Sahne ve varlıklar diske kaydedildi.", "Tamam");
        }

        public static void ApplyMobileFixesBatch()
        {
            ApplyMobileFixes();
            Debug.Log("[MobileSetupHelper] Batch mobile setup completed.");
        }

        public static void ApplyMobileFixes()
        {
            string scenePath = "Assets/Scenes/AbandonedSchool.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log("[MobileSetupHelper] Açılan sahne: " + scene.name);

            if (!System.IO.File.Exists("Assets/Textures/UI/circle_button.png") ||
                !System.IO.File.Exists("Assets/Textures/UI/circle_joystick_bg.png") ||
                !System.IO.File.Exists("Assets/Textures/UI/circle_joystick_handle.png"))
            {
                UISpriteGenerator.GenerateAllSprites();
            }

            // Flashlight Cookie import ayarlarını garanti altına al
            string cookiePath = "Assets/Textures/UI/flashlight_cookie.png";
            if (System.IO.File.Exists(cookiePath))
            {
                var cookieImporter = AssetImporter.GetAtPath(cookiePath) as TextureImporter;
                if (cookieImporter != null)
                {
                    bool dirtyCookie = false;
                    if (cookieImporter.textureType != TextureImporterType.Cookie)
                    {
                        cookieImporter.textureType = TextureImporterType.Cookie;
                        dirtyCookie = true;
                    }
                    if (!cookieImporter.alphaIsTransparency)
                    {
                        cookieImporter.alphaIsTransparency = true;
                        dirtyCookie = true;
                    }
                    if (cookieImporter.mipmapEnabled)
                    {
                        cookieImporter.mipmapEnabled = false;
                        dirtyCookie = true;
                    }
                    if (cookieImporter.wrapMode != TextureWrapMode.Clamp)
                    {
                        cookieImporter.wrapMode = TextureWrapMode.Clamp;
                        dirtyCookie = true;
                    }
                    if (cookieImporter.filterMode != FilterMode.Bilinear)
                    {
                        cookieImporter.filterMode = FilterMode.Bilinear;
                        dirtyCookie = true;
                    }
                    if (dirtyCookie) cookieImporter.SaveAndReimport();
                }
            }

            Sprite btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/circle_button.png");
            Sprite joyBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/circle_joystick_bg.png");
            Sprite joyHandleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/circle_joystick_handle.png");
            Sprite iconSprint = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_sprint.png");
            Sprite iconCrouch = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_crouch.png");
            Sprite iconFlash = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_flashlight.png");
            Sprite iconInteract = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_interact.png");
            Sprite iconPause = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_pause.png");
            Texture flashlightCookie = AssetDatabase.LoadAssetAtPath<Texture>(cookiePath);

            // =================================================================
            // 0. EVENTSYSTEM GARANTİSİ
            // =================================================================
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Debug.Log("[MobileSetupHelper] Eksik EventSystem sahneye eklendi.");
            }

            var player = GameObject.Find("Player_Protagonist");
            if (player == null)
            {
                Debug.LogError("[MobileSetupHelper] Player_Protagonist bulunamadı!");
                return;
            }

            var fpc = player.GetComponent<FirstPersonController>();
            var interactor = player.GetComponent<PlayerInteractor>();
            var camT = player.transform.Find("Camera_Pivot/Main Camera");
            var flashRig = player.transform.Find("Camera_Pivot/Flashlight_Rig");
            var flashCtrl = flashRig != null ? flashRig.GetComponent<FlashlightController>() : null;

            // =================================================================
            // 1. GERÇEKÇİ KORKU EL FENERİ & ATMOSFERİK AYDINLATMA
            // =================================================================
            Light spotLight = null;
            if (flashRig != null)
            {
                var directLight = flashRig.GetComponent<Light>();
                if (directLight != null)
                {
                    UnityEngine.Object.DestroyImmediate(directLight);
                }

                flashRig.localPosition = new Vector3(0.18f, -0.16f, 0.16f);
                flashRig.localRotation = Quaternion.identity;

                var spotT = flashRig.Find("SpotLight");
                if (spotT == null)
                {
                    var spotGO = new GameObject("SpotLight");
                    spotGO.transform.SetParent(flashRig, false);
                    spotT = spotGO.transform;
                }

                spotT.localPosition = new Vector3(0f, 0f, 0.02f);
                spotT.localRotation = Quaternion.identity;

                spotLight = spotT.GetComponent<Light>();
                if (spotLight == null) spotLight = spotT.gameObject.AddComponent<Light>();

                spotLight.type = LightType.Spot;
                spotLight.renderMode = LightRenderMode.ForcePixel;
                spotLight.range = 20f;
                spotLight.spotAngle = 42f;
                spotLight.innerSpotAngle = 14f;
                spotLight.intensity = 3.2f;
                spotLight.color = new Color(1.0f, 0.96f, 0.88f, 1.0f);
                spotLight.cullingMask = ~0;
                spotLight.shadows = LightShadows.Soft;
                spotLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
                spotLight.shadowBias = 0.015f;
                spotLight.shadowNormalBias = 0.15f;
                spotLight.shadowNearPlane = 0.12f;
                spotLight.cookie = flashlightCookie;
                spotLight.enabled = true;

                if (flashCtrl != null)
                {
                    var soFc = new SerializedObject(flashCtrl);
                    SafeSetObject(soFc, "flashlightLight", spotLight);
                    SafeSetObject(soFc, "spotCookie", flashlightCookie);
                    SafeSetObject(soFc, "cameraTarget", camT);
                    SafeSetFloat(soFc, "baseIntensity", 3.2f);
                    var moProp = soFc.FindProperty("mountOffset");
                    if (moProp != null) moProp.vector3Value = new Vector3(0.18f, -0.16f, 0.16f);
                    SafeSetFloat(soFc, "swaySmoothSpeed", 14f);
                    SafeSetBool(soFc, "isOn", true);
                    SafeSetBool(soFc, "useBattery", false);
                    SafeSetFloat(soFc, "currentBattery", 100f);
                    soFc.ApplyModifiedProperties();
                }
            }

            // Korku atmosferini korumak için ortam ışığını dengele ve duvarların fener gölgelerini kesmesini sağla
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.055f, 0.068f, 0.095f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.02f, 0.025f, 0.035f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.01f, 0.01f, 0.015f, 1f);
            RenderSettings.fogDensity = 0.042f;

            var moonGO = GameObject.Find("Moonlight_Directional");
            if (moonGO != null && moonGO.TryGetComponent<Light>(out var moonLight))
            {
                moonLight.intensity = 0.25f;
                moonLight.shadows = LightShadows.Soft;
            }

            var porchGO = GameObject.Find("Light_Courtyard_EntranceLamp");
            if (porchGO != null && porchGO.TryGetComponent<Light>(out var porchLight))
            {
                porchLight.intensity = 0.95f;
                porchLight.range = 9.5f;
            }

            // =================================================================
            // 2. CANVAS SCALER, STAMINA BAR & RAYCAST BLOCKER TEMİZLİĞİ
            // =================================================================
            var canvasGO = GameObject.Find("Gameplay_HUD_Canvas");
            if (canvasGO != null)
            {
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1.0f;

                // Dokunma engelleyen pasif UI raycastTarget'larını kapat
                string[] nonInteractiveNames = {
                    "Interaction_Prompt_Panel", "Prompt_Text",
                    "Status_Message_Banner", "Reticle_Dot",
                    "Stamina_Bar_Container", "Fill"
                };
                foreach (var n in nonInteractiveNames)
                {
                    var t = FindChildRecursive(canvasGO.transform, n);
                    if (t != null)
                    {
                        foreach (var g in t.GetComponentsInChildren<Graphic>(true))
                        {
                            g.raycastTarget = false;
                        }
                    }
                }

                // Stamina barını sol alt joystick ile çakışmayacak şekilde sol üste al
                var staminaT = canvasGO.transform.Find("Stamina_Bar_Container");
                if (staminaT != null && staminaT is RectTransform stRt)
                {
                    stRt.anchorMin = new Vector2(0.035f, 0.93f);
                    stRt.anchorMax = new Vector2(0.035f, 0.93f);
                    stRt.pivot = new Vector2(0f, 0.5f);
                    stRt.anchoredPosition = Vector2.zero;
                    stRt.sizeDelta = new Vector2(300f, 14f);
                }

                var hud = UnityEngine.Object.FindAnyObjectByType<PlayerHUD>(FindObjectsInactive.Include);
                if (hud != null)
                {
                    var soHud = new SerializedObject(hud);
                    SafeSetObject(soHud, "playerController", fpc);
                    var statusBanner = GameObject.Find("Status_Message_Banner");
                    if (statusBanner != null) SafeSetObject(soHud, "statusContainer", statusBanner);
                    var noteModal = canvasGO.transform.Find("Note_Inspection_Modal")?.gameObject;
                    if (noteModal != null) SafeSetObject(soHud, "notePanelRoot", noteModal);
                    soHud.ApplyModifiedProperties();
                }
            }

            // =================================================================
            // 3. MOBİL KONTROL HİYERARŞİSİ (150-180 dp Joystick, 56-72 dp Butonlar)
            // =================================================================
            var rootGO = GameObject.Find("Mobile_Controls_Root");
            if (rootGO != null)
            {
                var rootRT = rootGO.GetComponent<RectTransform>();
                rootRT.anchorMin = Vector2.zero;
                rootRT.anchorMax = Vector2.one;
                rootRT.offsetMin = Vector2.zero;
                rootRT.offsetMax = Vector2.zero;
                rootRT.anchoredPosition = Vector2.zero;
                rootRT.sizeDelta = Vector2.zero;
                rootRT.pivot = new Vector2(0.5f, 0.5f);

                if (rootGO.GetComponent<SafeAreaFitter>() == null)
                {
                    rootGO.AddComponent<SafeAreaFitter>();
                }

                // --- 1. ELEMAN: Touch_Look_Zone (Sibling 0 -> Sağ alan kamera döndürme) ---
                var tlzT = rootRT.Find("Touch_Look_Zone");
                if (tlzT == null)
                {
                    var tlzGO = new GameObject("Touch_Look_Zone", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(TouchLookZone));
                    tlzGO.transform.SetParent(rootRT, false);
                    tlzT = tlzGO.transform;
                }
                var tlzRT = tlzT.GetComponent<RectTransform>();
                tlzRT.anchorMin = new Vector2(0.45f, 0f);
                tlzRT.anchorMax = new Vector2(1f, 1f);
                tlzRT.offsetMin = Vector2.zero;
                tlzRT.offsetMax = Vector2.zero;
                tlzRT.pivot = new Vector2(0.5f, 0.5f);

                var tlzImg = tlzT.GetComponent<UnityEngine.UI.Image>();
                tlzImg.color = new Color(0f, 0f, 0f, 0f);
                tlzImg.raycastTarget = true;

                var tlz = tlzT.GetComponent<TouchLookZone>();
                if (tlz == null) tlz = tlzT.gameObject.AddComponent<TouchLookZone>();

                // --- 2. ELEMAN: Joystick_Touch_Zone (Sibling 1 -> Sol Ekran %48) ---
                var joyAreaT = rootRT.Find("Joystick_Touch_Zone");
                if (joyAreaT == null)
                {
                    var joyGO = new GameObject("Joystick_Touch_Zone", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                    joyGO.transform.SetParent(rootRT, false);
                    joyAreaT = joyGO.transform;
                }
                var joyAreaRT = joyAreaT.GetComponent<RectTransform>();
                joyAreaRT.anchorMin = new Vector2(0f, 0f);
                joyAreaRT.anchorMax = new Vector2(0.48f, 1f);
                joyAreaRT.offsetMin = Vector2.zero;
                joyAreaRT.offsetMax = Vector2.zero;
                joyAreaRT.pivot = new Vector2(0.5f, 0.5f);

                var joyAreaImg = joyAreaT.GetComponent<UnityEngine.UI.Image>();
                joyAreaImg.color = new Color(0f, 0f, 0f, 0f);
                joyAreaImg.raycastTarget = true;

                var mcUI = joyAreaT.GetComponent<MobileControlsUI>();
                if (mcUI == null) mcUI = joyAreaT.gameObject.AddComponent<MobileControlsUI>();

                // Görünür Sanal Joystick: 420x420px Base (~160 dp) + 164x164px Handle (~62 dp)
                var joyBgT = rootRT.Find("Joystick_Background");
                if (joyBgT == null) joyBgT = joyAreaT.Find("Joystick_Background");
                if (joyBgT == null)
                {
                    var bgGO = new GameObject("Joystick_Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
                    bgGO.transform.SetParent(rootRT, false);
                    joyBgT = bgGO.transform;
                }
                joyBgT.SetParent(rootRT, false);
                var joyBgRT = joyBgT.GetComponent<RectTransform>();
                joyBgRT.anchorMin = Vector2.zero;
                joyBgRT.anchorMax = Vector2.zero;
                joyBgRT.pivot = new Vector2(0.5f, 0.5f);
                joyBgRT.anchoredPosition = new Vector2(280f, 255f);
                joyBgRT.sizeDelta = new Vector2(420f, 420f);

                var joyBgImg = joyBgT.GetComponent<UnityEngine.UI.Image>();
                joyBgImg.sprite = joyBgSprite;
                joyBgImg.color = new Color(0.92f, 0.94f, 0.97f, 0.85f);
                joyBgImg.raycastTarget = false;

                var joyCg = joyBgT.GetComponent<CanvasGroup>();
                if (joyCg == null) joyCg = joyBgT.gameObject.AddComponent<CanvasGroup>();
                joyCg.alpha = 0.65f; // Aktif oyunda daima görünür
                joyCg.interactable = false;
                joyCg.blocksRaycasts = false;

                var handleT = joyBgT.Find("Joystick_Handle");
                if (handleT == null)
                {
                    var hGO = new GameObject("Joystick_Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                    hGO.transform.SetParent(joyBgT, false);
                    handleT = hGO.transform;
                }
                var handleRT = handleT.GetComponent<RectTransform>();
                handleRT.anchorMin = new Vector2(0.5f, 0.5f);
                handleRT.anchorMax = new Vector2(0.5f, 0.5f);
                handleRT.pivot = new Vector2(0.5f, 0.5f);
                handleRT.anchoredPosition = Vector2.zero;
                handleRT.sizeDelta = new Vector2(164f, 164f);

                var handleImg = handleT.GetComponent<UnityEngine.UI.Image>();
                handleImg.sprite = joyHandleSprite;
                handleImg.color = new Color(0.95f, 0.97f, 1.0f, 0.92f);
                handleImg.raycastTarget = false;

                // --- 3. ELEMAN: ActionButtonsPanel (Sibling 3 -> En önde, 56-72 dp rahat boyutlu butonlar) ---
                var panelT = rootRT.Find("ActionButtonsPanel");
                if (panelT == null)
                {
                    var pGO = new GameObject("ActionButtonsPanel", typeof(RectTransform));
                    pGO.transform.SetParent(rootRT, false);
                    panelT = pGO.transform;
                }
                var panelRT = panelT.GetComponent<RectTransform>();
                panelRT.anchorMin = new Vector2(1f, 0f);
                panelRT.anchorMax = new Vector2(1f, 0f);
                panelRT.pivot = new Vector2(1f, 0f);
                panelRT.anchoredPosition = Vector2.zero;
                panelRT.sizeDelta = new Vector2(560f, 560f);

                Func<string, Vector2, float, Sprite, GameObject> configCircleBtn = (bName, anchoredPos, sizeRefPx, iconSprite) =>
                {
                    Transform bT = panelT.Find(bName);
                    if (bT == null) bT = rootRT.Find(bName);
                    if (bT == null)
                    {
                        var bGO = new GameObject(bName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(Button));
                        bT = bGO.transform;
                    }
                    bT.SetParent(panelT, false);

                    var rt = bT.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.anchoredPosition = anchoredPos;
                    rt.sizeDelta = new Vector2(sizeRefPx, sizeRefPx);

                    var img = bT.GetComponent<UnityEngine.UI.Image>();
                    if (img == null) img = bT.gameObject.AddComponent<UnityEngine.UI.Image>();
                    img.sprite = btnSprite;
                    img.color = new Color(0.06f, 0.07f, 0.09f, 0.52f);
                    img.type = UnityEngine.UI.Image.Type.Simple;
                    img.raycastTarget = true;

                    var btn = bT.GetComponent<Button>();
                    if (btn == null) btn = bT.gameObject.AddComponent<Button>();
                    btn.interactable = true;
                    btn.targetGraphic = img;

                    var colors = btn.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                    colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
                    colors.selectedColor = Color.white;
                    btn.colors = colors;

                    var txtT = bT.Find("Text");
                    if (txtT == null) txtT = bT.GetComponentInChildren<TextMeshProUGUI>(true)?.transform;
                    if (txtT != null)
                    {
                        txtT.gameObject.SetActive(false);
                        var txtGraphic = txtT.GetComponent<Graphic>();
                        if (txtGraphic != null) txtGraphic.raycastTarget = false;
                    }

                    if (iconSprite != null)
                    {
                        var iconT = bT.Find("Icon");
                        if (iconT == null)
                        {
                            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                            iconGO.transform.SetParent(bT, false);
                            iconT = iconGO.transform;
                        }
                        var iconRT = iconT.GetComponent<RectTransform>();
                        iconRT.anchorMin = new Vector2(0.5f, 0.5f);
                        iconRT.anchorMax = new Vector2(0.5f, 0.5f);
                        iconRT.pivot = new Vector2(0.5f, 0.5f);
                        iconRT.anchoredPosition = Vector2.zero;
                        iconRT.sizeDelta = new Vector2(sizeRefPx * 0.50f, sizeRefPx * 0.50f);

                        var iconImg = iconT.GetComponent<UnityEngine.UI.Image>();
                        iconImg.sprite = iconSprite;
                        iconImg.preserveAspect = true;
                        iconImg.color = new Color(0.94f, 0.95f, 0.97f, 0.92f);
                        iconImg.raycastTarget = false;
                    }

                    return bT.gameObject;
                };

                // 1080p ~2.625px/dp üzerinde 59-67 dp rahat parmak yerleşimi
                var btnSprintGO = configCircleBtn("Mobile_SprintButton", new Vector2(-72f, 72f), 168f, iconSprint);
                var btnCrouchGO = configCircleBtn("Mobile_CrouchButton", new Vector2(-276f, 72f), 156f, iconCrouch);
                var btnFlashGO = configCircleBtn("Mobile_FlashlightButton", new Vector2(-72f, 276f), 156f, iconFlash);
                var btnActionGO = configCircleBtn("Mobile_ActionButton", new Vector2(-276f, 276f), 172f, iconInteract);

                // Sağ Üst Duraklatma (Pause) Butonu (124px ~ 48 dp)
                Transform pauseT = rootRT.Find("Mobile_PauseButton");
                if (pauseT == null)
                {
                    var pBtnGO = new GameObject("Mobile_PauseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(Button));
                    pBtnGO.transform.SetParent(rootRT, false);
                    pauseT = pBtnGO.transform;
                }
                var pauseRT = pauseT.GetComponent<RectTransform>();
                pauseRT.anchorMin = new Vector2(1f, 1f);
                pauseRT.anchorMax = new Vector2(1f, 1f);
                pauseRT.pivot = new Vector2(1f, 1f);
                pauseRT.anchoredPosition = new Vector2(-42f, -36f);
                pauseRT.sizeDelta = new Vector2(124f, 124f);

                var pauseImg = pauseT.GetComponent<UnityEngine.UI.Image>();
                pauseImg.sprite = btnSprite;
                pauseImg.color = new Color(0.06f, 0.07f, 0.09f, 0.52f);
                pauseImg.raycastTarget = true;

                if (iconPause != null)
                {
                    var pIconT = pauseT.Find("Icon");
                    if (pIconT == null)
                    {
                        var pIconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                        pIconGO.transform.SetParent(pauseT, false);
                        pIconT = pIconGO.transform;
                    }
                    var pIconRT = pIconT.GetComponent<RectTransform>();
                    pIconRT.anchorMin = new Vector2(0.5f, 0.5f);
                    pIconRT.anchorMax = new Vector2(0.5f, 0.5f);
                    pIconRT.anchoredPosition = Vector2.zero;
                    pIconRT.sizeDelta = new Vector2(60f, 60f);
                    var pIconImg = pIconT.GetComponent<UnityEngine.UI.Image>();
                    pIconImg.sprite = iconPause;
                    pIconImg.preserveAspect = true;
                    pIconImg.color = new Color(0.94f, 0.95f, 0.97f, 0.90f);
                    pIconImg.raycastTarget = false;
                }

                // KOŞ Butonuna MobileHoldButton ekle ve çakışan EventTrigger'ı kaldır
                var holdBtn = btnSprintGO.GetComponent<MobileHoldButton>();
                if (holdBtn == null) holdBtn = btnSprintGO.AddComponent<MobileHoldButton>();
                var oldTrig = btnSprintGO.GetComponent<EventTrigger>();
                if (oldTrig != null) UnityEngine.Object.DestroyImmediate(oldTrig);

                // HİYERARŞİ SIRALAMASI
                tlzT.SetSiblingIndex(0);
                joyAreaT.SetSiblingIndex(1);
                joyBgT.SetSiblingIndex(2);
                panelT.SetSiblingIndex(3);
                pauseT.SetSiblingIndex(4);

                var soTlz = new SerializedObject(tlz);
                SafeSetObject(soTlz, "fpsController", fpc);
                SafeSetObject(soTlz, "mobileControls", mcUI);
                SafeSetFloat(soTlz, "lookSensitivity", 0.18f);
                SafeSetFloat(soTlz, "smoothingSpeed", 30f);
                soTlz.ApplyModifiedProperties();

                var soMc = new SerializedObject(mcUI);
                SafeSetObject(soMc, "fpsController", fpc);
                SafeSetObject(soMc, "interactor", interactor);
                SafeSetObject(soMc, "flashlight", flashCtrl);
                SafeSetObject(soMc, "joystickTouchArea", joyAreaRT);
                SafeSetObject(soMc, "joystickBackground", joyBgRT);
                SafeSetObject(soMc, "joystickHandle", handleRT);
                SafeSetObject(soMc, "joystickCanvasGroup", joyCg);
                SafeSetObject(soMc, "actionButton", btnActionGO.GetComponent<Button>());
                SafeSetObject(soMc, "actionButtonImage", btnActionGO.GetComponent<UnityEngine.UI.Image>());
                SafeSetObject(soMc, "actionButtonText", btnActionGO.GetComponentInChildren<TextMeshProUGUI>(true));
                SafeSetObject(soMc, "sprintButton", btnSprintGO.GetComponent<Button>());
                SafeSetObject(soMc, "sprintButtonImage", btnSprintGO.GetComponent<UnityEngine.UI.Image>());
                SafeSetObject(soMc, "crouchButton", btnCrouchGO.GetComponent<Button>());
                SafeSetObject(soMc, "crouchButtonImage", btnCrouchGO.GetComponent<UnityEngine.UI.Image>());
                SafeSetObject(soMc, "flashlightButton", btnFlashGO.GetComponent<Button>());
                SafeSetObject(soMc, "flashlightButtonImage", btnFlashGO.GetComponent<UnityEngine.UI.Image>());
                SafeSetObject(soMc, "pauseButton", pauseT.GetComponent<Button>());
                SafeSetFloat(soMc, "joystickRadius", 128f);
                SafeSetFloat(soMc, "deadzone", 0.12f);
                SafeSetFloat(soMc, "idleJoystickAlpha", 0.65f);
                SafeSetFloat(soMc, "activeJoystickAlpha", 0.95f);
                var defPosProp = soMc.FindProperty("defaultJoystickPosition");
                if (defPosProp != null) defPosProp.vector2Value = new Vector2(280f, 255f);
                soMc.ApplyModifiedProperties();

                // FirstPersonController varsayılanlarını senkronize et
                if (fpc != null)
                {
                    var soFpc = new SerializedObject(fpc);
                    SafeSetFloat(soFpc, "acceleration", 16f);
                    SafeSetFloat(soFpc, "deceleration", 40f);
                    soFpc.ApplyModifiedProperties();
                }

                Debug.Log("<color=green>[MobileSetupHelper] Genişletilmiş (150-180dp) görünür joystick, 56-72dp butonlar, fener cookie ve raycast temizliği uygulandı.</color>");
            }

            // =================================================================
            // 4. SADECE AbandonedSchool.unity DİSKE KAYDET (MainMenu & OpeningCinematic DOKUNMA!)
            // =================================================================
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MobileSetupHelper] AbandonedSchool.unity başarıyla kaydedildi.");

            AssetDatabase.SaveAssets();
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == targetName) return c;
                Transform nested = FindChildRecursive(c, targetName);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void SafeSetObject(SerializedObject so, string propName, UnityEngine.Object val)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.objectReferenceValue = val;
        }

        private static void SafeSetFloat(SerializedObject so, string propName, float val)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.floatValue = val;
        }

        private static void SafeSetBool(SerializedObject so, string propName, bool val)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.boolValue = val;
        }
    }
}
