using System.Collections.Generic;
using UnityEngine;

namespace NHSRemont.Environment
{
    /// <summary>
    /// Makes this terrain react to explosions
    /// </summary>
    [RequireComponent(typeof(RuntimeTerrain), typeof(TreeOptimiser))]
    public class ReactiveTerrain : MonoBehaviour
    {
        private TerrainData terrainData;
        private TreeOptimiser treeOptimiser;
        private Vector3 terrainPos;
        private Vector3 terrainSize;

        [SerializeField, Tooltip("The layer to apply when an explosion chars the surrounding terrain")]
        private int terrainCharredLayer;
        [SerializeField, Tooltip("How powerful does an explosion have to be to fully char the terrain at the explosion's very centre?")]
        private float explosionPowerForFullyCharred = 6000f;
        private const float detailsRemovalDifficulty = 0.5f;

        [SerializeField]
        private AnimationCurve charringCurve =
            new AnimationCurve(new[] {new Keyframe(0f, 0f), new Keyframe(1f, 1f, 0f, 0f)});

        private void Start()
        {
            terrainData = GetComponent<Terrain>().terrainData;
            treeOptimiser = GetComponent<TreeOptimiser>();
            terrainPos = transform.position;
            terrainSize = terrainData.size;
            PhysicsManager.instance.onExplosion += OnExplosion;
        }

        private void OnDestroy()
        {
            PhysicsManager.instance.onExplosion -= OnExplosion;
        }

        public void OnExplosion(ExplosionInfo explosionInfo)
        {
            explosionInfo.ApplyToRigidbodies(treeOptimiser.DestroyTreesNear(explosionInfo.position, explosionInfo.blastRadius));
            ModifyTerrain(explosionInfo);
        }

        /// <summary>
        /// Modifies terrain as a result of an explosion. Does not affect trees.
        /// </summary>
        public void ModifyTerrain(ExplosionInfo explosionInfo)
        {
            DoCharring(explosionInfo);
            RemoveGrass(explosionInfo);
        }

        private void DoCharring(ExplosionInfo explosionInfo)
        {
            float maxCharring = explosionInfo.power / explosionPowerForFullyCharred;
            int layersCount = terrainData.terrainLayers.Length;
            var (min, max, coords) = GetMapCoordinatesInSphere(explosionInfo.position, explosionInfo.blastRadius,
                terrainData.alphamapWidth, terrainData.alphamapHeight);
            int deltaX = max.x - min.x;
            int deltaZ = max.z - min.z;
            if(deltaX < 0 || deltaZ < 0) return; //fully out of bounds

            float[,,] alphamap = terrainData.GetAlphamaps(min.x, min.z, deltaX+1, deltaZ + 1);
            foreach ((int x, int z, float sqrDist) coordAndDist in coords)
            {
                int x = coordAndDist.x - min.x;
                int z = coordAndDist.z - min.z;
                float dist = Mathf.Sqrt(coordAndDist.sqrDist);
                float factor = dist / explosionInfo.blastRadius;
                
                float previousCharring = alphamap[z, x, terrainCharredLayer]; //charring before this explosion
                if(previousCharring >= 1) continue;
                float charringAdded = Mathf.Lerp(maxCharring, 0f, factor); //amount of charring this explosion will cause at this coordinate
                charringAdded = Mathf.Clamp01(charringAdded);
                charringAdded = charringCurve.Evaluate(charringAdded);
                
                float totalCharring = alphamap[z, x, terrainCharredLayer] = Mathf.Clamp01(previousCharring + charringAdded); //add charring
                
                //calculate headroom for other layers
                float previousHeadroom = 1 - previousCharring;
                if(previousHeadroom == 0) continue;
                float headroom = 1 - totalCharring;
                float ratioToAdjustLayers = headroom / previousHeadroom;
                //weaken other layers
                for (int i = 0; i < layersCount; i++)
                {
                    if(i==terrainCharredLayer) continue;
                    alphamap[z, x, i] *= ratioToAdjustLayers;
                }
            }

            terrainData.SetAlphamaps(min.x, min.z, alphamap);
        }

