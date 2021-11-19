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
        /// <param name="anchor">Where should the anchor chunks be?</param>
        /// <param name="seed">Seed for fracturing RNG</param>
        /// <param name="totalChunks">Amount of segments to split this object into</param>
        /// <param name="insideMaterial">Material for the inside of this object</param>
        /// <param name="outsideMaterial">Material for the outside of this object</param>
        /// <param name="breakOffTolerance">Maximum impulse per unit mass that a chunk can withstand before being broken off</param>
        /// <param name="density">The density of this object, in kg/m^3</param>
        /// <param name="category">The category that broken off chunks get registered to in the physics manager</param>
        /// <returns>The chunk graph manager that contains all the resulting chunks</returns>
        public static ChunkGraphManager FractureGameObject(GameObject gameObject, Anchor anchor, int seed, int totalChunks,Material insideMaterial, Material outsideMaterial, float breakOffTolerance, float density, PhysicsManager.PhysObjectType category)
        {
            var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
            if(mesh.subMeshCount > 1)
                Debug.LogWarning("Mesh for fracturing has more than 1 submeshes!", gameObject);

            var verts = mesh.vertices;
            Vector3 scale = gameObject.transform.localScale;
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
            var chunkMass = (mesh.Volume() * scale.x * scale.y * scale.z) * density / totalChunks; //TODO per-chunk mass
            var chunks = BuildChunks(insideMaterial, outsideMaterial, meshes, chunkMass);
            
            var fractureGameObject = new GameObject(gameObject.name + " Fractured");
            fractureGameObject.transform.position = gameObject.transform.position;
            foreach (var chunk in chunks)
            {
                chunk.transform.SetParent(fractureGameObject.transform, false);
            }
            
            // Connect chunks that are touching
            foreach (var chunk in chunks)
            {
                ConnectTouchingChunks(chunk, chunks);
            }

            // Set up & anchor chunks
            foreach (ChunkNode chunk in chunks)
            {
                chunk.SetPhysicsDetails(breakOffTolerance, category);
            }
            Bounds bounds = new Bounds();
            foreach (Vector3 vert in verts)
            {
                bounds.Encapsulate(vert);
            }
            AnchorChunks(fractureGameObject.transform, bounds, chunks, anchor);
            
            //now we can rotate it since we're done with AABBs
            fractureGameObject.transform.rotation = gameObject.transform.rotation;

            // Graph manager freezes/unfreezes blocks depending on whether they are connected to the graph or not
            var graphManager = fractureGameObject.AddComponent<ChunkGraphManager>();
            graphManager.Setup(chunks);
            
            return graphManager;
        }

        private static void AnchorChunks(Transform root, Bounds bounds, List<ChunkNode> chunks, Anchor anchor)
        {
            var anchoredChunks = GetAnchoredColliders(anchor, root, chunks, bounds);
            
            foreach (var chunk in anchoredChunks)
            {
                chunk.isAnchor = true;
            }
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

        private static IEnumerable<ChunkNode> GetAnchoredColliders(Anchor anchor, Transform meshTransform, List<ChunkNode> chunks, Bounds bounds)
        {
            var anchoredChunks = new HashSet<ChunkNode>();
            var frameWidth = .01f;
            var meshWorldCenter = meshTransform.TransformPoint(bounds.center);
            var meshWorldExtents = bounds.extents.Multiply(meshTransform.lossyScale);
            
            foreach (ChunkNode chunk in chunks)
            {
                if (anchor.HasFlag(Anchor.Left))
                {
                    var center = meshWorldCenter - meshTransform.right * meshWorldExtents.x;
                    var halfExtents = meshWorldExtents.Abs().SetX(frameWidth);
                    if (DoesBoxOverlapChunk(chunk.collider, new Bounds(center, halfExtents * 2f)))
                        anchoredChunks.Add(chunk);
                }
                
                if (anchor.HasFlag(Anchor.Right))
                {
                    var center = meshWorldCenter + meshTransform.right * meshWorldExtents.x;
                    var halfExtents = meshWorldExtents.Abs().SetX(frameWidth);
                    if (DoesBoxOverlapChunk(chunk.collider, new Bounds(center, halfExtents * 2f)))
                        anchoredChunks.Add(chunk);
                }
                
                if (anchor.HasFlag(Anchor.Bottom))
                {
                    var center = meshWorldCenter - meshTransform.up * meshWorldExtents.y;
                    var halfExtents = meshWorldExtents.Abs().SetY(frameWidth);
                    if (DoesBoxOverlapChunk(chunk.collider, new Bounds(center, halfExtents * 2f)))
                        anchoredChunks.Add(chunk);
                }
                
                if (anchor.HasFlag(Anchor.Top))
                {
                    var center = meshWorldCenter + meshTransform.up * meshWorldExtents.y;
                    var halfExtents = meshWorldExtents.Abs().SetY(frameWidth);
                    if (DoesBoxOverlapChunk(chunk.collider, new Bounds(center, halfExtents * 2f)))
                        anchoredChunks.Add(chunk);
                }
                
                if (anchor.HasFlag(Anchor.Front))
                {
                    var center = meshWorldCenter - meshTransform.forward * meshWorldExtents.z;
                    var halfExtents = meshWorldExtents.Abs().SetZ(frameWidth);
                    if (DoesBoxOverlapChunk(chunk.collider, new Bounds(center, halfExtents * 2f)))
                        anchoredChunks.Add(chunk);
                }

                if (anchor.HasFlag(Anchor.Back))
                {
                    var center = meshWorldCenter + meshTransform.forward * meshWorldExtents.z;
                    var halfExtents = meshWorldExtents.Abs().SetZ(frameWidth);
                    if (DoesBoxOverlapChunk(chunk.collider, new Bounds(center, halfExtents * 2f)))
                        anchoredChunks.Add(chunk);
                }
            }
            
            return anchoredChunks;
            
            bool DoesBoxOverlapChunk(MeshCollider chunk, Bounds box)
            {
                var verts = chunk.sharedMesh.vertices;
                for (var i = 0; i < verts.Length; i++)
                {
                    var worldPosition = chunk.transform.TransformPoint(verts[i]);
                    if (box.Contains(worldPosition))
                        return true;
                }

                return false;
            }
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
            node.SetCollider(mc);
            
            return node;
        }
        
        private static void ConnectTouchingChunks(ChunkNode chunk, List<ChunkNode> chunks, float touchRadius = .03f)
        {
            var vertices = chunk.collider.sharedMesh.vertices;
            Vector3 extents = chunk.collider.sharedMesh.bounds.extents;
            
            foreach (ChunkNode other in chunks)
            {
                if(other == chunk) continue;

                Vector3 centresOffset = other.transform.position - chunk.transform.position;
                Vector3 maxOffsetPerAxis = other.collider.sharedMesh.bounds.extents + extents;
                
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