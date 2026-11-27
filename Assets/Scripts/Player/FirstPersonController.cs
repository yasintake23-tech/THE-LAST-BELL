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
        [SerializeField] private float acceleration = 16f;
        [SerializeField] private float deceleration = 40f;

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

        [Header("--- Karakter Animator Kontrolcüsü ---")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private ProtagonistAnimationController animController;
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

        // Mobil Girdiler (MobileControlsUI tarafından set edilir)
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
            if (animController == null)
            {
                animController = GetComponentInChildren<ProtagonistAnimationController>(true);
            }
            if (animController == null && characterAnimator != null)
            {
                animController = characterAnimator.gameObject.AddComponent<ProtagonistAnimationController>();
            }
            animController?.InitializeIfNeeded();

            // Root Motion'ı CharacterController ile çakışmaması için devre dışı bırak
            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false;
            }

            if (!Application.isMobilePlatform)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

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
            StepController(Time.deltaTime);
        }

        public void StepController(float deltaTime)
        {
            float dt = Mathf.Max(0.0001f, deltaTime);
            HandleLook();
            HandleCrouch(dt);
            HandleStamina(dt);
            HandleMovement(dt);
            HandleHeadBobAndFOV(dt);
            HandleFootsteps(dt);
            UpdateAnimator(dt);
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

            // Mobil Dokunmatik Bakış Girdisi
            if (MobileLookInput != Vector2.zero)
            {
                lookX += MobileLookInput.x * mobileSensitivityX;
                lookY += MobileLookInput.y * mobileSensitivityY;
                MobileLookInput = Vector2.zero; // Harcandı
            }

            // Yatay dönüş: Karakter gövdesini döndür
            transform.Rotate(Vector3.up * lookX);

            // Dikey bakış: Kamera holder pitch clamp (-75, 75)
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

        private void HandleCrouch(float dt)
        {
            if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
            {
                ToggleCrouch();
            }
            else if (MobileCrouchInput && !isCrouching)
            {
                SetCrouch(true);
            }

            // CharacterController yükseklik ve merkez lerp
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            controller.height = Mathf.Lerp(controller.height, targetHeight, dt * crouchTransitionSpeed);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

            // Camera_Pivot yükseklik lerp
            if (cameraHolder != null)
            {
                float targetCamY = isCrouching ? crouchCameraY : standingCameraY;
                Vector3 camPos = cameraHolder.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, targetCamY, dt * crouchTransitionSpeed);
                cameraHolder.localPosition = camPos;
            }
        }

        private void GetCombinedMoveInput(out float horizontal, out float vertical)
        {
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");

            Vector2 mobileMove = MobileMoveInput;
            if (mobileMove == Vector2.zero && TheLastBell.UI.MobileControlsUI.Instance != null)
            {
                mobileMove = TheLastBell.UI.MobileControlsUI.Instance.MovementInput;
            }

            horizontal += mobileMove.x;
            vertical += mobileMove.y;

            horizontal = Mathf.Clamp(horizontal, -1f, 1f);
            vertical = Mathf.Clamp(vertical, -1f, 1f);
        }

        private void HandleStamina(float dt)
        {
            GetCombinedMoveInput(out float h, out float v);
            bool movingEnough = (v > 0.1f) || ((h * h + v * v) > 0.09f && v >= -0.15f);
            bool mobileSprint = (TheLastBell.UI.MobileControlsUI.Instance != null && TheLastBell.UI.MobileControlsUI.Instance.IsSprinting) || MobileSprintInput;
            bool wantSprint = (Input.GetKey(KeyCode.LeftShift) || mobileSprint) && movingEnough && !isCrouching;

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
                currentStamina = Mathf.Max(0f, currentStamina - staminaDrainRate * dt);
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
                    currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * dt);
                }
            }

            OnStaminaChanged?.Invoke(StaminaPercent);
            HandleBreathingAudio(dt);
        }

        private void HandleBreathingAudio(float dt)
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
                    breathingAudioSource.volume = Mathf.MoveTowards(breathingAudioSource.volume, 0f, dt * 1.5f);
                    if (breathingAudioSource.volume <= 0.01f)
                    {
                        breathingAudioSource.Stop();
                    }
                }
            }
        }

        private void HandleMovement(float dt)
        {
            GetCombinedMoveInput(out float horizontal, out float vertical);

            Vector3 inputDir = transform.forward * vertical + transform.right * horizontal;
            float inputMag = inputDir.magnitude;
            if (inputMag > 1f)
            {
                inputDir /= inputMag;
                inputMag = 1f;
            }

            float targetSpeed = 0f;
            if (inputMag > 0.05f)
            {
                float maxModeSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
                targetSpeed = maxModeSpeed * inputMag;
                Vector3 targetHorizontalVel = inputDir.normalized * targetSpeed;
                moveVelocity.x = Mathf.MoveTowards(moveVelocity.x, targetHorizontalVel.x, acceleration * dt * maxModeSpeed);
                moveVelocity.z = Mathf.MoveTowards(moveVelocity.z, targetHorizontalVel.z, acceleration * dt * maxModeSpeed);
            }
            else
            {
                // Joystick bırakıldığında kaymayı önlemek için anında keskin duruş
                moveVelocity.x = Mathf.MoveTowards(moveVelocity.x, 0f, deceleration * dt);
                moveVelocity.z = Mathf.MoveTowards(moveVelocity.z, 0f, deceleration * dt);
                if ((moveVelocity.x * moveVelocity.x + moveVelocity.z * moveVelocity.z) < 0.02f)
                {
                    moveVelocity.x = 0f;
                    moveVelocity.z = 0f;
                }
            }

            if (controller.isGrounded)
            {
                verticalVelocity = groundedGravity;
            }
            else
            {
                verticalVelocity += gravity * dt;
            }
            moveVelocity.y = verticalVelocity;

            if (controller.enabled)
            {
                controller.Move(moveVelocity * dt);
            }
        }

        private void HandleHeadBobAndFOV(float dt)
        {
            if (playerCamera == null) return;

            float targetFOV = isSprinting ? sprintFOV : baseFOV;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, dt * fovChangeSpeed);

            if (isTrueFPS || !enableHeadBob || cameraHolder == null) return;

            float horizontalSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;

            if (controller.isGrounded && horizontalSpeed > 0.2f)
            {
                float freq = isSprinting ? sprintBobFrequency : walkBobFrequency;
                float amp = (isSprinting ? sprintBobAmplitude : walkBobAmplitude) * bobIntensity;

                bobTimer += dt * freq;

                float bobY = Mathf.Sin(bobTimer) * amp;
                float bobX = Mathf.Cos(bobTimer * 0.5f) * (amp * 0.5f);

                cameraHolder.localPosition = defaultCameraLocalPos + new Vector3(bobX, bobY, 0f);
            }
            else
            {
                bobTimer = 0f;
                cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, defaultCameraLocalPos, dt * 8f);
            }
        }

        private void HandleFootsteps(float dt)
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

            stepTimer += dt;
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

        private void UpdateAnimator(float dt)
        {
            if (characterAnimator == null) return;

            GetCombinedMoveInput(out float horizontal, out float vertical);
            float inputMag = Mathf.Clamp01(Mathf.Sqrt(horizontal * horizontal + vertical * vertical));

            // Karakterin gerçek yatay hızını hesapla (CharacterController veya moveVelocity üzerinden)
            float actualHorizSpeed = Mathf.Max(
                new Vector2(controller.velocity.x, controller.velocity.z).magnitude,
                new Vector2(moveVelocity.x, moveVelocity.z).magnitude);

            float targetSpeed = 0f;
            float targetStrafe = 0f;

            if (inputMag > 0.05f || actualHorizSpeed > 0.1f)
            {
                float speedScale = isSprinting ? 3.0f : 1.0f;
                targetSpeed = vertical * speedScale;
                targetStrafe = horizontal;
            }

            currentAnimSpeed = Mathf.MoveTowards(currentAnimSpeed, targetSpeed, dt * 8f);
            currentAnimStrafe = Mathf.MoveTowards(currentAnimStrafe, targetStrafe, dt * 8f);

            // Animasyon oynatma hızını gerçek yürüme hızına oranla (kaymayı engeller)
            float playbackMult = 1.0f;
            if (actualHorizSpeed > 0.1f && walkSpeed > 0.01f)
            {
                float refSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
                playbackMult = Mathf.Clamp(actualHorizSpeed / Mathf.Max(0.5f, refSpeed), 0.7f, 1.35f);
            }

            if (animController != null)
            {
                animController.UpdateLocomotionParameters(
                    currentAnimSpeed,
                    currentAnimStrafe,
                    inputMag,
                    playbackMult,
                    isCrouching,
                    controller.isGrounded,
                    isExhausted);
            }
            else
            {
                characterAnimator.SetFloat("Speed", currentAnimSpeed);
                characterAnimator.SetFloat("Strafe", currentAnimStrafe);
                characterAnimator.SetBool("IsCrouching", isCrouching);
                characterAnimator.SetBool("IsGrounded", controller.isGrounded);
                characterAnimator.SetBool("IsExhausted", isExhausted);
            }
        }

        #region Animasyon Tetikleyicileri
        public void PlayInteractAnimation()
        {
            if (animController != null) animController.TriggerDoorInteract();
            else if (characterAnimator != null) characterAnimator.SetTrigger("Interact");
        }

        public void PlayPickupAnimation()
        {
            if (animController != null) animController.TriggerItemPickup();
            else if (characterAnimator != null) characterAnimator.SetTrigger("PickUp");
        }

        public void PlayHitAnimation()
        {
            if (animController != null) animController.TriggerImpactFlinch();
            else if (characterAnimator != null) characterAnimator.SetTrigger("Hit");
        }

        public void PlayDeathAnimation()
        {
            if (animController != null) animController.TriggerFall();
            else if (characterAnimator != null) characterAnimator.SetTrigger("Die");
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