        private void RemoveGrass(ExplosionInfo explosionInfo)
        {
            float maxDegrassing = explosionInfo.power / (explosionPowerForFullyCharred*detailsRemovalDifficulty);
            
            var (min, max, coords) = GetMapCoordinatesInSphere(explosionInfo.position, explosionInfo.blastRadius,
                terrainData.detailWidth, terrainData.detailHeight);
            int deltaX = max.x - min.x;
            int deltaZ = max.z - min.z;
            if(deltaX < 0 || deltaZ < 0) return; //fully out of bounds
            
            int[] supportedLayers = terrainData.GetSupportedLayers(min.x, min.z, deltaX + 1, deltaZ + 1);
            var detailMaps = new List<(int layer, int[,] details)>();
            foreach (int layer in supportedLayers)
            {
                detailMaps.Add((
                    layer,
                    terrainData.GetDetailLayer(min.x, min.z, deltaX + 1, deltaZ + 1, layer)
                    ));
            }
            foreach ((int x, int z, float sqrDist) coordAndDist in coords)
            {
                int x = coordAndDist.x - min.x;
                int z = coordAndDist.z - min.z;
                float dist = Mathf.Sqrt(coordAndDist.sqrDist);
                float factor = dist / explosionInfo.blastRadius;
                
                float degrassing = Mathf.Lerp(maxDegrassing, 0f, factor); //amount of degrassing this explosion should cause at this coordinate
                foreach (var detailMap in detailMaps)
                {
                    detailMap.details[z, x] = (int) Mathf.Clamp(detailMap.details[z, x] - (degrassing * 10), 0, 255);
                }
            }
            foreach (var detailMap in detailMaps)
            {
                terrainData.SetDetailLayer(min.x, min.z, detailMap.layer, detailMap.details);
            }
        }
        
        /// <summary>
        /// Gets all the map coordinates that are within a given radius of a point, accounting for height.
        /// </summary>
        /// <param name="centre">The centre of the sphere</param>
        /// <param name="radius">The radius of the sphere</param>
        /// <param name="mapWidth">The width of the map in question (heightmap, alphamap, etc)</param>
        /// <param name="mapHeight">The height of the map in question (heightmap, alphamap, etc)</param>
        /// <returns>A tuple containing:
        /// - The minimum coordinate (inclusive, greater than 0)
        /// - The maximum coordinate (inclusive, less than size)
        /// - A list of all points on the map that are contained within the sphere, as well as the distance squared from the centre to that point.</returns>
        private (
            (int x, int z) min,
            (int x, int z) max,
            List<(int x, int z, float sqrDistance)> coords) GetMapCoordinatesInSphere(Vector3 centre, float radius, int mapWidth, int mapHeight)
        {
            var min = WorldToMapSpace(new Vector3(centre.x - radius, centre.y - radius, centre.z - radius), mapWidth, mapHeight);
            var max = WorldToMapSpace(new Vector3(centre.x + radius, centre.y + radius, centre.z + radius), mapWidth, mapHeight);

            if (min.x < 0) min.x = 0;
            if (min.z < 0) min.z = 0;
            if (max.x >= mapWidth) max.x = mapWidth - 1;
            if (max.z >= mapHeight) max.z = mapHeight - 1;
            
            var result = new List<(int x, int z, float sqrDistance)>();
            for (int z = min.z; z <= max.z; z++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    Vector3 worldPoint = MapToWorldSpace((x, z), mapWidth, mapHeight);
                    float sqrDist = (worldPoint - centre).sqrMagnitude;
                    if (sqrDist <= radius * radius)
                    {
                        result.Add((x,z,sqrDist));
                    }
                }
            }

            return (min, max, result);
        }

        /// <summary>
        /// Transforms a coordinate from world space to the terrain's map space
        /// </summary>
        /// <returns>The position in alphamap space</returns>
        private (int x, int z) WorldToMapSpace(Vector3 point, int mapWidth, int mapHeight)
        {
            point -= terrainPos;
            int x = (int) ((point.x / terrainSize.x) * mapWidth);
            int z = (int) ((point.z / terrainSize.z) * mapHeight);
            return (x, z);
        }
        
        /// <summary>
        /// Transforms a coordinate from the terrain's map space to world space.
        /// Takes terrain height into account.
        /// </summary>
        private Vector3 MapToWorldSpace((int x, int z) mapPoint, int mapWidth, int mapHeight)
        {
            int heightmapX = (int) (((float)mapPoint.x / mapWidth)*terrainData.heightmapResolution);
            int heightmapZ = (int) (((float)mapPoint.z / mapHeight)*terrainData.heightmapResolution);

            (int x, int z) = mapPoint;
            Vector3 worldPoint = new Vector3(
                (x+0.5f) * (terrainSize.x / mapWidth),
                terrainData.GetHeight(heightmapX, heightmapZ),
                (z+0.5f) * (terrainSize.z / mapHeight));
            worldPoint += terrainPos;
            return worldPoint;
        }

        /// <summary>
        /// Returns if the coordinate is within the bounds of the map
        /// </summary>
        private bool IsCoordinateOnMap((int x, int z) coord, int mapWidth, int mapHeight)
        {
            (int x, int z) = coord;
            return x >= 0
                   && z >= 0
                   && x < mapWidth
                   && z < mapHeight;
        }

        /// <summary>
        /// Clamps a map coordinate to be within the bounds of the map
        /// </summary>
        /// <returns>The clamped coordinate</returns>
        private (int x, int z) ClampToMap((int x, int z) coord, int mapWidth, int mapHeight)
        {
            (int x, int z) = coord;
            if (x < 0) x = 0;
            else if (x >= mapWidth) x = mapWidth - 1;
            if (z < 0) z = 0;
            else if (z >= mapWidth) z = mapWidth - 1;

            return (x, z);
        }
    }
}