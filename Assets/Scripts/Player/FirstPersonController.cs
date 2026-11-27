using System;
using UnityEngine;

namespace TheLastBell.Player
{
    public enum SurfaceType
    {
        Concrete,
        Wood,
        Metal
    }

    /// <summary>
    /// THE LAST BELL - Profesyonel Birinci ahs Korku Kontrolcs (First Person Horror Controller).
    /// Yrme, koma, melme, ivmelenme/yavalama, tavan engel kontrol,
    /// kafa sallants (head bob), dinamik FOV, dayankllk (stamina), tkeli nefes sistemi,
    /// zemin tipine gre ayak sesleri ve 178 animasyonlu Mecanim Animator Controller balantsn ynetir.
    /// Hem PC (Klavye + Fare) hem Android (Dokunmatik Sanal Kontroller) ile tam uyumludur.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("--- Hareket Hızları (m/s) ---")]
        [SerializeField] private float walkSpeed = 2.8f;
        [SerializeField] private float sprintSpeed = 5.6f;
        [SerializeField] private float crouchSpeed = 1.4f;
        [SerializeField] private float acceleration = 12f;
        [SerializeField] private float deceleration = 14f;

        [Header("--- Çömelme (Crouch) Ayarları ---")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField] private float standingCameraY = 1.8f;
        [SerializeField] private float crouchCameraY = 1.0f;
        [SerializeField] private float crouchTransitionSpeed = 10f;
        [SerializeField] private float ceilingCheckRadius = 0.3f;
        [SerializeField] private LayerMask ceilingLayers = ~0;

        [Header("--- Yerçekimi ve Eğim ---")]
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundedGravity = -4.5f;

        [Header("--- True First Person (Kamera Kafada) ---")]
        [SerializeField] private bool isTrueFPS = true;
        [SerializeField] private Vector3 eyeLocalOffset = new Vector3(0f, 1.8f, 0.15f);

        [Header("--- Kamera & Hassasiyet ---")]
        [SerializeField] private Transform cameraHolder;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float mouseSensitivityX = 2.0f;
        [SerializeField] private float mouseSensitivityY = 2.0f;
        [SerializeField] private float mobileSensitivityX = 1.4f;
        [SerializeField] private float mobileSensitivityY = 1.2f;
        [SerializeField] private float minPitch = -70f;
        [SerializeField] private float maxPitch = 70f;

        [Header("--- Kafa Sallants (Head Bob) ---")]
        [SerializeField] private bool enableHeadBob = true;
        [SerializeField] private float bobIntensity = 1.0f;
        [SerializeField] private float walkBobFrequency = 8.5f;
        [SerializeField] private float walkBobAmplitude = 0.032f;
        [SerializeField] private float sprintBobFrequency = 13.5f;
        [SerializeField] private float sprintBobAmplitude = 0.062f;

        [Header("--- Dinamik FOV ---")]
        [SerializeField] private float baseFOV = 60f;
        [SerializeField] private float sprintFOV = 64.5f;
        [SerializeField] private float fovChangeSpeed = 6f;

