using TheLastBell.Interaction;
using UnityEngine;

namespace TheLastBell.Player
{
    [CreateAssetMenu(menuName = "The Last Bell/Inventory Item")]
    public sealed class InventoryItemDefinition : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private Texture2D iconTexture;
        [System.NonSerialized] private Sprite fallbackIcon;
        [SerializeField] private GameObject model;
        [SerializeField] private bool questItem;
        [SerializeField] private HandGripProfile grip;
        [SerializeField] private HandGripProfile pocketGrip;
        [SerializeField, Min(0.2f)] private float pocketDuration = 0.72f;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon
        {
            get
            {
                if (icon != null) return icon;
                if (fallbackIcon == null && iconTexture != null)
                    fallbackIcon = Sprite.Create(iconTexture, new Rect(0, 0, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
                return fallbackIcon;
            }
        }
        public GameObject Model => model;
        public bool IsQuestItem => questItem;
        public HandGripProfile Grip => grip.objectRadius > 0f ? grip : HandGripProfile.Device;
        public HandGripProfile PocketGrip => pocketGrip.objectRadius > 0f ? pocketGrip : Grip;
        public float PocketDuration => Mathf.Max(0.2f, pocketDuration);

        public void Configure(string id, string title, string details, Sprite itemIcon, GameObject physicalModel,
            HandGripProfile handGrip, bool isQuestItem, Texture2D fallbackTexture = null, HandGripProfile storageGrip = default)
        {
            itemId = id;
            displayName = title;
            description = details;
            icon = itemIcon;
            iconTexture = fallbackTexture;
            model = physicalModel;
            grip = handGrip;
            pocketGrip = storageGrip;
            questItem = isQuestItem;
        }
    }

    public interface IInventoryPhysicalItem
    {
        bool TryEquip(GameObject player);
        void StoreAt(Transform pocket);
    }
}
