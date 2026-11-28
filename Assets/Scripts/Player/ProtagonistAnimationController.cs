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
    [DefaultExecutionOrder(80)]
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

        [Serializable]
        private sealed class GripPoseData
        {
            public string id;
            public float[] joints;
        }

        [Serializable]
        private sealed class GripPoseLibrary { public GripPoseData[] poses; }
        private static float[][] authoredGrips;

        private sealed class HandCalibration
        {
            public Transform wrist;
            public Vector3 palmInWristMetres;
            public Quaternion gripFrameInWrist;
            public Vector3 cylinderCentre;
            public Quaternion cylinderFrame;
            public Transform[] digits;
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
                }
            }
            leftCalibration ??= CalibrateHand(true);
            rightCalibration ??= CalibrateHand(false);
            ApplyCylinderMount(rightHandAttachSocket, rightCalibration, 0.0305f);
            ApplyCylinderMount(LeftHandFestekSocket, leftCalibration, 0.026f);
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

        private static void ApplyCylinderMount(Transform socket, HandCalibration hand, float radiusAndSpacing)
        {
            if (socket == null || hand == null) return;
            socket.localPosition = hand.wrist.InverseTransformPoint(hand.cylinderCentre +
                hand.wrist.rotation * (hand.gripFrameInWrist * Vector3.forward) * radiusAndSpacing);
            socket.localRotation = hand.cylinderFrame;
        }

        private HandCalibration CalibrateHand(bool left)
        {
            var wrist = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var index = animator.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var middle = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var little = animator.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var thumbTip = animator.GetBoneTransform(left ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal);
            if (wrist == null || index == null || middle == null || little == null || thumbTip == null) return null;

            Vector3 knuckles = (index.position + little.position) * 0.5f;
            Vector3 along = (middle.position - wrist.position).normalized;
            Vector3 across = (index.position - little.position).normalized;
            Vector3 inward = Vector3.Cross(along, across).normalized;
            if (Vector3.Dot(inward, thumbTip.position - wrist.position) < 0f) inward = -inward;
            var hand = new HandCalibration
            {
                wrist = wrist,
                palmInWristMetres = Quaternion.Inverse(wrist.rotation) * (knuckles - along * 0.027f - wrist.position),
                cylinderCentre = knuckles - along * 0.027f,
                gripFrameInWrist = Quaternion.Inverse(wrist.rotation) * Quaternion.LookRotation(inward, along),
                cylinderFrame = Quaternion.Inverse(wrist.rotation) * Quaternion.LookRotation(across, inward),
                digits = new Transform[15], bindRotations = new Quaternion[15], bendAxes = new Vector3[15]
            };

            int first = (int)(left ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            for (int i = 0; i < hand.digits.Length; i++)
            {
                Transform joint = animator.GetBoneTransform((HumanBodyBones)(first + i));
                if (joint == null) continue;
                hand.digits[i] = joint;
                hand.bindRotations[i] = joint.localRotation;
                Transform next = i % 3 == 2
                    ? joint.childCount > 0 ? joint.GetChild(0) : null
                    : animator.GetBoneTransform((HumanBodyBones)(first + i + 1));
                Vector3 segment = next != null ? next.position - joint.position : joint.position - joint.parent.position;
                Vector3 flexionDirection = i < 3 ? knuckles + inward * 0.024f - joint.position : inward;
                Vector3 axis = Vector3.Cross(segment, flexionDirection);
                if (axis.sqrMagnitude > 0.000000001f)
                    hand.bendAxes[i] = joint.InverseTransformDirection(axis.normalized);
            }
            return hand;
        }

        public void SetPhysicalContact(PreferredInteractionHand side, Transform point, HandGripProfile profile, float weight)
        {
            if (point == null) return;
            InitializeIfNeeded();
            var hand = side == PreferredInteractionHand.Right ? rightCalibration : leftCalibration;
            if (hand == null) return;
            // The marker's forward points at the user's side of the handle. The palm faces the handle.
            Quaternion destination = Quaternion.LookRotation(-point.forward, -point.up);
            Quaternion rotation = destination * Quaternion.Inverse(hand.gripFrameInWrist);
            Vector3 contact = point.position + point.forward * (profile.objectRadius + 0.007f);
            SetPhysicalWrist(side, contact - rotation * hand.palmInWristMetres, rotation, profile, weight);
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

        public void ClearPhysicalContact(PreferredInteractionHand side)
        {
            if (side == PreferredInteractionHand.Right) rightContact = default;
            else leftContact = default;
        }

        private static float[][] LoadGripPoses()
        {
            var poses = new float[Enum.GetValues(typeof(AuthoredGrip)).Length][];
            TextAsset asset = Resources.Load<TextAsset>("Chapter1HandPoses");
            if (asset == null) return poses;
            var library = JsonUtility.FromJson<GripPoseLibrary>(asset.text);
            if (library?.poses == null) return poses;
            foreach (var entry in library.poses)
                if (entry != null && entry.joints?.Length == 15 && Enum.TryParse(entry.id, out AuthoredGrip pose) &&
                    (int)pose >= 0 && (int)pose < poses.Length)
                    poses[(int)pose] = entry.joints;
            return poses;
        }

        private static void ApplyGrip(HandCalibration hand, HandGripProfile grip, float weight, AuthoredGrip opening)
        {
            if (hand == null || hand.wrist == null) return;
            authoredGrips ??= LoadGripPoses();
            float[] closed = authoredGrips[(int)grip.pose];
            float[] open = authoredGrips[(int)opening];
            if (closed == null || open == null) return;
            float progress = Mathf.Clamp01(grip.closure * weight);
            for (int i = 0; i < hand.digits.Length; i++)
            {
                Transform joint = hand.digits[i];
                if (joint == null || hand.bendAxes[i].sqrMagnitude < 0.5f) continue;
                float limit = i < 3 ? 38f : i % 3 == 1 ? 78f : i % 3 == 0 ? 62f : 40f;
                float bend = Mathf.Clamp(Mathf.Lerp(open[i], closed[i], progress), 0f, limit);
                joint.localRotation = hand.bindRotations[i] * Quaternion.AngleAxis(bend, hand.bendAxes[i]);
            }
        }

        // Animator and arm IK finish before these authored finger poses are applied. No other layer writes them afterward.
        private void LateUpdate()
        {
            if (isIsolatedPreview || IsFullBodyOverrideActive || animator == null || !animator.isHuman) return;
            if (rightContact.weight > 0.001f) ApplyGrip(rightCalibration, rightContact.grip, 1f, AuthoredGrip.Open);
            else if (flashlightEquipped && !leftHandHoldingFlashlight)
                ApplyGrip(rightCalibration, HandGripProfile.Flashlight, flashlightGripWeight, AuthoredGrip.Release);
            else ApplyGrip(rightCalibration, new HandGripProfile { pose = AuthoredGrip.Relaxed }, 0f, AuthoredGrip.Relaxed);

            if (leftContact.weight > 0.001f) ApplyGrip(leftCalibration, leftContact.grip, 1f, AuthoredGrip.Open);
            else if (IsHoldingFestek) ApplyGrip(leftCalibration, HandGripProfile.Device, 1f, AuthoredGrip.Relaxed);
            else if (leftHandHoldingFlashlight) ApplyGrip(leftCalibration, HandGripProfile.Flashlight, 1f, AuthoredGrip.Relaxed);
            else ApplyGrip(leftCalibration, new HandGripProfile { pose = AuthoredGrip.Relaxed }, 0f, AuthoredGrip.Relaxed);
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
                }
            }

            // Layer 3: LeftArm_Interaction -> Çevresel etkileşim (kapı/duvar) veya sol ele aktarılmış el feneri IK'sı
            if (layerIndex == idxLeftArm)
            {
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
