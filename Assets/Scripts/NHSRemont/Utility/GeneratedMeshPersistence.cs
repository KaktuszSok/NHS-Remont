using UnityEngine;

namespace NHSRemont.Utility
{
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteInEditMode]
    public class GeneratedMeshPersistence : MonoBehaviour
    {
        [SerializeField]
        private SerialisableMesh savedMesh;

        private void Awake()
        {
            ApplyMesh();
        }

        public void SetMesh(Mesh mesh)
        {
            savedMesh = new SerialisableMesh(mesh);
            ApplyMesh();
        }

        public void ApplyMesh()
        {
            if(savedMesh == null)
                return;
            
            GetComponent<MeshFilter>().sharedMesh = savedMesh.CreateMesh();
        }
    }
}