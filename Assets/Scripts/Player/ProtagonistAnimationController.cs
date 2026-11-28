using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using TheLastBell.Interaction;

namespace TheLastBell.Player
{
    /// <summary>
    /// THE LAST BELL - Modüler 7 Katmanlı Karakter Animasyon Yöneticisi.
    /// Animator bileşeninin bulunduğu karakter modeli (Character_Model) üzerinde çalışır.
    /// Özellikler:
    /// - 7 bağımsız katman (Base, UpperBody, RightArm_Flashlight, LeftArm_Interaction, Head_Look, Additive_Reactions, FullBody_Override)
    /// - Mobil performanslı Foot IK (zemin adaptasyonu ve ayak gömülmesini önleme)
    /// - İzole klip önizleme için PlayableGraph desteği (AnimationDebugPanel)
    /// - Hata ayıklama için katman ağırlığı geçersiz kılma (override) ve canlı telemetri
    /// </summary>
    [DisallowMultipleComponent]
    public class ProtagonistAnimationController : MonoBehaviour
    {
        public const string LAYER_BASE = "Base Layer";
        public const string LAYER_UPPER_BODY = "UpperBody";
        public const string LAYER_RIGHT_ARM = "RightArm_Flashlight";
        public const string LAYER_LEFT_ARM = "LeftArm_Interaction";
        public const string LAYER_HEAD_LOOK = "Head_Look";
        public const string LAYER_ADDITIVE = "Additive_Reactions";
        public const string LAYER_FULL_OVERRIDE = "FullBody_Override";

        [Header("--- Bileşen Referansları ---")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform lookTargetOverride;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform rightHandAttachSocket;
        [SerializeField] private Transform rightHandFestekSocket;
        [SerializeField] private Transform leftHandFlashlightSocket;
        [SerializeField] private Transform leftHandInteractTarget;

        [Header("--- Katman Ağırlıkları & Geçiş Hızı ---")]
        [SerializeField] private float layerBlendSpeed = 10f;
        [SerializeField] private bool flashlightEquipped = true;
        [SerializeField] private bool rightHandHoldingCustomItem = false;
        [SerializeField] private bool leftHandHoldingFlashlight = false;
        [SerializeField] private float headLookAtWeight = 0.45f;
        [SerializeField] private float rightHandIkWeight = 0.0f;

        [Header("--- Ayak Zemin IK (Foot IK) ---")]
        [SerializeField] private bool enableFootIK = true;
        [SerializeField] private float footSoleOffset = 0.086f;
        [SerializeField] private float footRayHeight = 0.45f;
        [SerializeField] private float footRayDistance = 0.70f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("--- Timeline / Sinematik Entegrasyonu ---")]
        [SerializeField] private PlayableDirector activeDirector;

        private int idxUpperBody = -1;
        private int idxRightArm = -1;
        private int idxLeftArm = -1;
        private int idxHeadLook = -1;
        private int idxAdditive = -1;
        private int idxFullOverride = -1;

        private float leftArmTimer = 0f;
        private float headLookTimer = 0f;
        private float fullOverrideTimer = 0f;
        private float fearLevel = 0f;
        private bool isCinematicOverride = false;
        private bool isInitialized = false;
        private bool isGroundedCached = true;

        // Debug manuel katman ağırlığı geçersiz kılmaları (-1 = otomatik)
        private readonly float[] manualLayerWeights = new float[] { -1f, -1f, -1f, -1f, -1f, -1f, -1f };

        // İzole klip önizleme (PlayableGraph)
        private PlayableGraph previewGraph;
        private AnimationClipPlayable previewClipPlayable;
        private bool isIsolatedPreview = false;
        private bool previewPlaying = false;
        private bool previewLoop = true;
        private float previewSpeed = 1f;
        private AnimationClip currentPreviewClip;

        // Prosedürel IK hedefleri (CharacterProceduralController tarafından beslenir)
        private Vector3 rightHandIkTargetPos;
        private Quaternion rightHandIkTargetRot = Quaternion.identity;
        private float dynamicRightHandIkWeight = 0f;
        private float dynamicLeftHandIkWeight = 0f;
        private Vector3 leftHandAimIkPos;
        private Quaternion leftHandAimIkRot = Quaternion.identity;
        private float leftHandAimIkWeight = 0f;

