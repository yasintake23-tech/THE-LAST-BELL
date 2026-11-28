using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Sinematik Video Arka Plan Yöneticisi.
    /// Android & PC ortamlarında PlayOnAwake yarış durumlarını önler, RenderTexture'ı garanti altına alır,
    /// pürüzsüz fade-in, otomatik döngü (loop) ve yaşam döngüsü (pause/resume) uyumluluğu sağlar.
    /// </summary>
    public class MenuVideoBackground : MonoBehaviour
    {
        [Header("Bileşenler")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoRawImage;
        [SerializeField] private CanvasGroup videoCanvasGroup;
        [SerializeField] private GameObject fallbackBackground;

        [Header("Ayarlar")]
        [SerializeField] private string streamingAssetsFileName = "MainMenuBackground.mp4";
        [SerializeField] private float fadeInDuration = 0.6f;
        [SerializeField] private bool maintain16x9 = true;
        [SerializeField] private float prepareTimeoutSeconds = 6.0f;

        private Coroutine fadeCoroutine;
        private RenderTexture runtimeRT;
        private bool hasRevealed = false;
        private bool eventsSubscribed = false;

        private void Awake()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
            }

            if (videoCanvasGroup != null)
            {
                videoCanvasGroup.alpha = 0f;
            }

            if (fallbackBackground != null)
            {
                fallbackBackground.SetActive(true);
            }

            SetupAspectRatio();

            if (videoPlayer != null)
            {
                // PlayOnAwake yarış durumunu önlemek için Awake'de kapat ve olayları erken bağla
                videoPlayer.playOnAwake = false;
                videoPlayer.waitForFirstFrame = true;
                videoPlayer.isLooping = true;
                videoPlayer.skipOnDrop = true;
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                for (ushort i = 0; i < videoPlayer.controlledAudioTrackCount; i++)
                {
                    videoPlayer.EnableAudioTrack(i, false);
                    videoPlayer.SetDirectAudioMute(i, true);
                }

                EnsureRenderTexture();
                SubscribeEvents();
            }
        }

        private void Start()
        {
            if (videoPlayer == null)
            {
                Debug.LogError("[MenuVideoBackground] VideoPlayer bileşeni bulunamadı.");
                return;
            }

            if (videoPlayer.clip == null && string.IsNullOrEmpty(videoPlayer.url) && !string.IsNullOrEmpty(streamingAssetsFileName))
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);
                Debug.Log($"[MenuVideoBackground] VideoClip null -> StreamingAssets URL kullanılıyor: {videoPlayer.url}");
            }

            if (videoPlayer.clip == null && string.IsNullOrEmpty(videoPlayer.url))
            {
                Debug.LogError("[MenuVideoBackground] VideoClip veya URL atanmamış!");
                return;
            }

            Debug.Log($"[MenuVideoBackground] Hazırlanıyor -> Source={videoPlayer.source}, Clip={videoPlayer.clip?.name}, Url={videoPlayer.url}, RenderMode={videoPlayer.renderMode}, TargetRT={videoPlayer.targetTexture?.name}, GraphicsAPI={SystemInfo.graphicsDeviceType}");

            videoPlayer.Stop();
            videoPlayer.Prepare();
            StartCoroutine(PrepareWatchdogRoutine());
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
            videoPlayer.errorReceived += OnVideoError;
            eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!eventsSubscribed || videoPlayer == null) return;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.started -= OnVideoStarted;
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
                    name = "MenuBackground_RuntimeRT",
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

            // Başlangıçta siyah/temiz doku garantisi
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = prev;

            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = target;

            if (videoRawImage != null)
            {
                videoRawImage.texture = target;
                videoRawImage.color = Color.white;
                videoRawImage.enabled = true;
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

            fitter.aspectRatio = 16f / 9f;
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            Debug.Log($"[MenuVideoBackground] Video hazır! Boyut={source.width}x{source.height}, Süre={source.length:F2}s");
            EnsureRenderTexture();
            source.Play();
            RevealVideo();
        }

        private void OnVideoStarted(VideoPlayer source)
        {
            Debug.Log("[MenuVideoBackground] Video oynatma başladı.");
            RevealVideo();
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"[MenuVideoBackground] VideoPlayer hatası: {message} (Clip={source?.clip?.name}, GraphicsAPI={SystemInfo.graphicsDeviceType})");
            if (fallbackBackground != null)
            {
                fallbackBackground.SetActive(true);
            }
        }

        private IEnumerator PrepareWatchdogRoutine()
        {
            float elapsed = 0f;
            while (!hasRevealed && elapsed < prepareTimeoutSeconds)
            {
                if (videoPlayer != null && videoPlayer.isPrepared)
                {
                    if (!videoPlayer.isPlaying)
                    {
                        videoPlayer.Play();
                    }
                    if (videoPlayer.frame > 0 || elapsed > 0.5f)
                    {
                        RevealVideo();
                        yield break;
                    }
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!hasRevealed)
            {
                Debug.LogWarning($"[MenuVideoBackground] Video {prepareTimeoutSeconds:F1}s içinde hazırlanamadı (isPrepared={videoPlayer?.isPrepared}, isPlaying={videoPlayer?.isPlaying}). Yedek arka plan korunuyor.");
            }
        }

        private void RevealVideo()
        {
            if (hasRevealed) return;
            hasRevealed = true;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeInVideo());
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
