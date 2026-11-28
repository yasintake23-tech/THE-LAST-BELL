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
        public bool enableToeAlignment = true;
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
        private Transform boneLeftShoulder;
        private Transform boneRightShoulder;
        private Transform boneRightHand;
        private Transform boneLeftHand;
        private Transform boneLeftFoot;
        private Transform boneRightFoot;
        private Transform boneLeftToes;
        private Transform boneRightToes;
        private Transform boneLeftToeEnd;
        private Transform boneRightToeEnd;
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

        // Aktif çevresel etkileşim (kapı kolu, şalter kolu, yerden alma)
        private InteractionTarget activeInteractionTarget;
        private float interactionElapsed = 0f;
        private bool interactionActive = false;

        // Sağ el açık hedef geçersiz kılması (Feştek soket yerleştirme / buton basma aşamaları için)
        private bool rightHandExplicitActive = false;
        private Vector3 rightHandExplicitPos;
        private Quaternion rightHandExplicitRot = Quaternion.identity;
        private float rightHandExplicitWeight = 0f;

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
            boneLeftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            boneRightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            boneRightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            boneLeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            boneLeftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            boneRightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            boneLeftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            boneRightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);

            if (boneLeftToes != null && boneLeftToes.childCount > 0)
                boneLeftToeEnd = boneLeftToes.Find("LeftToe_End") ?? boneLeftToes.GetChild(0);
            if (boneRightToes != null && boneRightToes.childCount > 0)
                boneRightToeEnd = boneRightToes.Find("RightToe_End") ?? boneRightToes.GetChild(0);
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
                    animController.TriggerItemPickup(null);
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

        public void SetRightHandExplicitOverride(Vector3 worldPos, Quaternion worldRot, float weight)
        {
            rightHandExplicitPos = worldPos;
            rightHandExplicitRot = worldRot;
            rightHandExplicitWeight = Mathf.Clamp01(weight);
            rightHandExplicitActive = rightHandExplicitWeight > 0.005f;
        }

        public void ClearRightHandExplicitOverride()
        {
            rightHandExplicitActive = false;
            rightHandExplicitWeight = 0f;
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

            // 4. Ayak ucu (LeftToes / RightToes) düz taban zemin hizalaması (yukarı kalkık parmak ucunu düzeltir)
            if (enableToeAlignment && !fullOverride)
            {
                ApplyToeSoleAlignment();
            }

            // 5. Aktif çevresel etkileşim IK zamanlaması (kapı kolu / şalter kolu / nesne teması)
            UpdateEnvironmentalInteraction(safeDt);

            // 6. Dinamik el feneri (sağ veya sol el) + sağ el Feştek taşıma & duvar kaçınma koordinasyonu
            UpdateDualHandItemAndFlashlightIK(safeDt, isolated || fullOverride);

            // 7. Opsiyonel geliştirme görselleştirme işaretçileri
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

        private void ApplyToeSoleAlignment()
        {
            Transform root = playerController != null ? playerController.transform : transform.root;
            float rootY = root != null ? root.position.y : transform.position.y;
            AlignSingleFootToe(boneLeftFoot, boneLeftToes, boneLeftToeEnd, rootY);
            AlignSingleFootToe(boneRightFoot, boneRightToes, boneRightToeEnd, rootY);
        }

        private void AlignSingleFootToe(Transform footBone, Transform toeBone, Transform toeEndBone, float rootY)
        {
            if (footBone == null || toeBone == null) return;

            float footRelY = footBone.position.y - rootY;
            // Sadece ayak zemine temas halindeyken / yakınken düzelt (havadaki adımı bozma)
            if (footRelY > 0.165f) return;

            float stanceWeight = Mathf.Clamp01((0.165f - footRelY) / 0.055f);

            // 1. Aşama: Ayak bileği (LeftFoot/RightFoot) yukarı dikilmişse tarak kemiğini (ToeBase) doğal taban yüksekliğine (~4.4cm) indir
            Vector3 anklePos = footBone.position;
            Vector3 ballPos = toeBone.position;
            Vector3 ankleToBall = ballPos - anklePos;
            float desiredBallY = rootY + 0.044f;
            if (ballPos.y > desiredBallY + 0.002f && ankleToBall.sqrMagnitude > 0.001f)
            {
                Vector3 horiz1 = Vector3.ProjectOnPlane(ankleToBall, Vector3.up);
                if (horiz1.sqrMagnitude > 0.0004f)
                {
                    float len1 = ankleToBall.magnitude;
                    float dy1 = Mathf.Clamp(desiredBallY - anklePos.y, -len1 * 0.85f, len1 * 0.85f);
                    float hMag1 = Mathf.Sqrt(Mathf.Max(0.0004f, len1 * len1 - dy1 * dy1));
                    Vector3 targetVec1 = horiz1.normalized * hMag1 + Vector3.up * dy1;
                    Quaternion deltaFoot = Quaternion.FromToRotation(ankleToBall.normalized, targetVec1.normalized);
                    footBone.rotation = Quaternion.Slerp(Quaternion.identity, deltaFoot, stanceWeight) * footBone.rotation;
                }
            }

            // 2. Aşama: Parmak ucu (LeftToe_End/RightToe_End) tarak kemiğinden yukarı kalkıksa zemine paralel (~3.8cm) yatır
            ballPos = toeBone.position;
            Vector3 endPos = toeEndBone != null ? toeEndBone.position : (ballPos + (ballPos - footBone.position).normalized * 0.095f);
            Vector3 ballToTip = endPos - ballPos;
            float desiredTipY = Mathf.Clamp(ballPos.y - 0.006f, rootY + 0.028f, rootY + 0.042f);
            if (endPos.y > desiredTipY + 0.001f && ballToTip.sqrMagnitude > 0.0004f)
            {
                Vector3 horiz2 = Vector3.ProjectOnPlane(ballToTip, Vector3.up);
                if (horiz2.sqrMagnitude > 0.0002f)
                {
                    float len2 = ballToTip.magnitude;
                    float dy2 = Mathf.Clamp(desiredTipY - ballPos.y, -len2 * 0.75f, len2 * 0.75f);
                    float hMag2 = Mathf.Sqrt(Mathf.Max(0.0002f, len2 * len2 - dy2 * dy2));
                    Vector3 targetVec2 = horiz2.normalized * hMag2 + Vector3.up * dy2;
                    Quaternion deltaToe = Quaternion.FromToRotation(ballToTip.normalized, targetVec2.normalized);
                    toeBone.rotation = Quaternion.Slerp(Quaternion.identity, deltaToe, stanceWeight) * toeBone.rotation;
                }
            }
        }

        private void UpdateEnvironmentalInteraction(float dt)
        {
            if (!interactionActive || activeInteractionTarget == null) return;

            interactionElapsed += dt;
            float dur = Mathf.Max(0.2f, activeInteractionTarget.TotalDuration);
            float norm = interactionElapsed / dur;

            float contactIn = Mathf.Clamp01((norm - (activeInteractionTarget.ContactNormalizedTime - 0.16f)) / 0.16f);
            float contactOut = 1f - Mathf.Clamp01((norm - activeInteractionTarget.ReleaseNormalizedTime) / 0.18f);
            float ikWeight = enableHandIK ? Mathf.Clamp01(Mathf.Min(contactIn, contactOut)) : 0f;

            if (activeInteractionTarget.PreferredHand == PreferredInteractionHand.Left)
            {
                animController?.SetLeftHandInteractionIK(activeInteractionTarget.HandTarget, ikWeight);
            }
            else if (activeInteractionTarget.HandTarget != null)
            {
                SetRightHandExplicitOverride(activeInteractionTarget.HandTarget.position, activeInteractionTarget.HandTarget.rotation, ikWeight);
            }

            if (norm >= 1f)
            {
                if (activeInteractionTarget.PreferredHand == PreferredInteractionHand.Right)
                {
                    ClearRightHandExplicitOverride();
                }
                CancelEnvironmentalInteraction();
            }
        }

        private void UpdateDualHandItemAndFlashlightIK(float dt, bool suppressed)
        {
            if (suppressed || playerController == null || animController == null)
            {
                computedRightHandIkWeight = 0f;
                animController?.SetRightHandAimIK(Vector3.zero, Quaternion.identity, 0f);
                animController?.SetLeftHandAimIK(Vector3.zero, Quaternion.identity, 0f);
                return;
            }

            Vector3 gazeOrigin = eyeAnchor != null
                ? eyeAnchor.position
                : (playerController.transform.position + Vector3.up * (playerController.IsCrouching ? 1.0f : 1.55f));
            Vector3 gazeDir = playerController.DesiredLookDirection;
            Vector3 rightVec = playerController.transform.right;
            Vector3 upVec = Vector3.up;

            // Duvar kaçınma SphereCast'i
            Vector3 shoulderR = boneRightShoulder != null
                ? boneRightShoulder.position
                : (gazeOrigin + rightVec * 0.22f - upVec * 0.18f);

            float targetPullback = 0f;
            if (Physics.SphereCast(shoulderR, wallAvoidRadius, gazeDir, out RaycastHit hit, wallAvoidCheckDistance, wallAvoidMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(playerController.transform))
                {
                    targetPullback = Mathf.Clamp01(1f - (hit.distance / wallAvoidCheckDistance));
                }
            }
            wallPullback01 = Mathf.Lerp(wallPullback01, targetPullback, 1f - Mathf.Exp(-14f * dt));

            // Doğal fener hedef noktası
            Vector3 rawAimPoint = gazeOrigin + gazeDir * 10f;
            if (smoothedFlashlightAimPoint == Vector3.zero) smoothedFlashlightAimPoint = rawAimPoint;
            smoothedFlashlightAimPoint = Vector3.Lerp(smoothedFlashlightAimPoint, rawAimPoint, 1f - Mathf.Exp(-flashlightAimSmooth * dt));

            bool flashlightNotDropped = (flashlightPhysical == null || flashlightPhysical.CurrentState != FlashlightPhysicalState.DroppedPhysics);
            bool flashlightInLeftHand = animController.IsLeftHandHoldingFlashlight && flashlightNotDropped;
            bool flashlightInRightHand = animController.IsFlashlightEquipped && !flashlightInLeftHand && flashlightNotDropped;
            bool holdingCustomRightItem = animController.IsRightHandHoldingCustomItem;

            // --- SOL EL: Aktarılmış El Feneri Nişan Alma (Sağ el Feştek tutarken çakışmasız aydınlatma!) ---
            if (flashlightInLeftHand && enableFlashlightAiming)
            {
                Vector3 shoulderL = boneLeftShoulder != null
                    ? boneLeftShoulder.position
                    : (gazeOrigin - rightVec * 0.22f - upVec * 0.18f);
                float fwdL = Mathf.Lerp(0.40f, 0.18f, wallPullback01);
                float vertL = Mathf.Clamp(gazeDir.y * 0.20f, -0.16f, 0.20f);
                float latL = Mathf.Lerp(-0.18f, -0.12f, wallPullback01);

                Vector3 leftIkPos = shoulderL + gazeDir * fwdL + rightVec * latL + upVec * (vertL - 0.09f);
                Vector3 leftToAim = smoothedFlashlightAimPoint - leftIkPos;
                Quaternion leftIkRot = leftToAim.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(leftToAim.normalized, upVec)
                    : playerController.transform.rotation;

                animController.SetLeftHandAimIK(leftIkPos, leftIkRot, enableHandIK ? 0.72f : 0f);

                if (animController.LeftHandFlashlightSocket != null)
                {
                    Vector3 sockToAim = smoothedFlashlightAimPoint - animController.LeftHandFlashlightSocket.position;
                    if (sockToAim.sqrMagnitude > 0.01f)
                    {
                        Quaternion desiredWorldRot = Quaternion.LookRotation(sockToAim.normalized, upVec);
                        animController.LeftHandFlashlightSocket.rotation = Quaternion.Slerp(
                            animController.LeftHandFlashlightSocket.rotation, desiredWorldRot, 1f - Mathf.Exp(-flashlightAimSmooth * dt));
                    }
                }
            }
            else
            {
                animController.SetLeftHandAimIK(Vector3.zero, Quaternion.identity, 0f);
            }

            // --- SAĞ EL: 1) Açık Etkileşim Hedefi (Şalter / Soket / Buton) -> 2) Feştek Tutuşu -> 3) Normal El Feneri ---
            if (rightHandExplicitActive && rightHandExplicitWeight > 0.005f)
            {
                computedRightHandIkPos = rightHandExplicitPos;
                computedRightHandIkRot = rightHandExplicitRot;
                computedRightHandIkWeight = enableHandIK ? rightHandExplicitWeight : 0f;
                animController.SetRightHandAimIK(computedRightHandIkPos, computedRightHandIkRot, computedRightHandIkWeight);
                return;
            }

            if (holdingCustomRightItem)
            {
                float fwdItem = Mathf.Lerp(0.36f, 0.18f, wallPullback01);
                float vertItem = Mathf.Clamp(gazeDir.y * 0.18f, -0.18f, 0.16f);
                computedRightHandIkPos = shoulderR + gazeDir * fwdItem + rightVec * 0.16f + upVec * (vertItem - 0.13f);
                computedRightHandIkRot = Quaternion.LookRotation(gazeDir, upVec);
                computedRightHandIkWeight = enableHandIK ? 0.78f : 0f;
                animController.SetRightHandAimIK(computedRightHandIkPos, computedRightHandIkRot, computedRightHandIkWeight);
                return;
            }

            if (!flashlightInRightHand || !enableFlashlightAiming)
            {
                computedRightHandIkWeight = 0f;
                animController.SetRightHandAimIK(Vector3.zero, Quaternion.identity, 0f);
                return;
            }

            float forwardReach = Mathf.Lerp(0.39f, 0.18f, wallPullback01);
            float verticalOffset = Mathf.Clamp(gazeDir.y * 0.29f, -0.23f, 0.24f);
            Vector3 flatAim = Vector3.ProjectOnPlane(gazeDir, upVec).normalized;
            if (flatAim.sqrMagnitude < 0.5f) flatAim = playerController.transform.forward;
            Vector3 gripPosition = shoulderR + flatAim * forwardReach + rightVec * 0.12f + upVec * (verticalOffset - 0.085f);
            Vector3 beam = (smoothedFlashlightAimPoint - gripPosition).normalized;
            Vector3 stableUp = Vector3.Cross(beam, rightVec);
            if (stableUp.sqrMagnitude < 0.1f) stableUp = upVec;
            Quaternion beamRotation = Quaternion.LookRotation(beam.sqrMagnitude > 0.5f ? beam : flatAim, stableUp);
            Transform mount = animController.RightHandSocket;
            if (mount != null && mount.parent != null)
            {
                // Solve the wrist for the fixed grip socket. Rotating the socket itself twists the torch through the fingers.
                Transform wrist = mount.parent;
                Quaternion wristRotation = beamRotation * Quaternion.Inverse(mount.localRotation);
                Vector3 wristToMount = Quaternion.Inverse(wrist.rotation) * (mount.position - wrist.position);
                computedRightHandIkRot = wristRotation;
                computedRightHandIkPos = gripPosition - wristRotation * wristToMount;
            }
            else
            {
                computedRightHandIkPos = gripPosition;
                computedRightHandIkRot = beamRotation;
            }
            computedRightHandIkWeight = enableHandIK ? 0.87f : 0f;
            animController.SetRightHandAimIK(computedRightHandIkPos, computedRightHandIkRot, computedRightHandIkWeight);
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
