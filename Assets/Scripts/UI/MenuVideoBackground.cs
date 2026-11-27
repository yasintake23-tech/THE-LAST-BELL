using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Sinematik Video Arka Plan Yöneticisi.
    /// Siyah ekran oluşmasını önler, pürüzsüz geçiş (fade-in), otomatik döngü (loop)
    /// ve Android/PC yaşam döngüsü (pause/resume) uyumluluğu sağlar.
    /// </summary>
    public class MenuVideoBackground : MonoBehaviour
    {
        [Header("Bileşenler")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoRawImage;
        [SerializeField] private CanvasGroup videoCanvasGroup;
        [SerializeField] private GameObject fallbackBackground;

        [Header("Ayarlar")]
        [SerializeField] private float fadeInDuration = 0.6f;
        [SerializeField] private bool maintain16x9 = true;

        private Coroutine fadeCoroutine;

        private void Awake()
        {
            if (videoCanvasGroup != null)
            {
                videoCanvasGroup.alpha = 0f;
            }

            if (fallbackBackground != null)
            {
                fallbackBackground.SetActive(true);
            }

            SetupAspectRatio();
        }

        private void Start()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
            }

            if (videoPlayer != null)
            {
                // Sesi tamamen devre dışı bırak (Video sesi çalınmaz, ayrılmış müzik/SFX sistemine hazır)
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                for (ushort i = 0; i < videoPlayer.controlledAudioTrackCount; i++)
                {
                    videoPlayer.EnableAudioTrack(i, false);
                    videoPlayer.SetDirectAudioMute(i, true);
                }
                videoPlayer.isLooping = true;
                videoPlayer.skipOnDrop = true;

                // Olay dinleyicileri
                videoPlayer.prepareCompleted += OnVideoPrepared;
                videoPlayer.errorReceived += OnVideoError;

                // Videoyu hazırla
                videoPlayer.Prepare();
            }
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.errorReceived -= OnVideoError;
            }
        }

        private void SetupAspectRatio()
        {
            if (!maintain16x9 || videoRawImage == null) return;

            AspectRatioFitter fitter = videoRawImage.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = videoRawImage.gameObject.AddComponent<AspectRatioFitter>();
            }

            fitter.aspectRatio = 16f / 9f; // 1.777778f
            // EnvelopeParent: 18:9, 19.5:9 veya 21:9 gibi farklı ekranlarda siyah bant bırakmadan tam ekran doldurur
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            source.Play();

            // İlk kare hazır olunca pürüzsüz fade-in
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeInVideo());
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[THE LAST BELL] Video yükleme uyarısı: {message}. Yedek karanlık arka plan devrede.");
            if (fallbackBackground != null)
            {
                fallbackBackground.SetActive(true);
            }
        }

        private IEnumerator FadeInVideo()
        {
            if (videoCanvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                videoCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            videoCanvasGroup.alpha = 1f;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // Android mobil cihazlarda uygulama arka plana atıldığında / geri gelindiğinde
            if (videoPlayer != null && videoPlayer.isPrepared)
            {
                if (pauseStatus)
                {
                    videoPlayer.Pause();
                }
                else
                {
                    videoPlayer.Play();
                }
            }
        }
    }
}
