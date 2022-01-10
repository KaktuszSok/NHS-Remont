using System;
using System.Collections.Generic;
using NHSRemont.Networking;
using TMPro;
using UnityEngine;

namespace NHSRemont.UI
{
    /// <summary>
    /// Displays FPS and bandwidth per category as measured by Bandwidth Limiter
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class PerformanceMonitor : MonoBehaviour
    {
        private TextMeshProUGUI text;
        private string bandwidthText;

        private void Awake()
        {
            text = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            BandwidthLimiter.onBandwidthUsageMeasured += UpdateText;
        }

        private void OnDestroy()
        {
            BandwidthLimiter.onBandwidthUsageMeasured -= UpdateText;
        }

        private void Update()
        {
            text.text = GetFPSText(Time.deltaTime) + "\n" + bandwidthText;
        }

        private string GetFPSText(float deltaTime)
        {
            return "FRAME TIME: " + (deltaTime * 1000f).ToString("0.00") + "ms";
        }

        private void UpdateText(Dictionary<BandwidthLimiter.BandwidthBudgetCategory, int> usages)
        {
            string txt = "BANDWIDTH USAGE:";
            foreach (var keyValuePair in usages)
            {
                txt += "\n" + keyValuePair.Key + " - " + (keyValuePair.Value / 1000f).ToString("0.00") + "KB/s";
            }
            bandwidthText = txt;
        }
    }
}