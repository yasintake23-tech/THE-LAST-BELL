using System.Collections;
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
        private Transform latchGrip;
        private Quaternion latchRest;
        private Transform oppositeLatch;
        private Transform latchVisual;
        private Transform oppositeVisual;
        private Vector3 latchStart;
        private Vector3 oppositeStart;
        [SerializeField] private bool classroomDoor;
        private const float ContactDelay = 0.42f;
        private bool isAnimating;
        private bool stowingItem;
        private PhysicalContact contact;
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
            if (!isAnimating) return;
            StopAllCoroutines();
            if (stowingItem && interactionActor != null)
                interactionActor.GetComponent<PlayerInventory>()?.CancelFocusedStorage();
            FinishDoor(contact == null || swingStarted ? targetOpen : isOpen);
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

            Transform back = handle.parent.Find("Door_Handle_Opposite");
            if (back == null)
            {
                back = new GameObject("Door_Handle_Opposite").transform;
                back.SetParent(handle.parent, false);
                Vector3 mirrored = handle.localPosition;
                mirrored.z = -mirrored.z;
                back.localPosition = mirrored;
                back.localRotation = handle.localRotation * Quaternion.Euler(0, 180, 0);
            }
            latchGrip = handle;
            oppositeLatch = back;
            latchVisual = handle.parent.Find("Door_Lever_Pivot");
            oppositeVisual = handle.parent.Find("Door_Lever_Pivot_Back");
            latchStart = handle.localPosition;
            oppositeStart = back.localPosition;
            latchRest = handle.localRotation;
            interactionTarget.ConfigureApproach(null, null, back);
            interactionTarget.Configure(handle, PreferredInteractionHand.Left, "DoorOpen", 2.0f, 0.38f, 0.65f);
            var grip = HandGripProfile.Handle;
            if (classroomDoor) grip.wristAngles = new Vector3(7f, 5f, -10f);
            interactionTarget.ConfigurePhysicalContact(2.38f, 0.51f, grip);
        }

        public string GetInteractionPrompt()
        {
            if (isAnimating) return "";
            if (isLocked) return "[E] - Kapıyı Aç (Kilitli)";
            return isOpen ? "[E] - Kapıyı Kapat" : "[E] - Kapıyı Aç";
        }

        public bool CanInteract() => isActiveAndEnabled && !isAnimating && (doorLeafLeft != null || doorLeafRight != null);

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

            PlayerInventory stow = null;
            if (Application.isPlaying && interactor != null)
            {
                if (interactionTarget == null) EnsureDoorHandleTarget();
                var animation = interactor.GetComponentInChildren<ProtagonistAnimationController>(true);
                var inventory = interactor.GetComponent<PlayerInventory>();
                if (animation != null && animation.IsHoldingFestek && inventory != null && inventory.EquippedItem != null)
                    stow = inventory;
                contact = PhysicalContact.TryBegin(interactor.gameObject, interactionTarget, this, allowOccupiedHand: stow != null);
                if (contact == null)
                {
                    interactor.ShowStatusMessage(PhysicalContact.ObstructionMessage);
                    return;
                }
            }

            InitializePose();
            targetOpen = !isOpen;
            interactionActor = interactor != null ? interactor.transform : null;
            elapsed = 0f;
            latchPlayed = false;
            swingStarted = false;
            isAnimating = true;
            if (stow != null)
            {
                stowingItem = true;
                StartCoroutine(FreeLeftHand(stow));
            }
        }

        private IEnumerator FreeLeftHand(PlayerInventory inventory)
        {
            yield return inventory.StoreEquippedForInteraction();
            stowingItem = false;
            if (inventory.EquippedItem != null)
            {
                interactionActor?.GetComponent<PlayerInteractor>()?.ShowStatusMessage("Kapı kolu için sol el boşaltılamadı.");
                FinishDoor(isOpen);
            }
        }

        // Physical contact gates the swing in play mode. The editor also exercises the door mechanism directly.
        public void StepDoor(float deltaTime)
        {
            if (!isAnimating || stowingItem) return;
            if (contact != null)
            {
                contact.Step(deltaTime);
                if (!contact.Ready)
                {
                    if (contact.Failed)
                    {
                        interactionActor?.GetComponent<PlayerInteractor>()?.ShowStatusMessage(PhysicalContact.ObstructionMessage);
                        FinishDoor(isOpen);
                    }
                    return;
                }
            }
            elapsed += Mathf.Max(0f, deltaTime);
            if (!latchPlayed && elapsed >= ContactDelay * 0.6f)
            {
                latchPlayed = true;
                PlayDoorSound(handleLatchClip, 0.85f);
            }

            if (elapsed < ContactDelay)
            {
                if (classroomDoor && latchGrip != null)
                {
                    float depress = Mathf.Sin(Mathf.Clamp01(elapsed / ContactDelay) * Mathf.PI * 0.5f);
                    SetLatch(-20f * depress);
                }
                return;
            }
            if (!targetOpen && interactionActor != null && !swingStarted)
            {
                Vector3 actor = transform.InverseTransformPoint(interactionActor.position);
                if (Mathf.Abs(actor.x) < 0.79f && actor.z > -0.45f && actor.z < 0.84f)
                {
                    interactionActor.GetComponent<PlayerInteractor>()?.ShowStatusMessage("Kapının geçişi açık kalmalı.");
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
            contact?.FadeContact(1f - Mathf.InverseLerp(0.72f, 0.97f, progress));
            if (classroomDoor) SetLatch(-20f * (1f - Mathf.InverseLerp(0.15f, 0.52f, progress)));
            ApplyPose(Mathf.SmoothStep(0f, 1f, targetOpen ? progress : 1f - progress));
            if (progress >= 1f) FinishDoor(targetOpen);
        }

        private void SetLatch(float degrees)
        {
            Quaternion movement = Quaternion.Euler(0, 0, degrees);
            if (latchVisual != null && latchGrip != null)
            {
                latchVisual.localRotation = movement;
                latchGrip.localPosition = latchVisual.localPosition + movement * (latchStart - latchVisual.localPosition);
                latchGrip.localRotation = movement * latchRest;
            }
            if (oppositeVisual != null && oppositeLatch != null)
            {
                oppositeVisual.localRotation = movement;
                oppositeLatch.localPosition = oppositeVisual.localPosition + movement * (oppositeStart - oppositeVisual.localPosition);
                oppositeLatch.localRotation = movement;
            }
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
            contact?.End();
            contact = null;
            if (classroomDoor) SetLatch(0f);
            ApplyPose(open ? 1f : 0f);
            isAnimating = false;
            stowingItem = false;
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

        public void ConfigureClassroomDoor()
        {
            classroomDoor = true;
            openAngle = 94f;
            EnsureDoorHandleTarget();
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