        // Animator Parametre Hash'leri
        private static readonly int HashSpeed = Animator.StringToHash("Speed");
        private static readonly int HashStrafe = Animator.StringToHash("Strafe");
        private static readonly int HashMoveMag = Animator.StringToHash("MoveMagnitude");
        private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int HashLocomotionMult = Animator.StringToHash("LocomotionSpeedMultiplier");
        private static readonly int HashIsCrouching = Animator.StringToHash("IsCrouching");
        private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int HashIsExhausted = Animator.StringToHash("IsExhausted");
        private static readonly int HashFlashlightHeld = Animator.StringToHash("FlashlightHeld");
        private static readonly int HashFlashlightAim = Animator.StringToHash("FlashlightAim");
        private static readonly int HashFearLevel = Animator.StringToHash("FearLevel");
        private static readonly int HashInteract = Animator.StringToHash("Interact");
        private static readonly int HashPickUp = Animator.StringToHash("PickUp");
        private static readonly int HashDoorOpen = Animator.StringToHash("DoorOpen");
        private static readonly int HashWallSupport = Animator.StringToHash("WallSupport");
        private static readonly int HashImpactFlinch = Animator.StringToHash("ImpactFlinch");
        private static readonly int HashStartle = Animator.StringToHash("Startle");
        private static readonly int HashTurnLeft = Animator.StringToHash("TurnLeft");
        private static readonly int HashTurnRight = Animator.StringToHash("TurnRight");
        private static readonly int HashFall = Animator.StringToHash("Fall");
        private static readonly int HashGetUp = Animator.StringToHash("GetUp");
        private static readonly int HashStumble = Animator.StringToHash("Stumble");
        private static readonly int HashFlashlightRaise = Animator.StringToHash("FlashlightRaise");
        private static readonly int HashFlashlightLower = Animator.StringToHash("FlashlightLower");
        private static readonly int HashFlashlightRelease = Animator.StringToHash("FlashlightRelease");
        private static readonly int HashFlashlightPickup = Animator.StringToHash("FlashlightPickup");
        private static readonly int HashLookRight = Animator.StringToHash("LookRight");
        private static readonly int HashLookLeft = Animator.StringToHash("LookLeft");
        private static readonly int HashLookUp = Animator.StringToHash("LookUp");
        private static readonly int HashLookDown = Animator.StringToHash("LookDown");
        private static readonly int HashCinematicActive = Animator.StringToHash("CinematicActive");

        // Animasyon Olay Kancaları (Animation Events)
        public event Action OnFootstepEvent;
        public event Action OnFlashlightGripEvent;
        public event Action OnFlashlightReleaseEvent;
        public event Action OnDoorInteractEvent;
        public event Action OnDoorReleaseEvent;
        public event Action OnPickupEvent;
        public event Action OnFallImpactEvent;

        public Animator AnimatorComponent => animator;
        public bool IsFlashlightEquipped => flashlightEquipped;
        public bool IsRightHandHoldingCustomItem => rightHandHoldingCustomItem;
        public bool IsLeftHandHoldingFlashlight => leftHandHoldingFlashlight;
        public float FearLevel => fearLevel;
        public bool IsFullBodyOverrideActive => isCinematicOverride || fullOverrideTimer > 0f;
        public bool IsIsolatedPreviewActive => isIsolatedPreview;
        public bool IsPreviewPlaying => previewPlaying;
        public bool IsPreviewLoop => previewLoop;
        public float PreviewSpeed => previewSpeed;
        public AnimationClip CurrentPreviewClip => currentPreviewClip;
        public Transform RightHandSocket => rightHandAttachSocket;
        public Transform RightHandFestekSocket => rightHandFestekSocket;
        private struct WristContact
        {
            public Vector3 position;
            public Quaternion rotation;
            public float weight;
            public HandGripProfile grip;
        }

        private sealed class HandCalibration
        {
            public Transform wrist;
            public Vector3 palmInWristMetres;
            public Quaternion gripFrameInWrist;
            public Vector3 thumbRollAxis;
            public bool right;
            public HumanBodyBones[] digits;
            public Quaternion[] bindRotations;
            public Vector3[] bendAxes;
        }

        private WristContact leftContact;
        private float lowReach;
        private float flashlightGripWeight = 1f;
        public void SetFlashlightGripWeight(float weight) => flashlightGripWeight = Mathf.Clamp01(weight);
        private WristContact rightContact;
        private HandCalibration leftCalibration;
        private HandCalibration rightCalibration;

        public Transform LeftHandFestekSocket { get; private set; }
        public bool IsHoldingFestek { get; set; }
        public bool IsPosingFestek { get; set; }
        public Transform LeftHandFlashlightSocket => leftHandFlashlightSocket;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnDestroy()
        {
            StopIsolatedPreview();
        }

        public void InitializeIfNeeded()
        {
            if (isInitialized && animator != null) return;

            if (animator == null) animator = GetComponent<Animator>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

            if (animator != null)
            {
                animator.applyRootMotion = false;
                CacheLayerIndices();
                EnsureHandSocket();
            }

            isInitialized = true;
        }

        private void CacheLayerIndices()
        {
            if (animator == null) return;
            idxUpperBody = animator.GetLayerIndex(LAYER_UPPER_BODY);
            idxRightArm = animator.GetLayerIndex(LAYER_RIGHT_ARM);
            idxLeftArm = animator.GetLayerIndex(LAYER_LEFT_ARM);
            idxHeadLook = animator.GetLayerIndex(LAYER_HEAD_LOOK);
            idxAdditive = animator.GetLayerIndex(LAYER_ADDITIVE);
            idxFullOverride = animator.GetLayerIndex(LAYER_FULL_OVERRIDE);
        }

