using System;
using UnityEngine;
using UnityEngine.Playables;

namespace TheLastBell.Player
{
    /// <summary>
    /// THE LAST BELL - Modüler Çok Katmanlı Karakter Animasyon Yöneticisi.
    /// Animator bileşeninin bulunduğu karakter modeli (Character_Model) üzerinde çalışır.
    /// Katmanlar:
    ///   0: Base Layer (Locomotion, Crouch, Exhausted, Climb, Crawl)
    ///   1: UpperBody (Nefes, korku/tetikte olma, gövde dönüşü)
    ///   2: RightArm_Flashlight (El feneri tutma, nişan alma, bırakma)
    ///   3: LeftArm_Interaction (Kapı açma, eşya alma, çevre etkileşimi)
    ///   4: Head_Look (Kafa/boyun bakış takibi ve ses tepkileri)
    ///   5: Additive_Reactions (Titreme, darbe irkilmesi, nefes katmanı)
    ///   6: FullBody_Override (Düşme, kalkma, senaryolu olaylar ve sinematikler)
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
        [SerializeField] private Transform leftHandInteractTarget;

        [Header("--- Katman Ağırlıkları & Geçiş Hızı ---")]
        [SerializeField] private float layerBlendSpeed = 10f;
        [SerializeField] private bool flashlightEquipped = true;
        [SerializeField] private float additiveWeight = 0.35f;
        [SerializeField] private float headLookAtWeight = 0.55f;
        [SerializeField] private float rightHandIkWeight = 0.25f;

        [Header("--- Timeline / Sinematik Entegrasyonu ---")]
        [SerializeField] private PlayableDirector activeDirector;

        private int idxUpperBody = -1;
        private int idxRightArm = -1;
        private int idxLeftArm = -1;
        private int idxHeadLook = -1;
        private int idxAdditive = -1;
        private int idxFullOverride = -1;

        private float leftArmTimer = 0f;
        private float fullOverrideTimer = 0f;
        private bool isCinematicOverride = false;
        private bool isInitialized = false;

        // Animator Parametre Hash'leri
        private static readonly int HashSpeed = Animator.StringToHash("Speed");
        private static readonly int HashStrafe = Animator.StringToHash("Strafe");
        private static readonly int HashMoveMag = Animator.StringToHash("MoveMagnitude");
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
        private static readonly int HashImpactFlinch = Animator.StringToHash("ImpactFlinch");
        private static readonly int HashFall = Animator.StringToHash("Fall");
        private static readonly int HashGetUp = Animator.StringToHash("GetUp");
        private static readonly int HashCinematicActive = Animator.StringToHash("CinematicActive");

        // Animasyon Olay Kancaları (Animation Events)
        public event Action OnFootstepEvent;
        public event Action OnFlashlightGripEvent;
        public event Action OnFlashlightReleaseEvent;
        public event Action OnDoorInteractEvent;
        public event Action OnPickupEvent;
        public event Action OnFallImpactEvent;

        public Animator AnimatorComponent => animator;
        public bool IsFlashlightEquipped => flashlightEquipped;
        public bool IsFullBodyOverrideActive => isCinematicOverride || fullOverrideTimer > 0f;
        public Transform RightHandSocket => rightHandAttachSocket;

