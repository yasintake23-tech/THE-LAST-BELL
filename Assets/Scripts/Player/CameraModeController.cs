using System;
using UnityEngine;
using UnityEngine.Rendering;
using TheLastBell.Interaction;

namespace TheLastBell.Player
{
    public enum CameraViewMode
    {
        FirstPerson,
        ThirdPerson,
        FreeInspectionOrbit
    }

    /// <summary>
    /// THE LAST BELL - Gerçek Birinci Şahıs (True FPS), Üçüncü Şahıs (3P) ve Serbest İnceleme (Orbit) Kamera Yöneticisi.
    /// ExecutionOrder(100): CharacterProceduralController(50) sonrası çalışır -> geri besleme döngüsü (feedback loop) sıfırdır.
    /// - 1P modunda yalnızca kafa/yüz mesh'lerini ShadowsOnly yapar; gövde, kollar, eller, bacaklar ve ayaklar görünür kalır.
    /// - 1P kamerası göz çapasına (EyeAnchor) yumuşak stabilizasyonla bağlanır; düşme/sinematikte kafa hareketini takip eder.
    /// - 3P modunda duvar çarpışmasını (SphereCast) önler; FreeInspectionOrbit modunda karakter bakışını bozmadan 360° inceleme sunar.
    /// - Geliştirme amaçlı Nötr İnceleme Işığı (Neutral Inspection Light) anahtarı içerir.
    /// </summary>
    [DefaultExecutionOrder(100)]
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
        [SerializeField] private CharacterProceduralController proceduralController;
        [SerializeField] private ProtagonistAnimationController animController;

        [Header("--- True First-Person (1P) Ayarları ---")]
        [SerializeField] private float firstPersonNearClip = 0.06f;
        [SerializeField] [Range(0f, 1f)] private float verticalStabilization = 0.65f;
        [SerializeField] private float eyeFollowSmooth = 18f;

        [Header("--- Üçüncü Şahıs (3P) & Serbest Yörünge Ayarları ---")]
        [SerializeField] private CameraViewMode currentMode = CameraViewMode.FirstPerson;
        [SerializeField] private float thirdPersonDistance = 2.65f;
        [SerializeField] private float thirdPersonHeight = 1.55f;
        [SerializeField] private float thirdPersonShoulderOffset = 0.28f;
        [SerializeField] private float transitionSpeed = 12f;
        [SerializeField] private float collisionRadius = 0.22f;
        [SerializeField] private float minWallDistance = 0.55f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private Renderer[] characterRenderers;
        private float currentBlend = 0f; // 0 = 1P, 1 = 3P/Orbit
        private float smoothedDistance;
        private float smoothedEyeY;
        private bool hasSmoothedEyeY = false;

        // Serbest inceleme yörüngesi (karakter bakışından bağımsız)
        private float orbitYaw = 155f;
        private float orbitPitch = 12f;

        // Nötr geliştirme inceleme ışığı
        private Light neutralInspectionLight;
        private Color savedAmbientLight;
        private float savedAmbientIntensity;
        private bool neutralLightActive = false;

        public CameraViewMode CurrentMode => currentMode;
        public bool IsThirdPerson => currentMode == CameraViewMode.ThirdPerson || currentMode == CameraViewMode.FreeInspectionOrbit;
        public bool IsFreeOrbit => currentMode == CameraViewMode.FreeInspectionOrbit;
        public float ThirdPersonDistance => thirdPersonDistance;
        public float ThirdPersonHeight => thirdPersonHeight;
        public float OrbitYaw => orbitYaw;
        public bool IsNeutralLightActive => neutralLightActive;

        public event Action<CameraViewMode> OnCameraModeChanged;

        private void Awake()
        {
            Instance = this;
            ResolveReferences();
            smoothedDistance = thirdPersonDistance;
            currentBlend = IsThirdPerson ? 1f : 0f;
            EnsureMeshBoundsAndCulling();
            ApplyMeshVisibility(IsThirdPerson);
            EnsureFlashlightInHand();
        }

        private void OnDestroy()
        {
            SetNeutralInspectionLight(false);
            if (Instance == this) Instance = null;
        }

        public void ResolveReferences()
        {
            if (playerController == null) playerController = GetComponent<FirstPersonController>();
            if (proceduralController == null) proceduralController = GetComponentInChildren<CharacterProceduralController>(true);
            if (animController == null) animController = GetComponentInChildren<ProtagonistAnimationController>(true);
            if (cameraPivot == null) cameraPivot = transform.Find("Camera_Pivot");
            if (mainCamera == null && cameraPivot != null) mainCamera = cameraPivot.GetComponentInChildren<Camera>(true);
            if (mainCamera == null) mainCamera = Camera.main;
            if (characterModelRoot == null) characterModelRoot = transform.Find("Character_Model");
            if (flashlightPhysical == null) flashlightPhysical = GetComponentInChildren<FlashlightPhysicalItem>(true);

            if (characterModelRoot != null)
            {
                characterRenderers = characterModelRoot.GetComponentsInChildren<Renderer>(true);
            }
            if (mainCamera != null)
            {
                mainCamera.nearClipPlane = firstPersonNearClip;
            }
        }