        private void EnsureHandSocket()
        {
            if (animator == null || !animator.isHuman) return;

            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
            {
                if (rightHandAttachSocket == null)
                {
                    Transform existing = rightHand.Find("Flashlight_Hand_Socket");
                    if (existing != null)
                    {
                        rightHandAttachSocket = existing;
                    }
                    else
                    {
                        var sock = new GameObject("Flashlight_Hand_Socket");
                        sock.transform.SetParent(rightHand, false);
                        sock.transform.localPosition = new Vector3(0.01f, 0.055f, 0.025f);
                        sock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        rightHandAttachSocket = sock.transform;
                    }
                }

                if (rightHandFestekSocket == null)
                {
                    Transform existingFestek = rightHand.Find("RightHand_Festek_Socket");
                    if (existingFestek != null)
                    {
                        rightHandFestekSocket = existingFestek;
                    }
                    else
                    {
                        var fsock = new GameObject("RightHand_Festek_Socket");
                        fsock.transform.SetParent(rightHand, false);
                        fsock.transform.localPosition = new Vector3(-0.015f, 0.065f, 0.030f);
                        fsock.transform.localRotation = Quaternion.Euler(85f, 0f, 0f);
                        rightHandFestekSocket = fsock.transform;
                    }
                }
            }

            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (leftHand != null && LeftHandFestekSocket == null)
            {
                LeftHandFestekSocket = leftHand.Find("LeftHand_Festek_Socket");
                if (LeftHandFestekSocket == null)
                {
                    LeftHandFestekSocket = new GameObject("LeftHand_Festek_Socket").transform;
                    LeftHandFestekSocket.SetParent(leftHand, false);
                    var index = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
                    var little = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
                    Vector3 palm = index != null && little != null ? (index.position + little.position) * 0.5f : leftHand.position;
                    Vector3 across = index != null && little != null ? (index.position - little.position).normalized : leftHand.up;
                    Vector3 palmLength = (palm - leftHand.position).normalized;
                    if (palmLength.sqrMagnitude < 0.5f) palmLength = leftHand.forward;
                    Vector3 palmNormal = Vector3.Cross(across, palmLength).normalized;
                    if (palmNormal.sqrMagnitude < 0.5f) palmNormal = leftHand.right;
                    LeftHandFestekSocket.SetPositionAndRotation(Vector3.Lerp(leftHand.position, palm, 0.71f) + palmNormal * 0.015f,
                        Quaternion.LookRotation(across, palmNormal));
                }
            }
            leftCalibration ??= CalibrateHand(true);
            rightCalibration ??= CalibrateHand(false);
            if (rightHandAttachSocket != null && rightCalibration != null)
            {
                var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                if (index != null && little != null)
                {
                    Vector3 axis = (index.position - little.position).normalized;
                    Vector3 support = rightHand.rotation * rightCalibration.gripFrameInWrist * Vector3.back;
                    Vector3 knuckles = (index.position + little.position) * 0.5f;
                    if (axis.sqrMagnitude > 0.5f)
                        rightHandAttachSocket.SetPositionAndRotation(
                            Vector3.Lerp(rightHand.position, knuckles, 0.71f) + support * 0.024f,
                            Quaternion.LookRotation(axis, support));
                }
            }
            if (leftHand != null && leftHandFlashlightSocket == null)
            {
                Transform existingLeft = leftHand.Find("LeftHand_Flashlight_Socket");
                if (existingLeft != null)
                {
                    leftHandFlashlightSocket = existingLeft;
                }
                else
                {
                    var lsock = new GameObject("LeftHand_Flashlight_Socket");
                    lsock.transform.SetParent(leftHand, false);
                    lsock.transform.localPosition = new Vector3(-0.01f, 0.055f, 0.025f);
                    lsock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    leftHandFlashlightSocket = lsock.transform;
                }
            }
        }

        private HandCalibration CalibrateHand(bool left)
        {
            var wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (wrist == null) return null;
            var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            Vector3 palm = middle != null ? Vector3.Lerp(wrist.position, middle.position, 0.74f) : wrist.position;
            var hand = new HandCalibration
            {
                wrist = wrist, right = !left, palmInWristMetres = Quaternion.Inverse(wrist.rotation) * (palm - wrist.position),
                digits = new HumanBodyBones[15], bindRotations = new Quaternion[15], bendAxes = new Vector3[15]
            };
            var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            hand.gripFrameInWrist = Quaternion.identity;
            if (index != null && little != null && (palm - wrist.position).sqrMagnitude > 0.00001f)
            {
                Vector3 alongFinger = (palm - wrist.position).normalized;
                Vector3 across = (index.position - little.position).normalized;
                if (!left) across = -across;
                Vector3 normal = Vector3.Cross(across, alongFinger).normalized;
                if (normal.sqrMagnitude > 0.5f)
                    hand.gripFrameInWrist = Quaternion.Inverse(wrist.rotation) * Quaternion.LookRotation(normal, alongFinger);
            }
            Vector3 palmNormal = wrist.rotation * hand.gripFrameInWrist * Vector3.forward;
            int thumb = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            for (int i = 0; i < hand.digits.Length; i++)
            {
                HumanBodyBones id = (HumanBodyBones)(thumb + i);
                hand.digits[i] = id;
                var joint = animator.GetBoneTransform(id);
                if (joint == null) continue;
                hand.bindRotations[i] = joint.localRotation;
                if (i == 0)
                {
                    Transform thumbSecond = animator.GetBoneTransform((HumanBodyBones)((int)id + 1));
                    if (thumbSecond != null)
                        hand.thumbRollAxis = joint.InverseTransformDirection((thumbSecond.position - joint.position).normalized);
                }
                if (i % 3 == 2)
                {
                    Transform middleJoint = animator.GetBoneTransform((HumanBodyBones)((int)id - 1));
                    if (middleJoint != null)
                        hand.bendAxes[i] = joint.InverseTransformDirection(middleJoint.TransformDirection(hand.bendAxes[i - 1]));
                    continue;
                }
                var next = animator.GetBoneTransform((HumanBodyBones)((int)id + 1));
                if (next == null) continue;
                Vector3 inward = i < 3 ? palm - joint.position : -palmNormal;
                var bend = Vector3.Cross(next.position - joint.position, inward);
                if (bend.sqrMagnitude > 0.00000001f)
                    hand.bendAxes[i] = joint.InverseTransformDirection(bend.normalized);
            }
            return hand;
        }

