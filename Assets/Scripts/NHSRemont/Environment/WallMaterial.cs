using NHSRemont.Gameplay;
using NHSRemont.Utility;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

namespace NHSRemont.Environment.Fractures
{
    [CreateAssetMenu(fileName = "New Wall Material", menuName = "NHS/Wall Material")]
    public class WallMaterial : ScriptableObject
    {
        public float density = 2400;
        [Tooltip("How much impulse per unit density can chunks withstand before being broken off?")]
        public float internalStrength = 5f;
        [Tooltip("The higher this number, the more difficult it is to penetrate this material.")]
        public float penetrationMultiplier = 1.0f;

        public Material outsideMaterial;
        public Material insideMaterial;
        
        public SFXCollection breakOffSound;
        public SFXCollection hardImpulseSound;
        public SFXCollection destroySound;
        
        [Tooltip("VFX that plays when a wall with this material is destroyed")]
        public GameObject destroyVFX;
        [Tooltip("VFX that plays when a wall with this material is damaged (e.g. shot at)")]
        public GameObject damageVFX;

        [Tooltip("Prefab to use for holes in this wall. These are scaled by the bullet's hole size multiplier (1x for standard rifle rounds).")]
        public GameObject bulletHolePrefab;
        
        [FormerlySerializedAs("destroyVFXColour")]
        public Color VFXColour = Color.white;
        public string VFXColourField = "Colour";

        /// <summary>
        /// Automatically sets the outside and inside materials if they are null
        /// </summary>
        /// <param name="gameObject">The gameobject to source the materials from, using its renderer</param>
        public void AutoDetectMaterials(GameObject gameObject)
        {
            if (outsideMaterial == null)
            {
                outsideMaterial = gameObject.GetComponent<Renderer>()?.sharedMaterial;
            }

            if (insideMaterial == null)
            {
                Renderer rend = gameObject.GetComponent<Renderer>();
                if (!rend)
                {
                    insideMaterial = outsideMaterial;
                }
                else
                {
                    var mats = rend.sharedMaterials;
                    insideMaterial = mats.Length > 1 ? mats[1] : mats[0];
                }
            }
        }

        public void PlayDestroyVFXAndSFX(Vector3 position, Quaternion rotation)
        {
            destroySound.PlayRandomSoundAtPosition(position);
            
            GameObject VFXInstance = Instantiate(destroyVFX, position, rotation);
            VisualEffect vfx = VFXInstance.GetComponent<VisualEffect>();
            if (!string.IsNullOrEmpty(VFXColourField) && vfx.HasVector4(VFXColourField))
            {
                vfx.SetVector4(VFXColourField, VFXColour);
            }
            vfx.Play();
        }
        
        public void PlayImpactVFXAndSFX(Vector3 position, Quaternion rotation, float sfxVolume = 1f, float sfxPitch = 1f)
        {
            hardImpulseSound.PlayRandomSoundAtPosition(position, sfxVolume, 1.3f);

            SpawnDamageVFX(position, rotation);
            SpawnImpactParticles(position, rotation*Vector3.forward);
        }

        /// <summary>
        /// Spawns visual effects for this wall being damaged (by impact, being broken off, etc)
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void SpawnDamageVFX(Vector3 position, Quaternion rotation)
        {
            GameObject VFXInstance = Instantiate(damageVFX, position, rotation);
            VisualEffect vfx = VFXInstance.GetComponent<VisualEffect>();
            if (!string.IsNullOrEmpty(VFXColourField) && vfx.HasVector4(VFXColourField))
            {
                vfx.SetVector4(VFXColourField, VFXColour);
            }
            vfx.Play();
        }

        /// <summary>
        /// Spawns a particle system depicting little chunks of wall flying out from a point in some general direction 
        /// </summary>
        private void SpawnImpactParticles(Vector3 point, Vector3 direction)
        {
            float lifetime = 5f;
            float sizeMin = 0.03f;
            float sizeMax = 0.09f;
            float speedMin = 1.0f;
            float speedMax = 5.0f;
            float angle = 22.5f;
            
            ParticleSystem particles = new GameObject(name + " Damage PFX").AddComponent<ParticleSystem>();
            particles.Stop();
            particles.gameObject.AddComponent<Autodestroy>().destroyTimer = lifetime;
            particles.transform.position = point;
            particles.transform.forward = direction;
            
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = main.duration = lifetime;
            main.gravityModifier = 1f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.9f, 1f),
                new Keyframe(1f, 0f)
            ));

            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounceMultiplier = 0.25f;
            collision.dampen = 0.5f;
            
            ParticleSystem.RotationBySpeedModule rotationBySpeed = particles.rotationBySpeed;
            rotationBySpeed.enabled = true;
            rotationBySpeed.range = new Vector2(0f, speedMax);
            rotationBySpeed.separateAxes = false;
            ParticleSystem.MinMaxCurve angVelCurve = new()
            {
                mode = ParticleSystemCurveMode.TwoCurves,
                curveMultiplier = 3*360f*Mathf.Deg2Rad,
                curveMin = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 1/6f)),
                curveMax = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 1f))
            };
            rotationBySpeed.z = angVelCurve;

            ParticleSystem.Particle[] ps = new ParticleSystem.Particle[3];
            for (int i = 0; i < ps.Length; i++)
            {
                Vector3 randomisedDirection = VectorUtils.RandomiseDirection(Vector3.forward, angle);
                Vector3 localPos = randomisedDirection * 0.1f;
                float randomisedLifetime = lifetime * Random.Range(0.4f, 1f);
                ps[i] = new ParticleSystem.Particle()
                {
                    position = localPos,
                    axisOfRotation = Random.onUnitSphere,
                    rotation = Random.Range(0,360f),
                    velocity = randomisedDirection*Random.Range(speedMin, speedMax),
                    startLifetime = randomisedLifetime,
                    remainingLifetime = randomisedLifetime,
                    startSize3D = new Vector3(Random.Range(sizeMin, sizeMax), Random.Range(sizeMin, sizeMax), Random.Range(sizeMin, sizeMax)),
                    randomSeed = (uint)Random.Range(int.MinValue, int.MaxValue),
                };
            }
            
            ParticleSystemRenderer particleRend = particles.GetComponent<ParticleSystemRenderer>();
            particleRend.renderMode = ParticleSystemRenderMode.Mesh;
            particleRend.mesh = GameManager.instance.gameplayReferences.chunkFragmentsMesh;
            particleRend.sharedMaterial = outsideMaterial;
            
            particles.SetParticles(ps);
            particles.Play();
        }
    }
}