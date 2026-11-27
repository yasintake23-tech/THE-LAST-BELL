using System;
using UnityEngine;
using UnityEngine.Rendering;
using TheLastBell.Interaction;

namespace TheLastBell.Player
{
    public enum CameraViewMode
    {
        FirstPerson,
        ThirdPerson
    }

    /// <summary>
    /// THE LAST BELL - Birinci Şahıs / Üçüncü Şahıs Kamera Modu Yöneticisi.
    /// Sahne yenilemeden, pozisyon veya animasyon sıfırlamadan yumuşak geçiş yapar.
    /// - 1P modunda karakter gövde/kafa mesh'lerini ShadowsOnly yaparak kamera tıkanmasını önler.
    /// - 3P modunda tüm karakteri görünür kılar, duvar çarpışmalarını (SphereCast) önler,
    ///   mesafe/yükseklik ayarını destekler ve el fenerini sağ el soketine bağlar.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraModeController : MonoBehaviour
    {
        public static CameraModeController Instance { get; private set; }

        [Header("--- Referanslar ---")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform characterModelRoot;
        [SerializeField] private FlashlightPhysicalItem flashlightPhysical;
        [SerializeField] private FirstPersonController playerController;

        [Header("--- Üçüncü Şahıs (3P) Ayarları ---")]
        [SerializeField] private CameraViewMode currentMode = CameraViewMode.FirstPerson;
        [SerializeField] private float thirdPersonDistance = 2.65f;
        [SerializeField] private float thirdPersonHeight = 1.55f;
        [SerializeField] private float thirdPersonShoulderOffset = 0.28f;
        [SerializeField] private float transitionSpeed = 12f;
        [SerializeField] private float collisionRadius = 0.22f;
        [SerializeField] private float minWallDistance = 0.55f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private Renderer[] characterRenderers;
        private float currentBlend = 0f; // 0 = 1P, 1 = 3P
        private float smoothedDistance;

        public CameraViewMode CurrentMode => currentMode;
        public bool IsThirdPerson => currentMode == CameraViewMode.ThirdPerson;
        public float ThirdPersonDistance => thirdPersonDistance;
        public float ThirdPersonHeight => thirdPersonHeight;

        public event Action<CameraViewMode> OnCameraModeChanged;

        private void Awake()
        {
            Instance = this;
            ResolveReferences();
            smoothedDistance = thirdPersonDistance;
            currentBlend = IsThirdPerson ? 1f : 0f;
            ApplyMeshVisibility(IsThirdPerson);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ResolveReferences()
        {
            if (playerController == null) playerController = GetComponent<FirstPersonController>();
            if (cameraPivot == null) cameraPivot = transform.Find("Camera_Pivot");
            if (mainCamera == null && cameraPivot != null) mainCamera = cameraPivot.GetComponentInChildren<Camera>(true);
            if (mainCamera == null) mainCamera = Camera.main;
            if (characterModelRoot == null) characterModelRoot = transform.Find("Character_Model");
            if (flashlightPhysical == null) flashlightPhysical = GetComponentInChildren<FlashlightPhysicalItem>(true);

            if (characterModelRoot != null)
            {
                characterRenderers = characterModelRoot.GetComponentsInChildren<Renderer>(true);
            }
        }

        private void Update()
        {
            // PC kısayolu: V tuşu ile 1P/3P geçişi
            if (Input.GetKeyDown(KeyCode.V))
            {
                ToggleCameraMode();
            }
        }

        private void LateUpdate()
        {
            StepCamera(Time.unscaledDeltaTime);
        }

        public void StepCamera(float dt)
        {
            if (mainCamera == null || cameraPivot == null) return;
            // SeamlessIntro sırasında kamera pivot'a bağlı değilse müdahale etme
            if (mainCamera.transform.parent != cameraPivot) return;

            float safeDt = Mathf.Max(0.0001f, dt);
            float targetBlend = IsThirdPerson ? 1f : 0f;
            currentBlend = Mathf.MoveTowards(currentBlend, targetBlend, safeDt * (transitionSpeed * 0.35f));

            if (currentBlend <= 0.001f)
            {
                mainCamera.transform.localPosition = Vector3.zero;
                mainCamera.transform.localRotation = Quaternion.identity;
                return;
            }

            // Pivot'un dünya konumu ve yönü (FirstPersonController pitch + yaw içerir)
            Vector3 pivotOrigin = transform.position + Vector3.up * thirdPersonHeight;
            Quaternion lookRot = cameraPivot.rotation;
            Vector3 desiredDir = (lookRot * new Vector3(thirdPersonShoulderOffset, 0f, -thirdPersonDistance));
            float desiredDist = desiredDir.magnitude;
            Vector3 castDir = desiredDist > 0.001f ? desiredDir / desiredDist : -cameraPivot.forward;

            float targetDist = desiredDist;
            if (Physics.SphereCast(pivotOrigin, collisionRadius, castDir, out RaycastHit hit, desiredDist, collisionMask, QueryTriggerInteraction.Ignore))
            {
                // Oyuncunun kendi collider'ını yok say
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
                {
                    targetDist = Mathf.Max(minWallDistance, hit.distance - 0.12f);
                }
            }

            smoothedDistance = Mathf.Lerp(smoothedDistance, targetDist, 1f - Mathf.Exp(-transitionSpeed * safeDt));
            Vector3 tpWorldPos = pivotOrigin + castDir * smoothedDistance;
            Vector3 fpWorldPos = cameraPivot.position;

            mainCamera.transform.position = Vector3.Lerp(fpWorldPos, tpWorldPos, currentBlend);
            mainCamera.transform.rotation = lookRot;
        }

        public void ToggleCameraMode()
        {
            SetCameraMode(IsThirdPerson ? CameraViewMode.FirstPerson : CameraViewMode.ThirdPerson);
        }

        public void SetCameraMode(CameraViewMode mode)
        {
            ResolveReferences();
            currentMode = mode;
            bool thirdPerson = (mode == CameraViewMode.ThirdPerson);

            ApplyMeshVisibility(thirdPerson);

            if (flashlightPhysical != null && flashlightPhysical.CurrentState != FlashlightPhysicalState.DroppedPhysics)
            {
                if (thirdPerson) flashlightPhysical.AttachToHand();
                else flashlightPhysical.AttachToCameraRig();
            }

            OnCameraModeChanged?.Invoke(currentMode);
            Debug.Log($"[CameraModeController] Kamera modu değiştirildi -> {currentMode}");
        }

        public void SetThirdPersonDistance(float distance)
        {
            thirdPersonDistance = Mathf.Clamp(distance, 1.4f, 4.8f);
        }

        public void SetThirdPersonHeight(float height)
        {
            thirdPersonHeight = Mathf.Clamp(height, 1.1f, 2.3f);
        }

        public void ApplyMeshVisibility(bool showFullBody)
        {
            if (characterRenderers == null || characterRenderers.Length == 0)
            {
                if (characterModelRoot != null)
                {
                    characterRenderers = characterModelRoot.GetComponentsInChildren<Renderer>(true);
                }
            }
            if (characterRenderers == null) return;

            ShadowCastingMode mode = showFullBody ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            for (int i = 0; i < characterRenderers.Length; i++)
            {
                if (characterRenderers[i] != null)
                {
                    characterRenderers[i].shadowCastingMode = mode;
                }
            }
        }
    }
}
