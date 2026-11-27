using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Profesyonel Ana Menü Yöneticisi
    /// Android mobil ve PC platformları için optimize edilmiştir.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("--- Ana Menü Düğmeleri ---")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button aboutButton;
        [SerializeField] private Button exitButton;

        [Header("--- Paneller ---")]
        [SerializeField] private GameObject mainButtonsContainer;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject aboutPanel;

        [Header("--- Panel Kapatma Düğmeleri ---")]
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button controlsCloseButton;
        [SerializeField] private Button aboutCloseButton;

        [Header("--- Ayarlar Bileşenleri ---")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private TMP_Dropdown qualityDropdown;
        [SerializeField] private TMP_Dropdown fpsDropdown;
        [SerializeField] private Toggle hapticsToggle;

        [Header("--- Sahneler ---")]
        [SerializeField] private string openingCinematicSceneName = "OpeningCinematic";
        [SerializeField] private string gameplaySceneName = "AbandonedSchool";

        private const string PREF_HAS_SAVE = "TLB_HasSavedGame";
        private const string PREF_MASTER_VOL = "TLB_MasterVolume";
        private const string PREF_MUSIC_VOL = "TLB_MusicVolume";
        private const string PREF_SFX_VOL = "TLB_SFXVolume";
        private const string PREF_QUALITY = "TLB_QualityLevel";
        private const string PREF_FPS = "TLB_TargetFPS";
        private const string PREF_HAPTICS = "TLB_Haptics";

        private void Awake()
        {
            // Mobil cihazlarda ekranın kapanmasını engelle
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Yatay mod optimizasyonu
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
        }

        private void Start()
        {
            SetupButtonListeners();
            LoadSettings();
            CheckSaveData();
            CloseAllPanels();
        }

        private void SetupButtonListeners()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
            if (controlsButton != null) controlsButton.onClick.AddListener(() => OpenPanel(controlsPanel));
            if (aboutButton != null) aboutButton.onClick.AddListener(() => OpenPanel(aboutPanel));
            if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);

            if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(CloseAllPanels);
            if (controlsCloseButton != null) controlsCloseButton.onClick.AddListener(CloseAllPanels);
            if (aboutCloseButton != null) aboutCloseButton.onClick.AddListener(CloseAllPanels);

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
            if (qualityDropdown != null) qualityDropdown.onValueChanged.AddListener(SetQuality);
            if (fpsDropdown != null) fpsDropdown.onValueChanged.AddListener(SetFPS);
            if (hapticsToggle != null) hapticsToggle.onValueChanged.AddListener(SetHaptics);
        }

        public void CheckSaveData()
        {
            bool hasSave = PlayerPrefs.GetInt(PREF_HAS_SAVE, 0) == 1;
            if (continueButton != null)
            {
                continueButton.interactable = hasSave;
                CanvasGroup cg = continueButton.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = continueButton.gameObject.AddComponent<CanvasGroup>();
                }
                cg.alpha = hasSave ? 1.0f : 0.4f;
                cg.blocksRaycasts = hasSave;
            }
        }

        public void OpenPanel(GameObject panel)
        {
            if (panel == null) return;

            if (mainButtonsContainer != null) mainButtonsContainer.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (aboutPanel != null) aboutPanel.SetActive(false);

            panel.SetActive(true);

            // Panel açılış efekti
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeCanvasGroup(cg, 0f, 1f, 0.25f));
            }
        }

        public void CloseAllPanels()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (aboutPanel != null) aboutPanel.SetActive(false);

            if (mainButtonsContainer != null)
            {
                mainButtonsContainer.SetActive(true);
                CanvasGroup cg = mainButtonsContainer.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    StopAllCoroutines();
                    StartCoroutine(FadeCanvasGroup(cg, 0f, 1f, 0.2f));
                }
            }
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end, float duration)
        {
            float elapsed = 0f;
            cg.alpha = start;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            cg.alpha = end;
        }

        public void OnNewGameClicked()
        {
            Debug.Log("[THE LAST BELL] Yeni Oyun başlatılıyor...");
            // Yeni oyunda kayıt bayrağını oluştur
            PlayerPrefs.SetInt(PREF_HAS_SAVE, 1);
            PlayerPrefs.Save();

            // Yeni oyunda açılış sinematiği varsa başlat, yoksa doğrudan oyuna geç
            if (Application.CanStreamedLevelBeLoaded(openingCinematicSceneName))
            {
                SceneManager.LoadScene(openingCinematicSceneName);
            }
            else if (Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
            else
            {
                Debug.LogWarning($"[THE LAST BELL] Sahne bulunamadı. Ana menü aktif kalıyor.");
            }
        }

        public void OnContinueClicked()
        {
            Debug.Log("[THE LAST BELL] Kayıtlı oyun yükleniyor...");
            if (Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
        }

        public void OnExitClicked()
        {
            Debug.Log("[THE LAST BELL] Oyundan çıkılıyor...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #region Ayarlar Metotları

        private void LoadSettings()
        {
            float masterVol = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 0.8f);
            float musicVol = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.7f);
            float sfxVol = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.9f);
            int quality = PlayerPrefs.GetInt(PREF_QUALITY, QualitySettings.GetQualityLevel());
            int fpsIndex = PlayerPrefs.GetInt(PREF_FPS, 1); // 1 = 60 FPS
            bool haptics = PlayerPrefs.GetInt(PREF_HAPTICS, 1) == 1;

            if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;
            if (musicVolumeSlider != null) musicVolumeSlider.value = musicVol;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVol;
            if (qualityDropdown != null) qualityDropdown.value = quality;
            if (fpsDropdown != null) fpsDropdown.value = fpsIndex;
            if (hapticsToggle != null) hapticsToggle.isOn = haptics;

            ApplyFPS(fpsIndex);
        }

        public void SetMasterVolume(float val)
        {
            AudioListener.volume = val;
            PlayerPrefs.SetFloat(PREF_MASTER_VOL, val);
            PlayerPrefs.Save();
        }

        public void SetMusicVolume(float val)
        {
            PlayerPrefs.SetFloat(PREF_MUSIC_VOL, val);
            PlayerPrefs.Save();
        }

        public void SetSFXVolume(float val)
        {
            PlayerPrefs.SetFloat(PREF_SFX_VOL, val);
            PlayerPrefs.Save();
        }

        public void SetQuality(int index)
        {
            QualitySettings.SetQualityLevel(index, true);
            PlayerPrefs.SetInt(PREF_QUALITY, index);
            PlayerPrefs.Save();
        }

        public void SetFPS(int index)
        {
            ApplyFPS(index);
            PlayerPrefs.SetInt(PREF_FPS, index);
            PlayerPrefs.Save();
        }

        private void ApplyFPS(int index)
        {
            switch (index)
            {
                case 0: Application.targetFrameRate = 30; break;
                case 1: Application.targetFrameRate = 60; break;
                case 2: Application.targetFrameRate = 120; break;
                default: Application.targetFrameRate = -1; break;
            }
        }

        public void SetHaptics(bool enabled)
        {
            PlayerPrefs.SetInt(PREF_HAPTICS, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        #endregion
    }
}
