using System;
using UnityEngine;

namespace TheLastBell.Player
{
    public enum SurfaceType
    {
        Concrete,
        Wood,
        Metal,
        Tile,
        Asphalt,
        Grass,
        Soil,
        Gravel
    }

    [Serializable]
    public struct SurfaceFootstepClips
    {
        public SurfaceType surface;
        public AudioClip[] clips;
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
        [SerializeField] private SurfaceFootstepClips[] surfaceSteps;
        [SerializeField] private AudioClip exhaustedBreathingClip;

        [Header("--- Karakter Animator Kontrolcüsü ---")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private ProtagonistAnimationController animController;
        [SerializeField] private float animDampTime = 0.15f;

        [Header("--- Dinamik Bakış & Yerinde Dönüş (Turn-in-Place) ---")]
        [SerializeField] private float turnInPlaceTriggerAngle = 58f;
        [SerializeField] private float turnInPlaceStopAngle = 6f;
        [SerializeField] private float turnInPlaceSpeed = 135f;
        [SerializeField] private float movingBodyAlignSpeed = 8.5f;

        private CharacterController controller;
        private Vector3 moveVelocity = Vector3.zero;
        private float verticalVelocity = 0f;
        private float cameraPitch = 0f;
        private float desiredLookYaw = 0f;
        private float lastKnownBodyYaw = 0f;
        private bool isTurningInPlace = false;
        private float turnAnimCooldown = 0f;

        // Sinematik / Senaryolu Bakış Geçersiz Kılma (Bölüm 13)
        private bool scriptedGazeActive = false;
        private float scriptedGazeYaw = 0f;
        private float scriptedGazePitch = 0f;
        private float scriptedGazeBlend = 0f;

        private bool isCrouching = false;
        private bool isSprinting = false;
        private bool isExhausted = false;
        private float lastSprintTime = 0f;

        private float bobTimer = 0f;
        private Vector3 defaultCameraLocalPos;

        private float stepDistance;
        private float timeSinceFootstep = 1f;
        private AudioClip previousFootstep;

        private float currentAnimSpeed = 0f;
        private float currentAnimStrafe = 0f;

        // Mobil Girdiler (MobileControlsUI tarafından set edilir)
        public Vector2 MobileMoveInput { get; set; }
        public Vector2 MobileLookInput { get; set; }
        public bool MobileSprintInput { get; set; }
        public bool MobileCrouchInput { get; set; }
        public bool AllowMovement { get; set; } = true;
        public bool FreezeGazeForInspection { get; set; } = false;

        public float StaminaPercent => currentStamina / maxStamina;
        public bool IsCrouching => isCrouching;
        public bool IsSprinting => isSprinting;
        public bool IsExhausted => isExhausted;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public bool IsTurningInPlace => isTurningInPlace;
        public float DesiredLookYaw => desiredLookYaw;
        public float DesiredLookPitch => cameraPitch;
        public float RelativeLookYaw => Mathf.DeltaAngle(transform.eulerAngles.y, desiredLookYaw);
        public Vector3 DesiredLookDirection => Quaternion.Euler(cameraPitch, desiredLookYaw, 0f) * Vector3.forward;
        public Vector3 CurrentVelocity => controller != null ? controller.velocity : moveVelocity;

        public event Action<float> OnStaminaChanged;

        public void SetScriptedGazeOverride(bool active, float worldYaw, float pitch, float blend = 1f)
        {
            scriptedGazeActive = active;
            scriptedGazeYaw = worldYaw;
            scriptedGazePitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            scriptedGazeBlend = Mathf.Clamp01(blend);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;

            desiredLookYaw = transform.eulerAngles.y;
            lastKnownBodyYaw = desiredLookYaw;

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
            var camMode = GetComponent<CameraModeController>();
            if (camMode != null)
            {
                camMode.ApplyMeshVisibility(camMode.IsThirdPerson);
                return;
            }

            Transform modelRoot = transform.Find("Character_Model");
            if (modelRoot != null)
            {
                foreach (var r in modelRoot.GetComponentsInChildren<Renderer>(true))
                {
                    bool isHeadPart = r.gameObject.name.Contains("Head") ||
                                      r.gameObject.name.Contains("Eye") ||
                                      r.gameObject.name.Contains("Teeth") ||
                                      r.gameObject.name.Contains("Eyelash");
                    r.shadowCastingMode = isHeadPart
                        ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                        : UnityEngine.Rendering.ShadowCastingMode.On;
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
            SyncExternalRotationIfChanged();
            HandleLook(dt);
            if (!AllowMovement) return;
            HandleCrouch(dt);
            HandleStamina(dt);
            HandleMovement(dt);
            HandleHeadBobAndFOV(dt);
            HandleFootsteps(dt);
            UpdateAnimator(dt);
        }

        private void SyncExternalRotationIfChanged()
        {
            float curYaw = transform.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(lastKnownBodyYaw, curYaw)) > 0.5f)
            {
                desiredLookYaw = curYaw;
                lastKnownBodyYaw = curYaw;
                isTurningInPlace = false;
            }
        }

        private void HandleLook(float dt)
        {
            if (turnAnimCooldown > 0f) turnAnimCooldown = Mathf.Max(0f, turnAnimCooldown - dt);

            float lookX = 0f;
            float lookY = 0f;

            if (!FreezeGazeForInspection)
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    lookX += Input.GetAxisRaw("Mouse X") * mouseSensitivityX;
                    lookY += Input.GetAxisRaw("Mouse Y") * mouseSensitivityY;
                }

                if (MobileLookInput != Vector2.zero)
                {
                    lookX += MobileLookInput.x * mobileSensitivityX;
                    lookY += MobileLookInput.y * mobileSensitivityY;
                    MobileLookInput = Vector2.zero;
                }
            }
            else
            {
                MobileLookInput = Vector2.zero;
            }

