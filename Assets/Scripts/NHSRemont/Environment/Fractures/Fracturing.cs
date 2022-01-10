using System.Collections.Generic;
using System.Linq;
using NHSRemont.Environment.Fractures.NvBlast.Plugins;
using NHSRemont.Gameplay;
using UnityEngine;

namespace NHSRemont.Environment.Fractures
{
    public static class Fracturing
    {
        /// <summary>
        /// Creates a fractured version of this gameobject.
        /// </summary>
        /// <param name="gameObject">The gameobject to fracture</param>
        /// <param name="seed">Seed for fracturing RNG</param>
        /// <param name="totalChunks">Amount of segments to split this object into</param>
        /// <param name="material">Wall material to use for this object</param>
        /// <param name="category">The category that broken off chunks get registered to in the physics manager</param>
        /// <returns>The renderer of the chunks parent that contains all the resulting chunks</returns>
        public static FracturedRenderer FractureGameObject(GameObject gameObject, int seed, int totalChunks, WallMaterial material, PhysicsManager.PhysObjectType category)
        {
            var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            if(mesh.subMeshCount > 1)
                Debug.LogWarning("Mesh for fracturing has more than 1 submeshes!", gameObject);

            var verts = mesh.vertices;
            Vector3 scale = gameObject.transform.lossyScale;
            if (scale != Vector3.one)
            {
                for (var i = 0; i < verts.Length; i++)
                {
                    Vector3 vert = verts[i];
                    verts[i] = new Vector3(vert.x * scale.x, vert.y * scale.y, vert.z * scale.z);
                }
            }
            
            NvBlastExtUnity.setSeed(seed);

            var nvMesh = new NvMesh(
                verts,
                mesh.normals,
                mesh.uv,
                mesh.vertexCount,
                mesh.GetIndices(0),
                (int) mesh.GetIndexCount(0)
            );

            var meshes = FractureMeshesInNvBlast(totalChunks, nvMesh);

            // Build chunks gameobjects
            var chunkMass = (mesh.Volume() * scale.x * scale.y * scale.z) * material.density / totalChunks; //TODO per-chunk mass
            var chunks = BuildChunks(material.insideMaterial, material.outsideMaterial, meshes, chunkMass);
            
            var fractureGameObject = new GameObject(gameObject.name + " Fractured");
            fractureGameObject.transform.position = gameObject.transform.position;
            foreach (var chunk in chunks)
            {
                chunk.transform.SetParent(fractureGameObject.transform, false);
            }

            // Set up chunks
            foreach (ChunkNode chunk in chunks)
            {
                chunk.mass = chunkMass;
                chunk.breakOffImpulse = material.density * material.internalStrength;
                chunk.category = category;
            }

            //now we can rotate it since we're done with AABBs (this may be outdated in the current version of code with no BB anchoring idk lol)
            fractureGameObject.transform.rotation = gameObject.transform.rotation;

            FracturedRenderer fracturedRenderer = fractureGameObject.AddComponent<FracturedRenderer>();
            fracturedRenderer.Setup(chunks);
            
            return fracturedRenderer;
        }

        private static List<ChunkNode> BuildChunks(Material insideMaterial, Material outsideMaterial, List<Mesh> meshes, float chunkMass)
        {
            return meshes.Select((chunkMesh, i) =>
            {
                var chunk = BuildChunk(insideMaterial, outsideMaterial, chunkMesh, chunkMass);
                chunk.name += $" [{i}]";
                return chunk;
            }).ToList();
        }

        private static List<Mesh> FractureMeshesInNvBlast(int totalChunks, NvMesh nvMesh)
        {
            var fractureTool = new NvFractureTool();
            fractureTool.setRemoveIslands(false);
            fractureTool.setSourceMesh(nvMesh);
            var sites = new NvVoronoiSitesGenerator(nvMesh);
            sites.uniformlyGenerateSitesInMesh(totalChunks);
            fractureTool.voronoiFracturing(0, sites);
            fractureTool.finalizeFracturing();

            // Extract meshes
            var meshCount = fractureTool.getChunkCount();
            var meshes = new List<Mesh>(fractureTool.getChunkCount());
            for (var i = 1; i < meshCount; i++)
            {
                meshes.Add(ExtractChunkMesh(fractureTool, i));
            }

            return meshes;
        }

        private static Mesh ExtractChunkMesh(NvFractureTool fractureTool, int index)
        {
            var outside = fractureTool.getChunkMesh(index, false);
            var inside = fractureTool.getChunkMesh(index, true);
            var chunkMesh = outside.toUnityMesh();
            chunkMesh.subMeshCount = 2;
            chunkMesh.SetIndices(inside.getIndexes(), MeshTopology.Triangles, 1);
            return chunkMesh;
        }

        private static bool ValidateMesh(Mesh mesh)
        {
            if (mesh.isReadable == false)
            {
                Debug.LogError($"Mesh [{mesh}] has to be readable.");
                return false;
            }
            
            if (mesh.vertices == null || mesh.vertices.Length == 0)
            {
                Debug.LogError($"Mesh [{mesh}] does not have any vertices.");
                return false;
            }
            
            if (mesh.uv == null || mesh.uv.Length == 0)
            {
                Debug.LogError($"Mesh [{mesh}] does not have any uvs.");
                return false;
            }

            return true;
        }

        private static ChunkNode BuildChunk(Material insideMaterial, Material outsideMaterial, Mesh mesh, float mass)
        {
            var chunk = new GameObject($"Chunk");
            
            //fix mesh offset
            Vector3 offset = mesh.bounds.center;
            var verts = mesh.vertices;
            for (var i = 0; i < verts.Length; i++)
            {
                verts[i] -= offset;
            }
            mesh.SetVertices(verts);
            mesh.Optimize();
            mesh.RecalculateBounds();
            chunk.transform.position += offset;
            
            var meshFilter = chunk.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            
            var renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                outsideMaterial,
                insideMaterial
            };

            var mc = chunk.AddComponent<MeshCollider>();
            mc.convex = true;

            //chunk node
            var node = chunk.AddComponent<ChunkNode>();
            node.mass = mass;
            node.SetColliderAndSaveMesh(mc);
            
            return node;
        }
        
        public static void ConnectTouchingChunks(ChunkNode chunk, List<ChunkNode> chunks, float touchRadius = .03f)
        {
            var vertices = chunk.meshCollider.sharedMesh.vertices;
            Vector3 extents = chunk.meshCollider.sharedMesh.bounds.extents;
            
            foreach (ChunkNode other in chunks)
            {
                if(other == chunk) continue;

                Vector3 centresOffset = other.transform.position - chunk.transform.position;
                Vector3 maxOffsetPerAxis = other.meshCollider.sharedMesh.bounds.extents + extents;
                
                if(Mathf.Abs(centresOffset.x) > maxOffsetPerAxis.x 
                   || Mathf.Abs(centresOffset.y) > maxOffsetPerAxis.y
                   || Mathf.Abs(centresOffset.z) > maxOffsetPerAxis.z)
                    continue; //chunks too far apart

                for (var i = 0; i < vertices.Length; i++)
                {
                    var worldPosition = chunk.transform.TransformPoint(vertices[i]);
                    Vector3 closestPoint = other.collider.ClosestPoint(worldPosition);
                    if ((worldPosition - closestPoint).sqrMagnitude <= touchRadius * touchRadius)
                    {
                        chunk.AddNeighbour(other);
                        break;
                    }
                }
            }
            chunk.SaveNeighbours();
        }
    }
}