        public void SetPhysicalContact(PreferredInteractionHand side, Transform point, HandGripProfile profile, float weight)
        {
            if (point == null) return;
            InitializeIfNeeded();
            var hand = side == PreferredInteractionHand.Right ? rightCalibration : leftCalibration;
            if (hand == null) return;
            Quaternion rotation = point.rotation * Quaternion.Euler(profile.wristAngles) * Quaternion.Inverse(hand.gripFrameInWrist);
            Vector3 contact = point.TransformPoint(profile.palmOffset);
            Vector3 surface = point.forward * Mathf.Min(profile.objectRadius, 0.028f) * 0.55f;
            SetPhysicalWrist(side, contact + surface - rotation * hand.palmInWristMetres, rotation, profile, weight);
        }

        public void SetPhysicalWrist(PreferredInteractionHand side, Vector3 position, Quaternion rotation,
            HandGripProfile grip, float weight)
        {
            var request = new WristContact { position = position, rotation = rotation, weight = Mathf.Clamp01(weight), grip = grip };
            if (side == PreferredInteractionHand.Right) rightContact = request;
            else leftContact = request;
        }

        public void SetLowReach(float weight) => lowReach = Mathf.Clamp01(weight);

        public void PoseLeftHandForDevice(Vector3 gripCentre, Quaternion deviceRotation, float weight, HandGripProfile? profile = null)
        {
            if (LeftHandFestekSocket == null) return;
            Transform wrist = LeftHandFestekSocket.parent;
            Quaternion rotation = deviceRotation * Quaternion.Inverse(LeftHandFestekSocket.localRotation);
            Vector3 offset = Quaternion.Inverse(wrist.rotation) * (LeftHandFestekSocket.position - wrist.position);
            SetPhysicalWrist(PreferredInteractionHand.Left, gripCentre - rotation * offset, rotation, profile ?? HandGripProfile.Device, weight);
        }

        public bool IsPalmNear(PreferredInteractionHand side, Vector3 position, float tolerance)
        {
            var hand = side == PreferredInteractionHand.Right ? rightCalibration : leftCalibration;
            return hand != null && hand.wrist != null &&
                Vector3.Distance(hand.wrist.position + hand.wrist.rotation * hand.palmInWristMetres, position) <= tolerance;
        }

        public bool CanReachByArm(PreferredInteractionHand side, Vector3 contact, float tolerance)
        {
            if (animator == null || !animator.isHuman) return false;
            bool right = side == PreferredInteractionHand.Right;
            Transform shoulder = animator.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            Transform elbow = animator.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            Transform wrist = animator.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (shoulder == null || elbow == null || wrist == null) return false;
            float arm = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, wrist.position);
            return Vector3.Distance(shoulder.position, contact) <= arm + tolerance + 0.075f;
        }

        public void ClearPhysicalContact(PreferredInteractionHand side)
        {
            if (side == PreferredInteractionHand.Right) rightContact = default;
            else leftContact = default;
        }

        private void ApplyGrip(HandCalibration hand, HandGripProfile grip, float weight)
        {
            if (hand == null || hand.wrist == null) return;
            for (int i = 0; i < hand.digits.Length; i++)
            {
                if (hand.bendAxes[i].sqrMagnitude < 0.5f) continue;
                int segment = i % 3;
                Vector3 limits = grip.fingerJointLimits.sqrMagnitude > 0.01f ? grip.fingerJointLimits : new Vector3(37, 52, 24);
                Vector3 thumb = grip.thumbOpposition.sqrMagnitude > 0.01f ? grip.thumbOpposition : new Vector3(17, 27, 18);
                float bias = grip.fingerBias.sqrMagnitude > 0.01f ? grip.fingerBias[Mathf.Max(0, i / 3 - 1)] : 1f;
                float curl = i < 3 ? thumb[segment] * grip.thumbCurl : limits[segment] * grip.fingerCurl * bias;
                Quaternion flexion = Quaternion.AngleAxis(Mathf.Min(curl * weight, limits[segment]), hand.bendAxes[i]);
                Quaternion opposition = i == 0 && hand.thumbRollAxis.sqrMagnitude > 0.5f
                    ? Quaternion.AngleAxis((hand.right ? -1f : 1f) * 24f * grip.thumbCurl * weight, hand.thumbRollAxis)
                    : Quaternion.identity;
                animator.SetBoneLocalRotation(hand.digits[i], hand.bindRotations[i] * opposition * flexion);
            }
        }

