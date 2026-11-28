using UnityEngine;
using TheLastBell.Interaction;

namespace TheLastBell.Player
{
    /// <summary>
    /// THE LAST BELL - Birleşik Prosedürel Karakter Kontrolcüsü.
    /// Animator değerlendirmesinden sonra (ExecutionOrder 50), kameradan önce (ExecutionOrder 100) çalışır:
    /// - Önbelleğe alınmış kemik referansları (sıfır per-frame tahsis)
    /// - Anatomik kafa / boyun / üst gövde bakış takibi (pelvis ve bacaklar sabit kalır)
    /// - Dinamik el feneri nişan alma + duvar çarpışma geri çekilmesi (wall avoidance)
    /// - Çevresel etkileşim (kapı kolu, yerden fener alma) için hedef tabanlı el IK koordinasyonu
    /// - Bağlama duyarlı ikincil hareket (sakin nefes, korku titremesi)
    /// - Geliştirme amaçlı bağımsız alt sistem anahtarları ve çalışma zamanı görselleştirme işaretçileri
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class CharacterProceduralController : MonoBehaviour
    {
        [Header("--- Referanslar ---")]
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private ProtagonistAnimationController animController;
        [SerializeField] private Animator animator;
        [SerializeField] private FlashlightPhysicalItem flashlightPhysical;

        [Header("--- Bağımsız Geliştirme Anahtarları (Debug Toggles) ---")]
        public bool enableHeadTracking = true;
        public bool enableTorsoTracking = true;
        public bool enableFootIK = true;
        public bool enableHandIK = true;
        public bool enableFlashlightAiming = true;
        public bool enableSecondaryMotion = true;
        public bool enableDebugVisuals = false;

        [Header("--- Anatomik Bakış Dağılımı & Sınırları ---")]
        [SerializeField] private float gazeSmoothSpeed = 12f;
        [SerializeField] private float maxHeadYaw = 42f;
        [SerializeField] private float maxNeckYaw = 20f;
        [SerializeField] private float maxTorsoYaw = 18f;
        [SerializeField] private float maxHeadPitch = 46f;
        [SerializeField] private float maxNeckPitch = 18f;
        [SerializeField] private float maxTorsoPitch = 8f;

        [Header("--- Fener Nişan & Duvar Kaçınma ---")]
        [SerializeField] private float flashlightAimSmooth = 10f;
        [SerializeField] private float wallAvoidCheckDistance = 0.72f;
        [SerializeField] private float wallAvoidRadius = 0.12f;
        [SerializeField] private LayerMask wallAvoidMask = ~0;

        // Önbelleğe alınmış kemikler
        private Transform boneHips;
        private Transform boneSpine;
        private Transform boneChest;
        private Transform boneUpperChest;
        private Transform boneNeck;
        private Transform boneHead;
        private Transform boneLeftEye;
        private Transform boneRightEye;
        private Transform boneRightShoulder;
        private Transform boneRightHand;
        private Transform boneLeftHand;
        private Transform boneLeftFoot;
        private Transform boneRightFoot;
        private Transform eyeAnchor;

        // Yumuşatılmış durumlar
        private float smoothedRelYaw = 0f;
        private float smoothedPitch = 0f;
        private float proceduralGazeWeight = 1f;
        private float wallPullback01 = 0f;
        private Vector3 smoothedFlashlightAimPoint;
        private Vector3 computedRightHandIkPos;
        private Quaternion computedRightHandIkRot = Quaternion.identity;
        private float computedRightHandIkWeight = 0f;

        // Aktif çevresel etkileşim (örn. kapı kolu)
        private InteractionTarget activeInteractionTarget;
        private float interactionElapsed = 0f;
        private bool interactionActive = false;

        // Runtime Debug Marker'ları (yalnızca enableDebugVisuals=true iken aktif)
        private GameObject debugVisualRoot;
        private Transform dbgEyeMarker;
        private Transform dbgHandSocketMarker;
        private Transform dbgAimTargetMarker;
        private Transform dbgLeftFootMarker;
        private Transform dbgRightFootMarker;
        private Transform dbgInteractMarker;

        public Transform EyeAnchor => eyeAnchor;
        public Transform HeadBone => boneHead;
        public Vector3 RightHandIkPosition => computedRightHandIkPos;
        public Quaternion RightHandIkRotation => computedRightHandIkRot;
        public float RightHandIkWeight => computedRightHandIkWeight;
        public float WallPullback01 => wallPullback01;
        public bool IsEnvironmentalInteractionActive => interactionActive;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (playerController == null) playerController = GetComponentInParent<FirstPersonController>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animController == null) animController = GetComponentInChildren<ProtagonistAnimationController>(true);
            if (flashlightPhysical == null) flashlightPhysical = GetComponentInChildren<FlashlightPhysicalItem>(true);