            if (scriptedGazeActive && scriptedGazeBlend > 0.01f)
            {
                desiredLookYaw = Mathf.LerpAngle(desiredLookYaw, scriptedGazeYaw, 1f - Mathf.Exp(-8f * dt) * (1f - scriptedGazeBlend));
                cameraPitch = Mathf.Lerp(cameraPitch, scriptedGazePitch, 1f - Mathf.Exp(-8f * dt));
            }
            else
            {
                desiredLookYaw = Mathf.Repeat(desiredLookYaw + lookX, 360f);
                cameraPitch = Mathf.Clamp(cameraPitch - lookY, minPitch, maxPitch);
            }

            // Hareket halindeyken veya büyük yatay dönüşlerde gövde yönelimini yumuşakça hizala
            GetCombinedMoveInput(out float h, out float v);
            bool hasMoveInput = AllowMovement && (h * h + v * v) > 0.01f;
            float relYaw = Mathf.DeltaAngle(transform.eulerAngles.y, desiredLookYaw);

            if (hasMoveInput)
            {
                isTurningInPlace = false;
                float nextYaw = Mathf.LerpAngle(transform.eulerAngles.y, desiredLookYaw, 1f - Mathf.Exp(-movingBodyAlignSpeed * dt));
                transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
            }
            else
            {
                if (!isTurningInPlace && Mathf.Abs(relYaw) >= turnInPlaceTriggerAngle)
                {
                    isTurningInPlace = true;
                    if (turnAnimCooldown <= 0f)
                    {
                        animController?.TriggerTurnInPlace(relYaw < 0f ? -1 : 1);
                        turnAnimCooldown = 0.85f;
                    }
                }

                if (isTurningInPlace)
                {
                    if (Mathf.Abs(relYaw) > turnInPlaceTriggerAngle * 0.85f && turnAnimCooldown <= 0f)
                    {
                        animController?.TriggerTurnInPlace(relYaw < 0f ? -1 : 1);
                        turnAnimCooldown = 0.85f;
                    }

                    float stepYaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, desiredLookYaw, turnInPlaceSpeed * dt);
                    transform.rotation = Quaternion.Euler(0f, stepYaw, 0f);

                    if (Mathf.Abs(Mathf.DeltaAngle(stepYaw, desiredLookYaw)) <= turnInPlaceStopAngle)
                    {
                        isTurningInPlace = false;
                    }
                }
            }

            lastKnownBodyYaw = transform.eulerAngles.y;