        private void ApplyWrist(WristContact contact, bool right)
        {
            var goal = right ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            var elbow = right ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow;
            Vector3 hint = contact.grip.elbowOffset;
            if (!right) hint.x = -hint.x;
            animator.SetIKPositionWeight(goal, contact.weight);
            animator.SetIKRotationWeight(goal, contact.weight);
            animator.SetIKPosition(goal, contact.position);
            animator.SetIKRotation(goal, contact.rotation);
            animator.SetIKHintPositionWeight(elbow, contact.weight * 0.64f);
            animator.SetIKHintPosition(elbow, transform.root.TransformPoint(hint));
            ApplyGrip(right ? rightCalibration : leftCalibration, contact.grip, contact.weight);
        }

        private void Update()
        {
            if (isIsolatedPreview)
            {
                StepIsolatedPreview(Time.unscaledDeltaTime);
                return;
            }
            StepAnimationLayers(Time.deltaTime);
        }

        public void StepAnimationLayers(float dt)
        {
            if (animator == null) return;
            float safeDt = Mathf.Max(0.0001f, dt);

            if (leftArmTimer > 0f) leftArmTimer = Mathf.Max(0f, leftArmTimer - safeDt);
            if (headLookTimer > 0f) headLookTimer = Mathf.Max(0f, headLookTimer - safeDt);
            if (fullOverrideTimer > 0f) fullOverrideTimer = Mathf.Max(0f, fullOverrideTimer - safeDt);

            bool fullOverride = IsFullBodyOverrideActive;

            float targetUpper = fullOverride ? 0f : (fearLevel > 0.05f ? fearLevel : 0f);
            float targetRight = (!fullOverride && (flashlightEquipped || rightHandHoldingCustomItem || rightContact.weight > 0.01f || dynamicRightHandIkWeight > 0.01f)) ? 1f : 0f;
            float targetLeft = (!fullOverride && (leftArmTimer > 0f || leftContact.weight > 0.01f || leftHandHoldingFlashlight || IsHoldingFestek || IsPosingFestek || leftHandAimIkWeight > 0.01f)) ? 1f : 0f;
            float targetHead = (!fullOverride && (headLookTimer > 0f || lookTargetOverride != null)) ? 1f : 0f;
            float targetAdd = (!fullOverride && fearLevel > 0.1f) ? Mathf.Clamp01(fearLevel * 0.45f) : 0f;
            float targetFull = fullOverride ? 1f : 0f;

            BlendLayer(idxUpperBody, targetUpper, safeDt);
            BlendLayer(idxRightArm, targetRight, safeDt);
            BlendLayer(idxLeftArm, targetLeft, safeDt);
            BlendLayer(idxHeadLook, targetHead, safeDt);
            BlendLayer(idxAdditive, targetAdd, safeDt);
            BlendLayer(idxFullOverride, targetFull, safeDt);
        }

        private void BlendLayer(int layerIdx, float autoTarget, float dt)
        {
            if (layerIdx < 0 || layerIdx >= animator.layerCount) return;
            float target = (layerIdx < manualLayerWeights.Length && manualLayerWeights[layerIdx] >= 0f)
                ? manualLayerWeights[layerIdx]
                : autoTarget;

            float cur = animator.GetLayerWeight(layerIdx);
            float next = Mathf.MoveTowards(cur, target, dt * layerBlendSpeed);
            animator.SetLayerWeight(layerIdx, next);
        }

        public void SetManualLayerWeight(int layerIndex, float weight)
        {
            if (layerIndex < 0 || layerIndex >= manualLayerWeights.Length) return;
            manualLayerWeights[layerIndex] = weight;
            if (animator != null && layerIndex < animator.layerCount && weight >= 0f)
            {
                animator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
            }
        }

        public void ClearManualLayerWeights()
        {
            for (int i = 0; i < manualLayerWeights.Length; i++) manualLayerWeights[i] = -1f;
        }

        public void SetFootIKEnabled(bool enabled) => enableFootIK = enabled;

        public void SetRightHandCustomItemHeld(bool held)
        {
            rightHandHoldingCustomItem = held;
        }

        public void SetLeftHandFlashlightHeld(bool held)
        {
            leftHandHoldingFlashlight = held;
        }

        public void SetRightHandAimIK(Vector3 worldPos, Quaternion worldRot, float weight)
        {
            rightHandIkTargetPos = worldPos;
            rightHandIkTargetRot = worldRot;
            dynamicRightHandIkWeight = Mathf.Clamp01(weight);
        }

        public void SetLeftHandAimIK(Vector3 worldPos, Quaternion worldRot, float weight)
        {
            leftHandAimIkPos = worldPos;
            leftHandAimIkRot = worldRot;
            leftHandAimIkWeight = Mathf.Clamp01(weight);
        }