            CacheBones();
            EnsureEyeAnchor();
        }

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman) return;

            boneHips = animator.GetBoneTransform(HumanBodyBones.Hips);
            boneSpine = animator.GetBoneTransform(HumanBodyBones.Spine);
            boneChest = animator.GetBoneTransform(HumanBodyBones.Chest);
            boneUpperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            boneNeck = animator.GetBoneTransform(HumanBodyBones.Neck);
            boneHead = animator.GetBoneTransform(HumanBodyBones.Head);
            boneLeftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            boneRightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            boneRightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            boneRightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            boneLeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            boneLeftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            boneRightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }

        private void EnsureEyeAnchor()
        {
            if (boneHead == null) return;
            Transform existing = boneHead.Find("TrueFPS_Eye_Anchor");
            if (existing != null)
            {
                eyeAnchor = existing;
                return;
            }

            var go = new GameObject("TrueFPS_Eye_Anchor");
            go.transform.SetParent(boneHead, false);
            if (boneLeftEye != null && boneRightEye != null)
            {
                Vector3 midWorld = (boneLeftEye.position + boneRightEye.position) * 0.5f;
                Vector3 localMid = boneHead.InverseTransformPoint(midWorld);
                // Göz yüzeyinin hemen önüne (iç kafa geometrisinden kurtulacak şekilde) yerleştir
                go.transform.localPosition = localMid + new Vector3(0f, 0.01f, 0.065f);
            }
            else
            {
                go.transform.localPosition = new Vector3(0f, 0.085f, 0.115f);
            }
            go.transform.localRotation = Quaternion.identity;
            eyeAnchor = go.transform;
        }

        public void BeginEnvironmentalInteraction(InteractionTarget target)
        {
            if (target == null) return;
            activeInteractionTarget = target;
            interactionElapsed = 0f;
            interactionActive = true;

            if (animController != null)
            {
                if (target.PreferredHand == PreferredInteractionHand.Left)
                {
                    animController.TriggerDoorInteract(target.HandTarget);
                }
                else
                {
                    animController.TriggerItemPickup(target.HandTarget);
                }
            }
        }

        public void CancelEnvironmentalInteraction()
        {
            interactionActive = false;
            activeInteractionTarget = null;
            interactionElapsed = 0f;
            animController?.ClearLeftHandInteractTarget();
        }

        private void LateUpdate()
        {
            StepProcedural(Time.deltaTime);
        }

        public void StepProcedural(float dt)
        {
            if (animator == null) return;
            if (boneHead == null) CacheBones();
            if (eyeAnchor == null) EnsureEyeAnchor();

            float safeDt = Mathf.Max(0.0001f, dt);

            // Foot IK toggle senkronizasyonu
            if (animController != null)
            {
                animController.SetFootIKEnabled(enableFootIK);
            }

            // İzole klip önizleme veya tam gövde override (düşme/kalkma/sinematik) sırasında prosedürel bakışı yumuşakça azalt
            bool isolated = animController != null && animController.IsIsolatedPreviewActive;
            bool fullOverride = animController != null && animController.IsFullBodyOverrideActive;
            float targetWeight = (isolated || fullOverride) ? 0f : 1f;
            proceduralGazeWeight = Mathf.MoveTowards(proceduralGazeWeight, targetWeight, safeDt * 6f);

            // 1. İstenen bakış yönünden (kamera değil!) göreceli kafa/gövde açılarını yumuşat
            float targetRelYaw = playerController != null ? playerController.RelativeLookYaw : 0f;
            float targetPitch = playerController != null ? playerController.DesiredLookPitch : 0f;

            float blend = 1f - Mathf.Exp(-gazeSmoothSpeed * safeDt);
            smoothedRelYaw = Mathf.Lerp(smoothedRelYaw, targetRelYaw, blend);
            smoothedPitch = Mathf.Lerp(smoothedPitch, targetPitch, blend);

            // 2. Prosedürel üst gövde + boyun + kafa dönüşü (pelvis/bacaklar kesinlikle etkilenmez)
            if (proceduralGazeWeight > 0.001f)
            {
                ApplyTorsoAndHeadTracking(proceduralGazeWeight);
            }

            // 3. Bağlama duyarlı ikincil hareket (sakin nefes + korku titremesi)
            if (enableSecondaryMotion && proceduralGazeWeight > 0.05f)
            {
                ApplySecondaryMotion(proceduralGazeWeight);
            }

            // 4. Aktif çevresel etkileşim IK zamanlaması (kapı kolu / nesne teması)
            UpdateEnvironmentalInteraction(safeDt);

            // 5. Dinamik el feneri nişan alma ve duvar çarpışma kaçınması
            UpdateFlashlightAimingAndWallAvoidance(safeDt, isolated || fullOverride);

            // 6. Opsiyonel geliştirme görselleştirme işaretçileri
            UpdateDebugVisuals();
        }

        private void ApplyTorsoAndHeadTracking(float weight)
        {
            Transform root = playerController != null ? playerController.transform : transform;
            Vector3 upAxis = root.up;
            Vector3 rightAxis = root.right;

            if (enableTorsoTracking)
            {
                float torsoYaw = Mathf.Clamp(smoothedRelYaw * 0.22f, -maxTorsoYaw, maxTorsoYaw) * weight;
                float torsoPitch = Mathf.Clamp(smoothedPitch * 0.12f, -maxTorsoPitch, maxTorsoPitch) * weight;
                Transform torsoBone = boneUpperChest != null ? boneUpperChest : boneChest;
                if (torsoBone != null)
                {
                    Quaternion yawRot = Quaternion.AngleAxis(torsoYaw, upAxis);
                    Quaternion pitchRot = Quaternion.AngleAxis(torsoPitch, rightAxis);
                    torsoBone.rotation = yawRot * pitchRot * torsoBone.rotation;
                }
            }

            if (enableHeadTracking)
            {
                float neckYaw = Mathf.Clamp(smoothedRelYaw * 0.26f, -maxNeckYaw, maxNeckYaw) * weight;
                float neckPitch = Mathf.Clamp(smoothedPitch * 0.28f, -maxNeckPitch, maxNeckPitch) * weight;
                if (boneNeck != null)
                {
                    boneNeck.rotation = Quaternion.AngleAxis(neckYaw, upAxis) * Quaternion.AngleAxis(neckPitch, rightAxis) * boneNeck.rotation;
                }

                float headYaw = Mathf.Clamp(smoothedRelYaw * 0.52f, -maxHeadYaw, maxHeadYaw) * weight;
                float headPitch = Mathf.Clamp(smoothedPitch * 0.60f, -maxHeadPitch, maxHeadPitch) * weight;
                if (boneHead != null)
                {
                    boneHead.rotation = Quaternion.AngleAxis(headYaw, upAxis) * Quaternion.AngleAxis(headPitch, rightAxis) * boneHead.rotation;
                }
            }
        }

        private void ApplySecondaryMotion(float weight)
        {
            Transform torsoBone = boneChest != null ? boneChest : boneSpine;
            if (torsoBone == null) return;

            float t = Time.time;
            float fear = animController != null ? animController.FearLevel : 0f;
            float breatheFreq = Mathf.Lerp(1.35f, 3.1f, fear);
            float breatheAmp = Mathf.Lerp(0.55f, 1.35f, fear) * weight;
            float breatheAngle = Mathf.Sin(t * breatheFreq) * breatheAmp;

            torsoBone.localRotation = torsoBone.localRotation * Quaternion.Euler(breatheAngle, 0f, 0f);

            // Korku yüksekse el feneri tutan sağ bilekte hafif, gerçekçi gerginlik titremesi
            if (fear > 0.15f && boneRightHand != null && animController != null && animController.IsFlashlightEquipped)
            {
                float tremor = (fear - 0.15f) * 0.9f * weight;
                float rx = (Mathf.PerlinNoise(t * 9f, 0.3f) - 0.5f) * tremor;
                float ry = (Mathf.PerlinNoise(0.7f, t * 9f) - 0.5f) * tremor;
                boneRightHand.localRotation = boneRightHand.localRotation * Quaternion.Euler(rx, ry, 0f);
            }
        }

        private void UpdateEnvironmentalInteraction(float dt)
        {
            if (!interactionActive || activeInteractionTarget == null) return;

            interactionElapsed += dt;
            float dur = Mathf.Max(0.2f, activeInteractionTarget.TotalDuration);
            float norm = interactionElapsed / dur;

            // Temas aralığı boyunca sol el IK ağırlığını yumuşakça yükseltip indir
            float contactIn = Mathf.Clamp01((norm - (activeInteractionTarget.ContactNormalizedTime - 0.16f)) / 0.16f);
            float contactOut = 1f - Mathf.Clamp01((norm - activeInteractionTarget.ReleaseNormalizedTime) / 0.18f);
            float ikWeight = enableHandIK ? Mathf.Clamp01(Mathf.Min(contactIn, contactOut)) : 0f;

            animController?.SetLeftHandInteractionIK(activeInteractionTarget.HandTarget, ikWeight);

            if (norm >= 1f)
            {
                CancelEnvironmentalInteraction();
            }
        }

        private void UpdateFlashlightAimingAndWallAvoidance(float dt, bool suppressed)
        {
            bool flashlightHeld = animController != null && animController.IsFlashlightEquipped &&
                                  (flashlightPhysical == null || flashlightPhysical.CurrentState != FlashlightPhysicalState.DroppedPhysics);

            if (suppressed || !flashlightHeld || !enableFlashlightAiming || playerController == null)
            {
                computedRightHandIkWeight = 0f;
                animController?.SetRightHandAimIK(Vector3.zero, Quaternion.identity, 0f);
                return;
            }

            Vector3 gazeOrigin = eyeAnchor != null
                ? eyeAnchor.position
                : (playerController.transform.position + Vector3.up * (playerController.IsCrouching ? 1.0f : 1.55f));
            Vector3 gazeDir = playerController.DesiredLookDirection;

            // Duvar kaçınma SphereCast'i (sağ omuzdan ileri)
            Vector3 shoulderOrigin = boneRightShoulder != null
                ? boneRightShoulder.position
                : (gazeOrigin + playerController.transform.right * 0.22f - Vector3.up * 0.18f);

            float targetPullback = 0f;
            if (Physics.SphereCast(shoulderOrigin, wallAvoidRadius, gazeDir, out RaycastHit hit, wallAvoidCheckDistance, wallAvoidMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(playerController.transform))
                {
                    targetPullback = Mathf.Clamp01(1f - (hit.distance / wallAvoidCheckDistance));
                }
            }
            wallPullback01 = Mathf.Lerp(wallPullback01, targetPullback, 1f - Mathf.Exp(-14f * dt));

            // Doğal fener hedef noktası (hafif gecikmeli yumuşatma)
            Vector3 rawAimPoint = gazeOrigin + gazeDir * 10f;
            if (smoothedFlashlightAimPoint == Vector3.zero) smoothedFlashlightAimPoint = rawAimPoint;
            smoothedFlashlightAimPoint = Vector3.Lerp(smoothedFlashlightAimPoint, rawAimPoint, 1f - Mathf.Exp(-flashlightAimSmooth * dt));

            // Sağ el IK konumu: sağ omuzun önünde, duvar yaklaştıkça gövdeye doğru geri çekilir
            Vector3 rightVec = playerController.transform.right;
            Vector3 upVec = Vector3.up;
            float forwardReach = Mathf.Lerp(0.42f, 0.18f, wallPullback01);
            float verticalOffset = Mathf.Clamp(gazeDir.y * 0.22f, -0.18f, 0.22f);
            float lateralOffset = Mathf.Lerp(0.18f, 0.12f, wallPullback01);

            computedRightHandIkPos = shoulderOrigin + gazeDir * forwardReach + rightVec * lateralOffset + upVec * (verticalOffset - 0.08f);
            Vector3 toTarget = (smoothedFlashlightAimPoint - computedRightHandIkPos);
            computedRightHandIkRot = toTarget.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(toTarget.normalized, Vector3.up)
                : playerController.transform.rotation;

            // Hand IK açıksa kolu ve bileği anatomik hedefe yönlendir
            computedRightHandIkWeight = enableHandIK ? 0.72f : 0f;
            animController?.SetRightHandAimIK(computedRightHandIkPos, computedRightHandIkRot, computedRightHandIkWeight);

            // Fiziksel fener soketi ve ışık hüzmesi daima fiziksel fener objesiyle senkronize kalır
            if (animController.RightHandSocket != null)
            {
                Vector3 sockToAim = smoothedFlashlightAimPoint - animController.RightHandSocket.position;
                if (sockToAim.sqrMagnitude > 0.01f)
                {
                    Quaternion desiredWorldRot = Quaternion.LookRotation(sockToAim.normalized, Vector3.up);
                    // Anatomik bilek sınırı içinde soketi fener hedefine yumuşakça hizala
                    animController.RightHandSocket.rotation = Quaternion.Slerp(
                        animController.RightHandSocket.rotation, desiredWorldRot, 1f - Mathf.Exp(-flashlightAimSmooth * dt));
                }
            }
        }

        private void UpdateDebugVisuals()
        {
            if (!enableDebugVisuals)
            {
                if (debugVisualRoot != null && debugVisualRoot.activeSelf) debugVisualRoot.SetActive(false);
                return;
            }

            if (debugVisualRoot == null)
            {
                debugVisualRoot = new GameObject("_AnimationDebug_VisualMarkers");
                dbgEyeMarker = CreateDebugMarker("Marker_EyeAnchor", Color.cyan, 0.055f);
                dbgHandSocketMarker = CreateDebugMarker("Marker_RightHandSocket", Color.yellow, 0.055f);
                dbgAimTargetMarker = CreateDebugMarker("Marker_FlashlightIKTarget", new Color(1f, 0.55f, 0.1f), 0.065f);
                dbgLeftFootMarker = CreateDebugMarker("Marker_LeftFoot", Color.green, 0.06f);
                dbgRightFootMarker = CreateDebugMarker("Marker_RightFoot", Color.green, 0.06f);
                dbgInteractMarker = CreateDebugMarker("Marker_InteractTarget", Color.magenta, 0.07f);
            }
            if (!debugVisualRoot.activeSelf) debugVisualRoot.SetActive(true);

            if (eyeAnchor != null) dbgEyeMarker.position = eyeAnchor.position;
            if (animController != null && animController.RightHandSocket != null)
                dbgHandSocketMarker.position = animController.RightHandSocket.position;
            dbgAimTargetMarker.position = computedRightHandIkPos;
            if (boneLeftFoot != null) dbgLeftFootMarker.position = boneLeftFoot.position;
            if (boneRightFoot != null) dbgRightFootMarker.position = boneRightFoot.position;

            bool hasInteract = interactionActive && activeInteractionTarget != null;
            dbgInteractMarker.gameObject.SetActive(hasInteract);
            if (hasInteract) dbgInteractMarker.position = activeInteractionTarget.HandTarget.position;
        }

        private Transform CreateDebugMarker(string name, Color color, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(debugVisualRoot.transform, false);
            go.transform.localScale = Vector3.one * scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Standard"));
                mat.color = color;
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go.transform;
        }
    }
}
