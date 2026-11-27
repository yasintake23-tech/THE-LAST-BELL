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

        private bool isAnimating = false;

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
        }

        public string GetInteractionPrompt()
        {
            if (isAnimating) return "";
            if (isLocked) return "[E] - Kapy A (Kilitli)";
            return isOpen ? "[E] - Kapy Kapat" : "[E] - Kapy A";
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
                    StartCoroutine(AnimateDoor(true));
                    return;
                }
                else
                {
                    interactor.ShowStatusMessage(lockedNotice);
                    return;
                }
            }

            StartCoroutine(AnimateDoor(!isOpen));
        }

        private IEnumerator AnimateDoor(bool targetOpen)
        {
            isAnimating = true;

            // Geii amak iin blocker collider hemen kapatlr
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