            // Camera_Pivot her zaman istenen bakış yönünü takip eder (gövde hizalanırken kamera sıçramaz)
            if (cameraHolder != null)
            {
                float updatedRelYaw = Mathf.DeltaAngle(transform.eulerAngles.y, desiredLookYaw);
                cameraHolder.localRotation = Quaternion.Euler(cameraPitch, updatedRelYaw, 0f);
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

            Quaternion gazeYawRot = Quaternion.Euler(0f, desiredLookYaw, 0f);
            Vector3 gazeForward = gazeYawRot * Vector3.forward;
            Vector3 gazeRight = gazeYawRot * Vector3.right;
            Vector3 inputDir = gazeForward * vertical + gazeRight * horizontal;
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

        private void OnEnable()
        {
            if (animController != null) animController.OnFootstepEvent += OnAnimationFootstep;
        }

        private void OnDisable()
        {
            if (animController != null) animController.OnFootstepEvent -= OnAnimationFootstep;
            stepDistance = 0f;
        }

        private float FootstepStride => isCrouching ? 0.47f : (isSprinting ? 1.27f : 0.82f);

        private void OnAnimationFootstep()
        {
            if (!AllowMovement || controller == null || !controller.isGrounded || timeSinceFootstep < 0.17f ||
                stepDistance < FootstepStride * 0.69f) return;

            stepDistance = 0f;
            timeSinceFootstep = 0f;
            PlayFootstepSound();
        }

        private void HandleFootsteps(float dt)
        {
            timeSinceFootstep += dt;
            float horizontalSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
            if (!controller.isGrounded || horizontalSpeed < 0.18f)
            {
                stepDistance = 0f;
                return;
            }

            // A step corresponds to horizontal motion on the floor, including analog speeds.
            float stride = FootstepStride;
            stepDistance += horizontalSpeed * dt;
            if (stepDistance >= stride && timeSinceFootstep >= 0.17f)
            {
                stepDistance %= stride;
                timeSinceFootstep = 0f;
                PlayFootstepSound();
            }
        }

        private void PlayFootstepSound()
        {
            if (!Application.isPlaying || footstepAudioSource == null) return;
            SurfaceType surface = DetectSurface();
            AudioClip[] clipPool = surface == SurfaceType.Wood ? woodSteps :
                surface == SurfaceType.Metal ? metalSteps : concreteSteps;
            if (surfaceSteps != null)
            {
                foreach (var bank in surfaceSteps)
                {
                    if (bank.surface != surface || bank.clips == null || bank.clips.Length == 0) continue;
                    clipPool = bank.clips;
                    break;
                }
            }

            if (clipPool == null || clipPool.Length == 0) return;
            int choice = UnityEngine.Random.Range(0, clipPool.Length);
            if (clipPool.Length > 1 && clipPool[choice] == previousFootstep)
                choice = (choice + UnityEngine.Random.Range(1, clipPool.Length)) % clipPool.Length;
            var clip = clipPool[choice];
            if (clip == null) return;
            previousFootstep = clip;
            float volume = isCrouching ? 0.27f : (isSprinting ? 0.64f : 0.46f);
            footstepAudioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            footstepAudioSource.PlayOneShot(clip, volume * UnityEngine.Random.Range(0.90f, 1.08f));
        }

        private SurfaceType DetectSurface()
        {
            Vector3 foot = transform.TransformPoint(controller.center) - Vector3.up * (controller.height * 0.5f + 0.02f);
            if (!Physics.Raycast(foot + Vector3.up * 0.17f, Vector3.down, out var hit, 0.75f, ~0, QueryTriggerInteraction.Ignore))
                return SurfaceType.Concrete;

            string name = hit.collider.name.ToLowerInvariant();
            var physicsMaterial = hit.collider.sharedMaterial;
            if (physicsMaterial != null) name += " " + physicsMaterial.name.ToLowerInvariant();
            var renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                name += " " + renderer.sharedMaterial.name.ToLowerInvariant();

            if (name.Contains("gravel")) return SurfaceType.Gravel;
            if (name.Contains("asphalt")) return SurfaceType.Asphalt;
            if (name.Contains("grass")) return SurfaceType.Grass;
            if (name.Contains("soil") || name.Contains("dirt")) return SurfaceType.Soil;
            if (name.Contains("tile")) return SurfaceType.Tile;
            if (name.Contains("wood") || name.Contains("parquet")) return SurfaceType.Wood;
            if (name.Contains("metal") || name.Contains("iron") || name.Contains("elevator")) return SurfaceType.Metal;
            return SurfaceType.Concrete;
        }

        public void ConfigureSurfaceAudio(AudioSource source, SurfaceFootstepClips[] banks)
        {
            footstepAudioSource = source;
            surfaceSteps = banks;
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
                float speedScale = isSprinting ? 2.0f : 1.0f;
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
