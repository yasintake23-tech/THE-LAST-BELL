using System.Collections;
using UnityEngine;
using TheLastBell.Player;
using TheLastBell.Interaction;
using TheLastBell.UI;

namespace TheLastBell.Cinematics
{
    /// <summary>
    /// THE LAST BELL - Kesintisiz Sahne İçi Sinematik Giriş Yöneticisi (Seamless Intro Manager).
    /// Oyun başladığında video oynatmak yerine doğrudan oyun motoru içinde başlar:
    /// Kamera başlangıçta okul binasını ve karakterin yüzünü/profilini çeken atmosferik bir dış açıda bekler (3 saniye).
    /// Ardından yumuşak bir kamera uçuşuyla (2.5 saniye) karakterin kafa kemiğine ("Head") süzülerek göz hizasına girer.
    /// Kamera kafaya yerleştiğinde True First Person kontrolleri ve oyun arayüzü (UI / Reticle) aktif hale gelir.
    /// </summary>
    public class SeamlessIntroManager : MonoBehaviour
    {
        [Header("--- Kamera & Karakter Referansları ---")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform headBone;
        [SerializeField] private Vector3 eyeLocalOffset = new Vector3(0f, 1.7f, 0.15f);
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private PlayerInteractor playerInteractor;
        [SerializeField] private FlashlightController flashlight;

        [Header("--- Sinematik Başlangıç Açısı ---")]
        [Tooltip("Kameranın başlangıçta duracağı dış açı transformu (boş bırakılırsa otomatik hesaplanır)")]
        [SerializeField] private Transform cinematicStartTransform;
        [SerializeField] private Vector3 defaultStartOffset = new Vector3(2.5f, 0.8f, 3.2f); // Karakterin sağ-ön çaprazından yüzü ve okulu gösterir

        [Header("--- Süreler ve Zamanlama ---")]
        [SerializeField] private float holdDuration = 3.5f;        // Başlangıç açısında bekleme süresi (3.5 saniye)
        [SerializeField] private float flightDuration = 2.0f;      // Kameranın Camera_Pivot'a uçuş süresi (2.0 saniye)

        [Header("--- UI & Atmosfer ---")]
        [SerializeField] private PlayerHUD playerHUD;
        [SerializeField] private CanvasGroup introCanvasGroup;    // Açılış siyah ekran geçişi (varsa)
        [SerializeField] private AudioSource introAudioSource;
        [SerializeField] private AudioClip windWhooshClip;

        private bool isIntroFinished = false;

        public bool IsIntroFinished => isIntroFinished;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (playerController == null) playerController = FindAnyObjectByType<FirstPersonController>();
            if (playerInteractor == null) playerInteractor = FindAnyObjectByType<PlayerInteractor>();
            if (flashlight == null) flashlight = FindAnyObjectByType<FlashlightController>();
            if (playerHUD == null) playerHUD = FindAnyObjectByType<PlayerHUD>();

            // Başlangıçta kontrolleri kapat
            if (playerController != null) playerController.enabled = false;
            if (playerInteractor != null) playerInteractor.enabled = false;
            if (MobileControlManager.Instance != null) MobileControlManager.Instance.SetIntroActive(true);

            // HUD ve imleci gizle
            if (playerHUD != null)
            {
                CanvasGroup hudCg = playerHUD.GetComponent<CanvasGroup>();
                if (hudCg != null) hudCg.alpha = 0f;
            }

            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Start()
        {
            StartCoroutine(PlaySeamlessIntroRoutine());
        }

        private void Update()
        {
            // İsteğe bağlı: Oyuncu Boşluk (Space) veya Sol Fare ile sinematiği atlayabilir
            if (!isIntroFinished && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
            {
                StopAllCoroutines();
                FinishIntroImmediately();
            }
        }

        private IEnumerator PlaySeamlessIntroRoutine()
        {
            if (mainCamera == null || playerController == null)
            {
                FinishIntroImmediately();
                yield break;
            }

            // 1. Kafa kemiğini doğrula
            if (headBone == null)
            {
                Animator anim = playerController.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    headBone = anim.GetBoneTransform(HumanBodyBones.Head);
                }
            }

            if (headBone == null)
            {
                Debug.LogWarning("[SeamlessIntro] Head kemiği bulunamadı, doğrudan FPS başlatılıyor.");
                FinishIntroImmediately();
                yield break;
            }

            // 2. Kamerayı dış dünyada bağımsız yap (uçuş için parent'ı null)
            mainCamera.transform.parent = null;
            mainCamera.nearClipPlane = 0.05f;

            // Giriş sinematiği sırasında yüz ve kafa mesh'lerinin kameraya görünür olmasını sağla
            SetHeadPartsShadowOnly(false);

            // 3. Başlangıç pozisyonunu belirle
            Vector3 startPos;
            Quaternion startRot;

            if (cinematicStartTransform != null)
            {
                startPos = cinematicStartTransform.position;
                startRot = cinematicStartTransform.rotation;
            }
            else
            {
                // Karakterin ön-sağ çaprazından bakarak hem karakteri hem arka planı göster
                Transform playerT = playerController.transform;
                startPos = playerT.position + playerT.forward * defaultStartOffset.z + playerT.right * defaultStartOffset.x + Vector3.up * defaultStartOffset.y;
                Vector3 lookTarget = headBone.position + Vector3.down * 0.1f;
                startRot = Quaternion.LookRotation((lookTarget - startPos).normalized, Vector3.up);
            }

            mainCamera.transform.position = startPos;
            mainCamera.transform.rotation = startRot;

            // Siyah ekran açılış geçişi (varsa)
            if (introCanvasGroup != null)
            {
                float fadeElapsed = 0f;
                while (fadeElapsed < 1.0f)
                {
                    fadeElapsed += Time.deltaTime;
                    introCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / 1.0f);
                    yield return null;
                }
                introCanvasGroup.alpha = 0f;
            }

            // 4. Atmosferik Bekleme (Hold Phase - 3 saniye)
            float holdElapsed = 0f;
            Vector3 driftPos = startPos;
            while (holdElapsed < holdDuration)
            {
                holdElapsed += Time.deltaTime;
                // Çok hafif, sinematik kamera süzülmesi (cinematic drift)
                float driftFactor = holdElapsed / holdDuration;
                mainCamera.transform.position = startPos + mainCamera.transform.right * (Mathf.Sin(driftFactor * Mathf.PI * 0.5f) * 0.2f);
                yield return null;
            }

            // 5. Uçuş ve Göz Hizasına Giriş (Flight Phase - 2.5 saniye)
            if (introAudioSource != null && windWhooshClip != null)
            {
                introAudioSource.PlayOneShot(windWhooshClip, 0.7f);
            }

            Vector3 flightStartPos = mainCamera.transform.position;
            Quaternion flightStartRot = mainCamera.transform.rotation;

            float flightElapsed = 0f;
            while (flightElapsed < flightDuration)
            {
                flightElapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(flightElapsed / flightDuration);
                // Yumuşak hızlanma ve yavaşlama (SmoothStep S-Curve)
                float t = Mathf.SmoothStep(0f, 1f, progress);

                // Hedef: Camera_Pivot noktası
                Vector3 targetPos = (cameraPivot != null) ? cameraPivot.position : (headBone != null ? headBone.position : playerController.transform.position + Vector3.up * 1.65f);
                Quaternion targetRot = (cameraPivot != null) ? cameraPivot.rotation : playerController.transform.rotation;

                mainCamera.transform.position = Vector3.Lerp(flightStartPos, targetPos, t);
                mainCamera.transform.rotation = Quaternion.Slerp(flightStartRot, targetRot, t);

                yield return null;
            }

            // 6. Girişi tamamla
            FinishIntroImmediately();
        }

