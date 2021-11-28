using System;
using UnityEngine;

namespace NHSRemont.UI
{
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD instance;
        public RectTransform healthBar;

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
    }
}