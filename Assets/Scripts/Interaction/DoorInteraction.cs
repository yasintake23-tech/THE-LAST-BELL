using System.Collections;
using UnityEngine;
using TMPro;

namespace TheLastBell.Interaction
{
    /// <summary>
    /// THE LAST BELL - Okul Giriş Kapısı Etkileşim Script'i.
    /// Oyuncu yaklaştığında [E] etkileşim yazısını gösterir.
    /// PC'de 'E' tuşu veya mobilde dokunmatik butonla kapıyı açar.
    /// Kapı açıldığında pürüzsüz animasyonla döner ve koridora geçişi sağlar.
    /// </summary>
    public class DoorInteraction : MonoBehaviour
    {
        [Header("--- Kapı Kanatları ---")]
        [Tooltip("Dönecek kapı kanadı Transform'u")]
        [SerializeField] private Transform doorLeaf;
        [Tooltip("İkinci kapı kanadı (çift kanatlı kapı için isteğe bağlı)")]
        [SerializeField] private Transform doorLeafRight;
        [Tooltip("Kapı kapalıyken yolu tıkayan Collider")]
        [SerializeField] private Collider doorBlockerCollider;

        [Header("--- Etkileşim Ayarları ---")]
        [SerializeField] private float interactionDistance = 2.8f;
        [SerializeField] private float openAngle = 95f;
        [SerializeField] private float openDuration = 1.3f;

        [Header("--- UI Bildirimi ---")]
        [SerializeField] private GameObject interactionPromptUI;
        [SerializeField] private TextMeshProUGUI promptText;

        private Transform playerTransform;
        private bool isPlayerNearby = false;
        private bool isOpen = false;
        private bool isOpening = false;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
                return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            bool nearby = dist <= interactionDistance && !isOpen && !isOpening;

            if (nearby != isPlayerNearby)
            {
                isPlayerNearby = nearby;
                UpdatePromptUI(isPlayerNearby);
            }

            // PC Etkileşim Tuşu: E
            if (isPlayerNearby && Input.GetKeyDown(KeyCode.E) && !isOpen && !isOpening)
            {
                OpenDoor();
            }
        }

        public void OpenDoor()
        {
            if (isOpen || isOpening) return;
            StartCoroutine(AnimateDoorOpening());
        }

        private IEnumerator AnimateDoorOpening()
        {
            isOpening = true;
            UpdatePromptUI(false);

            // Girişi açmak için blocker collider'ı devre dışı bırak
            if (doorBlockerCollider != null)
            {
                doorBlockerCollider.enabled = false;
            }

            Quaternion startRotL = doorLeaf != null ? doorLeaf.localRotation : Quaternion.identity;
            Quaternion endRotL = startRotL * Quaternion.Euler(0f, -openAngle, 0f);

            Quaternion startRotR = doorLeafRight != null ? doorLeafRight.localRotation : Quaternion.identity;
            Quaternion endRotR = startRotR * Quaternion.Euler(0f, openAngle, 0f);

            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / openDuration);

                if (doorLeaf != null)
                {
                    doorLeaf.localRotation = Quaternion.Slerp(startRotL, endRotL, t);
                }
                if (doorLeafRight != null)
                {
                    doorLeafRight.localRotation = Quaternion.Slerp(startRotR, endRotR, t);
                }

                yield return null;
            }

            if (doorLeaf != null) doorLeaf.localRotation = endRotL;
            if (doorLeafRight != null) doorLeafRight.localRotation = endRotR;

            isOpen = true;
            isOpening = false;
            Debug.Log("[THE LAST BELL] Okulun ana giriş kapısı açıldı! Koridora giriş serbest.");
        }

        private void UpdatePromptUI(bool show)
        {
            if (interactionPromptUI != null)
            {
                interactionPromptUI.SetActive(show);
                if (promptText != null && show)
                {
                    promptText.text = Application.isMobilePlatform ? "KAPIYI AÇ" : "[E] - Okul Kapısını Aç";
                }
            }
        }

        public bool IsNearby => isPlayerNearby;
        public bool IsOpen => isOpen;

        /// <summary>
        /// Sahne oluşturucu tarafından reflection olmadan temiz yapılandırma sağlar.
        /// </summary>
        public void Initialize(Transform leftLeaf, Transform rightLeaf, Collider blocker, GameObject promptUI, TextMeshProUGUI promptTmp)
        {
            doorLeaf = leftLeaf;
            doorLeafRight = rightLeaf;
            doorBlockerCollider = blocker;
            interactionPromptUI = promptUI;
            promptText = promptTmp;
        }
    }
}