        private void Awake()
        {
            InitializeIfNeeded();
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
            if (rightHandAttachSocket != null || animator == null || !animator.isHuman) return;

            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
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
                    sock.transform.localPosition = new Vector3(0.02f, 0.04f, 0.02f);
                    sock.transform.localRotation = Quaternion.identity;
                    rightHandAttachSocket = sock.transform;
                }
            }
        }

        private void Update()
        {
            StepAnimationLayers(Time.deltaTime);
        }

        public void StepAnimationLayers(float dt)
        {
            if (animator == null) return;
            float safeDt = Mathf.Max(0.0001f, dt);

            if (leftArmTimer > 0f) leftArmTimer = Mathf.Max(0f, leftArmTimer - safeDt);
            if (fullOverrideTimer > 0f) fullOverrideTimer = Mathf.Max(0f, fullOverrideTimer - safeDt);

            bool fullOverride = IsFullBodyOverrideActive;

            float targetUpper = fullOverride ? 0f : 0.25f;
            float targetRight = (!fullOverride && flashlightEquipped) ? 1f : 0f;
            float targetLeft = (!fullOverride && leftArmTimer > 0f) ? 1f : 0f;
            float targetHead = fullOverride ? 0f : 1f;
            float targetAdd = fullOverride ? 0f : additiveWeight;
            float targetFull = fullOverride ? 1f : 0f;

            BlendLayer(idxUpperBody, targetUpper, safeDt);
            BlendLayer(idxRightArm, targetRight, safeDt);
            BlendLayer(idxLeftArm, targetLeft, safeDt);
            BlendLayer(idxHeadLook, targetHead, safeDt);
            BlendLayer(idxAdditive, targetAdd, safeDt);
            BlendLayer(idxFullOverride, targetFull, safeDt);
        }

        private void BlendLayer(int layerIdx, float targetWeight, float dt)
        {
            if (layerIdx < 0 || layerIdx >= animator.layerCount) return;
            float cur = animator.GetLayerWeight(layerIdx);
            float next = Mathf.MoveTowards(cur, targetWeight, dt * layerBlendSpeed);
            animator.SetLayerWeight(layerIdx, next);
        }

        public void UpdateLocomotionParameters(float speed, float strafe, float moveMagnitude, float speedMultiplier, bool isCrouching, bool isGrounded, bool isExhausted)
        {
            if (animator == null) return;
            animator.SetFloat(HashSpeed, speed);
            animator.SetFloat(HashStrafe, strafe);
            animator.SetFloat(HashMoveMag, Mathf.Clamp01(moveMagnitude));
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
            if (animator != null) animator.SetFloat(HashFearLevel, Mathf.Clamp01(fear01));
        }

        public void SetLookTarget(Transform target)
        {
            lookTargetOverride = target;
        }

        public void TriggerDoorInteract(Transform doorHandle = null)
        {
            leftArmInteractTargetSafe(doorHandle);
            leftArmTimer = 0.85f;
            if (animator != null)
            {
                animator.SetTrigger(HashDoorOpen);
                animator.SetTrigger(HashInteract);
            }
        }

        public void TriggerItemPickup(Transform itemTarget = null)
        {
            leftArmInteractTargetSafe(itemTarget);
            leftArmTimer = 0.9f;
            if (animator != null)
            {
                animator.SetTrigger(HashPickUp);
            }
        }

        public void TriggerImpactFlinch()
        {
            if (animator != null) animator.SetTrigger(HashImpactFlinch);
        }

        public void TriggerFall(float duration = 2.0f)
        {
            fullOverrideTimer = Mathf.Max(0.5f, duration);
            if (animator != null) animator.SetTrigger(HashFall);
        }

        public void TriggerGetUp(float duration = 1.6f)
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

        private void leftArmInteractTargetSafe(Transform target)
        {
            if (target != null) leftHandInteractTarget = target;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman || IsFullBodyOverrideActive) return;

            // Katman 4: Head_Look -> Kamera veya hedef takibi
            if (layerIndex == idxHeadLook || (idxHeadLook < 0 && layerIndex == 0))
            {
                Vector3 lookPos;
                if (lookTargetOverride != null)
                {
                    lookPos = lookTargetOverride.position;
                }
                else if (cameraTransform != null)
                {
                    lookPos = cameraTransform.position + cameraTransform.forward * 6f;
                }
                else
                {
                    lookPos = transform.position + transform.forward * 6f + Vector3.up * 1.65f;
                }

                animator.SetLookAtWeight(headLookAtWeight, 0.15f, 0.75f, 0.9f, 0.5f);
                animator.SetLookAtPosition(lookPos);
            }

            // Katman 2: RightArm_Flashlight -> Fener tutuş desteği
            if (layerIndex == idxRightArm && flashlightEquipped && cameraTransform != null && rightHandIkWeight > 0.01f)
            {
                Vector3 targetHandPos = cameraTransform.position + cameraTransform.right * 0.20f - cameraTransform.up * 0.18f + cameraTransform.forward * 0.32f;
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandIkWeight);
                animator.SetIKPosition(AvatarIKGoal.RightHand, targetHandPos);
            }

            // Katman 3: LeftArm_Interaction -> Etkileşim hedefi varsa uzanma
            if (layerIndex == idxLeftArm && leftArmTimer > 0f && leftHandInteractTarget != null)
            {
                float w = Mathf.Clamp01(leftArmTimer * 2f) * 0.7f;
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandInteractTarget.position);
            }
        }

        #region Unity Animation Event Callbacks
        public void AnimEvent_Footstep() => OnFootstepEvent?.Invoke();
        public void AnimEvent_FlashlightGrip() => OnFlashlightGripEvent?.Invoke();
        public void AnimEvent_FlashlightRelease() => OnFlashlightReleaseEvent?.Invoke();
        public void AnimEvent_DoorInteract() => OnDoorInteractEvent?.Invoke();
        public void AnimEvent_Pickup() => OnPickupEvent?.Invoke();
        public void AnimEvent_FallImpact() => OnFallImpactEvent?.Invoke();
        #endregion
    }
}
