using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TheLastBell.Cinematics
{
    /// <summary>
    /// THE LAST BELL - Yeni Açılış Sinematiği ve Başlık Ekranı Yöneticisi.
    /// Akış:
    /// 1. Açılış videosu otomatik ve kesintisiz başlar (en-boy oranı korunur, ekranda yazı/buton yoktur).
    /// 2. Video bittiğinde ~0.8 saniyede yumuşakça siyaha geçer.
    /// 3. Geçiş anında "Dı... dı... dı... dııııınnn..." korku tınısı başlar.
    /// 4. THE LAST BELL başlık görseli ~1.2 saniyede yavaşça belirir.
    /// 5. Derin son çan/piyano notası ve yankısı eşliğinde görsel ekranda ~5 saniye kalır.
    /// 6. Siyaha sönüşün ardından oyunun başlangıç sahnesine (AbandonedSchool) geçilir.
    /// </summary>
    public class OpeningCinematicManager : MonoBehaviour
    {
        [Header("--- Video Oynatıcı ---")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoDisplay;
        [SerializeField] private CanvasGroup videoCanvasGroup;

        [Header("--- Başlık Görseli ---")]
        [SerializeField] private Image titleDisplayImage;
        [SerializeField] private CanvasGroup titleCanvasGroup;
        [SerializeField] private RectTransform titleRectTransform;

        [Header("--- Siyah Geçiş Katmanı ---")]
        [SerializeField] private CanvasGroup blackFadeOverlay;

        [Header("--- 4 Saniye Basılı Tutarak Atlama (Hold to Skip) ---")]
        [SerializeField] private CanvasGroup skipCanvasGroup;
        [SerializeField] private Image skipRadialFillImage;
        [SerializeField] private TMPro.TextMeshProUGUI skipPromptText;
        [SerializeField] private float requiredHoldDuration = 4.0f;
        [SerializeField] private float skipFadeSpeed = 4.0f;

        [Header("--- Korku Sesi ---")]
        [SerializeField] private AudioSource horrorAudioSource;
        [SerializeField] private AudioClip horrorJingleClip;

        [Header("--- Sahne Geçişi ve Süreler ---")]
        [SerializeField] private string nextSceneName = "AbandonedSchool";
        [SerializeField] private float videoFadeOutDuration = 0.8f;
        [SerializeField] private float titleFadeInDuration = 1.2f;
        [SerializeField] private float titleDisplayDuration = 5.0f;
        [SerializeField] private float titleFadeOutDuration = 1.2f;

        [Header("--- Teşhis ve Zaman Aşımı ---")]
        [SerializeField] private string streamingAssetsFileName = "OpeningCinematicVideo.mp4";
        [SerializeField] private float prepareTimeoutSeconds = 8.0f;

        private bool isTransitioning = false;
        private bool isVideoStarted = false;
        private float holdTimer = 0f;
        private bool eventsSubscribed = false;
        private RenderTexture runtimeRT;

        private void Awake()
        {
            // Başlangıç alfa durumları
            if (blackFadeOverlay != null) blackFadeOverlay.alpha = 0f;
            if (titleCanvasGroup != null) titleCanvasGroup.alpha = 0f;
            if (videoCanvasGroup != null) videoCanvasGroup.alpha = 1f;
            if (skipCanvasGroup != null) skipCanvasGroup.alpha = 0f;
            if (skipRadialFillImage != null) skipRadialFillImage.fillAmount = 0f;

            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (horrorAudioSource == null)
            {
                var jingleT = transform.Find("HorrorJingle_AudioSource");
                if (jingleT != null) horrorAudioSource = jingleT.GetComponent<AudioSource>();
            }

            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = false;
                videoPlayer.waitForFirstFrame = true;
                videoPlayer.skipOnDrop = true;

                // Video ses çıkışını garanti altına al
                var videoAudio = GetComponent<AudioSource>();
                if (videoAudio != null && videoAudio != horrorAudioSource)
                {
                    videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                    videoPlayer.SetTargetAudioSource(0, videoAudio);
                    videoPlayer.EnableAudioTrack(0, true);
                    videoPlayer.SetDirectAudioMute(0, false);
                    videoPlayer.SetDirectAudioVolume(0, 1.0f);
                }
                else
                {
                    videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                    videoPlayer.EnableAudioTrack(0, true);
                    videoPlayer.SetDirectAudioMute(0, false);
                    videoPlayer.SetDirectAudioVolume(0, 1.0f);
                }

                EnsureRenderTexture();
                SubscribeEvents();
            }
        }

        private void Start()
        {
            if (videoPlayer != null)
            {
                if (videoPlayer.clip == null && string.IsNullOrEmpty(videoPlayer.url) && !string.IsNullOrEmpty(streamingAssetsFileName))
                {
                    videoPlayer.source = VideoSource.Url;
                    videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);
                    Debug.Log($"[OpeningCinematic] VideoClip null -> StreamingAssets URL kullanılıyor: {videoPlayer.url}");
                }

                if (videoPlayer.clip == null && string.IsNullOrEmpty(videoPlayer.url))
                {
                    Debug.LogError("[OpeningCinematic] VideoClip veya URL atanmamış!");
                    StartTransitionToTitle();
                    return;
                }

                Debug.Log($"[OpeningCinematic] Hazırlanıyor -> Source={videoPlayer.source}, Clip={videoPlayer.clip?.name}, Url={videoPlayer.url}, RenderMode={videoPlayer.renderMode}, TargetRT={videoPlayer.targetTexture?.name}, AudioMode={videoPlayer.audioOutputMode}, GraphicsAPI={SystemInfo.graphicsDeviceType}");
                videoPlayer.Stop();
                videoPlayer.Prepare();
                StartCoroutine(PrepareWatchdogRoutine());
            }
            else
            {
                Debug.LogError("[THE LAST BELL] VideoPlayer bileşeni bulunamadı!");
                StartTransitionToTitle();
            }
        }

        private void Update()
        {
            // Video oynatma takibi (yalnızca aktif oynatma sırasında ve son karelere ulaşıldığında)
            if (videoPlayer != null && isVideoStarted && !isTransitioning)
            {
                if (videoPlayer.isPlaying && videoPlayer.frameCount > 10 && videoPlayer.frame >= (long)(videoPlayer.frameCount - 2))
                {
                    OnVideoFinished(videoPlayer);
                }
            }

            // 4 Saniye Basılı Tutarak Atlama (Hold to Skip) Mantığı
            if (Application.isMobilePlatform)
            {
                if (skipCanvasGroup != null) skipCanvasGroup.alpha = 0f;
            }
            else HandleHoldToSkip();
        }

        private void HandleHoldToSkip()
        {
            if (isTransitioning)
            {
                if (skipCanvasGroup != null && skipCanvasGroup.alpha > 0f)
                {
                    skipCanvasGroup.alpha = Mathf.MoveTowards(skipCanvasGroup.alpha, 0f, Time.deltaTime * skipFadeSpeed * 2f);
                }
                return;
            }

            // Dokunmatik dokunma, fare sol tıkı veya Space tuşuna basılı tutuluyor mu?
            bool isPressing = Input.GetKey(KeyCode.Space) ||
                              Input.GetMouseButton(0) ||
                              (Input.touchCount > 0 && Input.GetTouch(0).phase != TouchPhase.Ended && Input.GetTouch(0).phase != TouchPhase.Canceled);

            if (isPressing)
            {
                holdTimer += Time.deltaTime;

                // Çemberi görünür yap
                if (skipCanvasGroup != null)
                {
                    skipCanvasGroup.alpha = Mathf.MoveTowards(skipCanvasGroup.alpha, 1f, Time.deltaTime * skipFadeSpeed);
                }

                // Radyal dolumu güncelle
                if (skipRadialFillImage != null)
                {
                    skipRadialFillImage.fillAmount = Mathf.Clamp01(holdTimer / requiredHoldDuration);
                }

                // 4 saniye dolduysa atla!
                if (holdTimer >= requiredHoldDuration)
                {
                    Debug.Log("[THE LAST BELL] 4 saniye basılı tutuldu! Video durduruluyor, başlık ekranına geçiliyor...");
                    if (skipCanvasGroup != null) skipCanvasGroup.alpha = 0f;
                    if (videoPlayer != null && videoPlayer.isPlaying)
                    {
                        videoPlayer.Stop();
                    }
                    StartTransitionToTitle();
                }
            }
            else
            {
                // Basma bırakıldıysa sıfırla ve çemberi yavaşça kaybet
                if (holdTimer > 0f)
                {
                    holdTimer = 0f;
                    if (skipRadialFillImage != null)
                    {
                        skipRadialFillImage.fillAmount = 0f;
                    }
                }

                if (skipCanvasGroup != null && skipCanvasGroup.alpha > 0f)
                {
                    skipCanvasGroup.alpha = Mathf.MoveTowards(skipCanvasGroup.alpha, 0f, Time.deltaTime * skipFadeSpeed);
                }
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();

            if (runtimeRT != null)
            {
                if (videoPlayer != null && videoPlayer.targetTexture == runtimeRT)
                {
                    videoPlayer.targetTexture = null;
                }
                runtimeRT.Release();
                Destroy(runtimeRT);
                runtimeRT = null;
            }
        }

        private void SubscribeEvents()
        {
            if (eventsSubscribed || videoPlayer == null) return;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.started += OnVideoStarted;
            videoPlayer.loopPointReached += OnVideoFinished;
            videoPlayer.errorReceived += OnVideoError;
            eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!eventsSubscribed || videoPlayer == null) return;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.started -= OnVideoStarted;
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
            eventsSubscribed = false;
        }

        private void EnsureRenderTexture()
        {
            if (videoPlayer == null) return;

            RenderTexture target = videoPlayer.targetTexture;
            if (target == null)
            {
                int w = videoPlayer.clip != null && videoPlayer.clip.width > 0 ? (int)videoPlayer.clip.width : 1280;
                int h = videoPlayer.clip != null && videoPlayer.clip.height > 0 ? (int)videoPlayer.clip.height : 720;
                runtimeRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
                {
                    name = "OpeningCinematic_RuntimeRT",
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0
                };
                target = runtimeRT;
            }
            else
            {
                target.anisoLevel = 0;
            }

            if (!target.IsCreated())
            {
                target.Create();
            }

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = prev;

            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = target;

            if (videoDisplay != null)
            {
                videoDisplay.texture = target;
                videoDisplay.color = Color.white;
                videoDisplay.enabled = true;
            }
        }

        private IEnumerator PrepareWatchdogRoutine()
        {
            float elapsed = 0f;
            while (!isVideoStarted && !isTransitioning && elapsed < prepareTimeoutSeconds)
            {
                if (videoPlayer != null && videoPlayer.isPrepared)
                {
                    EnsureRenderTexture();
                    if (!videoPlayer.isPlaying)
                    {
                        videoPlayer.Play();
                    }
                    isVideoStarted = true;
                    Debug.Log("[OpeningCinematic] Watchdog videoyu başlattı.");
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!isVideoStarted && !isTransitioning)
            {
                Debug.LogError($"[OpeningCinematic] Video {prepareTimeoutSeconds:F1}s içinde hazırlanamadı (isPrepared={videoPlayer?.isPrepared}). Başlık ekranına geçiliyor.");
                StartTransitionToTitle();
            }
        }

        private void OnVideoPrepared(VideoPlayer vp)
        {
            EnsureRenderTexture();
            vp.Play();
            isVideoStarted = true;
            Debug.Log($"[THE LAST BELL] Açılış videosu başarıyla başladı. Boyut={vp.width}x{vp.height}, Süre={vp.length:F2}s");
        }

        private void OnVideoStarted(VideoPlayer vp)
        {
            isVideoStarted = true;
        }

        private void OnVideoFinished(VideoPlayer vp)
        {
            if (isTransitioning) return;
            Debug.Log("[THE LAST BELL] Açılış videosu doğal olarak sonlandı. Başlık görseline geçiliyor...");
            StartTransitionToTitle();
        }

        private void OnVideoError(VideoPlayer vp, string message)
        {
            Debug.LogError($"[THE LAST BELL] VideoPlayer hatası: {message} (Clip={vp?.clip?.name}, GraphicsAPI={SystemInfo.graphicsDeviceType})");
            if (!isTransitioning)
            {
                StartTransitionToTitle();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (isTransitioning || videoPlayer == null || !videoPlayer.isPrepared) return;
            if (pauseStatus) videoPlayer.Pause();
            else videoPlayer.Play();
        }

        private void StartTransitionToTitle()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            StartCoroutine(CinematicFlowCoroutine());
        }

        private IEnumerator CinematicFlowCoroutine()
        {
            // 1. ADIM: Videoyu durdur ve siyaha yumuşak geçiş yap (~0.8 saniye)
            float elapsed = 0f;
            while (elapsed < videoFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / videoFadeOutDuration);
                if (blackFadeOverlay != null) blackFadeOverlay.alpha = t;
                yield return null;
            }
            if (blackFadeOverlay != null) blackFadeOverlay.alpha = 1f;

            // Video oynatıcıyı temizle
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }
            if (videoCanvasGroup != null)
            {
                videoCanvasGroup.alpha = 0f;
            }

            // 2. ADIM: Korku sesini başlat ("Dı... dı... dı... dııııınnn...")
            // İlk 4 nota siyaha geçiş ve başlığın ilk belirme anında çalar;
            // Derin yankılı son çan/piyano notası THE LAST BELL yazısı tam görünürken patlar.
            if (horrorAudioSource != null && horrorJingleClip != null)
            {
                horrorAudioSource.clip = horrorJingleClip;
                horrorAudioSource.volume = 0.95f;
                horrorAudioSource.Play();
            }

            yield return new WaitForSeconds(0.2f);

            // 3. ADIM: Başlık Görselini Yavaşça Ortaya Çıkar (~1.2 saniye)
            elapsed = 0f;
            Vector3 startScale = new Vector3(0.985f, 0.985f, 1f);
            Vector3 endScale = Vector3.one;

            while (elapsed < titleFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / titleFadeInDuration);

                // Siyah katmanı aç, başlığı parlat
                if (blackFadeOverlay != null) blackFadeOverlay.alpha = 1f - t;
                if (titleCanvasGroup != null) titleCanvasGroup.alpha = t;

                if (titleRectTransform != null)
                {
                    titleRectTransform.localScale = Vector3.Lerp(startScale, endScale, t);
                }

                yield return null;
            }

            if (blackFadeOverlay != null) blackFadeOverlay.alpha = 0f;
            if (titleCanvasGroup != null) titleCanvasGroup.alpha = 1f;
            if (titleRectTransform != null) titleRectTransform.localScale = endScale;

            // 4. ADIM: Başlık Görseli Ekranda Kalır (~5 saniye)
            // Çok hafif, sinematik yavaş yakınlaşma efekti (Slow Creep: 1.0 -> 1.03)
            elapsed = 0f;
            Vector3 holdStartScale = Vector3.one;
            Vector3 holdEndScale = new Vector3(1.03f, 1.03f, 1f);

            while (elapsed < titleDisplayDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / titleDisplayDuration;

                if (titleRectTransform != null)
                {
                    titleRectTransform.localScale = Vector3.Lerp(holdStartScale, holdEndScale, t);
                }

                yield return null;
            }

            // 5. ADIM: Başlık Görseli Siyaha Doğru Yavaşça Kaybolur (~1.2 saniye)
            elapsed = 0f;
            while (elapsed < titleFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / titleFadeOutDuration);
                if (blackFadeOverlay != null) blackFadeOverlay.alpha = t;
                yield return null;
            }
            if (blackFadeOverlay != null) blackFadeOverlay.alpha = 1f;

            yield return new WaitForSeconds(0.5f);

            // 6. ADIM: Sonraki Sahneye Geçiş (AbandonedSchool)
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                Debug.Log("[THE LAST BELL] Açılış sinematiği tamamlandı. Oyun sahnesine geçiliyor: " + nextSceneName);
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                Debug.LogError("[THE LAST BELL] Hedef sahne yüklenemedi: " + nextSceneName);
            }
        }

        public void ConfigureSkipUI(CanvasGroup cg, Image fillImg, TMPro.TextMeshProUGUI text)
        {
            skipCanvasGroup = cg;
            skipRadialFillImage = fillImg;
            skipPromptText = text;
        }

        /// <summary>
        /// Sahne oluşturucu tarafından bileşenlerin temiz ve güvenli bağlanması için.
        /// </summary>
        public void ConfigureComponents(
            VideoPlayer vp, RawImage vDisplay, CanvasGroup vCg,
            Image tDisplay, CanvasGroup tCg, RectTransform tRect,
            CanvasGroup blackFade, AudioSource audioSrc, AudioClip jingleClip,
            string targetScene)
        {
            videoPlayer = vp;
            videoDisplay = vDisplay;
            videoCanvasGroup = vCg;
            titleDisplayImage = tDisplay;
            titleCanvasGroup = tCg;
            titleRectTransform = tRect;
            blackFadeOverlay = blackFade;
            horrorAudioSource = audioSrc;
            horrorJingleClip = jingleClip;
            nextSceneName = targetScene;
        }
    }
}
