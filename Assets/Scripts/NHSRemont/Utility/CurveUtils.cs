using UnityEngine;

namespace NHSRemont.Utility
{
    public static class CurveUtils
    {
        public static float EvaluateClamped(this AnimationCurve curve, float time)
        {
            if (curve.length == 0) return 0f;
            
            time = Mathf.Clamp(time, curve.keys[0].time, curve.keys[curve.length - 1].time);
            return curve.Evaluate(time);
        }
    }
}