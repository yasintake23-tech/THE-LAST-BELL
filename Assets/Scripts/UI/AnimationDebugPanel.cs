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
    /// THE LAST BELL - Gelişmiş Mobil Animasyon & Prosedürel Karakter Hata Ayıklama Paneli.
    /// Özellikler:
    /// - Tüm 52 (29 V2 + 23 V1) klibe ALL / V2 / V1 filtresiyle erişim ve karşılaştırma.
    /// - Canlı Oynanış vs İzole Önizleme (PlayableGraph) modları.
    /// - 1P (True FPS) / 3P / Serbest İnceleme Yörüngesi (karakter bakışını bozmadan 360° inceleme).
    /// - Nötr İnceleme Işığı ve çalışma zamanı IK/kemik/soket görselleştirme işaretçileri.
    /// - Bağımsız prosedürel anahtarlar: Kafa, Gövde, Ayak IK, El IK, Fener Nişan, İkincil Hareket.
    /// - Taşma yapmayan kaydırılabilir gövde + canlı kemik/zemin/bakış telemetrisi.
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
        [SerializeField] private CharacterProceduralController proceduralController;
        [SerializeField] private CameraModeController cameraModeController;
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private FlashlightPhysicalItem flashlightPhysical;

        private bool isBuilt = false;
        private bool isPanelOpen = false;
        private bool isIsolatedMode = false;
        private int selectedClipIndex = 0;
        private string activeVersionFilter = "ALL"; // "ALL", "V2", "V1"
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
        private TextMeshProUGUI neutralLightBtnLabel;
        private TextMeshProUGUI gizmoBtnLabel;
        private TextMeshProUGUI telemetryText;
        private Slider timelineSlider;
        private Slider speedSlider;
        private Slider camDistSlider;
        private Slider camHeightSlider;
        private Slider orbitYawSlider;
        private Transform clipListContent;
        private readonly List<int> filteredIndices = new List<int>();
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
            RebuildClipListItems();
            RefreshClipSelectionUI();
        }

        private void ResolveReferences()
        {
            if (libraryRegistry == null)
            {
                libraryRegistry = Resources.Load<AnimationLibraryRegistry>("AnimationLibraryRegistry");
            }
            if (playerController == null) playerController = FindAnyObjectByType<FirstPersonController>();
            if (cameraModeController == null) cameraModeController = FindAnyObjectByType<CameraModeController>();
            if (animController == null) animController = FindAnyObjectByType<ProtagonistAnimationController>();
            if (proceduralController == null) proceduralController = FindAnyObjectByType<CharacterProceduralController>();
            if (flashlightPhysical == null) flashlightPhysical = FindAnyObjectByType<FlashlightPhysicalItem>();

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

        public void SetCameraModeDirect(CameraViewMode mode)
        {
            ResolveReferences();
            if (cameraModeController == null) return;
            cameraModeController.SetCameraMode(mode);
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

            if (!isIsolatedMode) EnterIsolatedPreview();
            else PlaySelectedClipInPreview();
        }

        private void SelectFilteredStep(int delta)
        {
            if (filteredIndices.Count == 0) return;
            int curPos = filteredIndices.IndexOf(selectedClipIndex);
            if (curPos < 0) curPos = 0;
            int nextPos = ((curPos + delta) % filteredIndices.Count + filteredIndices.Count) % filteredIndices.Count;
            SelectClipByIndex(filteredIndices[nextPos]);
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
            if (cameraModeController == null) { cameraToggleLabel.text = "CAM: 1P"; return; }
            switch (cameraModeController.CurrentMode)
            {
                case CameraViewMode.FirstPerson: cameraToggleLabel.text = "CAM: 1P"; break;
                case CameraViewMode.ThirdPerson: cameraToggleLabel.text = "CAM: 3P"; break;
                case CameraViewMode.FreeInspectionOrbit: cameraToggleLabel.text = "CAM: ORB"; break;
            }
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
                playPauseBtnLabel.text = (isIsolatedMode && animController.IsPreviewPlaying) ? "DURAKLAT" : "OYNAT";
            if (loopBtnLabel != null)
                loopBtnLabel.text = animController.IsPreviewLoop ? "DÖNGÜ: AÇIK" : "DÖNGÜ: KAPALI";
            if (neutralLightBtnLabel != null && cameraModeController != null)
                neutralLightBtnLabel.text = cameraModeController.IsNeutralLightActive ? "IŞIK: NÖTR" : "IŞIK: KORKU";
            if (gizmoBtnLabel != null && proceduralController != null)
                gizmoBtnLabel.text = proceduralController.enableDebugVisuals ? "GİZMO: AÇIK" : "GİZMO: KAPALI";
        }

        private void SetVersionFilter(string filter)
        {
            activeVersionFilter = filter;
            RebuildClipListItems();
            RefreshClipSelectionUI();
        }

        private void RebuildClipListItems()
        {
            if (clipListContent == null) return;
            for (int i = clipListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(clipListContent.GetChild(i).gameObject);
            }
            filteredIndices.Clear();
            clipListBackgrounds.Clear();

            if (libraryRegistry == null) return;

            for (int i = 0; i < libraryRegistry.Entries.Count; i++)
            {
                var entry = libraryRegistry.Entries[i];
                if (entry == null) continue;
                string ver = string.IsNullOrEmpty(entry.versionTag) ? "V1" : entry.versionTag;
                if (activeVersionFilter != "ALL" && !string.Equals(ver, activeVersionFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                int idx = i;
                filteredIndices.Add(idx);
                string durText = entry.clip != null ? $"{entry.clip.length:F2}s" : "MISSING";
                string statusTag = entry.approvalStatus == AnimationApprovalStatus.DebugPreviewOnly ? " [DBG]" : "";
                string label = $"{filteredIndices.Count:D2}. [{ver}] {entry.displayName} ({durText}){statusTag}";

                var btn = CreateRowButton(clipListContent, label, 0f, new Color(0.14f, 0.16f, 0.20f, 0.88f), () => SelectClipByIndex(idx));
                btn.GetComponent<LayoutElement>().preferredHeight = 36f;
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.margin = new Vector4(10f, 0f, 8f, 0f);
                tmp.fontSize = 16;

                clipListBackgrounds.Add(btn.GetComponent<Image>());
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
                string ver = string.IsNullOrEmpty(entry.versionTag) ? "V1" : entry.versionTag;
                clipTitleText.text = $"[{selectedClipIndex + 1}/{libraryRegistry.Entries.Count}] [{ver}] {entry.displayName} ({len:F2}s)";
            }

            for (int i = 0; i < filteredIndices.Count && i < clipListBackgrounds.Count; i++)
            {
                if (clipListBackgrounds[i] != null)
                {
                    clipListBackgrounds[i].color = (filteredIndices[i] == selectedClipIndex)
                        ? new Color(0.75f, 0.52f, 0.14f, 0.90f)
                        : new Color(0.14f, 0.16f, 0.20f, 0.88f);
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
                    timelineSlider.SetValueWithoutNotify(normTime);
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
                stateName = $"Canlı ({normTime:F2})";
                if (timelineLabel != null) timelineLabel.text = $"Canlı Döngü: {normTime * 100f:F0}%";
            }

            if (telemetryText != null)
            {
                float rootY = playerController != null ? playerController.transform.position.y : anim.transform.position.y;
                float hipsY = GetBoneRelY(anim, HumanBodyBones.Hips, rootY);
                float lFootY = GetBoneRelY(anim, HumanBodyBones.LeftFoot, rootY);
                float rFootY = GetBoneRelY(anim, HumanBodyBones.RightFoot, rootY);
                bool grounded = playerController != null && playerController.IsGrounded;
                float vel = playerController != null ? new Vector3(playerController.CurrentVelocity.x, 0f, playerController.CurrentVelocity.z).magnitude : 0f;
                float relYaw = playerController != null ? playerController.RelativeLookYaw : 0f;
                float pitch = playerController != null ? playerController.DesiredLookPitch : 0f;

                telemetryText.text =
                    $"{stateName} | Grounded:{grounded} | Hız:{vel:F2}m/s\n" +
                    $"Bakış RelYaw:{relYaw:F1}° Pitch:{pitch:F1}° | WallPull:{(proceduralController != null ? proceduralController.WallPullback01 : 0f):F2}\n" +
                    $"HipsY:{hipsY:F2}m | LFootY:{lFootY:F2}m | RFootY:{rFootY:F2}m";
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

            // Üst Sol: "ANIM" Aç/Kapa Butonu
            var animBtn = CreateActionButton(hudToggleRoot.transform, "Mobile_AnimDebugButton", "ANIM",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -150f), new Vector2(138f, 64f),
                new Color(0.10f, 0.12f, 0.16f, 0.78f), () => SetPanelOpen(!isPanelOpen));
            AddBorderOutline(animBtn.gameObject, new Color(0.85f, 0.65f, 0.22f, 0.75f));

            // Üst Sağ: "CAM: 1P / 3P" Butonu
            var camBtn = CreateActionButton(hudToggleRoot.transform, "Mobile_CameraToggleButton", "CAM: 1P",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-170f, -38f), new Vector2(156f, 68f),
                new Color(0.10f, 0.14f, 0.18f, 0.80f), ToggleCameraMode);
            cameraToggleLabel = camBtn.GetComponentInChildren<TextMeshProUGUI>();
            AddBorderOutline(camBtn.gameObject, new Color(0.45f, 0.78f, 0.95f, 0.75f));

            // Katlanabilir Ana Panel (Sol tarafta, taşmayı önlemek için içten kaydırılabilir)
            panelRoot = new GameObject("Animation_Debug_Panel_Root", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(parentTransform, false);
            var panelRt = panelRoot.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0.03f);
            panelRt.anchorMax = new Vector2(0f, 0.97f);
            panelRt.pivot = new Vector2(0f, 0.5f);
            panelRt.anchoredPosition = new Vector2(24f, 0f);
            panelRt.sizeDelta = new Vector2(700f, 0f);

            panelRoot.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.92f);
            AddBorderOutline(panelRoot, new Color(0.82f, 0.62f, 0.24f, 0.65f));

            // Ana ScrollRect (hiçbir eleman çerçeveden taşmaz!)
            var bodyScroll = new GameObject("Panel_Body_Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            bodyScroll.transform.SetParent(panelRoot.transform, false);
            var bsRt = bodyScroll.GetComponent<RectTransform>();
            bsRt.anchorMin = Vector2.zero;
            bsRt.anchorMax = Vector2.one;
            bsRt.offsetMin = new Vector2(8f, 8f);
            bsRt.offsetMax = new Vector2(-8f, -8f);

            var bodyContent = new GameObject("Body_Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            bodyContent.transform.SetParent(bodyScroll.transform, false);
            var bcRt = bodyContent.GetComponent<RectTransform>();
            bcRt.anchorMin = new Vector2(0f, 1f);
            bcRt.anchorMax = new Vector2(1f, 1f);
            bcRt.pivot = new Vector2(0.5f, 1f);
            bcRt.offsetMin = Vector2.zero;
            bcRt.offsetMax = Vector2.zero;

            var vLayout = bodyContent.GetComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(6, 6, 4, 8);
            vLayout.spacing = 6f;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childForceExpandWidth = true;
            bodyContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var mainSr = bodyScroll.GetComponent<ScrollRect>();
            mainSr.content = bcRt;
            mainSr.horizontal = false;
            mainSr.vertical = true;
            mainSr.scrollSensitivity = 30f;

            Transform body = bodyContent.transform;

            // 1. Başlık & Mod Satırı
            var headerRow = CreateHorizontalRow(body, 40f, 6f);
            modeBadgeText = CreateLabel(headerRow, "MOD: CANLI OYNANIŞ", 19, FontStyles.Bold, new Color(0.45f, 0.92f, 0.62f));
            modeBadgeText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            CreateRowButton(headerRow, "CANLI", 96f, new Color(0.14f, 0.34f, 0.22f, 0.95f), ExitIsolatedPreview);
            CreateRowButton(headerRow, "ÖNİZLE", 104f, new Color(0.45f, 0.32f, 0.10f, 0.95f), EnterIsolatedPreview);
            CreateRowButton(headerRow, "X", 48f, new Color(0.48f, 0.14f, 0.14f, 0.95f), () => SetPanelOpen(false));

            // 2. Kamera Modları (1P / 3P / ORBIT) + Nötr Işık + Gizmo
            var camModeRow = CreateHorizontalRow(body, 38f, 6f);
            CreateRowButton(camModeRow, "1P", 0f, new Color(0.16f, 0.26f, 0.38f, 0.95f), () => SetCameraModeDirect(CameraViewMode.FirstPerson));
            CreateRowButton(camModeRow, "3P", 0f, new Color(0.16f, 0.30f, 0.44f, 0.95f), () => SetCameraModeDirect(CameraViewMode.ThirdPerson));
            CreateRowButton(camModeRow, "ORBIT", 0f, new Color(0.26f, 0.22f, 0.42f, 0.95f), () => SetCameraModeDirect(CameraViewMode.FreeInspectionOrbit));
            var lightBtn = CreateRowButton(camModeRow, "IŞIK: KORKU", 0f, new Color(0.32f, 0.28f, 0.16f, 0.95f), () =>
            {
                if (cameraModeController != null)
                {
                    cameraModeController.SetNeutralInspectionLight(!cameraModeController.IsNeutralLightActive);
                    RefreshPlaybackLabels();
                }
            });
            neutralLightBtnLabel = lightBtn.GetComponentInChildren<TextMeshProUGUI>();
            var gizBtn = CreateRowButton(camModeRow, "GİZMO: KAPALI", 0f, new Color(0.22f, 0.28f, 0.24f, 0.95f), () =>
            {
                if (proceduralController != null)
                {
                    proceduralController.enableDebugVisuals = !proceduralController.enableDebugVisuals;
                    RefreshPlaybackLabels();
                }
            });
            gizmoBtnLabel = gizBtn.GetComponentInChildren<TextMeshProUGUI>();

            // 3. Kamera Mesafe / Yükseklik / Yörünge Açısı Sliderları
            var camRow = CreateHorizontalRow(body, 40f, 8f);
            camDistSlider = CreateLabeledSlider(camRow, "Mesafe", 1.5f, 4.5f,
                cameraModeController != null ? cameraModeController.ThirdPersonDistance : 2.65f,
                v => { if (cameraModeController != null) cameraModeController.SetThirdPersonDistance(v); });
            camHeightSlider = CreateLabeledSlider(camRow, "Yükseklik", 1.0f, 2.2f,
                cameraModeController != null ? cameraModeController.ThirdPersonHeight : 1.55f,
                v => { if (cameraModeController != null) cameraModeController.SetThirdPersonHeight(v); });
            orbitYawSlider = CreateLabeledSlider(camRow, "Orbit Açısı", 0f, 360f, 155f,
                v => { if (cameraModeController != null) cameraModeController.SetOrbitAngles(v, 12f); });

            // 4. Sürüm Filtresi (ALL 52 / V2 29 / V1 23) + Önceki/Sonraki
            var filterRow = CreateHorizontalRow(body, 36f, 6f);
            CreateRowButton(filterRow, "TÜMÜ (52)", 0f, new Color(0.22f, 0.24f, 0.30f, 0.95f), () => SetVersionFilter("ALL"));
            CreateRowButton(filterRow, "V2 YENİ (29)", 0f, new Color(0.18f, 0.34f, 0.24f, 0.95f), () => SetVersionFilter("V2"));
            CreateRowButton(filterRow, "V1 ESKİ (23)", 0f, new Color(0.28f, 0.22f, 0.18f, 0.95f), () => SetVersionFilter("V1"));

            var clipNavRow = CreateHorizontalRow(body, 38f, 6f);
            CreateRowButton(clipNavRow, "< ÖNCEKİ", 118f, new Color(0.18f, 0.20f, 0.25f, 0.95f), () => SelectFilteredStep(-1));
            clipTitleText = CreateLabel(clipNavRow, "Klip Seçilmedi", 16, FontStyles.Bold, Color.white);
            clipTitleText.alignment = TextAlignmentOptions.Center;
            clipTitleText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            CreateRowButton(clipNavRow, "SONRAKİ >", 122f, new Color(0.18f, 0.20f, 0.25f, 0.95f), () => SelectFilteredStep(1));

            // 5. Oynatma Kontrolleri + Zaman & Hız
            var playRow = CreateHorizontalRow(body, 38f, 6f);
            var playBtn = CreateRowButton(playRow, "OYNAT", 0f, new Color(0.16f, 0.36f, 0.24f, 0.95f), () =>
            {
                if (!isIsolatedMode) EnterIsolatedPreview();
                else if (animController != null)
                {
                    animController.SetPreviewPlaying(!animController.IsPreviewPlaying);
                    RefreshPlaybackLabels();
                }
            });
            playPauseBtnLabel = playBtn.GetComponentInChildren<TextMeshProUGUI>();
            CreateRowButton(playRow, "BAŞA SAR", 0f, new Color(0.20f, 0.24f, 0.30f, 0.95f), () =>
            {
                if (!isIsolatedMode) EnterIsolatedPreview();
                else if (animController != null)
                {
                    animController.SetPreviewNormalizedTime(0f);
                    animController.SetPreviewPlaying(true);
                    RefreshPlaybackLabels();
                }
            });
            var loopBtn = CreateRowButton(playRow, "DÖNGÜ: AÇIK", 0f, new Color(0.24f, 0.22f, 0.34f, 0.95f), () =>
            {
                if (animController != null)
                {
                    animController.SetPreviewLoop(!animController.IsPreviewLoop);
                    RefreshPlaybackLabels();
                }
            });
            loopBtnLabel = loopBtn.GetComponentInChildren<TextMeshProUGUI>();
            CreateRowButton(playRow, "DURDUR", 0f, new Color(0.42f, 0.16f, 0.16f, 0.95f), ExitIsolatedPreview);

            var scrubRow = CreateHorizontalRow(body, 32f, 8f);
            timelineLabel = CreateLabel(scrubRow, "Zaman: 0.00s", 15, FontStyles.Normal, new Color(0.86f, 0.88f, 0.92f));
            timelineLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 225f;
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

            var speedRow = CreateHorizontalRow(body, 32f, 8f);
            speedLabel = CreateLabel(speedRow, "Hız: 1.00x", 15, FontStyles.Normal, new Color(0.86f, 0.88f, 0.92f));
            speedLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 125f;
            speedSlider = CreateRawSlider(speedRow, 0.25f, 2.0f, 1.0f, v =>
            {
                if (speedLabel != null) speedLabel.text = $"Hız: {v:F2}x";
                if (animController != null) animController.SetPreviewSpeed(v);
            });

            // 6. Kaydırılabilir 52 Klip Listesi
            BuildClipScrollList(body, 170f);

            // 7. Bağımsız Prosedürel Sistem Anahtarları (6 Toggle)
            var procRow = CreateHorizontalRow(body, 36f, 4f);
            CreateToggleButton(procRow, "KAFA", () => proceduralController == null || proceduralController.enableHeadTracking,
                v => { if (proceduralController != null) proceduralController.enableHeadTracking = v; });
            CreateToggleButton(procRow, "GÖVDE", () => proceduralController == null || proceduralController.enableTorsoTracking,
                v => { if (proceduralController != null) proceduralController.enableTorsoTracking = v; });
            CreateToggleButton(procRow, "AYAK IK", () => proceduralController == null || proceduralController.enableFootIK,
                v => { if (proceduralController != null) proceduralController.enableFootIK = v; });
            CreateToggleButton(procRow, "EL IK", () => proceduralController == null || proceduralController.enableHandIK,
                v => { if (proceduralController != null) proceduralController.enableHandIK = v; });
            CreateToggleButton(procRow, "FENER", () => proceduralController == null || proceduralController.enableFlashlightAiming,
                v => { if (proceduralController != null) proceduralController.enableFlashlightAiming = v; });
            CreateToggleButton(procRow, "İKİNCİL", () => proceduralController == null || proceduralController.enableSecondaryMotion,
                v => { if (proceduralController != null) proceduralController.enableSecondaryMotion = v; });

            // 8. Hızlı Canlı Olay Tetikleyicileri
            var actionRow1 = CreateHorizontalRow(body, 36f, 5f);
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
                else animController?.TriggerFlashlightRelease();
            });
            CreateRowButton(actionRow1, "FENER AL", 0f, new Color(0.20f, 0.32f, 0.18f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                if (flashlightPhysical != null) flashlightPhysical.PickupAndReattach(true);
                else animController?.TriggerFlashlightPickup();
            });
            CreateRowButton(actionRow1, "KAPI AÇ", 0f, new Color(0.18f, 0.26f, 0.34f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerDoorInteract();
            });
            CreateRowButton(actionRow1, "İRKİL", 0f, new Color(0.30f, 0.20f, 0.32f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerStartle();
            });

            var actionRow2 = CreateHorizontalRow(body, 36f, 5f);
            CreateRowButton(actionRow2, "DÖN SOL", 0f, new Color(0.20f, 0.24f, 0.30f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerTurnInPlace(-1);
            });
            CreateRowButton(actionRow2, "DÖN SAĞ", 0f, new Color(0.20f, 0.24f, 0.30f, 0.95f), () =>
            {
                ExitIsolatedPreview();
                animController?.TriggerTurnInPlace(1);
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

            // 9. Katman Ağırlıkları (1..6)
            var layerRow = CreateHorizontalRow(body, 38f, 5f);
            CreateRowButton(layerRow, "OTO KAT", 100f, new Color(0.20f, 0.24f, 0.28f, 0.95f), () =>
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

            // 10. Canlı Telemetri Kutusu
            var telObj = new GameObject("Telemetry_Box", typeof(RectTransform), typeof(Image));
            telObj.transform.SetParent(body, false);
            telObj.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.05f, 0.95f);
            telObj.AddComponent<LayoutElement>().preferredHeight = 76f;

            telemetryText = CreateLabel(telObj.transform, "Telemetri hazırlanıyor...", 15, FontStyles.Normal, new Color(0.76f, 0.92f, 0.84f));
            var telRt = telemetryText.GetComponent<RectTransform>();
            telRt.anchorMin = Vector2.zero;
            telRt.anchorMax = Vector2.one;
            telRt.offsetMin = new Vector2(8f, 4f);
            telRt.offsetMax = new Vector2(-8f, -4f);
        }

        private void BuildClipScrollList(Transform parent, float height)
        {
            var scrollObj = new GameObject("Clip_ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObj.transform.SetParent(parent, false);
            scrollObj.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.07f, 0.92f);
            scrollObj.AddComponent<LayoutElement>().preferredHeight = height;

            var contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(scrollObj.transform, false);
            var contentRt = contentObj.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var contentLayout = contentObj.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(5, 5, 5, 5);
            contentLayout.spacing = 3f;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;

            contentObj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollObj.GetComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            clipListContent = contentObj.transform;
        }

        private void CreateToggleButton(Transform parent, string label, Func<bool> getter, Action<bool> setter)
        {
            var btn = CreateRowButton(parent, label, 0f, new Color(0.16f, 0.34f, 0.22f, 0.95f), null);
            var img = btn.GetComponent<Image>();
            void Refresh()
            {
                bool on = getter();
                img.color = on ? new Color(0.16f, 0.36f, 0.22f, 0.95f) : new Color(0.28f, 0.16f, 0.16f, 0.95f);
            }
            Refresh();
            btn.onClick.AddListener(() => { setter(!getter()); Refresh(); });
        }

        private void ResetCharacterAndAnimations()
        {
            ExitIsolatedPreview();
            animController?.ResetAnimationState();
            proceduralController?.CancelEnvironmentalInteraction();
            if (playerController != null && hasSavedInitialPose)
            {
                var cc = playerController.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerController.transform.position = initialPlayerPosition;
                playerController.transform.rotation = initialPlayerRotation;
                if (cc != null) cc.enabled = true;
            }
            if (flashlightPhysical != null) flashlightPhysical.AttachToHand();
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
            row.AddComponent<LayoutElement>().preferredHeight = height;
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
            if (onClick != null) btn.onClick.AddListener(() => onClick.Invoke());

            var tmp = CreateLabel(go.transform, text, 15, FontStyles.Bold, Color.white);
            tmp.alignment = TextAlignmentOptions.Center;
            var trt = tmp.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(3f, 0f);
            trt.offsetMax = new Vector2(-3f, 0f);
            return btn;
        }

        private Slider CreateLabeledSlider(Transform parent, string labelText, float min, float max, float initial, Action<float> onChanged)
        {
            var box = new GameObject($"SliderBox_{labelText}", typeof(RectTransform), typeof(VerticalLayoutGroup));
            box.transform.SetParent(parent, false);
            var vl = box.GetComponent<VerticalLayoutGroup>();
            vl.spacing = 1f;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            box.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var lbl = CreateLabel(box.transform, labelText, 13, FontStyles.Normal, new Color(0.85f, 0.85f, 0.90f));
            lbl.alignment = TextAlignmentOptions.Center;
            return CreateRawSlider(box.transform, min, max, initial, onChanged);
        }

        private Slider CreateRawSlider(Transform parent, float min, float max, float initial, Action<float> onChanged)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 20f;

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
            hRt.anchorMin = new Vector2(0.5f, 0.5f);
            hRt.anchorMax = new Vector2(0.5f, 0.5f);
            hRt.sizeDelta = new Vector2(16f, 16f);
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
