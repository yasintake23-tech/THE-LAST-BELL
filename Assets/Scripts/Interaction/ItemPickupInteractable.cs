using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    public enum ItemType
    {
        Key,
        FlashlightBattery,
        QuestItem
    }

    /// <summary>
    /// THE LAST BELL - Toplanabilir Eya Etkileimi (Item Pickup Interactable).
    /// Anahtar, el feneri pili ve grev eyalarnn toplanmasn salar.
    /// </summary>
    public class ItemPickupInteractable : MonoBehaviour, IInteractable
    {
        [Header("--- Eya Bilgisi ---")]
        [SerializeField] private ItemType itemType = ItemType.Key;
        [SerializeField] private string itemId = "Classroom103_Key";
        [SerializeField] private string displayName = "Pasl Okul Anahtar";

        [Header("--- Ses & Grsel ---")]
        [SerializeField] private AudioClip pickupSound;

        public string GetInteractionPrompt() => $"[E] - {displayName} Al";

        public bool CanInteract() => gameObject.activeSelf;

        public void Interact(PlayerInteractor interactor)
        {
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                switch (itemType)
                {
                    case ItemType.Key:
                        inventory.AddKey(itemId, displayName);
                        break;
                    case ItemType.FlashlightBattery:
                        inventory.AddBattery(1);
                        break;
                    case ItemType.QuestItem:
                        inventory.AddKey(itemId, displayName);
                        break;
                }
            }

            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position, 1.0f);
            }

            interactor.ShowStatusMessage($"{displayName} envantere eklendi.");
            gameObject.SetActive(false);
        }

        public void Configure(ItemType type, string id, string name, AudioClip sound)
        {
            itemType = type;
            itemId = id;
            displayName = name;
            pickupSound = sound;
        }
    }
}