        [Header("--- Dayankllk (Stamina) ---")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina = 100f;
        [SerializeField] private float staminaDrainRate = 16f; // Saniyede harcanan
        [SerializeField] private float staminaRegenRate = 13f; // Saniyede yenilenen
        [SerializeField] private float regenDelay = 1.2f;
        [SerializeField] private float exhaustionRecoveryThreshold = 25f;

        [Header("--- Ayak Sesleri & Ses Kaynaklar ---")]
        [SerializeField] private AudioSource footstepAudioSource;
        [SerializeField] private AudioSource breathingAudioSource;
        [SerializeField] private AudioClip[] concreteSteps;
        [SerializeField] private AudioClip[] woodSteps;
        [SerializeField] private AudioClip[] metalSteps;
        [SerializeField] private AudioClip exhaustedBreathingClip;

        [Header("--- Karakter Animator Kontrolcs ---")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private float animDampTime = 0.15f;

        private CharacterController controller;
        private Vector3 moveVelocity = Vector3.zero;
        private float verticalVelocity = 0f;
        private float cameraPitch = 0f;

        private bool isCrouching = false;
        private bool isSprinting = false;
        private bool isExhausted = false;
        private float lastSprintTime = 0f;

        private float bobTimer = 0f;
        private Vector3 defaultCameraLocalPos;

        private float stepTimer = 0f;

        private float currentAnimSpeed = 0f;
        private float currentAnimStrafe = 0f;

        // Mobil Girdiler (MobileControlsUI tarafndan set edilir)
        public Vector2 MobileMoveInput { get; set; }
        public Vector2 MobileLookInput { get; set; }
        public bool MobileSprintInput { get; set; }
        public bool MobileCrouchInput { get; set; }

        public float StaminaPercent => currentStamina / maxStamina;
        public bool IsCrouching => isCrouching;
        public bool IsSprinting => isSprinting;
        public bool IsExhausted => isExhausted;

        public event Action<float> OnStaminaChanged;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;

            if (playerCamera == null && cameraHolder != null)
            {
                playerCamera = cameraHolder.GetComponentInChildren<Camera>();
            }
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
            if (cameraHolder == null && playerCamera != null)
            {
                cameraHolder = playerCamera.transform;
            }

            if (isTrueFPS)
            {
                enableHeadBob = false;
                if (cameraHolder != null)
                {
                    cameraHolder.localPosition = eyeLocalOffset;
                }
            }
            else if (cameraHolder != null)
            {
                defaultCameraLocalPos = new Vector3(0f, standingCameraY, 0f);
                cameraHolder.localPosition = defaultCameraLocalPos;
            }

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = baseFOV;
            }

            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            // Root Motion' CharacterController ile akmamas iin devre d brak
            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
            }

