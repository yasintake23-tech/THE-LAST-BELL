using System.Collections;
using UnityEngine;

namespace TheLastBell.Interaction
{
    /// <summary>
    /// THE LAST BELL - ekmece ve Dolap Etkileimi (Drawer / Cabinet Interactable).
    /// ekmeceleri ileri-geri kaydrr veya dolap kapaklarn aar/kapatr.
    /// </summary>
    public class DrawerInteractable : MonoBehaviour, IInteractable
    {
        [Header("--- ekmece / Kapak Transform ---")]
        [SerializeField] private Transform slidingPart;
        [SerializeField] private Vector3 slideOffset = new Vector3(0f, 0f, 0.42f);
        [SerializeField] private float duration = 0.55f;
        [SerializeField] private bool isOpen = false;

        [Header("--- Ses ---")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip slideSound;

        private Vector3 closedPos;
        private Vector3 openPos;
        private bool isAnimating = false;

        private void Awake()
        {
            if (slidingPart == null) slidingPart = transform;
            closedPos = slidingPart.localPosition;
            openPos = closedPos + slideOffset;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1f;
                    audioSource.maxDistance = 10f;
                }
            }
        }

        public string GetInteractionPrompt()
        {
            if (isAnimating) return "";
            return isOpen ? "[E] - ekmeceyi Kapat" : "[E] - ekmeceyi A";
        }

        public bool CanInteract() => !isAnimating;

        public void Interact(PlayerInteractor interactor)
        {
            if (isAnimating) return;
            bool targetOpen = !isOpen;

            if (slideSound != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(slideSound, 0.85f);
            }

            StartCoroutine(AnimateSlide(targetOpen));
        }

        private IEnumerator AnimateSlide(bool targetOpen)
        {
            isAnimating = true;
            Vector3 startPos = slidingPart.localPosition;
            Vector3 endPos = targetOpen ? openPos : closedPos;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                slidingPart.localPosition = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            slidingPart.localPosition = endPos;
            isOpen = targetOpen;
            isAnimating = false;
        }

        public void Configure(Transform part, Vector3 offset, AudioClip sfx)
        {
            slidingPart = part;
            slideOffset = offset;
            slideSound = sfx;
            closedPos = part.localPosition;
            openPos = closedPos + slideOffset;
        }
    }
}
