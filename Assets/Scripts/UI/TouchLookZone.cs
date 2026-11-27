using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TheLastBell.Player;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Profesyonel Dokunmatik Bakış Alanı (Smooth Touch Look Zone).
    /// Ekranın sağ yarısındaki serbest alanda parmak sürüklemelerini algılar.
    /// EventSystem ve IsPointerOverGameObject denetimiyle aksiyon butonlarına (Fener, Çömel, Koş)
    /// veya Joystick'e basıldığında KESİNLİKLE bakış girdisi üretmez, kamera dönüşünü kilitler.
    /// </summary>
    public class TouchLookZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("--- Referanslar ---")]
        [SerializeField] private FirstPersonController fpsController;
        [SerializeField] private MobileControlsUI mobileControls;

        [Header("--- Bakış Hassasiyeti & Yumuşatma ---")]
        [Tooltip("Dokunmatik bakış hassasiyeti çarpanı")]
        [SerializeField] private float lookSensitivity = 0.18f;

        [Tooltip("Piksel atlamalarını ve titremeyi engelleyen yumuşatma oranı")]
        [SerializeField] private float smoothingSpeed = 28f;

        private int activePointerId = -1;
        private Vector2 previousTouchPos;
        private Vector2 targetDelta = Vector2.zero;
        private Vector2 smoothedDelta = Vector2.zero;

        private void Awake()
        {
            if (mobileControls == null) mobileControls = GetComponentInParent<MobileControlsUI>();
            if (fpsController == null) fpsController = FindAnyObjectByType<FirstPersonController>();
        }

        private void Update()
        {
            if (fpsController == null)
            {
                fpsController = FindAnyObjectByType<FirstPersonController>();
                if (fpsController == null) return;
            }

            // Aktif dokunma varsa ve bu dokunma bir UI butonunun/joystick'in üzerindeyse hemen iptal et
            if (activePointerId != -1)
            {
                if (IsTouchOverOtherUI(activePointerId))
                {
                    CancelLook();
                    return;
                }
            }

            // Hafif yumuşatma (damping/smoothing) ile piksel atlamalarını engelle
            smoothedDelta = Vector2.Lerp(smoothedDelta, targetDelta, Time.deltaTime * smoothingSpeed);
            targetDelta = Vector2.zero; // Girdiyi tüket

            if (smoothedDelta.sqrMagnitude > 0.0001f)
            {
                Vector2 lookInput = smoothedDelta * lookSensitivity;
                fpsController.MobileLookInput += lookInput;
            }
            else
            {
                smoothedDelta = Vector2.zero;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Ekranın sol yarısına dokunulduğunda TouchLookZone KESİNLİKLE devreye girmesin!
            if (eventData.position.x < Screen.width * 0.5f) return;

            // Kural: Eğer parmak herhangi bir UI butonuna veya Joystick'e basıyorsa yok say!
            if (IsPointerOverOtherUI(eventData))
            {
                return;
            }

            // Sadece tek bir bakış parmağı kilitlenir (Multi-touch koruması)
            if (activePointerId == -1)
            {
                activePointerId = eventData.pointerId;
                previousTouchPos = eventData.position;
                targetDelta = Vector2.zero;
                smoothedDelta = Vector2.zero;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                if (IsPointerOverOtherUI(eventData))
                {
                    CancelLook();
                    return;
                }

                Vector2 delta = eventData.position - previousTouchPos;
                previousTouchPos = eventData.position;
                targetDelta += delta;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointerId)
            {
                CancelLook();
            }
        }

        private void CancelLook()
        {
            activePointerId = -1;
            targetDelta = Vector2.zero;
            smoothedDelta = Vector2.zero;
        }

        /// <summary>
        /// Parmak bu TouchLookZone haricinde bir UI elemanına (Buton, Joystick vb.) değiyor mu kontrolü
        /// </summary>
        private bool IsPointerOverOtherUI(PointerEventData eventData)
        {
            if (eventData == null) return false;

            // Eğer raycast bu objeden başka bir objeye çarptıysa (örneğin buton)
            if (eventData.pointerCurrentRaycast.gameObject != null && eventData.pointerCurrentRaycast.gameObject != this.gameObject)
            {
                return true;
            }

            if (EventSystem.current != null)
            {
                // IsPointerOverGameObject kontrolü
                if (EventSystem.current.IsPointerOverGameObject(eventData.pointerId))
                {
                    var results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(eventData, results);
                    foreach (var r in results)
                    {
                        if (r.gameObject != this.gameObject)
                        {
                            if (r.gameObject.GetComponentInParent<Button>() != null ||
                                r.gameObject.name.Contains("Button") ||
                                r.gameObject.name.Contains("Joystick"))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool IsTouchOverOtherUI(int fingerId)
        {
            if (EventSystem.current == null) return false;

            if (fingerId >= 0 && EventSystem.current.IsPointerOverGameObject(fingerId))
            {
                PointerEventData ped = new PointerEventData(EventSystem.current)
                {
                    pointerId = fingerId
                };

                // Dokunma konumunu bul
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.fingerId == fingerId)
                    {
                        ped.position = t.position;
                        break;
                    }
                }

                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(ped, results);
                foreach (var r in results)
                {
                    if (r.gameObject != this.gameObject &&
                        (r.gameObject.GetComponentInParent<Button>() != null ||
                         r.gameObject.name.Contains("Button") ||
                         r.gameObject.name.Contains("Joystick")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void SetSensitivity(float sens)
        {
            lookSensitivity = Mathf.Max(0.01f, sens);
        }
    }
}