        public void SetLeftHandInteractionIK(Transform target, float weight)
        {
            leftHandInteractTarget = target;
            dynamicLeftHandIkWeight = Mathf.Clamp01(weight);
            if (weight > 0.01f) leftArmTimer = Mathf.Max(leftArmTimer, 0.25f);
        }

        public void ClearLeftHandInteractTarget()
        {
            leftHandInteractTarget = null;
            dynamicLeftHandIkWeight = 0f;
        }

        public void UpdateLocomotionParameters(float speed, float strafe, float moveMagnitude, float speedMultiplier, bool isCrouching, bool isGrounded, bool isExhausted)
        {
            if (animator == null || isIsolatedPreview) return;
            isGroundedCached = isGrounded;
            float clampedMag = Mathf.Clamp01(moveMagnitude);
            animator.SetFloat(HashSpeed, speed);
            animator.SetFloat(HashStrafe, strafe);
            animator.SetFloat(HashMoveMag, clampedMag);
            animator.SetBool(HashIsMoving, clampedMag > 0.08f);
            animator.SetFloat(HashLocomotionMult, Mathf.Clamp(speedMultiplier, 0.5f, 1.8f));
            animator.SetBool(HashIsCrouching, isCrouching);
            animator.SetBool(HashIsGrounded, isGrounded);
            animator.SetBool(HashIsExhausted, isExhausted);
            animator.SetBool(HashFlashlightHeld, flashlightEquipped);
        }

        public void SetFlashlightEquipped(bool equipped, bool aiming = false)
        {
            flashlightEquipped = equipped;
            if (animator != null)
            {
                animator.SetBool(HashFlashlightHeld, equipped);
                animator.SetBool(HashFlashlightAim, aiming);
            }
        }

        public void SetFearLevel(float fear01)
        {
            fearLevel = Mathf.Clamp01(fear01);
            if (animator != null) animator.SetFloat(HashFearLevel, fearLevel);
        }

        public void SetLookTarget(Transform target) => lookTargetOverride = target;

        public void TriggerTurnInPlace(int directionSign)
        {
            if (animator == null || isIsolatedPreview || IsFullBodyOverrideActive) return;
            animator.SetTrigger(directionSign < 0 ? HashTurnLeft : HashTurnRight);
        }

        public void TriggerStartle(float duration = 1.3f)
        {
            fullOverrideTimer = Mathf.Max(0.4f, duration);
            if (animator != null) animator.SetTrigger(HashStartle);
        }

        public void TriggerWallSupport(Transform wallPoint = null)
        {
            if (wallPoint != null) leftHandInteractTarget = wallPoint;
            leftArmTimer = 2.3f;
            if (animator != null) animator.SetTrigger(HashWallSupport);
        }

        public void TriggerLookRight(float duration = 0.8f)
        {
            headLookTimer = Mathf.Max(0.4f, duration);
            if (animator != null) animator.SetTrigger(HashLookRight);
        }

        public void TriggerLookLeft(float duration = 0.8f)
        {
            headLookTimer = Mathf.Max(0.4f, duration);
            if (animator != null) animator.SetTrigger(HashLookLeft);
        }

        public void TriggerLookUp(float duration = 0.8f)
        {
            headLookTimer = Mathf.Max(0.4f, duration);
            if (animator != null) animator.SetTrigger(HashLookUp);
        }

        public void TriggerLookDown(float duration = 0.8f)
        {
            headLookTimer = Mathf.Max(0.4f, duration);
            if (animator != null) animator.SetTrigger(HashLookDown);
        }

        public void TriggerFlashlightRaise()
        {
            flashlightEquipped = true;
            if (animator != null)
            {
                animator.SetBool(HashFlashlightHeld, true);
                animator.SetTrigger(HashFlashlightRaise);
            }
        }

        public void TriggerFlashlightLower()
        {
            if (animator != null) animator.SetTrigger(HashFlashlightLower);
        }

        public void TriggerFlashlightRelease()
        {
            if (animator != null) animator.SetTrigger(HashFlashlightRelease);
        }

        public void TriggerFlashlightPickup(float duration = 2.5f)
        {
            fullOverrideTimer = Mathf.Max(0.8f, duration);
            if (animator != null) animator.SetTrigger(HashFlashlightPickup);
        }

        public void TriggerDoorInteract(Transform doorHandle = null)
        {
            if (doorHandle != null) leftHandInteractTarget = doorHandle;
            leftArmTimer = 2.0f;
            if (animator != null)
            {
                animator.SetTrigger(HashDoorOpen);
                animator.SetTrigger(HashInteract);
            }
        }

        public void TriggerItemPickup(Transform itemTarget = null)
        {
            if (itemTarget != null) leftHandInteractTarget = itemTarget;
            leftArmTimer = 1.2f;
            if (animator != null) animator.SetTrigger(HashPickUp);
        }

        public void TriggerImpactFlinch()
        {
            if (animator != null) animator.SetTrigger(HashImpactFlinch);
        }

