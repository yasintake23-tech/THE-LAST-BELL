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
        [SerializeField] private string unlockedNotice = "Kilit ald!";

        private Quaternion closedRotL;
        private Quaternion openRotL;
        private Quaternion closedRotR;
        private Quaternion openRotR;

        private InteractionTarget interactionTarget;
        private bool isAnimating = false;

        public InteractionTarget DoorInteractionTarget => interactionTarget;

        private void Awake()
        {
            if (doorLeafLeft != null)
            {
                closedRotL = doorLeafLeft.localRotation;
                openRotL = closedRotL * Quaternion.Euler(0f, -openAngle, 0f);
            }
            if (doorLeafRight != null)
            {
                closedRotR = doorLeafRight.localRotation;
                openRotR = closedRotR * Quaternion.Euler(0f, openAngle, 0f);
            }

            if (blockerCollider != null)
            {
                blockerCollider.enabled = !isOpen;
            }

            EnsureDoorHandleTarget();
        }

        private void EnsureDoorHandleTarget()
        {
            interactionTarget = GetComponent<InteractionTarget>();
            if (interactionTarget == null)
            {
                interactionTarget = gameObject.AddComponent<InteractionTarget>();
            }

            Transform leaf = doorLeafLeft != null ? doorLeafLeft : (doorLeafRight != null ? doorLeafRight : transform);
            Transform handle = leaf.Find("Door_Handle_Target");
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
            if (isAnimating) return;

            if (isLocked)
            {
                var inventory = interactor.GetComponent<PlayerInventory>();
                if (inventory != null && !string.IsNullOrEmpty(requiredKeyId) && inventory.HasKey(requiredKeyId))
                {
                    isLocked = false;
                    interactor.ShowStatusMessage(unlockedNotice);
                    TriggerHandInteraction(interactor);
                    StartCoroutine(AnimateDoor(true, 0.42f));
                    return;
                }
                else
                {
                    interactor.ShowStatusMessage(lockedNotice);
                    return;
                }
            }

            TriggerHandInteraction(interactor);
            StartCoroutine(AnimateDoor(!isOpen, 0.42f));
        }

        private void TriggerHandInteraction(PlayerInteractor interactor)
        {
            if (interactor == null) return;
            EnsureDoorHandleTarget();
            var proc = interactor.GetComponentInChildren<CharacterProceduralController>(true);
            if (proc != null && interactionTarget != null)
            {
                proc.BeginEnvironmentalInteraction(interactionTarget);
            }
        }

        private IEnumerator AnimateDoor(bool targetOpen, float contactDelay = 0f)
        {
            isAnimating = true;

            if (contactDelay > 0f)
            {
                yield return new WaitForSeconds(contactDelay);
            }

            // Geçişi açmak için blocker collider hemen kapatılır
            if (targetOpen && blockerCollider != null)
            {
                blockerCollider.enabled = false;
            }

            Quaternion startL = doorLeafLeft != null ? doorLeafLeft.localRotation : Quaternion.identity;
            Quaternion endL = targetOpen ? openRotL : closedRotL;

            Quaternion startR = doorLeafRight != null ? doorLeafRight.localRotation : Quaternion.identity;
            Quaternion endR = targetOpen ? openRotR : closedRotR;

            float elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);

                if (doorLeafLeft != null)
                    doorLeafLeft.localRotation = Quaternion.Slerp(startL, endL, t);
                if (doorLeafRight != null)
                    doorLeafRight.localRotation = Quaternion.Slerp(startR, endR, t);

                yield return null;
            }

            if (doorLeafLeft != null) doorLeafLeft.localRotation = endL;
            if (doorLeafRight != null) doorLeafRight.localRotation = endR;

            isOpen = targetOpen;
            if (blockerCollider != null)
            {
                blockerCollider.enabled = !isOpen;
            }

            isAnimating = false;
        }

        public void Configure(Transform left, Transform right, Collider blocker)
        {
            doorLeafLeft = left;
            doorLeafRight = right;
            blockerCollider = blocker;
        }

        public void SetLockState(bool locked, string keyId, string lockNotice)
        {
            isLocked = locked;
            requiredKeyId = keyId;
            lockedNotice = lockNotice;
        }
    }
}
