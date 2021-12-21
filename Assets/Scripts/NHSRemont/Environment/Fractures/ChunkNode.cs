using NHSRemont.Gameplay;
using NHSRemont.Networking;
using NHSRemont.Utility;
using Photon.Pun;
using UnityEngine;

namespace NHSRemont.Environment.Fractures
{
    public class ChunkNode : GraphNode
    {
        private const float unfreezeObjectScaling = 0.925f;

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

        public void SetCollider(MeshCollider collider)
        {
            this.collider = collider;
            meshCollider = collider;
            savedMesh = new SerialisableMesh(collider.sharedMesh);
        }

        private void FixedUpdate()
        {
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
            syncedRb.rb.mass = mass;
            PhysicsManager.instance.RegisterRigidbody(syncedRb.rb, category);

            transform.SetParent(PhysicsManager.instance.transform);

            return syncedRb.rb;
        }
    }
}