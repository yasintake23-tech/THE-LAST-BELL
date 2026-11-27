using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TheLastBell.Player;
using TheLastBell.Interaction;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Profesyonel Konsol/Mobil Standartlarında Dokunmatik Kontrol Arayüzü.
    /// Özellikler:
    /// - Sol Ekran: Dinamik Yüzen (Floating) Joystick, Deadzone (0.1f) ve yumuşak Lerp sönümleme.
    /// - Sağ Ekran: Dokunmatik serbest bakış (TouchLookZone) ile tam entegrasyon.
    /// - Aksiyon Butonları (Ergonomik Başparmak Yayı):
    ///     * FENER: Anında SpotLight aç/kapa + altın sarısı/kehribar iç parlama (#E5B842, %75 alfa).
    ///     * ÇÖMEL: Anında boy ve kamera geçişi (1.0m - 1.8m) + kehribar görsel geri bildirim.
    ///     * KOŞ: Basılı tutma (Hold) mantığıyla 2 kat hız + buz mavisi/beyaz parlama.
    ///     * ETKİLEŞİM: Yakında nesne varsa belirir, tek dokunuşla tetikler.
    /// - Taktil boyut darbesi (scale punch) ile dokunma hissi.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        public static MobileControlsUI Instance { get; private set; }
        public Vector2 MovementInput => currentMoveInput;
        public bool IsSprinting => isSprinting;

        [Header("--- Karakter & Sistem Referansları ---")]
        [SerializeField] private FirstPersonController fpsController;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private FlashlightController flashlight;

        [Header("--- Dinamik Yüzen (Floating) Joystick ---")]
        [SerializeField] private RectTransform joystickTouchArea;
        [SerializeField] private RectTransform joystickBackground;
        [SerializeField] private RectTransform joystickHandle;
        [SerializeField] private CanvasGroup joystickCanvasGroup;
        [SerializeField] private float joystickRadius = 50f;
        [SerializeField] private float deadzone = 0.1f;
        [SerializeField] private float inputLerpSpeed = 22f;
        [SerializeField] private Vector2 defaultJoystickPosition = new Vector2(140f, 140f);

        [Header("--- Aksiyon Butonları ---")]
        [SerializeField] private Button actionButton;
        [SerializeField] private CanvasGroup actionButtonCanvasGroup;
        [SerializeField] private TextMeshProUGUI actionButtonText;
        [SerializeField] private Image actionButtonImage;

        [SerializeField] private Button sprintButton;
        [SerializeField] private Image sprintButtonImage;

        [SerializeField] private Button crouchButton;
        [SerializeField] private Image crouchButtonImage;

        [SerializeField] private Button flashlightButton;
        [SerializeField] private Image flashlightButtonImage;

        [SerializeField] private Button pauseButton;

        [Header("--- Renk Temaları (Minimalist Korku Füme & İnce Parlama) ---")]
        [SerializeField] private Color normalButtonColor = new Color(0.06f, 0.07f, 0.09f, 0.42f);
        [SerializeField] private Color flashlightActiveColor = new Color(0.85f, 0.68f, 0.25f, 0.70f);
        [SerializeField] private Color crouchActiveColor = new Color(0.88f, 0.48f, 0.18f, 0.70f);
        [SerializeField] private Color sprintActiveColor = new Color(0.45f, 0.78f, 0.92f, 0.74f);
        [SerializeField] private Color actionActiveColor = new Color(0.78f, 0.22f, 0.22f, 0.80f);

        private int movePointerId = int.MinValue;
        private Vector2 targetMoveInput = Vector2.zero;
        private Vector2 currentMoveInput = Vector2.zero;
        private bool isSprinting = false;
        private bool isPaused = false;
        private RectTransform parentRect;
        private GameObject pauseOverlayRoot;
        private TouchLookZone touchLookZone;

        private void Awake()
        {
            Instance = this;
            parentRect = transform.parent as RectTransform ?? GetComponent<RectTransform>();

            if (fpsController == null) fpsController = FindAnyObjectByType<FirstPersonController>();
            if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
            if (flashlight == null) flashlight = FindAnyObjectByType<FlashlightController>();
            if (touchLookZone == null) touchLookZone = transform.parent != null ? transform.parent.GetComponentInChildren<TouchLookZone>(true) : FindAnyObjectByType<TouchLookZone>();

            if (joystickTouchArea == null) joystickTouchArea = GetComponent<RectTransform>();

            if (joystickCanvasGroup == null && joystickBackground != null)
            {
                joystickCanvasGroup = joystickBackground.GetComponent<CanvasGroup>();
                if (joystickCanvasGroup == null) joystickCanvasGroup = joystickBackground.gameObject.AddComponent<CanvasGroup>();
            }

            if (actionButton != null && actionButtonCanvasGroup == null)
            {
                actionButtonCanvasGroup = actionButton.GetComponent<CanvasGroup>();
                if (actionButtonCanvasGroup == null) actionButtonCanvasGroup = actionButton.gameObject.AddComponent<CanvasGroup>();
            }

            ResetJoystickVisual(false);
            SetActionButtonVisible(false);
        }

        private void Start()
        {
            if (actionButton != null) actionButton.onClick.AddListener(OnActionButtonClicked);
            if (crouchButton != null) crouchButton.onClick.AddListener(OnCrouchButtonClicked);
            if (flashlightButton != null) flashlightButton.onClick.AddListener(OnFlashlightButtonClicked);
            if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);

            SetupSprintButtonHold();

            if (interactor != null)
            {
                interactor.OnPromptUpdated += OnInteractionPromptUpdated;
            }

            if (flashlight != null)
            {
                flashlight.OnFlashlightToggled += OnFlashlightStateChanged;
                UpdateFlashlightVisual(flashlight.IsOn);
            }
            else
            {
                var spot = GameObject.Find("SpotLight")?.GetComponent<Light>();
                UpdateFlashlightVisual(spot != null && spot.enabled);
            }

            UpdateCrouchVisual(fpsController != null && fpsController.IsCrouching);
            UpdateSprintVisual(isSprinting);

            Debug.Log($"[MobileControlsUI] Başlatıldı -> FPC={fpsController != null}, Interactor={interactor != null}, Flashlight={flashlight != null}, JoystickBG={joystickBackground != null}, TouchLook={touchLookZone != null}");
        }

        private void SetupSprintButtonHold()
        {
            if (sprintButton == null) return;

            var holdBtn = sprintButton.GetComponent<MobileHoldButton>();
            if (holdBtn == null) holdBtn = sprintButton.gameObject.AddComponent<MobileHoldButton>();

            holdBtn.onPointerDown = RunStart;
            holdBtn.onPointerUp = RunStop;

            // Çift tetiklemeyi önlemek için eski EventTrigger delegate'lerini temizle
            var trigger = sprintButton.GetComponent<EventTrigger>();
            if (trigger != null && trigger.triggers != null)
            {
                trigger.triggers.Clear();
            }
        }

        private void OnDisable()
        {
            ResetAllTouchInput();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) ResetAllTouchInput();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (interactor != null)
            {
                interactor.OnPromptUpdated -= OnInteractionPromptUpdated;
            }
            if (flashlight != null)
            {
                flashlight.OnFlashlightToggled -= OnFlashlightStateChanged;
            }

            Time.timeScale = 1f;
        }

        private void ResetAllTouchInput()
        {
            movePointerId = int.MinValue;
            targetMoveInput = Vector2.zero;
            currentMoveInput = Vector2.zero;
            if (isSprinting)
            {
                isSprinting = false;
                UpdateSprintVisual(false);
            }
            if (fpsController != null)
            {
                fpsController.MobileMoveInput = Vector2.zero;
                fpsController.MobileSprintInput = false;
            }
            ResetJoystickVisual(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (PlayerHUD.Instance == null || !PlayerHUD.Instance.IsReadingNote)
                {
                    TogglePause();
                }
            }

            // Aktif joystick parmağı kalktıysa güvenlik sıfırlaması
            if (movePointerId != int.MinValue && Input.touchCount > 0)
            {
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.fingerId == movePointerId)
                    {
                        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        {
                            movePointerId = int.MinValue;
                            targetMoveInput = Vector2.zero;
                            ResetJoystickVisual(true);
                        }
                        found = true;
                        break;
                    }
                }
                if (!found && movePointerId >= 0)
                {
                    movePointerId = int.MinValue;
                    targetMoveInput = Vector2.zero;
                    ResetJoystickVisual(true);
                }
            }

            currentMoveInput = Vector2.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * inputLerpSpeed);
            if (targetMoveInput == Vector2.zero && currentMoveInput.sqrMagnitude < 0.0001f)
            {
                currentMoveInput = Vector2.zero;
            }

            if (fpsController != null && fpsController.enabled)
            {
                fpsController.MobileMoveInput = currentMoveInput;
                fpsController.MobileSprintInput = isSprinting;
            }
        }

        #region Dinamik Yüzen (Floating) Joystick Olayları
        public void OnPointerDown(PointerEventData eventData)
        {
            if (Time.timeScale <= 0.001f || isPaused) return;

            // Yalnızca ilk temas eden parmak joystick'e bağlanır
            if (movePointerId != int.MinValue) return;

            // Dokunulan nokta ekranın sol bölgesinde mi kontrolü
            if (eventData.position.x > Screen.width * 0.55f) return;

            movePointerId = eventData.pointerId;

            // Dokunulan konumu joystick merkezine dönüştür (Floating Joystick)
            if (joystickBackground != null && parentRect != null)
            {
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out localPoint))
                {
                    joystickBackground.anchoredPosition = localPoint;
                }
            }

            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }

            if (joystickCanvasGroup != null)
            {
                joystickCanvasGroup.alpha = 0.90f;
            }

            targetMoveInput = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != movePointerId || joystickBackground == null || joystickHandle == null) return;

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(joystickBackground, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                Vector2 clamped = Vector2.ClampMagnitude(localPoint, joystickRadius);
                joystickHandle.anchoredPosition = clamped;

                Vector2 raw = clamped / joystickRadius;
                float mag = raw.magnitude;

                // Deadzone (0.1f) filtresi ile mikro titremeleri engelle
                if (mag < deadzone)
                {
                    targetMoveInput = Vector2.zero;
                }
                else
                {
                    float normalizedMag = Mathf.Clamp01((mag - deadzone) / (1f - deadzone));
                    targetMoveInput = raw.normalized * normalizedMag;
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ReleaseJoystickPointer(eventData.pointerId);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ReleaseJoystickPointer(eventData.pointerId);
        }

        private void ReleaseJoystickPointer(int pointerId)
        {
            if (pointerId == movePointerId)
            {
                movePointerId = int.MinValue;
                targetMoveInput = Vector2.zero;

                if (joystickHandle != null)
                {
                    joystickHandle.anchoredPosition = Vector2.zero;
                }

                ResetJoystickVisual(true);
            }
        }

        private void ResetJoystickVisual(bool animate)
        {
            if (joystickBackground == null) return;

            if (animate)
            {
                StartCoroutine(FadeAndResetJoystickRoutine());
            }
            else
            {
                joystickBackground.anchoredPosition = defaultJoystickPosition;
                if (joystickCanvasGroup != null) joystickCanvasGroup.alpha = 0f;
            }
        }

        private IEnumerator FadeAndResetJoystickRoutine()
        {
            float elapsed = 0f;
            float duration = 0.25f;
            float startAlpha = (joystickCanvasGroup != null) ? joystickCanvasGroup.alpha : 0.9f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                if (joystickCanvasGroup != null)
                {
                    joystickCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }
                yield return null;
            }

            if (movePointerId == int.MinValue)
            {
                joystickBackground.anchoredPosition = defaultJoystickPosition;
                if (joystickCanvasGroup != null) joystickCanvasGroup.alpha = 0f;
            }
        }
        #endregion

        #region Aksiyon Butonları & Taktil Geri Bildirim
        public void RunStart()
        {
            isSprinting = true;
            if (sprintButton != null) StartCoroutine(PunchButtonScaleRoutine(sprintButton.transform));
            UpdateSprintVisual(true);
        }

        public void RunStop()
        {
            isSprinting = false;
            UpdateSprintVisual(false);
        }

        private void OnActionButtonClicked()
        {
            if (actionButton != null) StartCoroutine(PunchButtonScaleRoutine(actionButton.transform));

            if (interactor != null)
            {
                interactor.TriggerInteraction();
            }
        }

        private void OnCrouchButtonClicked()
        {
            if (crouchButton != null) StartCoroutine(PunchButtonScaleRoutine(crouchButton.transform));
            if (fpsController != null)
            {
                fpsController.ToggleCrouch();
                UpdateCrouchVisual(fpsController.IsCrouching);
            }
        }

        private void OnFlashlightButtonClicked()
        {
            if (flashlightButton != null) StartCoroutine(PunchButtonScaleRoutine(flashlightButton.transform));
            if (flashlight == null) flashlight = FindAnyObjectByType<FlashlightController>();

            Light spot = null;
            var spotGo = GameObject.Find("SpotLight");
            if (spotGo != null) spot = spotGo.GetComponent<Light>();
            if (spot == null) spot = GameObject.Find("Flashlight_Rig")?.GetComponentInChildren<Light>();

            if (flashlight != null)
            {
                flashlight.ToggleFlashlight();
                bool isOn = flashlight.IsOn;
                if (spot != null && spot.enabled != isOn) spot.enabled = isOn;
                UpdateFlashlightVisual(isOn);
            }
            else if (spot != null)
            {
                spot.enabled = !spot.enabled;
                UpdateFlashlightVisual(spot.enabled);
            }
        }

        private void OnFlashlightStateChanged(bool isOn)
        {
            UpdateFlashlightVisual(isOn);
        }

        private void OnInteractionPromptUpdated(string prompt)
        {
            bool hasInteractable = !string.IsNullOrEmpty(prompt);
            SetActionButtonVisible(hasInteractable);

            if (hasInteractable && actionButtonText != null)
            {
                actionButtonText.text = "ETKİLEŞİM";
            }
        }

        private void SetActionButtonVisible(bool visible)
        {
            if (actionButtonCanvasGroup != null)
            {
                actionButtonCanvasGroup.alpha = visible ? 1f : 0f;
                actionButtonCanvasGroup.interactable = visible;
                actionButtonCanvasGroup.blocksRaycasts = visible;
            }
            else if (actionButton != null)
            {
                actionButton.gameObject.SetActive(visible);
            }

            if (visible && actionButtonImage != null)
            {
                actionButtonImage.color = actionActiveColor;
            }
        }

        public void UpdateFlashlightVisual(bool isOn)
        {
            if (flashlightButtonImage != null)
            {
                flashlightButtonImage.color = isOn ? flashlightActiveColor : normalButtonColor;
            }
        }

        public void UpdateCrouchVisual(bool isCrouched)
        {
            if (crouchButtonImage != null)
            {
                crouchButtonImage.color = isCrouched ? crouchActiveColor : normalButtonColor;
            }
        }

        public void UpdateSprintVisual(bool isSprint)
        {
            if (sprintButtonImage != null)
            {
                sprintButtonImage.color = isSprint ? sprintActiveColor : normalButtonColor;
            }
        }

        private IEnumerator PunchButtonScaleRoutine(Transform btnTransform)
        {
            if (btnTransform == null) yield break;

            Vector3 originalScale = Vector3.one;
            Vector3 targetScale = Vector3.one * 0.88f;

            float elapsed = 0f;
            float punchDur = 0.06f;

            while (elapsed < punchDur)
            {
                elapsed += Time.unscaledDeltaTime;
                btnTransform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / punchDur);
                yield return null;
            }

            elapsed = 0f;
            float returnDur = 0.10f;
            while (elapsed < returnDur)
            {
                elapsed += Time.unscaledDeltaTime;
                btnTransform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / returnDur);
                yield return null;
            }

            btnTransform.localScale = originalScale;
        }
        #endregion

        #region Duraklatma (Pause) & Hassasiyet Menüsü
        public void TogglePause()
        {
            SetPaused(!isPaused);
        }

        public void SetPaused(bool paused)
        {
            isPaused = paused;
            if (isPaused)
            {
                ResetAllTouchInput();
                BuildPauseOverlayIfNeeded();
                if (pauseOverlayRoot != null) pauseOverlayRoot.SetActive(true);
                Time.timeScale = 0f;
            }
            else
            {
                if (pauseOverlayRoot != null) pauseOverlayRoot.SetActive(false);
                Time.timeScale = 1f;
            }

            if (MobileControlManager.Instance != null)
            {
                MobileControlManager.Instance.SetPaused(isPaused);
            }
        }

        public void ResumeGame()
        {
            SetPaused(false);
        }

        public void ReturnToMainMenu()
        {
            isPaused = false;
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private void BuildPauseOverlayIfNeeded()
        {
            if (pauseOverlayRoot != null) return;

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            Transform host = parentCanvas != null ? parentCanvas.transform : transform.parent;
            if (host == null) host = transform;

            pauseOverlayRoot = new GameObject("Mobile_Pause_Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pauseOverlayRoot.transform.SetParent(host, false);
            pauseOverlayRoot.transform.SetAsLastSibling();

            RectTransform overlayRt = pauseOverlayRoot.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;

            Image bg = pauseOverlayRoot.GetComponent<Image>();
            bg.color = new Color(0.03f, 0.03f, 0.04f, 0.84f);

            // Orta Panel
            GameObject card = new GameObject("Pause_Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(pauseOverlayRoot.transform, false);
            RectTransform cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(440f, 340f);
            card.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

            // Başlık
            CreateOverlayText(card.transform, "Title", "DURAKLATILDI", 28, new Vector2(0f, 118f), new Vector2(360f, 44f), new Color(0.92f, 0.90f, 0.86f, 1f));

            // Hassasiyet Etiketi
            float currentSens = touchLookZone != null ? touchLookZone.LookSensitivity : PlayerPrefs.GetFloat(TouchLookZone.PREF_LOOK_SENSITIVITY, 0.18f);
            TextMeshProUGUI sensLabel = CreateOverlayText(card.transform, "SensLabel", $"Bakış Hassasiyeti: {Mathf.RoundToInt(currentSens * 100f)}%", 18, new Vector2(0f, 52f), new Vector2(360f, 30f), new Color(0.75f, 0.78f, 0.82f, 1f));

            // Hassasiyet Kaydırıcısı
            Slider slider = CreateOverlaySlider(card.transform, new Vector2(0f, 12f), new Vector2(320f, 28f), 0.05f, 0.50f, currentSens);
            slider.onValueChanged.AddListener((val) =>
            {
                if (touchLookZone == null) touchLookZone = FindAnyObjectByType<TouchLookZone>(FindObjectsInactive.Include);
                if (touchLookZone != null)
                {
                    touchLookZone.SetSensitivity(val);
                }
                else
                {
                    PlayerPrefs.SetFloat(TouchLookZone.PREF_LOOK_SENSITIVITY, val);
                }
                if (sensLabel != null) sensLabel.text = $"Bakış Hassasiyeti: {Mathf.RoundToInt(val * 100f)}%";
            });

            // Devam Et Butonu
            Button resumeBtn = CreateOverlayButton(card.transform, "ResumeBtn", "DEVAM ET", new Vector2(0f, -58f), new Vector2(280f, 48f), new Color(0.18f, 0.24f, 0.30f, 0.95f));
            resumeBtn.onClick.AddListener(ResumeGame);

            // Ana Menü Butonu
            Button menuBtn = CreateOverlayButton(card.transform, "MenuBtn", "ANA MENÜ", new Vector2(0f, -118f), new Vector2(280f, 44f), new Color(0.26f, 0.11f, 0.11f, 0.92f));
            menuBtn.onClick.AddListener(ReturnToMainMenu);
        }

        private TextMeshProUGUI CreateOverlayText(Transform parent, string name, string text, int fontSize, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            return tmp;
        }

        private Button CreateOverlayButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            go.GetComponent<Image>().color = bgColor;
            Button btn = go.GetComponent<Button>();

            CreateOverlayText(go.transform, "Label", label, 18, Vector2.zero, size, Color.white);
            return btn;
        }

        private Slider CreateOverlaySlider(Transform parent, Vector2 pos, Vector2 size, float minVal, float maxVal, float initialVal)
        {
            GameObject root = new GameObject("SensitivitySlider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = pos;
            rootRt.sizeDelta = size;

            GameObject track = new GameObject("Background", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(root.transform, false);
            RectTransform trackRt = track.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0f, 0.35f);
            trackRt.anchorMax = new Vector2(1f, 0.65f);
            trackRt.offsetMin = Vector2.zero;
            trackRt.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.28f, 1f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.35f);
            faRt.anchorMax = new Vector2(1f, 0.65f);
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.75f, 0.60f, 0.28f, 1f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            RectTransform haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = Vector2.zero;
            haRt.offsetMax = Vector2.zero;

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(24f, 24f);
            handle.GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.value = Mathf.Clamp(initialVal, minVal, maxVal);
            return slider;
        }
        #endregion

        public void Configure(FirstPersonController fpc, PlayerInteractor pint, FlashlightController flash,
            RectTransform touchArea, RectTransform bg, RectTransform handle,
            Button btnAction, Button btnSprint, Button btnCrouch, Button btnFlash, Button btnPause = null)
        {
            fpsController = fpc;
            interactor = pint;
            flashlight = flash;
            joystickTouchArea = touchArea;
            joystickBackground = bg;
            joystickHandle = handle;
            actionButton = btnAction;
            sprintButton = btnSprint;
            crouchButton = btnCrouch;
            flashlightButton = btnFlash;
            if (btnPause != null) pauseButton = btnPause;
        }
    }
}
