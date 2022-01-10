using UnityEngine;

namespace NHSRemont.Utility
{
    public static class ColourUtils
    {
        /// <summary>
        /// Returns a colour with a randomised hue
        /// <param name="alpha">The opacity of the colour</param>
        /// <param name="saturation">The saturation of the colour</param>
        /// <param name="value">The value (brightness) of the colour</param>
        /// </summary>
        public static Color RandomColour(float alpha = 1.0f, float saturation = 1.0f, float value = 1.0f)
        {
            float hue = Random.Range(0f, 1.0f);
            Color rgb = Color.HSVToRGB(hue, saturation, value);
            rgb.a = alpha;
            return rgb;
        }
    }
}