        private void EnsureMeshBoundsAndCulling()
        {
            if (characterModelRoot == null) return;
            foreach (var smr in characterModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // True FPS ve 3P'de karakterin beklenmedik şekilde kaybolmasını (frustum culling) önle
                smr.updateWhenOffscreen = true;
            }
            var anim = characterModelRoot.GetComponent<Animator>();
            if (anim != null)
            {
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private void EnsureFlashlightInHand()
        {
            if (flashlightPhysical != null && flashlightPhysical.CurrentState != FlashlightPhysicalState.DroppedPhysics)
            {
                flashlightPhysical.AttachToHand();
            }
        }

        private void Update()
        {
            if (playerController != null && (!playerController.enabled || !playerController.AllowMovement)) return;
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
            if (mainCamera.transform.parent != cameraPivot) return;

            float safeDt = Mathf.Max(0.0001f, dt);
            float targetBlend = IsThirdPerson ? 1f : 0f;
            currentBlend = Mathf.MoveTowards(currentBlend, targetBlend, safeDt * (transitionSpeed * 0.35f));

            // 1. True First-Person Göz Konumu & Yönelimi (EyeAnchor + stabilizasyon)
            Vector3 fpWorldPos = ComputeTrueFirstPersonPosition(safeDt);
            Quaternion fpWorldRot = ComputeTrueFirstPersonRotation();

            if (currentBlend <= 0.001f)
            {
                mainCamera.transform.position = fpWorldPos;
                mainCamera.transform.rotation = fpWorldRot;
                return;
            }

            // 2. Üçüncü Şahıs veya Serbest İnceleme Yörüngesi
            Vector3 pivotOrigin = transform.position + Vector3.up * thirdPersonHeight;
            Quaternion lookRot;
            Vector3 desiredOffset;

            if (currentMode == CameraViewMode.FreeInspectionOrbit)
            {
                lookRot = Quaternion.Euler(orbitPitch, transform.eulerAngles.y + orbitYaw, 0f);
                desiredOffset = lookRot * new Vector3(0f, 0f, -thirdPersonDistance);
            }
            else
            {
                lookRot = playerController != null
                    ? Quaternion.Euler(playerController.DesiredLookPitch, playerController.DesiredLookYaw, 0f)
                    : cameraPivot.rotation;
                desiredOffset = lookRot * new Vector3(thirdPersonShoulderOffset, 0f, -thirdPersonDistance);
            }

            float desiredDist = desiredOffset.magnitude;
            Vector3 castDir = desiredDist > 0.001f ? desiredOffset / desiredDist : -transform.forward;

            float targetDist = desiredDist;
            if (Physics.SphereCast(pivotOrigin, collisionRadius, castDir, out RaycastHit hit, desiredDist, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
                {
                    targetDist = Mathf.Max(minWallDistance, hit.distance - 0.12f);
                }
            }

            smoothedDistance = Mathf.Lerp(smoothedDistance, targetDist, 1f - Mathf.Exp(-transitionSpeed * safeDt));
            Vector3 tpWorldPos = pivotOrigin + castDir * smoothedDistance;
            Quaternion tpWorldRot = (currentMode == CameraViewMode.FreeInspectionOrbit)
                ? Quaternion.LookRotation((pivotOrigin - tpWorldPos).normalized, Vector3.up)
                : lookRot;

            mainCamera.transform.position = Vector3.Lerp(fpWorldPos, tpWorldPos, currentBlend);
            mainCamera.transform.rotation = Quaternion.Slerp(fpWorldRot, tpWorldRot, currentBlend);
        }

        public bool TryGetFirstPersonPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (cameraPivot == null) return false;
            position = ComputeTrueFirstPersonPosition(Time.unscaledDeltaTime);
            rotation = ComputeTrueFirstPersonRotation();
            return true;
        }

        private Vector3 ComputeTrueFirstPersonPosition(float dt)
        {
            if (proceduralController != null && proceduralController.EyeAnchor != null)
            {
                Vector3 rawEye = proceduralController.EyeAnchor.position;
                bool fullOverride = animController != null && animController.IsFullBodyOverrideActive;

                if (fullOverride)
                {
                    smoothedEyeY = rawEye.y;
                    hasSmoothedEyeY = true;
                    return rawEye;
                }

                // Yürüme/koşma sırasında aşırı dikey sarsıntıyı (head bob) yumuşat, ancak karakterden kopma
                float nominalY = transform.position.y + (playerController != null && playerController.IsCrouching ? 1.02f : 1.62f);
                float stabilizedTargetY = Mathf.Lerp(rawEye.y, nominalY, verticalStabilization * 0.45f);
                if (!hasSmoothedEyeY)
                {
                    smoothedEyeY = stabilizedTargetY;
                    hasSmoothedEyeY = true;
                }
                else
                {
                    smoothedEyeY = Mathf.Lerp(smoothedEyeY, stabilizedTargetY, 1f - Mathf.Exp(-eyeFollowSmooth * dt));
                }

                return new Vector3(rawEye.x, smoothedEyeY, rawEye.z);
            }

            return cameraPivot.position;
        }

        private Quaternion ComputeTrueFirstPersonRotation()
        {
            Quaternion gazeRot = playerController != null
                ? Quaternion.Euler(playerController.DesiredLookPitch, playerController.DesiredLookYaw, 0f)
                : cameraPivot.rotation;

            bool fullOverride = animController != null && animController.IsFullBodyOverrideActive;
            if (fullOverride && proceduralController != null && proceduralController.EyeAnchor != null)
            {
                // Düşme / kalkma / sinematik sırasında kafa yönelimini (roll sınırlı) takip et
                Vector3 fwd = proceduralController.EyeAnchor.forward;
                if (fwd.sqrMagnitude > 0.001f)
                {
                    return Quaternion.LookRotation(fwd.normalized, Vector3.up);
                }
            }

            return gazeRot;
        }

        public void ToggleCameraMode()
        {
            if (currentMode == CameraViewMode.FirstPerson) SetCameraMode(CameraViewMode.ThirdPerson);
            else SetCameraMode(CameraViewMode.FirstPerson);
        }

        public void SetCameraMode(CameraViewMode mode)
        {
            ResolveReferences();
            currentMode = mode;
            bool thirdPerson = IsThirdPerson;

            if (playerController != null)
            {
                playerController.FreezeGazeForInspection = (mode == CameraViewMode.FreeInspectionOrbit);
            }

            ApplyMeshVisibility(thirdPerson);
            EnsureFlashlightInHand();

            OnCameraModeChanged?.Invoke(currentMode);
            Debug.Log($"[CameraModeController] Kamera modu değiştirildi -> {currentMode}");
        }

        public void SetThirdPersonDistance(float distance)
        {
            thirdPersonDistance = Mathf.Clamp(distance, 1.4f, 4.8f);
        }

        public void SetThirdPersonHeight(float height)
        {
            thirdPersonHeight = Mathf.Clamp(height, 1.0f, 2.3f);
        }

        public void SetOrbitAngles(float yawDeg, float pitchDeg)
        {
            orbitYaw = Mathf.Repeat(yawDeg, 360f);
            orbitPitch = Mathf.Clamp(pitchDeg, -30f, 55f);
        }

        public void SetNeutralInspectionLight(bool active)
        {
            if (neutralLightActive == active) return;
            neutralLightActive = active;

            if (active)
            {
                savedAmbientLight = RenderSettings.ambientLight;
                savedAmbientIntensity = RenderSettings.ambientIntensity;
                RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
                RenderSettings.ambientIntensity = 1.0f;

                if (neutralInspectionLight == null)
                {
                    var go = new GameObject("_Dev_NeutralInspectionLight");
                    neutralInspectionLight = go.AddComponent<Light>();
                    neutralInspectionLight.type = LightType.Directional;
                    neutralInspectionLight.intensity = 1.05f;
                    neutralInspectionLight.color = new Color(0.96f, 0.97f, 1.0f);
                    neutralInspectionLight.shadows = LightShadows.None;
                    go.transform.rotation = Quaternion.Euler(36f, -145f, 0f);
                }
                neutralInspectionLight.enabled = true;
            }
            else
            {
                RenderSettings.ambientLight = savedAmbientLight;
                RenderSettings.ambientIntensity = savedAmbientIntensity;
                if (neutralInspectionLight != null)
                {
                    neutralInspectionLight.enabled = false;
                }
            }
        }

        public void ApplyMeshVisibility(bool showFullHeadAndBody)
        {
            if (characterRenderers == null || characterRenderers.Length == 0)
            {
                if (characterModelRoot != null)
                {
                    characterRenderers = characterModelRoot.GetComponentsInChildren<Renderer>(true);
                }
            }
            if (characterRenderers == null) return;

            for (int i = 0; i < characterRenderers.Length; i++)
            {
                var r = characterRenderers[i];
                if (r == null) continue;

                if (showFullHeadAndBody)
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
                else
                {
                    // True First-Person: Yalnızca kamerayı tıkayan kafa/yüz parçalarını ShadowsOnly yap;
                    // gövde, ceket, kollar, eller, pantolon ve ayakkabılar görünür kalır!
                    string n = r.gameObject.name;
                    bool isHeadGeometry =
                        n.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Teeth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Eyelash", StringComparison.OrdinalIgnoreCase) >= 0;

                    r.shadowCastingMode = isHeadGeometry ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                }
            }
        }
    }
}
