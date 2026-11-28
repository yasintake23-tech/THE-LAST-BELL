using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    /// <summary>
    /// THE LAST BELL - Modler Kap Etkileimi (Door Interactable).
    /// Tek veya ift kanatl kaplar animasyonla aar ve kapatr.
    /// Kapy atkktan sonra geii tamamen ak brakr; hibir grnmez engel brakmaz.
    /// </summary>
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("--- Kap Kanatlar ---")]
        [SerializeField] private Transform doorLeafLeft;
        [SerializeField] private Transform doorLeafRight;
        [SerializeField] private Collider blockerCollider;

        [Header("--- Al ve Hareket Ayarlar ---")]
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float transitionDuration = 1.0f;
        [SerializeField] private bool isOpen = false;

        [Header("--- Kilit Sistemi ---")]
        [SerializeField] private bool isLocked = false;
        [SerializeField] private string requiredKeyId = "";
        [SerializeField] private string lockedNotice = "Kilitli";
        [SerializeField] private string unlockedNotice = "Kilit açıldı!";

        [Header("--- Uzamsal 3D Kapı Sesleri ---")]
        [SerializeField] private AudioSource doorAudioSource;
        [SerializeField] private AudioClip handleLatchClip;
        [SerializeField] private AudioClip openHingeClip;
        [SerializeField] private AudioClip closeHingeClip;

        private Quaternion closedRotL;
        private Quaternion openRotL;
        private Quaternion closedRotR;
        private Quaternion openRotR;

        private InteractionTarget interactionTarget;
        private const float ContactDelay = 0.42f;
        private bool isAnimating;
        private bool targetOpen;
        private bool latchPlayed;
        private bool swingStarted;
        private bool poseInitialized;
        private float elapsed;
        private Transform interactionActor;

        public InteractionTarget DoorInteractionTarget => interactionTarget;
        public bool IsOpen => isOpen;
        public event System.Action<bool> OnDoorStateChanged;

        private void Awake()
        {
            InitializePose();
            ApplyPose(isOpen ? 1f : 0f);
            EnsureDoorHandleTarget();
        }

        private void InitializePose()
        {
            if (poseInitialized) return;
            closedRotL = doorLeafLeft != null ? doorLeafLeft.localRotation : Quaternion.identity;
            closedRotR = doorLeafRight != null ? doorLeafRight.localRotation : Quaternion.identity;
            openRotL = closedRotL * Quaternion.Euler(0f, -openAngle, 0f);
            openRotR = closedRotR * Quaternion.Euler(0f, openAngle, 0f);
            poseInitialized = true;
        }

        private void Update() => StepDoor(Time.deltaTime);

        private void OnDisable()
        {
            if (isAnimating) FinishDoor(true);
        }

        private void EnsureDoorHandleTarget()
        {
            interactionTarget = GetComponent<InteractionTarget>();
            if (interactionTarget == null)
            {
                interactionTarget = gameObject.AddComponent<InteractionTarget>();
            }

            Transform leaf = doorLeafLeft != null ? doorLeafLeft : (doorLeafRight != null ? doorLeafRight : transform);
            Transform handle = doorLeafLeft != null ? doorLeafLeft.Find("Door_Handle_Target") : null;
            if (handle == null && doorLeafRight != null) handle = doorLeafRight.Find("Door_Handle_Target");
            if (handle == null)
            {
                var handleGo = new GameObject("Door_Handle_Target");
                handleGo.transform.SetParent(leaf, false);
                handleGo.transform.localPosition = new Vector3(0.42f, 1.02f, -0.08f);
                handleGo.transform.localRotation = Quaternion.identity;
                handle = handleGo.transform;
            }

            interactionTarget.Configure(handle, PreferredInteractionHand.Left, "DoorOpen", 2.0f, 0.38f, 0.65f);
        }

        public string GetInteractionPrompt()
        {
            if (isAnimating) return "";
            if (isLocked) return "[E] - Kapıyı Aç (Kilitli)";
            return isOpen ? "[E] - Kapıyı Kapat" : "[E] - Kapıyı Aç";
        }

        public bool CanInteract() => !isAnimating;

        public void Interact(PlayerInteractor interactor)
        {
            if (!isActiveAndEnabled || isAnimating || (doorLeafLeft == null && doorLeafRight == null)) return;

            if (isLocked)
            {
                var inventory = interactor != null ? interactor.GetComponent<PlayerInventory>() : null;
                if (inventory == null || string.IsNullOrEmpty(requiredKeyId) || !inventory.HasKey(requiredKeyId))
                {
                    PlayDoorSound(handleLatchClip, 0.65f);
                    interactor?.ShowStatusMessage(lockedNotice);
                    return;
                }

                isLocked = false;
                interactor.ShowStatusMessage(unlockedNotice);
            }

            InitializePose();
            targetOpen = !isOpen;
            interactionActor = interactor != null ? interactor.transform : null;
            elapsed = 0f;
            latchPlayed = false;
            swingStarted = false;
            isAnimating = true;

            if (interactor == null) return;
            EnsureDoorHandleTarget();
            var procedural = interactor.GetComponentInChildren<CharacterProceduralController>(true);
            var animation = interactor.GetComponentInChildren<ProtagonistAnimationController>(true);
            if (procedural != null && (animation == null || !animation.IsHoldingFestek) && interactionTarget.IsWithinReach(interactor.transform.position))
                procedural.BeginEnvironmentalInteraction(interactionTarget);
        }

        // The same clock drives the game and editor verification. IK is a visual follower.
        public void StepDoor(float deltaTime)
        {
            if (!isAnimating) return;
            elapsed += Mathf.Max(0f, deltaTime);
            if (!latchPlayed && elapsed >= ContactDelay * 0.6f)
            {
                latchPlayed = true;
                PlayDoorSound(handleLatchClip, 0.85f);
            }

            if (elapsed < ContactDelay) return;
            if (!targetOpen && interactionActor != null)
            {
                Vector3 actor = transform.InverseTransformPoint(interactionActor.position);
                if (Mathf.Abs(actor.x) < 0.79f && actor.z > -0.45f && actor.z < 0.84f)
                {
                    FinishDoor(true);
                    return;
                }
            }
            if (!swingStarted)
            {
                swingStarted = true;
                PlayDoorSound(targetOpen ? openHingeClip : closeHingeClip, 0.90f);
            }

            float progress = Mathf.Clamp01((elapsed - ContactDelay) / Mathf.Max(0.01f, transitionDuration));
            ApplyPose(Mathf.SmoothStep(0f, 1f, targetOpen ? progress : 1f - progress));
            if (progress >= 1f) FinishDoor(targetOpen);
        }

        private void ApplyPose(float openness)
        {
            if (doorLeafLeft != null)
                doorLeafLeft.localRotation = Quaternion.Slerp(closedRotL, openRotL, openness);
            if (doorLeafRight != null)
                doorLeafRight.localRotation = Quaternion.Slerp(closedRotR, openRotR, openness);
            if (blockerCollider != null)
                blockerCollider.enabled = openness < 0.8f;
        }

        private void FinishDoor(bool open)
        {
            ApplyPose(open ? 1f : 0f);
            isAnimating = false;
            interactionActor = null;
            if (isOpen == open) return;
            isOpen = open;
            OnDoorStateChanged?.Invoke(isOpen);
        }

        private void PlayDoorSound(AudioClip clip, float volume)
        {
            if (Application.isPlaying && doorAudioSource != null && clip != null)
                doorAudioSource.PlayOneShot(clip, volume);
        }

        public void SetOpenImmediate(bool open)
        {
            InitializePose();
            FinishDoor(open);
        }

        public void Configure(Transform left, Transform right, Collider blocker)
        {
            doorLeafLeft = left;
            doorLeafRight = right;
            blockerCollider = blocker;
            poseInitialized = false;
        }

        public void ConfigureAudio(AudioSource source, AudioClip latch, AudioClip openHinge, AudioClip closeHinge)
        {
            doorAudioSource = source;
            handleLatchClip = latch;
            openHingeClip = openHinge;
            closeHingeClip = closeHinge;
        }

        public void SetLockState(bool locked, string keyId, string lockNotice)
        {
            isLocked = locked;
            requiredKeyId = keyId;
            lockedNotice = lockNotice;
        }
    }
}
