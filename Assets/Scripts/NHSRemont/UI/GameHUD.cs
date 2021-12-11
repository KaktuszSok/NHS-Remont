using System.Collections.Generic;
using NHSRemont.Entity;
using NHSRemont.Gameplay.ItemSystem;
using NHSRemont.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NHSRemont.UI
{
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD instance;
        public RectTransform healthBar;
        public RectTransform hotbar;

        private int selectedHotbarSlot = 0;

        private static RuntimePreviewGenerator previewGenerator = new RuntimePreviewGenerator
        {
            BackgroundColor = Color.clear
        };
        private static Dictionary<string, Sprite> itemIcons = new();

        private void Awake()
        {
            instance = this;
        }

        public void UpdateHealthBar(Health health)
        {
            float fraction = health.hp / health.maxHp;
            Vector3 healthBarScale = healthBar.localScale;
            healthBarScale.x = fraction;
            healthBar.localScale = healthBarScale;
        }

        public void UpdateSelectedHotbarSlot(int selectedSlot)
        {
            hotbar.GetChild(selectedHotbarSlot).GetComponent<Image>().color = new Color32(0,0,0,128);
            selectedHotbarSlot = selectedSlot;
            hotbar.GetChild(selectedHotbarSlot).GetComponent<Image>().color = new Color32(0,0,0,200);
        }

        public void UpdateHotbarItemDisplay(Item item, int slotIndex)
        {
            Sprite sprite = GetIcon(item);
            Transform slot = hotbar.GetChild(slotIndex);
            slot.GetComponent<Image>().sprite = sprite;
            slot.GetChild(0).GetComponent<TextMeshProUGUI>().text = (item == null || item.amount == 1) ? "" : item.amount.ToString();
        }

        private Sprite GetIcon(Item item)
        {
            if (item == null)
                return null;

            if (!itemIcons.TryGetValue(item.typeName, out Sprite sprite))
            {
                Texture2D tex = previewGenerator.GenerateModelPreview(item.transform, 128, 128);
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f,0.5f));
                itemIcons[item.typeName] = sprite;
            }
            return sprite;
        }
    }
}