            // Fare imlecini kilitle
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (isTrueFPS)
            {
                ApplyTrueFPSHeadMeshCulling();
            }
        }

        public void ApplyTrueFPSHeadMeshCulling()
        {
            string[] headPartNames = new string[] {
                "AvatarHead", "AvatarEyelashes", "AvatarLeftEyeball", "AvatarRightEyeball", "AvatarTeethLower", "AvatarTeethUpper"
            };
            foreach (var partName in headPartNames)
            {
                Transform t = transform.Find("Character_Model/" + partName);
                if (t != null)
                {
                    Renderer r = t.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                    }
                }
            }
        }

        private void Update()
        {
            HandleLook();
            HandleCrouch();
            HandleStamina();
            HandleMovement();
            HandleHeadBobAndFOV();
            HandleFootsteps();
            UpdateAnimator();
        }

        private void HandleLook()
        {
            float lookX = 0f;
            float lookY = 0f;

            // PC Fare Girdisi
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                lookX += Input.GetAxisRaw("Mouse X") * mouseSensitivityX;
                lookY += Input.GetAxisRaw("Mouse Y") * mouseSensitivityY;
            }

            // Mobil Dokunmatik Bak Girdisi
            if (MobileLookInput != Vector2.zero)
            {
                lookX += MobileLookInput.x * mobileSensitivityX;
                lookY += MobileLookInput.y * mobileSensitivityY;
                MobileLookInput = Vector2.zero; // Harcand
            }

            // Yatay dn: Karakter gvdesini dndr
            transform.Rotate(Vector3.up * lookX);

            // Dikey bak: Kamera holder pitch clamp (-80, 80)
            cameraPitch = Mathf.Clamp(cameraPitch - lookY, minPitch, maxPitch);
            if (cameraHolder != null)
            {
                cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        public void ToggleCrouch()
        {
            SetCrouch(!isCrouching);
        }

        public void SetCrouch(bool crouch)
        {
            if (crouch)
            {
                isCrouching = true;
            }
            else
            {
                // Ayağa kalkarken tavan engeli kontrolü (SphereCast)
                Vector3 sphereOrigin = transform.position + Vector3.up * crouchHeight;
                float castDist = standingHeight - crouchHeight;
                bool ceilingBlocked = Physics.SphereCast(sphereOrigin, ceilingCheckRadius, Vector3.up, out _, castDist, ceilingLayers, QueryTriggerInteraction.Ignore);

                if (!ceilingBlocked)
                {
                    isCrouching = false;
                }
            }
        }

        private void HandleCrouch()
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
            {
                ToggleCrouch();
            }
            else if (MobileCrouchInput != isCrouching)
            {
                SetCrouch(MobileCrouchInput);
            }

            // CharacterController yükseklik ve merkez lerp
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            // Camera_Pivot yükseklik lerp (Ayakta: 1.7m, Çömelme: 1.0m)
            if (cameraHolder != null)
            {
                float targetCamY = isCrouching ? crouchCameraY : standingCameraY;
                Vector3 camPos = cameraHolder.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
                cameraHolder.localPosition = camPos;
            }
        }

        private void HandleStamina()
        {
            float v = Input.GetAxisRaw("Vertical");
            if (TheLastBell.UI.MobileControlsUI.Instance != null && (Application.isMobilePlatform || Application.isEditor))
            {
                v += TheLastBell.UI.MobileControlsUI.Instance.MovementInput.y;
            }
            else if (MobileMoveInput != Vector2.zero)
            {
                v += MobileMoveInput.y;
            }

            bool movingForward = v > 0.1f;
            bool mobileSprint = (TheLastBell.UI.MobileControlsUI.Instance != null && TheLastBell.UI.MobileControlsUI.Instance.IsSprinting) || MobileSprintInput;
            bool wantSprint = (Input.GetKey(KeyCode.LeftShift) || mobileSprint) && movingForward && !isCrouching;

            if (isExhausted)
            {
                if (currentStamina >= exhaustionRecoveryThreshold)
                {
                    isExhausted = false;
                }
                isSprinting = false;
            }
            else
            {
                isSprinting = wantSprint && currentStamina > 0f;
            }

            if (isSprinting)
            {
                currentStamina = Mathf.Max(0f, currentStamina - staminaDrainRate * Time.deltaTime);
                lastSprintTime = Time.time;

                if (currentStamina <= 0f)
                {
                    isExhausted = true;
                    isSprinting = false;
                }
            }
            else
            {
                if (Time.time - lastSprintTime > regenDelay)
                {
                    currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
                }
            }

            OnStaminaChanged?.Invoke(StaminaPercent);
            HandleBreathingAudio();
        }

        private void HandleBreathingAudio()
        {
            if (breathingAudioSource == null) return;

            bool shouldBreathe = isExhausted || (currentStamina < 30f);

            if (shouldBreathe)
            {
                if (!breathingAudioSource.isPlaying && exhaustedBreathingClip != null)
                {
                    breathingAudioSource.clip = exhaustedBreathingClip;
                    breathingAudioSource.loop = true;
                    breathingAudioSource.volume = Mathf.Lerp(0.3f, 0.85f, 1f - (currentStamina / 30f));
                    breathingAudioSource.Play();
                }
            }
            else
            {
                if (breathingAudioSource.isPlaying)
                {
                    breathingAudioSource.volume = Mathf.MoveTowards(breathingAudioSource.volume, 0f, Time.deltaTime * 1.5f);
                    if (breathingAudioSource.volume <= 0.01f)
                    {
                        breathingAudioSource.Stop();
                    }
                }
            }
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            if (TheLastBell.UI.MobileControlsUI.Instance != null && (Application.isMobilePlatform || Application.isEditor))
            {
                horizontal += TheLastBell.UI.MobileControlsUI.Instance.MovementInput.x;
                vertical += TheLastBell.UI.MobileControlsUI.Instance.MovementInput.y;
            }
            else if (MobileMoveInput != Vector2.zero)
            {
                horizontal += MobileMoveInput.x;
                vertical += MobileMoveInput.y;
            }

            horizontal = Mathf.Clamp(horizontal, -1f, 1f);
            vertical = Mathf.Clamp(vertical, -1f, 1f);

            // Hareket formülü (İleri/Geri ve Sağa/Sola):
            // W tuşu (vertical > 0) = İLERİ (transform.forward)
            // S tuşu (vertical < 0) = GERİ (-transform.forward)
            // D tuşu (horizontal > 0) = SAĞ (transform.right)
            // A tuşu (horizontal < 0) = SOL (-transform.right)
            Vector3 move = (transform.forward * vertical + transform.right * horizontal);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            float targetSpeed = 0f;
            if (move.sqrMagnitude > 0.0025f)
            {
                if (isCrouching) targetSpeed = crouchSpeed;
                else if (isSprinting) targetSpeed = sprintSpeed;
                else targetSpeed = walkSpeed;
            }

            float accelRate = (targetSpeed > 0.01f) ? acceleration : deceleration;
            Vector3 targetHorizontalVel = move * targetSpeed;
            moveVelocity.x = Mathf.MoveTowards(moveVelocity.x, targetHorizontalVel.x, accelRate * Time.deltaTime);
            moveVelocity.z = Mathf.MoveTowards(moveVelocity.z, targetHorizontalVel.z, accelRate * Time.deltaTime);

            if (controller.isGrounded)
            {
                verticalVelocity = groundedGravity;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
            moveVelocity.y = verticalVelocity;

            controller.Move(moveVelocity * Time.deltaTime);
        }

        private void HandleHeadBobAndFOV()
        {
            if (playerCamera == null) return;

            float targetFOV = isSprinting ? sprintFOV : baseFOV;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * fovChangeSpeed);

            if (isTrueFPS || !enableHeadBob || cameraHolder == null) return;

            float horizontalSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;

            if (controller.isGrounded && horizontalSpeed > 0.2f)
            {
                float freq = isSprinting ? sprintBobFrequency : walkBobFrequency;
                float amp = (isSprinting ? sprintBobAmplitude : walkBobAmplitude) * bobIntensity;

                bobTimer += Time.deltaTime * freq;

                float bobY = Mathf.Sin(bobTimer) * amp;
                float bobX = Mathf.Cos(bobTimer * 0.5f) * (amp * 0.5f);

                cameraHolder.localPosition = defaultCameraLocalPos + new Vector3(bobX, bobY, 0f);
            }
            else
            {
                bobTimer = 0f;
                cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, defaultCameraLocalPos, Time.deltaTime * 8f);
            }
        }

        private void HandleFootsteps()
        {
            if (!controller.isGrounded) return;

            float horizontalSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            if (horizontalSpeed < 0.2f)
            {
                stepTimer = 0f;
                return;
            }

            float stepInterval = 0.54f;
            if (isSprinting) stepInterval = 0.35f;
            else if (isCrouching) stepInterval = 0.72f;

            stepTimer += Time.deltaTime;
            if (stepTimer >= stepInterval)
            {
                stepTimer = 0f;
                PlayFootstepSound();
            }
        }

        private void PlayFootstepSound()
        {
            if (footstepAudioSource == null) return;

            SurfaceType surface = DetectSurface();
            AudioClip[] clipPool = concreteSteps;

            switch (surface)
            {
                case SurfaceType.Wood:
                    if (woodSteps != null && woodSteps.Length > 0) clipPool = woodSteps;
                    break;
                case SurfaceType.Metal:
                    if (metalSteps != null && metalSteps.Length > 0) clipPool = metalSteps;
                    break;
                default:
                    if (concreteSteps != null && concreteSteps.Length > 0) clipPool = concreteSteps;
                    break;
            }

            if (clipPool != null && clipPool.Length > 0)
            {
                AudioClip clip = clipPool[UnityEngine.Random.Range(0, clipPool.Length)];
                float volume = isCrouching ? 0.35f : (isSprinting ? 0.95f : 0.65f);
                footstepAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                footstepAudioSource.PlayOneShot(clip, volume);
            }
        }

        private SurfaceType DetectSurface()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                string hitName = hit.collider.gameObject.name.ToLower();
                string hitTag = hit.collider.tag.ToLower();
                PhysicsMaterial mat = hit.collider.sharedMaterial;
                string matName = mat != null ? mat.name.ToLower() : "";

                if (hitName.Contains("wood") || hitName.Contains("parquet") || hitName.Contains("board") || hitTag.Contains("wood") || matName.Contains("wood"))
                {
                    return SurfaceType.Wood;
                }
                if (hitName.Contains("metal") || hitName.Contains("iron") || hitName.Contains("elevator") || hitName.Contains("gate") || hitTag.Contains("metal") || matName.Contains("metal"))
                {
                    return SurfaceType.Metal;
                }
            }
            return SurfaceType.Concrete;
        }

        private void UpdateAnimator()
        {
            if (characterAnimator == null) return;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            if (TheLastBell.UI.MobileControlsUI.Instance != null && (Application.isMobilePlatform || Application.isEditor))
            {
                horizontal += TheLastBell.UI.MobileControlsUI.Instance.MovementInput.x;
                vertical += TheLastBell.UI.MobileControlsUI.Instance.MovementInput.y;
            }
            else if (MobileMoveInput != Vector2.zero)
            {
                horizontal += MobileMoveInput.x;
                vertical += MobileMoveInput.y;
            }

            horizontal = Mathf.Clamp(horizontal, -1f, 1f);
            vertical = Mathf.Clamp(vertical, -1f, 1f);

            float targetSpeed = 0f;
            float horizontalSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;

            if (horizontalSpeed > 0.15f)
            {
                if (vertical < -0.1f && Mathf.Abs(horizontal) < 0.5f)
                {
                    targetSpeed = -1.0f; // Walk Backwards
                }
                else if (isSprinting)
                {
                    targetSpeed = 3.0f; // Sprint
                }
                else
                {
                    targetSpeed = 1.0f; // Walk / Jog blend
                }
            }

            float targetStrafe = 0f;
            if (Mathf.Abs(horizontal) > 0.1f)
            {
                targetStrafe = Mathf.Sign(horizontal);
            }

            currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, targetSpeed, Time.deltaTime * 6f);
            currentAnimStrafe = Mathf.MoveTowards(currentAnimStrafe, targetStrafe, Time.deltaTime * 6f);

            characterAnimator.SetFloat("Speed", currentAnimSpeed);
            characterAnimator.SetFloat("Strafe", currentAnimStrafe);
            characterAnimator.SetBool("IsCrouching", isCrouching);
            characterAnimator.SetBool("IsGrounded", controller.isGrounded);
            characterAnimator.SetBool("IsExhausted", isExhausted);
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

        public void ConfigureComponents(
            Transform camHolder, Camera cam,
            AudioSource footstepSrc, AudioSource breathSrc,
            AudioClip[] concreteClips, AudioClip[] woodClips, AudioClip[] metalClips,
            AudioClip breathClip,
            Animator anim = null)
        {
            cameraHolder = camHolder;
            playerCamera = cam;
            footstepAudioSource = footstepSrc;
            breathingAudioSource = breathSrc;
            concreteSteps = concreteClips;
            woodSteps = woodClips;
            metalSteps = metalClips;
            exhaustedBreathingClip = breathClip;
            if (anim != null) characterAnimator = anim;
        }

        public void SetHeadBobSettings(bool enabled, float intensity)
        {
            enableHeadBob = enabled;
            bobIntensity = Mathf.Clamp01(intensity);
        }
    }
}
