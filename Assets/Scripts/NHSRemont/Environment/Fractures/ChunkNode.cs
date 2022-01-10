using NHSRemont.Gameplay;
using NHSRemont.Networking;
using NHSRemont.Utility;
using Photon.Pun;
using UnityEngine;
using UnityEngine.VFX;

namespace NHSRemont.Environment.Fractures
{
    public class ChunkNode : GraphNode
    {
        private const float unfreezeObjectScaling = 0.925f;
        private const float unfreezeParticlesChance = 0.35f;

        public SyncedRigidbody syncedRb;
        [SerializeField]
        private SerialisableMesh savedMesh;
        public MeshCollider meshCollider { get; private set; }
        [SerializeField]
        private PhysicsManager.PhysObjectType _category;

        public PhysicsManager.PhysObjectType category
        {
            get => _category;
            set
            {
                if (!frozen)
                {
                    Debug.LogWarning($"Can not change physics object category of unfrozen chunk! ({_category} to {value})", this);
                    return;
                }
                _category = value;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            ApplySavedMesh();
            //set impulse particles and sound once we unfreeze
            bool makesUnfreezeParticles = transform.GetSiblingIndex() % (1f / unfreezeParticlesChance) < 1f;
            breakOffCallbackLate += _ =>
            {
                if(destroyed) return;
                
                NHSWall wall = GetComponent<NHSWall>();
                if(wall == null)
                    return;
                WallMaterial material = wall.material;

                if(makesUnfreezeParticles)
                    material.SpawnDamageVFX(transform.position, transform.rotation);
                SetImpulseSound(material.hardImpulseSound);
            };
            //spawn chunk destruction particles when destroyed
            destroyedCallback += (_, vel) => SpawnDestructionParticles(vel);
            //play material's destruction effects when destroyed 
            destroyedCallback += (_, _) =>
            {
                WallMaterial material = GetComponent<NHSWall>()?.material;
                if(material == null) return;
                
                material.PlayDestroyVFXAndSFX(transform.position, transform.rotation);
            };
        }

        /// <summary>
        /// Set the mesh of this chunk to the saved mesh in this component's data
        /// </summary>
        /// <param name="force">If true, will set the mesh even if one is already set</param>
        public void ApplySavedMesh(bool force=false)
        {
            meshCollider = (MeshCollider) collider;
            
            if (savedMesh != null && (force || meshCollider.sharedMesh == null))
            {
                Mesh mesh = savedMesh.CreateMesh();
                meshCollider.sharedMesh = mesh;
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Sets the collider of this chunk and persists the mesh so that it is not forgotten by the editor.
        /// </summary>
        public void SetColliderAndSaveMesh(MeshCollider collider)
        {
            this.collider = collider;
            meshCollider = collider;
            savedMesh = new SerialisableMesh(collider.sharedMesh);
        }

        /// <summary>
        /// Set the sound this node plays when receiving a strong impulse.
        /// Only works when called at runtime!
        /// </summary>
        private void SetImpulseSound(SFXCollection sound)
        {
            PlaySoundOnImpulse soundOnImpulse = this.GetOrAddComponent<PlaySoundOnImpulse>();
            soundOnImpulse.SetUp(
                sound,
                breakOffImpulse*0.3f,
                breakOffImpulse*1.4f,
                breakOffImpulse*destroyImpulseFactor,
                true);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!frozen && PhotonNetwork.IsMasterClient)
            {
                if (transform.position.y < -1000)
                {
                    DestroySelf();
                    return;
                }
            }
        }

        public override Rigidbody Unfreeze()
        {
            base.Unfreeze();

            transform.localScale *= unfreezeObjectScaling;
            
            syncedRb = gameObject.GetOrAddComponent<SyncedRigidbody>();
            Rigidbody rb = gameObject.GetOrAddComponent<Rigidbody>();
            rb.mass = mass;
            PhysicsManager.instance.RegisterRigidbody(rb, category);
            
            transform.SetParent(PhysicsManager.instance.transform);

            return syncedRb.rb;
        }

        public override Vector3 GetVelocity()
        {
            if (frozen)
                return Vector3.zero;
            else
                return syncedRb.rb.velocity;
        }

        public override void WritePhysicsState(ref NetworkedPhysicsState state)
        {
            if (frozen)
                base.WritePhysicsState(ref state);
            else
                state.From(syncedRb.rb);
        }

        public override void ApplyPhysicsState(NetworkedPhysicsState state, float lag = 0f)
        {
            if (frozen)
                Unfreeze();
            
            syncedRb.ReceivePhysicsState(state, lag);
        }

        private void SpawnDestructionParticles(Vector3 chunkVel)
        {
            float lifetime = 5f;
            float sizeMin = 0.04f;
            float sizeMax = 0.12f;
            float speedMin = 0.15f; //extra outwards boost as proportion of speed
            float speedMax = 0.3f; //extra outwards boost as proportion of speed
            float inheritedVelocityFactor = 0.6f;
            Mesh chunkMesh = meshCollider.sharedMesh;
            var verts = chunkMesh.vertices;
            Vector3 chunkLocalVelocity = chunkVel;
            chunkLocalVelocity = transform.InverseTransformVector(chunkLocalVelocity);
            chunkLocalVelocity *= inheritedVelocityFactor;
            float chunkLocalSpeed = chunkLocalVelocity.magnitude;
            float chunkSize = chunkMesh.bounds.size.magnitude;
            
            ParticleSystem particles = new GameObject(name + " Destruction PFX").AddComponent<ParticleSystem>();
            particles.Stop();
            particles.gameObject.AddComponent<Autodestroy>().destroyTimer = lifetime;
            particles.transform.position = transform.position;
            particles.transform.rotation = transform.rotation;
            particles.transform.localScale = transform.localScale;
            
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
            rotationBySpeed.range = new Vector2(0f, speedMax*chunkLocalSpeed);
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

            ParticleSystem.Particle[] ps = new ParticleSystem.Particle[10];
            for (int i = 0; i < ps.Length; i++)
            {
                Vector3 localPos = RandomPointInMesh();
                float randomisedLifetime = lifetime * Random.Range(0.4f, 1f);
                ps[i] = new ParticleSystem.Particle()
                {
                    position = localPos,
                    axisOfRotation = Random.onUnitSphere,
                    rotation = Random.Range(0,360f),
                    velocity = chunkLocalVelocity + localPos.normalized*Random.Range(speedMin, speedMax)*chunkLocalSpeed,
                    startLifetime = randomisedLifetime,
                    remainingLifetime = randomisedLifetime,
                    startSize3D = new Vector3(chunkSize*Random.Range(sizeMin, sizeMax), chunkSize*Random.Range(sizeMin, sizeMax), chunkSize*Random.Range(sizeMin, sizeMax)),
                    randomSeed = (uint)Random.Range(int.MinValue, int.MaxValue),
                };
            }
            
            ParticleSystemRenderer particleRend = particles.GetComponent<ParticleSystemRenderer>();
            particleRend.renderMode = ParticleSystemRenderMode.Mesh;
            particleRend.mesh = GameManager.instance.gameplayReferences.chunkFragmentsMesh;
            particleRend.sharedMaterial = GetComponent<Renderer>().sharedMaterial;
            
            particles.SetParticles(ps);
            particles.Play();

            Vector3 RandomPointInMesh()
            {
                //choose 3 random points and average their position.
                //ensure no points repeat by choosing each point from a different set of vertices.
                int p1IdxCap = verts.Length / 3;
                int p2IdxCap = (verts.Length / 3)*2;
                int p3IdxCap = verts.Length;

                int p1Idx = Random.Range(0, p1IdxCap);
                int p2Idx = Random.Range(p1IdxCap, p2IdxCap);
                int p3Idx = Random.Range(p2IdxCap, p3IdxCap);

                Vector3 point = verts[p1Idx] + verts[p2Idx] + verts[p3Idx];
                point /= 3;
                return point;
            }
        }
    }
}