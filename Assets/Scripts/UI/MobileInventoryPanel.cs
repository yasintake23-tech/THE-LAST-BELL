using TheLastBell.Gameplay;
using TheLastBell.Player;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastBell.UI
{
    public sealed class MobileInventoryPanel : MonoBehaviour
    {
        private PlayerInventory inventory;
        private ChapterObjectiveManager objectives;
        private Font font;
        private GameObject contents;
        private GameObject previous;
        private GameObject next;
        private Text toggleLabel;
        private Text itemName;
        private Text description;
        private Text equipLabel;
        private Button equip;
        private Image icon;
        private int selectedIndex;
        private static readonly Color PanelColor = new Color(0.035f, 0.046f, 0.050f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.14f, 0.17f, 0.16f, 0.97f);
        private static readonly Color TextColor = new Color(0.94f, 0.88f, 0.76f);

        public static void Create(PlayerInventory inventory)
        {
            var hud = GameObject.Find("Gameplay_HUD_Canvas");
            var safeArea = hud != null ? hud.transform.Find("Mobile_Controls_Root") : null;
            if (safeArea == null || safeArea.Find("Mobile_Inventory") != null) return;
            var root = Rect("Mobile_Inventory", safeArea, new Vector2(28f, -231f), new Vector2(300f, 254f));
            root.gameObject.AddComponent<MobileInventoryPanel>().Build(inventory);
        }

        private void Build(PlayerInventory owner)
        {
            inventory = owner;
            objectives = ChapterObjectiveManager.Instance;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MakeButton("Inventory_Open", transform, new Vector2(0f, 0f), new Vector2(167f, 54f),
                "Envanter", Toggle, out toggleLabel);
            var menu = Rect("Inventory_Contents", transform, new Vector2(0f, -61f), new Vector2(300f, 203f));
            contents = menu.gameObject;
            contents.AddComponent<Image>().color = PanelColor;
            icon = Rect("Item_Icon", menu, new Vector2(16f, -13f), new Vector2(61f, 61f)).gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            itemName = Label("Item_Name", menu, new Vector2(90f, -11f), new Vector2(194f, 34f), 24);
            description = Label("Item_Description", menu, new Vector2(90f, -49f), new Vector2(194f, 72f), 18);
            previous = MakeButton("Previous_Item", menu, new Vector2(16f, -135f), new Vector2(55f, 52f),
                "‹", () => Cycle(-1), out _).gameObject;
            next = MakeButton("Next_Item", menu, new Vector2(229f, -135f), new Vector2(55f, 52f),
                "›", () => Cycle(1), out _).gameObject;
            equip = MakeButton("Toggle_Equipment", menu, new Vector2(79f, -135f), new Vector2(142f, 52f),
                "Eline al", EquipSelected, out equipLabel);
            inventory.PhysicalItemsChanged += Refresh;
            if (objectives != null) objectives.OnObjectiveChanged += OnObjectiveChanged;
            contents.SetActive(false);
            Refresh();
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.PhysicalItemsChanged -= Refresh;
            if (objectives != null) objectives.OnObjectiveChanged -= OnObjectiveChanged;
        }

        private void OnObjectiveChanged(ChapterObjectiveState state)
        {
            if (state == ChapterObjectiveState.StoreFestek || state == ChapterObjectiveState.ChapterMilestoneComplete)
                contents.SetActive(false);
            Refresh();
        }

        private void Toggle()
        {
            if (PocketObjective && inventory.EquippedItem != null)
            {
                inventory.ToggleEquipment(inventory.EquippedItem);
                return;
            }
            contents.SetActive(!contents.activeSelf);
            Refresh();
        }

        private bool PocketObjective => objectives != null && objectives.CurrentState == ChapterObjectiveState.StoreFestek;

        private void Cycle(int direction)
        {
            int count = inventory.PhysicalItems.Count;
            if (count < 2) return;
            selectedIndex = (selectedIndex + direction + count) % count;
            Refresh();
        }

        private void EquipSelected()
        {
            if (selectedIndex < inventory.PhysicalItems.Count)
                inventory.ToggleEquipment(inventory.PhysicalItems[selectedIndex]);
        }

        private void Refresh()
        {
            int count = inventory.PhysicalItems.Count;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, count - 1));
            var item = count > 0 ? inventory.PhysicalItems[selectedIndex] : null;
            toggleLabel.text = PocketObjective ? "Cebe koy" : contents.activeSelf ? "Kapat" : "Envanter";
            itemName.text = item != null ? item.Definition.DisplayName : "Envanter boş";
            description.text = item != null
                ? item.Definition.Description + (item == inventory.EquippedItem ? "\nElde" : "\nCep")
                : "";
            icon.sprite = item != null ? item.Definition.Icon : null;
            icon.enabled = icon.sprite != null;
            equip.gameObject.SetActive(item != null);
            equip.interactable = !inventory.IsTransferring;
            equipLabel.text = item != null && item == inventory.EquippedItem ? "Cebe koy" : "Eline al";
            previous.SetActive(count > 1);
            next.SetActive(count > 1);
        }

        private Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size, string title,
            UnityEngine.Events.UnityAction action, out Text text)
        {
            var rect = Rect(name, parent, position, size);
            var bg = rect.gameObject.AddComponent<Image>();
            bg.color = ButtonColor;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(action);
            text = Label("Label", rect, Vector2.zero, size, 20);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = title;
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = TextColor;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
