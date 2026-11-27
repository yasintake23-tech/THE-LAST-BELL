using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheLastBell.Player;
using TheLastBell.Interaction;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Oyun i HUD ve Kullanc Arayz Yneticisi (Player HUD).
    /// Merkez imle (reticle), etkileim bildirimleri, durum mesajlar,
    /// dayankllk (stamina) bar, fener pil gstergesi ve not okuma panelini ynetir.
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        public static PlayerHUD Instance { get; private set; }

        [Header("--- Referanslar ---")]
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private FlashlightController flashlight;

        [Header("--- Merkez mlei (Reticle) ---")]
        [SerializeField] private Image reticleDot;
        [SerializeField] private Color reticleNormalColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color reticleInteractColor = new Color(0.9f, 0.25f, 0.25f, 0.95f);

        [Header("--- Etkileim Bildirimi ---")]
        [SerializeField] private GameObject promptContainer;
        [SerializeField] private TextMeshProUGUI promptText;

        [Header("--- Durum Mesaj Bildirimi ---")]
        [SerializeField] private GameObject statusContainer;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private CanvasGroup statusCanvasGroup;

        [Header("--- Dayankllk (Stamina) Bar ---")]
        [SerializeField] private CanvasGroup staminaCanvasGroup;
        [SerializeField] private Image staminaFillImage;

        [Header("--- El Feneri Pil Gstergesi ---")]
        [SerializeField] private CanvasGroup batteryCanvasGroup;
        [SerializeField] private Image batteryFillImage;

        [Header("--- Not Okuma Paneli ---")]
        [SerializeField] private GameObject notePanelRoot;
        [SerializeField] private TextMeshProUGUI noteTitleText;
        [SerializeField] private TextMeshProUGUI noteContentText;
        [SerializeField] private Button noteCloseButton;

        private Coroutine statusFadeCoroutine;
        private bool isReadingNote = false;

        public bool IsReadingNote => isReadingNote;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            if (promptContainer != null) promptContainer.SetActive(false);
            if (statusContainer != null) statusContainer.SetActive(false);
            if (notePanelRoot != null) notePanelRoot.SetActive(false);

            if (noteCloseButton != null)
            {
                noteCloseButton.onClick.AddListener(CloseNoteUI);
            }
        }

        private void Start()
        {
            if (playerInteractor != null)
            {
                playerInteractor.OnPromptUpdated += SetPrompt;
                playerInteractor.OnStatusMessageTriggered += ShowStatusMessage;
            }

            if (playerController != null)
            {
                playerController.OnStaminaChanged += UpdateStamina;
            }

            if (flashlight != null)
            {
                flashlight.OnBatteryChanged += UpdateBattery;
            }
        }

        private void Update()
        {
            // Not okurken 'E', ESC veya bolukla kapatma
            if (isReadingNote)
            {
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
                {
                    CloseNoteUI();
                }
            }
        }

        public void SetPrompt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (promptContainer != null) promptContainer.SetActive(false);
                if (reticleDot != null)
                {
                    reticleDot.color = reticleNormalColor;
                    reticleDot.transform.localScale = Vector3.one;
                }
            }
            else
            {
                if (promptContainer != null) promptContainer.SetActive(true);
                if (promptText != null) promptText.text = text;
                if (reticleDot != null)
                {
                    reticleDot.color = reticleInteractColor;
                    reticleDot.transform.localScale = Vector3.one * 1.35f;
                }
            }
        }

        public void ShowStatusMessage(string message)
        {
            if (statusFadeCoroutine != null) StopCoroutine(statusFadeCoroutine);
            statusFadeCoroutine = StartCoroutine(StatusMessageRoutine(message));
        }

        private IEnumerator StatusMessageRoutine(string msg)
        {
            if (statusContainer != null) statusContainer.SetActive(true);
            if (statusText != null) statusText.text = msg;
            if (statusCanvasGroup != null) statusCanvasGroup.alpha = 1f;

            yield return new WaitForSeconds(2.4f);

            float elapsed = 0f;
            float fadeDur = 0.6f;
            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                if (statusCanvasGroup != null)
                {
                    statusCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDur);
                }
                yield return null;
            }

            if (statusContainer != null) statusContainer.SetActive(false);
        }

        private void UpdateStamina(float percent)
        {
            if (staminaFillImage != null)
            {
                staminaFillImage.fillAmount = percent;
            }

            // Stamina tam doluyken arayz sade tutmak iin yava yava gizle (alpha = 0)
            if (staminaCanvasGroup != null)
            {
                float targetAlpha = (percent >= 0.99f) ? 0f : 0.85f;
                staminaCanvasGroup.alpha = Mathf.MoveTowards(staminaCanvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
            }
        }

        private void UpdateBattery(float percent)
        {
            if (batteryFillImage != null)
            {
                batteryFillImage.fillAmount = percent;
            }
        }

        public void ShowNoteUI(string title, string content)
        {
            isReadingNote = true;
            if (notePanelRoot != null) notePanelRoot.SetActive(true);
            if (noteTitleText != null) noteTitleText.text = title;
            if (noteContentText != null) noteContentText.text = content;

            // Not okurken imleci gster
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseNoteUI()
        {
            isReadingNote = false;
            if (notePanelRoot != null) notePanelRoot.SetActive(false);

            // Oyuna dn: mlei tekrar kilitle
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Configure(
            FirstPersonController fpc, PlayerInteractor interactor, FlashlightController flash,
            Image reticle, GameObject promptObj, TextMeshProUGUI promptTmp,
            GameObject statusObj, TextMeshProUGUI statusTmp, CanvasGroup statusCg,
            CanvasGroup stamCg, Image stamFill,
            GameObject noteRoot, TextMeshProUGUI noteTitleTmp, TextMeshProUGUI noteBodyTmp, Button noteBtn)
        {
            playerController = fpc;
            playerInteractor = interactor;
            flashlight = flash;
            reticleDot = reticle;
            promptContainer = promptObj;
            promptText = promptTmp;
            statusContainer = statusObj;
            statusText = statusTmp;
            statusCanvasGroup = statusCg;
            staminaCanvasGroup = stamCg;
            staminaFillImage = stamFill;
            notePanelRoot = noteRoot;
            noteTitleText = noteTitleTmp;
            noteContentText = noteBodyTmp;
            noteCloseButton = noteBtn;
        }
    }
}
