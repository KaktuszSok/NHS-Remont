using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using NHSRemont.Gameplay;
using NHSRemont.Utility;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace NHSRemont.Environment.Terrain
{
    /// <summary>
    /// Converts a terrain's trees to interactable, instanced trees
    /// </summary>
    [RequireComponent(typeof(RuntimeTerrain))]
    public class TreeOptimiser : MonoBehaviour
    {
        private const int defaultTreeMass = 2250;
        
        [Header("Resources")]
        [SerializeField] private Material billboardMaterial;
        private Camera cam; //main camera
        private Transform camTransform; //main camera transform
        private UnityEngine.Terrain terrain;
        private Vector3 terrainPos;
        private TreeChunk[,,] treeChunks; //x,z,type
        private CommonlyNullCheckedArray3D<Transform> chunkGOs; //gameobjects corresponding to each tree chunk
        private ChunkGroup[,,] chunkGroups; //x/4, z/4, type

        [Header("Settings")]
        [SerializeField, Tooltip("Side-length of Tree Chunks in metres. Each chunk may render at most 1023 trees.")]
        private float treeChunkSize = 25;
        [SerializeField, Tooltip("Side-length of chunk groups, measured in chunks")]
        private int chunkGroupSize = 4;
        
        [SerializeField, Tooltip("Tree Chunks who's centre is closer to the camera than this distance (as manhattan distance) will be drawn as meshes")]
        private float meshDrawingMaxDistance = 150;
        [SerializeField, Tooltip("Tree Chunks who's centre is farther from the camera than this distance (as manhattan distance) will be drawn as billboards")]
        private float billboardingMinDistance = 150;
        [SerializeField, Tooltip("How often to check the distance between the camera and all tree chunks, to determine whether they should be drawn as billboards or not")]
        private float timeBetweenBillboardChecks = 0.25f;
        [SerializeField, Tooltip("Should billboard meshes be flat (false) or X-shaped (true)? Twice as many tris if X-shaped.")]
        private bool crossShapedBillboards = true;
        [SerializeField]
        private int billboardResolutionY = 128;
        [SerializeField]
        private FilterMode billboardFilterMode;
        
        private enum CollisionGenerationMode
        {
            ALL_ON_LOAD_DISTRIBUTED,
            ALL_ON_LOAD_INSTANT,
            WHEN_RB_NEAR,
            DONT
        }
        [SerializeField]
        private CollisionGenerationMode collisionGenerationMode;

        //Runtime
        private TerrainData originalTerrainData;
        private Transform fallingTreesParent;
        private int chunksX, chunksZ, typesCount;
        private float billboardCheckTime = 0f;
        //index = tree type:
        private Mesh[] billboardMeshes;
        private Material[] billboardMaterials;
        /// <summary>
        /// Bounding box size of each tree type at standard scale.
        /// </summary>
        private Vector3[] treeSizes;
        /// <summary>
        /// List of templates for tree collision. To use, instantiate one at the correct position and make sure to set it to active.
        /// </summary>
        private GameObject[] treeCollisionTemplates;
        /// <summary>
        /// Mass of each tree type at standard scale
        /// </summary>
        private float[] treeMasses;

        #region Initialisation
        
        private void Start()
        {
            billboardCheckTime = Random.Range(0f, timeBetweenBillboardChecks); //randomly offset billboard check time for each terrain
            cam = Camera.main;
            // ReSharper disable once PossibleNullReferenceException
            camTransform = cam.transform;
            terrain = GetComponent<UnityEngine.Terrain>();
            //copy terrain data so that we still have access to the original tree instances after they've been removed
            originalTerrainData = Instantiate(terrain.terrainData);
            //create parent object for falling trees
            fallingTreesParent = new GameObject("Falling Trees").transform;
            fallingTreesParent.SetParent(transform);
            //generate tree chunks
            RegenerateTreeChunks();
        }

        private void RegenerateTreeChunks()
        {
            terrainPos = terrain.GetPosition();
            var trees = originalTerrainData.treeInstances;
            var treeTypes = originalTerrainData.treePrototypes;
            billboardMeshes = new Mesh[treeTypes.Length];
            billboardMaterials = new Material[treeTypes.Length];
            treeSizes = new Vector3[treeTypes.Length];
            treeCollisionTemplates = new GameObject[treeTypes.Length];
            treeMasses = new float[treeTypes.Length];

            //clean up
            if (chunkGOs != null)
            {
                foreach (Transform chunkGO in chunkGOs)
                {
                    Destroy(chunkGO);
                }
            }

            //precalculate commonly used values
            Vector3 terrainSize = terrain.terrainData.size;
            chunksX = Mathf.CeilToInt(terrainSize.x / treeChunkSize);
            chunksZ = Mathf.CeilToInt(terrainSize.z / treeChunkSize);
            typesCount = treeTypes.Length;
            float relativeChunkSizeX = treeChunkSize / terrainSize.x;
            float relativeChunkSizeZ = treeChunkSize / terrainSize.z;
            
            //instantiate chunks array with appropriate size
            treeChunks = new TreeChunk[chunksX, chunksZ, typesCount];
            chunkGOs = new CommonlyNullCheckedArray3D<Transform>(chunksX, chunksZ, typesCount);
            for (int type = 0; type < treeTypes.Length; type++)
            {
                //calculate once the required data for this tree type
                //mesh & material
                Mesh treeMesh = treeTypes[type].prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                Material[] treeMaterials = treeTypes[type].prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;
                foreach (Material treeMaterial in treeMaterials)
                    treeMaterial.enableInstancing = true; //force enable instancing
                treeSizes[type] = treeMesh.bounds.size;

                //billboard
                Mesh billboardMesh = CreateBillboardMesh(treeMesh.bounds.size.x, treeMesh.bounds.size.y);
                billboardMeshes[type] = billboardMesh;
                float billboardAspectRatio = treeMesh.bounds.size.x / treeMesh.bounds.size.y;
                Texture2D billboardTexture =  BillboardGenerator.GenerateBillboard(treeTypes[type].prefab, (int) (billboardResolutionY*billboardAspectRatio), billboardResolutionY, billboardFilterMode);
                Material treeBillboardMaterial = new Material(billboardMaterial)
                {
                    mainTexture = billboardTexture
                }; //clone material but with different texture
                billboardMaterials[type] = treeBillboardMaterial;

                //physics
                treeCollisionTemplates[type] = GenerateTreeCollisionTemplate(type);
                Rigidbody rb = treeTypes[type].prefab.GetComponent<Rigidbody>();
                treeMasses[type] = rb != null ? rb.mass : defaultTreeMass;

                //create each tree chunk
                for (int z = 0; z < chunksZ; z++)
                {
                    for (int x = 0; x < chunksX; x++)
                    {
                        //bounding box in normalised terrain space
                        Rect boundingBox = Rect.MinMaxRect(
                            x*relativeChunkSizeX, z*relativeChunkSizeZ,
                            (x+1)*relativeChunkSizeX, (z+1)*relativeChunkSizeZ
                            );
                        treeChunks[x, z, type] = new TreeChunk(boundingBox, treeMesh, treeMaterials, billboardMesh, treeBillboardMaterial);
                    }
                }
            }
            
            //add trees to chunks
            foreach (TreeInstance tree in trees)
            {
                int chunkX = (int) (tree.position.x / relativeChunkSizeX);
                int chunkZ = (int) (tree.position.z / relativeChunkSizeZ);
                TreeChunk treeChunk = GetTreeChunk(chunkX, chunkZ, tree.prototypeIndex);
                treeChunk.AddTree(tree);
            }
            
            //update matrix arrays (and collision if set to instant)
            for (int type = 0; type < typesCount; type++)
            {
                for (int z = 0; z < chunksZ; z++)
                {
                    for (int x = 0; x < chunksX; x++)
                    {
                        treeChunks[x, z, type].UpdateMatrixArray(terrainPos, terrainSize);
                        if (collisionGenerationMode == CollisionGenerationMode.ALL_ON_LOAD_INSTANT)
                        {
                            UpdateChunkCollision(x, z, type);
                        }
                    }
                }
            }
            if (collisionGenerationMode == CollisionGenerationMode.ALL_ON_LOAD_DISTRIBUTED)
            {
                StartCoroutine(GenerateCollisionDistributed());
            }
            
            //generate groups
            int groupsX = Mathf.CeilToInt((float)chunksX / chunkGroupSize);
            int groupsZ = Mathf.CeilToInt((float) chunksZ / chunkGroupSize);
            chunkGroups = new ChunkGroup[groupsX, groupsZ, typesCount];
            for (int type = 0; type < typesCount; type++)
            {
                for (int z = 0; z < groupsZ; z++)
                {
                    for (int x = 0; x < groupsX; x++)
                    {
                        chunkGroups[x, z, type] = new ChunkGroup(
                            treeChunks, 
                            x * chunkGroupSize, z * chunkGroupSize, type,
                            chunkGroupSize
                            );
                        chunkGroups[x,z,type].UpdateCombinedMatrixArray();
                    }
                }
            }
            
            //make terrain not render trees
            terrain.terrainData.treeInstances = Array.Empty<TreeInstance>();
        }

        private IEnumerator GenerateCollisionDistributed()
        {
            //Debug.Log("starting distributed chunk collision generation...");
            YieldInstruction wait = new WaitForEndOfFrame();
            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int x = 0; x < chunksX; x++)
            {
                for (int z = 0; z < chunksZ; z++)
                {
                    for (int type = 0; type < typesCount; type++)
                    {
                        UpdateChunkCollision(x, z, type);
                        yield return wait;
                    }
                }
            }
            stopwatch.Stop();
            Debug.Log("distributed chunk collision generation complete! (" + stopwatch.Elapsed.TotalMilliseconds + "ms)");
        }

        private GameObject GenerateTreeCollisionTemplate(int type)
        {
            GameObject prefab = originalTerrainData.treePrototypes[type].prefab;
            List<GameObject> treeCollisionGOs = new List<GameObject>();
            if(prefab.GetComponent<Collider>() != null)
                Debug.LogWarning("All foliage colliders should be on the prefab's children!", prefab);
            foreach (Transform child in prefab.transform)
            {
                string childName = child.name.ToLowerInvariant();
                if (childName.Contains("collider") || childName.Contains("collision"))
                {
                    treeCollisionGOs.Add(child.gameObject);
                }
            }
            GameObject treeCollisionTemplate = new GameObject(prefab.name + " Collision");
            treeCollisionTemplate.transform.SetParent(transform);
            foreach (GameObject c in treeCollisionGOs)
            {
                GameObject collisionPart = Instantiate(c, treeCollisionTemplate.transform, false);
                collisionPart.name = c.name;
                collisionPart.AddComponent<TreeCollisionHandler>();
            }
            treeCollisionTemplate.transform.position = Vector3.one * -5000;
            treeCollisionTemplate.SetActive(false);
            return treeCollisionTemplate;
        }
        
        #endregion

        #region Update
        
        private void Update()
        {
            DrawAllChunks();
        }

        private void DrawAllChunks()
        {
            if (billboardCheckTime >= timeBetweenBillboardChecks)
            {
                billboardCheckTime = 0f;
                Vector3 camPosOnTerrainFloat = camTransform.position - terrainPos;
                int camPosOnTerrainX = (int) camPosOnTerrainFloat.x;
                int camPosOnTerrainZ = (int) camPosOnTerrainFloat.z;
                int chunkSizeInt = (int) treeChunkSize;
                int chunkCentreOffset = (int) treeChunkSize >> 1;
                
                foreach (ChunkGroup chunkGroup in chunkGroups)
                {
                    chunkGroup.allBillboard = true; //reset this value as we will set it to false later if necessary
                }
                
                for (int z = 0; z < chunksZ; z++)
                {
                    for (int x = 0; x < chunksX; x++)
                    {
                        //calculate distance to chunk
                        int chunkCentrePosOnTerrainX = chunkCentreOffset + x * chunkSizeInt;
                        int chunkCentrePosOnTerrainZ = chunkCentreOffset + z * chunkSizeInt;
                        int dx = chunkCentrePosOnTerrainX - camPosOnTerrainX;
                        int dy = chunkCentrePosOnTerrainZ - camPosOnTerrainZ;
                        int manhattanDist = Mathf.Abs(dx) + Math.Abs(dy);

                        for (int type = 0; type < typesCount; type++)
                        {
                            TreeChunk treeChunk = treeChunks[x, z, type];
                            treeChunk.drawAsMesh = manhattanDist <= meshDrawingMaxDistance;
                            if (treeChunk.drawAsMesh)
                            {
                                GetGroupAtChunk(x, z, type).allBillboard = false;
                            }

                            treeChunk.drawAsBillboard = manhattanDist > billboardingMinDistance;
                        }
                    }
                }
            }
            billboardCheckTime += Time.deltaTime;

            foreach (ChunkGroup chunkGroup in chunkGroups)
            {
                chunkGroup.Draw();
            }
        }

        private void FixedUpdate()
        {
            if (collisionGenerationMode == CollisionGenerationMode.WHEN_RB_NEAR
                || collisionGenerationMode == CollisionGenerationMode.ALL_ON_LOAD_DISTRIBUTED) //both the WHEN_RB_NEAR and ALL_ON_LOAD_DISTRIBUTED should load chunks for nearby rigidbodies.
            {
                var rbs = PhysicsManager.instance.GetAwakeRigidbodies();
                var chunksWithRBsNear = new Queue<(int x, int z)>();
                for(int i = 0; i < rbs.Count; i++)
                {
                    var chunkPos = GetChunkPosAt(rbs[i].position);
                    if (chunkPos.x < -1 || chunkPos.z < -1 || chunkPos.x > chunksX || chunkPos.z > chunksZ) continue;
                    AddPosIfValid(chunkPos);
                    AddPosIfValid((chunkPos.x + 1, chunkPos.z + 1));
                    AddPosIfValid((chunkPos.x + 1, chunkPos.z));
                    AddPosIfValid((chunkPos.x + 1, chunkPos.z - 1));
                    AddPosIfValid((chunkPos.x, chunkPos.z + 1));
                    AddPosIfValid((chunkPos.x, chunkPos.z - 1));
                    AddPosIfValid((chunkPos.x - 1, chunkPos.z + 1));
                    AddPosIfValid((chunkPos.x - 1, chunkPos.z));
                    AddPosIfValid((chunkPos.x - 1, chunkPos.z - 1));
                }
                void AddPosIfValid((int x, int z) chunkPos)
                {
                    if(chunkPos.x < 0 || chunkPos.z < 0 || chunkPos.x >= chunksX || chunkPos.z >= chunksZ) return;
                    chunksWithRBsNear.Enqueue(chunkPos);
                }
                
                foreach (var chunkPos in chunksWithRBsNear)
                {
                    if(chunkGOs.NotNull(chunkPos.x, chunkPos.z, 0)) continue;
                    for (int type = 0; type < typesCount; type++)
                    {
                        UpdateChunkCollision(chunkPos.x, chunkPos.z, type);
                    }
                }
            }
        }

        #endregion

        #region Modify Chunks

        /// <summary>
        /// Destroys all trees within a radius of a specific point
        /// </summary>
        /// <returns>List of rigidbodies, one for each falling tree</returns>
        public List<Rigidbody> DestroyTreesNear(Vector3 point, float radius, bool spawnFallingTrees = true, Predicate<TreeInstance> shouldKnockOver = null)
        {
            //calculate bounds to search in chunk- and group-coordinates
            CalculateChunkAndGroupSearchBounds(point, radius,
                out (int x, int z) chunkPosMin,
                out (int x, int z) chunkPosMax,
                out (int x, int z) groupPosMin,
                out (int x, int z) groupPosMax);

            //get bounding box local to normalised terrain space
            Vector3 terrainSize = terrain.terrainData.size;
            point -= terrainPos;
            Rect boundingBoxLocal = Rect.MinMaxRect(
                (point.x - radius) / terrainSize.x, (point.z - radius) / terrainSize.z,
                (point.x + radius) / terrainSize.x, (point.z + radius) / terrainSize.z
            );

            //set up predicate if it's null
            if (shouldKnockOver == null)
            {
                float sqrRadius = radius * radius;
                //get tree position in world space (relative to terrain position, as our point is now also relative to terrain position) and compare distance to point against radius
                shouldKnockOver = tree => (LocalToTerrainPos(tree.position) - point).sqrMagnitude <= sqrRadius;
            }

            List<Rigidbody> fallingTrees = new List<Rigidbody>();
            //loop chunks
            for (int type = 0; type < typesCount; type++)
            {
                for (int z = chunkPosMin.z; z <= chunkPosMax.z; z++)
                {
                    for (int x = chunkPosMin.x; x <= chunkPosMax.x; x++)
                    {
                        TreeChunk treeChunk = GetTreeChunk(x, z, type);
                        var nearbyTrees = treeChunk.GetTreesWithinBounds(boundingBoxLocal);
                        int amt = nearbyTrees.Count;
                        for (int i = 0; i < amt; i++) //loop through all trees
                        {
                            (TreeInstance treeInstance, Vector2 position) = nearbyTrees[i];
                            if (shouldKnockOver(treeInstance)) //check predicate
                            {
                                Maybe<TreeInstance> tree = treeChunk.RemoveTree(position);
                                if (spawnFallingTrees && tree.hasValue)
                                {
                                    fallingTrees.Add(SpawnFallingTree(tree.value));
                                }
                            }
                        }
                        treeChunk.UpdateMatrixArray(terrainPos, terrainSize);
                        UpdateChunkCollision(x,z,type);
                    }
                }
            }
            
            //loop groups
            for (int type = 0; type < typesCount; type++)
            {
                for (int z = groupPosMin.z; z <= groupPosMax.z; z++)
                {
                    for (int x = groupPosMin.x; x <= groupPosMax.x; x++)
                    {
                        chunkGroups[x,z,type].UpdateCombinedMatrixArray();
                    }   
                }
            }

            PhysicsManager.instance.RegisterRigidbodies(fallingTrees, PhysicsManager.PhysObjectType.DEBRIS_LARGE);
            return fallingTrees;
        }

        /// <summary>
        /// Knocks over trees near the explosion, if it is strong enough
        /// </summary>
        /// <returns>A list of rigidbodies, one for each falling tree</returns>
        public List<Rigidbody> ApplyExplosionToNearbyTrees(ExplosionInfo explosionInfo)
        {
            var fallingTrees = DestroyTreesNear(explosionInfo.position, explosionInfo.blastRadius*1.3f, true,
                tree =>
                    TreeCollisionHandler.DoesExplosionFellTree(
                        explosionInfo, 
                        LocalToWorldPos(tree.position),
                        treeSizes[tree.prototypeIndex].y*tree.heightScale,
                        CalculateTreeMass(tree)
                    )
                );
            explosionInfo.ApplyToRigidbodies(fallingTrees);
            return fallingTrees;
        }

        /// <summary>
        /// Creates a new falling tree rigidbody using the given tree instance's data
        /// </summary>
        /// <param name="tree">The tree instance to base our new gameobject off of</param>
        /// <returns>The spawned falling tree</returns>
        private Rigidbody SpawnFallingTree(TreeInstance tree)
        {
            Vector3 pos = LocalToWorldPos(tree.position);
            GameObject prefab = originalTerrainData.treePrototypes[tree.prototypeIndex].prefab;
            GameObject fallingTree = Instantiate(prefab,
                pos, Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f), fallingTreesParent);
            
            Rigidbody rb = fallingTree.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = fallingTree.AddComponent<Rigidbody>();
                rb.mass = CalculateTreeMass(tree);
                rb.drag = rb.angularDrag = 0.1f;
            }
            //adjust inertia tensor to fit triangular tree shape
            Vector3 tensorShape = treeSizes[tree.prototypeIndex];
            tensorShape.y *= tree.heightScale;
            tensorShape.x *= tree.widthScale*0.5f;
            tensorShape.z *= tree.widthScale*0.5f;
            tensorShape.Normalize();
            rb.inertiaTensor = rb.inertiaTensor.magnitude * tensorShape;

            LODGroup lod = fallingTree.GetComponent<LODGroup>();
            if (lod == null)
            {
                lod = fallingTree.AddComponent<LODGroup>();
                Renderer meshRend = fallingTree.GetComponent<Renderer>();
                GameObject billboardGO = new GameObject("LOD1");
                billboardGO.transform.SetParent(fallingTree.transform, false);
                billboardGO.AddComponent<MeshFilter>().mesh = billboardMeshes[tree.prototypeIndex];
                Renderer billboardRend = billboardGO.AddComponent<MeshRenderer>();
                billboardRend.material = billboardMaterials[tree.prototypeIndex];
                LOD[] lods =
                {
                    new LOD(0.11f, new []{meshRend}),
                    new LOD(0f, new []{billboardRend})
                };
                lod.SetLODs(lods);
            }
            
            return rb;
        }

        #endregion

        #region Update Chunks
        
        private void UpdateChunkMatrices(int chunkX, int chunkZ, int type)
        {
            GetTreeChunk(chunkX, chunkZ, type).UpdateMatrixArray(terrainPos, terrain.terrainData.size);
            GetGroupAtChunk(chunkX, chunkZ, type).UpdateCombinedMatrixArray();
        }

        [SuppressMessage("ReSharper", "LocalVariableHidesMember")]
        private void UpdateChunkCollision(int chunkX, int chunkZ, int type)
        {
            if(collisionGenerationMode == CollisionGenerationMode.DONT) return;
            
            Transform chunkGO;
            if (chunkGOs.NotNull(chunkX, chunkZ, type))
            {
                chunkGO = chunkGOs[chunkX, chunkZ, type];
            }
            else //create new chunk GO
            {
                chunkGO = chunkGOs[chunkX, chunkZ, type] = new GameObject("Tree Chunk (" + chunkX + "," + chunkZ + ",type=" + type + ")").transform;
                
                chunkGO.SetParent(transform);
                chunkGO.localPosition = new Vector3((chunkX + 0.5f) * treeChunkSize, 0f, (chunkZ + 0.5f) * treeChunkSize);
            }

            var trees = GetTreeChunk(chunkX, chunkZ, type).GetAllTrees();
            int existingChildCount = chunkGO.childCount;
            for (var i = 0; i < trees.Count || i < existingChildCount; i++)
            {
                Transform collisionObj;
                if (i < existingChildCount) //re-use tree collision GOs
                {
                    collisionObj = chunkGO.GetChild(i);
                    if (i >= trees.Count) //too many children for the amount of trees!
                    {
                        if (collisionObj.gameObject.activeSelf) //disable object
                        {
                            collisionObj.gameObject.SetActive(false);
                            continue;
                        }
                        //object is already disabled - this means rest of children should also be disabled
                        break;
                    }
                    collisionObj.gameObject.SetActive(true);
                    TreeCollisionHandler.SetTreeMass(collisionObj, CalculateTreeMass(trees[i])); //this gameobject may no longer represent the same tree - re-set mass.
                }
                else //otherwise, must create new GOs
                {
                    collisionObj = Instantiate(treeCollisionTemplates[type], chunkGO).transform;
                    collisionObj.name = treeCollisionTemplates[type].name;
                    collisionObj.gameObject.SetActive(true);
                    TreeCollisionHandler.MakeTreeFellableByImpact(collisionObj, this, CalculateTreeMass(trees[i]));
                }

                collisionObj.transform.position = LocalToWorldPos(trees[i].position);
                collisionObj.eulerAngles = new Vector3(0f, trees[i].rotation * Mathf.Rad2Deg, 0f);
                collisionObj.localScale = new Vector3(trees[i].widthScale, trees[i].heightScale, trees[i].widthScale);
            }
        }
        
        #endregion

        #region Utility
        
        private TreeChunk GetTreeChunk(int chunkX, int chunkZ, int type)
        {
            return treeChunks[chunkX, chunkZ, type];
        }

        private ChunkGroup GetGroupAtChunk(int chunkX, int chunkZ, int type)
        {
            return chunkGroups[chunkX / chunkGroupSize, chunkZ / chunkGroupSize, type];
        }

        private (int x, int z) GetChunkPosAt(Vector3 worldPos)
        {
            worldPos -= terrainPos;
            return ((int)(worldPos.x / treeChunkSize), (int)(worldPos.z / treeChunkSize));
        }

        private (int x, int z) GetGroupPosAt(Vector3 worldPos)
        {
            worldPos -= terrainPos;
            return ((int)(worldPos.x / (chunkGroupSize*treeChunkSize)), (int)(worldPos.z / (chunkGroupSize*treeChunkSize)));
        }

        /// <summary>
        /// Converts local position to world position
        /// </summary>
        /// <param name="localPos">Position in normalised terrain XYZ space</param>
        /// <returns>Position in world space</returns>
        private Vector3 LocalToWorldPos(Vector3 localPos)
        {
            return LocalToTerrainPos(localPos) + terrainPos;
        }
        
        /// <summary>
        /// Converts local position to a world position, relative to the terrain's position
        /// </summary>
        /// <param name="localPos">Position in normalised terrain XYZ space</param>
        /// <returns>Position in world space, relative to terrain position</returns>
        private Vector3 LocalToTerrainPos(Vector3 localPos)
        {
            Vector3 terrainSize = terrain.terrainData.size;
            Vector3 treeWorldPos = localPos;
            treeWorldPos.x *= terrainSize.x;
            treeWorldPos.y *= terrainSize.y;
            treeWorldPos.z *= terrainSize.z;
            return treeWorldPos;
        }
        
        /// <summary>
        /// Calculates the search bounds for chunks and groups within a radius of a specified point.
        /// Looping from minimum to maximum will never cause index OOB exceptions.
        /// </summary>
        private void CalculateChunkAndGroupSearchBounds(Vector3 point, float radius, out (int x, int z) chunkPosMin,
            out (int x, int z) chunkPosMax, out (int x, int z) groupPosMin, out (int x, int z) groupPosMax)
        {
            Vector3 minPoint = new Vector3(point.x - radius, point.y, point.z - radius);
            Vector3 maxPoint = new Vector3(point.x + radius, point.y, point.z + radius);
            chunkPosMin = GetChunkPosAt(minPoint);
            chunkPosMax = GetChunkPosAt(maxPoint);
            groupPosMin = GetGroupPosAt(minPoint);
            groupPosMax = GetGroupPosAt(maxPoint);

            chunkPosMin.x = Math.Max(0, chunkPosMin.x);
            chunkPosMin.z = Math.Max(0, chunkPosMin.z);
            chunkPosMax.x = Math.Min(chunksX - 1, chunkPosMax.x);
            chunkPosMax.z = Math.Min(chunksZ - 1, chunkPosMax.z);

            groupPosMin.x = Math.Max(0, groupPosMin.x);
            groupPosMin.z = Math.Max(0, groupPosMin.z);
            groupPosMax.x = Math.Min(chunkGroups.GetLength(0) - 1, groupPosMax.x);
            groupPosMax.z = Math.Min(chunkGroups.GetLength(1) - 1, groupPosMax.z);
        }

        private float CalculateTreeMass(TreeInstance tree)
        {
            return treeMasses[tree.prototypeIndex] * (tree.widthScale * tree.heightScale * tree.widthScale);
        }

        private Mesh CreateBillboardMesh(float width, float height)
        {
            return crossShapedBillboards ? CreateXQuads() : CreateQuad();
            
            Mesh CreateQuad()
            {
                Mesh mesh = new Mesh();
                Vector3[] verts = new Vector3[]
                {
                    new Vector3(-width / 2, 0, 0),
                    new Vector3(width / 2, 0, 0),
                    new Vector3(-width / 2, height, 0),
                    new Vector3(width / 2, height, 0)
                };
                mesh.vertices = verts;
                
                int[] tris = new int[]
                {
                    0, 2, 1,
                    2, 3, 1
                };
                mesh.triangles = tris;

                Vector3[] normals = new Vector3[]
                {
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward
                };
                mesh.normals = normals;

                Vector2[] uvs = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1)
                };
                mesh.uv = uvs;

                return mesh;
            }

            Mesh CreateXQuads()
            {
                Mesh mesh = new Mesh();
                Vector3[] verts = new Vector3[]
                {
                    new Vector3(-width / 2, 0, 0),
                    new Vector3(width / 2, 0, 0),
                    new Vector3(-width / 2, height, 0),
                    new Vector3(width / 2, height, 0),
                    
                    new Vector3(0, 0, -width / 2),
                    new Vector3(0, 0, width / 2),
                    new Vector3(0, height, -width / 2),
                    new Vector3(0, height, width / 2)
                };
                mesh.vertices = verts;
                
                int[] tris = new int[]
                {
                    0, 2, 1,
                    2, 3, 1,
                    
                    4, 6, 5,
                    6, 7, 5,
                };
                mesh.triangles = tris;

                Vector3[] normals = new Vector3[]
                {
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward,
                    -Vector3.forward,
                    
                    -Vector3.right,
                    -Vector3.right,
                    -Vector3.right,
                    -Vector3.right
                };
                mesh.normals = normals;

                Vector2[] uvs = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1),
                    
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(1, 1)
                };
                mesh.uv = uvs;

                return mesh;
            }
        }
        
        #endregion

        #region ChunkGroup
        
        /// <summary>
        /// A small group of tree chunks, which may be drawn as one billboard (if all chunks are billboarded)
        /// </summary>
        private class ChunkGroup
        {
            private readonly TreeChunk[,] chunks;
            private Matrix4x4[] combinedMatrix;
            
            public bool allBillboard = true;
            
            public ChunkGroup(TreeChunk[,,] allChunks, int startX, int startZ, int treeType, int groupSize)
            {
                int sizeX = Math.Min(groupSize, allChunks.GetLength(0) - startX);
                int sizeZ = Math.Min(groupSize, allChunks.GetLength(1) - startZ);

                //fill our chunk array with select chunks
                chunks = new TreeChunk[sizeX, sizeZ];
                for (int z = 0; z < sizeZ; z++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        chunks[x, z] = allChunks[startX + x, startZ + z, treeType];
                    }
                }
            }

            /// <summary>
            /// Updates the matrix array which is the union of all this group's chunks' matrix arrays
            /// </summary>
            public void UpdateCombinedMatrixArray()
            {
                //calculate total size
                int totalSize = 0;
                foreach (TreeChunk treeChunk in chunks)
                {
                    totalSize += treeChunk.MatricesCount();
                }

                if (totalSize > 1023)
                {
                    Debug.LogError("Tree Chunk Group has too many trees (" + totalSize + ")! Try lowering the group size.");
                }
                
                //create combined array
                combinedMatrix = new Matrix4x4[totalSize];
                int i = 0;
                foreach (TreeChunk treeChunk in chunks)
                {
                    foreach (Matrix4x4 treeMatrix in treeChunk.matrices)
                    {
                        combinedMatrix[i] = treeMatrix;
                        i++;
                    }
                }
            }
            
            public void Draw()
            {
                if (allBillboard)
                {
                    Graphics.DrawMeshInstanced(chunks[0,0].billboardMesh, 0, chunks[0,0].billboardMaterial, combinedMatrix);
                }
                else
                {
                    foreach (TreeChunk treeChunk in chunks)
                    {
                        treeChunk.DrawTrees();
                    }
                }
            }
        }
        
        #endregion
    }
}