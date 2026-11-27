using System;
using UnityEngine;

namespace TheLastBell.Player
{
    /// <summary>
    /// THE LAST BELL - 3. ahs Karakter Kontrolcs (Third Person Character Controller).
    /// Kamerann bakt yne gre hareket eder, karakteri yne evirir,
    /// yrme, koma, melme fizikleri ve 178 animasyonlu Mecanim Animator ile alr.
    /// PC ve Android tam uyumludur.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TPSController : MonoBehaviour
    {
        [Header("--- Hareket Hzlar (m/s) ---")]
        [SerializeField] private float walkSpeed = 2.8f;
        [SerializeField] private float sprintSpeed = 5.2f;
        [SerializeField] private float crouchSpeed = 1.4f;
        [SerializeField] private float rotationSmoothTime = 0.1f;
        [SerializeField] private float speedSmoothTime = 0.1f;

        [Header("--- melme (Crouch) ---")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;
        [SerializeField] private float crouchTransitionSpeed = 10f;

        [Header("--- Yerçekimi ve Zemin ---")]
        [SerializeField] private float gravity = -9.81f * 1.8f; // Doğal düşüş çarpanı
        [SerializeField] private float groundedGravity = -4.5f; // Yerdeyken sürekli yapışma kuvveti (-2f ile -5f arası)
        [SerializeField] private float groundCheckRadius = 0.28f;
        [SerializeField] private float groundCheckOffset = 0.05f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("--- Referanslar ---")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Animator characterAnimator;

        private CharacterController controller;
        private float currentSpeed;
        private float speedSmoothVelocity;
        private float rotationVelocity;
        private float verticalVelocity;
        private bool isCrouching = false;
        private bool isGroundedPhysics = false;

        // Mobil Girdiler (MobileControlsUI tarafndan set edilir)
        public Vector2 MobileMoveInput { get; set; }
        public bool MobileSprintInput { get; set; }
        public bool MobileCrouchInput { get; set; }

        public bool IsGrounded => isGroundedPhysics;
        public float CurrentSpeed => currentSpeed;
        public bool IsSprinting { get; private set; }
        public bool IsCrouching => isCrouching;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.05f;
            controller.minMoveDistance = 0f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.35f;

            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
            }
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            HandleCrouch();
            HandleMovement();
            UpdateAnimator();
        }

