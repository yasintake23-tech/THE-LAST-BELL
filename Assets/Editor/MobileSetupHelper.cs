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

            // Sadece sprite'lar eksikse üret (mevcut yüksek kaliteli sprite'ları ezme)
            if (!System.IO.File.Exists("Assets/Textures/UI/circle_button.png") ||
                !System.IO.File.Exists("Assets/Textures/UI/circle_joystick_bg.png") ||
                !System.IO.File.Exists("Assets/Textures/UI/circle_joystick_handle.png"))
            {
                UISpriteGenerator.GenerateAllSprites();
            }

            Sprite btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/circle_button.png");
            Sprite joyBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/circle_joystick_bg.png");
            Sprite joyHandleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/circle_joystick_handle.png");
            Sprite iconSprint = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_sprint.png");
            Sprite iconCrouch = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_crouch.png");
            Sprite iconFlash = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_flashlight.png");
            Sprite iconInteract = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_interact.png");
            Sprite iconPause = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/UI/icon_pause.png");

            // =================================================================
            // 0. EVENTSYSTEM GARANTİSİ (UI & DOKUNMATİK KONTROLLER İÇİN ZORUNLU)
            // =================================================================
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
            {
                var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
            var flashRig = player.transform.Find("Camera_Pivot/Flashlight_Rig");
            var flashCtrl = flashRig != null ? flashRig.GetComponent<FlashlightController>() : null;

            // =================================================================
            // 1. FENER SPOTLIGHT IŞIĞINI ÇALIŞIR HALE GETİR
            // =================================================================
            Light spotLight = null;
            if (flashRig != null)
            {
                var directLight = flashRig.GetComponent<Light>();
                if (directLight != null)
                {
                    UnityEngine.Object.DestroyImmediate(directLight);
                }

                var spotT = flashRig.Find("SpotLight");
                if (spotT == null)
                {
                    var spotGO = new GameObject("SpotLight");
                    spotGO.transform.SetParent(flashRig, false);
                    spotT = spotGO.transform;
                }

                spotT.localPosition = new Vector3(0f, 0f, 0.25f);
                spotT.localRotation = Quaternion.identity;

                spotLight = spotT.GetComponent<Light>();
                if (spotLight == null) spotLight = spotT.gameObject.AddComponent<Light>();

                spotLight.type = LightType.Spot;
                spotLight.range = 25f;
                spotLight.spotAngle = 55f;
                spotLight.innerSpotAngle = 30f;
                spotLight.intensity = 3.5f;
                spotLight.color = new Color(1.0f, 0.9725f, 0.9059f, 1.0f); // #FFF8E7
                spotLight.cullingMask = ~0;
                spotLight.shadows = LightShadows.Soft;
                spotLight.enabled = true;

                if (flashCtrl != null)
                {
                    var soFc = new SerializedObject(flashCtrl);
                    SafeSetObject(soFc, "flashlightLight", spotLight);
                    SafeSetFloat(soFc, "baseIntensity", 3.5f);
                    SafeSetBool(soFc, "isOn", true);
                    SafeSetBool(soFc, "useBattery", false);
                    SafeSetFloat(soFc, "currentBattery", 100f);
                    soFc.ApplyModifiedProperties();
                }
            }

            // =================================================================
            // 2. CANVAS SCALER & PLAYER HUD BAĞLANTILARI
            // =================================================================
            var canvasGO = GameObject.Find("Gameplay_HUD_Canvas");
            if (canvasGO != null)
            {
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = canvasGO.AddComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

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
            // 3. MOBİL KONTROL HİYERARŞİSİ VE MİNİMALİST İKONLU KORKU HUD'U
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

                // --- 1. ELEMAN: Touch_Look_Zone (EN ÜSTTE / Sibling 0 -> Arkada kalır) ---
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

                // --- 2. ELEMAN: JoystickArea / VirtualJoystick (Sibling 1 -> Sol Ekran) ---
                var joyAreaT = rootRT.Find("Joystick_Touch_Zone");
                if (joyAreaT == null)
                {
                    var joyGO = new GameObject("Joystick_Touch_Zone", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                    joyGO.transform.SetParent(rootRT, false);
                    joyAreaT = joyGO.transform;
                }
                var joyAreaRT = joyAreaT.GetComponent<RectTransform>();
                joyAreaRT.anchorMin = new Vector2(0f, 0f);
                joyAreaRT.anchorMax = new Vector2(0.45f, 1f);
                joyAreaRT.offsetMin = Vector2.zero;
                joyAreaRT.offsetMax = Vector2.zero;
                joyAreaRT.pivot = new Vector2(0.5f, 0.5f);

                var joyAreaImg = joyAreaT.GetComponent<UnityEngine.UI.Image>();
                joyAreaImg.color = new Color(0f, 0f, 0f, 0f);
                joyAreaImg.raycastTarget = true;

                var mcUI = joyAreaT.GetComponent<MobileControlsUI>();
                if (mcUI == null) mcUI = joyAreaT.gameObject.AddComponent<MobileControlsUI>();

                // Joystick Görselleri (136x136 Zemin, 54x54 Handle)
                var joyBgT = rootRT.Find("Joystick_Background");
                if (joyBgT == null) joyBgT = joyAreaT.Find("Joystick_Background");
                if (joyBgT == null)
                {
                    var bgGO = new GameObject("Joystick_Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(CanvasGroup));
                    bgGO.transform.SetParent(rootRT, false);
                    joyBgT = bgGO.transform;
                }
                joyBgT.SetParent(rootRT, true);
                var joyBgRT = joyBgT.GetComponent<RectTransform>();
                joyBgRT.anchorMin = Vector2.zero;
                joyBgRT.anchorMax = Vector2.zero;
                joyBgRT.pivot = new Vector2(0.5f, 0.5f);
                joyBgRT.anchoredPosition = new Vector2(140f, 140f);
                joyBgRT.sizeDelta = new Vector2(136f, 136f);

                var joyBgImg = joyBgT.GetComponent<UnityEngine.UI.Image>();
                joyBgImg.sprite = joyBgSprite;
                joyBgImg.color = new Color(0.90f, 0.92f, 0.95f, 0.50f);
                joyBgImg.raycastTarget = false;

                var joyCg = joyBgT.GetComponent<CanvasGroup>();
                if (joyCg == null) joyCg = joyBgT.gameObject.AddComponent<CanvasGroup>();
                joyCg.alpha = 0f;
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
                handleRT.sizeDelta = new Vector2(54f, 54f);

                var handleImg = handleT.GetComponent<UnityEngine.UI.Image>();
                handleImg.sprite = joyHandleSprite;
                handleImg.color = new Color(0.95f, 0.96f, 0.98f, 0.85f);
                handleImg.raycastTarget = false;

                // --- 3. ELEMAN: ActionButtonsPanel (EN ALTA / Sibling 2 -> En önde tıklanır!) ---
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
                panelRT.sizeDelta = new Vector2(340f, 340f);

                // Minimalist İkonlu Daire Buton Yapılandırma
                Func<string, Vector2, float, Sprite, GameObject> configCircleBtn = (bName, anchoredPos, sizeDp, iconSprite) =>
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
                    rt.sizeDelta = new Vector2(sizeDp, sizeDp);

                    var img = bT.GetComponent<UnityEngine.UI.Image>();
                    if (img == null) img = bT.gameObject.AddComponent<UnityEngine.UI.Image>();
                    img.sprite = btnSprite;
                    img.color = new Color(0.06f, 0.07f, 0.09f, 0.42f);
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

                    // Eski büyük metin etiketini gizle (ikonlu minimalist tasarım)
                    var txtT = bT.Find("Text");
                    if (txtT == null) txtT = bT.GetComponentInChildren<TextMeshProUGUI>(true)?.transform;
                    if (txtT != null)
                    {
                        txtT.gameObject.SetActive(false);
                    }

                    // Merkez İkon Sprite
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
                        iconRT.sizeDelta = new Vector2(sizeDp * 0.52f, sizeDp * 0.52f);

                        var iconImg = iconT.GetComponent<UnityEngine.UI.Image>();
                        iconImg.sprite = iconSprite;
                        iconImg.preserveAspect = true;
                        iconImg.color = new Color(0.92f, 0.93f, 0.95f, 0.90f);
                        iconImg.raycastTarget = false;
                    }

                    return bT.gameObject;
                };

                var btnSprintGO = configCircleBtn("Mobile_SprintButton", new Vector2(-44f, 44f), 72f, iconSprint);
                var btnCrouchGO = configCircleBtn("Mobile_CrouchButton", new Vector2(-134f, 44f), 68f, iconCrouch);
                var btnFlashGO = configCircleBtn("Mobile_FlashlightButton", new Vector2(-44f, 134f), 68f, iconFlash);
                var btnActionGO = configCircleBtn("Mobile_ActionButton", new Vector2(-134f, 134f), 74f, iconInteract);

                // Sağ Üst Duraklatma (Pause) Butonu
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
                pauseRT.anchoredPosition = new Vector2(-28f, -28f);
                pauseRT.sizeDelta = new Vector2(52f, 52f);

                var pauseImg = pauseT.GetComponent<UnityEngine.UI.Image>();
                pauseImg.sprite = btnSprite;
                pauseImg.color = new Color(0.06f, 0.07f, 0.09f, 0.42f);
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
                    pIconRT.sizeDelta = new Vector2(26f, 26f);
                    var pIconImg = pIconT.GetComponent<UnityEngine.UI.Image>();
                    pIconImg.sprite = iconPause;
                    pIconImg.preserveAspect = true;
                    pIconImg.color = new Color(0.92f, 0.93f, 0.95f, 0.88f);
                    pIconImg.raycastTarget = false;
                }

                // KOŞ Butonuna MobileHoldButton ekle ve eski çakışan EventTrigger'ı kaldır
                var holdBtn = btnSprintGO.GetComponent<MobileHoldButton>();
                if (holdBtn == null) holdBtn = btnSprintGO.AddComponent<MobileHoldButton>();
                var oldTrig = btnSprintGO.GetComponent<EventTrigger>();
                if (oldTrig != null) UnityEngine.Object.DestroyImmediate(oldTrig);

                // HİYERARŞİ SIRALAMASINI KESİNLEŞTİR:
                tlzT.SetSiblingIndex(0);
                joyAreaT.SetSiblingIndex(1);
                panelT.SetSiblingIndex(2);
                pauseT.SetSiblingIndex(3);

                // MobileControlsUI ve TouchLookZone referanslarını bağla
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
                SafeSetFloat(soMc, "joystickRadius", 50f);
                soMc.ApplyModifiedProperties();

                Debug.Log("<color=green>[MobileSetupHelper] Minimalist ikonlu mobil korku HUD'u, EventSystem ve SafeAreaFitter kuruldu.</color>");
            }

            // =================================================================
            // 4. SAHNE VE ASSETLERİ DİSKE KAYDET
            // =================================================================
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MobileSetupHelper] AbandonedSchool.unity başarıyla kaydedildi.");

            AndroidBuildHelper.ForceSaveAndSyncAll();

            var mm = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            EditorSceneManager.SaveScene(mm);
            Debug.Log("[MobileSetupHelper] MainMenu.unity başarıyla kaydedildi.");
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
