using UnityEngine;

namespace NHSRemont.Gameplay
{
    /// <summary>
    /// This dictates how much damage an entity receives from explosions, accounting only for their power and distance.
    /// </summary>
    [CreateAssetMenu(fileName = "Explosion Damage Profile", menuName = "NHS/Settings/Explosion Damage Profile", order = 0)]
    public class ExplosionDamageProfile : ScriptableObject
    {
        [SerializeField, Tooltip("A constant which linearly scales the amount of damage explosions do to an entity using this profile.")]
        private float baseExplosionDamage = 100f;
        
        [SerializeField, Tooltip(
             "An explosion equivalent to X kilograms of TNT will deal this profile's base damage at a distance of Y metres." + 
             "\nIt will deal more damage at a lesser distance and less damage at a greater distance.")]
        private AnimationCurve tntToBaseDamageDistance;

        [SerializeField, Tooltip(
             "An explosion who's distance/baseDamageDistance is X will have its damage multiplied by Y." +
             "\nAt X=1, Y should be =1." +
             "\nAt X=0, Y is the maximum damage multiplier for being close to explosions." +
             "\nAt Y=0, X is the maximum distance, as a fraction of base damage distance, where the explosion deals any damage." +
             "\nFor example, if Y=0 at X=2, explosions will only deal damage at up to twice their base damage distance.")]
        private AnimationCurve distanceFractionToDamage;

        /// <summary>
        /// Calculates the damage dealt to this entity by a given explosion
        /// </summary>
        public float CalculateDamage(Transform entity, ExplosionInfo explosionInfo)
        {
            float distance = (entity.position - explosionInfo.position).magnitude;
            float maxTNT = tntToBaseDamageDistance.keys[tntToBaseDamageDistance.length - 1].value;
            float baseDamageDistance = tntToBaseDamageDistance.Evaluate(Mathf.Min(explosionInfo.power_tnt, maxTNT));
            Debug.Log(explosionInfo.power_tnt + "g = " + baseDamageDistance + "m");
            if (baseDamageDistance == 0)
                return 0f;
            
            float fraction = distance / baseDamageDistance;
            float maxFraction = distanceFractionToDamage.keys[distanceFractionToDamage.length - 1].time;
            if (fraction >= maxFraction)
                return 0f;
            
            float damage = baseExplosionDamage * distanceFractionToDamage.Evaluate(fraction);
            return damage;
        }
    }
}