        private void FinishIntroImmediately()
        {
            if (isIntroFinished) return;
            isIntroFinished = true;

            if (mainCamera != null)
            {
                if (cameraPivot != null)
                {
                    mainCamera.transform.SetParent(cameraPivot, false);
                    mainCamera.transform.localPosition = Vector3.zero;
                    mainCamera.transform.localRotation = Quaternion.identity;
                }
                else if (playerController != null)
                {
                    mainCamera.transform.SetParent(playerController.transform, false);
                    mainCamera.transform.localPosition = new Vector3(0f, 1.65f, 0.18f);
                    mainCamera.transform.localRotation = Quaternion.identity;
                }
                mainCamera.nearClipPlane = 0.01f;
            }

            // Standart Cast Shadows = On modunu koru (aynalarda kafa tam görünsün)
            SetHeadPartsShadowOnly(false);

            // Kontrolleri ve etkileşimi aç
            if (playerController != null)
            {
                playerController.enabled = true;
            }
            if (playerInteractor != null)
            {
                playerInteractor.enabled = true;
            }

            // HUD / Reticle'ı görünür yap
            if (playerHUD != null)
            {
                CanvasGroup hudCg = playerHUD.GetComponent<CanvasGroup>();
                if (hudCg != null)
                {
                    StartCoroutine(FadeHUD(hudCg, 0f, 1f, 0.5f));
                }
            }

            bool isMobile = Application.isMobilePlatform ||
                            (MobileControlManager.Instance != null && MobileControlManager.Instance.IsMobileActive);

            if (MobileControlManager.Instance != null)
            {
                MobileControlManager.Instance.SetIntroActive(false);
            }
            else if (!isMobile)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            Debug.Log($"[THE LAST BELL] Seamless Intro tamamlandı! True First Person aktif (isMobile={isMobile}).");
        }

        private IEnumerator FadeHUD(CanvasGroup cg, float start, float end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
                yield return null;
            }
            cg.alpha = end;
        }

        private void SetHeadPartsShadowOnly(bool shadowsOnly)
        {
            if (playerController == null) return;
            string[] headPartNames = new string[] {
                "AvatarHead", "AvatarEyelashes", "AvatarLeftEyeball", "AvatarRightEyeball", "AvatarTeethLower", "AvatarTeethUpper"
            };
            foreach (var name in headPartNames)
            {
                Transform t = playerController.transform.Find("Character_Model/" + name);
                if (t != null)
                {
                    Renderer r = t.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.shadowCastingMode = shadowsOnly ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
                    }
                }
            }
        }

        public void Configure(Camera cam, Transform head, FirstPersonController fpc, PlayerInteractor interactor, PlayerHUD hud)
        {
            mainCamera = cam;
            headBone = head;
            playerController = fpc;
            playerInteractor = interactor;
            playerHUD = hud;
        }
    }
}
