using System;
using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    /// <summary>
    /// THE LAST BELL - Oyuncu Etkileim Denetleyicisi (Player Interactor).
    /// Kamerann / karakterin bakt dorultuda Raycast atarak etkileime geilebilir nesneleri (IInteractable) tespit eder.
    /// Bilgisayarda 'E' tuu veya Android'de dokunmatik etkileim butonu ile tetiklenir.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("--- Mesafe ve Alglama ---")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float maxInteractionDistance = 3.2f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("--- Gecikme & Gvenlik ---")]
        [SerializeField] private float interactionCooldown = 0.25f;

        private IInteractable currentInteractable = null;
        private float lastInteractTime = -1f;
        private TPSController tps;
        private FirstPersonController fpc;

        // Olaylar (UI / HUD gncellemesi iin)
        public event Action<string> OnPromptUpdated;
        public event Action<string> OnStatusMessageTriggered;

        public IInteractable CurrentInteractable => currentInteractable;

        private void Awake()
        {
            tps = GetComponent<TPSController>();
            fpc = GetComponent<FirstPersonController>();
            if (playerCamera == null && Camera.main != null)
            {
                playerCamera = Camera.main.transform;
            }
        }

        private void Update()
        {
            CheckForInteractable();

            if (Input.GetKeyDown(KeyCode.E))
            {
                TriggerInteraction();
            }
        }

        private void CheckForInteractable()
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 forward = transform.forward;

            if (playerCamera != null)
            {
                origin = playerCamera.position;
                forward = playerCamera.forward;
            }

            Ray ray = new Ray(origin, forward);
            RaycastHit hit;

            IInteractable foundInteractable = null;

            if (Physics.Raycast(ray, out hit, maxInteractionDistance, interactionLayers, QueryTriggerInteraction.Collide))
            {
                foundInteractable = hit.collider.GetComponentInParent<IInteractable>();
                if (foundInteractable == null)
                {
                    foundInteractable = hit.collider.GetComponent<IInteractable>();
                }
            }

            // Eer kamera raycast'i bulamadysa, karakterin tam nne ksa bir SphereCast yap
            if (foundInteractable == null)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 1.0f + transform.forward * 1.2f, 1.2f, interactionLayers, QueryTriggerInteraction.Collide);
                foreach (var h in hits)
                {
                    var it = h.GetComponentInParent<IInteractable>() ?? h.GetComponent<IInteractable>();
                    if (it != null)
                    {
                        foundInteractable = it;
                        break;
                    }
                }
            }

            if (foundInteractable != currentInteractable)
            {
                currentInteractable = foundInteractable;
                if (currentInteractable != null && currentInteractable.CanInteract())
                {
                    OnPromptUpdated?.Invoke(currentInteractable.GetInteractionPrompt());
                }
                else
                {
                    OnPromptUpdated?.Invoke(null);
                }
            }
        }

        public void TriggerInteraction()
        {
            if (Time.time - lastInteractTime < interactionCooldown) return;
            lastInteractTime = Time.time;

            if (currentInteractable != null && currentInteractable.CanInteract())
            {
                if (tps != null)
                {
                    if (currentInteractable is ItemPickupInteractable) tps.PlayPickupAnimation();
                    else tps.PlayInteractAnimation();
                }
                else if (fpc != null)
                {
                    if (currentInteractable is ItemPickupInteractable) fpc.PlayPickupAnimation();
                    else fpc.PlayInteractAnimation();
                }

                currentInteractable.Interact(this);

                if (currentInteractable != null && currentInteractable.CanInteract())
                {
                    OnPromptUpdated?.Invoke(currentInteractable.GetInteractionPrompt());
                }
                else
                {
                    OnPromptUpdated?.Invoke(null);
                }
            }
        }

        public void ShowStatusMessage(string message)
        {
            OnStatusMessageTriggered?.Invoke(message);
        }
    }
}
