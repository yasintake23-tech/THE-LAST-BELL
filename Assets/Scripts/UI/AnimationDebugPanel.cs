using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastBell.Player;
using TheLastBell.Interaction;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Mobil Uyumlu, Katlanabilir Animasyon Hata Ayıklama ve Önizleme Paneli.
    /// Özellikler:
    /// - Varsayılan olarak kapalı başlar; ekranın üst köşesinde dokunmatik dostu "ANIM" ve "1P/3P" butonları sunar.
    /// - Canlı Oynanış (Live Gameplay) ve İzole Önizleme (Isolated Preview / PlayableGraph) modları.
    /// - Kayıtlı tüm 23+ klibi listeler, oynatır, duraklatır, hız/zaman çizelgesi ve döngü kontrolü sağlar.
    /// - 7 katman ağırlık geçersiz kılma (override) ve canlı kemik/zemin telemetrisi (HipsY, LFootY, RFootY).
    /// - Üretim (Release) derlemelerinde tek bir bayrakla (enableDebugTools) kolayca kapatılabilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimationDebugPanel : MonoBehaviour
    {
        public static AnimationDebugPanel Instance { get; private set; }

        [Header("--- Yapılandırma ---")]
        [SerializeField] private bool enableDebugTools = true;
        [SerializeField] private AnimationLibraryRegistry libraryRegistry;

        [Header("--- Sahne Referansları ---")]
        [SerializeField] private ProtagonistAnimationController animController;
        [SerializeField] private CameraModeController cameraModeController;
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private FlashlightPhysicalItem flashlightPhysical;

        private bool isBuilt = false;
        private bool isPanelOpen = false;
        private bool isIsolatedMode = false;
        private int selectedClipIndex = 0;
        private bool isScrubbing = false;
        private Vector3 initialPlayerPosition;
        private Quaternion initialPlayerRotation;
        private bool hasSavedInitialPose = false;

        // UI Elemanları
        private GameObject hudToggleRoot;
        private GameObject panelRoot;
        private TextMeshProUGUI cameraToggleLabel;
        private TextMeshProUGUI modeBadgeText;
        private TextMeshProUGUI clipTitleText;
        private TextMeshProUGUI timelineLabel;
        private TextMeshProUGUI speedLabel;
        private TextMeshProUGUI loopBtnLabel;
        private TextMeshProUGUI playPauseBtnLabel;
        private TextMeshProUGUI telemetryText;
        private Slider timelineSlider;
        private Slider speedSlider;
        private Slider camDistSlider;
        private Slider camHeightSlider;
        private readonly List<Button> clipListButtons = new List<Button>();
        private readonly List<Image> clipListBackgrounds = new List<Image>();
        private readonly Slider[] layerSliders = new Slider[7];

        public bool IsPanelOpen => isPanelOpen;
        public bool IsIsolatedMode => isIsolatedMode;
        public AnimationLibraryRegistry LibraryRegistry => libraryRegistry;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (!enableDebugTools) return;
            EnsureBuilt();
        }

        private void OnDestroy()
        {
            if (isIsolatedMode) ExitIsolatedPreview();
            if (Instance == this) Instance = null;
        }

        public void SetRegistry(AnimationLibraryRegistry registry)
        {
            libraryRegistry = registry;
        }

        public void EnsureBuilt()
        {
            if (isBuilt || !enableDebugTools) return;
            ResolveReferences();
            BuildRuntimeUI();
            isBuilt = true;
            SetPanelOpen(false);
            RefreshCameraToggleLabel();
            RefreshClipSelectionUI();
        }

        private void ResolveReferences()
        {
            if (libraryRegistry == null)
            {
                libraryRegistry = Resources.Load<AnimationLibraryRegistry>("AnimationLibraryRegistry");
            }
            if (playerController == null) playerController = FindFirstObjectByType<FirstPersonController>();
            if (cameraModeController == null) cameraModeController = FindFirstObjectByType<CameraModeController>();
            if (animController == null) animController = FindFirstObjectByType<ProtagonistAnimationController>();
            if (flashlightPhysical == null) flashlightPhysical = FindFirstObjectByType<FlashlightPhysicalItem>();

            if (playerController != null && !hasSavedInitialPose)
            {
                initialPlayerPosition = playerController.transform.position;
                initialPlayerRotation = playerController.transform.rotation;
                hasSavedInitialPose = true;
            }
        }

        private void Update()
        {
            if (!enableDebugTools || !isBuilt) return;

            if (Input.GetKeyDown(KeyCode.F8))
            {
                SetPanelOpen(!isPanelOpen);
            }

            if (isPanelOpen)
            {
                UpdateTelemetryAndTimeline();
            }
        }

        public void SetPanelOpen(bool open)
        {
            if (!isBuilt) EnsureBuilt();
            isPanelOpen = open;
            if (panelRoot != null) panelRoot.SetActive(open);
            if (open)
            {
                RefreshCameraToggleLabel();
                RefreshModeBadge();
                RefreshClipSelectionUI();
                UpdateTelemetryAndTimeline();
            }
            else if (isIsolatedMode)
            {
                ExitIsolatedPreview();
            }
        }

        public void ToggleCameraMode()
        {
            ResolveReferences();
            if (cameraModeController == null) return;
            cameraModeController.ToggleCameraMode();
            RefreshCameraToggleLabel();
        }

        public void EnterIsolatedPreview()
        {
            ResolveReferences();
            if (animController == null || libraryRegistry == null || libraryRegistry.Entries.Count == 0) return;

            isIsolatedMode = true;
            if (playerController != null) playerController.AllowMovement = false;
            if (cameraModeController != null && !cameraModeController.IsThirdPerson)
            {
                cameraModeController.SetCameraMode(CameraViewMode.ThirdPerson);
                RefreshCameraToggleLabel();
            }

            PlaySelectedClipInPreview();
            RefreshModeBadge();
        }

        public void ExitIsolatedPreview()
        {
            isIsolatedMode = false;
            if (animController != null)
            {
                animController.StopIsolatedPreview();
                animController.ClearManualLayerWeights();
            }
            if (playerController != null)
            {
                playerController.AllowMovement = true;
            }
            RefreshModeBadge();
        }

        public void SelectClipByIndex(int index)
        {
            if (libraryRegistry == null || libraryRegistry.Entries.Count == 0) return;
            int count = libraryRegistry.Entries.Count;
            selectedClipIndex = ((index % count) + count) % count;
            RefreshClipSelectionUI();

            if (!isIsolatedMode)
            {
                EnterIsolatedPreview();
            }
            else
            {
                PlaySelectedClipInPreview();
            }
        }

        private void PlaySelectedClipInPreview()
        {
            if (animController == null || libraryRegistry == null || libraryRegistry.Entries.Count == 0) return;
            var entry = libraryRegistry.Entries[selectedClipIndex];
            if (entry == null || entry.clip == null) return;

            animController.StartIsolatedPreview(entry.clip, entry.defaultLoop);
            if (speedSlider != null) animController.SetPreviewSpeed(speedSlider.value);
            RefreshPlaybackLabels();
        }

        private void RefreshCameraToggleLabel()
        {
            if (cameraToggleLabel == null) return;
            bool is3P = cameraModeController != null && cameraModeController.IsThirdPerson;
            cameraToggleLabel.text = is3P ? "CAM: 3P" : "CAM: 1P";
        }

        private void RefreshModeBadge()
        {
            if (modeBadgeText != null)
            {
                modeBadgeText.text = isIsolatedMode ? "MOD: İZOLE ÖNİZLEME" : "MOD: CANLI OYNANIŞ";
                modeBadgeText.color = isIsolatedMode ? new Color(1f, 0.80f, 0.25f) : new Color(0.45f, 0.92f, 0.62f);
            }
            RefreshPlaybackLabels();
        }

        private void RefreshPlaybackLabels()
        {
            if (animController == null) return;
            if (playPauseBtnLabel != null)
            {
                playPauseBtnLabel.text = (isIsolatedMode && animController.IsPreviewPlaying) ? "DURAKLAT" : "OYNAT";
            }
            if (loopBtnLabel != null)
            {
                loopBtnLabel.text = animController.IsPreviewLoop ? "DÖNGÜ: AÇIK" : "DÖNGÜ: KAPALI";
            }
        }

        private void RefreshClipSelectionUI()
        {
            if (libraryRegistry == null || libraryRegistry.Entries.Count == 0)
            {
                if (clipTitleText != null) clipTitleText.text = "Klip bulunamadı";
                return;
            }

            selectedClipIndex = Mathf.Clamp(selectedClipIndex, 0, libraryRegistry.Entries.Count - 1);
            var entry = libraryRegistry.Entries[selectedClipIndex];
            if (clipTitleText != null && entry != null)
            {
                float len = entry.clip != null ? entry.clip.length : 0f;
                clipTitleText.text = $"[{selectedClipIndex + 1}/{libraryRegistry.Entries.Count}] {entry.displayName} ({entry.category}, {len:F2}s)";
            }

            for (int i = 0; i < clipListBackgrounds.Count; i++)
            {
                if (clipListBackgrounds[i] != null)
                {
                    clipListBackgrounds[i].color = (i == selectedClipIndex)
                        ? new Color(0.75f, 0.52f, 0.14f, 0.85f)
                        : new Color(0.14f, 0.16f, 0.20f, 0.85f);
                }
            }
        }

        private void UpdateTelemetryAndTimeline()
        {
            if (animController == null || animController.AnimatorComponent == null) return;
            Animator anim = animController.AnimatorComponent;

            float normTime = 0f;
            string stateName = "Idle";

            if (isIsolatedMode && animController.CurrentPreviewClip != null)
            {
                normTime = animController.GetPreviewNormalizedTime();
                stateName = $"Preview: {animController.CurrentPreviewClip.name}";
                if (timelineSlider != null && !isScrubbing)
                {
                    timelineSlider.SetValueWithoutNotify(normTime);
                }
                if (timelineLabel != null)
                {
                    float curSec = normTime * animController.CurrentPreviewClip.length;
                    timelineLabel.text = $"Zaman: {curSec:F2}s / {animController.CurrentPreviewClip.length:F2}s ({normTime * 100f:F0}%)";
                }
            }
            else
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                normTime = st.normalizedTime % 1f;
                stateName = $"BaseHash:{st.shortNameHash} ({normTime:F2})";
                if (timelineLabel != null)
                {
                    timelineLabel.text = $"Canlı Döngü: {normTime * 100f:F0}%";
                }
            }

            if (telemetryText != null)
            {
                float rootY = playerController != null ? playerController.transform.position.y : anim.transform.position.y;
                float hipsY = GetBoneRelY(anim, HumanBodyBones.Hips, rootY);
                float lFootY = GetBoneRelY(anim, HumanBodyBones.LeftFoot, rootY);
                float rFootY = GetBoneRelY(anim, HumanBodyBones.RightFoot, rootY);
                bool grounded = playerController != null && playerController.IsGrounded;
                float vel = playerController != null ? new Vector3(playerController.CurrentVelocity.x, 0f, playerController.CurrentVelocity.z).magnitude : 0f;

                telemetryText.text =
                    $"Durum: {stateName}\n" +
                    $"Grounded: {grounded} | Hız: {vel:F2} m/s | RootMotion: {anim.applyRootMotion}\n" +
                    $"HipsY: {hipsY:F2}m | LFootY: {lFootY:F2}m | RFootY: {rFootY:F2}m";
            }
        }

        private float GetBoneRelY(Animator anim, HumanBodyBones bone, float rootY)
        {
            if (anim == null || !anim.isHuman) return 0f;
            Transform t = anim.GetBoneTransform(bone);
            return t != null ? (t.position.y - rootY) : 0f;
        }

        #region Runtime UGUI Builder
        private void BuildRuntimeUI()
        {
            Transform parentTransform = transform;
            Transform safeRoot = transform.Find("Mobile_Controls_Root");
            if (safeRoot != null) parentTransform = safeRoot;

            hudToggleRoot = new GameObject("Animation_Debug_HUD_Buttons", typeof(RectTransform));
            hudToggleRoot.transform.SetParent(parentTransform, false);
            var rootRt = hudToggleRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 1. Üst Sol: "ANIM" Panel Aç/Kapa Butonu (Stamina barının altında, joystick'ten uzak)
            var animBtn = CreateActionButton(hudToggleRoot.transform, "Mobile_AnimDebugButton", "ANIM",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -150f), new Vector2(138f, 64f),
                new Color(0.10f, 0.12f, 0.16f, 0.78f), () => SetPanelOpen(!isPanelOpen));
            AddBorderOutline(animBtn.gameObject, new Color(0.85f, 0.65f, 0.22f, 0.75f));

            // 2. Üst Sağ: "CAM: 1P / 3P" Kamera Modu Butonu (Pause butonunun hemen solunda)
            var camBtn = CreateActionButton(hudToggleRoot.transform, "Mobile_CameraToggleButton", "CAM: 1P",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-170f, -38f), new Vector2(156f, 68f),
                new Color(0.10f, 0.14f, 0.18f, 0.80f), ToggleCameraMode);
            cameraToggleLabel = camBtn.GetComponentInChildren<TextMeshProUGUI>();
            AddBorderOutline(camBtn.gameObject, new Color(0.45f, 0.78f, 0.95f, 0.75f));

            // 3. Katlanabilir Debug Paneli (Sol tarafa yaslı, sağ taraf karakteri görmek için açık)
            panelRoot = new GameObject("Animation_Debug_Panel_Root", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(parentTransform, false);
            var panelRt = panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0.04f);
            panelRt.anchorMax = new Vector2(0f, 0.96f);
            panelRt.pivot = new Vector2(0f, 0.5f);
            panelRt.anchoredPosition = new Vector2(28f, 0f);
            panelRt.sizeDelta = new Vector2(690f, 0f);

            var panelBg = panelRoot.GetComponent<Image>();
            panelBg.color = new Color(0.06f, 0.07f, 0.09f, 0.90f);
            AddBorderOutline(panelRoot, new Color(0.82f, 0.62f, 0.24f, 0.55f));

            var vLayout = panelRoot.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(14, 14, 10, 10);
            vLayout.spacing = 6f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            // Başlık & Kapatma Satırı
            var headerRow = CreateHorizontalRow(panelRoot.transform, 42f, 8f);
            modeBadgeText = CreateLabel(headerRow, "MOD: CANLI OYNANIŞ", 21, FontStyles.Bold, new Color(0.45f, 0.92f, 0.62f));
            var badgeLE = modeBadgeText.gameObject.AddComponent<LayoutElement>();
            badgeLE.flexibleWidth = 1f;

            CreateRowButton(headerRow, "CANLI", 105f, new Color(0.14f, 0.34f, 0.22f, 0.9f), ExitIsolatedPreview);
            CreateRowButton(headerRow, "ÖNİZLE", 115f, new Color(0.45f, 0.32f, 0.10f, 0.9f), EnterIsolatedPreview);
            CreateRowButton(headerRow, "X", 52f, new Color(0.48f, 0.14f, 0.14f, 0.9f), () => SetPanelOpen(false));

            // Kamera Mesafe & Yükseklik Satırı
            var camRow = CreateHorizontalRow(panelRoot.transform, 40f, 10f);
            CreateRowButton(camRow, "1P / 3P", 120f, new Color(0.16f, 0.28f, 0.42f, 0.9f), ToggleCameraMode);
            camDistSlider = CreateLabeledSlider(camRow, "Mesafe", 1.5f, 4.5f,
                cameraModeController != null ? cameraModeController.ThirdPersonDistance : 2.65f,
                v => { if (cameraModeController != null) cameraModeController.SetThirdPersonDistance(v); });
            camHeightSlider = CreateLabeledSlider(camRow, "Yükseklik", 1.1f, 2.2f,
                cameraModeController != null ? cameraModeController.ThirdPersonHeight : 1.55f,
                v => { if (cameraModeController != null) cameraModeController.SetThirdPersonHeight(v); });

            // Seçili Klip Başlığı ve Önceki/Sonraki
            var clipNavRow = CreateHorizontalRow(panelRoot.transform, 42f, 8f);
            CreateRowButton(clipNavRow, "< ÖNCEKİ", 125f, new Color(0.18f, 0.20f, 0.25f, 0.95f), () => SelectClipByIndex(selectedClipIndex - 1));
            clipTitleText = CreateLabel(clipNavRow, "Klip Seçilmedi", 18, FontStyles.Bold, Color.white);
            clipTitleText.alignment = TextAlignmentOptions.Center;
            clipTitleText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            CreateRowButton(clipNavRow, "SONRAKİ >", 130f, new Color(0.18f, 0.20f, 0.25f, 0.95f), () => SelectClipByIndex(selectedClipIndex + 1));

            // Oynatma Kontrolleri Satırı
            var playRow = CreateHorizontalRow(panelRoot.transform, 40f, 8f);
            var playBtn = CreateRowButton(playRow, "OYNAT", 130f, new Color(0.16f, 0.36f, 0.24f, 0.95f), () =>
            {
                if (!isIsolatedMode) EnterIsolatedPreview();
                else if (animController != null)
                {
                    animController.SetPreviewPlaying(!animController.IsPreviewPlaying);
                    RefreshPlaybackLabels();
                }
            });
            playPauseBtnLabel = playBtn.GetComponentInChildren<TextMeshProUGUI>();

            CreateRowButton(playRow, "BAŞA SAR", 130f, new Color(0.20f, 0.24f, 0.30f, 0.95f), () =>
            {
                if (!isIsolatedMode) EnterIsolatedPreview();
                else if (animController != null)
                {
                    animController.SetPreviewNormalizedTime(0f);
                    animController.SetPreviewPlaying(true);
                    RefreshPlaybackLabels();
                }
            });

            var loopBtn = CreateRowButton(playRow, "DÖNGÜ: AÇIK", 160f, new Color(0.24f, 0.22f, 0.34f, 0.95f), () =>
            {
                if (animController != null)
                {
                    animController.SetPreviewLoop(!animController.IsPreviewLoop);
                    RefreshPlaybackLabels();
                }
            });
            loopBtnLabel = loopBtn.GetComponentInChildren<TextMeshProUGUI>();

            CreateRowButton(playRow, "DURDUR", 120f, new Color(0.42f, 0.16f, 0.16f, 0.95f), ExitIsolatedPreview);

            // Zaman Çizelgesi & Hız Sliderları
            var scrubRow = CreateHorizontalRow(panelRoot.transform, 36f, 10f);
            timelineLabel = CreateLabel(scrubRow, "Zaman: 0.00s", 16, FontStyles.Normal, new Color(0.86f, 0.88f, 0.92f));
            var tlLE = timelineLabel.gameObject.AddComponent<LayoutElement>();
            tlLE.preferredWidth = 235f;
            timelineSlider = CreateRawSlider(scrubRow, 0f, 1f, 0f, v =>
            {
                if (isIsolatedMode && animController != null)
                {
                    isScrubbing = true;
                    animController.SetPreviewPlaying(false);
                    animController.SetPreviewNormalizedTime(v);
                    RefreshPlaybackLabels();
                    isScrubbing = false;
                }
            });

            var speedRow = CreateHorizontalRow(panelRoot.transform, 36f, 10f);
            speedLabel = CreateLabel(speedRow, "Hız: 1.00x", 16, FontStyles.Normal, new Color(0.86f, 0.88f, 0.92f));
            speedLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 135f;
            speedSlider = CreateRawSlider(speedRow, 0.25f, 2.0f, 1.0f, v =>
            {
                if (speedLabel != null) speedLabel.text = $"Hız: {v:F2}x";
                if (animController != null) animController.SetPreviewSpeed(v);
            });

            // Kaydırılabilir 23 Klip Listesi
            BuildClipScrollList(panelRoot.transform, 220f);

            // Hızlı Canlı Olay Tetikleyicileri
            var actionRow1 = CreateHorizontalRow(panelRoot.transform, 38f, 6f);
            CreateRowButton(actionRow1, "FENER BIRAK", 0f, new Color(0.32f, 0.24f, 0.14f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                if (flashlightPhysical != null)
                {
                    Vector3 impulse = playerController != null
                        ? playerController.transform.forward * 1.4f + Vector3.up * 0.35f
                        : Vector3.forward;
                    flashlightPhysical.DropWithPhysics(impulse);
                }
                else if (animController != null) animController.TriggerFlashlightRelease();
            });
            CreateRowButton(actionRow1, "FENER AL", 0f, new Color(0.20f, 0.32f, 0.18f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                if (flashlightPhysical != null)
                {
                    bool toHand = cameraModeController != null && cameraModeController.IsThirdPerson;
                    flashlightPhysical.PickupAndReattach(toHand);
                }
                else if (animController != null) animController.TriggerFlashlightPickup();
            });
            CreateRowButton(actionRow1, "KAPI AÇ", 0f, new Color(0.18f, 0.26f, 0.34f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerDoorInteract();
            });
            CreateRowButton(actionRow1, "SAĞA BAK", 0f, new Color(0.22f, 0.22f, 0.32f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerLookRight();
            });

            var actionRow2 = CreateHorizontalRow(panelRoot.transform, 38f, 6f);
            CreateRowButton(actionRow2, "SENDELE", 0f, new Color(0.32f, 0.22f, 0.22f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerStumble();
            });
            CreateRowButton(actionRow2, "DÜŞ", 0f, new Color(0.38f, 0.16f, 0.16f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerFall();
            });
            CreateRowButton(actionRow2, "KALK", 0f, new Color(0.18f, 0.32f, 0.28f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerGetUp();
            });
            CreateRowButton(actionRow2, "SIFIRLA", 0f, new Color(0.26f, 0.26f, 0.28f, 0.95f), ResetCharacterAndAnimations);

            // Katman Ağırlıkları (1..6) + Otomatik Sıfırla
            var layerRow = CreateHorizontalRow(panelRoot.transform, 40f, 6f);
            CreateRowButton(layerRow, "OTO KATMAN", 130f, new Color(0.20f, 0.24f, 0.28f, 0.95f), () =>
            {
                animController?.ClearManualLayerWeights();
            });
            string[] shortNames = { "", "Üst", "SağK", "SolK", "Baş", "Ekl", "Tam" };
            for (int i = 1; i <= 6; i++)
            {
                int layerIdx = i;
                layerSliders[layerIdx] = CreateLabeledSlider(layerRow, shortNames[layerIdx], 0f, 1f, i == 2 ? 1f : 0f,
                    w => animController?.SetManualLayerWeight(layerIdx, w));
            }

            // Canlı Telemetri Kutusu
            var telObj = new GameObject("Telemetry_Box", typeof(RectTransform), typeof(Image));
            telObj.transform.SetParent(panelRoot.transform, false);
            telObj.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.05f, 0.92f);
            var telLE = telObj.AddComponent<LayoutElement>();
            telLE.preferredHeight = 82f;

            telemetryText = CreateLabel(telObj.transform, "Telemetri hazırlanıyor...", 16, FontStyles.Normal, new Color(0.76f, 0.92f, 0.84f));
            var telRt = telemetryText.GetComponent<RectTransform>();
            telRt.anchorMin = Vector2.zero;
            telRt.anchorMax = Vector2.one;
            telRt.offsetMin = new Vector2(10f, 6f);
            telRt.offsetMax = new Vector2(-10f, -6f);
        }

        private void BuildClipScrollList(Transform parent, float height)
        {
            var scrollObj = new GameObject("Clip_ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObj.transform.SetParent(parent, false);
            scrollObj.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.90f);
            var le = scrollObj.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            var contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(scrollObj.transform, false);
            var contentRt = contentObj.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var contentLayout = contentObj.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(6, 6, 6, 6);
            contentLayout.spacing = 4f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;

            var fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollObj.GetComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            clipListButtons.Clear();
            clipListBackgrounds.Clear();

            if (libraryRegistry != null)
            {
                for (int i = 0; i < libraryRegistry.Entries.Count; i++)
                {
                    int idx = i;
                    var entry = libraryRegistry.Entries[i];
                    if (entry == null) continue;

                    string durText = entry.clip != null ? $"{entry.clip.length:F2}s" : "MISSING";
                    string label = $"{i + 1:D2}. {entry.displayName}   [{entry.category} | {durText}]";

                    var btn = CreateRowButton(contentObj.transform, label, 0f, new Color(0.14f, 0.16f, 0.20f, 0.85f), () => SelectClipByIndex(idx));
                    var btnLE = btn.GetComponent<LayoutElement>();
                    btnLE.preferredHeight = 42f;
                    var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.margin = new Vector4(12f, 0f, 8f, 0f);
                    tmp.fontSize = 18;

                    clipListButtons.Add(btn);
                    clipListBackgrounds.Add(btn.GetComponent<Image>());
                }
            }
        }

        private void ResetCharacterAndAnimations()
        {
            ExitIsolatedPreview();
            animController?.ResetAnimationState();
            if (playerController != null && hasSavedInitialPose)
            {
                var cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerController.transform.position = initialPlayerPosition;
                playerController.transform.rotation = initialPlayerRotation;
                if (cc != null) cc.enabled = true;
            }
            if (flashlightPhysical != null && cameraModeController != null)
            {
                if (cameraModeController.IsThirdPerson) flashlightPhysical.AttachToHand();
                else flashlightPhysical.AttachToCameraRig();
            }
        }

        private Transform CreateHorizontalRow(Transform parent, float height, float spacing)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            return row.transform;
        }

        private Button CreateActionButton(Transform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
            Color bgColor, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            go.GetComponent<Image>().color = bgColor;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var tmp = CreateLabel(go.transform, text, 21, FontStyles.Bold, Color.white);
            tmp.alignment = TextAlignmentOptions.Center;
            var trt = tmp.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            return btn;
        }

        private Button CreateRowButton(Transform parent, string text, float preferredWidth, Color bgColor, Action onClick)
        {
            var go = new GameObject($"Btn_{text}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = bgColor;
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth > 0f) le.preferredWidth = preferredWidth;
            else le.flexibleWidth = 1f;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var tmp = CreateLabel(go.transform, text, 17, FontStyles.Bold, Color.white);
            tmp.alignment = TextAlignmentOptions.Center;
            var trt = tmp.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4f, 0f);
            trt.offsetMax = new Vector2(-4f, 0f);
            return btn;
        }

        private Slider CreateLabeledSlider(Transform parent, string labelText, float min, float max, float initial, Action<float> onChanged)
        {
            var box = new GameObject($"SliderBox_{labelText}", typeof(RectTransform), typeof(VerticalLayoutGroup));
            box.transform.SetParent(parent, false);
            var vl = box.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 2f;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            box.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var lbl = CreateLabel(box.transform, labelText, 14, FontStyles.Normal, new Color(0.85f, 0.85f, 0.90f));
            lbl.alignment = TextAlignmentOptions.Center;
            return CreateRawSlider(box.transform, min, max, initial, onChanged);
        }

        private Slider CreateRawSlider(Transform parent, float min, float max, float initial, Action<float> onChanged)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 26f;

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.35f);
            bgRt.anchorMax = new Vector2(1f, 0.65f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.22f, 0.25f, 0.30f, 1f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fRt = fill.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.85f, 0.64f, 0.20f, 1f);

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(8f, 0f);
            haRt.offsetMax = new Vector2(-8f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(22f, 22f);
            handle.GetComponent<Image>().color = Color.white;

            var slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;
            slider.fillRect = fRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return slider;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void AddBorderOutline(GameObject go, Color color)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
        #endregion
    }
}
