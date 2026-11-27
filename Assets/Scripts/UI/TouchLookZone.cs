using UnityEngine;
using UnityEngine.EventSystems;
using TheLastBell.Player;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Profesyonel Dokunmatik Bakış Alanı (Smooth Touch Look Zone).
    /// Ekranın sağ yarısındaki serbest alanda parmak sürüklemelerini algılar.
    /// Multi-touch ile hareket joystick'i ve aksiyon butonlarından bağımsız çalışır.
    /// Çözünürlük ve 60/120Hz yenileme hızından bağımsız, sıçramasız kamera dönüşü sağlar.
    /// </summary>
    public class TouchLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        public const string PREF_LOOK_SENSITIVITY = "TLB_LookSensitivity";

        [Header("--- Referanslar ---")]
        [SerializeField] private FirstPersonController fpsController;
        [SerializeField] private MobileControlsUI mobileControls;

        [Header("--- Bakış Hassasiyeti & Yumuşatma ---")]
        [SerializeField] private float lookSensitivity = 0.18f;
        [SerializeField] private float smoothingSpeed = 30f;
        [SerializeField] private float maxDeltaPerFrame = 120f;

        private int activePointerId = int.MinValue;
        private Vector2 previousTouchPos;
        private Vector2 pendingDelta = Vector2.zero;
        private Vector2 smoothedDelta = Vector2.zero;

        public float LookSensitivity => lookSensitivity;

        private void Awake()
        {
            if (mobileControls == null) mobileControls = GetComponentInParent<MobileControlsUI>();
            if (fpsController == null) fpsController = FindAnyObjectByType<FirstPersonController>();

            if (PlayerPrefs.HasKey(PREF_LOOK_SENSITIVITY))
            {
                lookSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(PREF_LOOK_SENSITIVITY, lookSensitivity), 0.05f, 0.50f);
            }
        }

        private void OnDisable()
        {
            CancelLook();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelLook();
        }

        private void Update()
        {
            if (fpsController == null)
            {
                fpsController = FindAnyObjectByType<FirstPersonController>();
                if (fpsController == null) return;
            }

            if (Time.timeScale <= 0.001f || !fpsController.enabled)
            {
                if (activePointerId != int.MinValue) CancelLook();
                return;
            }

            // Aktif dokunma parmağı kalktıysa güvenlik sıfırlaması (multi-touch / focus kaybı koruması)
            if (activePointerId != int.MinValue && Input.touchCount > 0)
            {
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.fingerId == activePointerId)
                    {
                        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        {
                            CancelLook();
                            return;
                        }
                        found = true;
                        break;
                    }
                }
                if (!found && activePointerId >= 0)
                {
                    CancelLook();
                    return;
                }
            }

            float dt = Time.unscaledDeltaTime;
            float lerpFactor = 1f - Mathf.Exp(-smoothingSpeed * dt);
            smoothedDelta = Vector2.Lerp(smoothedDelta, pendingDelta, lerpFactor);
            pendingDelta = Vector2.zero;

            if (smoothedDelta.sqrMagnitude > 0.0001f)
            {
                // 1080p referans yüksekliğine göre normalize et (Galaxy A56 5G vb. farklı ekranlarda tutarlı hız)
                float resScale = 1080f / Mathf.Max(480f, Screen.height);
                Vector2 lookInput = smoothedDelta * (lookSensitivity * resScale);
                fpsController.MobileLookInput += lookInput;
            }
            else
            {
                smoothedDelta = Vector2.zero;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Time.timeScale <= 0.001f) return;

            // Ekranın sol yarısına dokunulduğunda TouchLookZone devreye girmesin
            if (eventData.position.x < Screen.width * 0.45f) return;

            // Eğer EventSystem doğrudan bir butona veya joystick'e çarptıysa yok say
            if (eventData.pointerCurrentRaycast.gameObject != null &&
                eventData.pointerCurrentRaycast.gameObject != gameObject)
            {
                return;
            }

            if (activePointerId == int.MinValue)
            {
                activePointerId = eventData.pointerId;
                previousTouchPos = eventData.position;
                pendingDelta = Vector2.zero;
                smoothedDelta = Vector2.zero;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;

            Vector2 delta = eventData.position - previousTouchPos;
            previousTouchPos = eventData.position;

            // Dokunmatik kare sıçramalarını önle
            delta = Vector2.ClampMagnitude(delta, maxDeltaPerFrame);
            pendingDelta += delta;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                CancelLook();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                CancelLook();
            }
        }

        private void CancelLook()
        {
            activePointerId = int.MinValue;
            pendingDelta = Vector2.zero;
            smoothedDelta = Vector2.zero;
        }

        public void SetSensitivity(float sens)
        {
            lookSensitivity = Mathf.Clamp(sens, 0.05f, 0.50f);
            PlayerPrefs.SetFloat(PREF_LOOK_SENSITIVITY, lookSensitivity);
            PlayerPrefs.Save();
        }
    }
}
