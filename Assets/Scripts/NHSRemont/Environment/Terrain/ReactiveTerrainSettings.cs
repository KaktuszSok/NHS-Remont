using UnityEngine;

namespace NHSRemont.Environment.Terrain
{
    [CreateAssetMenu(fileName = "Reactive Terrain Settings", menuName = "NHS/Settings/Reactive Terrain")]
    public class ReactiveTerrainSettings : ScriptableObject
    {
        [Tooltip("How powerful does an explosion have to be to fully char the terrain at the explosion's very centre? Measured in intensity of shockwave at ground texel point.")]
        public float explosionIntensityForFullyCharred = 14000f;
        
        [Tooltip("A multiplier to the required explosion intensity to remove grass compared to charring the land")]
        public float detailsRemovalDifficulty = 0.5f;
        
        [Tooltip("A multiplier to the depth of craters")]
        public float terrainDeformationFactor = 1f;

        public AnimationCurve charringCurve = new AnimationCurve(new[]
        {
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.19f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, 0f, 0f),
        });
        
        public AnimationCurve craterShape = new AnimationCurve(new []
        {
            new Keyframe(0f, -0.04f, 0f, 0f),
            new Keyframe(0.17f, 0.009f, 0f, 0f),
            new Keyframe(0.205f, 0f, 0f, 0f),
            new Keyframe(0.5f, 0f, 0f, 0f),
            new Keyframe(1f, 0f, 0f, 0f)
        });
    }
}