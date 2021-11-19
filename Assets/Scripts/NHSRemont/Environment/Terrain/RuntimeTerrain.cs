using UnityEngine;

namespace NHSRemont.Environment.Terrain
{
    /// <summary>
    /// Copies terrain data on awake so as to not mess up the terrain files after exiting play mode
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Terrain))]
    public class RuntimeTerrain : MonoBehaviour
    {
        private UnityEngine.Terrain terrain;
        
        private void Awake()
        {
            terrain = GetComponent<UnityEngine.Terrain>();
            terrain.terrainData = Instantiate(terrain.terrainData);
            GetComponent<TerrainCollider>().terrainData = terrain.terrainData;
        }
    }
}