using UnityEngine;

namespace TheLastBell.Player
{
    /// <summary>
    /// THE LAST BELL - 3. Şahıs Korku Kamerası (TPS Orbit & Collision Detection).
    /// Karakteri omuz üstünden takip eder. PC'de fare, Android'de dokunmatik alanla döner.
    /// SphereCast ile duvar çarpışmalarını algılar ve kameranın duvarların içinden geçmesini önler.
    /// </summary>
    public class TPSCameraFollow : MonoBehaviour
    {
        [Header("--- Hedef ---")]
        [Tooltip("Kameranın odaklanacağı karakter veya pivot noktası")]
        [SerializeField] private Transform target;
        [Tooltip("Hedefin göğüs/baş seviyesine göre dikey ofseti")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0.35f, 1.45f, 0f);

        [Header("--- Mesafe ve Çarpışma ---")]
        [SerializeField] private float defaultDistance = 1.8f;
        [SerializeField] private float minDistance = 0.6f;
        [SerializeField] private float sphereCastRadius = 0.22f;
        [SerializeField] private LayerMask collisionLayers = ~0; // Tüm fiziksel nesneler
        [SerializeField] private float collisionDamping = 14f;

        [Header("--- Hassasiyet ---")]
        [SerializeField] private float pcSensitivityX = 160f;
        [SerializeField] private float pcSensitivityY = 120f;
        [SerializeField] private float mobileSensitivityX = 80f;
        [SerializeField] private float mobileSensitivityY = 60f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 65f;

        private float yaw;
        private float pitch;
        private float currentDistance;

        // Mobil Dokunmatik Bakış Değerleri (MobileControlsUI tarafından beslenir)
        public Vector2 MobileLookDelta { get; set; }

        private void Start()
        {
            currentDistance = defaultDistance;

            if (target != null)
            {
                yaw = target.eulerAngles.y;
                pitch = 12f;
            }

            // PC'de fare imlecini oyun penceresine kitle
            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            HandleInput();
            UpdateCameraPosition();
        }

        private void HandleInput()
        {
            float lookX = 0f;
            float lookY = 0f;

            if (Application.isMobilePlatform || MobileLookDelta.sqrMagnitude > 0.001f)
            {
                lookX = MobileLookDelta.x * mobileSensitivityX * Time.deltaTime;
                lookY = MobileLookDelta.y * mobileSensitivityY * Time.deltaTime;
                MobileLookDelta = Vector2.zero; // Her karede tüket
            }
            else
            {
                lookX = Input.GetAxis("Mouse X") * pcSensitivityX * Time.deltaTime;
                lookY = Input.GetAxis("Mouse Y") * pcSensitivityY * Time.deltaTime;
            }

            yaw += lookX;
            pitch -= lookY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // PC ESC tuşuna basınca imleç kilidini aç/kapa
            if (Input.GetKeyDown(KeyCode.Escape) && !Application.isMobilePlatform)
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            }
        }

        private void UpdateCameraPosition()
        {
            Vector3 focusPoint = target.position + targetOffset;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Hedeflenen geriye doğru yön vektörü
            Vector3 desiredDirection = rotation * -Vector3.forward;

            // Duvar Çarpışma Kontrolü (SphereCast)
            float targetDist = defaultDistance;
            RaycastHit hit;

            // Karakterin kendi collider'ını yok saymak için katman maskesi
            int mask = collisionLayers & ~(1 << target.gameObject.layer);

            if (Physics.SphereCast(focusPoint, sphereCastRadius, desiredDirection, out hit, defaultDistance, mask, QueryTriggerInteraction.Ignore))
            {
                targetDist = Mathf.Clamp(hit.distance - 0.1f, minDistance, defaultDistance);
            }

            // Pürüzsüz mesafe geçişi
            currentDistance = Mathf.Lerp(currentDistance, targetDist, Time.deltaTime * collisionDamping);

            Vector3 finalPos = focusPoint + desiredDirection * currentDistance;

            transform.position = finalPos;
            transform.rotation = rotation;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                yaw = target.eulerAngles.y;
            }
        }
    }
}