        public void TriggerStumble(float duration = 1.35f)
        {
            fullOverrideTimer = Mathf.Max(0.5f, duration);
            if (animator != null) animator.SetTrigger(HashStumble);
        }

        public void TriggerFall(float duration = 1.75f)
        {
            fullOverrideTimer = Mathf.Max(0.5f, duration);
            if (animator != null) animator.SetTrigger(HashFall);
        }

        public void TriggerGetUp(float duration = 3.0f)
        {
            fullOverrideTimer = Mathf.Max(0.5f, duration);
            if (animator != null) animator.SetTrigger(HashGetUp);
        }

        public void SetCinematicOverride(bool active, PlayableDirector director = null)
        {
            isCinematicOverride = active;
            activeDirector = director;
            if (animator != null) animator.SetBool(HashCinematicActive, active);
            if (director != null)
            {
                if (active) director.Play();
                else director.Stop();
            }
        }

        public void ResetAnimationState()
        {
            StopIsolatedPreview();
            ClearManualLayerWeights();
            leftArmTimer = 0f;
            headLookTimer = 0f;
            fullOverrideTimer = 0f;
            isCinematicOverride = false;
            fearLevel = 0f;
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        #region Isolated Animation Preview (PlayableGraph)
        public void StartIsolatedPreview(AnimationClip clip, bool loop)
        {
            InitializeIfNeeded();
            if (animator == null || clip == null) return;

            StopIsolatedPreview();

            currentPreviewClip = clip;
            previewLoop = loop;
            previewPlaying = true;
            isIsolatedPreview = true;

            previewGraph = PlayableGraph.Create("TLB_IsolatedPreview");
            previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(previewGraph, "PreviewOutput", animator);
            previewClipPlayable = AnimationClipPlayable.Create(previewGraph, clip);
            previewClipPlayable.SetApplyFootIK(true);
            previewClipPlayable.SetApplyPlayableIK(false);
            output.SetSourcePlayable(previewClipPlayable);
            previewGraph.Play();
            previewClipPlayable.SetTime(0.0);
            previewGraph.Evaluate(0f);
        }

        public void StepIsolatedPreview(float dt)
        {
            if (!isIsolatedPreview || !previewGraph.IsValid() || currentPreviewClip == null) return;
            float dur = Mathf.Max(0.01f, currentPreviewClip.length);

            if (previewPlaying)
            {
                double nextTime = previewClipPlayable.GetTime() + dt * previewSpeed;
                if (nextTime >= dur)
                {
                    if (previewLoop) nextTime %= dur;
                    else
                    {
                        nextTime = dur;
                        previewPlaying = false;
                    }
                }
                previewClipPlayable.SetTime(nextTime);
            }
            previewGraph.Evaluate(0f);
        }

        public void SetPreviewPlaying(bool playing) => previewPlaying = playing;
        public void SetPreviewLoop(bool loop) => previewLoop = loop;
        public void SetPreviewSpeed(float speed) => previewSpeed = Mathf.Clamp(speed, 0.25f, 2.0f);

        public float GetPreviewNormalizedTime()
        {
            if (!isIsolatedPreview || !previewGraph.IsValid() || currentPreviewClip == null || currentPreviewClip.length <= 0.001f)
                return 0f;
            return Mathf.Clamp01((float)(previewClipPlayable.GetTime() / currentPreviewClip.length));
        }

        public void SetPreviewNormalizedTime(float normalized01)
        {
            if (!isIsolatedPreview || !previewGraph.IsValid() || currentPreviewClip == null) return;
            double t = Mathf.Clamp01(normalized01) * currentPreviewClip.length;
            previewClipPlayable.SetTime(t);
            previewGraph.Evaluate(0f);
        }

        public void StopIsolatedPreview()
        {
            if (previewGraph.IsValid())
            {
                previewGraph.Destroy();
            }
            isIsolatedPreview = false;
            previewPlaying = false;
            currentPreviewClip = null;
        }
        #endregion

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman || isIsolatedPreview) return;

            if (layerIndex == 0 && lowReach > 0f && !IsFullBodyOverrideActive)
            {
                Transform actor = transform.root;
                animator.bodyPosition += actor.forward * (0.16f * lowReach) - Vector3.up * (0.31f * lowReach);
                animator.bodyRotation = Quaternion.AngleAxis(29f * lowReach, actor.right) * animator.bodyRotation;
            }

            // Layer 0: Foot IK (yalnızca yerdeyken ve tam gövde override yokken)
            if (layerIndex == 0 && enableFootIK && isGroundedCached && !IsFullBodyOverrideActive)
            {
                ApplyFootIK(AvatarIKGoal.LeftFoot);
                ApplyFootIK(AvatarIKGoal.RightFoot);
            }

            if (IsFullBodyOverrideActive) return;

            // Layer 4: Head_Look -> Kamera veya hedef takibi
            if (layerIndex == idxHeadLook && lookTargetOverride != null)
            {
                animator.SetLookAtWeight(headLookAtWeight, 0.1f, 0.7f, 0.9f, 0.5f);
                animator.SetLookAtPosition(lookTargetOverride.position);
            }