        private void LateUpdate()
        {
            // Karakter omurgası ve kafasının dümdüz ileriye (hareket/baktığı yöne) bakmasını sağla
            if (characterAnimator != null)
            {
                Transform head = characterAnimator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    head.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                }
            }
        }

        private void HandleCrouch()
        {
            bool wantCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || MobileCrouchInput;
            isCrouching = wantCrouch;

            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        }

        private void CheckGrounded()
        {
            isGroundedPhysics = controller != null && controller.isGrounded;
        }

        private void HandleMovement()
        {
            CheckGrounded();

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (TheLastBell.UI.MobileControlsUI.Instance != null && (Application.isMobilePlatform || Application.isEditor))
            {
                h += TheLastBell.UI.MobileControlsUI.Instance.MovementInput.x;
                v += TheLastBell.UI.MobileControlsUI.Instance.MovementInput.y;
            }
            else if (MobileMoveInput.sqrMagnitude > 0.01f)
            {
                h += MobileMoveInput.x;
                v += MobileMoveInput.y;
            }

            h = Mathf.Clamp(h, -1f, 1f);
            v = Mathf.Clamp(v, -1f, 1f);

            Vector2 inputDir = new Vector2(h, v);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            bool sprintKey = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || MobileSprintInput;
            IsSprinting = sprintKey && inputDir.sqrMagnitude > 0.01f && !isCrouching;

            float targetSpeed = 0f;
            if (inputDir.sqrMagnitude > 0.01f)
            {
                if (isCrouching) targetSpeed = crouchSpeed;
                else if (IsSprinting) targetSpeed = sprintSpeed;
                else targetSpeed = walkSpeed;
            }

            // Hızlanma ve ani duruş (kayma/drift olmadan oturaklı basma)
            if (inputDir.sqrMagnitude > 0.01f)
            {
                currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);
            }
            else
            {
                // Giriş bırakıldığında hemen oturaklı şekilde sıfırla (drift/kayma yapmaz)
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, 18f * Time.deltaTime);
            }

            // Kameraya göre hareket vektörü hesaplama (kamera forward ve right bileşenleri)
            Vector3 camFwd = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 camRight = cameraTransform != null ? cameraTransform.right : Vector3.right;
            camFwd.y = 0f;
            camRight.y = 0f;
            camFwd.Normalize();
            camRight.Normalize();

            Vector3 moveDirection = (camFwd * inputDir.y + camRight * inputDir.x);
            if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

            // Karakter Dönüşünü Kameranın İleri Yönüne Kilitle / Çevir
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                float targetRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref rotationVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }

            // Kesin Yerçekimi Döngüsü: Yerdeyken velocity.y = -2f, havadayken -9.81f * Time.deltaTime
            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -2f;
                }
            }
            else
            {
                verticalVelocity -= 9.81f * Time.deltaTime;
            }

            Vector3 motion = Vector3.zero;
            if (inputDir.sqrMagnitude > 0.01f && currentSpeed > 0.01f)
            {
                motion = transform.forward * currentSpeed;
            }
            // Idle durumunda yatay bileşen kesinlikle sıfırdır; dikey yerçekimi hızı uygulanır
            motion.y = verticalVelocity;

            controller.Move(motion * Time.deltaTime);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isGroundedPhysics ? Color.green : Color.red;
            Vector3 spherePos = transform.position + new Vector3(0f, groundCheckRadius - groundCheckOffset, 0f);
            Gizmos.DrawWireSphere(spherePos, groundCheckRadius);
        }

        private void UpdateAnimator()
        {
            if (characterAnimator == null) return;

            float targetSpeed = 0f;
            if (currentSpeed > 0.05f)
            {
                if (isCrouching)
                {
                    targetSpeed = Mathf.Clamp01(currentSpeed / crouchSpeed);
                }
                else if (IsSprinting)
                {
                    targetSpeed = Mathf.Lerp(1.2f, 3.0f, Mathf.InverseLerp(walkSpeed, sprintSpeed, currentSpeed));
                }
                else
                {
                    targetSpeed = Mathf.Clamp(currentSpeed / walkSpeed, 0f, 1.2f);
                }
            }

            // 0.12f Damp Time ile akıcı Speed geçişi
            characterAnimator.SetFloat("Speed", targetSpeed, 0.12f, Time.deltaTime);

            // Koşarken ve yürürken Strafe KESİNLİKLE ve anında 0.0f değerinde sabitlenir (yan karışmayı önler)
            characterAnimator.SetFloat("Strafe", 0.0f);

            characterAnimator.SetBool("IsCrouching", isCrouching);
            characterAnimator.SetBool("IsGrounded", isGroundedPhysics);
            characterAnimator.SetBool("IsExhausted", false);
        }

        #region Animasyon Tetikleyicileri
        public void PlayInteractAnimation()
        {
            if (characterAnimator != null) characterAnimator.SetTrigger("Interact");
        }

        public void PlayPickupAnimation()
        {
            if (characterAnimator != null) characterAnimator.SetTrigger("PickUp");
        }

        public void PlayHitAnimation()
        {
            if (characterAnimator != null) characterAnimator.SetTrigger("Hit");
        }

        public void PlayDeathAnimation()
        {
            if (characterAnimator != null) characterAnimator.SetTrigger("Die");
        }
        #endregion

        public void Configure(Transform cam, Animator anim)
        {
            cameraTransform = cam;
            characterAnimator = anim;
        }
    }
}
