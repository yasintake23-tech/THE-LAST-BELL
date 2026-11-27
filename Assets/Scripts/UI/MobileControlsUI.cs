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
    public class MobileControlsUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
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

        [Header("--- Renk Temaları (Modern Korku Füme & Parlama) ---")]
        // Normal durum: Koyu yarı saydam füme (#141418, Alfa: %60)
        [SerializeField] private Color normalButtonColor = new Color(0.078f, 0.078f, 0.094f, 0.60f);
        // Fener açıkken: Hafif altın sarısı/kehribar iç parlama (#E5B842, Alfa: %75)
        [SerializeField] private Color flashlightActiveColor = new Color(0.898f, 0.722f, 0.259f, 0.75f);
        // Çömelirken: Sıcak kehribar/turuncu (#F59433, Alfa: %80)
        [SerializeField] private Color crouchActiveColor = new Color(0.96f, 0.58f, 0.20f, 0.80f);
        // Koşarken: Hafif buz mavisi/beyaz parlama (#99D9F2, Alfa: %85)
        [SerializeField] private Color sprintActiveColor = new Color(0.60f, 0.85f, 0.95f, 0.85f);
        // Etkileşim aktifken: Zümrüt yeşili (#33E573, Alfa: %85)
        [SerializeField] private Color actionActiveColor = new Color(0.20f, 0.90f, 0.45f, 0.85f);

        private int movePointerId = -1;
        private Vector2 targetMoveInput = Vector2.zero;
        private Vector2 currentMoveInput = Vector2.zero;
        private bool isSprinting = false;
        private RectTransform parentRect;

        private void Awake()
        {
            Instance = this;
            parentRect = transform.parent as RectTransform ?? GetComponent<RectTransform>();

            if (fpsController == null) fpsController = FindAnyObjectByType<FirstPersonController>();
            if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
            if (flashlight == null) flashlight = FindAnyObjectByType<FlashlightController>();

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

            // Başlangıçta joystick gizli/şeffaf olsun (Sadece dokunulduğunda parmağın altında belirsin)
            ResetJoystickVisual(false);

            // Başlangıçta etkileşim butonu gizli olsun (nesneye yaklaşınca belirecek)
            SetActionButtonVisible(false);
        }

        private void Start()
        {
            // Fener ve Çömel buton tıklamalarını bağla
            if (actionButton != null) actionButton.onClick.AddListener(OnActionButtonClicked);
            if (crouchButton != null) crouchButton.onClick.AddListener(OnCrouchButtonClicked);
            if (flashlightButton != null) flashlightButton.onClick.AddListener(OnFlashlightButtonClicked);

            // Koşma butonu: Basılı tutulduğunda koş (PointerDown = RunStart, PointerUp = RunStop)
            SetupSprintButtonHold();

            // Etkileşim olayını bağla
            if (interactor != null)
            {
                interactor.OnPromptUpdated += OnInteractionPromptUpdated;
            }

            // Fener durumunu senkronize et
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

            // Başlangıç buton renklerini tazele
            UpdateCrouchVisual(fpsController != null && fpsController.IsCrouching);
            UpdateSprintVisual(isSprinting);
        }

        private void SetupSprintButtonHold()
        {
            if (sprintButton == null) return;

            var holdBtn = sprintButton.GetComponent<MobileHoldButton>();
            if (holdBtn == null) holdBtn = sprintButton.gameObject.AddComponent<MobileHoldButton>();

            holdBtn.onPointerDown = RunStart;
            holdBtn.onPointerUp = RunStop;

            // EventTrigger yedek olarak da bağla
            var trigger = sprintButton.GetComponent<EventTrigger>();
            if (trigger == null) trigger = sprintButton.gameObject.AddComponent<EventTrigger>();

            trigger.triggers.Clear();

            var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            downEntry.callback.AddListener((data) => { RunStart(); });
            trigger.triggers.Add(downEntry);

            var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            upEntry.callback.AddListener((data) => { RunStop(); });
            trigger.triggers.Add(upEntry);
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
        }

        private void Update()
        {
            // Yumuşak girdi sönümlemesi (Lerp)
            currentMoveInput = Vector2.Lerp(currentMoveInput, targetMoveInput, Time.deltaTime * inputLerpSpeed);
            if (targetMoveInput == Vector2.zero && currentMoveInput.sqrMagnitude < 0.0001f)
            {
                currentMoveInput = Vector2.zero;
            }

            // Karakter kontrolcüsüne aktar
            if (fpsController != null && fpsController.enabled)
            {
                fpsController.MobileMoveInput = currentMoveInput;
                fpsController.MobileSprintInput = isSprinting;
            }
        }

        #region Dinamik Yüzen (Floating) Joystick Olayları
        public void OnPointerDown(PointerEventData eventData)
        {
            // Yalnızca ilk temas eden parmak joystick'e bağlanır
            if (movePointerId != -1) return;

            // Dokunulan nokta ekranın sol yarısında mı kontrolü (Screen.width * 0.5f)
            if (eventData.position.x > Screen.width * 0.5f) return;

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
            if (eventData.pointerId == movePointerId)
            {
                movePointerId = -1;
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
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (joystickCanvasGroup != null)
                {
                    joystickCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }
                yield return null;
            }

            if (movePointerId == -1)
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

        public void Configure(FirstPersonController fpc, PlayerInteractor pint, FlashlightController flash,
            RectTransform touchArea, RectTransform bg, RectTransform handle,
            Button btnAction, Button btnSprint, Button btnCrouch, Button btnFlash)
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
        }
    }
}