            if (layerIndex == idxRightArm && rightContact.weight > 0.01f)
            {
                ApplyWrist(rightContact, true);
                return;
            }
            if (layerIndex == idxLeftArm && leftContact.weight > 0.01f)
            {
                ApplyWrist(leftContact, false);
                return;
            }

            // Layer 2: RightArm_Flashlight -> Dinamik fener nişan alma veya sağ el özel nesne (Feştek / şalter) IK'sı
            if (layerIndex == idxRightArm && (flashlightEquipped || rightHandHoldingCustomItem || dynamicRightHandIkWeight > 0.01f))
            {
                float w = Mathf.Max(rightHandIkWeight, dynamicRightHandIkWeight);
                if (w > 0.01f)
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
                    animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandIkTargetPos);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightHand, w * 0.72f);
                    animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandIkTargetRot);
                    if (flashlightEquipped && !leftHandHoldingFlashlight)
                        ApplyGrip(rightCalibration, HandGripProfile.Flashlight, 0.92f * flashlightGripWeight);
                }
            }

            // Layer 3: LeftArm_Interaction -> Çevresel etkileşim (kapı/duvar) veya sol ele aktarılmış el feneri IK'sı
            if (layerIndex == idxLeftArm)
            {
                if (IsHoldingFestek || IsPosingFestek)
                    ApplyGrip(leftCalibration, HandGripProfile.ForDevice(CameraModeController.Instance != null &&
                        CameraModeController.Instance.IsFreeOrbit ? DeviceGripAction.Inspect : DeviceGripAction.Carry),
                        IsHoldingFestek ? 1f : dynamicLeftHandIkWeight);
                if (leftHandInteractTarget != null)
                {
                    float w = dynamicLeftHandIkWeight > 0.01f
                        ? dynamicLeftHandIkWeight
                        : (leftArmTimer > 0f ? Mathf.Clamp01(leftArmTimer * 2f) * 0.70f : 0f);
                    if (w > 0.01f)
                    {
                        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
                        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandInteractTarget.position);
                        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, IsPosingFestek ? w : w * 0.55f);
                        animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandInteractTarget.rotation);
                    }
                }
                else if (IsHoldingFestek)
                {
                    var actor = transform.root;
                    Vector3 carry = actor.TransformPoint(new Vector3(-0.24f, animator.GetBool(HashIsCrouching) ? 0.82f : 1.26f, 0.42f));
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0.82f);
                    animator.SetIKPosition(AvatarIKGoal.LeftHand, carry);
                    if (LeftHandFestekSocket != null)
                    {
                        var itemRotation = actor.rotation * Quaternion.Euler(-19f, 12f, 8f);
                        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0.73f);
                        animator.SetIKRotation(AvatarIKGoal.LeftHand, itemRotation * Quaternion.Inverse(LeftHandFestekSocket.localRotation));
                    }
                }
                else if (leftHandAimIkWeight > 0.01f)
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandAimIkWeight);
                    animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandAimIkPos);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftHandAimIkWeight * 0.65f);
                    animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandAimIkRot);
                }
            }
        }

        private void ApplyFootIK(AvatarIKGoal footGoal)
        {
            Vector3 footPos = animator.GetIKPosition(footGoal);
            Vector3 rayOrigin = footPos + Vector3.up * footRayHeight;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, footRayHeight + footRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(transform.root))
                {
                    float targetY = hit.point.y + footSoleOffset;
                    // Yalnızca ayak zemine yakınsa (adım atarken havadaki ayağı aşağı çekme)
                    if (footPos.y <= targetY + 0.08f)
                    {
                        // Settle a planted idle shoe without pulling down a swinging locomotion foot.
                        float soleY = Mathf.Max(footPos.y, targetY);
                        if (!animator.GetBool(HashIsMoving) && footPos.y > targetY)
                            soleY = Mathf.Max(targetY, footPos.y - 0.025f);
                        animator.SetIKPositionWeight(footGoal, 0.85f);
                        animator.SetIKPosition(footGoal, new Vector3(footPos.x, soleY, footPos.z));

                        Quaternion footRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * animator.GetIKRotation(footGoal);
                        animator.SetIKRotationWeight(footGoal, 0.6f);
                        animator.SetIKRotation(footGoal, footRot);
                        return;
                    }
                }
            }

            animator.SetIKPositionWeight(footGoal, 0f);
            animator.SetIKRotationWeight(footGoal, 0f);
        }

        #region Unity Animation Event Callbacks
        public void AnimEvent_Footstep() => OnFootstepEvent?.Invoke();
        public void AnimEvent_FlashlightGrip() => OnFlashlightGripEvent?.Invoke();
        public void AnimEvent_FlashlightRelease() => OnFlashlightReleaseEvent?.Invoke();
        public void AnimEvent_DoorInteract() => OnDoorInteractEvent?.Invoke();
        public void AnimEvent_DoorRelease() => OnDoorReleaseEvent?.Invoke();
        public void AnimEvent_Pickup() => OnPickupEvent?.Invoke();
        public void AnimEvent_FallImpact() => OnFallImpactEvent?.Invoke();
        #endregion
    }
}
