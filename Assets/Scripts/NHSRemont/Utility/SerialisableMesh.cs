using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NHSRemont.Utility
{
    [Serializable]
    public class SerialisableMesh
    {
        [Serializable]
        private struct SerialisableSubmeshDescriptor
        {
            public int indexCount;
            public int indexStart;
            public int vertexCount;
            public int baseVertex;
            public int firstVertex;
            public Bounds bounds;
            public MeshTopology topology;
            
            public SerialisableSubmeshDescriptor(SubMeshDescriptor descriptor)
            {
                indexCount = descriptor.indexCount;
                indexStart = descriptor.indexStart;
                vertexCount = descriptor.vertexCount;
                baseVertex = descriptor.baseVertex;
                firstVertex = descriptor.firstVertex;
                bounds = descriptor.bounds;
                topology = descriptor.topology;
            }

            public SubMeshDescriptor CreateSubmeshDescriptor()
            {
                return new SubMeshDescriptor
                {
                    indexCount = indexCount,
                    indexStart = indexStart,
                    vertexCount = vertexCount,
                    baseVertex = baseVertex,
                    firstVertex = firstVertex,
                    bounds = bounds,
                    topology = topology
                };
            }

            public static SubMeshDescriptor[] ConvertArray(SerialisableSubmeshDescriptor[] arr)
            {
                var result = new SubMeshDescriptor[arr.Length];
                for (var i = 0; i < arr.Length; i++)
                {
                    result[i] = arr[i].CreateSubmeshDescriptor();
                }

                return result;
            }
        }
        
        [SerializeField] private Vector3[] verts;
        [SerializeField] private int[] tris;
        [SerializeField] private Vector2[] uv;
        [SerializeField] private Vector2[] uv2;
        [SerializeField] private Vector3[] normals;
        [SerializeField] private SerialisableSubmeshDescriptor[] submeshes;

        public SerialisableMesh(Mesh mesh)
        {
            verts = mesh.vertices;
            tris = mesh.triangles;
            uv = mesh.uv;
            uv2 = mesh.uv2;
            normals = mesh.normals;
            submeshes = new SerialisableSubmeshDescriptor[mesh.subMeshCount];
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                submeshes[i] = new SerialisableSubmeshDescriptor(mesh.GetSubMesh(i));
            }
        }

        public Mesh CreateMesh()
        {
            var mesh = new Mesh
            {
                vertices = verts,
                triangles = tris,
                uv = uv,
                uv2 = uv2,
                normals = normals
            };
            mesh.SetSubMeshes(SerialisableSubmeshDescriptor.ConvertArray(submeshes));
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}