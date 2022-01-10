using NHSRemont.Utility;
using UnityEngine;

namespace NHSRemont.Gameplay
{
    [CreateAssetMenu(fileName = "Gameplay References", menuName = "NHS/Settings/Gameplay References")]
    public class GameplayReferences : ScriptableObject
    {
        public GameObject explosionVFX;
        public Mesh chunkFragmentsMesh;
        public SFXCollection emptyHandPunchSFX;
        
        public LayerMask bulletCollisionLayers = ~0;
    }
}