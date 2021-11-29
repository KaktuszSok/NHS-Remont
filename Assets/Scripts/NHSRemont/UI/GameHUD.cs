using System;
using NHSRemont.Entity;
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
    }
}